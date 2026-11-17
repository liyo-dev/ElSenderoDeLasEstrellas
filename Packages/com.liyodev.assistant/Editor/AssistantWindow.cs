using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// Una sola ventana de chat para todas las herramientas. El contexto elegido pone el manual,
    /// el estado y la forma de aplicar los cambios; la ventana no sabe de grafos ni de cinemáticas.
    public sealed class AssistantWindow : EditorWindow
    {
        private static string s_wantedContextId;
        private static bool s_runQuickAction;

        private DropdownField _contexts;
        private VisualElement _host;
        private VisualElement _settingsPanel;
        private AssistantView _view;
        private IAssistantContext _context;

        [MenuItem("Liyodev/Asistente", false, 20)]
        public static void Open() => Open(null);

        public static void Open(string contextId, bool runQuickAction = false)
        {
            var window = GetWindow<AssistantWindow>(AssistantText.T("Asistente", "Assistant"));
            window.minSize = new Vector2(360f, 420f);
            s_wantedContextId = contextId;
            s_runQuickAction = runQuickAction;
            window.Show();
            window.RefreshContexts();
        }

        private void OnEnable()
        {
            AssistantContexts.Changed -= RefreshContexts;
            AssistantContexts.Changed += RefreshContexts;
            if (_context != null)
            {
                _context.Changed -= OnContextChanged;
                _context.Changed += OnContextChanged;
            }
        }

        private void OnDisable()
        {
            AssistantContexts.Changed -= RefreshContexts;
            if (_context != null) _context.Changed -= OnContextChanged;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("assistant-root");
            root.style.flexGrow = 1;

            string stylePath = AssistantPaths.StyleSheet;
            var style = string.IsNullOrEmpty(stylePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
            if (style != null) root.styleSheets.Add(style);

            var bar = new VisualElement();
            bar.AddToClassList("assistant-bar");
            _contexts = new DropdownField(AssistantText.T("Contexto", "Context"))
            {
                style = { flexGrow = 1 },
            };
            _contexts.RegisterValueChangedCallback(_ => SelectContext(CurrentContext()));
            bar.Add(_contexts);
            var settings = new Button(ToggleSettings)
            {
                text = "⚙",
                tooltip = AssistantText.T("Ajustes del asistente", "Assistant settings"),
            };
            settings.AddToClassList("story-button");
            bar.Add(settings);
            root.Add(bar);

            _settingsPanel = new VisualElement();
            _settingsPanel.AddToClassList("assistant-settings");
            _settingsPanel.style.display = DisplayStyle.None;
            AssistantSettingsUI.BuildSection(_settingsPanel);
            root.Add(_settingsPanel);

            _host = new VisualElement { style = { flexGrow = 1 } };
            root.Add(_host);

            RefreshContexts();
        }

        private IAssistantContext CurrentContext()
        {
            if (_contexts == null || _contexts.index < 0) return null;
            var all = AssistantContexts.All;
            return _contexts.index < all.Count ? all[_contexts.index] : null;
        }

        private void RefreshContexts()
        {
            if (_host == null) return; // Todavía no hay interfaz (OnEnable va antes que CreateGUI).
            var all = AssistantContexts.All;

            _contexts.choices = all
                .Select(c => c.Name + (string.IsNullOrEmpty(c.TargetName) ? string.Empty : " · " + c.TargetName))
                .ToList();

            if (all.Count == 0)
            {
                _contexts.SetEnabled(false);
                _host.Clear();
                var note = new Label(AssistantText.T(
                    "Abre la ventana de Historia o el Director para que el asistente sepa sobre qué trabajar.",
                    "Open the Story window or the Director so the assistant knows what to work on."));
                note.AddToClassList("story-muted");
                _host.Add(note);
                return;
            }

            _contexts.SetEnabled(true);
            string wanted = _context != null ? _context.Id : s_wantedContextId;
            int index = 0;
            if (!string.IsNullOrEmpty(wanted))
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Id == wanted) { index = i; break; }

            _contexts.SetValueWithoutNotify(_contexts.choices[index]);
            SelectContext(all[index]);
        }

        private void SelectContext(IAssistantContext context)
        {
            if (context == null) return;
            if (context == _context)
            {
                if (s_runQuickAction)
                {
                    s_runQuickAction = false;
                    _view?.SendQuickAction();
                }
                return;
            }
            if (_context != null) _context.Changed -= OnContextChanged;
            _context = context;
            _context.Changed += OnContextChanged;
            _host.Clear();
            _view = new AssistantView(context);
            _view.SettingsRequested += ToggleSettings;
            _host.Add(_view);

            if (s_runQuickAction)
            {
                s_runQuickAction = false;
                _view.SendQuickAction();
            }
        }

        private void OnContextChanged()
        {
            _view?.RefreshState();
            RefreshContexts();
        }

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool show = _settingsPanel.style.display == DisplayStyle.None;
            _settingsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
