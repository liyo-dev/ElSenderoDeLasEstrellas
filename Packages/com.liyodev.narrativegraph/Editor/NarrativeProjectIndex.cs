using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor
{
    /// <summary>
    /// Índice único del proyecto para el editor narrativo (solo editor).
    ///
    /// Resuelve, una sola vez y bajo demanda, todo lo que los nodos referencian por texto:
    /// misiones, actores, señales (quién emite / quién escucha), textos localizados, flags y marcas.
    /// Alimenta los desplegables de NarrativeKeyDrawer, las tarjetas de NodeView y el panel lateral.
    ///
    /// De dónde sale cada cosa:
    ///  - Lo que toca cada nodo de los grafos lo declara el propio nodo en NarrativeNode.Describe.
    ///  - Los campos de escenas y prefabs marcados con [NarrativeKey(Signal, Rol = …)] dicen si
    ///    emiten o escuchan la señal.
    ///  - Las literales de RaiseCustom("…") / OnCustom("…") del código.
    ///  - Lo que solo el juego sabe leer (sus misiones, sus actores, sus textos, convenciones de
    ///    nombres de campo en sus escenas) lo aportan sus INarrativeProjectCatalog.
    ///
    /// Se invalida solo cuando cambia el proyecto (EditorApplication.projectChanged) y se
    /// reconstruye en el siguiente acceso. Nunca se hace en un bucle.
    ///
    /// Coste: los campos estáticos se pierden en cada domain reload (cada Play, cada recompilación),
    /// y leer todas las escenas y prefabs cada vez cuesta decenas de segundos. Por eso los
    /// resultados por archivo se guardan en Library/NarrativeProjectIndex.cache.json (clave: ruta +
    /// tamaño + fecha de modificación) y solo se vuelven a leer los archivos que han cambiado.
    /// </summary>
    [InitializeOnLoad]
    public static class NarrativeProjectIndex
    {
        public sealed class QuestInfo
        {
            public string questId;
            public UnityEngine.Object asset;
            public string path;
            public string displayName;  // en el idioma principal
            public string description;  // en el idioma principal
            public string[] stepConditionIds = Array.Empty<string>();
            public string[] stepDescriptions = Array.Empty<string>();
        }

        public sealed class SignalInfo
        {
            public string key;
            public readonly List<string> emitters = new();   // «Grafo X › Emitir señal 'Y'», «Componente en Escena»…
            public readonly List<string> listeners = new();
        }

        public sealed class ActorInfo
        {
            public string id;
            public string source; // ruta del prefab / escena
            public string displayName;
        }

        // Dos capas: lo del proyecto (catálogos del juego, escenas, prefabs y código; caro de leer)
        // y lo de los grafos (barato). Guardar un grafo solo rehace la segunda.
        static bool _projectDirty = true;
        static bool _graphsDirty = true;
        static readonly Dictionary<string, (string[] emitters, string[] listeners)> _projectSignals = new(StringComparer.Ordinal);
        static readonly HashSet<string> _projectAnchors = new(StringComparer.Ordinal);
        static readonly Dictionary<string, QuestInfo> _quests = new(StringComparer.Ordinal);
        static readonly Dictionary<string, SignalInfo> _signals = new(StringComparer.Ordinal);
        static readonly Dictionary<string, ActorInfo> _actors = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string> _locEs = new(StringComparer.Ordinal);
        static readonly Dictionary<string, string> _locEn = new(StringComparer.Ordinal);
        static readonly HashSet<string> _flags = new(StringComparer.Ordinal);
        static readonly HashSet<string> _anchors = new(StringComparer.Ordinal);
        static readonly HashSet<string> _gameSteps = new(StringComparer.Ordinal);
        static readonly List<NarrativeGraph> _graphs = new();
        static readonly Dictionary<UnityEngine.Object, List<string>> _assetUsers = new();
        static readonly Dictionary<string, List<NodeUse>> _questUsers = new(StringComparer.Ordinal);

        /// <summary>Un nodo de un grafo que usa algo, con su descripción para una persona.</summary>
        public readonly struct NodeUse
        {
            public readonly NarrativeNode Node;
            public readonly string Label;
            public NodeUse(NarrativeNode node, string label) { Node = node; Label = label; }
        }

        public static event Action Rebuilt;

        /// Todo se vuelve a leer (proyecto y grafos).
        public static void Invalidate() { _projectDirty = true; _graphsDirty = true; }

        /// Solo han cambiado grafos: se relee lo que declaran sus pasos, no el resto del proyecto.
        public static void InvalidateGraphs() { _graphsDirty = true; }

        public static void ForceRebuild() { Invalidate(); EnsureBuilt(); }

        /// Clasifica los cambios del proyecto: si solo han cambiado grafos, la capa cara no se toca, y
        /// lo que el índice no lee (texturas, materiales, modelos, sonidos…) no lo toca nada.
        sealed class ChangeWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool invalidar = false;
                foreach (var path in imported.Concat(deleted).Concat(moved).Concat(movedFrom))
                {
                    var ext = Path.GetExtension(path);
                    if (!s_projectExtensions.Contains(ext)) continue;
                    // Lo que ha cambiado se vuelve a leer aunque la caché se dé por buena (ver Trusted).
                    MarkChanged(path);
                    // Entra, sale o se mueve una escena o un prefab: la lista de archivos que se leen
                    // ya no vale. Que solo cambie su contenido lo resuelve la caché por archivo.
                    bool yaml = ext.Equals(".unity", StringComparison.OrdinalIgnoreCase) || ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase);
                    if (yaml && (!IsListed(path) || Array.IndexOf(deleted, path) >= 0 || Array.IndexOf(movedFrom, path) >= 0))
                        _assetListDirty = true;
                    if (invalidar) continue;
                    var type = ext.Equals(".asset", StringComparison.OrdinalIgnoreCase) ? AssetDatabase.GetMainAssetTypeAtPath(path) : null;
                    if (type == typeof(NarrativeGraph)) { _graphsDirty = true; Story.StoryCache.Invalidate(); continue; }
                    // De los .asset solo cuentan los datos (ScriptableObject) que leen los catálogos:
                    // mallas, terrenos, luces horneadas o los ajustes del propio editor no aportan nada.
                    // Uno borrado ya no tiene tipo: cuenta, por si acaso.
                    if (type != null && !typeof(ScriptableObject).IsAssignableFrom(type)) continue;
                    if (type != null && !IsWatchedAssetType(type)) continue;
                    invalidar = true;
                    s_lastInvalidation = path;
                }
                if (invalidar) { Invalidate(); Story.StoryCache.Invalidate(); }
            }
        }

        // ─── Qué cambios cuentan ──────────────────────────────────────────

        // Tipos de ScriptableObject que leen los catálogos (Contributions.WatchAssetTypes). Sin
        // ninguno declarado, cuenta cualquier ScriptableObject.
        static readonly List<Type> s_watchedAssetTypes = new();
        static bool s_watchedAssetTypesDeclared;

        // El último archivo que obligó a rehacer el índice entero (sale en la sonda de tiempos).
        static string s_lastInvalidation;

        static bool IsWatchedAssetType(Type type)
        {
            if (!s_watchedAssetTypesDeclared) return true;
            foreach (var t in s_watchedAssetTypes) if (t.IsAssignableFrom(type)) return true;
            return false;
        }

        // La caché por archivo se comprueba (tamaño y fecha) una vez por sesión del editor. Después,
        // cada archivo que cambia pasa por ChangeWatcher, así que los demás se dan por buenos sin
        // abrirlos ni mirar su fecha: con miles de escenas y prefabs, solo mirar las fechas costaba
        // más de un segundo cada vez. La marca y la lista de cambiados viven en SessionState para
        // sobrevivir a la recarga de código de cada Play; al cerrar Unity se pierden y se vuelve a
        // comprobar todo.
        const string TrustedKey = "NarrativeProjectIndex.CacheTrusted";
        const string ChangedKey = "NarrativeProjectIndex.ChangedSinceScan";
        static HashSet<string> s_changed;

        static HashSet<string> Changed
        {
            get
            {
                if (s_changed != null) return s_changed;
                s_changed = new HashSet<string>(StringComparer.Ordinal);
                var saved = SessionState.GetString(ChangedKey, string.Empty);
                if (saved.Length > 0) s_changed.UnionWith(saved.Split('\n'));
                return s_changed;
            }
        }

        static bool Trusted => SessionState.GetBool(TrustedKey, false);

        static void MarkChanged(string path)
        {
            if (string.IsNullOrEmpty(path) || !Changed.Add(path.Replace('\\', '/'))) return;
            if (Trusted) SessionState.SetString(ChangedKey, string.Join("\n", s_changed));
        }

        /// Un archivo que no ha cambiado desde la última comprobación completa.
        static bool Unchanged(string path) => Trusted && !Changed.Contains(path.Replace('\\', '/'));

        static void MarkScanned()
        {
            Changed.Clear();
            SessionState.SetString(ChangedKey, string.Empty);
            SessionState.SetBool(TrustedKey, true);
        }

        static HashSet<string> s_listed;
        static bool IsListed(string path)
        {
            if (_yamlPaths == null) return false;
            s_listed ??= new HashSet<string>(_yamlPaths, StringComparer.Ordinal);
            return s_listed.Contains(path);
        }

        /// Archivos que lee la capa del proyecto: escenas, prefabs, código y assets de datos. Los
        /// catálogos del juego añaden los suyos con <see cref="Contributions.WatchFiles"/>.
        static readonly HashSet<string> s_projectExtensions = new(StringComparer.OrdinalIgnoreCase) { ".unity", ".prefab", ".cs", ".asset" };

        // ─── Acceso público ────────────────────────────────────────────────

        public static IReadOnlyDictionary<string, QuestInfo> Quests { get { EnsureBuilt(); return _quests; } }
        public static IReadOnlyDictionary<string, SignalInfo> Signals { get { EnsureBuilt(); return _signals; } }
        public static IReadOnlyDictionary<string, ActorInfo> Actors { get { EnsureBuilt(); return _actors; } }
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

        /// <summary>Nombres cortos de los dos idiomas que enseña el editor (los da el catálogo del juego).</summary>
        public static string PrimaryLanguage { get { EnsureBuilt(); return _primaryLanguage; } }
        public static string SecondaryLanguage { get { EnsureBuilt(); return _secondaryLanguage; } }

        /// <summary>Texto en el idioma principal para una clave, o el fallback si no existe.</summary>
        public static string Loc(string key, string fallback = null)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(key) && _locEs.TryGetValue(key, out var v)) return v;
            return fallback;
        }

        /// <summary>Texto en el segundo idioma para una clave, o el fallback si no existe.</summary>
        public static string LocEn(string key, string fallback = null)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(key) && _locEn.TryGetValue(key, out var v)) return v;
            return fallback;
        }

        public static bool HasLoc(string key) { EnsureBuilt(); return !string.IsNullOrEmpty(key) && _locEs.ContainsKey(key); }

        /// <summary>Nombre legible del hablante: su texto localizado o, si no lo hay, el id sin el prefijo CHAR_ (CHAR_HERRERO → «Herrero»).</summary>
        public static string SpeakerName(string speakerNameId, bool isPlayer)
        {
            if (!string.IsNullOrEmpty(speakerNameId))
            {
                var loc = Loc(speakerNameId);
                if (!string.IsNullOrEmpty(loc)) return loc;
                return speakerNameId.StartsWith("CHAR_") ? Capitalize(speakerNameId.Substring(5)) : speakerNameId;
            }
            return isPlayer ? "Jugador" : "?";
        }

        /// <summary>Texto real de una línea de diálogo (localizado si hay textId).</summary>
        public static string LineText(string textId, string text)
        {
            if (!string.IsNullOrEmpty(textId))
            {
                var loc = Loc(textId);
                if (!string.IsNullOrEmpty(loc)) return loc;
                if (!string.IsNullOrEmpty(text)) return text;
                return $"«{textId}» (sin traducción)";
            }
            return text ?? "";
        }

        /// <summary>Nodos de los grafos que declaran este asset (NarrativeNode.Describe).</summary>
        public static IReadOnlyList<string> AssetUsers(UnityEngine.Object asset)
        {
            EnsureBuilt();
            return asset != null && _assetUsers.TryGetValue(asset, out var l) ? l : Array.Empty<string>();
        }

        /// <summary>Nodos de los grafos que tocan esta quest (NarrativeNode.Describe).</summary>
        public static IReadOnlyList<NodeUse> QuestUsers(string questId)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(questId) && _questUsers.TryGetValue(questId, out var l) ? l : Array.Empty<NodeUse>();
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
                case NarrativeKeyKind.GameStep: return _gameSteps.OrderBy(k => k).ToList();
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
                case NarrativeKeyKind.GameStep: return _gameSteps.Contains(value);
                default: return true;
            }
        }

        // ─── Construcción ─────────────────────────────────────────────────

        static void EnsureBuilt()
        {
            if (!_projectDirty && !_graphsDirty) return;
            using var medida = Story.StoryTiming.Measure(_projectDirty
                ? "Índice del proyecto: rehacer todo" + (s_lastInvalidation != null ? $" (por {s_lastInvalidation})" : string.Empty)
                : "Índice del proyecto: rehacer grafos");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _scannedFiles = 0; _cachedFiles = 0;
            try
            {
                if (_projectDirty) using (Story.StoryTiming.Measure("proyecto")) RebuildProject();
                using (Story.StoryTiming.Measure("grafos")) RebuildGraphs();
                _projectDirty = false;
                _graphsDirty = false;
                s_lastInvalidation = null;
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

        static void RebuildProject()
        {
            _quests.Clear(); _signals.Clear(); _actors.Clear();
            _locEs.Clear(); _locEn.Clear(); _anchors.Clear(); _gameSteps.Clear();
            _primaryLanguage = "Principal"; _secondaryLanguage = "Secundario";

            using (Story.StoryTiming.Measure("caché")) LoadCache();
            s_watchedAssetTypes.Clear();
            s_watchedAssetTypesDeclared = false;

            // Lo que solo el juego sabe leer: textos, misiones, actores, convenciones de sus escenas.
            var contributions = new Contributions();
            // Lo del propio kit: el id de los pasos del juego puestos en escenas y prefabs.
            contributions.SceneField(NarrativeGameStepHook.IdFieldName, NarrativeKeyKind.GameStep, who: "«Pedir al juego» atendido en");
            foreach (var catalog in NarrativeEditorExtensions.CreateAll<INarrativeProjectCatalog>())
            {
                using var parte = Story.StoryTiming.Measure("catálogo " + catalog.GetType().Name);
                try { catalog.Contribute(contributions); }
                catch (Exception ex) { Debug.LogWarning($"[NarrativeProjectIndex] El catálogo {catalog.GetType().Name} falló: {ex.Message}"); }
            }
            using (Story.StoryTiming.Measure("campos con rol")) ValidateSceneCache(contributions.SceneRulesSignature);

            // Escenas y prefabs (YAML): campos con rol declarado y campos que el juego reconoce por su nombre.
            using (Story.StoryTiming.Measure("escenas y prefabs")) ScanYamlAssets(contributions);

            // Código C#: RaiseCustom("LITERAL") / OnCustom("LITERAL").
            using (Story.StoryTiming.Measure("código")) ScanScriptsForSignals(contributions);

            MarkScanned();
            SaveCacheIfChanged();

            _projectSignals.Clear();
            foreach (var pair in _signals) _projectSignals[pair.Key] = (pair.Value.emitters.ToArray(), pair.Value.listeners.ToArray());
            _projectAnchors.Clear();
            _projectAnchors.UnionWith(_anchors);
        }

        static void RebuildGraphs()
        {
            // Se parte de lo del proyecto y se añade lo que declara cada grafo.
            _signals.Clear();
            foreach (var pair in _projectSignals)
            {
                var info = GetSignal(pair.Key);
                info.emitters.AddRange(pair.Value.emitters);
                info.listeners.AddRange(pair.Value.listeners);
            }
            _anchors.Clear();
            _anchors.UnionWith(_projectAnchors);
            _flags.Clear(); _graphs.Clear(); _assetUsers.Clear(); _questUsers.Clear();

            // Grafos: lo que declara cada nodo (señales que emite o espera, flags, marcas, quests, assets).
            var refs = new NarrativeNodeReferences();
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (g == null) continue;
                _graphs.Add(g);
                if (g.nodes == null) continue;
                foreach (var n in g.nodes)
                {
                    if (n == null) continue;
                    string where = $"Grafo {g.name} › {NarrativeNodeCatalog.TitleOf(n)}{(string.IsNullOrEmpty(n.DisplayTitle) ? "" : " '" + n.DisplayTitle + "'")}";
                    refs.Collect(n);
                    foreach (var sig in refs.Signals)
                    {
                        if (string.IsNullOrEmpty(sig.Key)) continue;
                        if (sig.Role == SignalRole.Emite) GetSignal(sig.Key).emitters.Add(where);
                        else if (sig.Role == SignalRole.Escucha) GetSignal(sig.Key).listeners.Add(where);
                    }
                    foreach (var f in refs.Flags) if (!string.IsNullOrEmpty(f)) _flags.Add(f);
                    foreach (var a in refs.Anchors) if (!string.IsNullOrEmpty(a)) _anchors.Add(a);
                    foreach (var q in refs.Quests)
                    {
                        if (string.IsNullOrEmpty(q.QuestId)) continue;
                        if (!_questUsers.TryGetValue(q.QuestId, out var qu)) _questUsers[q.QuestId] = qu = new List<NodeUse>();
                        qu.Add(new NodeUse(n, where));
                    }
                    foreach (var asset in refs.Assets)
                    {
                        if (!_assetUsers.TryGetValue(asset, out var users)) _assetUsers[asset] = users = new List<string>();
                        users.Add(where);
                    }
                }
            }
            _graphs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        }

        /// <summary>
        /// Lo que un catálogo del juego aporta al índice. Se llama al reconstruirlo, antes de leer
        /// grafos, escenas y código.
        /// </summary>
        public sealed class Contributions
        {
            internal readonly List<string> ScriptFolders = new();
            internal readonly Dictionary<string, (NarrativeKeyKind kind, SignalRole role, string who)> SceneFields = new(StringComparer.Ordinal);

            internal Contributions() { }

            /// <summary>Firma de las reglas de campos de escena (si cambia, se relee la caché de escenas).</summary>
            internal string SceneRulesSignature => string.Join(";", SceneFields.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value.kind}/{kv.Value.role}/{kv.Value.who}"));

            /// <summary>Nombres cortos de los idiomas principal y secundario («ES», «EN»).</summary>
            public void LanguageNames(string primary, string secondary)
            {
                if (!string.IsNullOrEmpty(primary)) _primaryLanguage = primary;
                if (!string.IsNullOrEmpty(secondary)) _secondaryLanguage = secondary;
            }

            /// <summary>Partes de ruta que el índice no debe leer nunca (maquetas generadas, material apartado…).</summary>
            public void ExcludePaths(params string[] fragments)
            {
                foreach (var f in fragments)
                    if (!string.IsNullOrEmpty(f) && !_skipFragments.Contains(f)) _skipFragments.Add(f);
            }

            /// <summary>Extensiones de archivo que lee el catálogo («.json»): si cambia uno, el índice se rehace.</summary>
            public void WatchFiles(params string[] extensions)
            {
                foreach (var e in extensions)
                    if (!string.IsNullOrEmpty(e)) s_projectExtensions.Add(e.StartsWith(".") ? e : "." + e);
            }

            /// <summary>
            /// Tipos de ScriptableObject que lee el catálogo (sus misiones, sus secuencias…): solo los
            /// cambios en assets de esos tipos rehacen el índice. Si ningún catálogo declara tipos,
            /// cualquier ScriptableObject que cambie lo rehace.
            /// </summary>
            public void WatchAssetTypes(params Type[] types)
            {
                foreach (var t in types)
                {
                    if (t == null) continue;
                    s_watchedAssetTypesDeclared = true;
                    if (!s_watchedAssetTypes.Contains(t)) s_watchedAssetTypes.Add(t);
                }
            }

            /// <summary>Carpetas donde buscar señales en el código. Sin ninguna, se busca en todo Assets.</summary>
            public void ScanScriptsIn(params string[] folders)
            {
                foreach (var f in folders)
                    if (!string.IsNullOrEmpty(f) && !ScriptFolders.Contains(f)) ScriptFolders.Add(f);
            }

            /// <summary>
            /// Campo de escenas y prefabs que, por su nombre, es una señal (con quién la emite o la
            /// escucha) o una marca. <paramref name="who"/> describe al componente («Sequencer en»).
            /// </summary>
            public void SceneField(string fieldName, NarrativeKeyKind kind, SignalRole role = SignalRole.Ninguno, string who = "Componente en")
            {
                if (string.IsNullOrEmpty(fieldName)) return;
                SceneFields[fieldName] = (kind, role, who);
            }

            public void Text(string key, string text) { if (!string.IsNullOrEmpty(key)) _locEs[key] = text; }

            public void SecondaryText(string key, string text) { if (!string.IsNullOrEmpty(key)) _locEn[key] = text; }

            /// <summary>Texto principal ya aportado para una clave (aporta los textos antes que lo que los usa).</summary>
            public string Loc(string key, string fallback)
                => !string.IsNullOrEmpty(key) && _locEs.TryGetValue(key, out var v) ? v : fallback;

            public void Quest(string questId, string displayName, string description, UnityEngine.Object asset, string path,
                string[] stepConditionIds, string[] stepDescriptions)
            {
                if (string.IsNullOrEmpty(questId)) return;
                _quests[questId] = new QuestInfo
                {
                    questId = questId,
                    asset = asset,
                    path = path,
                    displayName = string.IsNullOrEmpty(displayName) ? questId : displayName,
                    description = description,
                    stepConditionIds = stepConditionIds ?? Array.Empty<string>(),
                    stepDescriptions = stepDescriptions ?? Array.Empty<string>()
                };
            }

            /// <summary>Actor con id persistente. Si dos fuentes dan el mismo id, se queda la primera.</summary>
            public void Actor(string id, string source, string displayName)
            {
                if (string.IsNullOrEmpty(id) || _actors.ContainsKey(id)) return;
                _actors[id] = new ActorInfo { id = id, source = source, displayName = displayName };
            }

            public void AddSignalEmitter(string key, string where) { if (!string.IsNullOrEmpty(key)) GetSignal(key).emitters.Add(where); }

            public void AddSignalListener(string key, string where) { if (!string.IsNullOrEmpty(key)) GetSignal(key).listeners.Add(where); }

            public void Anchor(string id) { if (!string.IsNullOrEmpty(id)) _anchors.Add(id); }

            public bool IsExcluded(string path) => ShouldSkipPath(path);

            /// <summary>
            /// Valores que el catálogo lee de un archivo caro de abrir (p. ej. un prefab), guardados en
            /// la caché del índice: <paramref name="read"/> solo se llama si el archivo ha cambiado.
            /// </summary>
            public string[] CachedFileValues(string path, Func<string[]> read) => GetFileValues(path, read);
        }

        // ─── Caché en disco (Library/) ─────────────────────────────────────
        //
        // Los campos estáticos de esta clase se vacían en cada domain reload (cada Play), así que sin
        // caché el índice tendría que releer todas las escenas y cargar todos los prefabs de NPC.
        // La caché guarda, por archivo, lo poco que el índice necesita de él, junto con tamaño y
        // fecha de modificación; si no han cambiado, no se vuelve a abrir el archivo.

        const string CachePath = "Library/NarrativeProjectIndex.cache.json";
        const int CacheVersion = 5;

        [Serializable] class YamlCacheEntry { public string path; public long size; public long mtime; public string[] campos = Array.Empty<string>(); public string[] escucha = Array.Empty<string>(); public string[] emite = Array.Empty<string>(); }
        [Serializable] class FileCacheEntry { public string path; public long size; public long mtime; public string[] values = Array.Empty<string>(); }
        [Serializable] class CacheFile { public int version; public string campos; public string[] paths = Array.Empty<string>(); public List<YamlCacheEntry> yaml = new(); public List<FileCacheEntry> files = new(); }

        static Dictionary<string, YamlCacheEntry> _yamlCache;
        static Dictionary<string, FileCacheEntry> _fileCache;
        static string[] _yamlPaths;      // escenas y prefabs de la última vez que se listaron
        static bool _assetListDirty = true;
        static string _yamlCacheFirma;   // reglas con las que se leyeron las entradas de _yamlCache
        static bool _cacheChanged;
        static int _scannedFiles, _cachedFiles;
        static string _primaryLanguage = "Principal", _secondaryLanguage = "Secundario";

        // Carpetas que nunca aportan nada al índice. Los catálogos del juego añaden las suyas.
        static readonly string[] DefaultSkipFragments = { "/Plugins/", "/Demo/" };
        static readonly List<string> _skipFragments = new(DefaultSkipFragments);

        static bool ShouldSkipPath(string path)
        {
            foreach (var f in _skipFragments)
                if (path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void LoadCache()
        {
            _skipFragments.Clear();
            _skipFragments.AddRange(DefaultSkipFragments);
            if (_yamlCache != null) return; // ya cargada en este dominio
            _yamlCache = new Dictionary<string, YamlCacheEntry>(StringComparer.Ordinal);
            _fileCache = new Dictionary<string, FileCacheEntry>(StringComparer.Ordinal);
            _yamlCacheFirma = null;
            _cacheChanged = false;
            try
            {
                if (!File.Exists(CachePath)) return;
                var data = JsonUtility.FromJson<CacheFile>(File.ReadAllText(CachePath));
                if (data == null || data.version != CacheVersion) return;
                _yamlCacheFirma = data.campos;
                _yamlPaths = data.paths ?? Array.Empty<string>();
                _assetListDirty = _yamlPaths.Length == 0;
                foreach (var e in data.yaml) if (e != null && !string.IsNullOrEmpty(e.path)) _yamlCache[e.path] = e;
                foreach (var e in data.files) if (e != null && !string.IsNullOrEmpty(e.path)) _fileCache[e.path] = e;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProjectIndex] Caché ilegible, se regenera: {ex.Message}");
                _yamlCache.Clear(); _fileCache.Clear();
            }
        }

        /// <summary>
        /// Las entradas de escenas guardan lo que se leyó con unas reglas concretas (campos con rol y
        /// campos que el juego reconoce por nombre): si las reglas cambian, se vuelven a leer.
        /// </summary>
        static void ValidateSceneCache(string sceneRules)
        {
            string firma = CamposConRol.Firma + "|" + sceneRules;
            if (_yamlCacheFirma == firma) return;
            _yamlCache.Clear();
            _yamlCacheFirma = firma;
            _cacheChanged = true;
        }

        static void SaveCacheIfChanged()
        {
            if (!_cacheChanged || _yamlCache == null) return;
            _cacheChanged = false;
            try
            {
                var data = new CacheFile { version = CacheVersion, campos = _yamlCacheFirma, paths = _yamlPaths ?? Array.Empty<string>(), yaml = _yamlCache.Values.ToList(), files = _fileCache.Values.ToList() };
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                File.WriteAllText(CachePath, JsonUtility.ToJson(data));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProjectIndex] No se pudo guardar la caché: {ex.Message}");
                // Sin caché al día en disco, tras la próxima recarga de código hay que comprobarlo todo.
                SessionState.SetBool(TrustedKey, false);
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

        static string[] GetFileValues(string path, Func<string[]> read)
        {
            if (read == null) return Array.Empty<string>();
            if (Unchanged(path) && _fileCache.TryGetValue(path, out var known))
            {
                _cachedFiles++;
                return known.values ?? Array.Empty<string>();
            }
            if (!TryGetFileStamp(path, out var size, out var mtime)) return Array.Empty<string>();
            if (_fileCache.TryGetValue(path, out var cached) && cached.size == size && cached.mtime == mtime)
            {
                _cachedFiles++;
                return cached.values ?? Array.Empty<string>();
            }

            _scannedFiles++;
            string[] values;
            try { values = read() ?? Array.Empty<string>(); }
            catch { values = Array.Empty<string>(); }
            _fileCache[path] = new FileCacheEntry { path = path, size = size, mtime = mtime, values = values };
            _cacheChanged = true;
            return values;
        }

        static SignalInfo GetSignal(string key)
        {
            if (!_signals.TryGetValue(key, out var s))
                _signals[key] = s = new SignalInfo { key = key };
            return s;
        }

        static void ScanYamlAssets(Contributions rules)
        {
            foreach (var path in YamlAssetPaths())
            {
                if (ShouldSkipPath(path)) continue;
                var entry = GetYamlEntry(path, rules);
                if (entry == null) continue;
                string label = Path.GetFileNameWithoutExtension(path);
                foreach (var k in entry.emite) GetSignal(k).emitters.Add($"Emisor en {label}");
                foreach (var k in entry.escucha) GetSignal(k).listeners.Add($"Componente en {label}");
                foreach (var campo in entry.campos)
                {
                    int tab = campo.IndexOf('\t');
                    if (tab <= 0) continue;
                    var nombre = campo.Substring(0, tab);
                    var valor = campo.Substring(tab + 1);
                    if (!rules.SceneFields.TryGetValue(nombre, out var regla)) continue;
                    if (regla.kind == NarrativeKeyKind.Anchor) _anchors.Add(valor);
                    else if (regla.kind == NarrativeKeyKind.GameStep) _gameSteps.Add(valor);
                    else if (regla.kind == NarrativeKeyKind.Signal && regla.role == SignalRole.Escucha) GetSignal(valor).listeners.Add($"{regla.who} {label}");
                    else if (regla.kind == NarrativeKeyKind.Signal) GetSignal(valor).emitters.Add($"{regla.who} {label}");
                }
            }
        }

        /// Escenas y prefabs que hay que leer. La lista se guarda con la caché: pedírsela a la base de
        /// assets (dos consultas y una ruta por cada guid, con miles de prefabs) se llevaba más tiempo
        /// que leer los que habían cambiado. Solo se vuelve a pedir cuando entra, sale o se mueve alguna.
        static string[] YamlAssetPaths()
        {
            if (!_assetListDirty && _yamlPaths != null && _yamlPaths.Length > 0) return _yamlPaths;
            using (Story.StoryTiming.Measure("listar escenas y prefabs"))
            {
                _yamlPaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                    .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".unity", StringComparison.Ordinal) || p.EndsWith(".prefab", StringComparison.Ordinal))
                    .ToArray();
            }
            _assetListDirty = false;
            _cacheChanged = true;
            s_listed = null;
            return _yamlPaths;
        }

        static YamlCacheEntry GetYamlEntry(string path, Contributions rules)
        {
            if (Unchanged(path) && _yamlCache.TryGetValue(path, out var known))
            {
                _cachedFiles++;
                return known;
            }
            if (!TryGetFileStamp(path, out var size, out var mtime)) return null;
            if (_yamlCache.TryGetValue(path, out var cached) && cached.size == size && cached.mtime == mtime)
            {
                _cachedFiles++;
                return cached;
            }

            _scannedFiles++;
            var campos = new List<string>();
            var escucha = new List<string>(); var emite = new List<string>();
            try
            {
                using var reader = new StreamReader(path);
                string line;
                Dictionary<string, SignalRole> camposDelBloque = null;   // campos con rol del componente en curso
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("--- ", StringComparison.Ordinal)) { camposDelBloque = null; continue; }
                    if (line.Length > 200) continue;
                    int dosPuntos = line.IndexOf(':');
                    if (dosPuntos < 0) continue;
                    if (line.StartsWith("  m_Script:", StringComparison.Ordinal))
                    {
                        var sm = YamlScriptGuid.Match(line);
                        camposDelBloque = sm.Success ? CamposConRol.De(sm.Groups[1].Value) : null;
                        continue;
                    }
                    if (camposDelBloque != null)
                    {
                        var fm = YamlCampo.Match(line);
                        if (fm.Success && camposDelBloque.TryGetValue(fm.Groups[1].Value, out var rol))
                        {
                            var v = Clean(fm.Groups[2].Value);
                            if (!string.IsNullOrEmpty(v)) (rol == SignalRole.Escucha ? escucha : emite).Add(v);
                            continue;   // declarado con su rol: no pasa por las reglas por nombre
                        }
                    }
                    // Campos que el juego reconoce por su nombre (sin regex: la inmensa mayoría de
                    // líneas no son ninguno de ellos).
                    if (rules.SceneFields.Count == 0) continue;
                    var nombre = line.Substring(0, dosPuntos).Trim();
                    if (!rules.SceneFields.ContainsKey(nombre)) continue;
                    var valor = line.Substring(dosPuntos + 1).Trim();
                    if (valor.Length == 0 || valor.IndexOf(' ') >= 0) continue;
                    valor = Clean(valor);
                    if (!string.IsNullOrEmpty(valor)) campos.Add(nombre + "\t" + valor);
                }
            }
            catch { /* archivo binario o inaccesible: se guarda vacío para no reintentarlo cada vez */ }

            var entry = new YamlCacheEntry
            {
                path = path, size = size, mtime = mtime,
                campos = campos.ToArray(), escucha = escucha.ToArray(), emite = emite.ToArray()
            };
            _yamlCache[path] = entry;
            _cacheChanged = true;
            return entry;
        }

        static readonly Regex YamlScriptGuid = new(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        static readonly Regex YamlCampo = new(@"^  (\w+):\s*(\S*)\s*$", RegexOptions.Compiled);

        /// Campos de componentes marcados con [NarrativeKey(Signal, Rol = Emite/Escucha)], por GUID
        /// de su script. Así el índice sabe si un componente escucha o emite la señal de un campo,
        /// aunque otro componente tenga un campo con el mismo nombre y el rol contrario.
        static class CamposConRol
        {
            static Dictionary<string, Dictionary<string, SignalRole>> _porGuid;
            static string _firma;

            public static string Firma { get { Construir(); return _firma; } }

            public static Dictionary<string, SignalRole> De(string guid)
            {
                Construir();
                return _porGuid.TryGetValue(guid, out var d) ? d : null;
            }

            static void Construir()
            {
                if (_porGuid != null) return;
                _porGuid = new Dictionary<string, Dictionary<string, SignalRole>>(StringComparer.Ordinal);
                var firma = new List<string>();
                foreach (var campo in TypeCache.GetFieldsWithAttribute<NarrativeKeyAttribute>())
                {
                    var attr = (NarrativeKeyAttribute)Attribute.GetCustomAttribute(campo, typeof(NarrativeKeyAttribute));
                    if (attr == null || attr.Kind != NarrativeKeyKind.Signal || attr.Rol == SignalRole.Ninguno) continue;
                    var tipo = campo.DeclaringType;
                    if (tipo == null || !typeof(MonoBehaviour).IsAssignableFrom(tipo)) continue;
                    foreach (var g in AssetDatabase.FindAssets($"t:MonoScript {tipo.Name}"))
                    {
                        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                        if (ms == null || ms.GetClass() != tipo) continue;
                        if (!_porGuid.TryGetValue(g, out var d)) _porGuid[g] = d = new Dictionary<string, SignalRole>(StringComparer.Ordinal);
                        d[campo.Name] = attr.Rol;
                        firma.Add($"{g}.{campo.Name}={attr.Rol}");
                    }
                }
                firma.Sort(StringComparer.Ordinal);
                _firma = string.Join(";", firma);
            }
        }

        static readonly Regex CsRaise = new(@"RaiseCustom\(\s*""([A-Za-z0-9_:\-]+)""", RegexOptions.Compiled);
        static readonly Regex CsOn = new(@"OnCustom\(\s*""([A-Za-z0-9_:\-]+)""", RegexOptions.Compiled);

        static void ScanScriptsForSignals(Contributions contributions)
        {
            var folders = (contributions.ScriptFolders.Count > 0 ? contributions.ScriptFolders : new List<string> { "Assets" })
                .Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return;
            var guids = AssetDatabase.FindAssets("t:MonoScript", folders);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs") || ShouldSkipPath(path)) continue;
                // Cada script se lee solo si ha cambiado desde la última vez (caché por tamaño y fecha).
                var found = GetFileValues(path, () => ReadScriptKeys(path));
                if (found.Length == 0) continue;
                string label = Path.GetFileNameWithoutExtension(path);
                foreach (var entry in found)
                {
                    if (entry.Length < 3 || entry[1] != '\t') continue;
                    var key = entry.Substring(2);
                    switch (entry[0])
                    {
                        case 'E': GetSignal(key).emitters.Add($"Código {label}.cs"); break;
                        case 'L': GetSignal(key).listeners.Add($"Código {label}.cs"); break;
                        case 'G': _gameSteps.Add(key); break;
                    }
                }
            }
        }

        /// Claves literales de un script: «E\tSEÑAL» (la emite), «L\tSEÑAL» (la escucha) y
        /// «G\tid» (registra algo que atiende «Pedir al juego»).
        static string[] ReadScriptKeys(string path)
        {
            var text = File.ReadAllText(path);
            bool signals = text.IndexOf("Custom(", StringComparison.Ordinal) >= 0;
            bool steps = text.IndexOf("NarrativeGameSteps.Register", StringComparison.Ordinal) >= 0;
            if (!signals && !steps) return Array.Empty<string>();
            var keys = new List<string>();
            if (signals)
            {
                foreach (Match m in CsRaise.Matches(text)) keys.Add("E\t" + m.Groups[1].Value);
                foreach (Match m in CsOn.Matches(text)) keys.Add("L\t" + m.Groups[1].Value);
            }
            if (steps) foreach (Match m in CsGameStep.Matches(text)) keys.Add("G\t" + m.Groups[1].Value);
            return keys.ToArray();
        }

        static readonly Regex CsGameStep = new(@"NarrativeGameSteps\.Register\(\s*""([^""]+)""", RegexOptions.Compiled);

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
    }
}
