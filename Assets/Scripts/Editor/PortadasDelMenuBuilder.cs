using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en MainMenu.unity las tres portadas del menú principal (una por etapa de la historia) y
/// las cablea a <see cref="PortadaDelMenu"/> y a <see cref="MainMenuController"/>:
///   1. Península: Will sentado en un banco en su pueblo, mirando la plaza de los árboles rosas
///      (partida nueva). El pueblo y el terreno se copian de MainWorld.
///   2. Viaje: los tres en el barco, con el agua del mar de MainWorld (flag SALIDA_DEL_REINO).
///   3. Sendero: los tres volando en un cielo de noche con estrellas y un sendero de luz (flag
///      ENTRADA_AL_SENDERO); reaprovecha los personajes voladores y la luz que ya tenía el menú.
///
/// Se puede volver a ejecutar: rehace el decorado generado de cada portada y reutiliza lo demás.
/// Las opciones «Previsualizar» encienden una portada en el Editor para verla en la Game view
/// sin dar Play (los personajes voladores solo se colocan en Play).
/// </summary>
public static class PortadasDelMenuBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string RaizNombre = "PortadaDelMenu";
    const string DecoradoNombre = "Decorado (generado)";

    const string NombrePeninsula = "Etapa 1 · Península";
    const string NombreViaje     = "Etapa 2 · Viaje";
    const string NombreSendero   = "Etapa 3 · Sendero";
    const string NombreNubes     = "Nubes";

    public const string FlagViaje = "SALIDA_DEL_REINO";
    public const string FlagSendero = "ENTRADA_AL_SENDERO";

    // Assets (rutas del proyecto)
    const string WillPath   = "Assets/Prefabs/_WILL.prefab";
    const string LiamPath   = "Assets/Prefabs/_LIAM.prefab";
    const string EstelaPath = "Assets/Prefabs/_ESTELA.prefab";
    const string FK = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    const string BarcoPath = FK + "Props/Ship/Ship01_a01.prefab";
    const string BancoPath = FK + "Props/Furniture/Chair/Chair02_a01.prefab";
    const string FarolaPath = FK + "Props/Lighting/Light01_a01.prefab";
    const string MainWorldPath = "Assets/Scenes/Worlds/MainWorld.unity";
        const string MusicaPeninsula = "Assets/Audio/Music/menu-principal-el-sendero-v2-alegre.mp3";
    const string MusicaViaje     = "Assets/Audio/Music/Brisa del Puerto.mp3";
    const string MusicaSendero   = "Assets/Audio/Music/El Sendero de las Estrellas (Tema Principal).mp3";

    const string CarpetaMateriales = "Assets/Art/MainMenu";
    const string CieloNochePath = CarpetaMateriales + "/CieloNocheMenu.mat";
    const string EstrellasMatPath = CarpetaMateriales + "/EstrellasMenu.mat";
    const string CieloDiaPath = CarpetaMateriales + "/CieloDiaMenu.mat";

    // Península (coordenadas de MainWorld): banco al norte de la plaza, mirando al pueblo, con el
    // árbol rosa (Tree03_c02 (1)) justo delante de Will y las casas detrás.
    static readonly Vector3 PuntoBanco  = new Vector3(19f, 0f, -91.5f);
    static readonly Vector3 PuntoMirado = new Vector3(6f, 0f, -122f);
    static readonly Vector3 ArbolRosa   = new Vector3(16.1f, 0f, -99.4f);

    // El barco vive lejos del pueblo para que no se vean entre sí.
    static readonly Vector3 OrigenViaje     = new Vector3(1000f, 0f, 0f);

    [MenuItem("El Sendero/MainMenu/Portadas: montar las tres etapas %#&0")]
    public static void Montar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var camara = Camera.main;
        var menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (camara == null || menu == null)
        {
            EditorUtility.DisplayDialog("Portadas del menú", "No encuentro la Main Camera o el MainMenuController en MainMenu.unity.", "Vale");
            return;
        }

        var raiz = GameObject.Find(RaizNombre);
        if (raiz == null) raiz = new GameObject(RaizNombre);
        var portada = Componente<PortadaDelMenu>(raiz);

        // Solo se montan las portadas que no existen: lo que ya está (y lo que se haya retocado a
        // mano en ellas) no se toca. Para rehacer una, se borra su GameObject y se vuelve a montar.
        var peninsula = Existente(raiz.transform, NombrePeninsula);
        var viaje = Existente(raiz.transform, NombreViaje);
        var sendero = Existente(raiz.transform, NombreSendero);
        if (peninsula == null || viaje == null)
        {
            using (var mundo = MundoCopiado.Abrir(MainWorldPath))
            {
                if (peninsula == null) peninsula = MontarPeninsula(raiz.transform, mundo);
                if (viaje == null) viaje = MontarViaje(raiz.transform, mundo.MaterialDelMar);
            }
        }
        if (sendero == null) sendero = MontarSendero(raiz.transform, camara);

        // Añadidos que no rehacen nada: nubes en los cielos de día y el título centrado sobre los botones.
        PonerNubes(peninsula);
        PonerNubes(viaje);
        AlinearTituloConBotones();

        var so = new SerializedObject(portada);
        var etapas = so.FindProperty("etapas");
        etapas.arraySize = 3;
        etapas.GetArrayElementAtIndex(0).objectReferenceValue = peninsula;
        etapas.GetArrayElementAtIndex(1).objectReferenceValue = viaje;
        etapas.GetArrayElementAtIndex(2).objectReferenceValue = sendero;
        so.FindProperty("camara").objectReferenceValue = camara;
        so.ApplyModifiedPropertiesWithoutUndo();

        var soMenu = new SerializedObject(menu);
        soMenu.FindProperty("portada").objectReferenceValue = portada;
        soMenu.ApplyModifiedPropertiesWithoutUndo();

        portada.Mostrar(0);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[PortadasDelMenuBuilder] Portadas montadas en MainMenu.unity (Península, Viaje, Sendero).");
    }

    [MenuItem("El Sendero/MainMenu/Portadas: previsualizar 1 Península %#&1")]
    static void Ver1() => Previsualizar(0);
    [MenuItem("El Sendero/MainMenu/Portadas: previsualizar 2 Viaje %#&2")]
    static void Ver2() => Previsualizar(1);
    [MenuItem("El Sendero/MainMenu/Portadas: previsualizar 3 Sendero %#&3")]
    static void Ver3() => Previsualizar(2);
    [MenuItem("El Sendero/MainMenu/Portadas: según la partida (quitar previsualización) %#&4")]
    static void VerSegunPartida() => Previsualizar(-1);

    /// <summary>
    /// Enciende la portada en el Editor y la deja forzada para el próximo Play. -1 = quitar.
    /// </summary>
    static void Previsualizar(int indice)
    {
        var portada = Object.FindAnyObjectByType<PortadaDelMenu>(FindObjectsInactive.Include);
        if (portada == null)
        {
            EditorUtility.DisplayDialog("Portadas del menú", "Abre MainMenu.unity (y monta las portadas si no lo has hecho).", "Vale");
            return;
        }
        var so = new SerializedObject(portada);
        so.FindProperty("forzarEtapaEnEditor").intValue = indice;
        so.ApplyModifiedPropertiesWithoutUndo();
        portada.Mostrar(Mathf.Max(0, indice));
        EditorSceneManager.MarkSceneDirty(portada.gameObject.scene);
        SceneView.RepaintAll();
    }

    // ── 1. Península ─────────────────────────────────────────────────────────────────────────

    static PortadaEtapa MontarPeninsula(Transform raiz, MundoCopiado mundo)
    {
        // La etapa está en el origen: el pueblo copiado conserva sus coordenadas de MainWorld.
        var etapa = Etapa(raiz, NombrePeninsula, Vector3.zero);
        var deco = DecoradoNuevo(etapa.transform);

        var pueblo = mundo.CopiarPueblo(deco);

        // Banco mirando al pueblo, con el árbol rosa delante.
        Vector3 posBanco = new Vector3(PuntoBanco.x, mundo.AlturaDelSuelo(PuntoBanco), PuntoBanco.z);
        Vector3 dir = new Vector3(PuntoMirado.x - posBanco.x, 0f, PuntoMirado.z - posBanco.z).normalized;
        Vector3 izquierda = Vector3.Cross(dir, Vector3.up);
        float yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;

        // Cámara: tres cuartos por detrás de Will, que queda en el tercio derecho (a la izquierda
        // van los botones del menú).
        Vector3 camara = posBanco - dir * 5.5f + izquierda * 3.2f + Vector3.up * 2.6f;
        Vector3 hacia = new Vector3(ArbolRosa.x, posBanco.y + 2f, ArbolRosa.z) + izquierda * 2f;
        var encuadre = Encuadre(etapa.transform, camara, hacia);

        mundo.RecortarPueblo(pueblo, camara, dir, posBanco);

        var banco = Instanciar(BancoPath, deco, posBanco, yaw);
        Instanciar(FarolaPath, deco, posBanco - izquierda * 1.8f + dir * 0.2f, yaw);

        // Will sentado en el banco
        Physics.SyncTransforms();
        float asiento = AlturaDeApoyo(posBanco, posBanco.y + 0.1f, posBanco.y + 0.45f);
        var will = Personaje(WillPath, deco, new Vector3(posBanco.x, asiento, posBanco.z), yaw);
        var actorWill = Actor(will, "SitMedium_Loop", "SitMedium_Exit");

        var sol = Sol(etapa.transform, new Color(1f, 0.93f, 0.8f), 1.1f, new Vector3(40f, yaw - 140f, 0f));

        var so = Config(etapa, "", encuadre, 45f, MusicaPeninsula, null, sol, PortadaEtapa.Ambiente.DelCielo, Color.gray);
        so.FindProperty("cielo").objectReferenceValue = CieloDeDia();
        so.FindProperty("niebla").boolValue = true;
        so.FindProperty("colorNiebla").colorValue = new Color(0.74f, 0.84f, 0.94f);
        so.FindProperty("densidadNiebla").floatValue = 0.006f;
        var actores = so.FindProperty("actoresQueSalen");
        actores.arraySize = actorWill != null ? 1 : 0;
        if (actorWill != null) actores.GetArrayElementAtIndex(0).objectReferenceValue = actorWill;
        so.FindProperty("esperaAlSalir").floatValue = 1.4f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return etapa;
    }

    // ── 2. Viaje ─────────────────────────────────────────────────────────────────────────────

    static PortadaEtapa MontarViaje(Transform raiz, Material materialDelMar)
    {
        var etapa = Etapa(raiz, NombreViaje, OrigenViaje);
        var deco = DecoradoNuevo(etapa.transform);

        var mar = GameObject.CreatePrimitive(PrimitiveType.Plane);
        mar.name = "Mar";
        mar.transform.SetParent(deco, false);
        mar.transform.localScale = new Vector3(80f, 1f, 80f);
        Object.DestroyImmediate(mar.GetComponent<Collider>());
        if (materialDelMar) mar.GetComponent<MeshRenderer>().sharedMaterial = materialDelMar;

        var barco = Instanciar(BarcoPath, deco, Vector3.zero, 0f);
        var personajes = new List<GameObject>();
        if (barco != null)
        {
            // Casco en el agua: la línea de flotación, a media altura del casco.
            var b = Limites(barco);
            barco.transform.position += Vector3.up * (etapa.transform.position.y - (b.min.y + b.size.y * 0.06f));
            b = Limites(barco);

            // Eje largo del barco (proa-popa) en horizontal.
            Vector3 largo = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            Vector3 ancho = Vector3.Cross(Vector3.up, largo);
            float L = Mathf.Max(b.size.x, b.size.z);

            // El prefab no trae colisiones: se ponen unas temporales para encontrar la cubierta.
            var temporales = new List<MeshCollider>();
            foreach (var mf in barco.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    temporales.Add(mc);
                }
            Physics.SyncTransforms();

            // Cubierta: la superficie más alta por debajo de las vergas (lo de más abajo es el
            // fondo del casco por dentro).
            float techo = b.min.y + b.size.y * 0.3f;
            float cubierta = AlturaMasAlta(b.center, techo, b.min.y + b.size.y * 0.2f);
            float separacion = 0.35f;
            Vector3[] sitios =
            {
                b.center + largo * L * 0.2f,                     // Will, hacia proa
                b.center + largo * L * 0.1f + ancho * separacion, // Estela
                b.center + largo * L * 0.02f - ancho * separacion, // Liam
            };
            float[] alturas = new float[sitios.Length];
            for (int i = 0; i < sitios.Length; i++) alturas[i] = AlturaMasAlta(sitios[i], techo, cubierta);
            foreach (var mc in temporales) Object.DestroyImmediate(mc);

            // Will en proa, mirando al frente, señala o saluda de vez en cuando. Estela, sentada en
            // cubierta, y Liam, de pie, charlan mirándose.
            string[] paths = { WillPath, EstelaPath, LiamPath };
            for (int i = 0; i < 3; i++)
            {
                var p = Personaje(paths[i], barco.transform, Vector3.zero, 0f);
                if (p == null) continue;
                p.transform.position = new Vector3(sitios[i].x, alturas[i], sitios[i].z);
                Vector3 mira = i == 0 ? largo : (sitios[i == 1 ? 2 : 1] - sitios[i]);
                mira.y = 0f;
                p.transform.rotation = Quaternion.LookRotation(mira.sqrMagnitude > 0.001f ? mira : largo, Vector3.up);
                if (i == 0) Actor(p, "", "", "FoundSomething_NoWeapon", "HandWave01", "Cheer01");
                else if (i == 1) Actor(p, "SitGround_Loop", "", "Laugh01", "Talk02", "HeadNod01");
                else Actor(p, "", "", "Talk01", "Talk03", "Question01", "Laugh01");
                personajes.Add(p);
            }

            // Vaivén suave del barco (los personajes van dentro y se mueven con él).
            var vaiven = barco.AddComponent<BalloonFloatingMotion>();
            var sov = new SerializedObject(vaiven);
            sov.FindProperty("verticalAmplitude").floatValue = 0.12f;
            sov.FindProperty("verticalSpeed").floatValue = 0.7f;
            sov.FindProperty("horizontalAmplitude").floatValue = 0.05f;
            sov.FindProperty("horizontalSpeed").floatValue = 0.5f;
            sov.FindProperty("rotationAmount").floatValue = 2f;
            sov.FindProperty("rotationSpeed").floatValue = 0.3f;
            sov.ApplyModifiedPropertiesWithoutUndo();

            // Cámara: de costado y algo por delante, con el barco a la derecha de la pantalla.
            Vector3 cubiertaCentro = new Vector3(b.center.x, cubierta, b.center.z);
            // De costado, algo por encima de la borda para ver la cubierta y aún con cielo y
            // horizonte detrás; mira a los tres y deja el barco a la derecha de la pantalla.
            Vector3 grupo = (sitios[0] + sitios[1] + sitios[2]) / 3f;
            grupo.y = cubierta;
            Vector3 desde = grupo + ancho * L * 0.55f + largo * L * 0.2f + Vector3.up * 6.5f;
            Vector3 hacia = grupo + Vector3.up * 0.8f;
            Vector3 lado = Vector3.Cross(Vector3.up, (hacia - desde).normalized);
            hacia -= lado * L * 0.25f;
            Encuadre(etapa.transform, desde - etapa.transform.position, hacia - etapa.transform.position);
        }
        else
        {
            Encuadre(etapa.transform, new Vector3(0f, 6f, -20f), new Vector3(0f, 2f, 0f));
        }

        var sol = Sol(etapa.transform, new Color(1f, 0.9f, 0.75f), 1.15f, new Vector3(28f, 60f, 0f));
        var so = Config(etapa, FlagViaje, etapa.transform.Find("Encuadre"), 45f, MusicaViaje, null, sol, PortadaEtapa.Ambiente.DelCielo, Color.gray);
        so.FindProperty("cielo").objectReferenceValue = CieloDeDia();
        so.FindProperty("niebla").boolValue = true;
        so.FindProperty("colorNiebla").colorValue = new Color(0.74f, 0.84f, 0.94f);
        so.FindProperty("densidadNiebla").floatValue = 0.006f;
        so.FindProperty("actoresQueSalen").arraySize = 0;
        so.FindProperty("esperaAlSalir").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return etapa;
    }

    // ── 3. Sendero ───────────────────────────────────────────────────────────────────────────

    static PortadaEtapa MontarSendero(Transform raiz, Camera camara)
    {
        var etapa = Etapa(raiz, NombreSendero, Vector3.zero);

        // La primera vez, el encuadre es el que ya tenía la cámara del menú.
        var encuadre = etapa.transform.Find("Encuadre");
        if (encuadre == null)
        {
            encuadre = new GameObject("Encuadre").transform;
            encuadre.SetParent(etapa.transform, false);
            encuadre.SetPositionAndRotation(camara.transform.position, camara.transform.rotation);
        }

        // Decorado que ya tenía el menú: nubes, personajes voladores y la luz.
        if (Reubicar("Clouds_Menu", etapa.transform) == null) RehacerNubes(etapa.transform);
        var voladores = Reubicar("FlyingCompanions_Menu", etapa.transform);
        if (voladores != null)
        {
            // De tres cuartos, abriéndose un poco hacia los lados, en vez de totalmente de espaldas.
            foreach (var volador in voladores.GetComponentsInChildren<MainMenuFlyingCompanion>(true))
            {
                string n = volador.name.ToUpperInvariant();
                float giro = n.Contains("WILL") ? 35f : n.Contains("LIAM") ? -35f : 15f;
                var sov = new SerializedObject(volador);
                sov.FindProperty("yawCorrectionDegrees").floatValue = giro;
                sov.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var luz = Reubicar("Directional light", etapa.transform);
        Light sol = luz != null ? luz.GetComponent<Light>() : null;
        if (sol != null)
        {
            sol.color = new Color(0.62f, 0.68f, 1f);
            sol.intensity = 0.6f;
        }

        var deco = DecoradoNuevo(etapa.transform);
        CampoDeEstrellas(deco, encuadre.position);
        SenderoDeLuz(deco, encuadre);

        var so = Config(etapa, FlagSendero, encuadre, 0f, MusicaSendero, null, sol,
            PortadaEtapa.Ambiente.Plano, new Color(0.34f, 0.36f, 0.6f));
        so.FindProperty("cielo").objectReferenceValue = CieloDeNoche();
        so.FindProperty("niebla").boolValue = false;
        so.FindProperty("actoresQueSalen").arraySize = 0;
        so.FindProperty("esperaAlSalir").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return etapa;
    }

    // ── Cielo de noche y estrellas (Sendero) ─────────────────────────────────────────────────

    /// <summary>Skybox procedural de día, como el cielo del juego (MainWorld usa el procedural).</summary>
    static Material CieloDeDia()
    {
        var mat = MaterialEnCarpeta(CieloDiaPath, "Skybox/Procedural");
        if (mat == null) return null;
        mat.SetFloat("_SunDisk", 2f);
        mat.SetFloat("_SunSize", 0.035f);
        mat.SetFloat("_SunSizeConvergence", 6f);
        mat.SetFloat("_AtmosphereThickness", 0.9f);
        mat.SetColor("_SkyTint", new Color(0.42f, 0.58f, 0.85f));
        mat.SetColor("_GroundColor", new Color(0.45f, 0.5f, 0.55f));
        mat.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Skybox procedural oscuro, sin sol, con el horizonte algo violeta.</summary>
    static Material CieloDeNoche()
    {
        var mat = MaterialEnCarpeta(CieloNochePath, "Skybox/Procedural");
        if (mat == null) return null;
        mat.SetFloat("_SunDisk", 0f);
        mat.SetFloat("_SunSize", 0f);
        mat.SetFloat("_AtmosphereThickness", 0.55f);
        mat.SetColor("_SkyTint", new Color(0.32f, 0.22f, 0.62f));
        mat.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.12f));
        mat.SetFloat("_Exposure", 0.32f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// Cúpula de estrellas: partículas quietas en una esfera alrededor de la cámara que aparecen
    /// y se apagan poco a poco (parpadeo). Se simula en mundo y empieza ya poblada.
    /// </summary>
    static void CampoDeEstrellas(Transform padre, Vector3 centro)
    {
        var go = new GameObject("Estrellas");
        go.transform.SetParent(padre, false);
        go.transform.position = centro;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 2.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f), new Color(0.75f, 0.85f, 1f));
        main.maxParticles = 1800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emision = ps.emission;
        emision.rateOverTime = 220f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 260f;
        forma.radiusThickness = 0f;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sharedMaterial = MaterialDeEstrellas();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
    }

    /// <summary>
    /// El «sendero»: una franja estrecha de chispas doradas que parte de debajo de los personajes
    /// y se pierde a lo lejos, delante de la cámara.
    /// </summary>
    static void SenderoDeLuz(Transform padre, Transform encuadre)
    {
        var go = new GameObject("Sendero de luz");
        go.transform.SetParent(padre, false);
        go.transform.position = encuadre.position + encuadre.forward * 70f + encuadre.right * 5f - Vector3.up * 5f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(encuadre.forward, Vector3.up), Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(0.85f, 0.9f, 1f));
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emision = ps.emission;
        emision.rateOverTime = 220f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(7f, 0.4f, 130f);

        // Deriva lenta hacia delante: el camino «avanza» hacia el horizonte.
        var velocidad = ps.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.space = ParticleSystemSimulationSpace.Local;
        velocidad.x = new ParticleSystem.MinMaxCurve(0f);
        velocidad.y = new ParticleSystem.MinMaxCurve(0f);
        velocidad.z = new ParticleSystem.MinMaxCurve(1.5f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sharedMaterial = MaterialDeEstrellas();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
    }

    /// <summary>Material aditivo de partículas URP con la textura de partícula por defecto.</summary>
    static Material MaterialDeEstrellas()
    {
        var mat = MaterialEnCarpeta(EstrellasMatPath, "Universal Render Pipeline/Particles/Unlit");
        if (mat == null) return null;
        mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MaterialEnCarpeta(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[PortadasDelMenuBuilder] No encuentro el shader {shaderName}.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
            AssetDatabase.CreateFolder("Assets/Art", "MainMenu");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    // Nubes del menú original (MainMenuStylingBuilder), por si se han perdido.
    static readonly (string guid, Vector3 pos, Quaternion rot)[] NubesOriginales =
    {
        ("bca0e32ecbad95342a83b9dae7fb5a97", new Vector3(45.652714f, 27.488546f, 24.54898f), Quaternion.identity),
        ("f87a1f7ed72c7ac4aa6a6c3fd94290f4", new Vector3(30.388594f, 28.818062f, 25.662107f), new Quaternion(0.0132015785f, -0.99979f, -0.00087675726f, 0.015649764f)),
        ("4cee5232a4db1a14c9257324c12fdaa7", new Vector3(15.124474f, 30.147577f, 26.775236f), Quaternion.identity),
    };

    static void RehacerNubes(Transform etapa)
    {
        var nubes = new GameObject("Clouds_Menu").transform;
        nubes.SetParent(etapa, false);
        foreach (var (guid, pos, rot) in NubesOriginales)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            var nube = (GameObject)PrefabUtility.InstantiatePrefab(prefab, nubes);
            nube.transform.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// MainWorld abierta en aditivo mientras se montan las portadas: de ella se copian el pueblo
    /// de Will (con su terreno) y el material del mar. Al cerrar, se descarga sin guardar.
    /// </summary>
    sealed class MundoCopiado : System.IDisposable
    {
        Scene _escena;
        bool _yaAbierta;
        Terrain _terreno;
        Transform _pueblo;
        public Material MaterialDelMar { get; private set; }

        public static MundoCopiado Abrir(string path)
        {
            var m = new MundoCopiado();
            var existente = SceneManager.GetSceneByPath(path);
            m._yaAbierta = existente.IsValid() && existente.isLoaded;
            m._escena = m._yaAbierta ? existente : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var root in m._escena.GetRootGameObjects())
            {
                if (m._terreno == null) m._terreno = root.GetComponentInChildren<Terrain>(true);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (m._pueblo == null && t.name.StartsWith("Pueblo inicial")) m._pueblo = t;
                    if (m.MaterialDelMar == null && t.name == "Mar")
                    {
                        var r = t.GetComponent<MeshRenderer>();
                        if (r != null) m.MaterialDelMar = r.sharedMaterial;
                    }
                }
            }
            if (m._terreno == null) Debug.LogWarning("[PortadasDelMenuBuilder] MainWorld sin Terrain: la Península saldrá sin suelo.");
            if (m._pueblo == null) Debug.LogWarning("[PortadasDelMenuBuilder] No encuentro «Pueblo inicial…» en MainWorld.");
            if (m.MaterialDelMar == null) Debug.LogWarning("[PortadasDelMenuBuilder] No encuentro el «Mar» de MainWorld.");
            return m;
        }

        public float AlturaDelSuelo(Vector3 p) =>
            _terreno != null ? _terreno.SampleHeight(p) + _terreno.transform.position.y : 0f;

        /// <summary>
        /// Copia el terreno y el pueblo de Will con sus coordenadas de MainWorld (sin su luz
        /// direccional: la portada trae su sol). Devuelve la raíz del pueblo copiado.
        /// </summary>
        public Transform CopiarPueblo(Transform padre)
        {
            if (_terreno != null)
            {
                var go = new GameObject("Terreno (copia de MainWorld)");
                go.transform.SetParent(padre, false);
                go.transform.position = _terreno.transform.position;
                go.layer = _terreno.gameObject.layer;
                var t = go.AddComponent<Terrain>();
                EditorUtility.CopySerialized(_terreno, t);
                var c = go.AddComponent<TerrainCollider>();
                c.terrainData = _terreno.terrainData;
            }

            if (_pueblo == null) return null;
            var copia = Object.Instantiate(_pueblo.gameObject, padre);
            copia.name = "Pueblo de Will (copia de MainWorld)";
            copia.transform.SetPositionAndRotation(_pueblo.position, _pueblo.rotation);
            copia.transform.localScale = _pueblo.lossyScale;

            var quitar = new List<GameObject>();
            foreach (Transform hijo in copia.transform)
            {
                var luz = hijo.GetComponent<Light>();
                if (luz != null && luz.type == LightType.Directional) quitar.Add(hijo.gameObject);
            }
            foreach (var go in quitar) Object.DestroyImmediate(go);
            return copia.transform;
        }

        /// <summary>
        /// Deja solo las piezas del pueblo que la cámara puede ver (delante y a menos de 150 m) y
        /// quita lo que se pondría en medio del plano: lo pegado a la cámara, lo que tapa a Will y
        /// lo que ocupa el sitio del banco (salvo casas).
        /// </summary>
        public void RecortarPueblo(Transform pueblo, Vector3 camara, Vector3 dir, Vector3 banco)
        {
            if (pueblo == null) return;
            Vector3 cabeza = banco + Vector3.up * 1.2f;
            var quitar = new List<GameObject>();
            foreach (Transform hijo in pueblo)
            {
                Vector3 v = hijo.position - camara;
                if (Vector3.Dot(v, dir) < -8f || v.magnitude > 150f) { quitar.Add(hijo.gameObject); continue; }
                if (hijo.name.StartsWith("Building")) continue;

                var rs = hijo.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var caja = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) caja.Encapsulate(rs[i].bounds);

                bool pegadoACamara = caja.SqrDistance(camara) < 3f * 3f;
                bool enElBanco = caja.SqrDistance(banco + Vector3.up * 0.5f) < 1.5f * 1.5f;
                bool tapaAWill = false;
                for (float t = 0.15f; t < 0.95f && !tapaAWill; t += 0.1f)
                    tapaAWill = caja.SqrDistance(Vector3.Lerp(camara, cabeza, t)) < 0.8f * 0.8f;
                if (pegadoACamara || enElBanco || tapaAWill) quitar.Add(hijo.gameObject);
            }
            foreach (var go in quitar) Object.DestroyImmediate(go);
        }

        public void Dispose()
        {
            if (!_yaAbierta && _escena.IsValid()) EditorSceneManager.CloseScene(_escena, true);
        }
    }

    static PortadaEtapa Existente(Transform raiz, string nombre)
    {
        var t = raiz.Find(nombre);
        return t != null ? t.GetComponent<PortadaEtapa>() : null;
    }

    /// <summary>
    /// Nubes del pack del menú (las mismas del Sendero) repartidas por el cielo delante del encuadre
    /// de la portada, altas y lejos. Solo si la portada aún no tiene las suyas.
    /// </summary>
    static void PonerNubes(PortadaEtapa etapa)
    {
        if (etapa == null || etapa.transform.Find(NombreNubes) != null || etapa.Encuadre == null) return;
        var nubes = new GameObject(NombreNubes).transform;
        nubes.SetParent(etapa.transform, false);

        var encuadre = etapa.Encuadre;
        Vector3 frente = Vector3.ProjectOnPlane(encuadre.forward, Vector3.up).normalized;
        Vector3 derecha = Vector3.Cross(Vector3.up, frente);
        // (distancia, lateral, altura, escala) — a mano, para que queden repartidas y sin taparse.
        var sitios = new (float d, float x, float h, float esc)[]
        {
            (150f, -70f, 38f, 3.2f), (130f, 5f, 48f, 2.6f), (170f, 70f, 34f, 3.6f), (110f, 45f, 55f, 2.2f), (190f, -20f, 30f, 4f),
        };
        for (int i = 0; i < sitios.Length; i++)
        {
            var (guid, _, rot) = NubesOriginales[i % NubesOriginales.Length];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            var nube = (GameObject)PrefabUtility.InstantiatePrefab(prefab, nubes);
            var (d, x, h, esc) = sitios[i];
            nube.transform.position = encuadre.position + frente * d + derecha * x + Vector3.up * h;
            nube.transform.rotation = Quaternion.LookRotation(-frente, Vector3.up) * rot;
            nube.transform.localScale = Vector3.one * esc;
        }
    }

    /// <summary>
    /// Centra el logo (y la etiqueta de fase, que cuelga de él) sobre la columna de botones del menú
    /// (mismo centro horizontal que las filas) y sube el bloque si los botones no caben por abajo.
    /// </summary>
    static void AlinearTituloConBotones()
    {
        // El lienzo del menú es el que contiene el logo (en la escena hay varios Canvas).
        Canvas canvas = null;
        RectTransform logo = null, panel = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            logo = BuscarHijo(c.transform, "LogoTitulo") as RectTransform;
            panel = BuscarHijo(c.transform, "ButtonPanel") as RectTransform;
            if (logo != null && panel != null) { canvas = c.rootCanvas; break; }
        }
        if (canvas == null) return;

        Canvas.ForceUpdateCanvases();
        float min = float.MaxValue, max = float.MinValue;
        var esquinas = new Vector3[4];
        foreach (var boton in panel.GetComponentsInChildren<UnityEngine.UI.Button>(false))
        {
            var rt = (RectTransform)boton.transform;
            rt.GetWorldCorners(esquinas);
            foreach (var e in esquinas)
            {
                float lx = ((RectTransform)logo.parent).InverseTransformPoint(e).x;
                min = Mathf.Min(min, lx); max = Mathf.Max(max, lx);
            }
        }
        if (min == float.MaxValue) return;
        float centroBotones = (min + max) * 0.5f;

        logo.GetWorldCorners(esquinas);
        float izq = ((RectTransform)logo.parent).InverseTransformPoint(esquinas[0]).x;
        float der = ((RectTransform)logo.parent).InverseTransformPoint(esquinas[2]).x;
        float desfase = centroBotones - (izq + der) * 0.5f;
        if (Mathf.Abs(desfase) >= 0.5f)
        {
            logo.anchoredPosition += new Vector2(desfase, 0f);
            Debug.Log($"[PortadasDelMenuBuilder] Logo movido {desfase:F0} px para centrarlo sobre los botones.");
        }

        SubirSiNoCabe(canvas, panel, logo);
    }

    const float MargenInferior = 50f;

    /// <summary>
    /// Si la columna de botones (con todas sus filas encendidas, Continuar incluido) se sale por
    /// abajo de la pantalla, sube el bloque entero (logo, etiqueta de fase y botones) lo que falte
    /// para dejar <see cref="MargenInferior"/> px de margen.
    /// </summary>
    static void SubirSiNoCabe(Canvas canvas, RectTransform panel, RectTransform logo)
    {
        var columna = panel.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>(true);
        var lienzo = canvas != null ? canvas.transform as RectTransform : null;
        if (columna == null || lienzo == null) return;

        var rt = (RectTransform)columna.transform;
        float alto = columna.padding.top + columna.padding.bottom;
        int filas = 0;
        foreach (RectTransform fila in rt)
        {
            if (!fila.gameObject.activeSelf) continue;
            alto += fila.rect.height;
            filas++;
        }
        if (filas == 0) return;
        alto += columna.spacing * (filas - 1);

        var esquinas = new Vector3[4];
        rt.GetWorldCorners(esquinas);
        float arriba = lienzo.InverseTransformPoint(esquinas[1]).y;
        float falta = lienzo.rect.yMin + MargenInferior - (arriba - alto);
        if (falta <= 0.5f) return;

        panel.anchoredPosition += new Vector2(0f, falta);
        logo.anchoredPosition += new Vector2(0f, falta);
        Debug.Log($"[PortadasDelMenuBuilder] Botones y logo subidos {falta:F0} px: las {filas} filas no cabían en pantalla.");
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        if (raiz == null) return null;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    // ── Utilidades ───────────────────────────────────────────────────────────────────────────

    static PortadaEtapa Etapa(Transform raiz, string nombre, Vector3 origen)
    {
        var t = raiz.Find(nombre);
        if (t == null)
        {
            t = new GameObject(nombre).transform;
            t.SetParent(raiz, false);
        }
        t.position = origen;
        // Encendida mientras se monta: las colisiones de lo apagado no cuentan para los raycasts.
        t.gameObject.SetActive(true);
        return Componente<PortadaEtapa>(t.gameObject);
    }

    static Transform DecoradoNuevo(Transform etapa)
    {
        var viejo = etapa.Find(DecoradoNombre);
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        var t = new GameObject(DecoradoNombre).transform;
        t.SetParent(etapa, false);
        return t;
    }

    static GameObject Instanciar(string path, Transform padre, Vector3 local, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[PortadasDelMenuBuilder] No encuentro {path}; se omite.");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    static GameObject Personaje(string path, Transform padre, Vector3 local, float yaw)
    {
        var go = Instanciar(path, padre, local, yaw);
        if (go != null) go.name = go.name.TrimStart('_');
        return go;
    }

    static ActorDePortada Actor(GameObject personaje, string bucle, string salida, params string[] gestos)
    {
        if (personaje == null) return null;
        var actor = Componente<ActorDePortada>(personaje);
        var so = new SerializedObject(actor);
        so.FindProperty("estadoEnBucle").stringValue = bucle;
        so.FindProperty("estadoAlSalir").stringValue = salida;
        var lista = so.FindProperty("gestos");
        lista.arraySize = gestos.Length;
        for (int i = 0; i < gestos.Length; i++) lista.GetArrayElementAtIndex(i).stringValue = gestos[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return actor;
    }

    static Transform Encuadre(Transform etapa, Vector3 localDesde, Vector3 localHacia)
    {
        var t = etapa.Find("Encuadre");
        if (t == null)
        {
            t = new GameObject("Encuadre").transform;
            t.SetParent(etapa, false);
        }
        t.localPosition = localDesde;
        t.rotation = Quaternion.LookRotation(localHacia - localDesde, Vector3.up);
        return t;
    }

    static Light Sol(Transform etapa, Color color, float intensidad, Vector3 euler)
    {
        var t = etapa.Find("Sol");
        if (t == null)
        {
            t = new GameObject("Sol").transform;
            t.SetParent(etapa, false);
        }
        var luz = Componente<Light>(t.gameObject);
        luz.type = LightType.Directional;
        luz.color = color;
        luz.intensity = intensidad;
        luz.shadows = LightShadows.Soft;
        t.rotation = Quaternion.Euler(euler);
        return luz;
    }

    static SerializedObject Config(PortadaEtapa etapa, string flag, Transform encuadre, float fov, string musicaPath,
        string cieloGuid, Light sol, PortadaEtapa.Ambiente ambiente, Color colorAmbiente)
    {
        var so = new SerializedObject(etapa);
        so.FindProperty("flagQueLaDesbloquea").stringValue = flag;
        so.FindProperty("encuadre").objectReferenceValue = encuadre;
        so.FindProperty("campoDeVision").floatValue = fov;
        so.FindProperty("musica").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(musicaPath);
        so.FindProperty("cielo").objectReferenceValue = string.IsNullOrEmpty(cieloGuid)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(cieloGuid));
        so.FindProperty("sol").objectReferenceValue = sol;
        so.FindProperty("ambiente").enumValueIndex = (int)ambiente;
        so.FindProperty("colorAmbiente").colorValue = colorAmbiente;
        return so;
    }

    static Transform Reubicar(string nombre, Transform nuevoPadre)
    {
        var existente = nuevoPadre.Find(nombre);
        if (existente != null) return existente;
        var go = GameObject.Find(nombre);
        if (go == null) return null;
        go.transform.SetParent(nuevoPadre, true);
        return go.transform;
    }

    // GetComponent devuelve en el Editor un objeto «nulo falso» que ?? no detecta.
    static T Componente<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Bounds Limites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>
    /// Altura de la superficie más baja (asiento, cubierta) que hay bajo el punto por encima de
    /// <paramref name="minY"/>: la más baja para no quedarse en velas, jarcias ni respaldos. Se
    /// ignoran los personajes (llevan NPCSimpleAnimator). Sin colisión, devuelve
    /// <paramref name="alternativa"/>.
    /// </summary>
    static float AlturaDeApoyo(Vector3 punto, float minY, float alternativa)
    {
        var desde = new Vector3(punto.x, punto.y + 100f, punto.z);
        float mejor = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(desde, Vector3.down, 300f))
        {
            if (hit.point.y <= minY) continue;
            if (hit.collider.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            if (hit.point.y < mejor) mejor = hit.point.y;
        }
        return mejor < float.MaxValue ? mejor : alternativa;
    }

    /// <summary>
    /// Altura de la superficie más alta bajo el punto que quede por debajo de
    /// <paramref name="maxY"/> (la cubierta, por debajo de vergas y velas). Se ignoran los
    /// personajes. Sin colisión, devuelve <paramref name="alternativa"/>.
    /// </summary>
    static float AlturaMasAlta(Vector3 punto, float maxY, float alternativa)
    {
        var desde = new Vector3(punto.x, punto.y + 100f, punto.z);
        float mejor = float.MinValue;
        foreach (var hit in Physics.RaycastAll(desde, Vector3.down, 300f))
        {
            if (hit.point.y >= maxY) continue;
            if (hit.collider.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            if (hit.point.y > mejor) mejor = hit.point.y;
        }
        return mejor > float.MinValue ? mejor : alternativa;
    }
}
