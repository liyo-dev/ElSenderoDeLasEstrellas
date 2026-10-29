using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// <summary>
    /// Construye el resumen legible que se muestra DENTRO de la tarjeta de cada nodo (texto real de
    /// los diálogos, nombre y pasos de la quest, quién emite/escucha una señal...) y la lista de
    /// avisos de validación del nodo. Lo que se enseña lo declara cada nodo en
    /// NarrativeNode.Describe; aquí solo se pinta, resolviendo las claves contra NarrativeProjectIndex.
    /// Un nodo que no se describe enseña sus primeros campos.
    /// </summary>
    public static class NodeSummary
    {
        const int MaxDialogueLinesInCard = 3;
        const int MaxCharsPerLine = 90;
        const int MaxCharsPerFreeLine = 160;

        public static VisualElement Build(NarrativeNode node, NarrativeGraph graph)
        {
            var root = new VisualElement();
            root.AddToClassList("narrative-summary");

            var card = new CardDescriber(root);
            node.Describe(card);
            if (root.childCount == 0) BuildGeneric(root, node);

            if (node is INarrativeStandingLine frase && !string.IsNullOrEmpty(frase.StandingActorId))
            {
                BuildActor(root, frase.StandingActorId, "Mientras tanto, si le hablas:");
                Line(root, frase.StandingDialogue != null ? "💬 " + frase.StandingDialogue.name : "(sin diálogo: no dice nada)", "summary__muted");
            }

            return root.childCount > 0 ? root : null;
        }

        /// <summary>Pinta en la tarjeta lo que declara el nodo.</summary>
        sealed class CardDescriber : INarrativeNodeDescriber
        {
            readonly VisualElement _root;
            int _dialogueLines;
            Label _moreLines;

            public CardDescriber(VisualElement root) { _root = root; }

            public void Quest(string questId, string verb, IReadOnlyList<string> steps = null) => BuildQuest(_root, questId, steps, verb);

            public void Signal(string key, SignalRole role, string label = null)
            {
                if (label != null) KeyValue(_root, label, key);
                else BuildSignal(_root, key, listening: role == SignalRole.Escucha);
            }

            public void Actor(string actorId, string verb) => BuildActor(_root, actorId, verb);

            public void Flag(string key, string shownAs = null) => NodeSummary.Line(_root, "⚑ " + (shownAs ?? key));

            public void Anchor(string anchorId) => KeyValue(_root, "anchor", anchorId);

            public void Asset(UnityEngine.Object asset, string kind, string detail = null)
            {
                _dialogueLines = 0;
                _moreLines = null;
                if (asset == null)
                {
                    NodeSummary.Line(_root, $"(sin {kind})", "summary__muted");
                    return;
                }
                var header = new VisualElement();
                header.AddToClassList("summary__dialogue-header");
                var name = new Label(asset.name);
                name.AddToClassList("summary__asset-name");
                header.Add(name);
                if (!string.IsNullOrEmpty(detail))
                {
                    var meta = new Label(detail);
                    meta.AddToClassList("summary__muted");
                    header.Add(meta);
                }
                _root.Add(header);
            }

            public void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null)
            {
                _dialogueLines++;
                if (_dialogueLines <= MaxDialogueLinesInCard)
                {
                    DialogueLineRow(_root, speakerId, isPlayer, textId, text);
                    return;
                }
                if (_moreLines == null)
                {
                    _moreLines = new Label();
                    _moreLines.AddToClassList("summary__text");
                    _moreLines.AddToClassList("summary__muted");
                    _root.Add(_moreLines);
                }
                _moreLines.text = $"… +{_dialogueLines - MaxDialogueLinesInCard} más";
            }

            public void Item(UnityEngine.Object item, int amount, string verb) => ItemLine(_root, item, amount, verb);

            public string Text(string locKey, string fallback) => NarrativeProjectIndex.Loc(locKey, fallback);

            public void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal)
            {
                if (tone == NarrativeTextTone.Note)
                {
                    var nl = new Label(text ?? "");
                    nl.AddToClassList("summary__note");
                    _root.Add(nl);
                    return;
                }
                string cls = tone == NarrativeTextTone.Strong ? "summary__strong" : tone == NarrativeTextTone.Muted ? "summary__muted" : null;
                NodeSummary.Line(_root, Truncate(text, MaxCharsPerFreeLine), cls);
            }

            public void Speaker(string name) => NodeSummary.Speaker(_root, name);
            public void Field(string label, string value) => KeyValue(_root, label, value);
            public void Tag(string text) => Chip(_root, text, "chip--muted");
        }

        // ─── piezas de la tarjeta ───────────────────────────────────────

        /// <summary>Fila «Hablante: texto» de una línea de diálogo, resuelta contra la localización.</summary>
        public static void DialogueLineRow(VisualElement root, string speakerId, bool isPlayer, string textId, string text, bool full = false)
        {
            var row = new VisualElement();
            row.AddToClassList("summary__line");
            var speaker = new Label(NarrativeProjectIndex.SpeakerName(speakerId, isPlayer) + ":");
            speaker.AddToClassList("summary__speaker");
            speaker.AddToClassList(isPlayer ? "summary__speaker--player" : "summary__speaker--npc");
            row.Add(speaker);
            var resolved = NarrativeProjectIndex.LineText(textId, text);
            var t = new Label(full ? resolved : Truncate(resolved, MaxCharsPerLine));
            t.AddToClassList("summary__text");
            row.Add(t);
            root.Add(row);
        }

        static void BuildQuest(VisualElement root, string questId, IReadOnlyList<string> highlightSteps, string verb)
        {
            if (string.IsNullOrEmpty(questId))
            {
                Line(root, "(sin questId)", "summary__muted");
                return;
            }
            var q = NarrativeProjectIndex.FindQuest(questId);
            if (q == null)
            {
                Line(root, $"{verb}: {questId}");
                Line(root, "no existe ninguna misión con ese id", "summary__error");
                return;
            }
            var head = new Label($"{verb}: {q.displayName}");
            head.AddToClassList("summary__strong");
            head.tooltip = q.path;
            root.Add(head);
            var idl = new Label(questId);
            idl.AddToClassList("summary__muted");
            root.Add(idl);

            if (q.stepConditionIds.Length > 0)
            {
                for (int i = 0; i < q.stepConditionIds.Length; i++)
                {
                    bool hi = highlightSteps != null && highlightSteps.Contains(q.stepConditionIds[i]);
                    var desc = i < q.stepDescriptions.Length && !string.IsNullOrEmpty(q.stepDescriptions[i]) ? q.stepDescriptions[i] : q.stepConditionIds[i];
                    var step = new Label($"{(hi ? "☑" : "☐")} {i + 1}. {Truncate(desc, 60)}");
                    step.AddToClassList("summary__step");
                    if (hi) step.AddToClassList("summary__step--hi");
                    step.tooltip = q.stepConditionIds[i];
                    root.Add(step);
                }
            }
        }

        static void BuildSignal(VisualElement root, string key, bool listening)
        {
            if (string.IsNullOrEmpty(key))
            {
                Line(root, "(sin clave)", "summary__muted");
                return;
            }
            Chip(root, (listening ? "⏳ " : "📤 ") + key, listening ? "chip--wait" : "chip--raise");
            var s = NarrativeProjectIndex.FindSignal(key);
            if (s == null) return;
            if (listening)
            {
                var others = s.emitters.Count == 0 ? "nadie la emite" : "la emite: " + string.Join(" · ", s.emitters.Take(3)) + (s.emitters.Count > 3 ? " …" : "");
                Line(root, others, s.emitters.Count == 0 ? "summary__error" : "summary__muted");
            }
            else
            {
                var others = s.listeners.Count == 0 ? "nadie la escucha" : "la escucha: " + string.Join(" · ", s.listeners.Take(3)) + (s.listeners.Count > 3 ? " …" : "");
                Line(root, others, s.listeners.Count == 0 ? "summary__warn" : "summary__muted");
            }
        }

        static void BuildActor(VisualElement root, string id, string verb)
        {
            if (string.IsNullOrEmpty(id)) { Line(root, "(sin npcId)", "summary__muted"); return; }
            var a = NarrativeProjectIndex.FindActor(id);
            Line(root, $"{verb} 🧑 {(a != null ? a.displayName : id)}", "summary__strong");
            if (a != null && a.displayName != id) Line(root, id, "summary__muted");
        }

        static void ItemLine(VisualElement root, UnityEngine.Object item, int amount, string verb)
        {
            string name = item != null ? item.name : "(sin objeto)";
            Line(root, $"🎒 {verb} ×{amount} {name}");
        }

        static void BuildGeneric(VisualElement root, NarrativeNode node)
        {
            int shown = 0;
            foreach (var f in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.DeclaringType == typeof(NarrativeNode)) continue;
                if (f.GetCustomAttribute<HideInInspector>() != null) continue;
                object v;
                try { v = f.GetValue(node); } catch { continue; }
                string text = DescribeValue(v);
                if (string.IsNullOrEmpty(text)) continue;
                KeyValue(root, ObjectNames.NicifyVariableName(f.Name), text);
                if (++shown >= 5) break;
            }
        }

        static string DescribeValue(object v)
        {
            switch (v)
            {
                case null: return null;
                case string s: return string.IsNullOrEmpty(s) ? null : s;
                case bool b: return b ? "sí" : "no";
                case UnityEngine.Object o: return o != null ? o.name : null;
                case System.Collections.IList list: return list.Count == 0 ? null : $"{list.Count} elementos";
                case Enum e: return e.ToString();
                case float f: return f.ToString("0.##");
                case int i: return i.ToString();
                default: return v.ToString();
            }
        }

        // ─── helpers UI ─────────────────────────────────────────────────

        static void Speaker(VisualElement root, string who)
        {
            var l = new Label(who + ":");
            l.AddToClassList("summary__speaker");
            root.Add(l);
        }

        static void Line(VisualElement root, string text, string extraClass = null)
        {
            var l = new Label(text ?? "");
            l.AddToClassList("summary__text");
            if (extraClass != null) l.AddToClassList(extraClass);
            root.Add(l);
        }

        static void KeyValue(VisualElement root, string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("summary__kv");
            var k = new Label(key + ":"); k.AddToClassList("summary__kv-key");
            var v = new Label(value ?? ""); v.AddToClassList("summary__kv-value");
            row.Add(k); row.Add(v);
            root.Add(row);
        }

        static void Chip(VisualElement root, string text, string extraClass = null)
        {
            var c = new Label(text);
            c.AddToClassList("chip");
            if (extraClass != null) c.AddToClassList(extraClass);
            root.Add(c);
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        // ─── validación por nodo ────────────────────────────────────────

        public static List<string> Validate(NarrativeNode node, NarrativeGraph graph)
        {
            var issues = new List<string>();
            if (node == null) return issues;

            foreach (var f in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var key = f.GetCustomAttribute<NarrativeKeyAttribute>();
                if (key == null) continue;
                string questId = null;
                if (key.Kind == NarrativeKeyKind.QuestStep && !string.IsNullOrEmpty(key.QuestIdField))
                    questId = node.GetType().GetField(key.QuestIdField)?.GetValue(node) as string;

                object val = f.GetValue(node);
                if (val is string s)
                {
                    if (!string.IsNullOrEmpty(s) && !NarrativeProjectIndex.Resolves(key.Kind, s, questId))
                        issues.Add($"{ObjectNames.NicifyVariableName(f.Name)} '{s}' no existe ({KindName(key.Kind)})");
                }
                else if (val is IEnumerable<string> list)
                {
                    foreach (var item in list)
                        if (!string.IsNullOrEmpty(item) && !NarrativeProjectIndex.Resolves(key.Kind, item, questId))
                            issues.Add($"'{item}' no es un paso de {questId ?? "?"}");
                }
            }

            node.CollectWarnings(issues);

            // Puertos con nombre sin conectar (aviso, no error: puede ser fin de flujo a propósito)
            var ports = node.GetOutputPorts();
            if (ports != null && node.outputs != null)
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    var g = i < node.outputs.Count ? node.outputs[i] : null;
                    if (!string.IsNullOrEmpty(g) && graph != null && graph.FindNode(g) == null)
                        issues.Add($"Salida '{ports[i]}' apunta a un nodo borrado");
                }
            }
            else if (node.outputs != null && graph != null)
            {
                foreach (var g in node.outputs)
                    if (!string.IsNullOrEmpty(g) && graph.FindNode(g) == null)
                        issues.Add("Una salida apunta a un nodo borrado");
            }

            return issues;
        }

        static string KindName(NarrativeKeyKind k) => k switch
        {
            NarrativeKeyKind.Quest => "quest",
            NarrativeKeyKind.QuestStep => "paso",
            NarrativeKeyKind.Signal => "señal",
            NarrativeKeyKind.Actor => "NPC",
            NarrativeKeyKind.LocKey => "clave de texto",
            NarrativeKeyKind.Anchor => "anchor",
            _ => k.ToString()
        };
    }
}
