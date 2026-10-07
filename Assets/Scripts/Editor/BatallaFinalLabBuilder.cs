using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using M = MomentoDeCombate;

/// Genera una escena de prueba de la batalla final contra el Mago Oscuro: el final del camino de
/// luz, con el suelo de luz, bordes invisibles y solo el altar de piedra blanca. Lo demás (espinas,
/// pilares con anclas, plataformas y lanzadores) está oculto hasta que el Mago lo conjura. Trae el
/// grupo completo (Will, Estela y Liam) y rellena el guion de comentarios de Estela. Usa el
/// jugador, los compañeros y el Mago reales; no toca el Sendero ni los prefabs. Regenerarla borra
/// los cambios hechos a mano dentro de la escena. Ver INC-509, INC-663.
public static class BatallaFinalLabBuilder
{
    const string ScenePath = "Assets/Scenes/Test/BatallaFinal.unity";
    const string Materiales = "Assets/Scenes/Test/BatallaFinalMaterials";
    const string GuionPath = "Assets/BOSSBATTLES/Guiones/Guion_BatallaFinal_Estela.asset";

    const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    const string PrefabMago = "Assets/Prefabs/_MAGO_OSCURO.prefab";
    const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    const string PrefabGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    const string PrefabLanzador = "Assets/Prefabs/Exploracion/LanzadorDeSalto.prefab";
    const string HechizoGolpe = "Assets/_SPELLS/MagoOscuroGolpe.asset";

    // Efectos (los que falten se avisan y se dejan vacíos)
    const string VfxAparicion = "Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Aparicion.prefab";
    const string VfxDerrota = "Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Derrota.prefab";
    const string VfxRebobinado = "Assets/_VFX/BatallaFinal/VFX_Rebobinado.prefab";
    const string VfxSacrificio = "Assets/_VFX/BatallaFinal/VFX_SacrificioLiam.prefab";
    const string VfxZona = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect2.prefab";
    const string VfxMarea = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect3.prefab";
    const string VfxRegeneracion = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect1.prefab";
    const string VfxCarga = "Assets/VFX/Free Game VFX/Prefab/FX_Purple_Hit_02.prefab";
    const string VfxSalto = "Assets/VFX/Free Game VFX/Prefab/FX_Greenlight_shrink.prefab";
    const string VfxCorte = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_02.prefab";
    const string VfxRoturaCristal = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab";
    const string VfxPozo = "Assets/_VFX/Prologo/VFX_AgujeroNegro.prefab";
    const string VfxImplosionPozo = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect4.prefab";

    [MenuItem("El Sendero/Combate/Crear o regenerar la Batalla Final (prueba)")]
    public static void Crear()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Batalla Final",
                "Se reemplazará BatallaFinal.unity (los cambios hechos a mano dentro se pierden). El Sendero y los prefabs no se tocan.",
                "Regenerar", "Cancelar"))
            return;

        var avisos = new List<string>();
        var will = Cargar<GameObject>(PrefabWill, avisos);
        var magoPrefab = Cargar<GameObject>(PrefabMago, avisos);
        if (will == null || magoPrefab == null)
        {
            EditorUtility.DisplayDialog("Batalla Final", "Faltan _WILL o _MAGO_OSCURO. No se ha creado nada.", "Aceptar");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Materiales)) AssetDatabase.CreateFolder("Assets/Scenes/Test", "BatallaFinalMaterials");
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Ambiente: el final del camino, entre estrellas ────────────────
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 0.9f;
        luz.color = new Color(0.85f, 0.85f, 1f);
        luz.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.24f, 0.36f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.06f, 0.05f, 0.14f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;

        var matSuelo = Mat("BF_SueloDeLuz", new Color(0.72f, 0.78f, 0.95f), emision: new Color(0.2f, 0.25f, 0.4f));
        var matAltar = Mat("BF_AltarBlanco", new Color(0.93f, 0.91f, 0.86f));
        var matConjuro = Mat("BF_PiedraConjurada", new Color(0.16f, 0.12f, 0.2f));
        var matCristal = Mat("BF_Ancla", new Color(0.55f, 0.2f, 0.9f), emision: new Color(0.6f, 0.15f, 1f));
        var rayoSombra = MatRayo("BF_RayoSombra", new Color(0.45f, 0.05f, 0.7f));
        var rayoEnergia = MatRayo("BF_RayoEnergia", new Color(1f, 0.55f, 0.75f));
        var rayoAguja = MatRayo("BF_RayoAguja", new Color(1f, 0.97f, 0.8f));

        // ── Geometría fija: suelo de luz, bordes invisibles y el altar ────
        var arena = new GameObject("BF_ARENA");
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "Suelo de luz";
        plaza.transform.SetParent(arena.transform);
        plaza.transform.position = new Vector3(0f, -0.25f, 0f);
        plaza.transform.localScale = new Vector3(42f, 0.25f, 42f);
        Object.DestroyImmediate(plaza.GetComponent<Collider>());
        plaza.AddComponent<MeshCollider>();
        plaza.GetComponent<Renderer>().sharedMaterial = matSuelo;
        plaza.isStatic = true;
        Capa(plaza, "Floor");

        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f;
            var borde = new GameObject($"Borde invisible {i}");
            borde.transform.SetParent(arena.transform);
            borde.transform.SetPositionAndRotation(Polar(21f, a) + Vector3.up * 2f, Quaternion.Euler(0f, a + 90f, 0f));
            borde.transform.localScale = new Vector3(5.6f, 4f, 0.8f);
            borde.AddComponent<BoxCollider>();
            borde.isStatic = true;
        }

        var altar = Cubo("Altar de piedra blanca", arena.transform, new Vector3(0f, 0.6f, 16f), new Vector3(4f, 1.2f, 3f), matAltar);
        Cubo("Altar · losa", arena.transform, new Vector3(0f, 1.4f, 16f), new Vector3(2.4f, 0.4f, 1.8f), matAltar);
        var puntoAltar = Punto("Sitio del Mago en el altar", arena.transform, new Vector3(0f, 0f, 12.5f));

        var nav = arena.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        nav.BuildNavMesh();

        // ── Escenario del Mago ────────────────────────────────────────────
        var raiz = new GameObject("BF_ESCENARIO");
        var escenario = raiz.AddComponent<EscenarioBatallaFinal>();
        escenario.centro = Punto("Centro", raiz.transform, Vector3.zero);
        escenario.radio = 20f;
        escenario.altar = altar.transform;
        escenario.puntoDelAltar = puntoAltar;
        escenario.puntosDeSalto = Anillo("Salto", raiz.transform, 9.5f, 8, 0f);
        escenario.cuadrantes = Anillo("Cuadrante", raiz.transform, 10f, 4, 45f);
        escenario.registro = raiz.AddComponent<RegistroTemporal>();

        // ── Lo que conjura (oculto hasta entonces) ────────────────────────
        var conjuros = new GameObject("BF_CONJUROS").transform;
        conjuros.SetParent(raiz.transform);
        var ctx = new Contexto
        {
            padre = conjuros,
            matPiedra = matConjuro,
            matCristal = matCristal,
            rayo = rayoSombra,
            vfxConjuro = Cargar<GameObject>(VfxAparicion, avisos),
            vfxRotura = Cargar<GameObject>(VfxRoturaCristal, avisos),
            lanzador = Cargar<GameObject>(PrefabLanzador, avisos),
        };

        // Fase 1: tres espinas bajas.
        escenario.espinas = Juego(ctx, "Espinas",
            pilares: new[] { 60f, 180f, 300f }, radioPilares: 9f, altoPilar: 2.6f, anchoPilar: 1.1f, vidaCristal: 40f,
            plataformas: new (float, float)[0], lanzadores: new float[0]);

        // Fase 2: dos juegos de pilares con anclas, plataformas y lanzadores, en sitios alternos.
        escenario.fase2 = new[]
        {
            Juego(ctx, "Fase 2 · A",
                pilares: new[] { 45f, 135f, 225f, 315f }, radioPilares: 13f, altoPilar: 7f, anchoPilar: 2f, vidaCristal: 60f,
                plataformas: new[] { (90f, 3f), (180f, 4f), (270f, 5f) }, lanzadores: new[] { 70f, 250f }),
            Juego(ctx, "Fase 2 · B",
                pilares: new[] { 22.5f, 112.5f, 202.5f, 292.5f }, radioPilares: 13f, altoPilar: 7f, anchoPilar: 2f, vidaCristal: 60f,
                plataformas: new[] { (67.5f, 4f), (157.5f, 3f), (247.5f, 5f) }, lanzadores: new[] { 135f, 315f }),
        };

        // El final (traición, Liam, Estela, aguja)
        var finalGo = new GameObject("Final del conducto");
        finalGo.transform.SetParent(raiz.transform);
        var final = finalGo.AddComponent<FinalDelConducto>();
        var soFinal = new SerializedObject(final);
        soFinal.FindProperty("rayoTraicion").objectReferenceValue = Rayo("Lanza de la traición", finalGo.transform, rayoSombra, 0.4f, false);
        soFinal.FindProperty("rayoEnergia").objectReferenceValue = Rayo("Energía de Estela", finalGo.transform, rayoEnergia, 0.2f, false);
        soFinal.FindProperty("rayoAguja").objectReferenceValue = Rayo("Aguja de luz", finalGo.transform, rayoAguja, 0.06f, false);
        soFinal.FindProperty("vfxSalto").objectReferenceValue = Cargar<GameObject>(VfxSalto, avisos);
        soFinal.FindProperty("vfxImpactoLanza").objectReferenceValue = Cargar<GameObject>(VfxSacrificio, avisos);
        soFinal.FindProperty("vfxCorte").objectReferenceValue = Cargar<GameObject>(VfxCorte, avisos);
        soFinal.FindProperty("vfxDeshacerse").objectReferenceValue = Cargar<GameObject>(VfxDerrota, avisos);
        soFinal.ApplyModifiedPropertiesWithoutUndo();

        // ── El Mago Oscuro ────────────────────────────────────────────────
        var mago = (GameObject)PrefabUtility.InstantiatePrefab(magoPrefab, escena);
        mago.name = "MAGO_OSCURO (jefe)";
        mago.transform.SetPositionAndRotation(new Vector3(0f, 0f, 8f), Quaternion.Euler(0f, 180f, 0f));
        Capa(mago, "Enemy", soloRaiz: true);
        var manager = mago.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager) manager.enabled = false;   // su IA de NPC no pinta nada aquí: manda MagoOscuroBossAI
        var charla = mago.GetComponent<Interactable>();
        if (charla) charla.enabled = false;     // no se habla con él en mitad del combate
        // Cuerpo sólido para los golpes; el disparador de charla (si lo hay) no cuenta.
        bool tieneCuerpo = false;
        foreach (var c in mago.GetComponents<Collider>())
        {
            if (c.isTrigger) c.enabled = false;
            else tieneCuerpo = true;
        }
        if (!tieneCuerpo)
        {
            var cap = mago.AddComponent<CapsuleCollider>();
            cap.center = Vector3.up;
            cap.height = 2f;
            cap.radius = 0.5f;
        }
        var vidaMago = mago.GetComponent<Damageable>();
        if (vidaMago == null) vidaMago = mago.AddComponent<Damageable>();
        var soVida = new SerializedObject(vidaMago);
        soVida.FindProperty("maxHealth").floatValue = 800f;
        soVida.FindProperty("destroyOnDeath").boolValue = false;
        soVida.ApplyModifiedPropertiesWithoutUndo();
        var transicion = mago.AddComponent<TransicionDeFaseDeJefe>();
        var golem = Cargar<GameObject>("Assets/Prefabs/Enemy/PBR_Golem.prefab", avisos);
        var golemIA = golem != null ? golem.GetComponentInChildren<GolemBossAI>(true) : null;
        if (golemIA != null)
        {
            var soT = new SerializedObject(transicion);
            soT.FindProperty("vfxOnda").objectReferenceValue = new SerializedObject(golemIA).FindProperty("shockwaveVFX").objectReferenceValue;
            soT.ApplyModifiedPropertiesWithoutUndo();
        }
        var barra = mago.AddComponent<BossHealthBar>();
        var soBarra = new SerializedObject(barra);
        soBarra.FindProperty("bossNameId").stringValue = "BOSS_MAGOOSCURO_NAME";
        soBarra.FindProperty("bossName").stringValue = "Mago Oscuro";
        soBarra.ApplyModifiedPropertiesWithoutUndo();

        var ia = mago.AddComponent<MagoOscuroBossAI>();
        var soIA = new SerializedObject(ia);
        soIA.FindProperty("escenario").objectReferenceValue = escenario;
        soIA.FindProperty("final").objectReferenceValue = final;
        soIA.FindProperty("golpe").objectReferenceValue = Cargar<MagicSpellSO>(HechizoGolpe, avisos);
        soIA.FindProperty("guion").objectReferenceValue = RellenarGuion(avisos);
        soIA.FindProperty("vfxAviso").objectReferenceValue = SombraDeAviso(avisos);
        soIA.FindProperty("vfxZona").objectReferenceValue = Cargar<GameObject>(VfxZona, avisos);
        soIA.FindProperty("vfxTeletransporte").objectReferenceValue = Cargar<GameObject>(VfxAparicion, avisos);
        soIA.FindProperty("vfxCarga").objectReferenceValue = Cargar<GameObject>(VfxCarga, avisos);
        soIA.FindProperty("vfxMarea").objectReferenceValue = Cargar<GameObject>(VfxMarea, avisos);
        soIA.FindProperty("vfxRebobinado").objectReferenceValue = Cargar<GameObject>(VfxRebobinado, avisos);
        soIA.FindProperty("vfxRegeneracion").objectReferenceValue = Cargar<GameObject>(VfxRegeneracion, avisos);
        soIA.FindProperty("vfxPozo").objectReferenceValue = Cargar<GameObject>(VfxPozo, avisos);
        soIA.FindProperty("vfxImplosionPozo").objectReferenceValue = Cargar<GameObject>(VfxImplosionPozo, avisos);
        soIA.FindProperty("esperaInicial").floatValue = 4f;
        soIA.ApplyModifiedPropertiesWithoutUndo();

        // ── Jugador y grupo ───────────────────────────────────────────────
        var jugador = (GameObject)PrefabUtility.InstantiatePrefab(will, escena);
        jugador.name = "BF_JUGADOR_WILL";
        jugador.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -12f), Quaternion.identity);
        var camara = Cargar<GameObject>(PrefabCamara, avisos);
        if (camara) ((GameObject)PrefabUtility.InstantiatePrefab(camara, escena)).transform.position = new Vector3(0f, 3f, -16f);
        var grupo = Cargar<GameObject>(PrefabGrupo, avisos);
        if (grupo) PrefabUtility.InstantiatePrefab(grupo, escena);

        var inicio = new GameObject("BF_INICIALIZACION").AddComponent<CombatLabBootstrap>();
        var soInicio = new SerializedObject(inicio);
        soInicio.FindProperty("player").objectReferenceValue = jugador;
        soInicio.FindProperty("grupoInicial").intValue = 3;   // Estela y Liam: la batalla final es de los tres
        soInicio.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test")) AssetDatabase.CreateFolder("Assets/Scenes", "Test");
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, ScenePath);
        AnadirABuild(ScenePath);
        AssetDatabase.SaveAssets();

        string resumen = "Batalla Final creada en " + ScenePath + ". Pulsa Play: el Mago empieza a los 4 s.";
        if (avisos.Count > 0) resumen += "\nAvisos:\n- " + string.Join("\n- ", avisos);
        Debug.Log("[BatallaFinalLabBuilder] " + resumen);
    }

    // ── Conjuros ──────────────────────────────────────────────────────────

    sealed class Contexto
    {
        public Transform padre;
        public Material matPiedra, matCristal, rayo;
        public GameObject vfxConjuro, vfxRotura, lanzador;
    }

    /// Un juego de conjuros: pilares con un cristal arriba, plataformas flotantes (ángulo, altura) y
    /// lanzadores de salto en el suelo mirando hacia fuera. Todo queda oculto.
    static JuegoDeConjuros Juego(Contexto ctx, string nombre, float[] pilares, float radioPilares, float altoPilar,
                                 float anchoPilar, float vidaCristal, (float angulo, float alto)[] plataformas, float[] lanzadores)
    {
        var raiz = new GameObject(nombre).transform;
        raiz.SetParent(ctx.padre);
        var objetos = new List<ObjetoConjurado>();
        var cristales = new List<CristalProtector>();
        var vuelo = new List<Transform>();

        foreach (float a in pilares)
        {
            var pilar = Conjuro(ctx, $"Pilar {a}", raiz, Polar(radioPilares, a));
            Pieza("Cuerpo", pilar.transform, Vector3.up * altoPilar * 0.5f, new Vector3(anchoPilar, altoPilar, anchoPilar), ctx.matPiedra);
            var obstaculo = pilar.gameObject.AddComponent<NavMeshObstacle>();
            obstaculo.shape = NavMeshObstacleShape.Box;
            obstaculo.center = Vector3.up * altoPilar * 0.5f;
            obstaculo.size = new Vector3(anchoPilar, altoPilar, anchoPilar);
            obstaculo.carving = true;
            float tamCristal = Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(2f, 7f, altoPilar));
            cristales.Add(Cristal(ctx, pilar.transform, Vector3.up * (altoPilar + tamCristal * 0.5f), tamCristal, vidaCristal));
            objetos.Add(pilar);
            var p = Polar(radioPilares * 0.85f, a);
            vuelo.Add(Punto("Vuelo", raiz, new Vector3(p.x, altoPilar + 1.5f, p.z)));
        }

        foreach (var (a, alto) in plataformas)
        {
            var plataforma = Conjuro(ctx, $"Plataforma {a}", raiz, Polar(10f, a) + Vector3.up * alto);
            Pieza("Losa", plataforma.transform, Vector3.zero, new Vector3(5f, 0.5f, 5f), ctx.matPiedra);
            objetos.Add(plataforma);
        }

        foreach (float a in lanzadores)
        {
            var runa = Conjuro(ctx, $"Lanzador {a}", raiz, Polar(6f, a));
            runa.transform.rotation = Quaternion.LookRotation(Polar(1f, a));
            if (ctx.lanzador != null)
            {
                var l = (GameObject)PrefabUtility.InstantiatePrefab(ctx.lanzador, runa.transform);
                l.transform.localPosition = Vector3.zero;
                l.transform.localRotation = Quaternion.identity;
            }
            objetos.Add(runa);
        }

        foreach (var o in objetos) o.gameObject.SetActive(false);
        return new JuegoDeConjuros { objetos = objetos.ToArray(), cristales = cristales.ToArray(), puntosDeVuelo = vuelo.ToArray() };
    }

    static ObjetoConjurado Conjuro(Contexto ctx, string nombre, Transform padre, Vector3 pos)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre);
        go.transform.position = pos;
        var oc = go.AddComponent<ObjetoConjurado>();
        var so = new SerializedObject(oc);
        so.FindProperty("vfxAparecer").objectReferenceValue = ctx.vfxConjuro;
        so.FindProperty("vfxDeshacer").objectReferenceValue = ctx.vfxConjuro;
        so.ApplyModifiedPropertiesWithoutUndo();
        return oc;
    }

    /// Geometría de un conjuro: no estática, porque crece y se encoge.
    static GameObject Pieza(string nombre, Transform padre, Vector3 local, Vector3 escala, Material mat)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        c.transform.SetParent(padre, false);
        c.transform.localPosition = local;
        c.transform.localScale = escala;
        c.GetComponent<Renderer>().sharedMaterial = mat;
        return c;
    }

    static CristalProtector Cristal(Contexto ctx, Transform padre, Vector3 local, float tam, float vida)
    {
        var go = new GameObject("Cristal");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        Capa(go, "Enemy");
        var col = go.AddComponent<SphereCollider>();
        col.radius = tam * 0.7f;
        go.AddComponent<Damageable>();
        go.AddComponent<DebilidadDePersonaje>();
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * tam;
        visual.GetComponent<Renderer>().sharedMaterial = ctx.matCristal;
        var cristal = go.AddComponent<CristalProtector>();
        var so = new SerializedObject(cristal);
        so.FindProperty("vida").floatValue = vida;
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("rayo").objectReferenceValue = Rayo("Rayo", go.transform, ctx.rayo, 0.08f, false);
        so.FindProperty("vfxRotura").objectReferenceValue = ctx.vfxRotura;
        so.ApplyModifiedPropertiesWithoutUndo();
        return cristal;
    }

    // ── Guion de Estela ───────────────────────────────────────────────────

    /// Rellena (o crea) el guion: Estela reacciona a lo que pasa y no enseña controles; como mucho
    /// señala lo propio de este jefe con sus palabras.
    static GuionDeCombate RellenarGuion(List<string> avisos)
    {
        var g = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(GuionPath);
        if (g == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/BOSSBATTLES/Guiones")) AssetDatabase.CreateFolder("Assets/BOSSBATTLES", "Guiones");
            g = ScriptableObject.CreateInstance<GuionDeCombate>();
            AssetDatabase.CreateAsset(g, GuionPath);
            avisos.Add("Creado " + GuionPath + ".");
        }
        g.hablanteId = "NPC_Estela";
        g.nombreHablante = "Estela";
        g.mirarAlJugador = false;
        g.introFallbackSeconds = 0.5f;
        g.comentarios = new List<ComentarioDeCombate>
        {
            new(M.Inicio, "FINAL_ESTELA_INICIO_01", null),
            new(M.JefeExpuesto, "FINAL_ESTELA_AHORA", null, vecesMax: 3, enfriamiento: 8f, caducidad: 0.8f, interrumpe: true, duracion: 1.6f),
            new(M.GolpeMalDado, "FINAL_ESTELA_ESCUDO", null, vecesMax: 2, enfriamiento: 25f, caducidad: 2f),
            new(M.GolpeValido, "FINAL_ESTELA_ACIERTO", null, vecesMax: 0, enfriamiento: 18f, caducidad: 2f),
            new(M.JugadorHerido, "FINAL_ESTELA_HERIDO", null, caducidad: 4f),
            new(M.JugadorPocaVida, "FINAL_ESTELA_POCA_VIDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 4f),
            new(M.CambioDeFase, "FINAL_ESTELA_FASE_2", null, interrumpe: true, duracion: 3f, fase: 1),
            new(M.SinAcertar, "FINAL_ESTELA_RECUERDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 3f),
        };
        EditorUtility.SetDirty(g);
        return g;
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    static T Cargar<T>(string ruta, List<string> avisos) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) avisos.Add("No encontrado: " + ruta);
        return a;
    }

    /// La misma sombra de aviso que usa la lluvia del Demonio 2.
    static GameObject SombraDeAviso(List<string> avisos)
    {
        var demonio = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Demon2.prefab");
        var ia = demonio != null ? demonio.GetComponentInChildren<ImpDemonAI>(true) : null;
        var sombra = ia != null ? new SerializedObject(ia).FindProperty("rainShadowPrefab").objectReferenceValue as GameObject : null;
        if (sombra == null) avisos.Add("No se encontró la sombra de aviso del Demonio 2 (rainShadowPrefab).");
        return sombra;
    }

    static Vector3 Polar(float radio, float grados)
        => Quaternion.Euler(0f, grados, 0f) * Vector3.forward * radio;

    static Transform Punto(string nombre, Transform padre, Vector3 pos)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre);
        t.position = pos;
        return t;
    }

    static Transform[] Anillo(string nombre, Transform padre, float radio, int n, float desfase)
    {
        var r = new Transform[n];
        for (int i = 0; i < n; i++) r[i] = Punto($"{nombre} {i}", padre, Polar(radio, desfase + i * 360f / n));
        return r;
    }

    static GameObject Cubo(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        c.transform.SetParent(padre);
        c.transform.position = pos;
        c.transform.localScale = escala;
        c.isStatic = true;
        c.GetComponent<Renderer>().sharedMaterial = mat;
        return c;
    }

    static void Capa(GameObject go, string capa, bool soloRaiz = false)
    {
        int l = LayerMask.NameToLayer(capa);
        if (l < 0) return;
        go.layer = l;
        if (soloRaiz) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = l;
    }

    static LineRenderer Rayo(string nombre, Transform padre, Material mat, float ancho, bool activo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.widthMultiplier = ancho;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.SetPosition(0, padre.position);
        lr.SetPosition(1, padre.position + Vector3.up);
        lr.enabled = activo;
        return lr;
    }

    static Material Mat(string nombre, Color color, Color? emision = null)
    {
        string ruta = $"{Materiales}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        if (emision.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision.Value * 1.5f);
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material MatRayo(string nombre, Color color)
    {
        string ruta = $"{Materiales}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static void AnadirABuild(string ruta)
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (escenas.Exists(e => e.path == ruta)) return;
        escenas.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
