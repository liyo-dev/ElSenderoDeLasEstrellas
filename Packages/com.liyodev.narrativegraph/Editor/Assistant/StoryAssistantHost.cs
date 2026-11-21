using System;
using Liyodev.Assistant;
using UnityEditor;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// El asistente de Liyodev enchufado al editor del grafo: el panel del chat, sus ajustes y el
    /// interruptor del servidor MCP compartido (AssistantBridge).
    public sealed class StoryAssistantHost : INarrativeAssistantHost
    {
        public bool ButtonVisible => AssistantSettings.Visible;

        public event Action SettingsChanged
        {
            add => AssistantSettings.Changed += value;
            remove => AssistantSettings.Changed -= value;
        }

        public VisualElement CreatePanel(Action settingsRequested) => new StoryAssistantPanel(settingsRequested);

        public void BuildAssistantSettings(VisualElement section)
        {
            var visible = new Toggle(StoryText.T("Mostrar el botón del asistente", "Show the assistant button")) { value = AssistantSettings.Visible };
            visible.RegisterValueChangedCallback(e => AssistantSettings.Visible = e.newValue);
            section.Add(visible);

            var conectar = new Button(AssistantConnectWindow.Open) { text = StoryText.T("Conectar mi IA…", "Connect my AI…") };
            conectar.AddToClassList("story-button");
            conectar.style.alignSelf = Align.FlexStart;
            conectar.style.marginLeft = 0;
            conectar.style.marginBottom = 6;
            section.Add(conectar);

            // Otras formas (clave de API, Ollama): los mismos controles que «Conectar mi IA».
            AssistantSettingsUI.BuildSection(section);
        }

        public void BuildOutsideToolsSettings(VisualElement section)
        {
            var bridge = new Toggle(StoryText.T($"Permitir herramientas externas (puerto {AssistantBridge.Port})", $"Allow outside tools (port {AssistantBridge.Port})"))
                { value = AssistantBridge.Enabled };
            bridge.RegisterValueChangedCallback(e => AssistantBridge.Enabled = e.newValue);
            section.Add(bridge);

            var readme = new Button(OpenReadme) { text = StoryText.T("Ver cómo conectarlo", "How to connect it") };
            readme.AddToClassList("story-button");
            readme.style.alignSelf = Align.FlexStart;
            readme.style.marginLeft = 0;
            section.Add(readme);
        }

        /// El README del servidor MCP del asistente, junto a su carpeta Editor.
        static void OpenReadme()
        {
            var editor = AssistantPaths.Editor;
            if (string.IsNullOrEmpty(editor)) return;
            var root = System.IO.Path.GetDirectoryName(editor);
            var readme = System.IO.Path.Combine(root ?? string.Empty, "Tools", "mcp", "README.md");
            EditorUtility.RevealInFinder(System.IO.Path.GetFullPath(readme));
        }
    }
}
