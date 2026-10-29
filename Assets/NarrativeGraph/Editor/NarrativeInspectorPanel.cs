using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// <summary>
    /// Panel lateral derecho del editor: muestra y edita el nodo seleccionado.
    ///
    /// Tres bloques: (1) los campos del nodo con los desplegables de NarrativeKeyDrawer (y las
    /// listas de enum marcadas con [NarrativeChipList] como etiquetas); (2) el CONTENIDO resuelto
    /// de lo que declara el nodo en NarrativeNode.Describe — guion completo del diálogo, ficha de la
    /// quest con sus pasos, quién emite/escucha una señal — para no tener que abrir el asset aparte;
    /// (3) conexiones entrantes y salientes con botones para saltar a ellas.
    /// </summary>
    public sealed class NarrativeInspectorPanel : VisualElement
    {
        readonly ScrollView _scroll;
        readonly Label _header;
        readonly VisualElement _body;

        NarrativeGraph _graph;
        SerializedObject _so;
        NodeView _current;

        /// <summary>Se dispara cuando el usuario cambia un campo del nodo (para refrescar la tarjeta).</summary>
        public event Action<NodeView> NodeFieldChanged;
        /// <summary>Pide a la ventana que centre/seleccione otro nodo.</summary>
        public event Action<string> NavigateToNode;

        public NarrativeInspectorPanel()
        {
            AddToClassList("narrative-inspector");
            _header = new Label("Nada seleccionado");
            _header.AddToClassList("narrative-inspector__header");
            Add(_header);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("narrative-inspector__scroll");
            Add(_scroll);
            _body = new VisualElement();
            _scroll.Add(_body);
            ShowEmpty();
        }

        public void SetGraph(NarrativeGraph graph, SerializedObject so)
        {
            _graph = graph;
            _so = so;
            Show(null);
        }

        public void Show(NodeView view)
        {
            _current = view;
            _body.Clear();
            if (view == null || view.Model == null || _graph == null || _so == null)
            {
                ShowEmpty();
                return;
            }

            var model = view.Model;
            var entry = NarrativeNodeCatalog.Get(model.GetType());
            _header.text = entry?.title ?? model.GetType().Name;
            _header.style.borderLeftColor = new StyleColor(NarrativeNodeCatalog.ColorOf(model.GetType()));

            if (!string.IsNullOrEmpty(entry?.description))
            {
                var d = new Label(entry.description);
                d.AddToClassList("narrative-inspector__description");
                _body.Add(d);
            }

            // ── 1. Campos ───────────────────────────────────────────────
            _so.Update();
            int idx = _graph.nodes.IndexOf(model);
            if (idx < 0) { ShowEmpty(); return; }
            var nodeProp = _so.FindProperty("nodes").GetArrayElementAtIndex(idx);

            var fields = Section("Campos");
            foreach (var child in EnumerateDirectChildren(nodeProp))
            {
                var name = child.name;
                if (name == "guid" || name == "position" || name == "outputs") continue;
                if (name == "inputAnchor" || name == "outputAnchor" || name == "overrideNodeColor" || name == "nodeColor") continue;

                var chipField = ChipListField(model.GetType(), name, out var chipAttr);
                if (chipField != null)
                {
                    BuildChipList(fields, model, chipField, chipAttr, view);
                    continue;
                }

                var pf = new PropertyField(child.Copy());
                if (name == "displayTitle") pf.label = "Etiqueta";
                else if (name == "chapter") pf.label = "Capítulo";
                else if (name == "blockSaving") pf.label = "Bloquea guardado";
                pf.Bind(_so);
                var captured = view;
                pf.RegisterValueChangeCallback(_ =>
                {
                    if (_suppressChangeEvents) return; // el bind inicial también dispara el evento
                    EditorUtility.SetDirty(_graph);
                    NodeFieldChanged?.Invoke(captured);
                    // El contenido resuelto depende de los campos: refrescar sin reconstruir todo el panel
                    RefreshContent();
                });
                fields.Add(pf);
            }

            _suppressChangeEvents = true;
            schedule.Execute(() => _suppressChangeEvents = false).ExecuteLater(50);

            // ── 2. Contenido resuelto ───────────────────────────────────
            _contentSection = Section("Contenido");
            BuildContent(_contentSection, model);

            // ── 3. Conexiones ───────────────────────────────────────────
            var links = Section("Conexiones");
            BuildLinks(links, model);

            // ── 4. Técnico ──────────────────────────────────────────────
            var tech = Section("Técnico", collapsed: true);
            tech.Add(new Label($"Tipo: {model.GetType().Name}") { name = "tech-type" });
            tech.Add(new Label($"GUID: {model.guid}") { name = "tech-guid" });
            var saveAttr = model.GetType().GetCustomAttributes(typeof(SavePointAttribute), false).FirstOrDefault() as SavePointAttribute;
            var unsafeAttr = model.GetType().GetCustomAttributes(typeof(UnsafeForSaveAttribute), false).FirstOrDefault() as UnsafeForSaveAttribute;
            if (saveAttr != null) tech.Add(new Label("Guardado: seguro" + (string.IsNullOrEmpty(saveAttr.Description) ? "" : " — " + saveAttr.Description)));
            if (unsafeAttr != null) tech.Add(new Label("Guardado: NO seguro" + (string.IsNullOrEmpty(unsafeAttr.Reason) ? "" : " — " + unsafeAttr.Reason)));
        }

        VisualElement _contentSection;
        bool _suppressChangeEvents;

        void RefreshContent()
        {
            if (_contentSection == null || _current?.Model == null) return;
            _contentSection.Clear();
            BuildContent(_contentSection, _current.Model);
        }

        void ShowEmpty()
        {
            _header.text = "Nada seleccionado";
            _header.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            _body.Clear();
            var hint = new Label("Selecciona un nodo para ver y editar su contenido.\n\nConsejos:\n• Doble clic en el lienzo o botón derecho → añadir nodo.\n• Arrastra desde un puerto para conectar.\n• Enter en la búsqueda salta entre coincidencias.");
            hint.AddToClassList("narrative-inspector__hint");
            _body.Add(hint);
        }

        VisualElement Section(string title, bool collapsed = false)
        {
            var fold = new Foldout { text = title, value = !collapsed };
            fold.AddToClassList("narrative-inspector__section");
            _body.Add(fold);
            return fold;
        }

        // ─── Listas de enum como etiquetas ([NarrativeChipList]) ────────

        /// <summary>
        /// Campo List&lt;enum&gt; marcado con [NarrativeChipList], o null si el campo no lo es.
        /// </summary>
        static FieldInfo ChipListField(Type type, string fieldName, out NarrativeChipListAttribute attr)
        {
            attr = null;
            for (var t = type; t != null && t != typeof(NarrativeNode); t = t.BaseType)
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f == null) continue;
                attr = f.GetCustomAttribute<NarrativeChipListAttribute>();
                if (attr == null) return null;
                var ft = f.FieldType;
                bool isEnumList = ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) && ft.GetGenericArguments()[0].IsEnum;
                return isEnumList ? f : null;
            }
            return null;
        }

        /// <summary>
        /// Sustituye una lista de enum en crudo por etiquetas con botón de quitar y un desplegable
        /// para añadir (sin repetir valores ni ofrecer «None»).
        /// </summary>
        void BuildChipList(VisualElement root, NarrativeNode node, FieldInfo field, NarrativeChipListAttribute attr, NodeView view)
        {
            var enumType = field.FieldType.GetGenericArguments()[0];
            var box = new VisualElement();
            box.AddToClassList("unlock-editor");
            root.Add(box);
            Rebuild();

            System.Collections.IList CurrentList()
            {
                var list = field.GetValue(node) as System.Collections.IList;
                if (list == null)
                {
                    list = (System.Collections.IList)Activator.CreateInstance(field.FieldType);
                    field.SetValue(node, list);
                }
                return list;
            }

            void Change(string undo, Action<System.Collections.IList> apply)
            {
                Undo.RecordObject(_graph, undo);
                apply(CurrentList());
                EditorUtility.SetDirty(_graph);
                _so.Update();
                NodeFieldChanged?.Invoke(view);
                schedule.Execute(Rebuild);
            }

            void Rebuild()
            {
                box.Clear();
                box.Add(SectionLabel(attr.Title ?? ObjectNames.NicifyVariableName(field.Name)));

                var list = CurrentList();
                var chips = new VisualElement();
                chips.AddToClassList("unlock-editor__chips");
                for (int i = 0; i < list.Count; i++)
                {
                    int index = i;
                    var chip = new Button(() => Change("Quitar de la lista", l => l.RemoveAt(index)))
                        { text = list[i] + "  ×", tooltip = "Quitar del nodo" };
                    chip.AddToClassList("unlock-editor__chip");
                    chips.Add(chip);
                }
                if (list.Count == 0) chips.Add(Muted("Ninguno."));
                box.Add(chips);

                var options = Enum.GetValues(enumType).Cast<object>()
                    .Where(v => v.ToString() != "None" && !list.Contains(v))
                    .Select(v => v.ToString()).ToList();
                if (options.Count > 0)
                    box.Add(AddRow(options, "Añadir", choice => Change("Añadir a la lista", l => l.Add(Enum.Parse(enumType, choice)))));
            }
        }

        static Label SectionLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("unlock-editor__title");
            return l;
        }

        VisualElement AddRow(List<string> options, string label, Action<string> add)
        {
            var row = new VisualElement();
            row.AddToClassList("unlock-editor__add");
            var dropdown = new DropdownField(label, options, 0);
            dropdown.AddToClassList("unlock-editor__dropdown");
            row.Add(dropdown);
            var button = new Button(() => add(dropdown.value)) { text = "＋" };
            button.AddToClassList("unlock-editor__add-button");
            row.Add(button);
            return row;
        }

        // ─── Contenido resuelto ─────────────────────────────────────────

        void BuildContent(VisualElement root, NarrativeNode model)
        {
            model.Describe(new ContentDescriber(this, root));
            if (root.childCount == 0) root.Add(Muted("Este nodo no referencia contenido externo."));
        }

        /// <summary>
        /// Pinta en el panel el contenido resuelto de las referencias que declara el nodo. La
        /// presentación de la tarjeta (líneas, campos, etiquetas) no se repite aquí.
        /// </summary>
        sealed class ContentDescriber : INarrativeNodeDescriber
        {
            readonly NarrativeInspectorPanel _panel;
            readonly VisualElement _root;
            VisualElement _script;
            int _lineNumber;

            public ContentDescriber(NarrativeInspectorPanel panel, VisualElement root) { _panel = panel; _root = root; }

            public void Quest(string questId, string verb, IReadOnlyList<string> steps = null) => _panel.QuestContent(_root, questId, steps);
            public void Signal(string key, SignalRole role, string label = null) => SignalContent(_root, key);
            public void Actor(string actorId, string verb) => ActorContent(_root, actorId);
            public void Flag(string key, string shownAs = null) { }
            public void Anchor(string anchorId) { }

            public void Asset(UnityEngine.Object asset, string kind, string detail = null)
            {
                _script = null;
                _lineNumber = 0;
                AssetContent(_root, asset, kind, detail);
            }

            public void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null)
            {
                if (_script == null)
                {
                    _script = new VisualElement();
                    _script.AddToClassList("narrative-inspector__script");
                    _root.Add(_script);
                }
                var row = new VisualElement(); row.AddToClassList("narrative-inspector__script-line");
                var num = new Label((++_lineNumber).ToString()); num.AddToClassList("narrative-inspector__script-num");
                row.Add(num);
                var body = new VisualElement(); body.style.flexGrow = 1;
                NodeSummary.DialogueLineRow(body, speakerId, isPlayer, textId, text, full: true);
                var meta = new List<string>();
                if (!string.IsNullOrEmpty(textId)) meta.Add(textId);
                if (!string.IsNullOrEmpty(detail)) meta.Add(detail);
                if (!string.IsNullOrEmpty(textId) && !NarrativeProjectIndex.HasLoc(textId)) meta.Add($"⚠ sin traducción {NarrativeProjectIndex.PrimaryLanguage}");
                if (meta.Count > 0) body.Add(Muted(string.Join(" · ", meta)));
                row.Add(body);
                _script.Add(row);
            }

            public void Item(UnityEngine.Object item, int amount, string verb) => AssetContent(_root, item, "objeto", $"{verb} ×{amount}");

            public string Text(string locKey, string fallback)
            {
                LocContent(_root, locKey, fallback);
                return NarrativeProjectIndex.Loc(locKey, fallback);
            }

            public void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal) { }
            public void Speaker(string name) { }
            public void Field(string label, string value) { }
            public void Tag(string text) { }
        }

        static void AssetContent(VisualElement root, UnityEngine.Object asset, string kind, string detail)
        {
            if (asset == null) { root.Add(Muted($"Sin {kind} asignado.")); return; }
            var head = new VisualElement(); head.AddToClassList("narrative-inspector__row");
            var name = new Label(asset.name); name.AddToClassList("narrative-inspector__asset-name");
            head.Add(name);
            head.Add(new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }) { text = "Abrir asset" });
            root.Add(head);
            if (!string.IsNullOrEmpty(detail)) root.Add(Muted(detail));

            var users = NarrativeProjectIndex.AssetUsers(asset);
            if (users.Count > 1) root.Add(Muted($"Usado también en: {string.Join(" · ", users.Take(4))}"));
        }

        void QuestContent(VisualElement root, string questId, IReadOnlyList<string> highlight)
        {
            if (string.IsNullOrEmpty(questId)) { root.Add(Muted("Sin questId.")); return; }
            var q = NarrativeProjectIndex.FindQuest(questId);
            if (q == null)
            {
                root.Add(Error($"No existe ninguna misión con id '{questId}'."));
                var similar = NarrativeProjectIndex.Quests.Keys.Where(k => k.IndexOf(questId, StringComparison.OrdinalIgnoreCase) >= 0 || questId.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0).Take(5).ToList();
                if (similar.Count > 0) root.Add(Muted("¿Querías decir? " + string.Join(", ", similar)));
                return;
            }
            var head = new VisualElement(); head.AddToClassList("narrative-inspector__row");
            var name = new Label(q.displayName); name.AddToClassList("narrative-inspector__asset-name");
            head.Add(name);
            head.Add(new Button(() => { Selection.activeObject = q.asset; EditorGUIUtility.PingObject(q.asset); }) { text = "Abrir asset" });
            root.Add(head);
            root.Add(Muted(q.questId + " · " + q.path));
            var desc = q.description;
            if (!string.IsNullOrEmpty(desc)) root.Add(new Label(desc) { name = "quest-desc" });

            if (q.stepConditionIds.Length == 0) { root.Add(Muted("Sin pasos.")); }
            for (int i = 0; i < q.stepConditionIds.Length; i++)
            {
                bool hi = highlight != null && highlight.Contains(q.stepConditionIds[i]);
                var row = new Label($"{(hi ? "☑" : "☐")} {i + 1}. {(i < q.stepDescriptions.Length ? q.stepDescriptions[i] : "")}   [{q.stepConditionIds[i]}]");
                row.AddToClassList("summary__step");
                if (hi) row.AddToClassList("summary__step--hi");
                root.Add(row);
            }

            // Quién más toca esta quest en los grafos
            var refs = NarrativeProjectIndex.QuestUsers(questId)
                .Where(u => u.Node != _current?.Model)
                .Select(u => u.Label)
                .ToList();
            if (refs.Count > 0)
            {
                root.Add(Muted("Otros nodos que la tocan:"));
                foreach (var r in refs.Take(12)) root.Add(Muted("• " + r));
            }
        }

        static void SignalContent(VisualElement root, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var s = NarrativeProjectIndex.FindSignal(key);
            var title = new Label("Señal " + key); title.AddToClassList("narrative-inspector__asset-name");
            root.Add(title);
            if (s == null) { root.Add(Muted("Nadie más la usa en el proyecto (todavía).")); return; }
            root.Add(Muted(s.emitters.Count == 0 ? "La emite: nadie ⚠" : "La emite: " + string.Join(" · ", s.emitters.Distinct())));
            root.Add(Muted(s.listeners.Count == 0 ? "La escucha: nadie" : "La escucha: " + string.Join(" · ", s.listeners.Distinct())));
        }

        static void ActorContent(VisualElement root, string id)
        {
            if (string.IsNullOrEmpty(id)) { root.Add(Muted("Sin npcId.")); return; }
            var a = NarrativeProjectIndex.FindActor(id);
            if (a == null) { root.Add(Error($"Ningún actor del proyecto tiene el id '{id}'.")); return; }
            var head = new VisualElement(); head.AddToClassList("narrative-inspector__row");
            head.Add(new Label($"🧑 {a.displayName}") { name = "actor-name" });
            head.Add(new Button(() =>
            {
                var source = AssetDatabase.LoadMainAssetAtPath(a.source);
                if (source != null) { Selection.activeObject = source; EditorGUIUtility.PingObject(source); }
            }) { text = "Abrir" });
            root.Add(head);
            root.Add(Muted(a.source));
        }

        static void LocContent(VisualElement root, string id, string fallback)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (!string.IsNullOrEmpty(fallback)) root.Add(new Label("«" + fallback + "» (texto directo, sin localizar)"));
                return;
            }
            var es = NarrativeProjectIndex.Loc(id);
            var en = NarrativeProjectIndex.LocEn(id);
            root.Add(new Label(id) { name = "loc-key" });
            var primary = NarrativeProjectIndex.PrimaryLanguage;
            var secondary = NarrativeProjectIndex.SecondaryLanguage;
            root.Add(es != null ? new Label($"{primary}: {es}") : Error($"{primary}: falta la traducción"));
            root.Add(en != null ? Muted($"{secondary}: {en}") : Error($"{secondary}: falta"));
        }

        // ─── Conexiones ─────────────────────────────────────────────────

        void BuildLinks(VisualElement root, NarrativeNode model)
        {
            var incoming = new List<(NarrativeNode from, int port)>();
            foreach (var n in _graph.nodes)
            {
                if (n == null || n.outputs == null) continue;
                for (int i = 0; i < n.outputs.Count; i++)
                    if (n.outputs[i] == model.guid) incoming.Add((n, i));
            }

            root.Add(Muted(incoming.Count == 0 ? "Entradas: ninguna" + (model.guid == _graph.startNodeGuid ? " (es el inicio)" : " ⚠ nodo inalcanzable") : "Entradas:"));
            foreach (var (from, port) in incoming)
            {
                var ports = from.GetOutputPorts();
                string via = ports != null && port < ports.Length ? $" (por '{ports[port]}')" : "";
                root.Add(LinkButton("← " + Describe(from) + via, from.guid));
            }

            var names = model.GetOutputPorts();
            if (names != null)
            {
                root.Add(Muted("Salidas:"));
                for (int i = 0; i < names.Length; i++)
                {
                    var g = model.GetOutputGuid(i);
                    var target = g != null ? _graph.FindNode(g) : null;
                    if (target != null) root.Add(LinkButton($"{names[i]} → {Describe(target)}", target.guid));
                    else root.Add(Muted($"{names[i]} → (sin conectar)"));
                }
            }
            else
            {
                var outs = (model.outputs ?? new List<string>()).Where(g => !string.IsNullOrEmpty(g)).ToList();
                root.Add(Muted(outs.Count == 0 ? "Salidas: ninguna (fin de flujo)" : outs.Count > 1 ? $"Salidas: {outs.Count} EN PARALELO (fork)" : "Salida:"));
                foreach (var g in outs)
                {
                    var target = _graph.FindNode(g);
                    root.Add(target != null ? LinkButton("→ " + Describe(target), target.guid) : Error("→ nodo borrado " + g));
                }
            }
        }

        Button LinkButton(string text, string guid)
        {
            var b = new Button(() => NavigateToNode?.Invoke(guid)) { text = text };
            b.AddToClassList("narrative-inspector__link");
            return b;
        }

        static string Describe(NarrativeNode n)
        {
            var t = NarrativeNodeCatalog.TitleOf(n.GetType());
            return string.IsNullOrEmpty(n.displayTitle) ? t : $"{t} '{NodeSummary.Truncate(n.displayTitle, 40)}'";
        }

        static Label Muted(string text) { var l = new Label(text); l.AddToClassList("summary__muted"); return l; }
        static Label Error(string text) { var l = new Label(text); l.AddToClassList("summary__error"); return l; }

        static IEnumerable<SerializedProperty> EnumerateDirectChildren(SerializedProperty parent)
        {
            var copy = parent.Copy();
            var end = copy.GetEndProperty();
            bool enterChildren = true;
            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                if (copy.depth == parent.depth + 1)
                    yield return copy.Copy();
            }
        }
    }
}
