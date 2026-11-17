using System;
using System.Collections.Generic;
using Liyodev.Assistant;

namespace NarrativeGraphEditor.Story
{
    /// El ajuste del asistente vive ahora en Liyodev.Assistant, común a todas las herramientas.
    /// Esta fachada mantiene los nombres que ya usaba el editor del grafo para no tocar sus
    /// ventanas ni perder las claves guardadas.
    public static class StoryAssistantSettings
    {
        public enum Provider { Anthropic, OpenAI, Gemini, Compatible, OpenCode, Ollama }

        public static bool Visible
        {
            get => AssistantSettings.Visible;
            set => AssistantSettings.Visible = value;
        }

        public static Provider Current
        {
            get => (Provider)(int)AssistantSettings.Current;
            set => AssistantSettings.Current = (AssistantSettings.Provider)(int)value;
        }

        public static string Key(Provider p) => AssistantSettings.Key(Map(p));
        public static void SetKey(Provider p, string key) => AssistantSettings.SetKey(Map(p), key);

        public static string Model(Provider p) => AssistantSettings.Model(Map(p));
        public static void SetModel(Provider p, string model) => AssistantSettings.SetModel(Map(p), model);

        public static string Address
        {
            get => AssistantSettings.Address;
            set => AssistantSettings.Address = value;
        }

        public static string Name(Provider p) => AssistantSettings.Name(Map(p));
        public static string DefaultModel(Provider p) => AssistantSettings.DefaultModel(Map(p));
        public static bool IsReady => AssistantSettings.IsReady;

        public static event Action Changed
        {
            add => AssistantSettings.Changed += value;
            remove => AssistantSettings.Changed -= value;
        }

        static AssistantSettings.Provider Map(Provider p) => (AssistantSettings.Provider)(int)p;
    }

    /// Fachada del cliente compartido. El panel del grafo ya no la usa (habla con AssistantView),
    /// pero se queda para que nada externo que llamara a Send deje de compilar.
    public static class StoryAssistantClient
    {
        public readonly struct Turn
        {
            public readonly bool FromUser;
            public readonly string Text;
            public Turn(bool fromUser, string text) { FromUser = fromUser; Text = text; }
        }

        public static void Send(string system, IReadOnlyList<Turn> turns,
            Action<string> onAnswer, Action<string> onError)
        {
            var shared = new List<AssistantTurn>(turns.Count);
            foreach (var turn in turns) shared.Add(new AssistantTurn(turn.FromUser, turn.Text));
            AssistantClient.Send(system, shared, onAnswer, onError);
        }
    }
}
