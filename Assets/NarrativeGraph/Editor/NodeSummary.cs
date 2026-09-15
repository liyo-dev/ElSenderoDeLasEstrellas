using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#pragma warning disable 618 // se referencian nodos [Obsolete] a propósito para seguir mostrando grafos viejos
namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Construye el resumen legible que se muestra DENTRO de la tarjeta de cada nodo (texto real de
    /// los diálogos, nombre y pasos de la quest, quién emite/escucha una señal...) y la lista de
    /// avisos de validación del nodo. Todo se resuelve contra NarrativeProjectIndex.
    /// </summary>
    public static class NodeSummary
    {
        const int MaxDialogueLinesInCard = 3;
        const int MaxCharsPerLine = 90;

        public static VisualElement Build(NarrativeNode node, NarrativeGraph graph)
        {
            var root = new VisualElement();
            root.AddToClassList("narrative-summary");

            switch (node)
            {
                case PlayDialogueNode pd: BuildDialogue(root, pd); break;
                case DialogueChoiceNode dc: BuildChoice(root, dc); break;
                case ShowSpeechBubbleNode sb:
                    Speaker(root, string.IsNullOrEmpty(sb.speakerName) ? (sb.targetTag == "Player" ? "Will" : sb.targetTag) : sb.speakerName);
                    Line(root, Quote(NarrativeProjectIndex.Loc(sb.textId, sb.text)));
                    break;
                case TutorialPromptNode tp:
                    Line(root, "🎮 " + (NarrativeProjectIndex.Loc(tp.textId, tp.text) ?? ""));
                    if (!string.IsNullOrEmpty(tp.buttonName)) Chip(root, tp.buttonName, "chip--muted");
                    break;
                case StartQuestNode sq: BuildQuest(root, sq.questId, null, "Se inicia"); break;
                case WaitQuestCompleteNode wq: BuildQuest(root, wq.questId, null, "Espera a que se complete"); break;
                case BranchQuestStateNode bq: BuildQuest(root, bq.questId, null, "Bifurca según"); break;
                case CompleteQuestStepsNode cq: BuildQuest(root, cq.questId, cq.stepConditionIds, cq.completeQuest ? "Completa pasos y CIERRA" : "Completa pasos"); break;
                case DeliverQuestCompleteNode dq: BuildQuest(root, dq.questId, null, "Entrega"); break;
                case RequireInventoryItemNode rq:
                    ItemLine(root, rq.item, rq.requiredAmount, rq.consumeOnSuccess ? "requiere y consume" : "requiere");
                    if (!string.IsNullOrEmpty(rq.questId)) BuildQuest(root, rq.questId, null, "Quest");
                    break;
                case GiveInventoryItemNode gi: ItemLine(root, gi.item, gi.amount, "da"); break;
                case WaitForItemAddedNode wi: ItemLine(root, wi.itemToWaitFor, wi.minimumAmount, "espera"); break;
                case WaitCustomEventNode we: BuildSignal(root, we.eventKey, listening: true); break;
                case RaiseCustomEventNode re: BuildSignal(root, re.eventKey, listening: false); break;
                case WaitNpcInteractionNode wn: BuildActor(root, wn.npcId, "Espera a que el jugador hable con"); break;
                case PlayCinematicNode pc:
                    Line(root, "🎬 " + (string.IsNullOrEmpty(pc.cinematicName) ? "(sin nombre)" : pc.cinematicName), "summary__strong");
                    KeyValue(root, "entra", pc.signalIn); KeyValue(root, "hecho", pc.signalDone);
                    if (!string.IsNullOrEmpty(pc.signalFailed)) KeyValue(root, "fallo", pc.signalFailed);
                    break;
                case SetFlagNode sf: Line(root, $"⚑ {sf.flagKey} = {(sf.value ? "sí" : "no")}"); break;
                case BranchFlagNode bf: Line(root, $"⚑ ¿{bf.flagKey}?{(bf.invert ? " (invertido)" : "")}"); break;
                case CheckpointNode cp:
                    Line(root, "⚑ " + (string.IsNullOrEmpty(cp.checkpointId) ? "(sin id)" : cp.checkpointId), "summary__strong");
                    if (!string.IsNullOrEmpty(cp.spawnAnchorId)) KeyValue(root, "anchor", cp.spawnAnchorId);
                    if (!string.IsNullOrEmpty(cp.sceneName)) KeyValue(root, "escena", cp.sceneName);
                    break;
                case StartBattleNode sbn:
                    Line(root, "⚔ " + (string.IsNullOrEmpty(sbn.battleId) ? "(sin battleId)" : sbn.battleId), "summary__strong");
                    break;
                case GraphNoteNode note:
                    var nl = new Label(note.note ?? "");
                    nl.AddToClassList("summary__note");
                    root.Add(nl);
                    break;
                case StartNode:
                case ForkNode:
                    break;
                default:
                    BuildGeneric(root, node);
                    break;
            }

            return root.childCount > 0 ? root : null;
        }

        // ─── constructores por tipo ─────────────────────────────────────

        static void BuildDialogue(VisualElement root, PlayDialogueNode pd)
        {
            if (pd.dialogue == null)
            {
                Line(root, "(sin DialogueAsset)", "summary__muted");
                return;
            }
            var header = new VisualElement();
            header.AddToClassList("summary__dialogue-header");
            var name = new Label(pd.dialogue.name);
            name.AddToClassList("summary__asset-name");
            header.Add(name);
            int count = pd.dialogue.lines?.Length ?? 0;
            var meta = new Label($"{count} línea{(count == 1 ? "" : "s")}{(pd.dialogue.isGroupConversation ? " · grupal" : "")}{(!string.IsNullOrEmpty(pd.npcId) ? " · " + pd.npcId : "")}");
            meta.AddToClassList("summary__muted");
            header.Add(meta);
            root.Add(header);

            if (pd.dialogue.lines == null) return;
            int shown = 0;
            foreach (var line in pd.dialogue.lines)
            {
                if (shown++ >= MaxDialogueLinesInCard)
                {
                    Line(root, $"… +{count - MaxDialogueLinesInCard} más", "summary__muted");
                    break;
                }
                DialogueLineRow(root, line);
            }
        }

        public static void DialogueLineRow(VisualElement root, DialogueLine line, bool full = false)
        {
            var row = new VisualElement();
            row.AddToClassList("summary__line");
            var speaker = new Label(NarrativeProjectIndex.SpeakerName(line.speakerNameId, line.isPlayerSpeaking) + ":");
            speaker.AddToClassList("summary__speaker");
            speaker.AddToClassList(line.isPlayerSpeaking ? "summary__speaker--player" : "summary__speaker--npc");
            row.Add(speaker);
            var text = NarrativeProjectIndex.LineText(line);
            var t = new Label(full ? text : Truncate(text, MaxCharsPerLine));
            t.AddToClassList("summary__text");
            row.Add(t);
            root.Add(row);
        }

        static void BuildChoice(VisualElement root, DialogueChoiceNode dc)
        {
            Line(root, "❓ " + Quote(NarrativeProjectIndex.Loc(dc.promptTextId, dc.promptText)), "summary__strong");
            Line(root, "A → " + (NarrativeProjectIndex.Loc(dc.optionATextId, dc.optionAText) ?? "A"));
            Line(root, "B → " + (NarrativeProjectIndex.Loc(dc.optionBTextId, dc.optionBText) ?? "B"));
        }

        static void BuildQuest(VisualElement root, string questId, List<string> highlightSteps, string verb)
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
                Line(root, "no existe ningún QuestData con ese id", "summary__error");
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

        static void ItemLine(VisualElement root, ItemData item, int amount, string verb)
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
                string text = Describe(v);
                if (string.IsNullOrEmpty(text)) continue;
                KeyValue(root, ObjectNames.NicifyVariableName(f.Name), text);
                if (++shown >= 5) break;
            }
        }

        static string Describe(object v)
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

        static string Quote(string s) => string.IsNullOrEmpty(s) ? "(sin texto)" : "«" + Truncate(s, MaxCharsPerLine) + "»";

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

            switch (node)
            {
                case PlayDialogueNode pd when pd.dialogue == null: issues.Add("Sin DialogueAsset"); break;
                case StartQuestNode sq when string.IsNullOrEmpty(sq.questId): issues.Add("questId vacío"); break;
                case WaitCustomEventNode we when string.IsNullOrEmpty(we.eventKey): issues.Add("eventKey vacío"); break;
                case RaiseCustomEventNode re when string.IsNullOrEmpty(re.eventKey): issues.Add("eventKey vacío"); break;
                case WaitNpcInteractionNode wn when string.IsNullOrEmpty(wn.npcId): issues.Add("npcId vacío"); break;
                case PlayCinematicNode pc when string.IsNullOrEmpty(pc.signalIn) || string.IsNullOrEmpty(pc.signalDone): issues.Add("Faltan señales de entrada/fin"); break;
                case CheckpointNode cp when string.IsNullOrEmpty(cp.checkpointId): issues.Add("checkpointId vacío"); break;
            }

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
