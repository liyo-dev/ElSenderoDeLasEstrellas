using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Liyodev.Assistant
{
    /// Lee los modelos instalados en Ollama por su API compatible con OpenAI. Se usa desde los
    /// ajustes para no obligar a escribir el nombre a mano.
    public static class AssistantOllama
    {
        public static void ListarModelos(Action<List<string>> onOk, Action<string> onError)
        {
            var request = UnityWebRequest.Get(AssistantSettings.OllamaBaseUrl.TrimEnd('/') + "/models");
            request.timeout = 5;
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke(request.error);
                        return;
                    }

                    var json = JObject.Parse(request.downloadHandler.text);
                    var modelos = new List<string>();
                    foreach (var item in json["data"] ?? new JArray())
                    {
                        string id = (string)item["id"];
                        if (!string.IsNullOrEmpty(id) && !modelos.Contains(id)) modelos.Add(id);
                    }
                    onOk?.Invoke(modelos);
                }
                catch (Exception e) { onError?.Invoke(e.Message); }
                finally { request.Dispose(); }
            };
        }
    }
}
