using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using M = MomentoDeCombate;

/// Genera una escena de prueba de la batalla final contra el Mago Oscuro (el Corazón del Sendero):
/// una plaza circular flotando en el vacío con el altar al norte, cuatro pilares con anclas, cuatro
/// plataformas elevadas, tres nodos de conducto y el grupo completo (Will, Estela y Liam).
/// Usa el jugador, los compañeros y el Mago reales. No toca el Sendero ni los prefabs. Crea también
/// el guion de Estela para este combate si no existe. Regenerarla borra los cambios hechos a mano
/// dentro de la escena. Ver INC-509.
public static class BatallaFinalLabBuilder
{
    const string ScenePath = "Assets/Scenes/Test/BatallaFinal.unity";
    const string Materiales = "Assets/Scenes/Test/BatallaFinalMaterials";
    const string GuionPath = "Assets/BOSSBATTLES/Guiones/Guion_BatallaFinal_Estela.asset";

    const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    const string PrefabMago = "Assets/Prefabs/_MAGO_OSCURO.prefab";
    const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    const string PrefabGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    const string PrefabSombra = "Assets/Prefabs/Enemy/Spider1.prefab";
    const string HechizoGolpe = "Assets/_SPELLS/MagoOscuroGolpe.asset";

    // Efectos (los que falten se avisan y se dejan vacíos)
    const string VfxAparicion = "Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Aparicion.prefab";
    const string VfxDerrota = "Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Derrota.prefab";
    const string VfxRebobinado = "Assets/_VFX/BatallaFinal/VFX_Rebobinado.prefab";
    const string VfxSacrificio = "Assets/_VFX/BatallaFinal/VFX_SacrificioLiam.prefab";
    const string VfxZona = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect2.prefab";
    const string VfxMarea = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect3.prefab";
    const string VfxCarga = "Assets/VFX/Free Game VFX/Prefab/FX_Purple_Hit_02.prefab";
    const string VfxResquicio = "Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab";
    const string VfxInvocacion = "Assets/VFX/100BestEffectPack/Effects/PortalEffect/PortalEffect2.prefab";
    const string VfxSalto = "Assets/VFX/Free Game VFX/Prefab/FX_Greenlight_shrink.prefab";
    const string VfxCorte = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_02.prefab";
    const string VfxRoturaAncla = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab";
    const string VfxRoturaNodo = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Red energy explosion.prefab";

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

        // ── Ambiente: el vacío entre mundos ───────────────────────────────
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 0.9f;
        luz.color = new Color(0.8f, 0.75f, 1f);
        luz.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.18f, 0.3f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.08f, 0.05f, 0.12f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;

        var matSuelo = Mat("BF_Suelo", new Color(0.24f, 0.22f, 0.3f));
        var matPiedra = Mat("BF_Piedra", new Color(0.35f, 0.32f, 0.42f));
        var matAltar = Mat("BF_Altar", new Color(0.18f, 0.1f, 0.25f));
        var matAncla = Mat("BF_Ancla", new Color(0.55f, 0.2f, 0.9f), emision: new Color(0.6f, 0.15f, 1f));
        var matNodo = Mat("BF_Nodo", new Color(0.15f, 0.05f, 0.25f), emision: new Color(0.35f, 0f, 0.6f));
        var matUnion = Mat("BF_Union", new Color(0.9f, 0.3f, 1f), emision: new Color(1f, 0.3f, 1f));
        var rayoSombra = MatRayo("BF_RayoSombra", new Color(0.45f, 0.05f, 0.7f));
        var rayoAliado = MatRayo("BF_RayoAliado", new Color(0.4f, 0.9f, 1f));
        var rayoEnergia = MatRayo("BF_RayoEnergia", new Color(1f, 0.55f, 0.75f));
        var rayoAguja = MatRayo("BF_RayoAguja", new Color(1f, 0.97f, 0.8f));

        // ── Geometría con NavMesh ─────────────────────────────────────────
        var arena = new GameObject("BF_ARENA");
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "Plaza del Corazón";
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
            var borde = Cubo($"Borde {i}", arena.transform, Polar(21f, a) + Vector3.up * 0.5f, new Vector3(5.2f, 1f, 0.8f), matPiedra);
            borde.transform.rotation = Quaternion.Euler(0f, a + 90f, 0f);
        }

        // Altar al norte
        var altar = Cubo("Altar", arena.transform, new Vector3(0f, 0.6f, 16f), new Vector3(4f, 1.2f, 3f), matAltar);
        Cubo("Altar · columna", arena.transform, new Vector3(0f, 2.6f, 17f), new Vector3(1.2f, 4f, 1.2f), matAltar);
        var union = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        union.name = "Unión del conducto";
        union.transform.SetParent(arena.transform);
        union.transform.position = new Vector3(0f, 5f, 17f);
        union.transform.localScale = Vector3.one * 0.9f;
        Object.DestroyImmediate(union.GetComponent<Collider>());
        union.GetComponent<Renderer>().sharedMaterial = matUnion;
        var puntoAltar = Punto("Sitio del Mago en el altar", arena.transform, new Vector3(0f, 0f, 12.5f));

        // Pilares y plataformas
        var pilares = new List<Transform>();
        foreach (float a in new[] { 45f, 135f, 225f, 315f })
            pilares.Add(Cubo($"Pilar {a}", arena.transform, Polar(13f, a) + Vector3.up * 3.5f, new Vector3(2f, 7f, 2f), matPiedra).transform);
        int k = 0;
        foreach (float a in new[] { 0f, 90f, 180f, 270f })
        {
            float alto = k++ % 2 == 0 ? 3f : 5f;
            if (a == 0f) continue; // al norte está el altar
            Cubo($"Plataforma {a}", arena.transform, Polar(10f, a) + Vector3.up * alto, new Vector3(5f, 0.5f, 5f), matPiedra);
        }

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
        escenario.unionDelConducto = union.transform;
        escenario.conductoPrincipal = Rayo("Conducto principal", altar.transform, rayoSombra, 0.25f, true);
        escenario.puntosDeSalto = Anillo("Salto", raiz.transform, 9.5f, 8, 0f);
        escenario.cuadrantes = Anillo("Cuadrante", raiz.transform, 10f, 4, 45f);
        escenario.puntosDeInvocacion = Anillo("Invocación", raiz.transform, 8f, 6, 30f);
        var vuelo = new List<Transform>();
        foreach (var p in pilares) vuelo.Add(Punto("Vuelo", raiz.transform, new Vector3(p.position.x * 0.85f, 8.5f, p.position.z * 0.85f)));
        escenario.puntosDeVuelo = vuelo.ToArray();
        escenario.registro = raiz.AddComponent<RegistroTemporal>();

        // Anclas en lo alto de los pilares
        var anclas = new List<AnclaDelSendero>();
        foreach (var p in pilares)
        {
            var ancla = new GameObject($"Ancla · {p.name}");
            ancla.transform.SetParent(raiz.transform);
            ancla.transform.position = p.position + Vector3.up * 4.3f;
            Capa(ancla, "Enemy");
            var col = ancla.AddComponent<SphereCollider>();
            col.radius = 0.9f;
            var vida = ancla.AddComponent<Damageable>();
            ancla.AddComponent<DebilidadDePersonaje>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Cristal";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(ancla.transform, false);
            visual.transform.localScale = Vector3.one * 1.3f;
            visual.GetComponent<Renderer>().sharedMaterial = matAncla;
            var a = ancla.AddComponent<AnclaDelSendero>();
            var so = new SerializedObject(a);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("rayo").objectReferenceValue = Rayo("Rayo", ancla.transform, rayoSombra, 0.08f, false);
            so.FindProperty("vfxRotura").objectReferenceValue = Cargar<GameObject>(VfxRoturaAncla, avisos);
            so.ApplyModifiedPropertiesWithoutUndo();
            anclas.Add(a);
        }
        escenario.anclas = anclas.ToArray();

        // Nodos de conducto: este, oeste y sur
        var redGo = new GameObject("Red de conductos");
        redGo.transform.SetParent(raiz.transform);
        var nodos = new List<NodoDeConducto>();
        foreach (float a in new[] { 90f, 270f, 180f })
        {
            var nodo = new GameObject($"Nodo {a}");
            nodo.transform.SetParent(redGo.transform);
            nodo.transform.position = Polar(15f, a);
            Capa(nodo, "Enemy");
            var col = nodo.AddComponent<CapsuleCollider>();
            col.center = Vector3.up * 1.2f;
            col.height = 2.4f;
            col.radius = 0.8f;
            nodo.AddComponent<Damageable>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Cristal oscuro";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(nodo.transform, false);
            visual.transform.localPosition = Vector3.up * 1.2f;
            visual.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            visual.GetComponent<Renderer>().sharedMaterial = matNodo;
            var n = nodo.AddComponent<NodoDeConducto>();
            var plantilla = Rayo("Rayo de aliado (plantilla)", nodo.transform, rayoAliado, 0.12f, false);
            var so = new SerializedObject(n);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("conducto").objectReferenceValue = Rayo("Conducto al altar", nodo.transform, rayoSombra, 0.15f, false);
            so.FindProperty("plantillaRayoAliado").objectReferenceValue = plantilla;
            so.FindProperty("vfxRotura").objectReferenceValue = Cargar<GameObject>(VfxRoturaNodo, avisos);
            so.ApplyModifiedPropertiesWithoutUndo();
            nodos.Add(n);
            CrearTexto($"Nodo", nodo.transform, Vector3.up * 3.4f);
        }
        var red = redGo.AddComponent<RedDeConductos>();
        var soRed = new SerializedObject(red);
        var arr = soRed.FindProperty("nodos");
        arr.arraySize = nodos.Count;
        for (int i = 0; i < nodos.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = nodos[i];
        soRed.ApplyModifiedPropertiesWithoutUndo();
        escenario.red = red;

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
        var escudo = mago.AddComponent<SoloDanoCuandoExpuesto>();
        var soEscudo = new SerializedObject(escudo);
        soEscudo.FindProperty("curacionPorGolpe").floatValue = 0f;   // fuera de ventana, los golpes no hacen nada
        soEscudo.ApplyModifiedPropertiesWithoutUndo();
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
        soIA.FindProperty("guion").objectReferenceValue = CrearGuion(avisos);
        soIA.FindProperty("vfxAviso").objectReferenceValue = SombraDeAviso(avisos);
        soIA.FindProperty("vfxZona").objectReferenceValue = Cargar<GameObject>(VfxZona, avisos);
        soIA.FindProperty("vfxTeletransporte").objectReferenceValue = Cargar<GameObject>(VfxAparicion, avisos);
        soIA.FindProperty("vfxCarga").objectReferenceValue = Cargar<GameObject>(VfxCarga, avisos);
        soIA.FindProperty("vfxMarea").objectReferenceValue = Cargar<GameObject>(VfxMarea, avisos);
        soIA.FindProperty("vfxResquicio").objectReferenceValue = Cargar<GameObject>(VfxResquicio, avisos);
        soIA.FindProperty("vfxInvocacion").objectReferenceValue = Cargar<GameObject>(VfxInvocacion, avisos);
        soIA.FindProperty("vfxRebobinado").objectReferenceValue = Cargar<GameObject>(VfxRebobinado, avisos);
        soIA.FindProperty("prefabSombra").objectReferenceValue = Cargar<GameObject>(PrefabSombra, avisos);
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

    // ── Guion de Estela ───────────────────────────────────────────────────

    static GuionDeCombate CrearGuion(List<string> avisos)
    {
        var g = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(GuionPath);
        if (g != null) return g;
        if (!AssetDatabase.IsValidFolder("Assets/BOSSBATTLES/Guiones")) AssetDatabase.CreateFolder("Assets/BOSSBATTLES", "Guiones");
        g = ScriptableObject.CreateInstance<GuionDeCombate>();
        g.hablanteId = "NPC_Estela";
        g.nombreHablante = "Estela";
        g.mirarAlJugador = false;
        g.introFallbackSeconds = 0.5f;
        g.comentarios = new List<ComentarioDeCombate>
        {
            new(M.Inicio, "FINAL_ESTELA_INICIO_01", null),
            new(M.Inicio, "FINAL_ESTELA_INICIO_02", null),
            new(M.JefeExpuesto, "FINAL_ESTELA_AHORA", null, vecesMax: 3, enfriamiento: 6f, caducidad: 0.8f, interrumpe: true, duracion: 1.6f),
            new(M.GolpeMalDado, "FINAL_ESTELA_ESCUDO", null, vecesMax: 2, enfriamiento: 20f, caducidad: 2f),
            new(M.GolpeValido, "FINAL_ESTELA_ACIERTO", null, vecesMax: 0, enfriamiento: 16f, caducidad: 2f),
            new(M.JugadorHerido, "FINAL_ESTELA_HERIDO", null, caducidad: 4f),
            new(M.JugadorPocaVida, "FINAL_ESTELA_POCA_VIDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 4f),
            new(M.CambioDeFase, "FINAL_ESTELA_FASE_2", null, interrumpe: true, duracion: 4f, fase: 1),
            new(M.CambioDeFase, "FINAL_ESTELA_FASE_3", null, interrumpe: true, duracion: 5f, fase: 2),
            new(M.SinAcertar, "FINAL_ESTELA_RECUERDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 3f),
        };
        AssetDatabase.CreateAsset(g, GuionPath);
        avisos.Add("Creado " + GuionPath + ".");
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

    static void CrearTexto(string texto, Transform padre, Vector3 local)
    {
        var go = new GameObject("Etiqueta");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        var tm = go.AddComponent<TextMesh>();
        tm.text = texto;
        tm.characterSize = 0.25f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.8f, 0.7f, 1f);
    }

    static void AnadirABuild(string ruta)
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (escenas.Exists(e => e.path == ruta)) return;
        escenas.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
