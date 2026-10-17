#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor para construir la escena final de previsualización y rodaje de la
/// Cinemática Final (<c>CinematicaFinal_Preview.unity</c>) con los tres escenarios de calidad
/// final (Santuario Astral del Sendero, Recinto de la Piedra Ancestral copiado de MainWorld,
/// y Ribera de la Laguna/Río copiada de MainWorld sin modificar MainWorld) y hornear sus dos
/// guiones (<c>Final_01_ElSacrificio</c> y <c>Final_02_ElRio</c>).
/// </summary>
public static class MontarCinematicaFinal
{
    private const string RutaEscenaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string RutaEscenaSendero = "Assets/Scenes/Worlds/Sendero.unity";
    private const string RutaEscenaPreview = "Assets/Scenes/Test/CinematicaFinal_Preview.unity";

    private const string RutaSeqParte1 = "Assets/_SEQUENCES/SEQ_Final_01_ElSacrificio.asset";
    private const string RutaSeqParte2 = "Assets/_SEQUENCES/SEQ_Final_02_ElRio.asset";

    public const string RutaGuionParte1 = "Assets/_SEQUENCES/Guiones/Final_01_ElSacrificio.guion.txt";
    public const string RutaGuionParte2 = "Assets/_SEQUENCES/Guiones/Final_02_ElRio.guion.txt";

    private const string CarpetaMallasGeneradas = "Assets/Scenes/Test/CinematicaFinal_Data";

    // Centros de cada zona en CinematicaFinal_Preview.unity
    public static readonly Vector3 CentroZonaSendero = new(0f, 0f, 0f);
    public static readonly Vector3 CentroZonaPiedra = new(200f, 0f, 0f);
    public static readonly Vector3 CentroZonaRio = new(400f, 0f, 0f);

    // Centros originales en MainWorld.unity
    private static readonly Vector3 OrigenMainWorldPiedra = new(565f, 18.6f, -505f);
    private static readonly Vector3 OrigenMainWorldRio = new(162f, 24.5f, -22f);

    [MenuItem("El Sendero/Cinemáticas/1. Montar Escena de Cinemática Final (Escenarios Finales)", priority = 20)]
    [MenuItem("El Sendero/Cinematicas/MontarEscenaFinal", priority = 21)]
    public static void MenuMontarEscena()
    {
        try
        {
            ConstruirEscenaCompleta();
            Debug.Log("[Cinemática Final] Escena montada con éxito en " + RutaEscenaPreview);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }


    /// <summary>
    /// Método invocado automáticamente por <see cref="HorneadorDeGuion"/> mediante la directiva
    /// <c>PREPARA MontarCinematicaFinal</c> antes de hornear los guiones.
    /// </summary>
    public static void Asegurar()
    {
        PrepararEscena(forzar: true);
    }

    public static void PrepararEscena(bool forzar = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Cinemática Final] No se puede montar la escena en PlayMode.");
            return;
        }

        var escenaActiva = SceneManager.GetActiveScene();
        var actorWill = GameObject.Find("Actor_Will");
        if (!forzar &&
            escenaActiva.path == RutaEscenaPreview &&
            GameObject.Find("CINEMATICA_FINAL_ROOT") != null &&
            actorWill != null &&
            actorWill.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
        {
            AsegurarDefinicionesYReferencias();
            return;
        }

        ConstruirEscenaCompleta();
    }

    public static void HornearGuiones()
    {
        PrepararEscena();

        Debug.Log("[Cinemática Final] Horneando Parte 1: El Sacrificio...");
        var h1 = HorneadorDeGuion.Hornear(RutaGuionParte1, storyboard: false);

        Debug.Log("[Cinemática Final] Horneando Parte 2: El Río con Tobías...");
        var h2 = HorneadorDeGuion.Hornear(RutaGuionParte2, storyboard: false);

        if (h1 != null && h2 != null)
        {
            Debug.Log("[Cinemática Final] ¡Ambos guiones horneados con éxito!");
        }
    }

    public static void ConstruirEscenaCompleta()
    {
        if (!Directory.Exists(CarpetaMallasGeneradas))
        {
            Directory.CreateDirectory(CarpetaMallasGeneradas);
            AssetDatabase.Refresh();
        }

        Scene escenaPreview = SceneManager.GetActiveScene();
        if (escenaPreview.IsValid() && escenaPreview.path == RutaEscenaPreview)
        {
            foreach (var rootGo in escenaPreview.GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(rootGo);
        }
        else
        {
            escenaPreview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(escenaPreview);
        }

        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer < 0) floorLayer = 0;

        // 1. Raíz de iluminación y cámara
        var lightingRoot = new GameObject("LIGHTING_AND_CAMERA");
        var sunGo = new GameObject("Directional Light");
        sunGo.transform.SetParent(lightingRoot.transform, false);
        sunGo.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        var sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(0.75f, 0.82f, 1.0f);
        sunLight.intensity = 1.25f;
        sunLight.shadows = LightShadows.Soft;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(lightingRoot.transform, false);
        camGo.transform.position = new Vector3(0f, 2.5f, -6f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.15f;
        cam.farClipPlane = 600f;
        cam.fieldOfView = 45f;
        var uac = camGo.AddComponent<UniversalAdditionalCameraData>();
        uac.renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();

        RenderSettings.skybox = ObtenerOCrearSkyboxAstralSendero();
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;

        // 2. Construir Zona 1: Santuario Astral de El Sendero (Altar y Umbral)
        var rootZona1 = ConstruirZona1Sendero(escenaPreview, floorLayer, out Transform altarTr, out Transform portalTr, out GameObject estrellasNocturnasRoot, out GameObject estrellaTorcidaGo);

        // 3. Construir Zona 2 (Piedra Ancestral) y Zona 3 (Río / Laguna) copiando de MainWorld sin tocar MainWorld
        var rootZona2 = new GameObject("ZONA_2_PIEDRA_ANCESTRAL");
        rootZona2.transform.position = CentroZonaPiedra;

        var rootZona3 = new GameObject("ZONA_3_EL_RIO");
        rootZona3.transform.position = CentroZonaRio;

        CopiarZonasDesdeMainWorld(escenaPreview, rootZona2.transform, rootZona3.transform, floorLayer, out Transform piedraTr, out Transform orillaAguaTr);
        LimpiarCollidersYTriggersDecorativos(rootZona2);
        LimpiarCollidersYTriggersDecorativos(rootZona3);

        // 4. NavMesh para todas las zonas (solo sobre geometría de suelo en capa Floor)
        var navGo = new GameObject("NAVMESH_CINEMATICA_FINAL");
        var navSurface = navGo.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navSurface.layerMask = 1 << floorLayer;
        navSurface.BuildNavMesh();

        // 5. Instanciar actores y efectos
        var rootActores = new GameObject("ACTORES_CINEMATICA_FINAL");
        InstanciarActoresYProps(
            rootActores.transform,
            out GameObject willGo,
            out GameObject estelaGo,
            out GameObject liamGo,
            out GameObject archimagoGo,
            out GameObject tobiasGo,
            out GameObject cintaRojaWill,
            out GameObject canaWill,
            out GameObject canaTobias);

        // 6. Instanciar VFX de apoyo
        var rootVfx = new GameObject("VFX_CINEMATICA_FINAL");
        InstanciarEfectosEscena(
            rootVfx.transform,
            altarTr,
            portalTr,
            out GameObject vfxDeseoAltar,
            out GameObject vfxPortal,
            out GameObject vfxEmpujeLuz,
            out GameObject vfxCirculoConjuro,
            out GameObject vfxAuraConjuroWill,
            out GameObject vfxPilarResurreccion,
            out GameObject vfxResurreccion,
            out GameObject vfxColapsoSendero,
            out GameObject vfxEspirituArchimago,
            out GameObject vfxLlamaWill,
            out GameObject vfxSalpicaduraRio);

        // 7. Crear/actualizar assets de SequenceDefinition y configurar SequencePlayer + SequenceStage + ModuloCinematicaFinal
        var seq1 = CrearOActualizarSequenceDefinition(RutaSeqParte1, "Final — Parte 1: El Sacrificio", "FINAL_PARTE_01_START", "FINAL_PARTE_02_START");
        var seq2 = CrearOActualizarSequenceDefinition(RutaSeqParte2, "Final — Parte 2: El Río con Tobías", "FINAL_PARTE_02_START", "FINAL_CREDITOS_START");

        var rootCinematica = new GameObject("CINEMATICA_FINAL_ROOT");
        var stage = rootCinematica.AddComponent<SequenceStage>();
        var player1 = rootCinematica.AddComponent<SequencePlayer>();
        var camDriver = rootCinematica.AddComponent<CinematicCameraDriver>();
        var modulo = rootCinematica.AddComponent<ModuloCinematicaFinal>();
        var autoStart = rootCinematica.AddComponent<SenderoBattleAutoStart>();

        // Segundo SequencePlayer en hijo para que HorneadorDeGuion encuentre también SEQ_Final_02_ElRio por Definition
        var goPlayer2 = new GameObject("SequencePlayer_Parte2");
        goPlayer2.transform.SetParent(rootCinematica.transform, false);
        var player2 = goPlayer2.AddComponent<SequencePlayer>();

        ConfigurarComponentesCinematica(
            stage,
            player1,
            player2,
            camDriver,
            modulo,
            autoStart,
            cam,
            sunLight,
            seq1,
            seq2,
            willGo.transform,
            estelaGo.transform,
            liamGo.transform,
            archimagoGo.transform,
            tobiasGo.transform,
            altarTr,
            portalTr,
            piedraTr,
            orillaAguaTr,
            vfxDeseoAltar,
            vfxPortal,
            vfxEmpujeLuz,
            vfxCirculoConjuro,
            vfxAuraConjuroWill,
            vfxPilarResurreccion,
            vfxResurreccion,
            vfxColapsoSendero,
            vfxEspirituArchimago,
            vfxLlamaWill,
            cintaRojaWill,
            canaWill,
            canaTobias,
            vfxSalpicaduraRio,
            estrellaTorcidaGo,
            estrellasNocturnasRoot);

        EditorSceneManager.MarkSceneDirty(escenaPreview);
        EditorSceneManager.SaveScene(escenaPreview, RutaEscenaPreview);
        AssetDatabase.SaveAssets();
    }

    private static void AsegurarDefinicionesYReferencias()
    {
        var seq1 = CrearOActualizarSequenceDefinition(RutaSeqParte1, "Final — Parte 1: El Sacrificio", "FINAL_PARTE_01_START", "FINAL_PARTE_02_START");
        var seq2 = CrearOActualizarSequenceDefinition(RutaSeqParte2, "Final — Parte 2: El Río con Tobías", "FINAL_PARTE_02_START", "FINAL_CREDITOS_START");

        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer < 0) floorLayer = 0;

        var zona1 = GameObject.Find("ZONA_1_EL_SENDERO");
        if (zona1 != null) LimpiarCollidersYTriggersDecorativos(zona1);

        var zona2 = GameObject.Find("ZONA_2_PIEDRA_ANCESTRAL");
        if (zona2 != null) LimpiarCollidersYTriggersDecorativos(zona2);

        var zona3 = GameObject.Find("ZONA_3_EL_RIO");
        if (zona3 != null) LimpiarCollidersYTriggersDecorativos(zona3);

        var navSurface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
        if (navSurface != null && navSurface.layerMask != (1 << floorLayer))
        {
            navSurface.layerMask = 1 << floorLayer;
            navSurface.BuildNavMesh();
        }

        var root = GameObject.Find("CINEMATICA_FINAL_ROOT");
        if (root == null) return;

        var stage = root.GetComponent<SequenceStage>();
        var player1 = root.GetComponent<SequencePlayer>();
        var player2Tr = root.transform.Find("SequencePlayer_Parte2");
        var player2 = player2Tr != null ? player2Tr.GetComponent<SequencePlayer>() : null;

        if (player1 != null)
        {
            var sp1 = new SerializedObject(player1);
            sp1.FindProperty("_definition").objectReferenceValue = seq1;
            sp1.FindProperty("_stage").objectReferenceValue = stage;
            sp1.FindProperty("_signalIn").stringValue = "FINAL_PARTE_01_START";
            sp1.FindProperty("_signalOut").stringValue = "FINAL_PARTE_02_START";
            sp1.ApplyModifiedPropertiesWithoutUndo();
        }

        if (player2 != null)
        {
            var sp2 = new SerializedObject(player2);
            sp2.FindProperty("_definition").objectReferenceValue = seq2;
            sp2.FindProperty("_stage").objectReferenceValue = stage;
            sp2.FindProperty("_signalIn").stringValue = "FINAL_PARTE_02_START";
            sp2.FindProperty("_signalOut").stringValue = "FINAL_CREDITOS_START";
            sp2.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static SequenceDefinition CrearOActualizarSequenceDefinition(string ruta, string displayName, string signalIn, string signalOut)
    {
        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(ruta);
        if (seq == null)
        {
            seq = ScriptableObject.CreateInstance<SequenceDefinition>();
            AssetDatabase.CreateAsset(seq, ruta);
        }

        seq.displayName = displayName;
        seq.signalIn = signalIn;
        seq.signalOut = signalOut;
        seq.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        EditorUtility.SetDirty(seq);
        return seq;
    }

    private static GameObject ConstruirZona1Sendero(
        Scene escenaDestino,
        int floorLayer,
        out Transform altarTransform,
        out Transform portalTransform,
        out GameObject estrellasNocturnasRoot,
        out GameObject estrellaTorcidaGo)
    {
        var root = new GameObject("ZONA_1_EL_SENDERO");
        root.transform.position = CentroZonaSendero;

        altarTransform = root.transform;
        portalTransform = root.transform;

        if (File.Exists(RutaEscenaSendero))
        {
            var escenaSendero = EditorSceneManager.OpenScene(RutaEscenaSendero, OpenSceneMode.Additive);
            try
            {
                GameObject worldSendero = null;
                foreach (var go in escenaSendero.GetRootGameObjects())
                {
                    if (go.name == "SENDERO_WORLD")
                    {
                        worldSendero = go;
                        break;
                    }
                }

                if (worldSendero != null)
                {
                    // 1. Copiar la Plaza del Altar Blanco con su suelo circular cósmico auténtico
                    var plaza = worldSendero.transform.Find("PLAZA_ALTAR_BLANCO");
                    if (plaza != null)
                    {
                        var copiaPlaza = UnityEngine.Object.Instantiate(plaza.gameObject, root.transform);
                        copiaPlaza.name = "PLAZA_ALTAR_BLANCO";
                        copiaPlaza.transform.localPosition = Vector3.zero;
                        copiaPlaza.transform.localRotation = Quaternion.identity;

                        var plazaCirc = copiaPlaza.transform.Find("Plaza_Circular_Altar");
                        if (plazaCirc != null)
                        {
                            var altar = plazaCirc.Find("Altar_De_Piedra_Blanca");
                            if (altar != null) altarTransform = altar;
                            else altarTransform = plazaCirc;
                        }
                    }

                    // 2. Copiar el Umbral de salida (Portal_00_UmbralTierra) en Z=9.5
                    var portal = worldSendero.transform.Find("PORTALES_CELESTIALES/Portal_00_UmbralTierra");
                    if (portal != null)
                    {
                        var copiaPortal = UnityEngine.Object.Instantiate(portal.gameObject, root.transform);
                        copiaPortal.name = "Umbral_Salida_Sendero";
                        copiaPortal.transform.localPosition = new Vector3(0f, 0f, 9.5f);
                        copiaPortal.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        portalTransform = copiaPortal.transform;
                    }

                    // 3. Copiar el entorno cósmico de estrellas 3D
                    var cosmicEnv = worldSendero.transform.Find("COSMIC_ENVIRONMENT_3D");
                    if (cosmicEnv != null)
                    {
                        var copiaCosmic = UnityEngine.Object.Instantiate(cosmicEnv.gameObject, root.transform);
                        copiaCosmic.name = "COSMIC_ENVIRONMENT_3D";
                        copiaCosmic.transform.localPosition = Vector3.zero;
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(escenaSendero, true);
                SceneManager.SetActiveScene(escenaDestino);
            }
        }

        estrellasNocturnasRoot = new GameObject("Estrellas_Nocturnas");
        estrellasNocturnasRoot.transform.SetParent(root.transform, false);

        // Domo de estrellas 3D alrededor del Santuario del Sendero para inmersión astral
        CrearCampoEstelarEnZona(root.transform, "Campo_Estelar_Sendero", new Vector3(0f, 0f, 4.5f), 65f, 95);

        // Domo de estrellas nocturnas en Zona 3 (El Río) y la estrella de madera de 5 puntas ligeramente torcida
        CrearCampoEstelarEnZona(estrellasNocturnasRoot.transform, "Campo_Estelar_Rio", CentroZonaRio, 85f, 110);
        estrellaTorcidaGo = CrearEstrellaDeMaderaTorcida(estrellasNocturnasRoot.transform, CentroZonaRio + new Vector3(0f, 26f, 28f));
        estrellaTorcidaGo.SetActive(false);

        LimpiarCollidersYTriggersDecorativos(root);
        AsignarCapaSueloRecursivo(root.transform, floorLayer);

        return root;
    }

    private static void CrearCampoEstelarEnZona(Transform parent, string nombre, Vector3 centro, float radio, int cantidad)
    {
        var domoRoot = new GameObject(nombre);
        domoRoot.transform.SetParent(parent, false);
        domoRoot.transform.position = centro;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var matDorada = new Material(sh) { color = new Color(1.0f, 0.93f, 0.68f, 1.0f) };
        var matCeleste = new Material(sh) { color = new Color(0.68f, 0.86f, 1.0f, 1.0f) };

        UnityEngine.Random.InitState(19970824 + nombre.GetHashCode());
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            if (dir.y < -0.15f) dir.y = Mathf.Abs(dir.y); // Concentradas en el hemisferio superior y horizonte
            dir.Normalize();

            var estrella = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            estrella.name = $"Estrella_{i}";
            UnityEngine.Object.DestroyImmediate(estrella.GetComponent<Collider>());
            estrella.transform.SetParent(domoRoot.transform, false);
            float dist = radio * UnityEngine.Random.Range(0.75f, 1.15f);
            estrella.transform.localPosition = dir * dist;
            float escala = UnityEngine.Random.Range(0.14f, 0.36f);
            estrella.transform.localScale = Vector3.one * escala;

            var mr = estrella.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = (i % 4 == 0) ? matCeleste : matDorada;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    private static void LimpiarCollidersYTriggersDecorativos(GameObject zonaRoot)
    {
        if (zonaRoot == null) return;

        // 1. Eliminar primero componentes interactivos/triggers para evitar warnings de diagnóstico
        var mbs = zonaRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = mbs.Length - 1; i >= 0; i--)
        {
            var mb = mbs[i];
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (tn.Contains("Portal") || tn.Contains("Trigger") || tn.Contains("Interact") || tn.Contains("Chest") || tn.Contains("Pickup"))
            {
                UnityEngine.Object.DestroyImmediate(mb);
            }
        }

        // 2. Desactivar colliders que no sean del suelo de navegación
        foreach (var col in zonaRoot.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            string n = col.gameObject.name;
            if (n.Contains("Plataforma") || n.Contains("Anillo") || n.StartsWith("Terreno_") || n.Contains("Camino") || n.Contains("Floor") || n.Contains("Suelo"))
                continue;

            col.enabled = false;
            if (col.gameObject.layer != 0)
                col.gameObject.layer = 0;
        }
    }

    private static GameObject CrearEstrellaDeMaderaTorcida(Transform parent, Vector3 worldPos)
    {
        var starRoot = new GameObject("Estrella_Torcida_Will");
        starRoot.transform.SetParent(parent, true);
        starRoot.transform.position = worldPos;
        starRoot.transform.rotation = Quaternion.Euler(-22f, 180f, 11f); // Ligeramente torcida, como la talló Will

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var matBrillo = new Material(sh);
        matBrillo.color = new Color(1.0f, 0.94f, 0.68f, 1.0f);

        // 5 puntas de la estrella
        for (int i = 0; i < 5; i++)
        {
            var punta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            punta.name = $"Punta_{i}";
            UnityEngine.Object.DestroyImmediate(punta.GetComponent<Collider>());
            punta.transform.SetParent(starRoot.transform, false);
            float ang = i * 72f + (i == 2 ? 9f : 0f); // Una punta ligeramente torcida
            punta.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            punta.transform.localPosition = Vector3.zero;
            punta.transform.localScale = new Vector3(0.32f, 2.2f, 0.18f);
            punta.GetComponent<MeshRenderer>().sharedMaterial = matBrillo;
        }

        var luzEstrella = new GameObject("Luz_Estrella_Will");
        luzEstrella.transform.SetParent(starRoot.transform, false);
        var light = luzEstrella.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.92f, 0.62f);
        light.intensity = 10f;
        light.range = 35f;

        return starRoot;
    }

    private static void CopiarZonasDesdeMainWorld(
        Scene escenaDestino,
        Transform rootZonaPiedra,
        Transform rootZonaRio,
        int floorLayer,
        out Transform piedraAncestralTransform,
        out Transform orillaAguaTransform)
    {
        piedraAncestralTransform = rootZonaPiedra;
        orillaAguaTransform = rootZonaRio;

        if (!File.Exists(RutaEscenaMainWorld))
        {
            Debug.LogWarning("[Cinemática Final] No se encontró MainWorld.unity para copiar las zonas.");
            return;
        }

        var escenaMain = EditorSceneManager.OpenScene(RutaEscenaMainWorld, OpenSceneMode.Additive);
        try
        {
            Terrain terreno = null;
            GameObject worldRoot = null;

            foreach (var go in escenaMain.GetRootGameObjects())
            {
                if (terreno == null)
                    terreno = go.GetComponentInChildren<Terrain>(true);
                if (go.name == "WORLD")
                    worldRoot = go;
            }

            // 1. Generar mallas de terreno fieles a MainWorld para ambas zonas
            if (terreno != null)
            {
                CrearMallaTerrenoZona(
                    terreno,
                    OrigenMainWorldPiedra,
                    84f,
                    72,
                    rootZonaPiedra,
                    floorLayer,
                    $"{CarpetaMallasGeneradas}/Terreno_PiedraAncestral.asset");

                CrearMallaTerrenoZona(
                    terreno,
                    OrigenMainWorldRio,
                    96f,
                    80,
                    rootZonaRio,
                    floorLayer,
                    $"{CarpetaMallasGeneradas}/Terreno_RiberaRio.asset");
            }

            // 2. Copiar jerarquías y objetos de vestimenta de MainWorld sin modificar MainWorld
            if (worldRoot != null)
            {
                // 2A. Santuario de la Piedra Ancestral
                var santuario = worldRoot.transform.Find("Hitos del mapa — faro, velero, puentes, cascada/Santuario ancestral — recinto de piedra y ruinas");
                if (santuario != null)
                {
                    var copiaSantuario = UnityEngine.Object.Instantiate(santuario.gameObject, rootZonaPiedra);
                    copiaSantuario.name = "Santuario_Piedra_Ancestral";
                    copiaSantuario.transform.position = santuario.position - OrigenMainWorldPiedra + CentroZonaPiedra;

                    var monolito = copiaSantuario.transform.Find("Piedra ancestral — monolito");
                    if (monolito != null)
                        piedraAncestralTransform = monolito;
                }

                // Árboles y rocas alrededor de la Piedra Ancestral (radio 42 m)
                CopiarObjetosCercanosDeMainWorld(
                    worldRoot.transform,
                    OrigenMainWorldPiedra,
                    42f,
                    CentroZonaPiedra,
                    rootZonaPiedra,
                    excluirSubarbol: santuario);

                // 2B. Laguna de la Era / Río y vegetación de ribera
                var aguaLaguna = worldRoot.transform.Find("Vestido del mundo (generado)/Agua — Laguna de la Era");
                if (aguaLaguna != null)
                {
                    var copiaAgua = UnityEngine.Object.Instantiate(aguaLaguna.gameObject, rootZonaRio);
                    copiaAgua.name = "Agua_Y_Ribera_Del_Rio";
                    copiaAgua.transform.position = aguaLaguna.position - OrigenMainWorldRio + CentroZonaRio;
                }

                // Árboles, piedras, setas y flores alrededor de la orilla (radio 48 m)
                CopiarObjetosCercanosDeMainWorld(
                    worldRoot.transform,
                    OrigenMainWorldRio,
                    48f,
                    CentroZonaRio,
                    rootZonaRio,
                    excluirSubarbol: aguaLaguna);
            }

            var marcadorOrilla = new GameObject("Punto_Agua_Salpicadura_Estela");
            marcadorOrilla.transform.SetParent(rootZonaRio, false);
            marcadorOrilla.transform.position = CentroZonaRio + new Vector3(4.2f, -0.35f, -3.5f);
            orillaAguaTransform = marcadorOrilla.transform;

            LimpiarCollidersYTriggersDecorativos(rootZonaPiedra.gameObject);
            LimpiarCollidersYTriggersDecorativos(rootZonaRio.gameObject);
        }
        finally
        {
            // Cerrar MainWorld sin guardarla jamás
            EditorSceneManager.CloseScene(escenaMain, true);
            SceneManager.SetActiveScene(escenaDestino);
        }
    }

    private static void CrearMallaTerrenoZona(
        Terrain terreno,
        Vector3 centroOrigen,
        float tamanoMetros,
        int resolucion,
        Transform parentDestino,
        int floorLayer,
        string rutaAssetMalla)
    {
        Mesh mesh = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(rutaAssetMalla)
        };

        int vertsPerSide = resolucion + 1;
        Vector3[] vertices = new Vector3[vertsPerSide * vertsPerSide];
        Vector2[] uvs = new Vector2[vertsPerSide * vertsPerSide];
        Vector3[] normals = new Vector3[vertsPerSide * vertsPerSide];
        int[] triangles = new int[resolucion * resolucion * 6];

        float mitad = tamanoMetros * 0.5f;
        float paso = tamanoMetros / resolucion;
        float yBaseTerreno = terreno.transform.position.y;

        for (int z = 0; z < vertsPerSide; z++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                float offsetX = -mitad + x * paso;
                float offsetZ = -mitad + z * paso;
                Vector3 posMundo = new Vector3(centroOrigen.x + offsetX, 0f, centroOrigen.z + offsetZ);
                float altura = terreno.SampleHeight(posMundo) + yBaseTerreno - centroOrigen.y;

                int idx = z * vertsPerSide + x;
                vertices[idx] = new Vector3(offsetX, altura, offsetZ);
                uvs[idx] = new Vector2(x / (float)resolucion * 12f, z / (float)resolucion * 12f);
                normals[idx] = Vector3.up;
            }
        }

        int t = 0;
        for (int z = 0; z < resolucion; z++)
        {
            for (int x = 0; x < resolucion; x++)
            {
                int i00 = z * vertsPerSide + x;
                int i10 = i00 + 1;
                int i01 = (z + 1) * vertsPerSide + x;
                int i11 = i01 + 1;

                triangles[t++] = i00;
                triangles[t++] = i01;
                triangles[t++] = i10;

                triangles[t++] = i10;
                triangles[t++] = i01;
                triangles[t++] = i11;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(rutaAssetMalla);
        if (existente != null)
        {
            EditorUtility.CopySerialized(mesh, existente);
            EditorUtility.SetDirty(existente);
            mesh = existente;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, rutaAssetMalla);
        }

        var goTerreno = new GameObject(mesh.name);
        goTerreno.layer = floorLayer;
        goTerreno.transform.SetParent(parentDestino, false);
        goTerreno.transform.localPosition = Vector3.zero;

        var mf = goTerreno.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = goTerreno.AddComponent<MeshRenderer>();
        var matHierba = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Pradera_Quibli.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Pradera.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Grass02.mat");
        if (matHierba != null)
            mr.sharedMaterial = matHierba;

        var mc = goTerreno.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private static void CopiarObjetosCercanosDeMainWorld(
        Transform worldRoot,
        Vector3 centroOrigen,
        float radio,
        Vector3 centroDestino,
        Transform parentDestino,
        Transform excluirSubarbol)
    {
        var contenedor = new GameObject("Entorno_Copiado_MainWorld");
        contenedor.transform.SetParent(parentDestino, false);
        contenedor.transform.localPosition = Vector3.zero;

        float radioSq = radio * radio;
        var renderers = worldRoot.GetComponentsInChildren<Renderer>(false);
        var raicesCopiadas = new HashSet<Transform>();

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (excluirSubarbol != null && r.transform.IsChildOf(excluirSubarbol))
                continue;

            Vector3 p = r.bounds.center;
            float dx = p.x - centroOrigen.x;
            float dz = p.z - centroOrigen.z;
            if (dx * dx + dz * dz > radioSq)
                continue;

            Transform propRoot = ObtenerRaizDeProp(r.transform, worldRoot);
            if (propRoot == null || !raicesCopiadas.Add(propRoot))
                continue;

            var clon = UnityEngine.Object.Instantiate(propRoot.gameObject, contenedor.transform);
            clon.name = propRoot.name;
            clon.transform.position = propRoot.position - centroOrigen + centroDestino;
            clon.transform.rotation = propRoot.rotation;
        }
    }

    private static Transform ObtenerRaizDeProp(Transform tr, Transform worldRoot)
    {
        Transform actual = tr;
        while (actual.parent != null && actual.parent != worldRoot)
        {
            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(actual);
            if (prefabRoot != null && prefabRoot.transform != worldRoot)
                return prefabRoot.transform;

            if (actual.parent.name.StartsWith("Bosque") ||
                actual.parent.name.StartsWith("Suelo vegetal") ||
                actual.parent.name.StartsWith("Vegetación") ||
                actual.parent.name.StartsWith("Hitos"))
            {
                return actual;
            }
            actual = actual.parent;
        }
        return actual;
    }

    private static void AsignarCapaSueloRecursivo(Transform root, int floorLayer)
    {
        if (root == null) return;
        if (root.GetComponent<Collider>() != null &&
            (root.name.Contains("Road") || root.name.Contains("Camino") || root.name.Contains("Floor") || root.name.Contains("Suelo") || root.name.Contains("Plataforma") || root.name.Contains("Anillo") || root.name.StartsWith("Terreno_")))
        {
            root.gameObject.layer = floorLayer;
        }
        for (int i = 0; i < root.childCount; i++)
            AsignarCapaSueloRecursivo(root.GetChild(i), floorLayer);
    }

    private static void InstanciarActoresYProps(
        Transform parent,
        out GameObject willGo,
        out GameObject estelaGo,
        out GameObject liamGo,
        out GameObject archimagoGo,
        out GameObject tobiasGo,
        out GameObject cintaRojaWill,
        out GameObject canaWill,
        out GameObject canaTobias)
    {
        willGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_WILL.prefab",
            "Actor_Will",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_WILL.asset",
            new Vector3(0f, 0f, 6f),
            0f);

        estelaGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_ESTELA.prefab",
            "Actor_Estela",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_ESTELA.asset",
            new Vector3(1.5f, 0f, 5.5f),
            -12f);

        liamGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_LIAM.prefab",
            "Actor_Liam",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_LIAM.asset",
            new Vector3(-1.4f, 0f, 10.5f),
            15f);

        archimagoGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_WILL_ORIGINAL.prefab",
            "Actor_Archimago",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_ARCHIMAGO.asset",
            CentroZonaPiedra + new Vector3(0f, 1.5f, 0f),
            180f);
        archimagoGo.SetActive(false);

        tobiasGo = InstanciarActor(
            parent,
            "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab",
            "Actor_Tobias",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_TOBI.asset",
            CentroZonaRio + new Vector3(-2.5f, 0f, 2.0f),
            145f);
        tobiasGo.SetActive(false);

        // Cinta roja de Estela en la muñeca derecha de Will
        cintaRojaWill = CrearCintaRojaEnMuneca(willGo.transform);

        // Cañas de pescar para la Parte 2 (El Río)
        canaWill = CrearCanaDePescar(willGo.transform, "CanaPescar_Will");
        canaWill.SetActive(false);

        canaTobias = CrearCanaDePescar(tobiasGo.transform, "CanaPescar_Tobias");
        canaTobias.SetActive(false);
    }

    private static GameObject InstanciarActor(
        Transform parent,
        string rutaPrefab,
        string nombreEscena,
        string rutaCharData,
        Vector3 posicion,
        float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        GameObject instancia;
        if (prefab != null)
        {
            instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }
        else
        {
            instancia = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instancia.transform.SetParent(parent, false);
        }

        instancia.name = nombreEscena;
        instancia.transform.position = posicion;
        instancia.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        DesactivarComponentesDeGameplayEnActor(instancia);

        if (instancia.GetComponent<NPCSimpleAnimator>() == null)
            instancia.AddComponent<NPCSimpleAnimator>();

        return instancia;
    }

    private static void DesactivarComponentesDeGameplayEnActor(GameObject actor)
    {
        foreach (var comp in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            string n = comp.GetType().Name;
            if (n.Contains("Input") ||
                n.Contains("Controller") && !n.Contains("Animator") && !n.Contains("AlertIcon") ||
                n.Contains("Brain") ||
                n.Contains("BehaviourManager"))
            {
                comp.enabled = false;
            }
        }

        // Apagar animators secundarios sin controller para evitar avisos de Unity
        var anims = actor.GetComponentsInChildren<Animator>(true);
        bool controllerPrincipalEncontrado = false;
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;
            if (a.runtimeAnimatorController == null)
            {
                a.enabled = false;
            }
            else if (!controllerPrincipalEncontrado)
            {
                controllerPrincipalEncontrado = true;
                a.enabled = true;
            }
        }
    }

    private static GameObject CrearCintaRojaEnMuneca(Transform willRoot)
    {
        Transform parentHueso = willRoot;
        var anim = willRoot.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var mano = anim.GetBoneTransform(HumanBodyBones.RightLowerArm) ?? anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (mano != null) parentHueso = mano;
        }

        var cinta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cinta.name = "Cinta_Roja_Estela";
        UnityEngine.Object.DestroyImmediate(cinta.GetComponent<Collider>());
        cinta.transform.SetParent(parentHueso, false);
        cinta.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        cinta.transform.localScale = new Vector3(0.11f, 0.025f, 0.11f);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var matRojo = new Material(sh)
        {
            color = new Color(0.86f, 0.12f, 0.16f)
        };
        cinta.GetComponent<MeshRenderer>().sharedMaterial = matRojo;
        return cinta;
    }

    private static GameObject CrearCanaDePescar(Transform actorRoot, string nombre)
    {
        var prefabFishingRod = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_FishingRod_01.prefab");
        GameObject root = new GameObject(nombre);
        root.transform.SetParent(actorRoot, false);
        root.transform.localPosition = new Vector3(0.28f, 0.95f, 0.45f);
        root.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);

        if (prefabFishingRod != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabFishingRod, root.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
        }
        else
        {
            var vara = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(vara.GetComponent<Collider>());
            vara.transform.SetParent(root.transform, false);
            vara.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            vara.transform.localScale = new Vector3(0.03f, 0.9f, 0.03f);
        }
        return root;
    }

    private static void InstanciarEfectosEscena(
        Transform parent,
        Transform altarTr,
        Transform portalTr,
        out GameObject vfxDeseoAltar,
        out GameObject vfxPortal,
        out GameObject vfxEmpujeLuz,
        out GameObject vfxCirculoConjuro,
        out GameObject vfxAuraConjuroWill,
        out GameObject vfxPilarResurreccion,
        out GameObject vfxResurreccion,
        out GameObject vfxColapsoSendero,
        out GameObject vfxEspirituArchimago,
        out GameObject vfxLlamaWill,
        out GameObject vfxSalpicaduraRio)
    {
        Vector3 posAltarLuz = altarTr != null ? altarTr.position + Vector3.up * 1.1f : new Vector3(0f, 1.2f, 2.5f);
        Vector3 posPortalLuz = portalTr != null ? portalTr.position + Vector3.up * 1.6f : new Vector3(0f, 1.6f, 9.5f);

        vfxDeseoAltar = InstanciarVfx(parent, "VFX_DeseoAltar", "Assets/EffectCore/prefabs/FX_LightPillar.prefab", posAltarLuz);
        // El umbral es celestial/astral sagrado: sin fuego naranja (vfx_Portal_01 eliminado)
        vfxPortal = InstanciarVfx(parent, "VFX_PortalUmbral", "Assets/Hovl Studio/HSFiles/Prefabs/Auras/Star aura.prefab", posPortalLuz);
        vfxEmpujeLuz = InstanciarVfx(parent, "VFX_EmpujeLuz", "Assets/Prefabs/Effects/SpecialAttack/VFX_LuzProtectora.prefab", posPortalLuz);
        vfxCirculoConjuro = InstanciarVfx(parent, "VFX_CirculoConjuroAnime", "Assets/Hovl Studio/HSFiles/Prefabs/Circles/Healing circle.prefab", new Vector3(0f, 0.05f, 4.5f));
        vfxAuraConjuroWill = InstanciarVfx(parent, "VFX_AuraConjuroWill", "Assets/Hovl Studio/HSFiles/Prefabs/Auras/Star aura.prefab", new Vector3(0f, 0.9f, 4.5f));
        vfxPilarResurreccion = InstanciarVfx(parent, "VFX_PilarResurreccion", "Assets/Hovl Studio/HSFiles/Prefabs/Circles/Magic circle.prefab", new Vector3(-1.2f, 0.05f, 4.5f));
        vfxResurreccion = InstanciarVfx(parent, "VFX_ResurreccionLiam", "Assets/Hovl Studio/HSFiles/Prefabs/Auras/Star aura.prefab", new Vector3(-1.2f, 0.5f, 4.5f));
        vfxColapsoSendero = InstanciarVfx(parent, "VFX_ColapsoSendero", "Assets/Prefabs/Effects/SpecialAttack/VFX_TormentaArcana.prefab", new Vector3(0f, 1f, 4.5f));
        vfxEspirituArchimago = InstanciarVfx(parent, "VFX_EspirituArchimago", "Assets/Hovl Studio/HSFiles/Prefabs/Auras/Star aura.prefab", CentroZonaPiedra + new Vector3(0f, 1.8f, 0f));
        vfxLlamaWill = InstanciarVfx(parent, "VFX_LlamaPequenaWill", "Assets/Hovl Studio/HSFiles/Prefabs/Auras/Star aura.prefab", CentroZonaPiedra);
        vfxSalpicaduraRio = InstanciarVfx(parent, "VFX_SalpicaduraRio", "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Water/CFXR2 Water Splash.prefab", CentroZonaRio);
    }

    private static GameObject InstanciarVfx(Transform parent, string nombre, string rutaPrefab, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }
        else
        {
            go = new GameObject(nombre);
            go.transform.SetParent(parent, false);
        }
        go.name = nombre;
        go.transform.position = pos;
        go.SetActive(false);
        return go;
    }

    private static void ConfigurarComponentesCinematica(
        SequenceStage stage,
        SequencePlayer player1,
        SequencePlayer player2,
        CinematicCameraDriver camDriver,
        ModuloCinematicaFinal modulo,
        SenderoBattleAutoStart autoStart,
        Camera cam,
        Light sunLight,
        SequenceDefinition seq1,
        SequenceDefinition seq2,
        Transform willTr,
        Transform estelaTr,
        Transform liamTr,
        Transform archimagoTr,
        Transform tobiasTr,
        Transform altarTr,
        Transform portalTr,
        Transform piedraTr,
        Transform orillaAguaTr,
        GameObject vfxDeseoAltar,
        GameObject vfxPortal,
        GameObject vfxEmpujeLuz,
        GameObject vfxCirculoConjuro,
        GameObject vfxAuraConjuroWill,
        GameObject vfxPilarResurreccion,
        GameObject vfxResurreccion,
        GameObject vfxColapsoSendero,
        GameObject vfxEspirituArchimago,
        GameObject vfxLlamaWill,
        GameObject cintaRojaWill,
        GameObject canaWill,
        GameObject canaTobias,
        GameObject vfxSalpicaduraRio,
        GameObject estrellaTorcidaGo,
        GameObject estrellasNocturnasRoot)
    {
        // 1. SequenceStage: registrar actores (por ID y alias) y módulo
        var soStage = new SerializedObject(stage);
        var propActors = soStage.FindProperty("_actors");
        propActors.ClearArray();

        AgregarBindingActor(propActors, propActors.arraySize, "Actor_Will", willTr);
        AgregarBindingActor(propActors, propActors.arraySize, "will", willTr);
        AgregarBindingActor(propActors, propActors.arraySize, "Player", willTr);
        AgregarBindingActor(propActors, propActors.arraySize, "Actor_Estela", estelaTr);
        AgregarBindingActor(propActors, propActors.arraySize, "estela", estelaTr);
        AgregarBindingActor(propActors, propActors.arraySize, "Actor_Liam", liamTr);
        AgregarBindingActor(propActors, propActors.arraySize, "liam", liamTr);
        AgregarBindingActor(propActors, propActors.arraySize, "Actor_Archimago", archimagoTr);
        AgregarBindingActor(propActors, propActors.arraySize, "archimago", archimagoTr);
        AgregarBindingActor(propActors, propActors.arraySize, "Actor_Tobias", tobiasTr);
        AgregarBindingActor(propActors, propActors.arraySize, "tobias", tobiasTr);

        var propModules = soStage.FindProperty("_modules");
        propModules.ClearArray();
        propModules.InsertArrayElementAtIndex(0);
        propModules.GetArrayElementAtIndex(0).objectReferenceValue = modulo;
        soStage.ApplyModifiedPropertiesWithoutUndo();

        // 2. CinematicCameraDriver
        var soDriver = new SerializedObject(camDriver);
        var propCam = soDriver.FindProperty("_managedCamera");
        if (propCam != null) propCam.objectReferenceValue = cam;
        soDriver.ApplyModifiedPropertiesWithoutUndo();

        // 3. SequencePlayer 1 (Parte 1) y SequencePlayer 2 (Parte 2)
        var soPlayer1 = new SerializedObject(player1);
        soPlayer1.FindProperty("_definition").objectReferenceValue = seq1;
        soPlayer1.FindProperty("_stage").objectReferenceValue = stage;
        soPlayer1.FindProperty("_signalIn").stringValue = "FINAL_PARTE_01_START";
        soPlayer1.FindProperty("_signalOut").stringValue = "FINAL_PARTE_02_START";
        soPlayer1.FindProperty("_cinematicCamera").objectReferenceValue = camDriver;
        soPlayer1.FindProperty("_idleGraceAfterSequence").floatValue = 0f;
        soPlayer1.ApplyModifiedPropertiesWithoutUndo();

        var soPlayer2 = new SerializedObject(player2);
        soPlayer2.FindProperty("_definition").objectReferenceValue = seq2;
        soPlayer2.FindProperty("_stage").objectReferenceValue = stage;
        soPlayer2.FindProperty("_signalIn").stringValue = "FINAL_PARTE_02_START";
        soPlayer2.FindProperty("_signalOut").stringValue = "FINAL_CREDITOS_START";
        soPlayer2.FindProperty("_cinematicCamera").objectReferenceValue = camDriver;
        soPlayer2.FindProperty("_idleGraceAfterSequence").floatValue = 0f;
        soPlayer2.ApplyModifiedPropertiesWithoutUndo();

        // 4. ModuloCinematicaFinal
        var soModulo = new SerializedObject(modulo);
        soModulo.FindProperty("_willActor").objectReferenceValue = willTr;
        soModulo.FindProperty("_estelaActor").objectReferenceValue = estelaTr;
        soModulo.FindProperty("_liamActor").objectReferenceValue = liamTr;
        soModulo.FindProperty("_archimagoActor").objectReferenceValue = archimagoTr;
        soModulo.FindProperty("_tobiasActor").objectReferenceValue = tobiasTr;

        soModulo.FindProperty("_altarTransform").objectReferenceValue = altarTr;
        soModulo.FindProperty("_portalUmbralTransform").objectReferenceValue = portalTr;
        soModulo.FindProperty("_piedraAncestralTransform").objectReferenceValue = piedraTr;
        soModulo.FindProperty("_orillaRioTransform").objectReferenceValue = orillaAguaTr;

        soModulo.FindProperty("_vfxDeseoAltar").objectReferenceValue = vfxDeseoAltar;
        soModulo.FindProperty("_vfxPortal").objectReferenceValue = vfxPortal;
        soModulo.FindProperty("_vfxEmpujeLuz").objectReferenceValue = vfxEmpujeLuz;
        soModulo.FindProperty("_vfxCirculoConjuro").objectReferenceValue = vfxCirculoConjuro;
        soModulo.FindProperty("_vfxAuraConjuroWill").objectReferenceValue = vfxAuraConjuroWill;
        soModulo.FindProperty("_vfxPilarResurreccion").objectReferenceValue = vfxPilarResurreccion;
        soModulo.FindProperty("_vfxResurreccion").objectReferenceValue = vfxResurreccion;
        soModulo.FindProperty("_vfxColapsoSendero").objectReferenceValue = vfxColapsoSendero;

        soModulo.FindProperty("_vfxEspirituArchimago").objectReferenceValue = vfxEspirituArchimago;
        soModulo.FindProperty("_vfxLlamaPequenaWill").objectReferenceValue = vfxLlamaWill;
        soModulo.FindProperty("_cintaRojaWill").objectReferenceValue = cintaRojaWill;

        soModulo.FindProperty("_canaPescarWill").objectReferenceValue = canaWill;
        soModulo.FindProperty("_canaPescarTobias").objectReferenceValue = canaTobias;
        soModulo.FindProperty("_vfxSalpicaduraRio").objectReferenceValue = vfxSalpicaduraRio;
        soModulo.FindProperty("_luzSolar").objectReferenceValue = sunLight;

        var skySendero = ObtenerOCrearSkyboxAstralSendero();
        var skyDia = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Cielo/Cielo_Dia.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/Cielo_Mediodia.mat")
            ?? skySendero;
        var skyTarde = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Cielo/Cielo_Atardecer.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Sky/Cielo_Atardecer.mat")
            ?? skyDia;
        var skyNoche = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Cielo/Cielo_Noche.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Day-Night Skyboxes/Materials/SkyMidnight.mat")
            ?? skySendero;

        soModulo.FindProperty("_skyboxSendero").objectReferenceValue = skySendero;
        soModulo.FindProperty("_skyboxDia").objectReferenceValue = skyDia;
        soModulo.FindProperty("_skyboxAtardecer").objectReferenceValue = skyTarde;
        soModulo.FindProperty("_skyboxNoche").objectReferenceValue = skyNoche;

        soModulo.FindProperty("_estrellaTorcidaObjeto").objectReferenceValue = estrellaTorcidaGo;
        soModulo.FindProperty("_estrellasNocturnasRoot").objectReferenceValue = estrellasNocturnasRoot;
        soModulo.ApplyModifiedPropertiesWithoutUndo();

        // 5. SenderoBattleAutoStart para arrancar SEQ_Final_01_ElSacrificio automáticamente en PlayMode
        var soAuto = new SerializedObject(autoStart);
        soAuto.FindProperty("_startSignal").stringValue = "FINAL_PARTE_01_START";
        soAuto.FindProperty("_startDelaySeconds").floatValue = 0.25f;
        soAuto.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material ObtenerOCrearSkyboxAstralSendero()
    {
        var matSendero = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Sendero/Mat_CosmosProfundo360.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Quantum Mana Studio/QMS - Cartoon Skybox Pack FREE/Materials/Sky0003_Night.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Quantum Mana Studio/QMS - Cartoon Skybox Pack FREE/Materials/Sky0020_NightMoon.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Day-Night Skyboxes/Materials/SkyMidnight.mat");
        if (matSendero != null) return matSendero;

        string rutaSkybox = $"{CarpetaMallasGeneradas}/Skybox_SenderoAstral.mat";
        var matExistente = AssetDatabase.LoadAssetAtPath<Material>(rutaSkybox);
        if (matExistente != null) return matExistente;

        Shader shaderCielo = Shader.Find("El Sendero/Cielo") ?? Shader.Find("Skybox/Panoramic");
        var mat = new Material(shaderCielo);
        if (shaderCielo.name == "El Sendero/Cielo")
        {
            mat.SetColor("_ColorCenit", new Color(0.015f, 0.022f, 0.085f, 1.0f)); // Azul cósmico profundo
            mat.SetColor("_ColorHorizonte", new Color(0.045f, 0.065f, 0.18f, 1.0f)); // Horizonte estrellado violeta
            mat.SetColor("_ColorSuelo", new Color(0.01f, 0.015f, 0.05f, 1.0f));
            mat.SetColor("_ColorDeBrumaDelHorizonte", new Color(0.035f, 0.055f, 0.15f, 1.0f));
            mat.SetColor("_ColorDelResplandor", new Color(0.65f, 0.75f, 1.0f, 1.0f));
            mat.SetFloat("_GrosorDeLaBruma", 0.03f);
            mat.SetFloat("_NubosidadBase", 0.02f);
            mat.SetFloat("_Nubosidad", 0f);
        }
        AssetDatabase.CreateAsset(mat, rutaSkybox);
        return mat;
    }

    private static void AgregarBindingActor(SerializedProperty propActors, int index, string id, Transform target)
    {
        if (target == null) return;
        propActors.InsertArrayElementAtIndex(index);
        var elem = propActors.GetArrayElementAtIndex(index);
        elem.FindPropertyRelative("id").stringValue = id;
        elem.FindPropertyRelative("target").objectReferenceValue = target;
        elem.FindPropertyRelative("eyeHeight").floatValue = 0f;

        // Seleccionar preferentemente el Animator que tenga un controller activo asignado
        Animator animActivo = null;
        var anims = target.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] != null && anims[i].runtimeAnimatorController != null)
            {
                animActivo = anims[i];
                break;
            }
        }
        if (animActivo == null && anims.Length > 0)
            animActivo = anims[0];

        elem.FindPropertyRelative("animator").objectReferenceValue = animActivo;
        elem.FindPropertyRelative("emotionController").objectReferenceValue = Game.NPC.Common.EmotionControllerResolver.Resolve(target.gameObject);
    }
}
#endif
