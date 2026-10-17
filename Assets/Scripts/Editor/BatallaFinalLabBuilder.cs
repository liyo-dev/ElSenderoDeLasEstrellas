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
/// luz, con el suelo de luz, bordes invisibles y solo el altar de piedra blanca.
/// Sin pilares, sin rocas moradas ni plataformas flotantes: un duelo implacable estilo KH2 Sephiroth
/// con combate en tierra, mar del abismo (vuelo 3D obligatorio) y ruptura de barrera final con Amanecer.
/// Trae el grupo completo (Will, Estela y Liam). Regenerarla borra los cambios hechos a mano dentro de la escena.
/// Ver INC-509, INC-663, INC-761.
public static class BatallaFinalLabBuilder
{
    const string ScenePath = "Assets/Scenes/Test/BatallaFinal.unity";
    const string Materiales = "Assets/Scenes/Test/BatallaFinalMaterials";
    const string GuionPath = "Assets/BOSSBATTLES/Guiones/Guion_BatallaFinal_Estela.asset";

    const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    const string PrefabMago = "Assets/Prefabs/_MAGO_OSCURO.prefab";
    const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    const string PrefabGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    const string HechizoGolpe = "Assets/_SPELLS/MagoOscuroGolpe.asset";

    // Efectos
    const string VfxAparicion = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect5.prefab";
    const string VfxDerrota = "Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Derrota.prefab";
    const string VfxRebobinado = "Assets/_VFX/BatallaFinal/VFX_Rebobinado.prefab";
    const string VfxSacrificio = "Assets/_VFX/BatallaFinal/VFX_SacrificioLiam.prefab";
    const string VfxZona = "Assets/_VFX/Hechizos/VFX_Hechizo_MagoOscuroGrieta_Zona.prefab";
    const string VfxMarea = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect3.prefab";
    const string VfxRegeneracion = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect1.prefab";
    const string VfxProtegido = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield pink.prefab";
    const string VfxCarga = "Assets/VFX/Free Game VFX/Prefab/FX_Purple_Hit_02.prefab";
    const string VfxSalto = "Assets/VFX/Free Game VFX/Prefab/FX_Greenlight_shrink.prefab";
    const string VfxCorte = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_02.prefab";
    const string VfxExplosionBomba = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab";
    const string VfxPozo = "Assets/_VFX/Prologo/VFX_AgujeroNegro.prefab";
    const string VfxImplosionPozo = "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect4.prefab";

    [MenuItem("El Sendero/Combate/Crear o regenerar la Batalla Final (prueba)")]
    public static void MenuCrear() => Crear(pedirConfirmacion: false);

    public static void Crear(bool pedirConfirmacion = false)
    {
        if (pedirConfirmacion && File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Batalla Final",
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
        var rayoSombra = MatRayo("BF_RayoSombra", new Color(0.45f, 0.05f, 0.7f));
        var rayoEnergia = MatRayo("BF_RayoEnergia", new Color(1f, 0.55f, 0.75f));
        var rayoAguja = MatRayo("BF_RayoAguja", new Color(1f, 0.97f, 0.8f));

        // ── Geometría fija: arena diáfana, bordes invisibles y el altar ──
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
        escenario.radio = 21f;
        escenario.altar = altar.transform;
        escenario.puntoDelAltar = puntoAltar;

        // Puntos de combate en tierra (deslizamientos y teletransportes)
        var saltos = new List<Transform>();
        saltos.AddRange(Anillo("Salto_Int", raiz.transform, 9.5f, 8, 0f));
        saltos.AddRange(Anillo("Salto_Ext", raiz.transform, 16f, 6, 15f));
        escenario.puntosDeSalto = saltos.ToArray();

        // Puntos de vuelo en fase 2 (combate aéreo obligatorio mid-air)
        var vuelos = new List<Transform>();
        vuelos.AddRange(Anillo("Vuelo_Int", raiz.transform, 10f, 8, 0f, altura: 6.5f));
        vuelos.AddRange(Anillo("Vuelo_Ext", raiz.transform, 15f, 4, 45f, altura: 7.0f));
        escenario.puntosDeVuelo = vuelos.ToArray();

        escenario.registro = raiz.AddComponent<RegistroTemporal>();

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
        Capa(mago, "Enemy", soloRaiz: false);
        var manager = mago.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager)
        {
            var soManager = new SerializedObject(manager);
            var config = soManager.FindProperty("configuration");
            var tipos = config.FindPropertyRelative("behaviourType");
            tipos.intValue &= ~(int)Game.NPC.Common.NPCBehaviourType.Combat;
            config.FindPropertyRelative("combatConfig").objectReferenceValue = null;
            soManager.ApplyModifiedPropertiesWithoutUndo();
            manager.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        }
        foreach (var componente in mago.GetComponentsInChildren<Game.NPC.NPCCombatBrain>(true)) Object.DestroyImmediate(componente);
        foreach (var componente in mago.GetComponentsInChildren<Game.NPC.NPCHealthBarSpawner>(true)) Object.DestroyImmediate(componente);
        foreach (var componente in mago.GetComponentsInChildren<Game.NPC.Modules.NPCCombatLifecycleHandler>(true)) Object.DestroyImmediate(componente);
        var elevacion = mago.GetComponentInChildren<ElevacionVisual>(true);
        if (elevacion == null) elevacion = mago.AddComponent<ElevacionVisual>();
        var soElevacion = new SerializedObject(elevacion);
        soElevacion.FindProperty("animator").objectReferenceValue = mago.GetComponentInChildren<Animator>(true);
        soElevacion.FindProperty("amplitudBalanceo").floatValue = 0.06f;
        soElevacion.FindProperty("frecuenciaBalanceo").floatValue = 0.55f;
        soElevacion.FindProperty("rebote").floatValue = 0f;
        soElevacion.ApplyModifiedPropertiesWithoutUndo();
        var charla = mago.GetComponent<Interactable>();
        if (charla) charla.enabled = false;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            mago.layer = enemyLayer;
            foreach (Transform hijo in mago.GetComponentsInChildren<Transform>(true))
                hijo.gameObject.layer = enemyLayer;
        }

        var targetable = mago.GetComponent<Targetable>() ?? mago.AddComponent<Targetable>();
        targetable.targetingRadius = 45f;
        targetable.isInActiveCombat = false;
        if (mago.GetComponent<EnemyMarker>() == null)
            mago.AddComponent<EnemyMarker>();

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
        soIA.FindProperty("vfxProtegido").objectReferenceValue = Cargar<GameObject>(VfxProtegido, avisos);
        soIA.FindProperty("vfxRegeneracion").objectReferenceValue = Cargar<GameObject>(VfxRegeneracion, avisos);
        soIA.FindProperty("vfxPozo").objectReferenceValue = Cargar<GameObject>(VfxPozo, avisos);
        soIA.FindProperty("vfxImplosionPozo").objectReferenceValue = Cargar<GameObject>(VfxImplosionPozo, avisos);
        soIA.FindProperty("vfxBomba").objectReferenceValue = Cargar<GameObject>(VfxZona, avisos);
        soIA.FindProperty("vfxExplosionBomba").objectReferenceValue = Cargar<GameObject>(VfxExplosionBomba, avisos);
        soIA.FindProperty("vfxMarAbismo").objectReferenceValue = Cargar<GameObject>(VfxZona, avisos);
        soIA.FindProperty("esperaInicial").floatValue = 4f;
        soIA.FindProperty("empezarSolo").boolValue = false;
        soIA.ApplyModifiedPropertiesWithoutUndo();

        // ── Plataforma del Vacío Tibio (en x = 80, aislada de la arena de combate) ──
        var vacio = new GameObject("BF_VACIO_TIBIO");
        var sueloVacio = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sueloVacio.name = "Suelo del Vacío Tibio";
        sueloVacio.transform.SetParent(vacio.transform);
        sueloVacio.transform.position = new Vector3(80f, -0.25f, 0f);
        sueloVacio.transform.localScale = new Vector3(28f, 0.25f, 28f);
        Object.DestroyImmediate(sueloVacio.GetComponent<Collider>());
        sueloVacio.AddComponent<MeshCollider>();
        var matVacio = Mat("BF_SueloVacioTibio", new Color(0.96f, 0.94f, 0.88f), emision: new Color(0.35f, 0.32f, 0.24f));
        sueloVacio.GetComponent<Renderer>().sharedMaterial = matVacio;
        sueloVacio.isStatic = true;
        Capa(sueloVacio, "Floor");

        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f;
            var borde = new GameObject($"Borde invisible Vacío {i}");
            borde.transform.SetParent(vacio.transform);
            borde.transform.SetPositionAndRotation(new Vector3(80f, 0f, 0f) + Polar(14f, a) + Vector3.up * 2f, Quaternion.Euler(0f, a + 90f, 0f));
            borde.transform.localScale = new Vector3(5.6f, 4f, 0.8f);
            borde.AddComponent<BoxCollider>();
            borde.isStatic = true;
        }

        var luzVacio = new GameObject("Luz_VacioTibio").AddComponent<Light>();
        luzVacio.transform.SetParent(vacio.transform);
        luzVacio.transform.position = new Vector3(80f, 4f, 0f);
        luzVacio.type = LightType.Point;
        luzVacio.color = new Color(1f, 0.94f, 0.82f);
        luzVacio.intensity = 2.5f;
        luzVacio.range = 25f;

        var vfxCirculo = Cargar<GameObject>("Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Healing circle.prefab", avisos);
        if (vfxCirculo != null)
        {
            var circ = (GameObject)PrefabUtility.InstantiatePrefab(vfxCirculo, vacio.transform);
            circ.transform.position = new Vector3(80f, 0.05f, 0f);
            circ.transform.localScale = Vector3.one * 2.5f;
        }

        var navVacio = vacio.AddComponent<NavMeshSurface>();
        navVacio.collectObjects = CollectObjects.Children;
        navVacio.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        navVacio.BuildNavMesh();

        // ── Jugador y grupo ───────────────────────────────────────────────
        var jugador = (GameObject)PrefabUtility.InstantiatePrefab(will, escena);
        jugador.name = "BF_JUGADOR_WILL";
        jugador.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -12f), Quaternion.identity);
        var camara = Cargar<GameObject>(PrefabCamara, avisos);
        if (camara) ((GameObject)PrefabUtility.InstantiatePrefab(camara, escena)).transform.position = new Vector3(0f, 3f, -16f);
        var grupo = Cargar<GameObject>(PrefabGrupo, avisos);
        if (grupo) PrefabUtility.InstantiatePrefab(grupo, escena);

        var estelaPrefab = Cargar<GameObject>("Assets/Prefabs/_ESTELA.prefab", avisos);
        var liamPrefab = Cargar<GameObject>("Assets/Prefabs/_LIAM.prefab", avisos);
        var archimagoPrefab = Cargar<GameObject>("Assets/Prefabs/_WILL_ORIGINAL.prefab", avisos);

        GameObject estela = null;
        if (estelaPrefab != null)
        {
            estela = (GameObject)PrefabUtility.InstantiatePrefab(estelaPrefab, escena);
            estela.name = "LAB_Estela";
            estela.transform.SetPositionAndRotation(new Vector3(1.6f, 0.1f, -12.3f), Quaternion.identity);
        }

        GameObject liam = null;
        if (liamPrefab != null)
        {
            liam = (GameObject)PrefabUtility.InstantiatePrefab(liamPrefab, escena);
            liam.name = "LAB_Liam";
            liam.transform.SetPositionAndRotation(new Vector3(-1.6f, 0.1f, -12.3f), Quaternion.identity);
        }

        if (estela != null && liam != null)
        {
            var porteo = estela.AddComponent<PorteoDeCompanero>();
            var soPorteo = new SerializedObject(porteo);
            soPorteo.FindProperty("cuerpo").objectReferenceValue = liam.transform;
            soPorteo.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject archimago = null;
        if (archimagoPrefab != null)
        {
            archimago = (GameObject)PrefabUtility.InstantiatePrefab(archimagoPrefab, escena);
            archimago.name = "BF_ARCHIMAGO_VISION";
            archimago.transform.SetPositionAndRotation(new Vector3(81.15f, 0.05f, 0f), Quaternion.Euler(0f, -90f, 0f));
            var mArch = archimago.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            if (mArch != null) mArch.enabled = false;
            var cArch = archimago.GetComponent<Interactable>();
            if (cArch != null) cArch.enabled = false;
        }

        var inicio = new GameObject("BF_INICIALIZACION").AddComponent<CombatLabBootstrap>();
        var soInicio = new SerializedObject(inicio);
        soInicio.FindProperty("player").objectReferenceValue = jugador;
        soInicio.FindProperty("grupoInicial").intValue = 3;
        soInicio.ApplyModifiedPropertiesWithoutUndo();

        // ── Cinemática de Entrada (Sistema Oficial: SequencePlayer + SequenceStage + ModuloBatallaFinal) ──
        var raizCine = new GameObject("SEQ_BatallaFinal");
        var driver = raizCine.AddComponent<CinematicCameraDriver>();
        var stage = raizCine.AddComponent<SequenceStage>();

        var soStage = new SerializedObject(stage);
        var actorsProp = soStage.FindProperty("_actors");
        actorsProp.ClearArray();

        void AddBoundActor(string id, Transform target)
        {
            if (target == null) return;
            int idx = actorsProp.arraySize;
            actorsProp.InsertArrayElementAtIndex(idx);
            var elem = actorsProp.GetArrayElementAtIndex(idx);
            elem.FindPropertyRelative("id").stringValue = id;
            elem.FindPropertyRelative("target").objectReferenceValue = target;
            elem.FindPropertyRelative("eyeHeight").floatValue = 0f;
            elem.FindPropertyRelative("animator").objectReferenceValue = target.GetComponentInChildren<Animator>(true);
            elem.FindPropertyRelative("emotionController").objectReferenceValue = Game.NPC.Common.EmotionControllerResolver.Resolve(target.gameObject);
        }

        var willCuerpo = jugador.GetComponentInChildren<Invector.vCharacterController.vThirdPersonMotor>(true)?.transform ?? jugador.transform;
        AddBoundActor("Player", willCuerpo);
        AddBoundActor("NPC_MagoOscuro", mago.transform);
        if (estela != null) AddBoundActor("NPC_Estela", estela.transform);
        if (liam != null) AddBoundActor("NPC_Liam", liam.transform);
        if (archimago != null) AddBoundActor("NPC_Archimago", archimago.transform);
        soStage.ApplyModifiedPropertiesWithoutUndo();

        var modulo = raizCine.AddComponent<ModuloBatallaFinal>();
        var soMod = new SerializedObject(modulo);
        soMod.FindProperty("_magoBossAI").objectReferenceValue = ia;
        soMod.FindProperty("_willActor").objectReferenceValue = willCuerpo;
        soMod.FindProperty("_estelaActor").objectReferenceValue = estela != null ? estela.transform : null;
        soMod.FindProperty("_liamActor").objectReferenceValue = liam != null ? liam.transform : null;
        soMod.FindProperty("_magoActor").objectReferenceValue = mago.transform;
        soMod.FindProperty("_archimagoActor").objectReferenceValue = archimago != null ? archimago.transform : null;
        soMod.FindProperty("_willAwakenedAuraVfx").objectReferenceValue = Cargar<GameObject>("Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab", avisos);
        soMod.FindProperty("_vfxAparicionMago").objectReferenceValue = null;
        var vfxPechoPrefab = Cargar<GameObject>("Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Character auras/Star aura.prefab", avisos)
            ?? Cargar<GameObject>("Assets/_VFX/Hechizos/VFX_Hechizo_Destello_Luz.prefab", avisos);
        soMod.FindProperty("_vfxDestelloPechoWill").objectReferenceValue = vfxPechoPrefab;
        soMod.ApplyModifiedPropertiesWithoutUndo();

        stage.AgregarModulos(new[] { modulo });
        soStage.Update();
        var modulesProp = soStage.FindProperty("_modules");
        modulesProp.ClearArray();
        modulesProp.InsertArrayElementAtIndex(0);
        modulesProp.GetArrayElementAtIndex(0).objectReferenceValue = modulo;
        soStage.ApplyModifiedPropertiesWithoutUndo();

        const string SeqPath = "Assets/_SEQUENCES/SEQ_BatallaFinal.asset";
        var seqDef = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SeqPath);
        var guionHorneado = AssetDatabase.LoadAssetAtPath<GuionHorneado>("Assets/_SEQUENCES/Guiones/BatallaFinal_Intro_Horneado.asset");
        if (seqDef == null)
        {
            seqDef = ScriptableObject.CreateInstance<SequenceDefinition>();
            seqDef.displayName = "Batalla Final — Intro";
            seqDef.summary = "Encuentro con el Mago Oscuro, viaje al Vacío Tibio y despertar de los cinco elementos antes de la Fase 1.";
            seqDef.signalIn = "BATALLA_FINAL_INTRO_START";
            seqDef.signalOut = "BATALLA_FINAL_COMBATE_START";
            seqDef.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
            seqDef.protagonistas = new List<string> { "Player", "NPC_MagoOscuro", "NPC_Estela", "NPC_Liam" };
            if (guionHorneado != null)
            {
                seqDef.phases = new List<SequencePhase>
                {
                    new SequencePhase
                    {
                        name = "Guion: BatallaFinal_Intro",
                        beats = new List<SequenceBeat>
                        {
                            new GuionBeat
                            {
                                note = "Generado por el horneado del guion",
                                guion = guionHorneado,
                                empezarEn = 0f
                            }
                        }
                    }
                };
            }
            AssetDatabase.CreateAsset(seqDef, SeqPath);
            AssetDatabase.SaveAssets();
        }
        else if (guionHorneado != null && (seqDef.phases == null || seqDef.phases.Count == 0 || seqDef.phases[0].beats == null || seqDef.phases[0].beats.Count == 0))
        {
            seqDef.phases = new List<SequencePhase>
            {
                new SequencePhase
                {
                    name = "Guion: BatallaFinal_Intro",
                    beats = new List<SequenceBeat>
                    {
                        new GuionBeat
                        {
                            note = "Generado por el horneado del guion",
                            guion = guionHorneado,
                            empezarEn = 0f
                        }
                    }
                }
            };
            EditorUtility.SetDirty(seqDef);
            AssetDatabase.SaveAssets();
        }

        var seqPlayer = raizCine.AddComponent<SequencePlayer>();
        var soPlayer = new SerializedObject(seqPlayer);
        soPlayer.FindProperty("_definition").objectReferenceValue = seqDef;
        soPlayer.FindProperty("_stage").objectReferenceValue = stage;
        soPlayer.FindProperty("_signalIn").stringValue = "BATALLA_FINAL_INTRO_START";
        soPlayer.FindProperty("_signalOut").stringValue = "BATALLA_FINAL_COMBATE_START";
        soPlayer.FindProperty("_cinematicCamera").objectReferenceValue = driver;
        soPlayer.FindProperty("_idleGraceAfterSequence").floatValue = 0f;
        soPlayer.ApplyModifiedPropertiesWithoutUndo();

        var autoStart = raizCine.AddComponent<SenderoBattleAutoStart>();
        var soAuto = new SerializedObject(autoStart);
        soAuto.FindProperty("_startSignal").stringValue = "BATALLA_FINAL_INTRO_START";
        soAuto.FindProperty("_startDelaySeconds").floatValue = 0.25f;
        soAuto.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test")) AssetDatabase.CreateFolder("Assets/Scenes", "Test");
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, ScenePath);
        AnadirABuild(ScenePath);
        AssetDatabase.SaveAssets();

        string resumen = "Batalla Final creada en " + ScenePath + ". Pulsa Play: arranca la cinemática de entrada (Actos 1-5) y enlaza con el combate.";
        if (avisos.Count > 0) resumen += "\nAvisos:\n- " + string.Join("\n- ", avisos);
        Debug.Log("[BatallaFinalLabBuilder] " + resumen);
    }

    // ── Guion de Estela ───────────────────────────────────────────────────

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
            new(M.JefeExpuesto, "FINAL_ESTELA_AHORA", null, vecesMax: 2, enfriamiento: 15f, caducidad: 0.8f, interrumpe: true, duracion: 1.5f),
            new(M.JugadorHerido, "FINAL_ESTELA_HERIDO", null, vecesMax: 1, enfriamiento: 45f, caducidad: 3f),
            new(M.JugadorPocaVida, "FINAL_ESTELA_POCA_VIDA", null, vecesMax: 1, enfriamiento: 60f, caducidad: 3f),
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

    static Transform PuntoConLente(string nombre, Transform padre, Vector3 pos, Quaternion rot, float fov)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre);
        go.transform.SetPositionAndRotation(pos, rot);
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = fov;
        return go.transform;
    }

    static Transform[] Anillo(string nombre, Transform padre, float radio, int n, float desfase, float altura = 0f)
    {
        var r = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Polar(radio, desfase + i * 360f / n);
            p.y = altura;
            r[i] = Punto($"{nombre} {i}", padre, p);
        }
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
