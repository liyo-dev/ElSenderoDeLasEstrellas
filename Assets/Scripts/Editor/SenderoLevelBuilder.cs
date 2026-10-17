using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Constructor oficial del nivel final «El Sendero de las Estrellas» (Sendero.unity).
/// Genera el escenario cósmico 3D sin skybox plano terrestre, el camino de cristal astral,
/// los 4 monumentos de portal celestiales secuenciales, el Altar de Piedra Blanca y el controlador de progreso.
/// Menú: El Sendero ▸ Escenario ▸ Construir Sendero de las Estrellas (Nivel Final)
/// </summary>
public static class SenderoLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Worlds/Sendero.unity";
    private const string FolderMateriales = "Assets/Art/Materials/Sendero";

    // Prefabs oficiales
    private const string PrefabWill = "Assets/Prefabs/_WILL.prefab";
    private const string PrefabEstela = "Assets/Prefabs/_ESTELA.prefab";
    private const string PrefabLiam = "Assets/Prefabs/_LIAM.prefab";
    private const string PrefabCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    private const string PrefabColapsoVfx = "Assets/_VFX/BatallaFinal/VFX_PortalSendero_Colapso.prefab";
    private const string PrefabHuellas = "Assets/Prefabs/FootprintEffect.prefab";

    [MenuItem("El Sendero/Escenario/Construir Sendero de las Estrellas (Nivel Final)")]
    public static void MenuConstruir()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("El Sendero de las Estrellas",
                "Se reconstruirá el escenario del nivel final en Sendero.unity.\n\n" +
                "Se aplicará el arte cósmico 3D, la calzada de cristal astral, los 4 portales narrativos " +
                "y la progresión lineal secuencial hacia el Altar Blanco.\n\n¿Deseas continuar?",
                "Construir Escenario", "Cancelar"))
        {
            return;
        }

        ConstruirEscenario();
    }

    public static void ConstruirEscenario()
    {
        EnsureFolder(FolderMateriales);

        var avisos = new List<string>();
        var willPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabWill);
        var estelaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEstela);
        var liamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabLiam);
        var camaraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCamara);
        var colapsoVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabColapsoVfx);

        // 1. Preparar o crear los materiales de autor necesarios
        var matCosmos = ObtenerOCrearMatCosmos();
        var matCristalSendero = ObtenerOCrearMatCristal();
        var matPortalTierra = ObtenerOCrearMatPortal("Mat_Portal_00_Tierra", new Color(0.4f, 0.8f, 1.2f), new Color(0.1f, 0.3f, 0.8f), new Color(0.8f, 1.4f, 2.2f));
        var matPortalWill = ObtenerOCrearMatPortal("Mat_Portal_01_Will", new Color(1.5f, 1.2f, 0.6f), new Color(1.0f, 0.6f, 0.1f), new Color(2.0f, 1.6f, 0.9f));
        var matPortalEstela = ObtenerOCrearMatPortal("Mat_Portal_02_Estela", new Color(1.6f, 0.4f, 0.8f), new Color(1.2f, 0.15f, 0.4f), new Color(2.0f, 0.8f, 1.4f));
        var matPortalLiam = ObtenerOCrearMatPortal("Mat_Portal_03_Liam", new Color(0.8f, 0.95f, 1.2f), new Color(0.3f, 0.45f, 0.7f), new Color(1.2f, 1.5f, 2.0f));
        var matAltarBlanco = ObtenerOCrearMatAltar();
        var matPiedraMegalito = ObtenerOCrearMatPiedraMegalito();
        var matObsidiana = ObtenerOCrearMatObsidiana();
        var matMaderaCaja = ObtenerOCrearMatMaderaCaja();
        var matOroAstral = ObtenerOCrearMatOroAstral();

        // 2. Crear o abrir la escena
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3. Atmósfera Cósmica 360° y Luz Primordial
        RenderSettings.skybox = matCosmos;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.06f, 0.16f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.03f, 0.02f, 0.08f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0025f;

        var luzObj = new GameObject("Luz Primordial Astral");
        var luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.15f;
        luz.color = new Color(0.88f, 0.92f, 1.0f);
        luz.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

        // 4. Raíces organizadas
        var raizMundo = new GameObject("SENDERO_WORLD");
        var raizCosmos = new GameObject("COSMIC_ENVIRONMENT_3D");
        raizCosmos.transform.SetParent(raizMundo.transform);

        var raizCamino = new GameObject("SENDERO_CAMINO_ASTRAL");
        raizCamino.transform.SetParent(raizMundo.transform);

        var raizPortales = new GameObject("PORTALES_CELESTIALES");
        raizPortales.transform.SetParent(raizMundo.transform);

        var raizAltar = new GameObject("PLAZA_ALTAR_BLANCO");
        raizAltar.transform.SetParent(raizMundo.transform);

        // 5. Entorno Cósmico 3D (Cuerpos Celestes y Constelaciones Físicas)
        ConstruirCosmos3D(raizCosmos.transform, matOroAstral);

        // 6. Calzada de Cristal Astral (Peregrinación por Tramos)
        ConstruirCalzadaSendero(raizCamino.transform, matCristalSendero, matOroAstral);

        // 7. Monumentos de Portales Celestiales
        var portal0 = ConstruirPortalTierra(raizPortales.transform, matPiedraMegalito, matPortalTierra);
        var portal1 = ConstruirPortalWill(raizPortales.transform, matOroAstral, matPortalWill);
        var portal2 = ConstruirPortalEstela(raizPortales.transform, matAltarBlanco, matPortalEstela);
        var portal3 = ConstruirPortalLiam(raizPortales.transform, matObsidiana, matMaderaCaja, matPortalLiam);

        // 8. Plaza del Altar de Piedra Blanca
        var altarMonolito = ConstruirPlazaAltar(raizAltar.transform, matAltarBlanco, matOroAstral, matObsidiana);

        // 9. Triggers Narrativos a lo largo del Sendero
        ConstruirTriggers(raizMundo.transform);

        // 10. Controlador de Progresión
        var ctrlObj = new GameObject("SENDERO_PROGRESSION_CONTROLLER");
        ctrlObj.transform.SetParent(raizMundo.transform);
        var ctrl = ctrlObj.AddComponent<SenderoProgressionController>();

        var soCtrl = new SerializedObject(ctrl);
        soCtrl.FindProperty("_portalOrigenTierra").objectReferenceValue = portal0;
        soCtrl.FindProperty("_portalWill").objectReferenceValue = portal1;
        soCtrl.FindProperty("_portalEstela").objectReferenceValue = portal2;
        soCtrl.FindProperty("_portalLiam").objectReferenceValue = portal3;
        if (colapsoVfxPrefab != null)
        {
            soCtrl.FindProperty("_vfxColapsoPrefab").objectReferenceValue = colapsoVfxPrefab;
        }
        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 11. Huellas de Constelaciones
        var footprintObj = new GameObject("StarWorldFootprintPool");
        footprintObj.transform.SetParent(raizMundo.transform);
        var pool = footprintObj.AddComponent<StarWorldFootprintPool>();
        var footprintPrefab = AssetDatabase.LoadAssetAtPath<FootprintEffect>(PrefabHuellas);
        if (footprintPrefab != null)
        {
            var soPool = new SerializedObject(pool);
            soPool.FindProperty("_prefab").objectReferenceValue = footprintPrefab;
            soPool.FindProperty("_footprintColor").colorValue = new Color(0.5f, 0.8f, 1f, 0.35f);
            soPool.FindProperty("_footprintScale").floatValue = 0.35f;
            soPool.ApplyModifiedPropertiesWithoutUndo();
        }

        // 12. Spawner de Héroes y Cámara
        if (willPrefab != null)
        {
            var will = (GameObject)PrefabUtility.InstantiatePrefab(willPrefab, escena);
            will.name = "_WILL";
            will.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        }
        if (estelaPrefab != null)
        {
            var estela = (GameObject)PrefabUtility.InstantiatePrefab(estelaPrefab, escena);
            estela.name = "_ESTELA";
            estela.transform.SetPositionAndRotation(new Vector3(1.5f, 0.05f, -1.2f), Quaternion.identity);
        }
        if (liamPrefab != null)
        {
            var liam = (GameObject)PrefabUtility.InstantiatePrefab(liamPrefab, escena);
            liam.name = "_LIAM";
            liam.transform.SetPositionAndRotation(new Vector3(-1.5f, 0.05f, -1.2f), Quaternion.identity);
        }
        if (camaraPrefab != null)
        {
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camaraPrefab, escena);
            cam.name = "CamaraDelJugador";
            cam.transform.SetPositionAndRotation(new Vector3(0f, 2.5f, -6f), Quaternion.Euler(15f, 0f, 0f));
        }

        // 13. Guardar escena
        EditorSceneManager.SaveScene(escena, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[SenderoLevelBuilder] ¡Escenario del Sendero de las Estrellas construido con éxito en {ScenePath}!");
#endif
    }

    // ── Construcción del Cosmos 3D ──────────────────────────────────────────

    private static void ConstruirCosmos3D(Transform padre, Material matOro)
    {
        // Posiciones de estrellas 3D físicas a distintas profundidades en el abismo
        Vector3[] posicionesEstrellas = {
            new Vector3(-45f, 18f, 30f),
            new Vector3(50f, 25f, 80f),
            new Vector3(-38f, -22f, 60f),  // Bajo el sendero
            new Vector3(42f, -18f, 140f),  // Bajo el sendero
            new Vector3(-55f, 32f, 170f),
            new Vector3(60f, 15f, 220f),
            new Vector3(0f, -40f, 180f),   // En el fondo del abismo
            new Vector3(-25f, 40f, 260f),
            new Vector3(30f, 35f, 275f),
            new Vector3(0f, 55f, 265f)     // Sobre el Altar
        };

        var matEstrellaEmisiva = ObtenerOCrearMatEstrella();

        for (int i = 0; i < posicionesEstrellas.Length; i++)
        {
            var starObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            starObj.name = $"Astro_Celeste_3D_{i + 1}";
            starObj.transform.SetParent(padre);
            starObj.transform.position = posicionesEstrellas[i];
            float escala = Random.Range(3.5f, 7.5f);
            starObj.transform.localScale = Vector3.one * escala;

            var r = starObj.GetComponent<Renderer>();
            if (r) r.sharedMaterial = matEstrellaEmisiva;
            Object.DestroyImmediate(starObj.GetComponent<Collider>());

            var l = starObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.Lerp(new Color(0.8f, 0.9f, 1f), new Color(1f, 0.85f, 0.5f), (float)i / posicionesEstrellas.Length);
            l.range = escala * 8f;
            l.intensity = 2.2f;
        }

        // Constelación de Will (tres estrellas desiguales conectadas por filamentos de luz)
        var constelacionObj = new GameObject("Constelacion_Guia_Will");
        constelacionObj.transform.SetParent(padre);
        var lr = constelacionObj.AddComponent<LineRenderer>();
        lr.positionCount = 4;
        lr.useWorldSpace = true;
        lr.startWidth = 0.4f;
        lr.endWidth = 0.4f;
        lr.sharedMaterial = matOro;

        Vector3 pA = new Vector3(-30f, 48f, 90f);
        Vector3 pB = new Vector3(-12f, 56f, 115f);
        Vector3 pC = new Vector3(-42f, 42f, 130f);
        lr.SetPositions(new Vector3[] { pA, pB, pC, pA });
    }

    // ── Construcción de la Calzada ──────────────────────────────────────────

    private static void ConstruirCalzadaSendero(Transform padre, Material matCristal, Material matOro)
    {
        // Tramo 0: Llegada (Z: -12 a 25)
        CrearSuelo("Suelo_00_Llegada", padre, new Vector3(0f, -0.3f, 6.5f), new Vector3(8f, 0.6f, 37f), matCristal);

        // Tramo 1: Cielos Fracturados hacia Will (Z: 25 a 70, curva suave y subida a Y=1.5)
        CrearSuelo("Suelo_01_CaminoWill_A", padre, new Vector3(0f, 0.2f, 42f), new Vector3(7.5f, 0.6f, 34f), matCristal);
        CrearSuelo("Suelo_01_PlataformaSantuarioWill", padre, new Vector3(0f, 0.8f, 64f), new Vector3(12f, 0.6f, 14f), matCristal);

        // Tramo 2: Sendero del Corazón hacia Estela (Z: 70 a 130, se abre hacia X=6, Y=3.0)
        CrearSuelo("Suelo_02_CaminoEstela_A", padre, new Vector3(3f, 1.6f, 85f), new Vector3(7f, 0.6f, 30f), matCristal);
        CrearSuelo("Suelo_02_CaminoEstela_B", padre, new Vector3(6f, 2.4f, 110f), new Vector3(7f, 0.6f, 24f), matCristal);
        CrearSuelo("Suelo_02_PlataformaMiradorEstela", padre, new Vector3(8f, 2.8f, 128f), new Vector3(13f, 0.6f, 14f), matCristal);

        // Tramo 3: Sendero de la Sombra hacia Liam (Z: 130 a 185, gira hacia X=-3, Y=2.2)
        CrearSuelo("Suelo_03_CaminoLiam_A", padre, new Vector3(3f, 2.5f, 145f), new Vector3(7f, 0.6f, 24f), matCristal);
        CrearSuelo("Suelo_03_CaminoLiam_B", padre, new Vector3(-3f, 2.2f, 165f), new Vector3(7.5f, 0.6f, 22f), matCristal);
        CrearSuelo("Suelo_03_PlataformaCajaLiam", padre, new Vector3(-6f, 2.0f, 180f), new Vector3(12f, 0.6f, 14f), matCristal);

        // Tramo 4: Ruptura y Reconciliación (Z: 185 a 230, amplia meseta astral, Y=1.5)
        CrearSuelo("Suelo_04_PlataformaReconciliacion", padre, new Vector3(0f, 1.5f, 208f), new Vector3(16f, 0.6f, 44f), matCristal);

        // Banco de luz estelar para sentarse
        var banco = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banco.name = "Banco_Luz_Estelar";
        banco.transform.SetParent(padre);
        banco.transform.SetPositionAndRotation(new Vector3(4f, 2.2f, 208f), Quaternion.Euler(0f, -35f, 0f));
        banco.transform.localScale = new Vector3(3f, 0.6f, 1.2f);
        var rBanco = banco.GetComponent<Renderer>();
        if (rBanco) rBanco.sharedMaterial = matOro;

        // Tramo 5: Fin del Sendero (Z: 230 a 242)
        CrearSuelo("Suelo_05_CorteDelCamino", padre, new Vector3(0f, 1.8f, 236f), new Vector3(8f, 0.6f, 14f), matCristal);

        // Puente de constelaciones flotantes (5 losas de luz cristalina cruzando el vacío)
        float[] zPuente = { 245f, 247.5f, 250f, 252.5f, 255f };
        for (int i = 0; i < zPuente.Length; i++)
        {
            var losa = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            losa.name = $"Losa_Constelacion_{i + 1}";
            losa.transform.SetParent(padre);
            losa.transform.SetPositionAndRotation(new Vector3(0f, 2.0f + i * 0.1f, zPuente[i]), Quaternion.identity);
            losa.transform.localScale = new Vector3(3.2f, 0.15f, 3.2f);
            losa.layer = LayerMask.NameToLayer("Floor");
            var rL = losa.GetComponent<Renderer>();
            if (rL) rL.sharedMaterial = matOro;
        }
    }

    // ── Monumentos de Portales ──────────────────────────────────────────────

    private static GameObject ConstruirPortalTierra(Transform padre, Material matPiedra, Material matVelo)
    {
        var root = new GameObject("Portal_00_UmbralTierra");
        root.transform.SetParent(padre);
        root.transform.position = new Vector3(0f, 0f, -6f);

        // Megalitos flotantes
        var m1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m1.transform.SetParent(root.transform);
        m1.transform.SetPositionAndRotation(new Vector3(-2.8f, 2.5f, 0f), Quaternion.Euler(0f, 15f, 8f));
        m1.transform.localScale = new Vector3(1.2f, 5.5f, 1.2f);
        if (m1.GetComponent<Renderer>()) m1.GetComponent<Renderer>().sharedMaterial = matPiedra;

        var m2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m2.transform.SetParent(root.transform);
        m2.transform.SetPositionAndRotation(new Vector3(2.8f, 2.5f, 0f), Quaternion.Euler(0f, -15f, -8f));
        m2.transform.localScale = new Vector3(1.2f, 5.5f, 1.2f);
        if (m2.GetComponent<Renderer>()) m2.GetComponent<Renderer>().sharedMaterial = matPiedra;

        var dintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dintel.transform.SetParent(root.transform);
        dintel.transform.SetPositionAndRotation(new Vector3(0f, 5.2f, 0f), Quaternion.Euler(5f, 0f, 0f));
        dintel.transform.localScale = new Vector3(6.5f, 1.1f, 1.4f);
        if (dintel.GetComponent<Renderer>()) dintel.GetComponent<Renderer>().sharedMaterial = matPiedra;

        // Velo del portal
        var velo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        velo.name = "Velo_Tierra";
        velo.transform.SetParent(root.transform);
        velo.transform.SetPositionAndRotation(new Vector3(0f, 2.6f, 0f), Quaternion.identity);
        velo.transform.localScale = new Vector3(4.5f, 4.8f, 1f);
        Object.DestroyImmediate(velo.GetComponent<Collider>());
        if (velo.GetComponent<Renderer>()) velo.GetComponent<Renderer>().sharedMaterial = matVelo;

        var l = root.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.3f, 0.7f, 1f);
        l.range = 8f;
        l.intensity = 2.5f;

        return root;
    }

    private static GameObject ConstruirPortalWill(Transform padre, Material matOro, Material matVelo)
    {
        var root = new GameObject("Portal_01_Will_Memorias");
        root.transform.SetParent(padre);
        root.transform.position = new Vector3(0f, 1.1f, 65f);

        // Alas de luz dorada
        var alaIzq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        alaIzq.transform.SetParent(root.transform);
        alaIzq.transform.SetPositionAndRotation(new Vector3(-2.6f, 3.2f, 0f), Quaternion.Euler(0f, 0f, 16f));
        alaIzq.transform.localScale = new Vector3(0.5f, 3.2f, 0.5f);
        if (alaIzq.GetComponent<Renderer>()) alaIzq.GetComponent<Renderer>().sharedMaterial = matOro;

        var alaDer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        alaDer.transform.SetParent(root.transform);
        alaDer.transform.SetPositionAndRotation(new Vector3(2.6f, 3.2f, 0f), Quaternion.Euler(0f, 0f, -16f));
        alaDer.transform.localScale = new Vector3(0.5f, 3.2f, 0.5f);
        if (alaDer.GetComponent<Renderer>()) alaDer.GetComponent<Renderer>().sharedMaterial = matOro;

        // Orbe celestial superior
        var orbe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbe.transform.SetParent(root.transform);
        orbe.transform.SetPositionAndRotation(new Vector3(0f, 6.4f, 0f), Quaternion.identity);
        orbe.transform.localScale = Vector3.one * 1.4f;
        if (orbe.GetComponent<Renderer>()) orbe.GetComponent<Renderer>().sharedMaterial = matOro;

        // Velo dorado líquido
        var velo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        velo.name = "Velo_Will";
        velo.transform.SetParent(root.transform);
        velo.transform.SetPositionAndRotation(new Vector3(0f, 3.2f, 0f), Quaternion.identity);
        velo.transform.localScale = new Vector3(4.2f, 5.2f, 1f);
        Object.DestroyImmediate(velo.GetComponent<Collider>());
        if (velo.GetComponent<Renderer>()) velo.GetComponent<Renderer>().sharedMaterial = matVelo;

        var l = root.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.82f, 0.4f);
        l.range = 10f;
        l.intensity = 3.2f;

        return root;
    }

    private static GameObject ConstruirPortalEstela(Transform padre, Material matCuarzo, Material matVelo)
    {
        var root = new GameObject("Portal_02_Estela_Corazon");
        root.transform.SetParent(padre);
        root.transform.position = new Vector3(8f, 3.1f, 128f);

        // Arco de cuarzo en forma de cerradura/corazón estilizado
        var pilarIzq = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarIzq.transform.SetParent(root.transform);
        pilarIzq.transform.SetPositionAndRotation(new Vector3(-2.4f, 3.0f, 0f), Quaternion.Euler(0f, 0f, 10f));
        pilarIzq.transform.localScale = new Vector3(0.9f, 5.5f, 0.9f);
        if (pilarIzq.GetComponent<Renderer>()) pilarIzq.GetComponent<Renderer>().sharedMaterial = matCuarzo;

        var pilarDer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilarDer.transform.SetParent(root.transform);
        pilarDer.transform.SetPositionAndRotation(new Vector3(2.4f, 3.0f, 0f), Quaternion.Euler(0f, 0f, -10f));
        pilarDer.transform.localScale = new Vector3(0.9f, 5.5f, 0.9f);
        if (pilarDer.GetComponent<Renderer>()) pilarDer.GetComponent<Renderer>().sharedMaterial = matCuarzo;

        // Cinta roja danzante de Mara
        var cinta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cinta.name = "Cinta_Roja_Mara";
        cinta.transform.SetParent(root.transform);
        cinta.transform.SetPositionAndRotation(new Vector3(0f, 5.8f, 0f), Quaternion.Euler(0f, 0f, 90f));
        cinta.transform.localScale = new Vector3(0.35f, 2.5f, 0.35f);
        var rC = cinta.GetComponent<Renderer>();
        if (rC) rC.sharedMaterial = ObtenerOCrearMatCintaRoja();

        // Velo rosa/fuego
        var velo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        velo.name = "Velo_Estela";
        velo.transform.SetParent(root.transform);
        velo.transform.SetPositionAndRotation(new Vector3(0f, 3.0f, 0f), Quaternion.identity);
        velo.transform.localScale = new Vector3(4.2f, 5.0f, 1f);
        Object.DestroyImmediate(velo.GetComponent<Collider>());
        if (velo.GetComponent<Renderer>()) velo.GetComponent<Renderer>().sharedMaterial = matVelo;

        var l = root.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.35f, 0.65f);
        l.range = 10f;
        l.intensity = 3.2f;

        return root;
    }

    private static GameObject ConstruirPortalLiam(Transform padre, Material matObsidiana, Material matMadera, Material matVelo)
    {
        var root = new GameObject("Portal_03_Liam_LaCaja");
        root.transform.SetParent(padre);
        root.transform.position = new Vector3(-6f, 2.3f, 180f);

        // Pedestal de obsidiana
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal_Obsidiana";
        pedestal.transform.SetParent(root.transform);
        pedestal.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.identity);
        pedestal.transform.localScale = new Vector3(2.4f, 0.5f, 2.4f);
        if (pedestal.GetComponent<Renderer>()) pedestal.GetComponent<Renderer>().sharedMaterial = matObsidiana;

        // La Caja misteriosa sobre el pedestal
        var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        caja.name = "La_Caja_Misteriosa";
        caja.transform.SetParent(root.transform);
        caja.transform.SetPositionAndRotation(new Vector3(0f, 1.3f, 0f), Quaternion.identity);
        caja.transform.localScale = new Vector3(1.4f, 0.9f, 1.1f);
        if (caja.GetComponent<Renderer>()) caja.GetComponent<Renderer>().sharedMaterial = matMadera;

        // Marco del espejo gótico agrietado que surge de la caja
        var marcoIzq = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marcoIzq.transform.SetParent(root.transform);
        marcoIzq.transform.SetPositionAndRotation(new Vector3(-2.0f, 4.2f, 0f), Quaternion.identity);
        marcoIzq.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
        if (marcoIzq.GetComponent<Renderer>()) marcoIzq.GetComponent<Renderer>().sharedMaterial = matObsidiana;

        var marcoDer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marcoDer.transform.SetParent(root.transform);
        marcoDer.transform.SetPositionAndRotation(new Vector3(2.0f, 4.2f, 0f), Quaternion.identity);
        marcoDer.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
        if (marcoDer.GetComponent<Renderer>()) marcoDer.GetComponent<Renderer>().sharedMaterial = matObsidiana;

        // Velo de espejo frío
        var velo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        velo.name = "Velo_Espejo_Liam";
        velo.transform.SetParent(root.transform);
        velo.transform.SetPositionAndRotation(new Vector3(0f, 4.2f, 0f), Quaternion.identity);
        velo.transform.localScale = new Vector3(3.8f, 5.5f, 1f);
        Object.DestroyImmediate(velo.GetComponent<Collider>());
        if (velo.GetComponent<Renderer>()) velo.GetComponent<Renderer>().sharedMaterial = matVelo;

        var l = root.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.6f, 0.8f, 1.2f);
        l.range = 9f;
        l.intensity = 2.8f;

        return root;
    }

    // ── Plaza del Altar de Piedra Blanca ────────────────────────────────────

    private static GameObject ConstruirPlazaAltar(Transform padre, Material matAltar, Material matOro, Material matObsidiana)
    {
        var plazaRoot = new GameObject("Plaza_Circular_Altar");
        plazaRoot.transform.SetParent(padre);
        plazaRoot.transform.position = new Vector3(0f, 2.3f, 266f);

        // Gran plataforma circular (diámetro 24m)
        var plataforma = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plataforma.name = "Plataforma_Altar";
        plataforma.transform.SetParent(plazaRoot.transform);
        plataforma.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0f), Quaternion.identity);
        plataforma.transform.localScale = new Vector3(24f, 0.5f, 24f);
        plataforma.layer = LayerMask.NameToLayer("Floor");
        if (plataforma.GetComponent<Renderer>()) plataforma.GetComponent<Renderer>().sharedMaterial = matAltar;

        // Anillos celestes concéntricos
        var anillo1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anillo1.name = "Anillo_Celeste_Interior";
        anillo1.transform.SetParent(plazaRoot.transform);
        anillo1.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        anillo1.transform.localScale = new Vector3(16f, 0.45f, 16f);
        anillo1.layer = LayerMask.NameToLayer("Floor");
        if (anillo1.GetComponent<Renderer>()) anillo1.GetComponent<Renderer>().sharedMaterial = matOro;

        // Monolito sagrado del Altar Blanco (en el centro, Z=266)
        var altar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        altar.name = "Altar_De_Piedra_Blanca";
        altar.transform.SetParent(plazaRoot.transform);
        altar.transform.SetPositionAndRotation(new Vector3(0f, 1.4f, 2.5f), Quaternion.identity);
        altar.transform.localScale = new Vector3(2.4f, 1.8f, 1.6f);
        if (altar.GetComponent<Renderer>()) altar.GetComponent<Renderer>().sharedMaterial = matAltar;

        // Luz pulsante del Altar que respira
        var luzAltar = altar.AddComponent<Light>();
        luzAltar.type = LightType.Point;
        luzAltar.color = new Color(1f, 0.98f, 0.92f);
        luzAltar.range = 14f;
        luzAltar.intensity = 3.5f;

        // Punto de rasgado del aire (donde emerge el Mago Oscuro en Z=260)
        var spawnerMago = new GameObject("Punto_Rasgado_Aire_MagoOscuro");
        spawnerMago.transform.SetParent(plazaRoot.transform);
        spawnerMago.transform.position = new Vector3(0f, 1.2f, -1.0f);

        return altar;
    }

    // ── Triggers de Progresión ──────────────────────────────────────────────

    private static void ConstruirTriggers(Transform padre)
    {
        var raizTriggers = new GameObject("TRIGGERS_DE_PROGRESION");
        raizTriggers.transform.SetParent(padre);

        // 1. Manifestar y entrar Will
        CrearTrigger("Trigger_01_Manifestar_Will", raizTriggers.transform, new Vector3(0f, 1.5f, 48f), new Vector3(10f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.ManifestarPortalWill);
        CrearTrigger("Trigger_01_Entrar_Will", raizTriggers.transform, new Vector3(0f, 2.5f, 64f), new Vector3(4f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.EntrarPruebaWill);

        // 2. Manifestar y entrar Estela
        CrearTrigger("Trigger_02_Manifestar_Estela", raizTriggers.transform, new Vector3(6f, 3.5f, 112f), new Vector3(10f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.ManifestarPortalEstela);
        CrearTrigger("Trigger_02_Entrar_Estela", raizTriggers.transform, new Vector3(8f, 4.0f, 127f), new Vector3(4f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.EntrarPruebaEstela);

        // 3. Manifestar y entrar Liam
        CrearTrigger("Trigger_03_Manifestar_Liam", raizTriggers.transform, new Vector3(-3f, 3.2f, 168f), new Vector3(10f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.ManifestarPortalLiam);
        CrearTrigger("Trigger_03_Entrar_Liam", raizTriggers.transform, new Vector3(-6f, 3.2f, 179f), new Vector3(4f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.EntrarPruebaLiam);

        // 4. Salida de la meseta de reconciliación hacia el Altar
        CrearTrigger("Trigger_04_Avanzar_Altar", raizTriggers.transform, new Vector3(0f, 2.5f, 226f), new Vector3(10f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.AvanzarHaciaAltar);

        // 5. Llegada frente al Altar (Rasgar el aire y Mago Oscuro)
        CrearTrigger("Trigger_05_Aparicion_MagoOscuro", raizTriggers.transform, new Vector3(0f, 3.2f, 260f), new Vector3(8f, 4f, 3f), SenderoTriggerArea.TipoAccionTrigger.DetonarAparicionMagoOscuro);
    }

    private static void CrearTrigger(string nombre, Transform padre, Vector3 pos, Vector3 tam, SenderoTriggerArea.TipoAccionTrigger accion)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre);
        go.transform.position = pos;

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = tam;

        var trigger = go.AddComponent<SenderoTriggerArea>();
        var so = new SerializedObject(trigger);
        so.FindProperty("_accion").enumValueIndex = (int)accion;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CrearSuelo(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = nombre;
        obj.transform.SetParent(padre);
        obj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        obj.transform.localScale = escala;
        obj.layer = LayerMask.NameToLayer("Floor");
        var r = obj.GetComponent<Renderer>();
        if (r && mat) r.sharedMaterial = mat;
        return obj;
    }

    // ── Creación de Materiales de Autor ─────────────────────────────────────

    private static Material ObtenerOCrearMatCosmos()
    {
        string ruta = $"{FolderMateriales}/Mat_CosmosProfundo360.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var shader = Shader.Find("El Sendero/CosmosProfundo360");
        if (shader == null) shader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Standard");

        mat = new Material(shader) { name = "Mat_CosmosProfundo360" };
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatCristal()
    {
        string ruta = $"{FolderMateriales}/Mat_Sendero_CristalAstral.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var shader = Shader.Find("El Sendero/CristalAstral") ?? Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(shader) { name = "Mat_Sendero_CristalAstral" };
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatPortal(string nombre, Color core, Color outer, Color rim)
    {
        string ruta = $"{FolderMateriales}/{nombre}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var shader = Shader.Find("El Sendero/PortalCelestialVelo") ?? Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(shader) { name = nombre };
        if (mat.HasProperty("_CoreColor")) mat.SetColor("_CoreColor", core);
        if (mat.HasProperty("_OuterColor")) mat.SetColor("_OuterColor", outer);
        if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", rim);

        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatAltar()
    {
        string ruta = $"{FolderMateriales}/Mat_AltarBlancoPuro.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(s) { name = "Mat_AltarBlancoPuro" };
        mat.color = new Color(0.96f, 0.95f, 0.93f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.35f, 0.33f, 0.38f));
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatEstrella()
    {
        string ruta = $"{FolderMateriales}/Mat_Estrella_Emisiva.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(s) { name = "Mat_Estrella_Emisiva" };
        mat.color = new Color(1.8f, 1.6f, 1.2f, 1f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatOroAstral()
    {
        string ruta = $"{FolderMateriales}/Mat_OroAstral.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(s) { name = "Mat_OroAstral" };
        mat.color = new Color(0.92f, 0.72f, 0.25f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0.1f));
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatPiedraMegalito()
    {
        string ruta = $"{FolderMateriales}/Mat_PiedraMegalito.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(s) { name = "Mat_PiedraMegalito" };
        mat.color = new Color(0.38f, 0.42f, 0.52f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatObsidiana()
    {
        string ruta = $"{FolderMateriales}/Mat_ObsidianaAstral.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(s) { name = "Mat_ObsidianaAstral" };
        mat.color = new Color(0.12f, 0.12f, 0.18f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatMaderaCaja()
    {
        string ruta = $"{FolderMateriales}/Mat_MaderaCajaMisteriosa.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(s) { name = "Mat_MaderaCajaMisteriosa" };
        mat.color = new Color(0.24f, 0.16f, 0.12f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static Material ObtenerOCrearMatCintaRoja()
    {
        string ruta = $"{FolderMateriales}/Mat_CintaRoja_Mara.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;

        var s = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(s) { name = "Mat_CintaRoja_Mara" };
        mat.color = new Color(0.95f, 0.18f, 0.25f, 1f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
