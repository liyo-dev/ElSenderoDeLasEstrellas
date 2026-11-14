using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lo que el montaje de los capítulos pone en las escenas: en MainWorld, bajo «Capítulos
/// (generado)» (se borra y se rehace entero), las marcas (SpawnAnchor: anclas de prueba, de guion y
/// de agenda), los puntos del roster, las llegadas (<see cref="SignalEmitter"/> por cercanía dentro
/// de un <see cref="ActivoEnTramo"/>), los puntos de inspección (Interactable con su diálogo + señal),
/// el atrezo y un SequencePlayer por cinemática de MainWorld; en Start, los grafos en el hub y las
/// misiones en el catálogo. También el roster, las agendas y las secuencias.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    // ── Tablas que rellena cada capítulo ────────────────────────────────────────────────────

    private sealed class Marca { public string Cap, Nombre; public Vector3 Pos; public float Rumbo; }
    private sealed class PuntoDeRoster { public string Cap, SpawnId; public Vector3 Pos; public float Rumbo; }
    /// <summary>
    /// Un sitio al que llegar. Con <c>Mision</c>, solo cuenta con esa misión activa y, mientras su
    /// tramo está en curso, el minimapa lo marca (salvo <c>SinMarcador</c>).
    /// </summary>
    private sealed class Llegada { public string Cap, Nombre, Senal, Mision; public Vector3 Pos; public float Radio; public TramoDeHistoria Tramo; public bool SinMarcador; }
    /// <summary>
    /// Un punto que mirar (Interactable con su diálogo y su señal). Con <c>Mision</c> y <c>Paso</c>, el minimapa
    /// lo marca y le flota encima el icono de «aquí» hasta que ese paso de la misión se cumple.
    /// </summary>
    private sealed class Inspeccion { public string Cap, Nombre, Dialogo, Senal, Mision; public int Paso = -1; public Vector3 Pos; public TramoDeHistoria Tramo; }
    /// <summary>Una pieza de atrezo. Con <c>Escondite</c>, no choca y es un sitio donde no se ve al jugador (sigilo).</summary>
    private sealed class Atrezo { public string Cap, Nombre, Prefab; public Vector3 Pos; public float Rumbo, Tamano; public bool Colision, Escondite; public TramoDeHistoria Tramo; }
    private sealed class Cine
    {
        public string Cap, Clave, Guion, Nombre, Resumen, Escena = "MainWorld", Plantilla;
        public PresentacionDeTexto Texto = PresentacionDeTexto.Subtitulo;
        public string Secuencia => "SEQ_" + Guion;
        public string RutaSecuencia => $"{CarpetaSecuencias}/{Secuencia}.asset";
        public string Entrada => Clave + "_START";
        public string Salida => Clave + "_DONE";
    }
    /// <summary>Un roster: entrada del NpcRoster_MainWorld.</summary>
    private sealed class Papel { public string SpawnId, Prefab, Nombre, Id, Voz = "Vecino", Flag; public bool Invertir, Activo = true, NavMesh = true; }
    /// <summary>Lo que hace un personaje en cada situación de la historia: (nombre, tramo, presencia, marca, frases: una ruta o varias separadas por «|»). Se pone en el grafo como piezas «Personaje desde aquí».</summary>
    private sealed class SituacionesDe { public string Actor; public List<(string nombre, TramoDeHistoria tramo, ActorPresence presencia, string marca, string dialogo)> Entradas = new(); }

    private static readonly List<Marca> s_marcas = new();
    private static readonly List<PuntoDeRoster> s_puntos = new();
    private static readonly List<Llegada> s_llegadas = new();
    private static readonly List<Inspeccion> s_inspecciones = new();
    private static readonly List<Atrezo> s_atrezo = new();
    private static readonly List<Cine> s_cines = new();
    private static readonly List<Papel> s_papeles = new();
    private static readonly List<SituacionesDe> s_personajes = new();

    private static void LimpiarTablas()
    {
        s_marcas.Clear(); s_puntos.Clear(); s_llegadas.Clear(); s_inspecciones.Clear(); s_atrezo.Clear();
        s_cines.Clear(); s_papeles.Clear(); s_personajes.Clear(); s_misiones.Clear();
    }

    private static TramoDeHistoria Tramo(string empieza, string acaba = null)
    {
        var t = new TramoDeHistoria();
        if (!string.IsNullOrEmpty(empieza)) t.empiezaConSenal.Add(empieza);
        if (!string.IsNullOrEmpty(acaba)) t.acabaConSenal.Add(acaba);
        return t;
    }

    private static void M(string cap, string nombre, float x, float z, float rumbo = 0f, float y = float.NaN) =>
        s_marcas.Add(new Marca { Cap = cap, Nombre = nombre, Pos = new Vector3(x, y, z), Rumbo = rumbo });

    /// <summary>La cinemática de la tabla con esa clave, como nodo del grafo.</summary>
    private static NarrativeNode Cinematica(string clave)
    {
        var c = s_cines.Find(x => x.Clave == clave);
        if (c == null) return new PlayCinematicNode { cinematicName = clave + " (no está en la tabla)" };
        return new PlayCinematicNode
        {
            cinematicName = c.Nombre, signalIn = c.Entrada, signalDone = c.Salida,
            secuencia = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia),
            plantillaDeAjustes = c.Plantilla ?? "",
        };
    }

    // ── Secuencias ──────────────────────────────────────────────────────────────────────────

    private static void AsegurarSecuencias(StringBuilder informe)
    {
        int nuevas = 0;
        foreach (var c in s_cines)
        {
            var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<SequenceDefinition>();
                AssetDatabase.CreateAsset(def, c.RutaSecuencia);
                nuevas++;
            }
            def.displayName = c.Nombre;
            def.summary = c.Resumen;
            def.signalIn = c.Entrada;
            def.signalOut = c.Salida;
            def.presentacionDeTexto = c.Texto;
            if (def.phases.Count == 0)
                def.phases.Add(new SequencePhase { name = "Guion", beats = new List<SequenceBeat> { new GuionBeat { note = "Guion: " + c.Guion } } });
            EditorUtility.SetDirty(def);
        }
        informe.AppendLine($"Cinemáticas: {s_cines.Count} secuencias ({nuevas} nuevas).");
    }

    // ── Sendero ─────────────────────────────────────────────────────────────────────────────

    /// <summary>El Sendero esperaba el fin del Cap. 2 (puente de pruebas): pasa a esperar el del último capítulo montado.</summary>
    private static void AjustarSendero(StringBuilder informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Sendero.asset");
        if (g?.FindNode("sendero-tras-capitulo") is WaitCustomEventNode w)
        {
            w.eventKey = FinCap4;
            w.displayTitle = $"Tras el capítulo anterior ({FinCap4})";
            EditorUtility.SetDirty(g);
            informe.AppendLine($"Sendero: espera {FinCap4} (antes {FinCap2}).");
        }
        else informe.AppendLine("AVISO: el Sendero no tiene el nodo «sendero-tras-capitulo».");
    }

    // ── Roster y agendas ────────────────────────────────────────────────────────────────────

    private static void AsegurarRoster(StringBuilder informe)
    {
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(RutaRoster);
        if (roster == null) { informe.AppendLine("AVISO: falta " + RutaRoster); return; }
        int cambios = 0;
        foreach (var p in s_papeles)
        {
            var e = roster.entries.Find(x => x != null && x.spawnId == p.SpawnId);
            if (e == null) { e = new NpcRosterSO.Entry(); roster.entries.Add(e); }
            e.enabled = true;
            e.spawnId = p.SpawnId;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Prefab);
            if (prefab == null) informe.AppendLine($"AVISO: roster {p.SpawnId}: falta {p.Prefab}.");
            e.prefab = prefab;
            if (prefab != null) AsegurarIconoDeMision(p.Prefab, informe);
            e.gameObjectName = p.Nombre;
            e.persistenceId = p.Id ?? "";
            e.vozDeReacciones = p.Voz;
            e.startActive = p.Activo;
            e.requireNavMesh = p.NavMesh;
            e.requiredFlag = string.IsNullOrEmpty(p.Flag) ? "" : NarrativeFlags.Key(p.Flag);
            e.invertCondition = p.Invertir;
            cambios++;
        }
        EditorUtility.SetDirty(roster);
        informe.AppendLine($"Roster de MainWorld: {cambios} entradas de los capítulos.");
    }

    /// <summary>El icono de «tiene algo para ti» que el grafo pone sobre los NPCs (el de Eldran, Erika…).</summary>
    private const string IconoDeMision = "Assets/_NPCs/OverHead/Canvas OverHere.prefab";

    /// <summary>
    /// Todo personaje del reparto puede recibir el icono del grafo cuando hay que hablar con él: le hace
    /// falta un <see cref="NarrativeActor"/> con icono por defecto (sin él, el nodo de charla no pinta nada).
    /// </summary>
    private static void AsegurarIconoDeMision(string rutaPrefab, StringBuilder informe)
    {
        var icono = Cargar<GameObject>(IconoDeMision);
        if (icono == null) { informe.AppendLine("AVISO: falta " + IconoDeMision); return; }
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var actor = raiz.GetComponent<NarrativeActor>();
            if (actor != null && actor.DefaultQuestIconPrefab != null) return;
            if (actor == null) actor = raiz.AddComponent<NarrativeActor>();
            var so = new SerializedObject(actor);
            so.FindProperty("defaultQuestIconPrefab").objectReferenceValue = icono;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            informe.AppendLine($"Icono de misión puesto en {rutaPrefab}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    private static void PonerPiezasDePersonajes(StringBuilder informe)
    {
        var situaciones = new List<Sendero.Narrative.Editor.PiezasDePersonaje.Situacion>();
        foreach (var a in s_personajes)
            foreach (var (nombre, tramo, presencia, marca, dialogo) in a.Entradas)
            {
                // Varias frases (una por charla, rotando): rutas separadas por «|».
                var frases = (dialogo ?? "").Split('|').Where(r => !string.IsNullOrEmpty(r)).Select(r => (NarrativeDialogueAsset)Cargar<DialogueAsset>(r));
                situaciones.Add(Sendero.Narrative.Editor.PiezasDePersonaje.Desde(a.Actor, nombre, tramo, presencia, marca, frases, informe));
            }
        informe.AppendLine($"Personajes según la historia: {s_personajes.Count} ({situaciones.Count} situaciones).");
        Sendero.Narrative.Editor.PiezasDePersonaje.Poner(situaciones, informe);
    }

    // ── Start ───────────────────────────────────────────────────────────────────────────────

    private static void RegistrarEnStart(StringBuilder informe, params (string etiqueta, NarrativeGraph grafo)[] grafos)
    {
        var escena = SceneManager.GetSceneByPath(RutaStart);
        bool cerrar = !escena.isLoaded;
        bool teniaCambios = !cerrar && escena.isDirty;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        bool cambia = false;

        // Restos de un montaje de prefabs interrumpido (Cap2ExtrasPrefabs crea en la escena activa).
        foreach (var resto in escena.GetRootGameObjects().Where(r => r.name == "CampamentoContrabandistas").ToArray())
        {
            Object.DestroyImmediate(resto);
            cambia = true;
            informe.AppendLine("Start: retirado un CampamentoContrabandistas suelto (resto de un montaje interrumpido).");
        }

        var hub = Buscar<NarrativeGraphHub>(escena);
        if (hub == null) informe.AppendLine("AVISO: Start.unity no tiene NarrativeGraphHub.");
        else
        {
            var so = new SerializedObject(hub);
            var lista = so.FindProperty("graphs");
            foreach (var (etiqueta, grafo) in grafos)
            {
                bool esta = false;
                for (int i = 0; i < lista.arraySize; i++)
                {
                    var e = lista.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("label").stringValue != etiqueta) continue;
                    esta = true;
                    if (e.FindPropertyRelative("graph").objectReferenceValue != grafo)
                    { e.FindPropertyRelative("graph").objectReferenceValue = grafo; cambia = true; }
                }
                if (esta) continue;
                lista.arraySize++;
                var n = lista.GetArrayElementAtIndex(lista.arraySize - 1);
                n.FindPropertyRelative("label").stringValue = etiqueta;
                n.FindPropertyRelative("graph").objectReferenceValue = grafo;
                n.FindPropertyRelative("initialBlackboardValues").arraySize = 0;
                cambia = true;
                informe.AppendLine($"Start: grafo «{etiqueta}» en el hub.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var quests = Buscar<QuestManager>(escena);
        if (quests == null) informe.AppendLine("AVISO: Start.unity no tiene QuestManager.");
        else
        {
            var so = new SerializedObject(quests);
            var catalogo = so.FindProperty("questCatalog");
            foreach (var mision in s_misiones)
            {
                bool esta = false;
                for (int i = 0; i < catalogo.arraySize; i++)
                    if (catalogo.GetArrayElementAtIndex(i).objectReferenceValue == mision) esta = true;
                if (esta) continue;
                catalogo.arraySize++;
                catalogo.GetArrayElementAtIndex(catalogo.arraySize - 1).objectReferenceValue = mision;
                cambia = true;
                informe.AppendLine($"Start: misión {mision.questId} en el catálogo.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (cambia)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            if (teniaCambios) informe.AppendLine("AVISO: Start.unity tenía cambios sin guardar; no la guardo: hay que guardarla a mano.");
            else EditorSceneManager.SaveScene(escena);
        }
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
    }

    // ── MainWorld ───────────────────────────────────────────────────────────────────────────

    private static void MontarEnMainWorld(StringBuilder informe, params string[] etiquetas)
    {
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool cerrar = !escena.isLoaded;
        bool teniaCambios = !cerrar && escena.isDirty;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);

        // Los arranques del grafo de MainWorld también lanzan los capítulos nuevos.
        foreach (var arranque in escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NarrativeGraphStarter>(true)))
        {
            var so = new SerializedObject(arranque);
            var l = so.FindProperty("graphLabels");
            var actuales = new List<string>();
            for (int i = 0; i < l.arraySize; i++) actuales.Add(l.GetArrayElementAtIndex(i).stringValue);
            if (!actuales.Contains("Cap2")) continue;
            foreach (var e in etiquetas)
            {
                if (actuales.Contains(e)) continue;
                // Antes del Sendero, para que el orden del arranque siga el de la historia.
                int sitio = actuales.IndexOf("Sendero");
                if (sitio < 0) sitio = actuales.Count;
                actuales.Insert(sitio, e);
                informe.AppendLine($"MainWorld: el arranque {arranque.name} lanza también «{e}».");
            }
            l.arraySize = actuales.Count;
            for (int i = 0; i < actuales.Count; i++) l.GetArrayElementAtIndex(i).stringValue = actuales[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var r in escena.GetRootGameObjects().Where(r => r.name == NombreGenerado).ToArray())
            Object.DestroyImmediate(r);
        var raiz = new GameObject(NombreGenerado);
        SceneManager.MoveGameObjectToScene(raiz, escena);

        var marcas = new Dictionary<string, Transform>();
        foreach (var grupo in s_marcas.GroupBy(m => m.Cap))
        {
            var padre = Hijo(raiz.transform, grupo.Key + " · marcas");
            foreach (var m in grupo)
            {
                var go = new GameObject(m.Nombre);
                go.transform.SetParent(padre, false);
                go.transform.SetPositionAndRotation(AlSuelo(m.Pos), Quaternion.Euler(0f, m.Rumbo, 0f));
                go.AddComponent<SpawnAnchor>().anchorId = m.Nombre;
                marcas[m.Nombre] = go.transform;
            }
        }
        foreach (var grupo in s_puntos.GroupBy(p => p.Cap))
        {
            var padre = Hijo(raiz.transform, grupo.Key + " · roster");
            foreach (var p in grupo)
            {
                var go = new GameObject("SP_" + p.SpawnId);
                go.transform.SetParent(padre, false);
                go.transform.SetPositionAndRotation(AlSuelo(p.Pos), Quaternion.Euler(0f, p.Rumbo, 0f));
                go.AddComponent<NpcSpawnPoint>().spawnId = p.SpawnId;
            }
        }
        // El marcador viejo de Liam (en el origen) deja paso al de los capítulos.
        foreach (var t in escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NpcSpawnPoint>(true)))
            if (t.transform.root != raiz.transform && s_puntos.Any(p => p.SpawnId == t.spawnId))
            {
                informe.AppendLine($"MainWorld: el marcador antiguo {t.name} ({t.spawnId}) se retira.");
                Object.DestroyImmediate(t.gameObject);
            }

        foreach (var grupo in s_llegadas.GroupBy(l => l.Cap))
        {
            var padre = Hijo(raiz.transform, grupo.Key + " · llegadas");
            foreach (var l in grupo)
            {
                var tramo = Hijo(padre, l.Nombre);
                var go = new GameObject("Disparador");
                go.transform.SetParent(tramo, false);
                go.transform.position = AlSuelo(l.Pos);
                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = true; col.radius = l.Radio;
                var s = go.AddComponent<SignalEmitter>();
                s.eventKey = l.Senal; s.trigger = SignalEmitter.TriggerType.PhysicsTrigger; s.once = true;
                s.radioDeProximidad = l.Radio;
                if (!string.IsNullOrEmpty(l.Mision)) s.soloConLaMision = l.Mision;
                if (!string.IsNullOrEmpty(l.Mision) && !l.SinMarcador)
                {
                    // El destino se ve en el minimapa mientras el tramo esté en curso (el disparador se enciende con él).
                    var marcador = new SerializedObject(go.AddComponent<QuestObjectiveMarker>());
                    marcador.FindProperty("questId").stringValue = l.Mision;
                    marcador.FindProperty("stepIndex").intValue = -1;
                    marcador.FindProperty("showAfterStep").intValue = -1;
                    marcador.ApplyModifiedPropertiesWithoutUndo();
                }
                EnTramo(tramo, l.Tramo, go);
            }
        }
        foreach (var grupo in s_inspecciones.GroupBy(i => i.Cap))
        {
            var padre = Hijo(raiz.transform, grupo.Key + " · inspección");
            foreach (var i in grupo)
            {
                var tramo = Hijo(padre, i.Nombre);
                var go = new GameObject("Punto");
                go.transform.SetParent(tramo, false);
                go.transform.position = AlSuelo(i.Pos);
                var interactuable = PuntoDeInteraccion(go.transform, 1.8f);
                var so = new SerializedObject(interactuable);
                so.FindProperty("mode").enumValueIndex = (int)InteractableMode.OpenDialogue;
                so.FindProperty("dialogue").objectReferenceValue = Cargar<DialogueAsset>(i.Dialogo);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!string.IsNullOrEmpty(i.Senal))
                {
                    var s = go.AddComponent<SignalEmitter>();
                    s.eventKey = i.Senal; s.trigger = SignalEmitter.TriggerType.OnInteract; s.once = true;
                }
                if (!string.IsNullOrEmpty(i.Mision) && i.Paso >= 0)
                {
                    var marcador = new SerializedObject(go.AddComponent<QuestObjectiveMarker>());
                    marcador.FindProperty("questId").stringValue = i.Mision;
                    marcador.FindProperty("stepIndex").intValue = i.Paso;
                    marcador.FindProperty("showAfterStep").intValue = -1;
                    marcador.FindProperty("iconoSobreLaCabeza").objectReferenceValue = Cargar<GameObject>(IconoDeMision);
                    marcador.ApplyModifiedPropertiesWithoutUndo();
                }
                EnTramo(tramo, i.Tramo, go);
            }
        }
        foreach (var grupo in s_atrezo.GroupBy(a => a.Cap))
        {
            var padre = Hijo(raiz.transform, grupo.Key + " · atrezo");
            foreach (var a in grupo)
            {
                var go = PonerAtrezo(padre, a, informe);
                if (go != null && a.Tramo != null)
                {
                    var tramo = Hijo(padre, a.Nombre + " (tramo)");
                    go.transform.SetParent(tramo, true);
                    EnTramo(tramo, a.Tramo, go);
                }
            }
        }
        MontarCinematicas(escena, raiz.transform, marcas, informe);
        PonerExtrasDeEscena(raiz.transform, marcas, informe);

        EditorSceneManager.MarkSceneDirty(escena);
        if (teniaCambios) informe.AppendLine("AVISO: MainWorld tenía cambios sin guardar; no la guardo: hay que guardarla a mano.");
        else EditorSceneManager.SaveScene(escena);
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
        informe.AppendLine($"MainWorld: {s_marcas.Count} marcas, {s_puntos.Count} puntos del roster, {s_llegadas.Count} llegadas, " +
                           $"{s_inspecciones.Count} puntos de inspección y {s_atrezo.Count} piezas de atrezo.");
    }

    /// <summary>Lo que cada capítulo pone en la escena además de las tablas (campamento, barro…).</summary>
    static partial void PonerExtrasCap3(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe);
    static partial void PonerExtrasCap4(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe);

    private static void PonerExtrasDeEscena(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        PonerExtrasCap3(raiz, marcas, informe);
        PonerExtrasCap4(raiz, marcas, informe);
        PonerExtrasAldeaDelMar(raiz, informe);
        PonerExtrasPuebloDeWill(raiz, informe);
    }

    private static void MontarCinematicas(Scene escena, Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var padre = Hijo(raiz, "Cinemáticas");
        var perfil = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PerfilDeAudio);
        int n = 0;
        // Todas las cinemáticas tienen su reproductor en MainWorld, también las de un interior que el
        // grafo carga en aditivo (la torre de Liam): los actores se buscan por id en cualquier escena.
        foreach (var c in s_cines.Where(c => string.IsNullOrEmpty(c.Plantilla)))
        {
            var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia);
            if (def == null) continue;
            var go = new GameObject(c.Secuencia);
            go.transform.SetParent(padre, false);
            var driver = go.AddComponent<CinematicCameraDriver>();
            var stage = go.AddComponent<SequenceStage>();
            var player = go.AddComponent<SequencePlayer>();
            var so = new SerializedObject(stage);
            var m = so.FindProperty("_marks");
            var propias = marcas.Where(k => s_marcas.Any(x => x.Nombre == k.Key && x.Cap == c.Cap)).ToList();
            m.arraySize = propias.Count;
            for (int i = 0; i < propias.Count; i++)
            {
                var e = m.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = propias[i].Key;
                e.FindPropertyRelative("target").objectReferenceValue = propias[i].Value;
            }
            var props = so.FindProperty("_props");
            var atrezo = raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("PROP_") && t.name.Contains(c.Cap.Replace(". ", ""))).ToList();
            props.arraySize = atrezo.Count;
            for (int i = 0; i < atrezo.Count; i++)
            {
                var e = props.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = atrezo[i].name.Split(' ')[0];
                e.FindPropertyRelative("target").objectReferenceValue = atrezo[i];
                e.FindPropertyRelative("eyeHeight").floatValue = 0.5f;
                e.FindPropertyRelative("esObstaculo").boolValue = false;
            }
            so.FindProperty("_cameraDriver").objectReferenceValue = driver;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sp = new SerializedObject(player);
            sp.FindProperty("_definition").objectReferenceValue = def;
            sp.FindProperty("_stage").objectReferenceValue = stage;
            sp.FindProperty("_cinematicCamera").objectReferenceValue = driver;
            sp.FindProperty("_audioProfile").objectReferenceValue = perfil;
            sp.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        informe.AppendLine($"Cinemáticas: {n} reproductores en MainWorld.");
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────────────

    private static Transform Hijo(Transform padre, string nombre)
    {
        var t = padre.Find(nombre);
        if (t != null) return t;
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    /// <summary>Enciende <paramref name="objeto"/> solo durante el tramo (sin tramo, siempre).</summary>
    private static void EnTramo(Transform padre, TramoDeHistoria tramo, GameObject objeto)
    {
        if (tramo == null) return;
        var a = padre.gameObject.AddComponent<ActivoEnTramo>();
        a.Tramo.empiezaConSenal.AddRange(tramo.empiezaConSenal);
        a.Tramo.acabaConSenal.AddRange(tramo.acabaConSenal);
        a.Objetos.Add(objeto);
        objeto.SetActive(false);
    }

    /// <summary>Punto con el botón de interactuar: zona en la capa Interactable e icono de la A.</summary>
    private static Interactable PuntoDeInteraccion(Transform go, float radio)
    {
        var zona = new GameObject("Zona");
        zona.transform.SetParent(go, false);
        zona.transform.localPosition = Vector3.up;
        int capa = LayerMask.NameToLayer("Interactable");
        if (capa >= 0) zona.layer = capa;
        var esfera = zona.AddComponent<SphereCollider>();
        esfera.isTrigger = true;
        esfera.radius = radio;
        var interactuable = go.gameObject.AddComponent<Interactable>();
        interactuable.SetMode(InteractableMode.Custom);
        MontadorDeBarco.CrearHint(go, Vector3.up * 1.2f, interactuable);
        return interactuable;
    }

    private static GameObject PonerAtrezo(Transform padre, Atrezo a, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(a.Prefab);
        if (prefab == null) { informe.AppendLine($"Atrezo: falta {a.Prefab} ({a.Nombre})."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.gameObject.scene);
        go.name = a.Nombre;
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(AlSuelo(a.Pos), Quaternion.Euler(0f, a.Rumbo, 0f));
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        if (!a.Colision) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        if (a.Tamano > 0f)
        {
            var b = Limites(go);
            float lado = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (lado > 0.001f) go.transform.localScale *= a.Tamano / lado;
        }
        var bb = Limites(go);
        go.transform.position += Vector3.up * (go.transform.position.y - bb.min.y);
        if (a.Escondite) HacerEscondite(go);
        return go;
    }

    // Trigger del tamaño de la pieza (y al menos de la altura de alguien agachado) con su Escondite.
    private static void HacerEscondite(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var b = Limites(go);
        Vector3 escala = go.transform.lossyScale;
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.center = go.transform.InverseTransformPoint(b.center + Vector3.up * Mathf.Max(0f, 1.6f - b.size.y) * 0.5f);
        caja.size = new Vector3(b.size.x / escala.x, Mathf.Max(b.size.y, 1.6f) / escala.y, b.size.z / escala.z);
        go.AddComponent<Escondite>();
    }

    /// <summary>
    /// Quita la hierba de detalle (flores incluidas) del terreno en un círculo: el sitio de una escena
    /// donde las flores altas taparían lo que importa. El vestido del mundo lo respeta al repintar
    /// (cuenta como retoque a mano). Devuelve cuántas celdas ha vaciado.
    /// </summary>
    private static int DespejarHierba(Vector3 centro, float radio)
    {
        int vaciadas = 0;
        foreach (var terreno in Terrain.activeTerrains)
        {
            var datos = terreno.terrainData;
            if (datos == null || datos.detailPrototypes.Length == 0) continue;
            Vector3 origen = terreno.transform.position, tam = datos.size;
            int res = datos.detailResolution;
            float celdaX = tam.x / res, celdaZ = tam.z / res;
            int i0 = Mathf.FloorToInt((centro.x - radio - origen.x) / celdaX), i1 = Mathf.CeilToInt((centro.x + radio - origen.x) / celdaX);
            int k0 = Mathf.FloorToInt((centro.z - radio - origen.z) / celdaZ), k1 = Mathf.CeilToInt((centro.z + radio - origen.z) / celdaZ);
            i0 = Mathf.Clamp(i0, 0, res - 1); i1 = Mathf.Clamp(i1, 0, res - 1);
            k0 = Mathf.Clamp(k0, 0, res - 1); k1 = Mathf.Clamp(k1, 0, res - 1);
            if (i1 < i0 || k1 < k0) continue;
            int ancho = i1 - i0 + 1, alto = k1 - k0 + 1;
            Undo.RegisterCompleteObjectUndo(datos, "Despejar hierba");
            for (int capa = 0; capa < datos.detailPrototypes.Length; capa++)
            {
                int[,] celdas = datos.GetDetailLayer(i0, k0, ancho, alto, capa);
                bool cambiada = false;
                for (int k = 0; k < alto; k++)
                    for (int i = 0; i < ancho; i++)
                    {
                        if (celdas[k, i] == 0) continue;
                        float x = origen.x + (i0 + i + 0.5f) * celdaX, z = origen.z + (k0 + k + 0.5f) * celdaZ;
                        if ((x - centro.x) * (x - centro.x) + (z - centro.z) * (z - centro.z) > radio * radio) continue;
                        celdas[k, i] = 0;
                        cambiada = true;
                        vaciadas++;
                    }
                if (cambiada) datos.SetDetailLayer(i0, k0, capa, celdas);
            }
            EditorUtility.SetDirty(datos);
        }
        return vaciadas;
    }

    private static Bounds Limites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>
    /// El punto sobre el suelo: el terreno (las marcas van en sitios abiertos); si no hay terreno
    /// debajo, el colisionador sólido más alto. Si la marca trae altura, esa.
    /// </summary>
    private static Vector3 AlSuelo(Vector3 p)
    {
        if (!float.IsNaN(p.y)) return p;
        float mejor = float.NegativeInfinity;
        foreach (var t in Terrain.activeTerrains)
        {
            var d = t.terrainData; var o = t.transform.position;
            if (p.x < o.x || p.z < o.z || p.x > o.x + d.size.x || p.z > o.z + d.size.z) continue;
            mejor = Mathf.Max(mejor, t.SampleHeight(new Vector3(p.x, 0f, p.z)) + o.y);
        }
        if (float.IsNegativeInfinity(mejor))
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 600f, p.z), Vector3.down, 1200f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.point.y > mejor) mejor = hit.point.y;
        return new Vector3(p.x, float.IsNegativeInfinity(mejor) ? 0f : mejor, p.z);
    }
}
