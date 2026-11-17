using System;
using Liyodev.Assistant;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// El panel del asistente dentro del editor del grafo: una fachada fina sobre el chat
    /// compartido. La conversación, los ajustes y el contrato de cambios viven en
    /// Liyodev.Assistant; aquí solo se enchufa el contexto del capítulo abierto.
    public sealed class StoryAssistantPanel : VisualElement
    {
        public Func<NarrativeGraph> Graph;
        public Func<NarrativeNode> Step;
        public event Action Changed;
        public event Action SettingsRequested;

        private readonly StoryAssistantContext _context;

        public StoryAssistantPanel()
        {
            style.flexGrow = 1;
            _context = new StoryAssistantContext(() => Graph?.Invoke(), () => Step?.Invoke());
            _context.Changed += () => Changed?.Invoke();

            var view = new AssistantView(_context);
            view.SettingsRequested += () => SettingsRequested?.Invoke();
            Add(view);

            // El contexto se apunta cuando el panel ya está en la ventana: así el nombre del
            // capítulo ya está disponible para el selector del asistente.
            RegisterCallback<AttachToPanelEvent>(_ => AssistantContexts.Register(_context));
            RegisterCallback<DetachFromPanelEvent>(_ => AssistantContexts.Unregister(_context));
        }
    }
}
