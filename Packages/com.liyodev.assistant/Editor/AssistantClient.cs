using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Liyodev.Assistant
{
    /// Un turno de conversación entre el usuario y el asistente.
    public readonly struct AssistantTurn
    {
        public readonly bool FromUser;
        public readonly string Text;
        public AssistantTurn(bool fromUser, string text) { FromUser = fromUser; Text = text; }
    }

    /// Habla con el servicio elegido en los ajustes y devuelve la respuesta como texto.
    public static class AssistantClient
    {
        // Si quien llama no da una sesión (el panel sí la da), se usa una estable por editor.
        private static readonly string s_fallbackOpenCodeSession = Guid.NewGuid().ToString("N");

        public static void Send(string system, IReadOnlyList<AssistantTurn> turns,
            Action<string> onAnswer, Action<string> onError, string openCodeSessionId = null)
        {
            var p = AssistantSettings.Current;
            turns = Recientes(turns);
            if (AssistantSettings.IsAccount(p))
            {
                AssistantCli.Send(p, system, turns, onAnswer, onError);
                return;
            }

            string key = AssistantSettings.Key(p);
            string model = AssistantSettings.Model(p);
            UnityWebRequest request;
            JObject body;

            switch (p)
            {
                case AssistantSettings.Provider.Anthropic:
                    body = new JObject
                    {
                        ["model"] = model,
                        ["max_tokens"] = 4096,
                        ["system"] = system,
                        ["messages"] = Messages(turns, "assistant", false),
                    };
                    request = Post("https://api.anthropic.com/v1/messages", body);
                    request.SetRequestHeader("x-api-key", key);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    break;
                case AssistantSettings.Provider.Gemini:
                    var contents = new JArray();
                    foreach (var t in turns)
                        contents.Add(new JObject
                        {
                            ["role"] = t.FromUser ? "user" : "model",
                            ["parts"] = new JArray { new JObject { ["text"] = t.Text } },
                        });
                    body = new JObject
                    {
                        ["systemInstruction"] = new JObject
                        {
                            ["parts"] = new JArray { new JObject { ["text"] = system } },
                        },
                        ["contents"] = contents,
                    };
                    request = Post($"https://generativelanguage.googleapis.com/v1beta/models/{UnityWebRequest.EscapeURL(model)}:generateContent", body);
                    request.SetRequestHeader("x-goog-api-key", key);
                    break;
                default:
                    string address = p switch
                    {
                        AssistantSettings.Provider.OpenAI => "https://api.openai.com/v1",
                        AssistantSettings.Provider.OpenCode => AssistantSettings.OpenCodeBaseUrl,
                        AssistantSettings.Provider.Ollama => AssistantSettings.OllamaBaseUrl,
                        _ => AssistantSettings.Address.TrimEnd('/'),
                    };
                    body = new JObject { ["model"] = model, ["messages"] = Messages(turns, "assistant", true, system) };
                    request = Post(address + "/chat/completions", body);
                    if (!string.IsNullOrEmpty(key)) request.SetRequestHeader("Authorization", "Bearer " + key);
                    if (p == AssistantSettings.Provider.OpenCode ||
                        address.Contains("opencode.ai", StringComparison.OrdinalIgnoreCase))
                    {
                        request.SetRequestHeader("User-Agent", "LiyodevAssistant/0.1.0");
                        string sesion = string.IsNullOrEmpty(openCodeSessionId)
                            ? s_fallbackOpenCodeSession
                            : openCodeSessionId;
                        request.SetRequestHeader("x-opencode-session", sesion);
                    }
                    break;
            }

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                        onError?.Invoke($"{request.error} {request.downloadHandler?.text}");
                    else
                        onAnswer?.Invoke(ReadAnswer(p, JObject.Parse(request.downloadHandler.text)));
                }
                catch (Exception e) { onError?.Invoke(e.Message); }
                finally { request.Dispose(); }
            };
        }

        /// Cuántos turnos de la conversación se mandan. El estado y el manual ya van en las
        /// instrucciones de cada petición; los turnos viejos solo gastan.
        public const int TurnosEnviados = 8;

        static IReadOnlyList<AssistantTurn> Recientes(IReadOnlyList<AssistantTurn> turns)
        {
            if (turns == null || turns.Count <= TurnosEnviados) return turns;
            var recientes = new List<AssistantTurn>(TurnosEnviados);
            int desde = turns.Count - TurnosEnviados;
            // Que empiece por un turno del usuario, como piden las API.
            if (!turns[desde].FromUser && desde + 1 < turns.Count) desde++;
            for (int i = desde; i < turns.Count; i++) recientes.Add(turns[i]);
            return recientes;
        }

        static string ReadAnswer(AssistantSettings.Provider p, JObject answer)
        {
            var sb = new StringBuilder();
            switch (p)
            {
                case AssistantSettings.Provider.Anthropic:
                    foreach (var block in answer["content"] ?? new JArray())
                        if ((string)block["type"] == "text") sb.Append((string)block["text"]);
                    break;
                case AssistantSettings.Provider.Gemini:
                    foreach (var part in answer.SelectToken("candidates[0].content.parts") ?? new JArray())
                        sb.Append((string)part["text"]);
                    break;
                default:
                    sb.Append((string)answer.SelectToken("choices[0].message.content"));
                    break;
            }
            return sb.ToString();
        }

        static JArray Messages(IReadOnlyList<AssistantTurn> turns, string assistantRole, bool withSystem, string system = null)
        {
            var list = new JArray();
            if (withSystem) list.Add(new JObject { ["role"] = "system", ["content"] = system });
            foreach (var t in turns)
                list.Add(new JObject { ["role"] = t.FromUser ? "user" : assistantRole, ["content"] = t.Text });
            return list;
        }

        static UnityWebRequest Post(string url, JObject body)
        {
            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString())),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("content-type", "application/json");
            return request;
        }
    }
}
