using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Configuración de una prueba del grafo desde un nodo. Es la misma que usa Quick Test:
    /// cuerpo del jugador (nivel, vida, maná, habilidades y hechizos), punto de aparición,
    /// Fast-Forward y restauración del boot preset al salir.
    /// </summary>
    [Serializable]
    public sealed class NarrativeTestConfig
    {
        public string graphLabel = "Cap1";
        public string spawnAnchorId = "Bedroom";
        public int level = 1;
        public float maxHP = 100f;
        public float maxMP = 50f;

        public bool abilitySwim;
        public bool abilityJump;
        public bool abilityClimb;
        public bool abilityMagic;
        public bool abilityFly;
        public bool abilitySprint;
        public bool abilityShield;

        public SpellId[] basicSpellIds = new SpellId[MagicCaster.BasicSlotCount];
        public SpellId comboSpellId = SpellId.None;

        public bool fastForward = true;
        public bool autoRestore = true;

        const string PrefsKey = "Sendero.Narrative.TestConfig";

        public static NarrativeTestConfig Load()
        {
            var json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var cfg = JsonUtility.FromJson<NarrativeTestConfig>(json);
                    if (cfg != null)
                    {
                        cfg.EnsureSpellSlots();
                        return cfg;
                    }
                }
                catch (Exception) { /* configuración vieja o corrupta: se empieza de cero */ }
            }
            var fresh = new NarrativeTestConfig();
            fresh.EnsureSpellSlots();
            return fresh;
        }

        public void EnsureSpellSlots()
        {
            if (basicSpellIds == null || basicSpellIds.Length != MagicCaster.BasicSlotCount)
            {
                var slots = new SpellId[MagicCaster.BasicSlotCount];
                if (basicSpellIds != null)
                    for (int i = 0; i < Mathf.Min(basicSpellIds.Length, slots.Length); i++) slots[i] = basicSpellIds[i];
                basicSpellIds = slots;
            }
        }

        public void Save()
        {
            EnsureSpellSlots();
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
        }
    }

    /// <summary>Resultado de simular el camino desde el inicio del grafo hasta el nodo objetivo.</summary>
    public sealed class NarrativeFastForwardPreview
    {
        public bool ran;
        public bool reachedTarget;
        public string objectiveTitle = "";
        public readonly List<string> warnings = new();
        public readonly List<string> actors = new();
        public readonly List<string> notes = new();
    }

    /// <summary>
    /// Único camino para lanzar una prueba desde un nodo: crea el preset temporal, aplica el
    /// estado del jugador, proyecta el progreso previo (Fast-Forward), coloca los blackboards
    /// (capítulo objetivo en el nodo; capítulos previos terminados) y entra en Play Mode.
    ///
    /// Lo usan tanto la ventana Quick Test de siempre como la pestaña «Prueba» del editor del
    /// grafo, para que no haya dos formas distintas de preparar la misma prueba.
    /// </summary>
    public static class NarrativeTestLauncher
    {
        public const string QuickTestPresetPath = "Assets/Editor/QuickTestPreset_Temp.asset";
        const string ProfilePath = "Assets/_BootProfile/GameBootProfile.asset";

        static PlayerPresetSO _originalBootPreset;
        static bool _originalUsePreset;
        static bool _needsRestore;
        static bool _launching;

        public static bool IsPlaying => EditorApplication.isPlaying;
        public static bool NeedsRestore => _needsRestore;

        // La restauración no puede depender de que quede abierta una ventana concreta: si se
        // lanza una prueba desde el grafo y se cierra la ventana antes de salir de Play, el
        // bootPreset se quedaría apuntando al preset temporal.
        [InitializeOnLoadMethod]
        static void HookPlayModeRestore()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) RestoreIfNeeded();
        }

        public static bool Launch(NarrativeGraph graph, string nodeGuid, NarrativeTestConfig cfg,
            out string status, out NarrativeFastForwardPreview preview)
        {
            preview = new NarrativeFastForwardPreview();
            if (EditorApplication.isPlaying)
            {
                status = "Ya estás en Play Mode. Para primero la prueba en curso.";
                return false;
            }
            if (_launching)
            {
                status = "Ya se está preparando otra prueba; espera un momento.";
                return false;
            }
            _launching = true;
            EditorApplication.delayCall += () => _launching = false;
            if (graph == null || string.IsNullOrEmpty(nodeGuid))
            {
                status = "Selecciona un nodo del grafo antes de lanzar la prueba.";
                return false;
            }

            var profile = FindProfile();
            if (profile == null)
            {
                status = "GameBootProfile no encontrado.";
                return false;
            }

            _originalBootPreset = profile.bootPreset;
            _originalUsePreset = profile.usePresetInsteadOfSave;
            _needsRestore = cfg.autoRestore;

            var tempPreset = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>(QuickTestPresetPath);
            if (tempPreset == null)
            {
                tempPreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
                AssetDatabase.CreateAsset(tempPreset, QuickTestPresetPath);
            }

            ApplyPlayerState(tempPreset, profile.defaultPlayerPreset, cfg);
            tempPreset.name = "QuickTest_Temp";

            if (cfg.fastForward)
                preview = RunFastForward(graph, nodeGuid, cfg, tempPreset);

            var graphLabel = string.IsNullOrEmpty(cfg.graphLabel)
                ? DetectGraphLabel(graph) ?? graph.name
                : cfg.graphLabel;

            var objetivo = nodeGuid;
            var previos = CapitulosPrevios(graph, ref objetivo);
            var snapshots = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
            foreach (var previo in previos)
            {
                var terminado = new SimpleBlackboard();
                terminado.Set(NarrativeRunner.CurrentNodeKey, string.Empty);
                terminado.Set(NarrativeRunner.FlowEndedKey, true);
                snapshots.Add(new PlayerSaveData.NarrativeBlackboardSnapshot
                {
                    graphLabel = previo.label,
                    blackboardData = terminado.ExportToSerializable()
                });
            }
            var actual = new SimpleBlackboard();
            actual.Set(NarrativeRunner.CurrentNodeKey, objetivo);
            snapshots.Add(new PlayerSaveData.NarrativeBlackboardSnapshot
            {
                graphLabel = graphLabel,
                blackboardData = actual.ExportToSerializable()
            });
            tempPreset.narrativeBlackboards = snapshots;

            profile.usePresetInsteadOfSave = true;
            profile.bootPreset = tempPreset;
            EditorUtility.SetDirty(tempPreset);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var selectedNode = graph.FindNode(objetivo);
            string nodeDesc = selectedNode != null
                ? $"{selectedNode.GetType().Name} \"{selectedNode.displayTitle}\""
                : objetivo;
            status = $"Lanzando Play desde {nodeDesc} en {graphLabel} (anchor: {tempPreset.spawnAnchorId})...";

            EditorApplication.isPlaying = true;
            return true;
        }

        /// <summary>Simula el camino sin tocar Assets ni entrar en Play Mode.</summary>
        public static NarrativeFastForwardPreview Preview(NarrativeGraph graph, string nodeGuid, NarrativeTestConfig cfg)
        {
            var scratch = ScriptableObject.CreateInstance<PlayerPresetSO>();
            try
            {
                return RunFastForward(graph, nodeGuid, cfg, scratch);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scratch);
            }
        }

        static NarrativeFastForwardPreview RunFastForward(NarrativeGraph graph, string nodeGuid,
            NarrativeTestConfig cfg, PlayerPresetSO dst)
        {
            var preview = new NarrativeFastForwardPreview { ran = true };
            if (graph == null || string.IsNullOrEmpty(nodeGuid)) return preview;

            var writer = new PresetNarrativeStateWriter(dst);
            var objetivo = nodeGuid;
            var previos = CapitulosPrevios(graph, ref objetivo);
            foreach (var previo in previos)
            {
                var anterior = NarrativeStateProjector.Project(previo.grafo, previo.puenteGuid, writer);
                preview.warnings.AddRange(anterior.Decisions.Select(d => $"[{previo.label}] {d}"));
                preview.notes.Add($"Capítulo previo '{previo.label}' dado por terminado y su progreso proyectado.");
            }
            if (objetivo != nodeGuid)
            {
                var puente = graph.FindNode(objetivo);
                preview.notes.Add($"Se empieza tras el puente del capítulo previo: '{puente?.displayTitle}'.");
            }

            var result = NarrativeStateProjector.Project(graph, objetivo, writer);
            preview.actors.AddRange(writer.ApplyActorPlacements(new OpenScenesNarrativeWorld()));
            preview.reachedTarget = result.ReachedTarget;
            preview.warnings.AddRange(result.Decisions);
            preview.notes.AddRange(writer.Notes);
            preview.objectiveTitle = graph.FindNode(objetivo)?.displayTitle ?? "";
            return preview;
        }

        /// <summary>
        /// Aplica a <paramref name="dst"/> el nivel/vida/maná/habilidades/hechizos configurados.
        /// El progreso de historia se deja vacío a propósito; la apariencia se hereda del preset
        /// cosmético del perfil. El Fast-Forward escribe el progreso encima después.
        /// </summary>
        public static void ApplyPlayerState(PlayerPresetSO dst, PlayerPresetSO cosmeticBase, NarrativeTestConfig cfg)
        {
            if (dst == null) return;
            cfg.EnsureSpellSlots();

            dst.spawnAnchorId = string.IsNullOrEmpty(cfg.spawnAnchorId) ? "Bedroom" : cfg.spawnAnchorId;
            dst.level = cfg.level;
            dst.maxHP = cfg.maxHP;
            dst.currentHP = cfg.maxHP;
            dst.maxMP = cfg.maxMP;
            dst.currentMP = cfg.maxMP;

            dst.abilities = new PlayerAbilities
            {
                swim = cfg.abilitySwim,
                jump = cfg.abilityJump,
                climb = cfg.abilityClimb,
                magic = cfg.abilityMagic,
                fly = cfg.abilityFly,
                sprint = cfg.abilitySprint,
                shield = cfg.abilityShield
            };
            dst.unlockedAbilities = new List<AbilityId>();

            dst.basicSpellIds = cfg.basicSpellIds.Where(id => id != SpellId.None).Distinct().ToList();
            dst.unlockedSpells = cfg.basicSpellIds.Append(cfg.comboSpellId)
                .Where(id => id != SpellId.None)
                .Distinct()
                .ToList();

            dst.appearance = new List<AppearanceEntry>(cosmeticBase?.appearance ?? new List<AppearanceEntry>());
            dst.unlockedWardrobeIds = new List<string>(cosmeticBase?.unlockedWardrobeIds ?? new List<string>());

            dst.flags = new List<string>();
            dst.inventoryItems = new List<InventoryItemSave>();
            dst.defeatedBossIds = new List<string>();
            dst.consumedInteractableIds = new List<string>();
            dst.objetosDelMundo = new List<ObjetoPersistente.Estado>();
            dst.completedInteractiveNarratives = new List<string>();
            dst.seenLorePopupIds = new List<string>();
            dst.partyMemberIds = new List<string>();
            dst.activeCharacterSlot = 1;
            dst.unlockedTeleportPoints = new List<string>();
            dst.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        }

        static readonly Dictionary<string, string> LabelCache = new();

        public static void InvalidateLabelCache() => LabelCache.Clear();

        public static string DetectGraphLabel(NarrativeGraph graph)
        {
            if (graph == null) return null;
            var path = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(path)) return null;
            if (LabelCache.TryGetValue(path, out var cached)) return cached;
            var label = LabelEnHub(graph);
            LabelCache[path] = label;
            return label;
        }

        public static string[] DetectSpawnAnchors()
        {
            var anchors = UnityEngine.Object.FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Exclude);
            return anchors
                .Select(a => a.anchorId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
        }

        public static void RestoreIfNeeded()
        {
            if (_needsRestore) RestoreOriginalBootPreset();
        }

        public static void RestoreOriginalBootPreset()
        {
            var profile = FindProfile();
            if (profile != null)
            {
                profile.bootPreset = _originalBootPreset;
                profile.usePresetInsteadOfSave = _originalUsePreset;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log("[QuickTest] bootPreset restaurado al original.");
            }
            _needsRestore = false;
        }

        static GameBootProfile FindProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(ProfilePath);
            if (profile != null) return profile;
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        // ── Capítulos previos ──

        public sealed class CapituloPrevio
        {
            public NarrativeGraph grafo;
            public string label;
            public string puenteGuid; // nodo que emite la señal que da paso al capítulo siguiente
        }

        /// <summary>
        /// Capítulos que preceden a <paramref name="grafo"/>: grafos del Hub con un nodo que emite una
        /// señal que este espera antes del nodo objetivo (lo que emite y espera cada nodo lo declara
        /// él mismo en NarrativeNode.Describe). Si el objetivo es el inicio o la espera de ese puente,
        /// se adelanta al primer nodo jugable, porque nadie va a volver a emitir la señal.
        /// Devuelve primero los más antiguos.
        /// </summary>
        public static List<CapituloPrevio> CapitulosPrevios(NarrativeGraph grafo, ref string objetivoGuid)
        {
            var resultado = new List<CapituloPrevio>();
            if (grafo == null || string.IsNullOrEmpty(objetivoGuid)) return resultado;

            var emisores = new Dictionary<string, CapituloPrevio>();
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var otro = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (otro == null || otro == grafo || otro.nodes == null) continue;
                var label = DetectGraphLabel(otro);
                if (string.IsNullOrEmpty(label)) continue;
                foreach (var emite in otro.nodes)
                {
                    if (emite == null) continue;
                    foreach (var senal in _refs.Collect(emite).Signals)
                        if (senal.Role == SignalRole.Emite && !string.IsNullOrEmpty(senal.Key) && !emisores.ContainsKey(senal.Key))
                            emisores.Add(senal.Key, new CapituloPrevio { grafo = otro, label = label, puenteGuid = emite.guid });
                }
            }

            bool EsPuente(NarrativeNode n) => EsperaSenalDe(n, emisores, out _);
            var nodo = grafo.FindNode(objetivoGuid);
            bool cruzaPuente = false;
            while (nodo != null && (nodo.IsGraphEntry || EsPuente(nodo)) && nodo.outputs != null && nodo.outputs.Count == 1)
            {
                cruzaPuente |= EsPuente(nodo);
                nodo = grafo.FindNode(nodo.outputs[0]);
            }
            if (cruzaPuente && nodo != null) objetivoGuid = nodo.guid;

            AnadirPrevios(grafo, objetivoGuid, emisores, resultado, new HashSet<NarrativeGraph> { grafo });
            return resultado;
        }

        static void AnadirPrevios(NarrativeGraph grafo, string hastaGuid, Dictionary<string, CapituloPrevio> emisores,
            List<CapituloPrevio> resultado, HashSet<NarrativeGraph> vistos)
        {
            var recorridos = new HashSet<string>();
            var cola = new Queue<string>();
            cola.Enqueue(grafo.startNodeGuid);
            while (cola.Count > 0)
            {
                var guid = cola.Dequeue();
                if (string.IsNullOrEmpty(guid) || guid == hastaGuid || !recorridos.Add(guid)) continue;
                var n = grafo.FindNode(guid);
                if (n == null) continue;
                if (EsperaSenalDe(n, emisores, out var previo) && vistos.Add(previo.grafo))
                {
                    AnadirPrevios(previo.grafo, previo.puenteGuid, emisores, resultado, vistos);
                    resultado.Add(previo);
                }
                if (n.outputs != null)
                    foreach (var o in n.outputs) cola.Enqueue(o);
            }
        }

        static readonly NarrativeNodeReferences _refs = new();

        /// <summary>
        /// true si el nodo espera una señal que emite otro capítulo; <paramref name="previo"/> es ese
        /// capítulo.
        /// </summary>
        static bool EsperaSenalDe(NarrativeNode n, Dictionary<string, CapituloPrevio> emisores, out CapituloPrevio previo)
        {
            previo = null;
            if (n == null) return false;
            foreach (var senal in _refs.Collect(n).Signals)
                if (senal.Role == SignalRole.Escucha && !string.IsNullOrEmpty(senal.Key) && emisores.TryGetValue(senal.Key, out previo))
                    return true;
            previo = null;
            return false;
        }

        /// <summary>Etiqueta con la que el NarrativeGraphHub de Start.unity registra el grafo, o null.</summary>
        public static string LabelEnHub(NarrativeGraph graph)
        {
            if (graph == null) return null;
            var graphPath = AssetDatabase.GetAssetPath(graph);
            var graphGuid = AssetDatabase.AssetPathToGUID(graphPath);

            var sceneGuids = AssetDatabase.FindAssets("t:Scene Start");
            foreach (var sceneGuid in sceneGuids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (!scenePath.Contains("Start")) continue;

                var sceneText = System.IO.File.ReadAllText(scenePath);
                int idx = sceneText.IndexOf(graphGuid, StringComparison.Ordinal);
                if (idx < 0) continue;

                int searchStart = Mathf.Max(0, idx - 200);
                string chunk = sceneText.Substring(searchStart, idx - searchStart);
                int labelIdx = chunk.LastIndexOf("label:", StringComparison.Ordinal);
                if (labelIdx >= 0)
                {
                    int lineEnd = chunk.IndexOf('\n', labelIdx);
                    if (lineEnd < 0) lineEnd = chunk.Length;
                    string labelLine = chunk.Substring(labelIdx + 6, lineEnd - labelIdx - 6).Trim();
                    if (!string.IsNullOrEmpty(labelLine))
                        return labelLine;
                }
            }
            return null;
        }
    }
}
