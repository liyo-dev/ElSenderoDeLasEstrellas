using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.Networking;

namespace NarrativeGraphEditor.Story
{
    /// Which service answers the assistant, and how to talk to it. Every user picks their own;
    /// settings are kept only on this computer.
    public static class StoryAssistantSettings
    {
        public enum Provider { Anthropic, OpenAI, Gemini, Compatible }

        const string Prefix = "NarrativeGraph.Assistant.";
        public static event Action Changed;

        public static bool Visible
        {
            get => EditorPrefs.GetBool(Prefix + "Visible", false);
            set { EditorPrefs.SetBool(Prefix + "Visible", value); Changed?.Invoke(); }
        }

        public static Provider Current
        {
            get => (Provider)EditorPrefs.GetInt(Prefix + "Provider", 0);
            set { EditorPrefs.SetInt(Prefix + "Provider", (int)value); Changed?.Invoke(); }
        }

        public static string Key(Provider p) => EditorPrefs.GetString(Prefix + p + ".Key", string.Empty);
        public static void SetKey(Provider p, string key) { EditorPrefs.SetString(Prefix + p + ".Key", key?.Trim() ?? string.Empty); Changed?.Invoke(); }

        public static string Model(Provider p) => EditorPrefs.GetString(Prefix + p + ".Model", DefaultModel(p));
        public static void SetModel(Provider p, string model) { EditorPrefs.SetString(Prefix + p + ".Model", model?.Trim() ?? string.Empty); Changed?.Invoke(); }

        /// Only for "Compatible": the address of a service that speaks the OpenAI chat format
        /// (Ollama, LM Studio, OpenRouter, Mistral, Groq...).
        public static string Address
        {
            get => EditorPrefs.GetString(Prefix + "Address", "http://localhost:11434/v1");
            set { EditorPrefs.SetString(Prefix + "Address", value?.Trim() ?? string.Empty); Changed?.Invoke(); }
        }

        public static string Name(Provider p) => p switch
        {
            Provider.Anthropic => "Anthropic (Claude)",
            Provider.OpenAI => "OpenAI (ChatGPT)",
            Provider.Gemini => "Google (Gemini)",
            _ => StoryText.T("Compatible con OpenAI (Ollama, LM Studio, OpenRouter…)", "OpenAI-compatible (Ollama, LM Studio, OpenRouter…)"),
        };

        public static string DefaultModel(Provider p) => p switch
        {
            Provider.Anthropic => "claude-sonnet-4-5",
            Provider.OpenAI => "gpt-4.1",
            Provider.Gemini => "gemini-2.5-flash",
            _ => "llama3.1",
        };

        /// A local service may need no key; the others do.
        public static bool IsReady => Current == Provider.Compatible ? !string.IsNullOrEmpty(Address) : !string.IsNullOrEmpty(Key(Current));
    }

    /// Sends a conversation to the chosen service and returns the answer as text.
    public static class StoryAssistantClient
    {
        public readonly struct Turn
        {
            public readonly bool FromUser;
            public readonly string Text;
            public Turn(bool fromUser, string text) { FromUser = fromUser; Text = text; }
        }

        public static void Send(string system, IReadOnlyList<Turn> turns, Action<string> onAnswer, Action<string> onError)
        {
            var p = StoryAssistantSettings.Current;
            string key = StoryAssistantSettings.Key(p);
            string model = StoryAssistantSettings.Model(p);
            UnityWebRequest request;
            JObject body;

            switch (p)
            {
                case StoryAssistantSettings.Provider.Anthropic:
                    body = new JObject { ["model"] = model, ["max_tokens"] = 4096, ["system"] = system, ["messages"] = Messages(turns, "assistant", false) };
                    request = Post("https://api.anthropic.com/v1/messages", body);
                    request.SetRequestHeader("x-api-key", key);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    break;
                case StoryAssistantSettings.Provider.Gemini:
                    var contents = new JArray();
                    foreach (var t in turns)
                        contents.Add(new JObject { ["role"] = t.FromUser ? "user" : "model", ["parts"] = new JArray { new JObject { ["text"] = t.Text } } });
                    body = new JObject
                    {
                        ["systemInstruction"] = new JObject { ["parts"] = new JArray { new JObject { ["text"] = system } } },
                        ["contents"] = contents,
                    };
                    request = Post($"https://generativelanguage.googleapis.com/v1beta/models/{UnityWebRequest.EscapeURL(model)}:generateContent", body);
                    request.SetRequestHeader("x-goog-api-key", key);
                    break;
                default:
                    string address = p == StoryAssistantSettings.Provider.OpenAI ? "https://api.openai.com/v1" : StoryAssistantSettings.Address.TrimEnd('/');
                    body = new JObject { ["model"] = model, ["messages"] = Messages(turns, "assistant", true, system) };
                    request = Post(address + "/chat/completions", body);
                    if (!string.IsNullOrEmpty(key)) request.SetRequestHeader("Authorization", "Bearer " + key);
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

        static string ReadAnswer(StoryAssistantSettings.Provider p, JObject answer)
        {
            var sb = new StringBuilder();
            switch (p)
            {
                case StoryAssistantSettings.Provider.Anthropic:
                    foreach (var block in answer["content"] ?? new JArray())
                        if ((string)block["type"] == "text") sb.Append((string)block["text"]);
                    break;
                case StoryAssistantSettings.Provider.Gemini:
                    foreach (var part in answer.SelectToken("candidates[0].content.parts") ?? new JArray())
                        sb.Append((string)part["text"]);
                    break;
                default:
                    sb.Append((string)answer.SelectToken("choices[0].message.content"));
                    break;
            }
            return sb.ToString();
        }

        static JArray Messages(IReadOnlyList<Turn> turns, string assistantRole, bool withSystem, string system = null)
        {
            var list = new JArray();
            if (withSystem) list.Add(new JObject { ["role"] = "system", ["content"] = system });
            foreach (var t in turns) list.Add(new JObject { ["role"] = t.FromUser ? "user" : assistantRole, ["content"] = t.Text });
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
