using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Tarjeta visual de un nodo en el grafo.
    ///
    /// Diseño (Septiembre 2026): cabecera con color de categoría + etiqueta editable + capítulo;
    /// cuerpo con un RESUMEN legible del contenido real (texto del diálogo, nombre y pasos de la
    /// quest, quién emite/escucha una señal...) construido por NodeSummary; puertos de salida con
    /// nombre para los nodos que bifurcan; avisos de validación inline. Los campos "en crudo" se
    /// editan en el panel lateral (NarrativeInspectorPanel), no dentro de la tarjeta.
    /// </summary>
    public class NodeView : Node
    {
        public NarrativeNode Model { get; private set; }
        public Port Input { get; private set; }
        /// <summary>Puertos de salida en orden. Un solo puerto para nodos legacy; N para nodos con puertos con nombre.</summary>
        public List<Port> Outputs { get; } = new();
        public bool HasNamedOutputs { get; private set; }

        readonly VisualElement _summary;
        readonly VisualElement _warnings;
        readonly Label _forkBadge;
        readonly Label _chapterBadge;
        readonly Label _startBadge;
        readonly TextField _labelField;
        readonly NarrativeGraph _graph;
        readonly SerializedObject _serializedGraph;

        public event Action<NodeView> LabelChanged;

        public NodeView(NarrativeNode model, NarrativeGraph graph, SerializedObject serializedGraph)
        {
            Model = model;
            _graph = graph;
            _serializedGraph = serializedGraph;

            var type = model.GetType();
            var entry = NarrativeNodeCatalog.Get(type);
            var category = entry?.category ?? "Otros";
            var color = NarrativeNodeCatalog.ColorOf(category);
            bool isNote = model is GraphNoteNode;

            AddToClassList("narrative-node");
            AddToClassList("narrative-node--" + Slug(category));
            if (isNote) AddToClassList("narrative-node--note");
            if (entry != null && entry.obsolete) AddToClassList("narrative-node--obsolete");

            title = entry?.title ?? type.Name;
            titleContainer.AddToClassList("narrative-node__title-bar");
            titleContainer.style.backgroundColor = new StyleColor(Color.Lerp(color, Color.black, 0.15f));
            mainContainer.AddToClassList("narrative-node__main");
            mainContainer.style.borderLeftColor = new StyleColor(color);
            mainContainer.style.borderLeftWidth = 4f;
            extensionContainer.AddToClassList("narrative-node__extension");
            inputContainer.AddToClassList("narrative-port-dock");
            outputContainer.AddToClassList("narrative-port-dock");
            outputContainer.AddToClassList("narrative-port-dock--right");

            // El título por defecto de GraphView es un Label "title-label"; lo dejamos como nombre
            // del tipo y añadimos la etiqueta editable debajo.
            var titleLabel = titleContainer.Q<Label>("title-label");
            if (titleLabel != null) titleLabel.AddToClassList("narrative-node__type-title");

            // Badge de inicio (solo si es el start del grafo)
            _startBadge = new Label("▶ INICIO");
            _startBadge.AddToClassList("narrative-node__start-badge");
            _startBadge.style.display = DisplayStyle.None;
            titleContainer.Insert(0, _startBadge);

            // Capítulo
            _chapterBadge = new Label();
            _chapterBadge.AddToClassList("narrative-node__chapter-badge");
            titleContainer.Add(_chapterBadge);

            // Fila de etiqueta editable (displayTitle)
            var labelRow = new VisualElement();
            labelRow.AddToClassList("narrative-node__label-row");
            _labelField = new TextField { isDelayed = true, multiline = false };
            _labelField.AddToClassList("narrative-node__label-field");
            _labelField.value = model.displayTitle ?? "";
            _labelField.tooltip = "Etiqueta del nodo (texto libre). Ej: '2.- Hablar con Eldran'";
            _labelField.RegisterValueChangedCallback(evt =>
            {
                if (_graph == null) return;
                Undo.RecordObject(_graph, "Editar etiqueta de nodo");
                Model.displayTitle = evt.newValue;
                EditorUtility.SetDirty(_graph);
                LabelChanged?.Invoke(this);
            });
            labelRow.Add(_labelField);
            mainContainer.Insert(1, labelRow); // debajo de la barra de título, encima de los puertos

            // Resumen de contenido
            _summary = new VisualElement();
            _summary.AddToClassList("narrative-node__summary");
            extensionContainer.Add(_summary);

            // Avisos
            _warnings = new VisualElement();
            _warnings.AddToClassList("narrative-node__warnings");
            extensionContainer.Add(_warnings);

            // Badge de fork (se rellena desde la ventana cuando se conocen las aristas)
            _forkBadge = new Label();
            _forkBadge.AddToClassList("narrative-node__fork-badge");
            _forkBadge.style.display = DisplayStyle.None;
            outputContainer.Add(_forkBadge);

            BuildPorts();

            var defaultSize = isNote ? new Vector2(240, 140) : new Vector2(300, 120);
            SetPosition(new Rect(model.position, defaultSize));

            RefreshAll();
            RefreshExpandedState();
            RefreshPorts();
        }

        void BuildPorts()
        {
            bool isNote = Model is GraphNoteNode;
            if (isNote) return;

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "";
            Input.AddToClassList("narrative-port");
            Input.AddToClassList("narrative-port--in");
            inputContainer.Add(Input);

            var names = Model.GetOutputPorts();
            HasNamedOutputs = names != null;
            if (names == null)
            {
                var p = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                p.portName = "";
                p.AddToClassList("narrative-port");
                p.AddToClassList("narrative-port--out");
                p.userData = 0;
                outputContainer.Add(p);
                Outputs.Add(p);
            }
            else
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var p = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    p.portName = names[i];
                    p.AddToClassList("narrative-port");
                    p.AddToClassList("narrative-port--out");
                    p.AddToClassList("narrative-port--named");
                    p.userData = i;
                    outputContainer.Add(p);
                    Outputs.Add(p);
                }
            }
        }

        /// <summary>Índice de puerto (0..N-1) de un Port de salida de esta vista.</summary>
        public int PortIndex(Port port)
        {
            for (int i = 0; i < Outputs.Count; i++)
                if (Outputs[i] == port) return i;
            return 0;
        }

        public Port GetOutputPort(int index)
        {
            if (Outputs.Count == 0) return null;
            if (!HasNamedOutputs) return Outputs[0];
            return index >= 0 && index < Outputs.Count ? Outputs[index] : null;
        }

        // ─── Refresco visual ─────────────────────────────────────────────

        public void RefreshAll()
        {
            RefreshLabel();
            RefreshChapterBadge();
            RefreshSummary();
            RefreshWarnings();
            RefreshStartBadge();
        }

        public void RefreshLabel()
        {
            if (_labelField.value != (Model.displayTitle ?? ""))
                _labelField.SetValueWithoutNotify(Model.displayTitle ?? "");
        }

        public void RefreshStartBadge()
        {
            bool isStart = _graph != null && _graph.startNodeGuid == Model.guid;
            _startBadge.style.display = isStart ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("narrative-node--start", isStart);
        }

        public void RefreshChapterBadge()
        {
            var ch = Model.chapter;
            if (string.IsNullOrWhiteSpace(ch))
            {
                _chapterBadge.style.display = DisplayStyle.None;
                return;
            }
            _chapterBadge.text = ch;
            _chapterBadge.style.display = DisplayStyle.Flex;
            _chapterBadge.style.backgroundColor = new StyleColor(ChapterColor(ch));
        }

        public void RefreshSummary()
        {
            _summary.Clear();
            try
            {
                var el = NodeSummary.Build(Model, _graph);
                if (el != null) _summary.Add(el);
            }
            catch (Exception ex)
            {
                _summary.Add(new Label("(resumen no disponible: " + ex.Message + ")") { name = "summary-error" });
            }
            RefreshExpandedState();
        }

        public void RefreshWarnings()
        {
            _warnings.Clear();
            var issues = NodeSummary.Validate(Model, _graph);
            foreach (var issue in issues)
            {
                var l = new Label("⚠ " + issue);
                l.AddToClassList("narrative-node__warning");
                _warnings.Add(l);
            }
            _warnings.style.display = issues.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("narrative-node--has-warnings", issues.Count > 0);
            RefreshExpandedState();
        }

        /// <summary>Número de aristas que salen de un nodo de puerto único (>1 = fork).</summary>
        public void SetForkCount(int edgeCount)
        {
            bool isFork = !HasNamedOutputs && edgeCount > 1;
            if (!isFork)
            {
                _forkBadge.style.display = DisplayStyle.None;
                RemoveFromClassList("narrative-node--fork");
                return;
            }
            _forkBadge.text = Model is ForkNode ? $"⑂ {edgeCount} ramas" : $"⑂ FORK ×{edgeCount}";
            _forkBadge.tooltip = Model is ForkNode
                ? "Todas las ramas corren en paralelo."
                : "Varias aristas desde un nodo normal = las ramas corren EN PARALELO. Si querías una bifurcación, usa 'Según estado de quest' / 'Según flag' / 'Pregunta con respuestas'.";
            _forkBadge.style.display = DisplayStyle.Flex;
            AddToClassList("narrative-node--fork");
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Model.position = newPos.position;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Marcar como inicio del grafo", _ =>
            {
                if (_graph == null) return;
                Undo.RecordObject(_graph, "Marcar inicio");
                _graph.startNodeGuid = Model.guid;
                EditorUtility.SetDirty(_graph);
                StartChanged?.Invoke();
            }, _ => Model is StartNode ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Quick Test desde aquí", _ =>
            {
                if (_graph != null) NarrativeQuickTestWindow.OpenWithNode(_graph, Model);
            });
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        public static event Action StartChanged;

        // ─── utilidades ──────────────────────────────────────────────────

        static readonly Color[] ChapterColors =
        {
            new Color(0.20f, 0.60f, 0.95f), new Color(0.18f, 0.78f, 0.45f), new Color(0.90f, 0.55f, 0.20f),
            new Color(0.75f, 0.35f, 0.85f), new Color(0.95f, 0.40f, 0.40f), new Color(0.25f, 0.75f, 0.75f),
            new Color(0.90f, 0.75f, 0.20f), new Color(0.55f, 0.45f, 0.80f),
        };

        public static Color ChapterColor(string chapter)
        {
            if (string.IsNullOrWhiteSpace(chapter)) return new Color(0.5f, 0.5f, 0.5f);
            int hash = 0;
            foreach (var c in chapter) hash = hash * 31 + c;
            return ChapterColors[Mathf.Abs(hash) % ChapterColors.Length];
        }

        static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in s.ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString().Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
        }
    }
}
