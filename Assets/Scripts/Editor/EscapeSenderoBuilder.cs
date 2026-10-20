using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera el nivel jugable de huida del Sendero de las Estrellas mientras colapsa.
/// Clona la estética, cielo, iluminación cósmica y plataformas reales de Sendero.unity,
/// integrando la huida con Estela portando a Liam (inerte, con animaciones congeladas) y
/// Will abriendo camino por delante (3 obstáculos) mientras el vacío cósmico persigue desde atrás.
/// </summary>
public static class EscapeSenderoBuilder
{
    public const string SourceScenePath = "Assets/Scenes/Worlds/Sendero.unity";
    public const string ScenePath = "Assets/Scenes/Test/EscapeSendero.unity";

    // Prefabs oficiales
    private const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    private const string PrefabEstela = "Assets/Prefabs/_ESTELA.prefab";
    private const string PrefabLiam = "Assets/Prefabs/_LIAM.prefab";
    private const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    private const string PrefabHuellas = "Assets/Prefabs/FootprintEffect.prefab";

    // Materiales auténticos de Sendero
    private const string MatCristalPath = "Assets/Art/Materials/Sendero/Mat_Sendero_CristalAstral.mat";
    private const string MatOroPath = "Assets/Art/Materials/Sendero/Mat_OroAstral.mat";
    private const string MatAltarPath = "Assets/Art/Materials/Sendero/Mat_AltarBlancoPuro.mat";
    private const string MatObsidianaPath = "Assets/Art/Materials/Sendero/Mat_ObsidianaAstral.mat";

    [MenuItem("El Sendero/Escenario/Crear o regenerar Nivel de Escape del Sendero")]
    public static void MenuCrear() => Crear(pedirConfirmacion: false);

    public static void Crear(bool pedirConfirmacion = false)
    {
        if (pedirConfirmacion && File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Escape del Sendero",
                "Se reemplazará EscapeSendero.unity con el diseño real de Sendero. ¿Continuar?", "Regenerar", "Cancelar"))
            return;

        if (!File.Exists(SourceScenePath))
        {
            EditorUtility.DisplayDialog("Escape del Sendero", $"No se encuentra la escena base: {SourceScenePath}", "Aceptar");
            return;
        }

        var avisos = new List<string>();
        var willPrefab = Cargar<GameObject>(PrefabWill, avisos);
        var estelaPrefab = Cargar<GameObject>(PrefabEstela, avisos);
        var liamPrefab = Cargar<GameObject>(PrefabLiam, avisos);
        var camaraPrefab = Cargar<GameObject>(PrefabCamara, avisos);

        if (willPrefab == null || estelaPrefab == null || liamPrefab == null)
        {
            EditorUtility.DisplayDialog("Escape del Sendero", "Faltan prefabs principales (_WILL, _ESTELA o _LIAM).", "Aceptar");
            return;
        }

        // 1. Clonar Sendero.unity directamente para heredar 100% de arte, cielo, luces y mallas
        File.Copy(SourceScenePath, ScenePath, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 2. Limpiar elementos de la historia previa y piezas viejas
        LimpiarObjetoEnEscena(escena, "TRIGGERS_DE_PROGRESION");
        LimpiarObjetoEnEscena(escena, "SENDERO_PROGRESSION_CONTROLLER");
        LimpiarObjetoEnEscena(escena, "_WILL");
        LimpiarObjetoEnEscena(escena, "_ESTELA");
        LimpiarObjetoEnEscena(escena, "_LIAM");
        LimpiarObjetoEnEscena(escena, "CamaraDelJugador");
        LimpiarObjetoEnEscena(escena, "ESCAPE_MECANICAS");
        LimpiarObjetoEnEscena(escena, "ESCAPE_NIVEL_CONTROLLER");
        LimpiarObjetoEnEscena(escena, "BOOTSTRAP_ESCAPE");
        LimpiarObjetoEnEscena(escena, "Anillo_Celeste_Interior");

        // 3. Cargar materiales
        var matCristal = AssetDatabase.LoadAssetAtPath<Material>(MatCristalPath) ??
                         CrearMatFallback("Mat_Cristal_Fallback", new Color(0.2f, 0.6f, 0.9f, 0.6f), new Color(0.4f, 0.8f, 1.2f));
        var matOro = AssetDatabase.LoadAssetAtPath<Material>(MatOroPath) ??
                     CrearMatFallback("Mat_Oro_Fallback", new Color(1.0f, 0.82f, 0.35f), new Color(1.2f, 0.9f, 0.4f));
        var matObsidiana = AssetDatabase.LoadAssetAtPath<Material>(MatObsidianaPath) ??
                           CrearMatFallback("Mat_Obsidiana_Fallback", new Color(0.08f, 0.04f, 0.12f), new Color(0.25f, 0.05f, 0.4f));

        // 4. Asegurar el Altar Blanco con suelo único y collider sólido
        var plaza = escena.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "Plaza_Circular_Altar")?.gameObject;

        if (plaza != null)
        {
            plaza.transform.position = new Vector3(0f, 2.30f, 266f);

            var plataformaAltar = plaza.transform.Find("Plataforma_Altar");
            if (plataformaAltar != null)
            {
                plataformaAltar.localPosition = Vector3.zero;
                plataformaAltar.localRotation = Quaternion.identity;
                plataformaAltar.localScale = new Vector3(24f, 0.5f, 24f);
                var cap = plataformaAltar.GetComponent<CapsuleCollider>();
                if (cap != null) Object.DestroyImmediate(cap);
                if (plataformaAltar.GetComponent<MeshCollider>() == null)
                    plataformaAltar.gameObject.AddComponent<MeshCollider>();
                plataformaAltar.gameObject.layer = LayerMask.NameToLayer("Floor");
            }
        }

        // 5. Controlador de Escape y puntos de avance
        var nivelCtrlObj = new GameObject("ESCAPE_NIVEL_CONTROLLER");
        var nivelCtrl = nivelCtrlObj.AddComponent<NivelEscapeSendero>();

        RenderSettings.fog = false;

        // ── Muro de Colapso Cósmico Persecutor (Limpio, sin humo ni niebla que tape la cámara) ──
        var muroColapso = new GameObject("Muro_De_Colapso_Cosmico");
        muroColapso.transform.SetPositionAndRotation(new Vector3(0f, 3.5f, 290f), Quaternion.Euler(0f, 180f, 0f));
        muroColapso.transform.SetParent(nivelCtrlObj.transform);

        var luzVacio = muroColapso.AddComponent<Light>();
        luzVacio.type = LightType.Point;
        luzVacio.color = new Color(0.55f, 0.08f, 0.85f);
        luzVacio.range = 35f;
        luzVacio.intensity = 2.5f;

        nivelCtrl.MuroColapso = muroColapso.transform;

        // ── Obstáculo 1: Barrera de Cristales Celestes en la Calzada (Z ≈ 220) ───
        // La calzada original (Suelo_04) se mantiene 100% continua y sólida.
        var barreraCristalObj = new GameObject("Barrera_Cristal_Astral");
        barreraCristalObj.transform.SetParent(nivelCtrlObj.transform, false);
        barreraCristalObj.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, 220f), Quaternion.identity);
        barreraCristalObj.layer = LayerMask.NameToLayer("Enemy");

        CrearBloque("Cristal_Centro", barreraCristalObj.transform, new Vector3(0f, 2.2f, 0f), new Vector3(2.6f, 4.4f, 2.0f), matCristal);
        CrearBloque("Cristal_Izq", barreraCristalObj.transform, new Vector3(-2.6f, 2.0f, 0.3f), new Vector3(2.2f, 4.0f, 1.8f), matCristal);
        CrearBloque("Cristal_Der", barreraCristalObj.transform, new Vector3(2.6f, 2.0f, -0.3f), new Vector3(2.2f, 4.0f, 1.8f), matCristal);
        CrearBloque("Cristal_ExtIzq", barreraCristalObj.transform, new Vector3(-5.0f, 1.6f, -0.2f), new Vector3(2.0f, 3.4f, 1.6f), matCristal);
        CrearBloque("Cristal_ExtDer", barreraCristalObj.transform, new Vector3(5.0f, 1.6f, 0.2f), new Vector3(2.0f, 3.4f, 1.6f), matCristal);

        var colCristal = barreraCristalObj.AddComponent<BoxCollider>();
        colCristal.center = new Vector3(0f, 2.0f, 0f);
        colCristal.size = new Vector3(12.0f, 5.0f, 2.5f);

        var luzCristal = barreraCristalObj.AddComponent<Light>();
        luzCristal.type = LightType.Point;
        luzCristal.color = new Color(0.3f, 0.85f, 1f);
        luzCristal.range = 16f;
        luzCristal.intensity = 4.0f;

        barreraCristalObj.AddComponent<Damageable>();
        var diana1 = barreraCristalObj.AddComponent<DianaAstralSendero>();
        diana1.NivelCtrl = nivelCtrl;
        diana1.NumeroObstaculo = 1;
        diana1.ObjetoDesactivarAlRomper = barreraCristalObj;

        nivelCtrl.BarreraCristal = barreraCristalObj;

        // ── Obstáculo 2: Barrera de Obsidiana Astral en el Desfiladero (Z ≈ 142) ─
        var barreraObsidianaObj = new GameObject("Barrera_Obsidiana_Astral");
        barreraObsidianaObj.transform.SetParent(nivelCtrlObj.transform, false);
        barreraObsidianaObj.transform.SetPositionAndRotation(new Vector3(3.0f, 2.5f, 142f), Quaternion.identity);
        barreraObsidianaObj.layer = LayerMask.NameToLayer("Enemy");

        CrearBloque("Pilar_Obsidiana_1", barreraObsidianaObj.transform, new Vector3(-2.2f, 2.0f, 0f), new Vector3(1.8f, 4.5f, 1.8f), matObsidiana);
        CrearBloque("Pilar_Obsidiana_2", barreraObsidianaObj.transform, new Vector3(-0.6f, 2.5f, 0.3f), new Vector3(2.0f, 5.4f, 2.0f), matObsidiana);
        CrearBloque("Pilar_Obsidiana_3", barreraObsidianaObj.transform, new Vector3(1.0f, 2.2f, -0.2f), new Vector3(1.8f, 4.8f, 1.8f), matObsidiana);
        CrearBloque("Pilar_Obsidiana_4", barreraObsidianaObj.transform, new Vector3(2.4f, 1.7f, 0.1f), new Vector3(1.6f, 4.0f, 1.6f), matObsidiana);

        var colBarrera = barreraObsidianaObj.AddComponent<BoxCollider>();
        colBarrera.center = new Vector3(0f, 2.0f, 0f);
        colBarrera.size = new Vector3(8.0f, 5.0f, 2.5f);

        barreraObsidianaObj.AddComponent<Damageable>();
        var diana2 = barreraObsidianaObj.AddComponent<DianaAstralSendero>();
        diana2.NivelCtrl = nivelCtrl;
        diana2.NumeroObstaculo = 2;
        diana2.ObjetoDesactivarAlRomper = barreraObsidianaObj;

        nivelCtrl.BarreraObsidiana = barreraObsidianaObj;

        // ── Obstáculo 3: Sello Celestial del Umbral (Z ≈ 58) y Megalito Rúnico (Z = 64) ───
        var selloUmbralObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        selloUmbralObj.name = "Sello_Celestial_Umbral";
        selloUmbralObj.transform.SetParent(nivelCtrlObj.transform, false);
        selloUmbralObj.transform.SetPositionAndRotation(new Vector3(0f, 1.8f, 58f), Quaternion.identity);
        selloUmbralObj.transform.localScale = new Vector3(8.5f, 4.2f, 0.5f);
        var rSello = selloUmbralObj.GetComponent<Renderer>();
        if (rSello != null) rSello.sharedMaterial = matOro;

        var luzSello = selloUmbralObj.AddComponent<Light>();
        luzSello.type = LightType.Point;
        luzSello.color = new Color(1f, 0.85f, 0.4f);
        luzSello.range = 10f;
        luzSello.intensity = 2.5f;

        nivelCtrl.SelloUmbral = selloUmbralObj;

        // Megalito rúnico en la terraza del santuario que Will golpea para abrir el sello
        var megalitoObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        megalitoObj.name = "Megalito_Runa_Amanecer";
        megalitoObj.transform.SetParent(nivelCtrlObj.transform, false);
        megalitoObj.transform.SetPositionAndRotation(new Vector3(3.5f, 2.0f, 64f), Quaternion.identity);
        megalitoObj.transform.localScale = new Vector3(1.6f, 3.4f, 1.6f);
        megalitoObj.layer = LayerMask.NameToLayer("Enemy");
        var rMegalito = megalitoObj.GetComponent<Renderer>();
        if (rMegalito != null) rMegalito.sharedMaterial = matOro;

        var luzMegalito = megalitoObj.AddComponent<Light>();
        luzMegalito.type = LightType.Point;
        luzMegalito.color = new Color(0.4f, 1f, 0.85f);
        luzMegalito.range = 8f;
        luzMegalito.intensity = 2.8f;

        megalitoObj.AddComponent<Damageable>();
        var diana3 = megalitoObj.AddComponent<DianaAstralSendero>();
        diana3.NivelCtrl = nivelCtrl;
        diana3.NumeroObstaculo = 3;
        diana3.ObjetoDesactivarAlRomper = selloUmbralObj;

        // ── Checkpoints de Will ──────────────────────────────────────────────────
        var cp0 = new GameObject("Checkpoint 0 (Altar)").transform;
        cp0.SetPositionAndRotation(new Vector3(0.5f, 2.95f, 260f), Quaternion.Euler(0f, 180f, 0f));
        cp0.SetParent(nivelCtrlObj.transform);

        var cp1 = new GameObject("Checkpoint 1 (Reconciliacion Z227)").transform;
        cp1.SetPositionAndRotation(new Vector3(0.5f, 1.95f, 227f), Quaternion.Euler(0f, 180f, 0f));
        cp1.SetParent(nivelCtrlObj.transform);

        var cp2 = new GameObject("Checkpoint 2 (Desfiladero Z155)").transform;
        cp2.SetPositionAndRotation(new Vector3(2.5f, 2.95f, 155f), Quaternion.Euler(0f, 180f, 0f));
        cp2.SetParent(nivelCtrlObj.transform);

        var cp3 = new GameObject("Checkpoint 3 (Santuario Z64)").transform;
        cp3.SetPositionAndRotation(new Vector3(0f, 1.25f, 64f), Quaternion.Euler(0f, 180f, 0f));
        cp3.SetParent(nivelCtrlObj.transform);

        var cp4 = new GameObject("Checkpoint 4 (Llegada Z15)").transform;
        cp4.SetPositionAndRotation(new Vector3(0f, 0.20f, 15f), Quaternion.Euler(0f, 180f, 0f));
        cp4.SetParent(nivelCtrlObj.transform);

        // ── Puntos de acompañamiento de Estela ────────────────────────────────────
        var puntoEstela0 = new GameObject("Punto Estela 0 (Ante Barrera Cristal Z226)").transform;
        puntoEstela0.SetPositionAndRotation(new Vector3(-0.5f, 1.95f, 226f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela0.SetParent(nivelCtrlObj.transform);

        var puntoEstela1 = new GameObject("Punto Estela 1 (Ante Barrera Obsidiana Z149)").transform;
        puntoEstela1.SetPositionAndRotation(new Vector3(2.8f, 2.95f, 149f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela1.SetParent(nivelCtrlObj.transform);

        var puntoEstela2 = new GameObject("Punto Estela 2 (Santuario Z64)").transform;
        puntoEstela2.SetPositionAndRotation(new Vector3(0f, 1.25f, 64f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela2.SetParent(nivelCtrlObj.transform);

        var puntoEstela3 = new GameObject("Punto Estela 3 (Umbral Z-4)").transform;
        puntoEstela3.SetPositionAndRotation(new Vector3(0f, 0.20f, -4f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela3.SetParent(nivelCtrlObj.transform);

        // ── Huellas estelares limpias ───────────────────────────────────────────
        var fpGo = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabHuellas);
        if (fpGo != null)
        {
            var col = fpGo.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col, true);
                EditorUtility.SetDirty(fpGo);
            }
        }

        var pool = escena.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<StarWorldFootprintPool>(true))
            .FirstOrDefault();
        if (pool == null)
        {
            var poolObj = new GameObject("StarWorldFootprintPool");
            pool = poolObj.AddComponent<StarWorldFootprintPool>();
        }
        var footprintPrefab = AssetDatabase.LoadAssetAtPath<FootprintEffect>(PrefabHuellas);
        if (footprintPrefab != null)
        {
            var soPool = new SerializedObject(pool);
            soPool.FindProperty("_prefab").objectReferenceValue = footprintPrefab;
            soPool.FindProperty("_footprintColor").colorValue = new Color(0.5f, 0.8f, 1f, 0.4f);
            soPool.FindProperty("_footprintScale").floatValue = 0.35f;
            soPool.FindProperty("_yOffset").floatValue = 0.02f;
            soPool.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Trigger de Meta en el Umbral ─────────────────────────────────────────
        var triggerMeta = new GameObject("Trigger_MetaUmbral");
        triggerMeta.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -6f), Quaternion.identity);
        var colMeta = triggerMeta.AddComponent<BoxCollider>();
        colMeta.isTrigger = true;
        colMeta.size = new Vector3(10f, 6f, 4f);
        var triggerEvento = triggerMeta.AddComponent<TriggerDeMeta>();
        triggerEvento.nivelCtrl = nivelCtrl;

        // ── Personajes y Porteo Calibrado en el Altar ────────────────────────────
        var jugador = (GameObject)PrefabUtility.InstantiatePrefab(willPrefab, escena);
        jugador.name = "_WILL";
        jugador.transform.SetPositionAndRotation(new Vector3(0.5f, 2.95f, 260f), Quaternion.Euler(0f, 180f, 0f));

        var estela = (GameObject)PrefabUtility.InstantiatePrefab(estelaPrefab, escena);
        estela.name = "_ESTELA";
        estela.transform.SetPositionAndRotation(new Vector3(-0.5f, 2.95f, 260f), Quaternion.Euler(0f, 180f, 0f));

        var liam = (GameObject)PrefabUtility.InstantiatePrefab(liamPrefab, escena);
        liam.name = "_LIAM";
        liam.transform.SetPositionAndRotation(new Vector3(-0.5f, 2.95f, 260f), Quaternion.Euler(0f, 180f, 0f));

        // Inmovilizar a Liam totalmente (sin respiración ni gestos, conservando pose Mecanim sin caer a T-pose)
        var animsLiam = liam.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animsLiam.Length; i++)
        {
            if (animsLiam[i] != null)
            {
                animsLiam[i].enabled = true;
                animsLiam[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animsLiam[i].speed = 0f;
            }
        }

        var porteo = estela.AddComponent<PorteoDeCompanero>();
        var soPorteo = new SerializedObject(porteo);
        soPorteo.FindProperty("cuerpo").objectReferenceValue = liam.transform;
        soPorteo.FindProperty("offsetRotacion").vector3Value = Vector3.zero;
        soPorteo.FindProperty("offsetPosicion").vector3Value = new Vector3(-0.08f, -0.16f, 0f);
        soPorteo.ApplyModifiedPropertiesWithoutUndo();

        foreach (var it in estela.GetComponentsInChildren<Interactable>(true))
        {
            it.enabled = false;
            var soIt = new SerializedObject(it);
            var initProp = soIt.FindProperty("initiallyEnabled");
            if (initProp != null) initProp.boolValue = false;
            soIt.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var it in liam.GetComponentsInChildren<Interactable>(true))
        {
            it.enabled = false;
            var soIt = new SerializedObject(it);
            var initProp = soIt.FindProperty("initiallyEnabled");
            if (initProp != null) initProp.boolValue = false;
            soIt.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cámara del jugador mirando al sur
        if (camaraPrefab != null)
        {
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camaraPrefab, escena);
            cam.name = "CamaraDelJugador";
            cam.transform.SetPositionAndRotation(new Vector3(0f, 4.0f, 264f), Quaternion.Euler(14f, 180f, 0f));
        }

        // Bootstrap limpio de Escape
        var inicio = new GameObject("BOOTSTRAP_ESCAPE").AddComponent<EscapeSenderoBootstrap>();
        var soInicio = new SerializedObject(inicio);
        soInicio.FindProperty("player").objectReferenceValue = jugador;
        soInicio.ApplyModifiedPropertiesWithoutUndo();

        // Conectar referencias en NivelEscapeSendero
        var soNivel = new SerializedObject(nivelCtrl);
        soNivel.FindProperty("jugador").objectReferenceValue = jugador.transform;
        soNivel.FindProperty("porteoEstela").objectReferenceValue = porteo;

        var puntosProp = soNivel.FindProperty("puntosSegurosEstela");
        puntosProp.arraySize = 4;
        puntosProp.GetArrayElementAtIndex(0).objectReferenceValue = puntoEstela0;
        puntosProp.GetArrayElementAtIndex(1).objectReferenceValue = puntoEstela1;
        puntosProp.GetArrayElementAtIndex(2).objectReferenceValue = puntoEstela2;
        puntosProp.GetArrayElementAtIndex(3).objectReferenceValue = puntoEstela3;

        var cpsProp = soNivel.FindProperty("checkpointsWill");
        cpsProp.arraySize = 5;
        cpsProp.GetArrayElementAtIndex(0).objectReferenceValue = cp0;
        cpsProp.GetArrayElementAtIndex(1).objectReferenceValue = cp1;
        cpsProp.GetArrayElementAtIndex(2).objectReferenceValue = cp2;
        cpsProp.GetArrayElementAtIndex(3).objectReferenceValue = cp3;
        cpsProp.GetArrayElementAtIndex(4).objectReferenceValue = cp4;

        soNivel.ApplyModifiedPropertiesWithoutUndo();

        // Guardar escena final
        EditorSceneManager.SaveScene(escena, ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EscapeSenderoBuilder] ¡Nivel de Escape generado con éxito con colapso persecutor y 3 obstáculos dinámicos!");
#endif
    }

    public static void AddToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != scenePath) continue;
            if (!scenes[i].enabled)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void LimpiarObjetoEnEscena(Scene escena, string nombre)
    {
        var encontrados = escena.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == nombre)
            .Select(t => t.gameObject)
            .ToList();

        foreach (var go in encontrados)
        {
            Object.DestroyImmediate(go);
        }
    }

    private static GameObject CrearBloque(string nombre, Transform padre, Vector3 posLocal, Vector3 escala, Material mat, bool capaSuelo = false)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = nombre;
        obj.transform.SetParent(padre, false);
        obj.transform.localPosition = posLocal;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = escala;
        var r = obj.GetComponent<Renderer>();
        if (r && mat) r.sharedMaterial = mat;
        if (capaSuelo)
        {
            obj.layer = LayerMask.NameToLayer("Floor");
            var cap = obj.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
            if (obj.GetComponent<MeshCollider>() == null)
                obj.AddComponent<MeshCollider>();
        }
        return obj;
    }

    private static T Cargar<T>(string ruta, List<string> avisos) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (asset == null) avisos.Add($"Falta asset: {ruta}");
        return asset;
    }

    private static Material CrearMatFallback(string nombre, Color albedo, Color? emision = null)
    {
        var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(s) { name = nombre };
        m.color = albedo;
        if (emision.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision.Value);
        }
        return m;
    }
}

/// <summary>
/// Trigger auxiliar para detectar la entrada del jugador en la meta del Umbral.
/// </summary>
public class TriggerDeMeta : MonoBehaviour
{
    public NivelEscapeSendero nivelCtrl;
    public UnityEvent alEntrar = new UnityEvent();

    private void OnTriggerEnter(Collider other)
    {
        var player = PlayerService.Player;
        if (player != null && (other.gameObject == player || other.transform.IsChildOf(player.transform)))
        {
            if (nivelCtrl != null) nivelCtrl.LlegarAlUmbral();
            alEntrar?.Invoke();
            enabled = false;
        }
    }
}
