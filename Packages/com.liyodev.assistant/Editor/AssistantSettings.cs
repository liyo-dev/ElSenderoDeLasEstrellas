using System;
using UnityEditor;

namespace Liyodev.Assistant
{
    /// Qué servicio responde al asistente y con qué clave. Es el mismo ajuste para todas las
    /// herramientas: una sola clave, un solo modelo, un solo sitio donde cambiarlo.
    public static class AssistantSettings
    {
        public enum Provider { Anthropic, OpenAI, Gemini, Compatible, OpenCode, Ollama }

        /// La API de inferencia de OpenCode habla el formato de OpenAI.
        public const string OpenCodeBaseUrl = "https://opencode.ai/inference/openai/v1";

        /// Ollama en el ordenador, sin clave. El servidor tiene que estar en marcha.
        public const string OllamaBaseUrl = "http://localhost:11434/v1";

        // Las claves nuevas. Las viejas se leen como respaldo para no perder la clave que ya
        // tuviera configurada el usuario cuando el chat vivía dentro del grafo.
        const string Prefix = "Liyodev.Assistant.";
        const string OldPrefix = "NarrativeGraph.Assistant.";

        public static event Action Changed;

        public static bool Visible
        {
            get => GetBool("Visible", false);
            set { SetBool("Visible", value); Changed?.Invoke(); }
        }

        public static Provider Current
        {
            get => (Provider)GetInt("Provider", 0);
            set { SetInt("Provider", (int)value); Changed?.Invoke(); }
        }

        public static string Key(Provider p) => GetString(p + ".Key", string.Empty);
        public static void SetKey(Provider p, string key)
        {
            SetString(p + ".Key", key?.Trim() ?? string.Empty);
            Changed?.Invoke();
        }

        public static string Model(Provider p) => GetString(p + ".Model", DefaultModel(p));
        public static void SetModel(Provider p, string model)
        {
            SetString(p + ".Model", model?.Trim() ?? string.Empty);
            Changed?.Invoke();
        }

        /// Solo para «Compatible»: dirección de un servicio que habla el formato de OpenAI
        /// (Ollama, LM Studio, OpenRouter, Mistral, Groq…).
        public static string Address
        {
            get => GetString("Address", "http://localhost:11434/v1");
            set { SetString("Address", value?.Trim() ?? string.Empty); Changed?.Invoke(); }
        }

        public static string Name(Provider p) => p switch
        {
            Provider.Anthropic => "Anthropic (Claude)",
            Provider.OpenAI => "OpenAI (ChatGPT)",
            Provider.Gemini => "Google (Gemini)",
            Provider.OpenCode => "OpenCode (inference)",
            Provider.Ollama => "Ollama (local, gratis)",
            _ => AssistantText.T("Compatible con OpenAI (Ollama, LM Studio, OpenRouter…)",
                                  "OpenAI-compatible (Ollama, LM Studio, OpenRouter…)"),
        };

        public static string DefaultModel(Provider p) => p switch
        {
            Provider.Anthropic => "claude-sonnet-4-5",
            Provider.OpenAI => "gpt-4.1",
            Provider.Gemini => "gemini-2.5-flash",
            Provider.OpenCode => "deepseek-v4.1-flash",
            Provider.Ollama => "qwen3:8b",
            _ => "llama3.1",
        };

        /// Un servicio local puede no necesitar clave; los demás sí.
        public static bool IsReady => Current switch
        {
            Provider.Compatible => !string.IsNullOrEmpty(Address),
            Provider.Ollama => true,
            _ => !string.IsNullOrEmpty(Key(Current)),
        };

        static string GetString(string key, string fallback)
        {
            if (EditorPrefs.HasKey(Prefix + key)) return EditorPrefs.GetString(Prefix + key, fallback);
            return EditorPrefs.GetString(OldPrefix + key, fallback);
        }

        static void SetString(string key, string value)
        {
            EditorPrefs.SetString(Prefix + key, value ?? string.Empty);
            EditorPrefs.SetString(OldPrefix + key, value ?? string.Empty);
        }

        static int GetInt(string key, int fallback)
        {
            if (EditorPrefs.HasKey(Prefix + key)) return EditorPrefs.GetInt(Prefix + key, fallback);
            return EditorPrefs.GetInt(OldPrefix + key, fallback);
        }

        static void SetInt(string key, int value)
        {
            EditorPrefs.SetInt(Prefix + key, value);
            EditorPrefs.SetInt(OldPrefix + key, value);
        }

        static bool GetBool(string key, bool fallback)
        {
            if (EditorPrefs.HasKey(Prefix + key)) return EditorPrefs.GetBool(Prefix + key, fallback);
            return EditorPrefs.GetBool(OldPrefix + key, fallback);
        }

        static void SetBool(string key, bool value)
        {
            EditorPrefs.SetBool(Prefix + key, value);
            EditorPrefs.SetBool(OldPrefix + key, value);
        }
    }
}
