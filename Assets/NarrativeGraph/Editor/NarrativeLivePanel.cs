using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// <summary>
    /// Pestaña «En vivo»: lo que el juego está haciendo ahora mismo mientras se prueba el grafo.
    /// Lee el estado real (sin duplicarlo) del NarrativeGraphHub, de los flags y del blackboard; lo
    /// propio del juego (grupo, misiones, actores en escena…) lo aportan sus INarrativeLiveSection.
    /// Fuera de Play Mode explica que no hay estado real y, si el juego aporta un
    /// INarrativeArrivalPreview, permite simular dónde queda cada actor al llegar al nodo.
    /// </summary>
    public sealed class NarrativeLivePanel : VisualElement
    {
        readonly ScrollView _scroll;
        readonly Label _headline;
        readonly Label _current;
        readonly Button _focusButton;
        readonly Label _notPlaying;
        readonly Button _projectButton;
        readonly VisualElement _projectedBox;
        readonly VisualElement _playingBox;
        readonly VisualElement _flagsBox;
        readonly VisualElement _blackboardBox;
        readonly Foldout _flagsFold;
        readonly Foldout _blackboardFold;
        readonly INarrativeArrivalPreview _arrivalPreview;
        readonly List<(INarrativeLiveSection section, Foldout fold, VisualElement box, List<string> rows)> _sections = new();

        NarrativeGraph _graph;
        NodeView _selected;
        string _lastSignature;
        double _nextRefresh;
        NarrativeGraph _projectedGraph;
        string _projectedNodeGuid;

        public string CurrentNodeGuid { get; private set; }
        public bool Active => style.display != DisplayStyle.None;

        /// <summary>Pide a la ventana centrar el nodo que está ejecutándose.</summary>
        public event Action<string> FocusNodeRequested;

        public NarrativeLivePanel()
        {
            AddToClassList("narrative-live");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("narrative-live__scroll");
            Add(_scroll);

            var title = new Label("Estado en vivo");
            title.AddToClassList("narrative-live__title");
            _scroll.Add(title);

            _notPlaying = new Label("Entra en Play para ver el estado real del juego. En el lienzo, el ▶ de " +
                                    "cada tarjeta lanza una prueba desde ese nodo.");
            _notPlaying.AddToClassList("narrative-live__hint");
            _scroll.Add(_notPlaying);

            _projectButton = new Button(ProjectSelectedNode) { text = "Simular posiciones al llegar al nodo seleccionado" };
            _projectButton.AddToClassList("narrative-test__small-button");
            _scroll.Add(_projectButton);

            _projectedBox = new VisualElement();
            _projectedBox.AddToClassList("narrative-live__list");
            _scroll.Add(_projectedBox);

            _playingBox = new VisualElement();
            _playingBox.AddToClassList("narrative-live__playing");
            _scroll.Add(_playingBox);

            _headline = new Label();
            _headline.AddToClassList("narrative-live__headline");
            _playingBox.Add(_headline);

            _current = new Label();
            _current.AddToClassList("narrative-live__current");
            _playingBox.Add(_current);

            _focusButton = new Button(() =>
            {
                if (!string.IsNullOrEmpty(CurrentNodeGuid)) FocusNodeRequested?.Invoke(CurrentNodeGuid);
            }) { text = "Centrar nodo actual" };
            _focusButton.AddToClassList("narrative-test__small-button");
            _playingBox.Add(_focusButton);

            _arrivalPreview = NarrativeEditorExtensions.CreateFirst<INarrativeArrivalPreview>();

            foreach (var section in NarrativeEditorExtensions.CreateAll<INarrativeLiveSection>().OrderBy(x => x.Order))
            {
                var fold = Fold(section.Title, out var box);
                _sections.Add((section, fold, box, new List<string>()));
            }
            _flagsFold = Fold("Flags narrativos", out _flagsBox);
            _blackboardFold = Fold("Blackboard del grafo", out _blackboardBox);

            Refresh(_graph, _selected, true);
        }

        Foldout Fold(string text, out VisualElement content)
        {
            var fold = new Foldout { text = text, value = true };
            fold.AddToClassList("narrative-live__section");
            content = new VisualElement();
            content.AddToClassList("narrative-live__list");
            fold.Add(content);
            _scroll.Add(fold);
            return fold;
        }

        public void RefreshNow() => _lastSignature = null;

        /// <summary>
        /// Relee el estado real. Se llama desde la ventana cada pocos cientos de milisegundos; si
        /// nada cambió (misma firma) no reconstruye la UI, para no parpadear ni resetear el scroll.
        /// </summary>
        public void Refresh(NarrativeGraph graph, NodeView selected, bool force = false)
        {
            _graph = graph;
            _selected = selected;

            if (!EditorApplication.isPlaying)
            {
                // La simulación de posiciones es de un nodo concreto: si cambia el grafo o la
                // selección, el resultado anterior ya no corresponde y se limpia.
                string selectedGuid = selected?.Model?.guid;
                if (graph != _projectedGraph || selectedGuid != _projectedNodeGuid)
                {
                    _projectedBox.Clear();
                    _projectedGraph = graph;
                    _projectedNodeGuid = selectedGuid;
                }

                CurrentNodeGuid = null;
                _playingBox.style.display = DisplayStyle.None;
                _notPlaying.style.display = DisplayStyle.Flex;
                _projectButton.style.display = _arrivalPreview != null ? DisplayStyle.Flex : DisplayStyle.None;
                _projectedBox.style.display = DisplayStyle.Flex;
                foreach (var entry in _sections) entry.fold.style.display = DisplayStyle.None;
                _flagsFold.style.display = DisplayStyle.None;
                _blackboardFold.style.display = DisplayStyle.None;
                return;
            }

            _projectButton.style.display = DisplayStyle.None;
            _projectedBox.style.display = DisplayStyle.None;

            if (!force && EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.4;

            _playingBox.style.display = DisplayStyle.Flex;
            _notPlaying.style.display = DisplayStyle.None;
            foreach (var entry in _sections) entry.fold.style.display = DisplayStyle.Flex;
            _flagsFold.style.display = DisplayStyle.Flex;
            _blackboardFold.style.display = DisplayStyle.Flex;

            var sb = new StringBuilder(2048);
            var runner = FindRunner(graph);
            var node = runner?.CurrentNode;
            CurrentNodeGuid = node?.guid;
            if (string.IsNullOrEmpty(CurrentNodeGuid) && runner?.Blackboard != null)
                CurrentNodeGuid = runner.Blackboard.Get<string>(NarrativeRunner.CurrentNodeKey, null);

            var scene = SceneManager.GetActiveScene();
            sb.Append("H|").Append(graph?.name).Append('|').Append(scene.name).Append('\n');
            sb.Append("C|").Append(CurrentNodeGuid).Append('|').Append(node?.GetType().Name).Append('|')
              .Append(node?.displayTitle).Append('\n');

            // Secciones del juego
            for (int i = 0; i < _sections.Count; i++)
            {
                var entry = _sections[i];
                entry.rows.Clear();
                try { entry.section.Collect(entry.rows); }
                catch (Exception e) { entry.rows.Add("(error: " + e.Message + ")"); }
                foreach (var row in entry.rows) sb.Append('S').Append(i).Append('|').Append(row).Append('\n');
            }

            // Flags
            var flags = NarrativeFlags.All().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var f in flags) sb.Append("F|").Append(f).Append('\n');

            // Blackboard
            var bb = runner?.Blackboard?.ExportToSerializable() ?? new List<SimpleBlackboard.Entry>();
            bb.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));
            foreach (var e in bb) sb.Append("B|").Append(e.key).Append('=').Append(e.value).Append('\n');

            var signature = sb.ToString();
            if (!force && signature == _lastSignature) return;
            _lastSignature = signature;

            Apply(scene.name, node, flags, bb);
        }

        /// <summary>
        /// Fuera de Play también se puede saber dónde debería estar cada actor al llegar al nodo:
        /// lo simula el INarrativeArrivalPreview del juego y aquí se listan las marcas.
        /// </summary>
        void ProjectSelectedNode()
        {
            _projectedBox.Clear();
            if (_arrivalPreview == null) return;
            if (_graph == null || _selected?.Model == null)
            {
                var hint = new Label("Selecciona un nodo en el lienzo para simular el estado al llegar a él.");
                hint.AddToClassList("narrative-live__muted");
                _projectedBox.Add(hint);
                return;
            }

            var actors = new List<string>();
            var warnings = new List<string>();
            _arrivalPreview.Preview(_graph, _selected.Model.guid, actors, warnings);
            if (actors.Count == 0 && warnings.Count == 0)
            {
                var hint = new Label("No hay actores colocados en el camino hasta este nodo.");
                hint.AddToClassList("narrative-live__muted");
                _projectedBox.Add(hint);
                return;
            }

            foreach (var a in actors)
            {
                var l = new Label("· " + a);
                l.AddToClassList("narrative-live__row");
                _projectedBox.Add(l);
            }
            foreach (var w in warnings)
            {
                var l = new Label("⚠ " + w);
                l.AddToClassList("narrative-test__preview-line");
                l.AddToClassList("is-warning");
                _projectedBox.Add(l);
            }
        }

        static NarrativeRunner FindRunner(NarrativeGraph graph)
        {
            if (graph == null || NarrativeGraphHub.Instance == null) return null;
            return NarrativeGraphHub.Instance.GetAllRunners()?.FirstOrDefault(r => r != null && r.graph == graph);
        }

        void Apply(string sceneName, NarrativeNode node, List<string> flags, List<SimpleBlackboard.Entry> bb)
        {
            _headline.text = $"EN PLAY · {(_graph != null ? _graph.name : "sin grafo")} · {sceneName}";
            var shown = node;
            bool branches = false;
            if (shown == null && _graph != null && !string.IsNullOrEmpty(CurrentNodeGuid))
            {
                shown = _graph.FindNode(CurrentNodeGuid);
                branches = shown != null;
            }
            _current.text = shown != null
                ? $"Nodo actual: {shown.displayTitle ?? NarrativeNodeCatalog.TitleOf(shown.GetType())}  ({shown.GetType().Name})" +
                  (branches ? "  ·  ramas en curso" : "")
                : "Sin nodo actual: el grafo no ha empezado o ya terminó.";
            _focusButton.SetEnabled(!string.IsNullOrEmpty(CurrentNodeGuid));

            foreach (var entry in _sections)
                Fill(entry.box, entry.rows, entry.section.EmptyText, row => row);

            Fill(_flagsBox, flags, "No hay flags narrativos activos.", s => s);

            Fill(_blackboardBox, bb.Select(e => $"{e.key} = {e.value}").ToList(),
                "Blackboard vacío.", s => s);
        }

        static void Fill(VisualElement box, List<string> rows, string empty, Func<string, string> format)
        {
            box.Clear();
            if (rows.Count == 0)
            {
                var l = new Label(empty);
                l.AddToClassList("narrative-live__muted");
                box.Add(l);
                return;
            }
            foreach (var row in rows)
            {
                var l = new Label(format(row));
                l.AddToClassList("narrative-live__row");
                box.Add(l);
            }
        }
    }
}
