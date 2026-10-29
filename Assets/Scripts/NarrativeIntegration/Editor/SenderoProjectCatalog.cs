using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Lo que el índice del editor del grafo (NarrativeProjectIndex) necesita saber de El Sendero:
    /// textos (Resources/Localization, ES y EN), misiones (QuestData), NPCs de los prefabs
    /// (persistenceId), señales de las secuencias (SequenceDefinition) y las convenciones de nombres
    /// de campo de sus escenas (_signalIn/_signalOut de los sequencers, eventKey de los emisores,
    /// anchorId de los SpawnAnchor).
    /// </summary>
    public sealed class SenderoProjectCatalog : INarrativeProjectCatalog
    {
        const string LocalizationFolder = "Assets/Resources/Localization";
        static readonly string[] LocCatalogs = { "dialogues", "quests", "ui", "cinematics", "prologue", "other" };
        static readonly string[] NpcPrefabFolders = { "Assets/_NPCs", "Assets/Prefabs", "Assets/_PREFABS" };

        public void Contribute(NarrativeProjectIndex.Contributions into)
        {
            into.LanguageNames("ES", "EN");

            // Carpetas enormes que nunca aportan nada: maquetas generadas por los builders de Eldoria,
            // cosas apartadas para borrar y versiones antiguas. (MainWorld_old sí se lee: queda en caché.)
            into.ExcludePaths("/EldoriaCodex_", "/CatalogoAssets_", "/_to_delete/", "/Versiones antiguas/");
            into.ScanScriptsIn("Assets/Scripts");

            into.SceneField("_signalIn", NarrativeKeyKind.Signal, SignalRole.Escucha, "Sequencer en");
            into.SceneField("_signalOut", NarrativeKeyKind.Signal, SignalRole.Emite, "Sequencer en");
            into.SceneField("eventKey", NarrativeKeyKind.Signal, SignalRole.Emite, "Emisor en");
            into.SceneField("narrativeEventKey", NarrativeKeyKind.Signal, SignalRole.Emite, "Emisor en");
            into.SceneField("defeatEventKey", NarrativeKeyKind.Signal, SignalRole.Emite, "Emisor en");
            into.SceneField("anchorId", NarrativeKeyKind.Anchor);

            LoadLocalization("es", into.Text);
            LoadLocalization("en", into.SecondaryText);
            AddQuests(into);
            AddNpcs(into);
            AddSequenceDefinitions(into);
        }

        static void AddQuests(NarrativeProjectIndex.Contributions into)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:QuestData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var q = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                into.Quest(
                    q.questId,
                    into.Loc(q.displayNameId, string.IsNullOrEmpty(q.displayName) ? q.questId : q.displayName),
                    into.Loc(q.descriptionId, q.description),
                    q,
                    path,
                    q.steps?.Select(s => s?.conditionId ?? "").ToArray() ?? Array.Empty<string>(),
                    q.steps?.Select(s => into.Loc(s?.descriptionId, s?.description ?? "")).ToArray() ?? Array.Empty<string>());
            }
        }

        /// NPCs (persistenceId) de los prefabs. Cargar un prefab entero solo para leer un string es
        /// caro: el índice guarda lo leído y solo se vuelve a abrir si el prefab ha cambiado.
        /// Valores guardados: [nombre del prefab, id, id…].
        static void AddNpcs(NarrativeProjectIndex.Contributions into)
        {
            var folders = NpcPrefabFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (into.IsExcluded(path)) continue;
                var values = into.CachedFileValues(path, () => ReadNpcIds(path));
                if (values.Length == 0) continue;
                string displayName = values[0];
                for (int i = 1; i < values.Length; i++)
                {
                    var id = values[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    into.Actor(id, path, displayName);
                    into.AddSignalEmitter(WaitNpcInteractionNode.SignalKeyFor(id), $"NPC {displayName} (al hablarle)");
                }
            }
        }

        static string[] ReadNpcIds(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return Array.Empty<string>();
            var values = new List<string> { go.name };
            foreach (var m in go.GetComponentsInChildren<Game.NPC.NPCBehaviourManagerV2>(true))
            {
                var id = m.PersistenceId;
                if (!string.IsNullOrEmpty(id)) values.Add(id);
            }
            return values.ToArray();
        }

        static readonly Regex YamlBeatSignal = new(@"^\s*(?:signal|signalOutOverride):\s*(\S+)\s*$", RegexOptions.Compiled);

        /// Secuencias reutilizables: SequencePlayer lee signalIn/signalOut de un SequenceDefinition
        /// compartido (no de campos de la escena), y sus beats pueden levantar más señales (SignalBeat,
        /// o EndSequenceBeat con otra salida para la rama de fallo).
        static void AddSequenceDefinitions(NarrativeProjectIndex.Contributions into)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SequenceDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (into.IsExcluded(path)) continue;
                var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(path);
                if (def == null) continue;
                string label = string.IsNullOrEmpty(def.name) ? Path.GetFileNameWithoutExtension(path) : def.name;
                if (!string.IsNullOrEmpty(def.signalIn)) into.AddSignalListener(def.signalIn, $"SequencePlayer con {label}");
                if (!string.IsNullOrEmpty(def.signalOut)) into.AddSignalEmitter(def.signalOut, $"SequencePlayer con {label}");

                try
                {
                    foreach (var linea in File.ReadLines(path))
                    {
                        var m = YamlBeatSignal.Match(linea);
                        if (m.Success) into.AddSignalEmitter(Clean(m.Groups[1].Value), $"SequencePlayer con {label}");
                    }
                }
                catch { /* ilegible: se queda con las señales de entrada y salida */ }
            }
        }

        static string Clean(string v)
        {
            v = v.Trim();
            if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
                v = v.Substring(1, v.Length - 2);
            return v;
        }

        [Serializable] class LocData { public LocTextEntry[] texts; public LocSubEntry[] subtitles; }
        [Serializable] class LocTextEntry { public string key; public string value; }
        [Serializable] class LocSubEntry { public string id; public string text; }

        static void LoadLocalization(string locale, Action<string, string> add)
        {
            foreach (var catalog in LocCatalogs)
            {
                var path = Path.Combine(LocalizationFolder, $"{catalog}_{locale}.json");
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<LocData>(File.ReadAllText(path));
                    if (data?.texts != null)
                        foreach (var e in data.texts)
                            if (!string.IsNullOrEmpty(e.key)) add(e.key, e.value);
                    if (data?.subtitles != null)
                        foreach (var e in data.subtitles)
                            if (!string.IsNullOrEmpty(e.id)) add(e.id, e.text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SenderoProjectCatalog] No se pudo leer {path}: {ex.Message}");
                }
            }
        }
    }
}
