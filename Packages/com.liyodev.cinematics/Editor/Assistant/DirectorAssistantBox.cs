using System;
using Liyodev.Assistant;
using UnityEngine.UIElements;

namespace Liyodev.Cinematics
{
    /// La caja de la IA del Director sobre el chat compartido de Liyodev Assistant, con el contexto
    /// de cinemáticas (CinematicsAssistantContext, siempre registrado).
    public sealed class DirectorAssistantBox : IDirectorAssistant
    {
        private IAssistantContext _context;
        private AssistantView _view;

        public string StyleSheetPath => AssistantPaths.StyleSheet;

        public VisualElement CreateView(Action applied, Action busyChanged)
        {
            _context = AssistantContexts.Find("cinematics");
            if (_context == null) return null;
            _context.Changed -= Refresh;
            _context.Changed += Refresh;
            _view = new AssistantView(_context);
            _view.SettingsRequested += AssistantConnectWindow.Open;
            if (applied != null) _view.Applied += applied;
            if (busyChanged != null) _view.BusyChanged += busyChanged;
            return _view;
        }

        public bool Busy => _view != null && _view.Busy;

        public void SendQuickAction()
        {
            if (!AssistantSettings.IsReady) { OpenConnect(); return; }
            _view?.SendQuickAction();
        }

        public string ConnectLabel => AssistantSettings.IsReady
            ? "IA: " + AssistantSettings.Name(AssistantSettings.Current)
            : "Conectar mi IA";

        public void OpenConnect() => AssistantConnectWindow.Open();

        public event Action SettingsChanged
        {
            add => AssistantSettings.Changed += value;
            remove => AssistantSettings.Changed -= value;
        }

        public void Dispose()
        {
            if (_context != null) _context.Changed -= Refresh;
            _context = null;
            _view = null;
        }

        private void Refresh() => _view?.RefreshState();
    }
}
