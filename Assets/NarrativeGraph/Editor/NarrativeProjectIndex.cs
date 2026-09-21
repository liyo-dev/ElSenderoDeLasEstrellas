using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#pragma warning disable 618 // se referencian nodos [Obsolete] a propósito para seguir mostrando grafos viejos
namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Índice único del proyecto para el editor narrativo (solo editor).
    ///
    /// Resuelve, una sola vez y bajo demanda, todo lo que los nodos referencian por texto:
    /// quests (questId → QuestData), diálogos, NPCs (persistenceId), señales (quién emite / quién
    /// escucha), claves de localización (texto real en ES/EN), flags y anchors. Alimenta los
    /// desplegables de NarrativeKeyDrawer, las previsualizaciones de NodeView y el panel lateral.
    ///
    /// Se invalida solo cuando cambia el proyecto (EditorApplication.projectChanged) y se
    /// reconstruye en el siguiente acceso. Nunca se hace en un bucle.
    ///
    /// Coste (12 sept 2026, Claude): el escaneo YAML de escenas/prefabs y la carga de todos los
    /// prefabs de NPCs se ejecutaban enteros en CADA domain reload (cada Play, cada recompilación),
    /// porque los campos estáticos se pierden al recargar el dominio. Con ~120 MB de escenas
    /// (MainWorld_old, maquetas EldoriaCodex...) eso eran 45-150 s de "Hold on" al darle al Play.
    /// Ahora los resultados por archivo se guardan en Library/NarrativeProjectIndex.cache.json
    /// (clave: ruta + tamaño + fecha de modificación) y solo se vuelven a leer los archivos que
    /// han cambiado. Las carpetas de maquetas generadas y versiones antiguas se omiten del todo.
    /// </summary>
    [InitializeOnLoad]
    public static class NarrativeProjectIndex
    {
        public sealed class QuestInfo
        {
            public string questId;
            public QuestData asset;
            public string path;
            public string displayName;  // localizado (ES) o displayName
            public string[] stepConditionIds = Array.Empty<string>();
            public string[] stepDescriptions = Array.Empty<string>();
        }

        public sealed class SignalInfo
        {
            public string key;
            public readonly List<string> emitters = new();   // "Grafo Cap1 › Emitir señal 'X'", "Sequencer StarAwakening", "NPC Eldran (interacción)"
            public readonly List<string> listeners = new();
        }

        public sealed class ActorInfo
        {
            public string id;
            public string source; // ruta del prefab / escena
            public string displayName;
        }

        public const string LocalizationFolder = "Assets/Resources/Localization";
        static readonly string[] LocCatalogs = { "dialogues", "quests", "ui", "cinematics", "prologue", "other" };

        static bool _dirty = true;
        static readonly Dictionary<string, QuestInfo> _quests = new(StringComparer.Ordinal);
        static readonly Dictionary<string, SignalInfo> _signals = new(StringComparer.Ordinal);
        static readonly Dictionary<string, ActorInfo> _actors = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string> _locEs = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string> _locEn = new(StringComparer.Ordinal);
        static readonly HashSet<string> _flags = new(StringComparer.Ordinal);
        static readonly HashSet<string> _anchors = new(StringComparer.Ordinal);
        static readonly List<DialogueAsset> _dialogues = new();
        static readonly List<NarrativeGraph> _graphs = new();
        static readonly Dictionary<DialogueAsset, List<string>> _dialogueUsers = new();

        public static event Action Rebuilt;

        static NarrativeProjectIndex()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static void Invalidate() { _dirty = true; }

        public static void ForceRebuild() { _dirty = true; EnsureBuilt(); }

        // ─── Acceso público ────────────────────────────────────────────────

        public static IReadOnlyDictionary<string, QuestInfo> Quests { get { EnsureBuilt(); return _quests; } }
        public static IReadOnlyDictionary<string, SignalInfo> Signals { get { EnsureBuilt(); return _signals; } }
        public static IReadOnlyDictionary<string, ActorInfo> Actors { get { EnsureBuilt(); return _actors; } }
        public static IReadOnlyList<DialogueAsset> Dialogues { get { EnsureBuilt(); return _dialogues; } }
        public static IReadOnlyList<NarrativeGraph> Graphs { get { EnsureBuilt(); return _graphs; } }
        public static IEnumerable<string> Flags { get { EnsureBuilt(); return _flags.OrderBy(f => f); } }
        public static IEnumerable<string> Anchors { get { EnsureBuilt(); return _anchors.OrderBy(f => f); } }
        public static IEnumerable<string> LocKeys { get { EnsureBuilt(); return _locEs.Keys.OrderBy(k => k); } }

        public static QuestInfo FindQuest(string questId)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(questId)) return null;
            return _quests.TryGetValue(questId, out var q) ? q : null;
        }

        public static ActorInfo FindActor(string id)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id)) return null;
            return _actors.TryGetValue(id, out var a) ? a : null;
        }

        public static SignalInfo FindSignal(string key)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(key)) return null;
            return _signals.TryGetValue(key, out var s) ? s : null;
        }

        /// <summary>Texto localizado (ES) para una clave, o el fallback si no existe.</summary>
        public static string Loc(string key, string fallback = null)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(key) && _locEs.TryGetValue(key, out var v)) return v;
            return fallback;
        }

        public static string LocEn(string key, string fallback = null)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(key) && _locEn.TryGetValue(key, out var v)) return v;
            return fallback;
        }

        public static bool HasLoc(string key) { EnsureBuilt(); return !string.IsNullOrEmpty(key) && _locEs.ContainsKey(key); }

        /// <summary>Nombre legible del hablante a partir de speakerNameId (CHAR_ELDRAN → "Eldran").</summary>
        public static string SpeakerName(string speakerNameId, bool isPlayer)
        {
            if (!string.IsNullOrEmpty(speakerNameId))
            {
                var loc = Loc(speakerNameId);
                if (!string.IsNullOrEmpty(loc)) return loc;
                return speakerNameId.StartsWith("CHAR_") ? Capitalize(speakerNameId.Substring(5)) : speakerNameId;
            }
            return isPlayer ? "Will" : "?";
        }

        /// <summary>Texto real de una línea de diálogo (localizado si hay textId).</summary>
        public static string LineText(in DialogueLine line)
        {
            if (!string.IsNullOrEmpty(line.textId))
            {
                var loc = Loc(line.textId);
                if (!string.IsNullOrEmpty(loc)) return loc;
                if (!string.IsNullOrEmpty(line.text)) return line.text;
                return $"«{line.textId}» (sin traducción)";
            }
            return line.text ?? "";
        }

        public static IReadOnlyList<string> DialogueUsers(DialogueAsset asset)
        {
            EnsureBuilt();
            return asset != null && _dialogueUsers.TryGetValue(asset, out var l) ? l : Array.Empty<string>();
        }

        /// <summary>Lista de opciones para un desplegable según el tipo de clave.</summary>
        public static List<string> OptionsFor(NarrativeKeyKind kind, string questIdForSteps = null)
        {
            EnsureBuilt();
            switch (kind)
            {
                case NarrativeKeyKind.Quest: return _quests.Keys.OrderBy(k => k).ToList();
                case NarrativeKeyKind.QuestStep:
                    {
                        var q = FindQuest(questIdForSteps);
                        return q != null ? q.stepConditionIds.Where(s => !string.IsNullOrEmpty(s)).ToList() : new List<string>();
                    }
                case NarrativeKeyKind.Signal: return _signals.Keys.OrderBy(k => k).ToList();
                case NarrativeKeyKind.Actor: return _actors.Keys.OrderBy(k => k).ToList();
                case NarrativeKeyKind.LocKey: return _locEs.Keys.OrderBy(k => k).ToList();
                case NarrativeKeyKind.Flag: return _flags.OrderBy(k => k).ToList();
                case NarrativeKeyKind.Anchor: return _anchors.OrderBy(k => k).ToList();
                default: return new List<string>();
            }
        }

        /// <summary>true si la clave resuelve contra el índice (para avisos en el editor).</summary>
        public static bool Resolves(NarrativeKeyKind kind, string value, string questIdForSteps = null)
        {
            if (string.IsNullOrEmpty(value)) return false;
            EnsureBuilt();
            switch (kind)
            {
                case NarrativeKeyKind.Quest: return _quests.ContainsKey(value);
                case NarrativeKeyKind.QuestStep:
                    {
                        var q = FindQuest(questIdForSteps);
                        return q != null && Array.IndexOf(q.stepConditionIds, value) >= 0;
                    }
                case NarrativeKeyKind.Signal: return _signals.ContainsKey(value);
                case NarrativeKeyKind.Actor: return _actors.ContainsKey(value);
                case NarrativeKeyKind.LocKey: return _locEs.ContainsKey(value);
                case NarrativeKeyKind.Flag: return true; // los flags se crean al usarlos
                case NarrativeKeyKind.Anchor: return _anchors.Count == 0 || _anchors.Contains(value);
                default: return true;
            }
        }

        // ─── Construcción ─────────────────────────────────────────────────

        static void EnsureBuilt()
        {
            if (!_dirty) return;
            _dirty = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _scannedFiles = 0; _cachedFiles = 0;
            try
            {
                Rebuild();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProjectIndex] Error reconstruyendo el índice: {ex.Message}");
            }
            sw.Stop();
            // Aviso solo cuando cuesta de verdad, para poder detectar regresiones sin ensuciar la consola.
            if (sw.ElapsedMilliseconds > 2000)
                Debug.Log($"[NarrativeProjectIndex] Índice reconstruido en {sw.ElapsedMilliseconds} ms ({_scannedFiles} archivo(s) leídos, {_cachedFiles} desde caché).");
            try { Rebuilt?.Invoke(); } catch { }
        }

        static void Rebuild()
        {
            _quests.Clear(); _signals.Clear(); _actors.Clear();
            _locEs.Clear(); _locEn.Clear(); _flags.Clear(); _anchors.Clear();
            _dialogues.Clear(); _graphs.Clear(); _dialogueUsers.Clear();

            LoadLocalization("es", _locEs);
            LoadLocalization("en", _locEn);

            LoadCache();

            // Quests
            foreach (var guid in AssetDatabase.FindAssets("t:QuestData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var q = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                var info = new QuestInfo
                {
                    questId = q.questId,
                    asset = q,
                    path = path,
                    displayName = Loc(q.displayNameId, string.IsNullOrEmpty(q.displayName) ? q.questId : q.displayName),
                    stepConditionIds = q.steps?.Select(s => s?.conditionId ?? "").ToArray() ?? Array.Empty<string>(),
                    stepDescriptions = q.steps?.Select(s => Loc(s?.descriptionId, s?.description ?? "")).ToArray() ?? Array.Empty<string>()
                };
                _quests[q.questId] = info;
            }

            // Diálogos
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueAsset"))
            {
                var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) _dialogues.Add(d);
            }
            _dialogues.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            // NPCs (persistenceId) desde prefabs. Cargar un prefab entero (mallas, materiales...) solo para
            // leer un string es caro y antes se hacía con TODOS en cada domain reload; ahora solo se carga
            // el prefab si ha cambiado desde la última vez (ver PrefabCacheEntry).
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_NPCs", "Assets/Prefabs", "Assets/_PREFABS" }.Where(AssetDatabase.IsValidFolder).ToArray()))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkipPath(path)) continue;
                var entry = GetPrefabEntry(path);
                if (entry == null) continue;
                for (int i = 0; i < entry.ids.Length; i++)
                {
                    var id = entry.ids[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!_actors.ContainsKey(id))
                        _actors[id] = new ActorInfo { id = id, source = path, displayName = entry.displayName };
                    var sig = GetSignal(WaitNpcInteractionNode.SignalKeyFor(id));
                    sig.emitters.Add($"NPC {entry.displayName} (al hablarle)");
                }
            }

            // Grafos: señales emitidas/escuchadas, flags, diálogos usados
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (g == null) continue;
                _graphs.Add(g);
                foreach (var n in g.nodes)
                {
                    if (n == null) continue;
                    string where = $"Grafo {g.name} › {NarrativeNodeCatalog.TitleOf(n.GetType())}{(string.IsNullOrEmpty(n.displayTitle) ? "" : " '" + n.displayTitle + "'")}";
                    switch (n)
                    {
                        case RaiseCustomEventNode r when !string.IsNullOrEmpty(r.eventKey): GetSignal(r.eventKey).emitters.Add(where); break;
                        case WaitCustomEventNode w when !string.IsNullOrEmpty(w.eventKey): GetSignal(w.eventKey).listeners.Add(where); break;
                        case PlayCinematicNode c:
                            if (!string.IsNullOrEmpty(c.signalIn)) GetSignal(c.signalIn).emitters.Add(where);
                            if (!string.IsNullOrEmpty(c.signalDone)) GetSignal(c.signalDone).listeners.Add(where);
                            if (!string.IsNullOrEmpty(c.signalFailed)) GetSignal(c.signalFailed).listeners.Add(where);
                            break;
                        case WaitNpcInteractionNode wn when !string.IsNullOrEmpty(wn.npcId): GetSignal(WaitNpcInteractionNode.SignalKeyFor(wn.npcId)).listeners.Add(where); break;
                        case SetFlagNode sf when !string.IsNullOrEmpty(sf.flagKey): _flags.Add(sf.flagKey); break;
                        case BranchFlagNode bf when !string.IsNullOrEmpty(bf.flagKey): _flags.Add(bf.flagKey); break;
                        case DialogueChoiceNode dc when !string.IsNullOrEmpty(dc.rememberAsFlag): _flags.Add(dc.rememberAsFlag); break;
                        case CheckpointNode cp when !string.IsNullOrEmpty(cp.spawnAnchorId): _anchors.Add(cp.spawnAnchorId); break;
                        case PlayDialogueNode pd when pd.dialogue != null:
                            if (!_dialogueUsers.TryGetValue(pd.dialogue, out var users)) _dialogueUsers[pd.dialogue] = users = new List<string>();
                            users.Add(where);
                            break;
                    }
                }
            }
            _graphs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            // Escenas y prefabs (YAML): _signalIn/_signalOut de sequencers, eventKey de emisores, anchorId de SpawnAnchor
            ScanYamlAssets();

            // Secuencias reutilizables (SequenceDefinition.asset): a diferencia de los sequencers antiguos
            // (que guardan _signalIn/_signalOut como campos propios en la escena, capturados arriba por
            // ScanYamlAssets), SequencePlayer deja esos campos heredados vacíos a propósito y en su lugar lee
            // signalIn/signalOut de un SequenceDefinition compartido (ver CinematicSequencerBase.SignalInOverride
            // / SequencePlayer.SignalInOverride), para poder reutilizar el mismo componente en varias secuencias.
            // FIX (17 sep 2026, Raúl: "mira el grafo" -- PROLOGUE_START salía como "nadie la escucha" y
            // PROLOGUE_DONE como "nadie la emite" pese a que SEQ_Prologo_UltimaNoche.asset sí los declara y
            // SequencePlayer sí los escucha/emite en runtime): sin este escaneo el índice nunca se enteraba de
            // estas señales, porque ScanYamlAssets() solo mira dentro de escenas/prefabs y las señales de un
            // SequenceDefinition viven en un .asset (ScriptableObject) aparte, que nunca se recorría.
            ScanSequenceDefinitions();

            // Código C#: RaiseCustom("LITERAL") en scripts del juego (sequencers, triggers)
            ScanScriptsForSignals();

            SaveCacheIfChanged();
        }

        // ─── Caché en disco (Library/) ─────────────────────────────────────
        //
        // Los campos estáticos de esta clase se vacían en cada domain reload (cada Play), así que sin
        // caché el índice releía ~120 MB de escenas y cargaba todos los prefabs de NPC cada vez.
        // La caché guarda, por archivo, lo poco que el índice necesita de él, junto con tamaño y
        // fecha de modificación; si no han cambiado, no se vuelve a abrir el archivo.

        const string CachePath = "Library/NarrativeProjectIndex.cache.json";
        const int CacheVersion = 1;

        [Serializable] class YamlCacheEntry { public string path; public long size; public long mtime; public string[] signalIn = Array.Empty<string>(); public string[] signalOut = Array.Empty<string>(); public string[] eventKeys = Array.Empty<string>(); public string[] anchors = Array.Empty<string>(); }
        [Serializable] class PrefabCacheEntry { public string path; public long size; public long mtime; public string displayName; public string[] ids = Array.Empty<string>(); }
        [Serializable] class CacheFile { public int version; public List<YamlCacheEntry> yaml = new(); public List<PrefabCacheEntry> prefabs = new(); }

        static Dictionary<string, YamlCacheEntry> _yamlCache;
        static Dictionary<string, PrefabCacheEntry> _prefabCache;
        static bool _cacheChanged;
        static int _scannedFiles, _cachedFiles;

        // Carpetas que nunca aportan nada al índice y son enormes: maquetas generadas por los builders
        // de Eldoria (EldoriaCodex_*, CatalogoAssets_*), cosas apartadas para borrar y versiones antiguas.
        // (MainWorld_old NO se omite: se escanea una vez y queda en caché.)
        static readonly string[] SkipFragments =
        {
            "/Demo/", "/Plugins/", "/EldoriaCodex_", "/CatalogoAssets_", "/_to_delete/",
            "/Versiones antiguas/"
        };

        static bool ShouldSkipPath(string path)
        {
            foreach (var f in SkipFragments)
                if (path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void LoadCache()
        {
            if (_yamlCache != null) return; // ya cargada en este dominio
            _yamlCache = new Dictionary<string, YamlCacheEntry>(StringComparer.Ordinal);
            _prefabCache = new Dictionary<string, PrefabCacheEntry>(StringComparer.Ordinal);
            _cacheChanged = false;
            try
            {
                if (!File.Exists(CachePath)) return;
                var data = JsonUtility.FromJson<CacheFile>(File.ReadAllText(CachePath));
                if (data == null || data.version != CacheVersion) return;
                foreach (var e in data.yaml) if (e != null && !string.IsNullOrEmpty(e.path)) _yamlCache[e.path] = e;
                foreach (var e in data.prefabs) if (e != null && !string.IsNullOrEmpty(e.path)) _prefabCache[e.path] = e;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProjectIndex] Caché ilegible, se regenera: {ex.Message}");
                _yamlCache.Clear(); _prefabCache.Clear();
            }
        }

        static void SaveCacheIfChanged()
        {
            if (!_cacheChanged || _yamlCache == null) return;
            _cacheChanged = false;
            try
            {
                var data = new CacheFile { version = CacheVersion, yaml = _yamlCache.Values.ToList(), prefabs = _prefabCache.Values.ToList() };
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonUtility.ToJson(data));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProjectIndex] No se pudo guardar la caché: {ex.Message}");
            }
        }

        static bool TryGetFileStamp(string path, out long size, out long mtime)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) { size = 0; mtime = 0; return false; }
                size = fi.Length; mtime = fi.LastWriteTimeUtc.Ticks;
                return true;
            }
            catch { size = 0; mtime = 0; return false; }
        }

        static PrefabCacheEntry GetPrefabEntry(string path)
        {
            if (!TryGetFileStamp(path, out var size, out var mtime)) return null;
            if (_prefabCache.TryGetValue(path, out var cached) && cached.size == size && cached.mtime == mtime)
            {
                _cachedFiles++;
                return cached;
            }

            _scannedFiles++;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return null;
            var ids = new List<string>();
            foreach (var m in go.GetComponentsInChildren<Game.NPC.NPCBehaviourManagerV2>(true))
            {
                var id = m.PersistenceId;
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            var entry = new PrefabCacheEntry { path = path, size = size, mtime = mtime, displayName = go.name, ids = ids.ToArray() };
            _prefabCache[path] = entry;
            _cacheChanged = true;
            return entry;
        }

        static SignalInfo GetSignal(string key)
        {
            if (!_signals.TryGetValue(key, out var s))
                _signals[key] = s = new SignalInfo { key = key };
            return s;
        }

        static readonly Regex YamlSignalIn = new(@"^\s*_signalIn:\s*(\S+)\s*$", RegexOptions.Compiled);
        static readonly Regex YamlSignalOut = new(@"^\s*_signalOut:\s*(\S+)\s*$", RegexOptions.Compiled);
        static readonly Regex YamlEventKey = new(@"^\s*(?:eventKey|narrativeEventKey|defeatEventKey):\s*(\S+)\s*$", RegexOptions.Compiled);
        static readonly Regex YamlAnchor = new(@"^\s*anchorId:\s*(\S+)\s*$", RegexOptions.Compiled);

        static void ScanYamlAssets()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;
                if (ShouldSkipPath(path)) continue;
                var entry = GetYamlEntry(path);
                if (entry == null) continue;
                string label = Path.GetFileNameWithoutExtension(path);
                foreach (var k in entry.signalIn) GetSignal(k).listeners.Add($"Sequencer en {label}");
                foreach (var k in entry.signalOut) GetSignal(k).emitters.Add($"Sequencer en {label}");
                foreach (var k in entry.eventKeys) GetSignal(k).emitters.Add($"Emisor en {label}");
                foreach (var k in entry.anchors) _anchors.Add(k);
            }
        }

        static void ScanSequenceDefinitions()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SequenceDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkipPath(path)) continue;
                var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(path);
                if (def == null) continue;
                string label = string.IsNullOrEmpty(def.name) ? Path.GetFileNameWithoutExtension(path) : def.name;
                if (!string.IsNullOrEmpty(def.signalIn)) GetSignal(def.signalIn).listeners.Add($"SequencePlayer con {label}");
                if (!string.IsNullOrEmpty(def.signalOut)) GetSignal(def.signalOut).emitters.Add($"SequencePlayer con {label}");
            }
        }

        static YamlCacheEntry GetYamlEntry(string path)
        {
            if (!TryGetFileStamp(path, out var size, out var mtime)) return null;
            if (_yamlCache.TryGetValue(path, out var cached) && cached.size == size && cached.mtime == mtime)
            {
                _cachedFiles++;
                return cached;
            }

            _scannedFiles++;
            var signalIn = new List<string>(); var signalOut = new List<string>();
            var eventKeys = new List<string>(); var anchors = new List<string>();
            try
            {
                using var reader = new StreamReader(path);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 200 || line.IndexOf(':') < 0) continue;
                    // Filtro barato antes de las regex: la inmensa mayoría de líneas no contienen ninguna de estas claves.
                    if (line.IndexOf("_signal", StringComparison.Ordinal) < 0 && line.IndexOf("ventKey", StringComparison.Ordinal) < 0 && line.IndexOf("anchorId", StringComparison.Ordinal) < 0) continue;
                    Match m;
                    if ((m = YamlSignalIn.Match(line)).Success) signalIn.Add(Clean(m.Groups[1].Value));
                    else if ((m = YamlSignalOut.Match(line)).Success) signalOut.Add(Clean(m.Groups[1].Value));
                    else if ((m = YamlEventKey.Match(line)).Success) eventKeys.Add(Clean(m.Groups[1].Value));
                    else if ((m = YamlAnchor.Match(line)).Success) anchors.Add(Clean(m.Groups[1].Value));
                }
            }
            catch { /* archivo binario o inaccesible: se guarda vacío para no reintentarlo cada vez */ }

            var entry = new YamlCacheEntry
            {
                path = path, size = size, mtime = mtime,
                signalIn = signalIn.ToArray(), signalOut = signalOut.ToArray(), eventKeys = eventKeys.ToArray(), anchors = anchors.ToArray()
            };
            _yamlCache[path] = entry;
            _cacheChanged = true;
            return entry;
        }

        static readonly Regex CsRaise = new(@"RaiseCustom\(\s*""([A-Za-z0-9_:\-]+)""", RegexOptions.Compiled);
        static readonly Regex CsOn = new(@"OnCustom\(\s*""([A-Za-z0-9_:\-]+)""", RegexOptions.Compiled);

        static void ScanScriptsForSignals()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" }.Where(AssetDatabase.IsValidFolder).ToArray());
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;
                string text;
                try { text = File.ReadAllText(path); } catch { continue; }
                if (text.IndexOf("Custom(", StringComparison.Ordinal) < 0) continue;
                string label = Path.GetFileNameWithoutExtension(path);
                foreach (Match m in CsRaise.Matches(text)) GetSignal(m.Groups[1].Value).emitters.Add($"Código {label}.cs");
                foreach (Match m in CsOn.Matches(text)) GetSignal(m.Groups[1].Value).listeners.Add($"Código {label}.cs");
            }
        }

        static string Clean(string v)
        {
            v = v.Trim();
            if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
                v = v.Substring(1, v.Length - 2);
            return v;
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace('_', ' ').ToLowerInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ─── Localización ─────────────────────────────────────────────────

        [Serializable] class LocData { public LocTextEntry[] texts; public LocSubEntry[] subtitles; }
        [Serializable] class LocTextEntry { public string key; public string value; }
        [Serializable] class LocSubEntry { public string id; public string text; }

        static void LoadLocalization(string locale, Dictionary<string, string> into)
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
                            if (!string.IsNullOrEmpty(e.key)) into[e.key] = e.value;
                    if (data?.subtitles != null)
                        foreach (var e in data.subtitles)
                            if (!string.IsNullOrEmpty(e.id)) into[e.id] = e.text;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NarrativeProjectIndex] No se pudo leer {path}: {ex.Message}");
                }
            }
        }
    }
}
