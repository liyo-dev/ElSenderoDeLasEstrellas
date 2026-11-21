using System;
using Liyodev.Assistant;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// El panel del asistente dentro del editor del grafo: una fachada fina sobre el chat
    /// compartido. La conversación, los ajustes y el contrato de cambios viven en
    /// Liyodev.Assistant; aquí solo se enchufa el contexto del grafo (siempre registrado).
    public sealed class StoryAssistantPanel : VisualElement
    {
        public StoryAssistantPanel(Action settingsRequested)
        {
            style.flexGrow = 1;
            var context = AssistantContexts.Find(StoryAssistantContext.ContextId) ?? new StoryAssistantContext();
            var view = new AssistantView(context);
            view.SettingsRequested += () => settingsRequested?.Invoke();
            Add(view);
        }
    }
}
