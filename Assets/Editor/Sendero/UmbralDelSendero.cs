#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// El umbral del Sendero en la Piedra Ancestral (MainWorld) y el arranque del grafo «Sendero».
/// Idempotente: cada ejecución rehace lo generado con los mismos datos.
///
/// Monta:
///   · Grafo Assets/NarrativeGraph/Sendero.asset (etiqueta «Sendero» en el hub de Start y en el
///     arranque de MainWorld): espera los requisitos (libro y ritual) → la Piedra admite el conjuro →
///     Will acepta leerlo → cinemática del conjuro → llegada al Sendero.
///   · En MainWorld, bajo «Santuario ancestral»: el punto «Leer el conjuro» (A, popup Sí/No, solo Will),
///     el portal al Sendero (la ventana de VistaDelPortal; abierto desde SENDERO_PORTAL_ABIERTO) y el ancla de prueba «PiedraAncestral».
///   · La secuencia SEQ_SenderoConjuro (la rellena el guion Sendero_01_ElConjuro al hornearlo).
///   · Sendero.unity en la build, con el ancla de llegada «SenderoLlegada».
public static class UmbralDelSendero
{
    public const string Etiqueta = "Sendero";
    const string RutaGrafo = "Assets/NarrativeGraph/Sendero.asset";
    const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_SenderoConjuro.asset";
    const string RutaDialogo = "Assets/_DIALOGUES/Common/DG_SENDERO_PIEDRA_PREGUNTA.asset";
    const string RutaCarta = "Assets/Prefabs/Cap1/Carta.prefab";
    const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    const string RutaSendero = "Assets/Scenes/Worlds/Sendero.unity";
    const string NombreGenerado = "Umbral del Sendero (generado)";
    const string PlantillaDeSecuencia = "SEQ_PerasEldran";

    public const string SenalCapituloAnterior = "CH_Cap2_BACK_1";
    public const string SenalRequisitos = "SENDERO_REQUISITOS_CUMPLIDOS";
    public const string SenalRitualListo = "SENDERO_RITUAL_LISTO";
    public const string SenalConjuroAceptado = "SENDERO_CONJURO_ACEPTADO";
    public const string SenalConjuroStart = "SENDERO_CONJURO_START";
    public const string SenalConjuroDone = "SENDERO_CONJURO_DONE";
    public const string SenalPortalAbierto = "SENDERO_PORTAL_ABIERTO";
    public const string SenalLlegada = "SENDERO_LLEGADA";
    public const string AnclaPiedra = "PiedraAncestral";
    public const string AnclaLlegada = "SenderoLlegada";

    public const string NodoRitualListo = "sendero-ritual-listo";

    // El monolito y el recinto: la entrada está al sur (z −516).
    static readonly Vector2 Monolito = new(565f, -505f);
    static readonly Vector2 PuntoConjuro = new(565f, -507.4f);
    static readonly Vector2 PuntoPortal = new(565f, -508.6f);
    static readonly Vector2 PuntoAncla = new(565f, -519.5f);

    [MenuItem("El Sendero/Sendero/1. Montar el umbral de la Piedra Ancestral")]
    public static void Montar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("El montaje necesita el Editor fuera de Play.");
        var informe = new List<string>();
        var secuencia = AsegurarSecuencia(informe);
        var dialogo = AsegurarDialogo(informe);
        var grafo = AsegurarGrafo(secuencia, informe);
        AsegurarEnLaBuild(RutaSendero, informe);
        AsegurarHub(grafo, informe);
        MontarMainWorld(dialogo, informe);
        MontarLlegadaAlSendero(informe);
        AssetDatabase.SaveAssets();
        Debug.Log("[UmbralDelSendero] " + string.Join(" · ", informe));
    }

    // ── Assets ──────────────────────────────────────────────────────────────────────────────

    static SequenceDefinition AsegurarSecuencia(List<string> informe)
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<SequenceDefinition>();
            AssetDatabase.CreateAsset(def, RutaSecuencia);
            informe.Add("secuencia creada");
        }
        def.displayName = "El conjuro de la Piedra Ancestral";
        def.summary = "Will recita el ritual y se abre el portal al Sendero. Guion: Sendero_01_ElConjuro.";
        def.signalIn = SenalConjuroStart;
        def.signalOut = SenalConjuroDone;
        def.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        EditorUtility.SetDirty(def);
        return def;
    }

    static DialogueAsset AsegurarDialogo(List<string> informe)
    {
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(RutaDialogo);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, RutaDialogo);
            informe.Add("pregunta creada");
        }
        dlg.lines = new[]
        {
            new DialogueLine
            {
                textId = "SENDERO_PIEDRA_PREGUNTA",
                text = "¿Quieres leer el conjuro para abrir el portal al Sendero de las Estrellas?"
            }
        };
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static NarrativeGraph AsegurarGrafo(SequenceDefinition secuencia, List<string> informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (g == null)
        {
            g = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(g, RutaGrafo);
            informe.Add("grafo creado");
        }
        const float y = 0f;
        Poner(g, "sendero-inicio", "Inicio del Sendero", new Vector2(0, y), new StartNode(), "sendero-tras-capitulo");
        // Puente con el capítulo anterior: así «Probar desde el nodo» da por terminados los capítulos
        // de antes (si no, arrancan desde su inicio y sale el prólogo). Hoy el último construido es el 2.
        Poner(g, "sendero-tras-capitulo", "Tras el capítulo anterior (CH_Cap2_BACK_1)", new Vector2(160, y - 160),
            new WaitCustomEventNode { eventKey = SenalCapituloAnterior }, "sendero-requisitos");
        Poner(g, "sendero-requisitos", "Espera: libro prohibido y ritual (lo emite el capítulo de la Piedra)", new Vector2(320, y),
            new WaitCustomEventNode { eventKey = SenalRequisitos }, NodoRitualListo);
        Poner(g, NodoRitualListo, "La Piedra admite el conjuro (probar desde aquí)", new Vector2(700, y),
            new RaiseCustomEventNode { eventKey = SenalRitualListo }, "sendero-espera-conjuro");
        Poner(g, "sendero-espera-conjuro", "Will acepta leer el conjuro ante la Piedra", new Vector2(1080, y),
            new WaitCustomEventNode { eventKey = SenalConjuroAceptado, acceptEarlierEvent = false }, "sendero-cinematica-conjuro");
        Poner(g, "sendero-cinematica-conjuro", "Cinemática: el conjuro abre el portal", new Vector2(1460, y),
            new PlayCinematicNode
            {
                cinematicName = "El conjuro de la Piedra Ancestral",
                signalIn = SenalConjuroStart,
                signalDone = SenalConjuroDone,
                secuencia = secuencia,
                plantillaDeAjustes = PlantillaDeSecuencia
            }, "sendero-llegada");
        Poner(g, "sendero-llegada", "Llegada al Sendero", new Vector2(1840, y),
            new WaitCustomEventNode { eventKey = SenalLlegada });
        g.startNodeGuid = "sendero-inicio";
        EditorUtility.SetDirty(g);
        return g;
    }

    static void Poner(NarrativeGraph g, string id, string titulo, Vector2 posicion, NarrativeNode n, params string[] salidas)
    {
        var anterior = g.FindNode(id);
        if (anterior != null && anterior.outputs != null && salidas.Length == 0)
            salidas = anterior.outputs.ToArray(); // lo que una tanda posterior colgó de aquí se conserva
        n.guid = id; n.displayTitle = titulo; n.chapter = "Sendero"; n.position = posicion;
        n.outputs = new List<string>(salidas);
        if (anterior == null) g.nodes.Add(n); else g.nodes[g.nodes.IndexOf(anterior)] = n;
    }

    static void AsegurarEnLaBuild(string ruta, List<string> informe)
    {
        var escenas = EditorBuildSettings.scenes.ToList();
        if (escenas.Any(e => e.path == ruta)) return;
        escenas.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
        informe.Add(ruta + " añadida a la build");
    }

    // ── Escenas ─────────────────────────────────────────────────────────────────────────────

    /// Abre la escena (en aditivo si no estaba) y dice si hay que cerrarla al acabar.
    static Scene Abrir(string ruta, out bool cerrar)
    {
        var escena = SceneManager.GetSceneByPath(ruta);
        cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        return escena;
    }

    static void Guardar(Scene escena, bool cerrar)
    {
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
    }

    static T Buscar<T>(Scene escena) where T : Component
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var c = raiz.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    static void AsegurarHub(NarrativeGraph grafo, List<string> informe)
    {
        var escena = Abrir(RutaStart, out bool cerrar);
        var hub = Buscar<NarrativeGraphHub>(escena)
            ?? throw new InvalidOperationException("Start.unity: no encuentro el NarrativeGraphHub.");
        var so = new SerializedObject(hub);
        var lista = so.FindProperty("graphs");
        bool esta = false;
        for (int i = 0; i < lista.arraySize; i++)
        {
            var e = lista.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("label").stringValue != Etiqueta) continue;
            e.FindPropertyRelative("graph").objectReferenceValue = grafo;
            esta = true;
        }
        if (!esta)
        {
            lista.arraySize++;
            var e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
            e.FindPropertyRelative("label").stringValue = Etiqueta;
            e.FindPropertyRelative("graph").objectReferenceValue = grafo;
            e.FindPropertyRelative("initialBlackboardValues").arraySize = 0;
            informe.Add("grafo «Sendero» en el hub");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Guardar(escena, cerrar);
    }

    static void AnadirEtiquetaAlArranque(Scene escena, List<string> informe)
    {
        var arranque = Buscar<NarrativeGraphStarter>(escena);
        if (arranque == null) { informe.Add($"AVISO: {escena.name} sin NarrativeGraphStarter"); return; }
        var so = new SerializedObject(arranque);
        var etiquetas = so.FindProperty("graphLabels");
        for (int i = 0; i < etiquetas.arraySize; i++)
            if (etiquetas.GetArrayElementAtIndex(i).stringValue == Etiqueta) return;
        etiquetas.arraySize++;
        etiquetas.GetArrayElementAtIndex(etiquetas.arraySize - 1).stringValue = Etiqueta;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.Add($"{escena.name} arranca el grafo «Sendero»");
    }

    static void MontarMainWorld(DialogueAsset dialogo, List<string> informe)
    {
        var escena = Abrir(RutaMainWorld, out bool cerrar);
        AnadirEtiquetaAlArranque(escena, informe);

        Transform santuario = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            santuario = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Santuario ancestral"));
            if (santuario != null) break;
        }
        if (santuario == null) throw new InvalidOperationException("MainWorld: no encuentro «Santuario ancestral».");

        var viejo = santuario.Find(NombreGenerado);
        if (viejo != null) UnityEngine.Object.DestroyImmediate(viejo.gameObject);
        var raizUmbral = new GameObject(NombreGenerado).transform;
        raizUmbral.SetParent(santuario, true);
        raizUmbral.position = Suelo(Monolito);

        var conjuro = MontarPuntoDelConjuro(raizUmbral, dialogo);
        var portal = MontarPortal(raizUmbral);

        var ancla = new GameObject("Ancla de prueba — " + AnclaPiedra);
        ancla.transform.SetParent(raizUmbral, true);
        ancla.transform.position = Suelo(PuntoAncla) + Vector3.up * 0.1f;
        ancla.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        ancla.AddComponent<SpawnAnchor>().anchorId = AnclaPiedra;

        // Leer el conjuro: desde que la Piedra lo admite hasta que se abre el portal.
        var tramoConjuro = raizUmbral.gameObject.AddComponent<ActivoEnTramo>();
        tramoConjuro.Tramo.empiezaConSenal.Add(SenalRitualListo);
        tramoConjuro.Tramo.acabaConSenal.Add(SenalPortalAbierto);
        tramoConjuro.Objetos.Add(conjuro);
        // El portal: abierto desde el conjuro.
        var tramoPortal = raizUmbral.gameObject.AddComponent<ActivoEnTramo>();
        tramoPortal.Tramo.empiezaConSenal.Add(SenalPortalAbierto);
        tramoPortal.Objetos.Add(portal);
        conjuro.SetActive(false);
        portal.SetActive(false);

        Guardar(escena, cerrar);
        informe.Add("MainWorld: umbral montado");
    }

    static GameObject MontarPuntoDelConjuro(Transform raiz, DialogueAsset dialogo)
    {
        var go = new GameObject("Leer el conjuro");
        go.transform.SetParent(raiz, true);
        go.transform.position = Suelo(PuntoConjuro);
        go.layer = LayerMask.NameToLayer("Interactable");
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;
        col.center = Vector3.up;

        var carta = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCarta)
            ?? throw new InvalidOperationException("No encuentro " + RutaCarta + " (de ahí sale el icono de la A).");
        var hintOrigen = carta.transform.Find("Canvas")
            ?? throw new InvalidOperationException("Carta.prefab ya no tiene el Canvas del icono.");
        var hint = UnityEngine.Object.Instantiate(hintOrigen.gameObject, go.transform);
        hint.name = "Canvas";
        hint.transform.localPosition = new Vector3(0f, 2.6f, 0f);

        var interactuable = go.AddComponent<Interactable>();
        var origen = new SerializedObject(carta.GetComponent<Interactable>());
        var so = new SerializedObject(interactuable);
        so.FindProperty("mode").intValue = (int)InteractableMode.ConfirmationPopup;
        so.FindProperty("hint").objectReferenceValue = hint;
        so.FindProperty("iconSet").objectReferenceValue = origen.FindProperty("iconSet").objectReferenceValue;
        so.FindProperty("dialogue").objectReferenceValue = dialogo;
        so.ApplyModifiedPropertiesWithoutUndo();

        var soloWill = go.AddComponent<WillOnlyInteractable>();
        var soW = new SerializedObject(soloWill);
        soW.FindProperty("avisoKey").stringValue = "SENDERO_PIEDRA_SOLO_WILL";
        soW.ApplyModifiedPropertiesWithoutUndo();

        var evento = go.AddComponent<InteractableToNarrativeEvent>();
        evento.eventKey = SenalConjuroAceptado;
        UnityEventTools.AddPersistentListener(interactuable.OnConfirm, evento.Send);
        return go;
    }

    static GameObject MontarPortal(Transform raiz)
    {
        var go = new GameObject("Portal al Sendero");
        go.transform.SetParent(raiz, true);
        go.transform.position = Suelo(PuntoPortal);
        // De cara a la entrada del recinto (sur), por donde llega Will.
        go.transform.rotation = Quaternion.LookRotation(Vector3.back);

        // La ventana de la cinemática final, con el Sendero dentro. Se cruza caminando hacia la
        // Piedra (norte) y al otro lado se aparece mirando el camino (+Z del Sendero).
        VistaDelPortal.Ventana(go.transform, VistaDelPortal.HaciaElSendero, Vector3.forward, Vector3.forward);

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 1.5f, 0f);
        col.size = new Vector3(2.4f, 3f, 0.8f);
        var portal = go.AddComponent<PortalTrigger>();
        portal.targetSceneName = "Sendero";
        portal.targetAnchorId = AnclaLlegada;
        return go;
    }

    static void MontarLlegadaAlSendero(List<string> informe)
    {
        var escena = Abrir(RutaSendero, out bool cerrar);
        Transform suelo = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            suelo = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Suelo_00_Llegada");
            if (suelo != null) break;
        }
        var existente = escena.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<SpawnAnchor>(true))
            .FirstOrDefault(a => a.anchorId == AnclaLlegada);
        var ancla = existente != null ? existente.gameObject : new GameObject("Ancla — " + AnclaLlegada);
        if (existente == null)
        {
            SceneManager.MoveGameObjectToScene(ancla, escena);
            ancla.AddComponent<SpawnAnchor>().anchorId = AnclaLlegada;
        }
        Vector3 p = suelo != null ? suelo.position : Vector3.zero;
        ancla.transform.position = new Vector3(p.x, p.y + 0.6f, p.z);
        ancla.transform.rotation = Quaternion.LookRotation(Vector3.forward);

        if (Buscar<WorldBootstrap>(escena) == null)
        {
            var wb = new GameObject("WorldBootstrap");
            SceneManager.MoveGameObjectToScene(wb, escena);
            wb.AddComponent<WorldBootstrap>();
            informe.Add("Sendero: WorldBootstrap añadido");
        }
        AnadirEtiquetaAlArranque(escena, informe);
        informe.Add($"Sendero: ancla de llegada en {ancla.transform.position}");
        Guardar(escena, cerrar);
    }

    static Vector3 Suelo(Vector2 xz)
    {
        var desde = new Vector3(xz.x, 200f, xz.y);
        foreach (var h in Physics.RaycastAll(desde, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            // Solo el terreno o lo que está en la capa de suelo (no árboles ni el monolito).
            if (h.collider is TerrainCollider || h.collider.gameObject.layer == LayerMask.NameToLayer("Floor"))
                return h.point;
        }
        return new Vector3(xz.x, 18f, xz.y);
    }
}
#endif
