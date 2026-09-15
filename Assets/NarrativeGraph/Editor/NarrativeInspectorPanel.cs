using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#pragma warning disable 618 // se referencian nodos [Obsolete] a propósito para seguir mostrando grafos viejos
namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Panel lateral derecho del editor: muestra y edita el nodo seleccionado.
    ///
    /// Tres bloques: (1) los campos del nodo con los desplegables de NarrativeKeyDrawer;
    /// (2) el CONTENIDO resuelto — guion completo del diálogo, ficha de la quest con sus pasos,
    /// quién emite/escucha una señal — para no tener que abrir el asset aparte; (3) conexiones
    /// entrantes y salientes con botones para saltar a ellas.
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

        // ─── Contenido resuelto ─────────────────────────────────────────

        void BuildContent(VisualElement root, NarrativeNode model)
        {
            switch (model)
            {
                case PlayDialogueNode pd: DialogueContent(root, pd.dialogue); break;
                case StartQuestNode sq: QuestContent(root, sq.questId, null); break;
                case WaitQuestCompleteNode wq: QuestContent(root, wq.questId, null); break;
                case BranchQuestStateNode bq: QuestContent(root, bq.questId, null); break;
                case CompleteQuestStepsNode cq: QuestContent(root, cq.questId, cq.stepConditionIds); break;
                case DeliverQuestCompleteNode dq: QuestContent(root, dq.questId, null); break;
                case RequireInventoryItemNode rq when !string.IsNullOrEmpty(rq.questId): QuestContent(root, rq.questId, null); break;
                case WaitCustomEventNode we: SignalContent(root, we.eventKey); break;
                case RaiseCustomEventNode re: SignalContent(root, re.eventKey); break;
                case PlayCinematicNode pc:
                    SignalContent(root, pc.signalIn); SignalContent(root, pc.signalDone);
                    if (!string.IsNullOrEmpty(pc.signalFailed)) SignalContent(root, pc.signalFailed);
                    break;
                case WaitNpcInteractionNode wn: ActorContent(root, wn.npcId); SignalContent(root, WaitNpcInteractionNode.SignalKeyFor(wn.npcId)); break;
                case ShowSpeechBubbleNode sb: LocContent(root, sb.textId, sb.text); break;
                case TutorialPromptNode tp: LocContent(root, tp.textId, tp.text); break;
                case DialogueChoiceNode dc:
                    LocContent(root, dc.promptTextId, dc.promptText);
                    LocContent(root, dc.optionATextId, dc.optionAText);
                    LocContent(root, dc.optionBTextId, dc.optionBText);
                    break;
                default:
                    root.Add(Muted("Este nodo no referencia contenido externo."));
                    break;
            }
        }

        void DialogueContent(VisualElement root, DialogueAsset asset)
        {
            if (asset == null) { root.Add(Muted("Sin DialogueAsset asignado.")); return; }
            var head = new VisualElement(); head.AddToClassList("narrative-inspector__row");
            var name = new Label(asset.name); name.AddToClassList("narrative-inspector__asset-name");
            head.Add(name);
            head.Add(new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }) { text = "Abrir asset" });
            root.Add(head);

            var users = NarrativeProjectIndex.DialogueUsers(asset);
            if (users.Count > 1) root.Add(Muted($"Usado también en: {string.Join(" · ", users.Where(u => true).Take(4))}"));

            if (asset.lines == null || asset.lines.Length == 0) { root.Add(Muted("Diálogo vacío.")); return; }
            var script = new VisualElement(); script.AddToClassList("narrative-inspector__script");
            for (int i = 0; i < asset.lines.Length; i++)
            {
                var line = asset.lines[i];
                var row = new VisualElement(); row.AddToClassList("narrative-inspector__script-line");
                var num = new Label((i + 1).ToString()); num.AddToClassList("narrative-inspector__script-num");
                row.Add(num);
                var body = new VisualElement(); body.style.flexGrow = 1;
                NodeSummary.DialogueLineRow(body, line, full: true);
                var meta = new List<string>();
                if (!string.IsNullOrEmpty(line.textId)) meta.Add(line.textId);
                if (line.emotion != default) meta.Add("emoción: " + line.emotion);
                if (!string.IsNullOrEmpty(line.textId) && !NarrativeProjectIndex.HasLoc(line.textId)) meta.Add("⚠ sin traducción ES");
                if (meta.Count > 0) body.Add(Muted(string.Join(" · ", meta)));
                row.Add(body);
                script.Add(row);
            }
            root.Add(script);
        }

        void QuestContent(VisualElement root, string questId, List<string> highlight)
        {
            if (string.IsNullOrEmpty(questId)) { root.Add(Muted("Sin questId.")); return; }
            var q = NarrativeProjectIndex.FindQuest(questId);
            if (q == null)
            {
                root.Add(Error($"No existe ningún QuestData con questId '{questId}'."));
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
            var desc = NarrativeProjectIndex.Loc(q.asset.descriptionId, q.asset.description);
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
            var refs = new List<string>();
            foreach (var g in NarrativeProjectIndex.Graphs)
                foreach (var n in g.nodes)
                {
                    if (n == null || n == _current?.Model) continue;
                    string qid = n switch
                    {
                        StartQuestNode s => s.questId,
                        WaitQuestCompleteNode w => w.questId,
                        CompleteQuestStepsNode c => c.questId,
                        BranchQuestStateNode b => b.questId,
                        DeliverQuestCompleteNode d => d.questId,
                        _ => null
                    };
                    if (qid == questId) refs.Add($"{g.name} › {NarrativeNodeCatalog.TitleOf(n.GetType())}{(string.IsNullOrEmpty(n.displayTitle) ? "" : " '" + n.displayTitle + "'")}");
                }
            if (refs.Count > 0)
            {
                root.Add(Muted("Otros nodos que la tocan:"));
                foreach (var r in refs.Take(12)) root.Add(Muted("• " + r));
            }
        }

        void SignalContent(VisualElement root, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var s = NarrativeProjectIndex.FindSignal(key);
            var title = new Label("Señal " + key); title.AddToClassList("narrative-inspector__asset-name");
            root.Add(title);
            if (s == null) { root.Add(Muted("Nadie más la usa en el proyecto (todavía).")); return; }
            root.Add(Muted(s.emitters.Count == 0 ? "La emite: nadie ⚠" : "La emite: " + string.Join(" · ", s.emitters.Distinct())));
            root.Add(Muted(s.listeners.Count == 0 ? "La escucha: nadie" : "La escucha: " + string.Join(" · ", s.listeners.Distinct())));
        }

        void ActorContent(VisualElement root, string id)
        {
            if (string.IsNullOrEmpty(id)) { root.Add(Muted("Sin npcId.")); return; }
            var a = NarrativeProjectIndex.FindActor(id);
            if (a == null) { root.Add(Error($"Ningún prefab de NPC tiene persistenceId '{id}'.")); return; }
            var head = new VisualElement(); head.AddToClassList("narrative-inspector__row");
            head.Add(new Label($"🧑 {a.displayName}") { name = "actor-name" });
            head.Add(new Button(() =>
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(a.source);
                if (go != null) { Selection.activeObject = go; EditorGUIUtility.PingObject(go); }
            }) { text = "Abrir prefab" });
            root.Add(head);
            root.Add(Muted(a.source));
        }

        void LocContent(VisualElement root, string id, string fallback)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (!string.IsNullOrEmpty(fallback)) root.Add(new Label("«" + fallback + "» (texto directo, sin localizar)"));
                return;
            }
            var es = NarrativeProjectIndex.Loc(id);
            var en = NarrativeProjectIndex.LocEn(id);
            root.Add(new Label(id) { name = "loc-key" });
            root.Add(es != null ? new Label("ES: " + es) : Error("ES: falta en dialogues_es.json / ui_es.json"));
            root.Add(en != null ? Muted("EN: " + en) : Error("EN: falta"));
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
