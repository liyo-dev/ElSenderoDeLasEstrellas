using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Los recuerdos de la Prueba de Will que se ven a través de un espejo. Cada uno es un decorado
/// montado lejos, bajo el palacio, y un espejo vivo en modo ventana que lo enseña
/// (<see cref="EspejoVivo.VentanaA"/>), con su secuencia (SequencePlayer + SequenceStage) y el
/// disparador que la arranca al acercarse. Lo llama <see cref="PruebaDeWillBuilder"/> al montar
/// el nivel, y tiene su propio menú para rehacer solo los recuerdos sobre la escena ya montada
/// (sin volver a hornear los reflejos del palacio). El guion de cada recuerdo está en
/// Assets/_SEQUENCES/Guiones y se hornea al terminar. Ver INC-881.
/// </summary>
public static class PruebaDeWillRecuerdos
{
    /// <summary>Los decorados de los recuerdos quedan a esta altura: ni se ven ni estorban desde el palacio.</summary>
    private const float Profundidad = -300f;

    private const string PerfilDeAudio = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string Kingdom = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs";
    private const float AlturaDelAsiento = 0.5f;

    /// <summary>Guion del recuerdo del discurso.</summary>
    public const string GuionDelDiscurso = "Assets/_SEQUENCES/Guiones/Prueba_01_ElDiscurso.guion.txt";

    private const string PrefijoDeRaiz = "Recuerdo · ";

    [MenuItem("El Sendero/Escenario/Prueba de Will: rehacer los recuerdos")]
    public static void RehacerMenu()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Recuerdos] Sal de Play para rehacer los recuerdos."); return; }
        var escena = SceneManager.GetSceneByPath(PruebaDeWillBuilder.RutaEscena);
        if (!escena.isLoaded)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Recuerdos] Hay escenas con cambios sin guardar: guárdalas o abre Laberinto antes.");
                    return;
                }
            escena = EditorSceneManager.OpenScene(PruebaDeWillBuilder.RutaEscena, OpenSceneMode.Single);
        }
        Montar(escena);
        EditorSceneManager.SaveScene(escena);
        Hornear(escena);
    }

    /// <summary>Quita los recuerdos que hubiera y los vuelve a montar sobre el palacio de la escena.</summary>
    public static void Montar(Scene escena)
    {
        Transform palacio = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name.StartsWith(PrefijoDeRaiz)) Object.DestroyImmediate(raiz);
            else if (raiz.name == "Palacio de espejos") palacio = raiz.transform;
        }
        if (palacio == null) { Debug.LogWarning("[Recuerdos] La escena no tiene «Palacio de espejos»: monta antes el nivel."); return; }
        Discurso(escena, Sala(palacio, "R3"), 1);
        EditorSceneManager.MarkSceneDirty(escena);
    }

    /// <summary>Hornea los guiones de los recuerdos (con storyboard) y guarda la escena si el horneado la ha tocado.</summary>
    public static string Hornear(Scene escena)
    {
        HorneadorDeGuion.Hornear(GuionDelDiscurso, storyboard: true);
        if (escena.isDirty) EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        return HorneadorDeGuion.UltimoResumen;
    }

    private static Transform Sala(Transform palacio, string id)
    {
        foreach (Transform hijo in palacio)
            if (hijo.name.StartsWith(id + " · ")) return hijo;
        return null;
    }

    /// <summary>Monta el recuerdo del discurso frente al espejo vivo del lado <paramref name="ladoDelEspejo"/> de su sala.</summary>
    private static void Discurso(Scene escena, Transform sala, int ladoDelEspejo)
    {
        var espejo = sala != null ? sala.Find($"Lado {ladoDelEspejo}/Espejo vivo") : null;
        if (espejo == null) { Debug.LogWarning($"[Recuerdos] La sala del discurso no tiene espejo vivo en el lado {ladoDelEspejo}."); return; }

        var raiz = new GameObject(PrefijoDeRaiz + "el discurso").transform;
        SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);

        // La ventana: el mismo punto y giro que el espejo, bajado a la profundidad de los recuerdos.
        // El decorado queda detrás (+Z de la ventana) y el suelo, a la altura del suelo del palacio.
        var ventana = new GameObject("Ventana").transform;
        ventana.SetParent(raiz, false);
        ventana.SetPositionAndRotation(espejo.position + Vector3.up * Profundidad, espejo.rotation);
        AsignarVentana(espejo, ventana);
        QuitarRotulo(escena, "Ventana del recuerdo: el discurso");

        Vector3 adelante = Plano(espejo.forward);        // de la ventana hacia el decorado
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
        Vector3 suelo = new Vector3(ventana.position.x, Profundidad, ventana.position.z);
        Vector3 silla = suelo + adelante * 7f;

        var decorado = new GameObject("Decorado: la plaza").transform;
        decorado.SetParent(raiz, false);
        Plaza(decorado, silla, adelante, derecha);

        // Los personajes del recuerdo: Will con 9 años de pie en la silla, Eldran a su lado y los vecinos en corro.
        var stage = new GameObject("SEQ_PruebaWill_Discurso").transform;
        stage.SetParent(raiz, false);
        var marcas = new List<Transform>();
        Transform Marca(string nombre, Vector3 pos, Vector3 mirar)
        {
            var t = KitDeLaberintoDeEspejos.Punto(stage, nombre, pos, mirar);
            marcas.Add(t);
            return t;
        }

        var actores = new List<(string id, GameObject go)>();
        var willNino = PersonajesConAspectoFijo.Asegurar("WillNino");
        var nino = Personaje(willNino, "Will niño", decorado, silla + Vector3.up * AlturaDelAsiento, -adelante);
        actores.Add(("Actor_WillNino", nino));
        Marca("M_Discurso_Nino", silla + Vector3.up * AlturaDelAsiento, -adelante);

        var eldran = Personaje(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/Eldran.prefab"), "Eldran", decorado,
            silla + Girar(-adelante, 30f) * 2.2f, Vector3.zero, silla);
        actores.Add(("Actor_EldranRecuerdo", eldran));
        Marca("M_Discurso_Eldran", eldran.transform.position, silla - eldran.transform.position);

        string[] vecinos = { "Manuel", "Patricia", "Roberto", "Sara", "Nora", "Pueblo/Jorge" };
        float[] angulos = { -42f, 42f, -64f, 64f, -86f, 86f };
        for (int i = 0; i < vecinos.Length; i++)
        {
            string ruta = $"Assets/_NPCs/{vecinos[i]}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null) { Debug.LogWarning($"[Recuerdos] No se encontró {ruta}."); continue; }
            Vector3 p = silla + Girar(-adelante, angulos[i]) * (3.4f + (i / 2) * 0.6f);
            var v = Personaje(prefab, prefab.name, decorado, p, Vector3.zero, silla);
            actores.Add(($"Actor_Vecino{i + 1}", v));
            Marca($"M_Discurso_Vecino{i + 1}", p, silla - p);
        }

        // El grupo delante del espejo y las cámaras.
        Vector3 normal = -Plano(espejo.forward);         // del espejo hacia la sala
        Vector3 pieDelEspejo = new Vector3(espejo.position.x, 0f, espejo.position.z);
        Vector3 haciaElEspejo = -normal;
        Vector3 izquierda = -Vector3.Cross(Vector3.up, haciaElEspejo);
        Marca("M_Discurso_Will", pieDelEspejo + normal * 3.0f, haciaElEspejo);
        Marca("M_Discurso_Estela", pieDelEspejo + normal * 3.4f + izquierda * 1.3f, haciaElEspejo);
        Marca("M_Discurso_Liam", pieDelEspejo + normal * 3.4f - izquierda * 1.3f, haciaElEspejo);
        Marca("M_Discurso_Espejo", pieDelEspejo + normal * 0.3f, normal);
        Marca("M_Discurso_CamGrupo", pieDelEspejo + normal * 7.0f, haciaElEspejo);
        Marca("M_Discurso_CamNino", silla - adelante * 2.6f + derecha * 0.4f, adelante);
        Marca("M_Discurso_CamPlaza", silla - adelante * 6.5f - derecha * 2.0f, adelante);

        Secuencia(stage.gameObject, actores, marcas);
        Disparador(raiz, pieDelEspejo + normal * 3.0f);

        var nav = decorado.gameObject.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        nav.BuildNavMesh();
    }

    // ── El decorado ─────────────────────────────────────────────────────────────

    /// <summary>Suelo de plaza, casas en semicírculo detrás de la silla, globos y la silla.</summary>
    private static void Plaza(Transform padre, Vector3 silla, Vector3 adelante, Vector3 derecha)
    {
        var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        suelo.name = "Suelo de la plaza";
        suelo.transform.SetParent(padre, false);
        suelo.transform.position = silla;
        suelo.transform.localScale = new Vector3(5f, 1f, 5f);
        var mat = MaterialDeLaPlaza();
        if (mat != null) suelo.GetComponent<Renderer>().sharedMaterial = mat;
        int capaSuelo = LayerMask.NameToLayer("Floor");
        if (capaSuelo >= 0) suelo.layer = capaSuelo;
        suelo.isStatic = true;

        // La silla con un colisionador a la altura del asiento: Will niño se sube encima.
        var asiento = Colocar($"{Kingdom}/Props/Furniture/Chair/Chair01_a01.prefab", "Silla", padre, silla, -adelante, conColision: false);
        if (asiento != null)
        {
            var caja = asiento.AddComponent<BoxCollider>();
            caja.center = new Vector3(0f, AlturaDelAsiento * 0.5f, 0f);
            caja.size = new Vector3(0.5f, AlturaDelAsiento, 0.5f);
        }

        // Casas en semicírculo detrás de la silla, de cara a la plaza.
        float[] angulos = { -58f, -29f, 0f, 29f, 58f };
        for (int i = 0; i < angulos.Length; i++)
        {
            Vector3 dir = Girar(adelante, angulos[i]);
            var casa = Colocar($"{Kingdom}/Building Combination/BuildingAT0{i + 1}.prefab", $"Casa {i + 1}", padre,
                silla + dir * 14f, -dir, conColision: true);
            if (casa != null) AlejarHasta(casa.transform, silla, dir, 12f);
        }

        // Globos de cumpleaños a los lados de la silla.
        string[] globos = { "Balloon01_a01", "Balloon01_a03", "Balloon01_a05", "Balloon01_a07" };
        for (int i = 0; i < globos.Length; i++)
        {
            float lado = i % 2 == 0 ? -1f : 1f;
            Colocar($"{Kingdom}/Props/Goods/{globos[i]}.prefab", "Globo", padre,
                silla + derecha * lado * (1.4f + (i / 2) * 0.5f) + adelante * (0.4f + (i / 2) * 0.6f), -adelante, conColision: false);
        }
    }

    /// <summary>El suelo de plaza del pack, repetido a escala de plaza (copia propia: el original lo usan otros decorados).</summary>
    private static Material MaterialDeLaPlaza()
    {
        const string ruta = "Assets/Art/Materials/Sendero/PruebaWill/Mat_Recuerdo_Plaza.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Ground02.mat");
            if (original == null) return null;
            mat = new Material(original);
            AssetDatabase.CreateAsset(mat, ruta);
        }
        foreach (string textura in new[] { "_BaseMap", "_MainTex" })
            if (mat.HasProperty(textura)) mat.SetTextureScale(textura, new Vector2(8f, 8f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject Colocar(string ruta, string nombre, Transform padre, Vector3 pos, Vector3 mirar, bool conColision)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null) { Debug.LogWarning($"[Recuerdos] No se encontró {ruta}."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Plano(mirar)));
        if (!conColision)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        return go;
    }

    /// <summary>Empuja el objeto hacia fuera hasta que su borde más cercano queda a <paramref name="radio"/> m del centro.</summary>
    private static void AlejarHasta(Transform t, Vector3 centro, Vector3 dir, float radio)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float cercano = Vector3.Dot(b.ClosestPoint(centro) - centro, dir);
        t.position += dir * (radio - cercano);
    }

    private static GameObject Personaje(GameObject prefab, string nombre, Transform padre, Vector3 pos, Vector3 mirar, Vector3? mirarA = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.name = nombre;
        Vector3 dir = mirarA.HasValue ? mirarA.Value - pos : mirar;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Plano(dir)));
        // En el recuerdo nadie pasea ni se le puede hablar: solo actúa en la secuencia.
        var manager = go.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("disableWander").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var interactable = go.GetComponent<Interactable>();
        if (interactable != null) interactable.enabled = false;
        return go;
    }

    // ── La secuencia ────────────────────────────────────────────────────────────

    /// <summary>SequencePlayer + SequenceStage con los actores del recuerdo (el grupo se busca por su nombre en la escena).</summary>
    private static void Secuencia(GameObject go, List<(string id, GameObject go)> actores, List<Transform> marcas)
    {
        var def = Definicion("Assets/_SEQUENCES/SEQ_PruebaWill_Discurso.asset", "Prueba de Will: el discurso",
            "Will, Estela y Liam ven en un espejo a Will con nueve años trabándose en el discurso de su cumpleaños.",
            "PRUEBA_WILL_DISCURSO_START", "PRUEBA_WILL_DISCURSO_DONE", "Prueba_01_ElDiscurso");

        var driver = go.AddComponent<CinematicCameraDriver>();
        var stage = go.AddComponent<SequenceStage>();
        var player = go.AddComponent<SequencePlayer>();

        foreach (var (nombre, id) in new[] { ("_ESTELA", "Actor_Estela"), ("_LIAM", "Actor_Liam") })
        {
            var g = GameObject.Find(nombre);
            if (g != null) actores.Add((id, g));
        }

        var so = new SerializedObject(stage);
        var lista = so.FindProperty("_actors");
        lista.arraySize = actores.Count;
        for (int i = 0; i < actores.Count; i++)
        {
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = actores[i].id;
            e.FindPropertyRelative("target").objectReferenceValue = actores[i].go.transform;
        }
        var m = so.FindProperty("_marks");
        m.arraySize = marcas.Count;
        for (int i = 0; i < marcas.Count; i++)
        {
            var e = m.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue = marcas[i].name;
            e.FindPropertyRelative("target").objectReferenceValue = marcas[i];
        }
        so.FindProperty("_cameraDriver").objectReferenceValue = driver;
        so.ApplyModifiedPropertiesWithoutUndo();

        var sp = new SerializedObject(player);
        sp.FindProperty("_definition").objectReferenceValue = def;
        sp.FindProperty("_stage").objectReferenceValue = stage;
        sp.FindProperty("_cinematicCamera").objectReferenceValue = driver;
        sp.FindProperty("_audioProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PerfilDeAudio);
        sp.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// La definición de la secuencia: señales y una fase con el GuionBeat que el horneado del guion
    /// rellena. Si ya existe, solo se actualizan los datos de cabecera.
    /// </summary>
    private static SequenceDefinition Definicion(string ruta, string nombre, string resumen, string entrada, string salida, string guion)
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(ruta);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<SequenceDefinition>();
            AssetDatabase.CreateAsset(def, ruta);
        }
        def.displayName = nombre;
        def.summary = resumen;
        def.signalIn = entrada;
        def.signalOut = salida;
        def.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        if (def.phases.Count == 0)
            def.phases.Add(new SequencePhase { name = "Guion", beats = new List<SequenceBeat> { new GuionBeat { note = "Guion: " + guion } } });
        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>
    /// Arranca el recuerdo cuando el jugador se acerca al espejo. Es el hecho de la escena; cuando el
    /// Sendero tenga su grafo, lo esperará un WaitCustomEventNode con la misma clave.
    /// </summary>
    private static void Disparador(Transform padre, Vector3 junto)
    {
        var go = new GameObject("Disparador: el discurso");
        go.transform.SetParent(padre, false);
        go.transform.position = junto;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(6f, 3f, 6f);
        col.center = Vector3.up * 1.5f;
        var emisor = go.AddComponent<SignalEmitter>();
        emisor.eventKey = "PRUEBA_WILL_DISCURSO_START";
        emisor.trigger = SignalEmitter.TriggerType.PhysicsTrigger;
        emisor.requiredTag = "Player";
        emisor.once = true;
        emisor.radioDeProximidad = 3f;
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────

    /// <summary>El recuerdo sustituye al rótulo de maqueta de su punto, que taparía el espejo.</summary>
    private static void QuitarRotulo(Scene escena, string texto)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != "Rótulos de maqueta") continue;
            var rotulo = raiz.transform.Find("Rótulo: " + texto);
            if (rotulo != null) Object.DestroyImmediate(rotulo.gameObject);
        }
    }

    private static void AsignarVentana(Transform espejo, Transform ventana)
    {
        var vivo = espejo.GetComponent<EspejoVivo>();
        var so = new SerializedObject(vivo);
        so.FindProperty("ventanaA").objectReferenceValue = ventana;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

    private static Vector3 Girar(Vector3 v, float grados) => Quaternion.Euler(0f, grados, 0f) * v;
}
