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
/// y Ribera escénica de la Laguna/Río copiada de MainWorld sin modificar MainWorld) y hornear
/// sus dos guiones (<c>Final_01_ElSacrificio</c> y <c>Final_02_ElRio</c>).
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
    private static readonly Vector3 OrigenMainWorldRio = new(176f, 24.5f, -44f);

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
        PrepararEscena(forzar: false);
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
        var asientoPrueba = GameObject.Find("Asiento_Rio_Tobias");
        if (!forzar &&
            escenaActiva.path == RutaEscenaPreview &&
            GameObject.Find("CINEMATICA_FINAL_ROOT") != null &&
            actorWill != null &&
            asientoPrueba != null &&
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
        try { RecursosDelPrologo.Asegurar(); } catch (Exception ex) { Debug.LogWarning("[Cinemática Final] Recursos prólogo: " + ex.Message); }

        if (!Directory.Exists(CarpetaMallasGeneradas))
        {
            Directory.CreateDirectory(CarpetaMallasGeneradas);
            AssetDatabase.Refresh();
        }

        Scene escenaPreview = SceneManager.GetSceneByPath(RutaEscenaPreview);
        if (!escenaPreview.IsValid())
            escenaPreview = SceneManager.GetSceneByName("CinematicaFinal_Preview");

        if (!escenaPreview.IsValid() || !escenaPreview.isLoaded)
        {
            if (File.Exists(RutaEscenaPreview))
                escenaPreview = EditorSceneManager.OpenScene(RutaEscenaPreview, OpenSceneMode.Single);
            else
                escenaPreview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        SceneManager.SetActiveScene(escenaPreview);
        foreach (var rootGo in escenaPreview.GetRootGameObjects())
            UnityEngine.Object.DestroyImmediate(rootGo);

        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer < 0) floorLayer = 0;

        // 1. Raíz de iluminación y cámara
        var lightingRoot = new GameObject("LIGHTING_AND_CAMERA");
        var sunGo = new GameObject("Directional Light");
        sunGo.transform.SetParent(lightingRoot.transform, false);
        sunGo.transform.rotation = Quaternion.Euler(46f, 155f, 0f);
        var sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(0.88f, 0.92f, 1.0f);
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
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.30f, 0.44f);
        RenderSettings.fog = false;

        // 2. Construir Zona 1: Santuario Astral de El Sendero (Altar y Umbral)
        var rootZona1 = ConstruirZona1Sendero(
            escenaPreview,
            floorLayer,
            out Transform altarTr,
            out Transform portalTr,
            out GameObject estrellasNocturnasRoot,
            out GameObject estrellaTorcidaGo,
            out GameObject portalSenderoGo,
            out GameObject ventanaPortalPiedraGo,
            out Camera camPortalSendero);

        // 3. Construir Zona 2 (Piedra Ancestral) y Zona 3 (Río / Laguna) copiando de MainWorld sin tocar MainWorld
        var rootZona2 = new GameObject("ZONA_2_PIEDRA_ANCESTRAL");
        rootZona2.transform.position = CentroZonaPiedra;

        var rootZona3 = new GameObject("ZONA_3_EL_RIO");
        rootZona3.transform.position = CentroZonaRio;

        CopiarZonasDesdeMainWorld(
            escenaPreview,
            rootZona2.transform,
            rootZona3.transform,
            floorLayer,
            portalSenderoGo,
            out Transform piedraTr,
            out Transform orillaAguaTr,
            out GameObject portalPiedraGo,
            out GameObject ventanaPortalSenderoGo,
            out Camera camPortalPiedra,
            out Light luzNocheRioGrupo);
        LimpiarCollidersYTriggersDecorativos(rootZona2);
        LimpiarCollidersYTriggersDecorativos(rootZona3);

        // 4. NavMesh para todas las zonas (solo sobre geometría de suelo en capa Floor)
        var navGo = new GameObject("NAVMESH_CINEMATICA_FINAL");
        var navSurface = navGo.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navSurface.layerMask = 1 << floorLayer;
        navSurface.BuildNavMesh();

        // 5. Instanciar actores y props
        var rootActores = new GameObject("ACTORES_CINEMATICA_FINAL");
        InstanciarActoresYProps(
            rootActores.transform,
            rootZona3.transform,
            out GameObject willGo,
            out GameObject estelaGo,
            out GameObject liamGo,
            out GameObject archimagoGo,
            out GameObject tobiasGo,
            out GameObject cintaRojaWill,
            out GameObject canaWill,
            out GameObject canaTobias,
            out GameObject cuadernoTobias);

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
            luzNocheRioGrupo,
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
            cuadernoTobias,
            vfxSalpicaduraRio,
            estrellaTorcidaGo,
            estrellasNocturnasRoot,
            portalSenderoGo,
            portalPiedraGo,
            ventanaPortalSenderoGo,
            ventanaPortalPiedraGo,
            camPortalSendero,
            camPortalPiedra);

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
        if (zona2 != null)
        {
            var santuarioTr = zona2.transform.Find("Santuario_Piedra_Ancestral");
            if (santuarioTr != null)
            {
                santuarioTr.position = new Vector3(CentroZonaPiedra.x, CentroZonaPiedra.y, CentroZonaPiedra.z - 4.0f);
            }
            LimpiarCollidersYTriggersDecorativos(zona2);
            foreach (var r in zona2.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                string nr = r.gameObject.name.ToLowerInvariant();
                if (nr.Contains("monolito") || nr.Contains("suelo") || nr.Contains("floor") || nr.Contains("terreno") || nr.Contains("plataforma") || nr.Contains("anillo") || nr.Contains("ventana_portal"))
                    continue;
                Vector3 c = r.bounds.center;
                if (c.x > CentroZonaPiedra.x - 16.0f && c.x < CentroZonaPiedra.x + 16.0f &&
                    c.z > CentroZonaPiedra.z - 2.0f && c.z < CentroZonaPiedra.z + 18.0f &&
                    r.bounds.size.y >= 0.30f)
                {
                    r.gameObject.SetActive(false);
                }
            }
        }

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

        GameObject FindInRoot(string name)
        {
            var g = GameObject.Find(name);
            if (g != null) return g;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
            return null;
        }

        var stage = root.GetComponent<SequenceStage>();
        var player1 = root.GetComponent<SequencePlayer>();
        var player2Tr = root.transform.Find("SequencePlayer_Parte2");
        var player2 = player2Tr != null ? player2Tr.GetComponent<SequencePlayer>() : null;

        if (stage != null)
        {
            var willGo = FindInRoot("Actor_Will");
            var estelaGo = FindInRoot("Actor_Estela");
            var liamGo = FindInRoot("Actor_Liam");
            var archimagoGo = FindInRoot("Actor_Archimago");
            var tobiasGo = FindInRoot("Actor_Tobias");

            var soStage = new SerializedObject(stage);
            var propActors = soStage.FindProperty("_actors");
            propActors.ClearArray();
            if (willGo != null)
            {
                AgregarBindingActor(propActors, propActors.arraySize, "Player", willGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "Actor_Will", willGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "will", willGo.transform);
            }
            if (estelaGo != null)
            {
                AgregarBindingActor(propActors, propActors.arraySize, "Actor_Estela", estelaGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "estela", estelaGo.transform);
            }
            if (liamGo != null)
            {
                AgregarBindingActor(propActors, propActors.arraySize, "Actor_Liam", liamGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "liam", liamGo.transform);
            }
            if (archimagoGo != null)
            {
                AgregarBindingActor(propActors, propActors.arraySize, "Actor_Archimago", archimagoGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "archimago", archimagoGo.transform);
            }
            if (tobiasGo != null)
            {
                AgregarBindingActor(propActors, propActors.arraySize, "Actor_Tobias", tobiasGo.transform);
                AgregarBindingActor(propActors, propActors.arraySize, "tobias", tobiasGo.transform);
            }
            soStage.ApplyModifiedPropertiesWithoutUndo();
        }

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
        out GameObject estrellaTorcidaGo,
        out GameObject portalSenderoGo,
        out GameObject ventanaPortalPiedraGo,
        out Camera camPortalSendero)
    {
        var root = new GameObject("ZONA_1_EL_SENDERO");
        root.transform.position = CentroZonaSendero;

        altarTransform = root.transform;
        portalTransform = root.transform;
        portalSenderoGo = null;
        ventanaPortalPiedraGo = null;
        camPortalSendero = null;

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
                    // 1. Copiar el Suelo Astral auténtico de Sendero (Suelo_00_Llegada)
                    Transform suelo = worldSendero.transform.Find("SENDERO_CAMINO_ASTRAL/Suelo_00_Llegada");
                    if (suelo == null)
                    {
                        foreach (var t in worldSendero.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "Suelo_00_Llegada") { suelo = t; break; }
                        }
                    }
                    if (suelo != null)
                    {
                        var copiaSuelo = UnityEngine.Object.Instantiate(suelo.gameObject, root.transform);
                        copiaSuelo.name = "Suelo_Astral_Sendero";
                        copiaSuelo.transform.localPosition = new Vector3(0f, -0.3f, 4.5f);
                        copiaSuelo.transform.localRotation = Quaternion.identity;
                        altarTransform = copiaSuelo.transform;
                    }

                    // 2. Copiar el Umbral de salida (Portal_00_UmbralTierra) en Z=8.2
                    Transform portal = worldSendero.transform.Find("PORTALES_CELESTIALES/Portal_00_UmbralTierra");
                    if (portal == null)
                    {
                        foreach (var t in worldSendero.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "Portal_00_UmbralTierra") { portal = t; break; }
                        }
                    }
                    if (portal != null)
                    {
                        var copiaPortal = UnityEngine.Object.Instantiate(portal.gameObject, root.transform);
                        copiaPortal.name = "Umbral_Salida_Sendero";
                        copiaPortal.transform.localPosition = new Vector3(0f, 0f, 8.2f);
                        copiaPortal.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        portalTransform = copiaPortal.transform;
                        portalSenderoGo = copiaPortal.gameObject;

                        // Desactivar todos los colliders del arco del portal para que jamás obstruya cámaras ni NavMesh
                        foreach (var c in copiaPortal.GetComponentsInChildren<Collider>(true))
                            if (c != null) c.enabled = false;

                        // Quad de ventana luminosa hacia La Piedra (en la cara sur del portal; se activa en PlayMode al abrir el portal con RenderTexture)
                        var quadPiedra = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quadPiedra.name = "Ventana_Portal_Piedra";
                        UnityEngine.Object.DestroyImmediate(quadPiedra.GetComponent<Collider>());
                        quadPiedra.transform.SetParent(copiaPortal.transform, false);
                        quadPiedra.transform.localPosition = new Vector3(0f, 1.65f, -0.05f);
                        quadPiedra.transform.localRotation = Quaternion.identity;
                        quadPiedra.transform.localScale = new Vector3(2.1f, 2.8f, 1.0f);
                        AplicarMaterialLuminosoPortal(quadPiedra, new Color(0.38f, 0.78f, 1.0f, 0.92f));
                        quadPiedra.SetActive(false);
                        ventanaPortalPiedraGo = quadPiedra;
                    }

                    // 3. Copiar el entorno cósmico de estrellas 3D filtrando árboles/rocas bajas sobre el altar
                    var cosmicEnv = worldSendero.transform.Find("COSMIC_ENVIRONMENT_3D");
                    if (cosmicEnv != null)
                    {
                        var copiaCosmic = UnityEngine.Object.Instantiate(cosmicEnv.gameObject, root.transform);
                        copiaCosmic.name = "COSMIC_ENVIRONMENT_3D";
                        copiaCosmic.transform.localPosition = Vector3.zero;
                        LimpiarObjetosFlotantesSobreAltar(copiaCosmic.transform, new Vector3(0f, 0f, 4.5f), 42f);
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(escenaSendero, true);
                SceneManager.SetActiveScene(escenaDestino);
            }
        }

        // Luz cálida astral sobre el altar para iluminar los rostros de Will y Liam
        var goLuzSendero = new GameObject("Luz_Astral_Santuario");
        goLuzSendero.transform.SetParent(root.transform, false);
        goLuzSendero.transform.localPosition = new Vector3(0f, 3.8f, 5.2f);
        var lSendero = goLuzSendero.AddComponent<Light>();
        lSendero.type = LightType.Point;
        lSendero.color = new Color(0.75f, 0.88f, 1.0f);
        lSendero.intensity = 2.2f;
        lSendero.range = 20f;

        // Cámara en Zona 1 mirando hacia Will en Z=4.8 (para emitir la textura que se ve desde el portal de La Piedra)
        var goCamSendero = new GameObject("CamPortal_Sendero");
        goCamSendero.transform.SetParent(root.transform, false);
        goCamSendero.transform.localPosition = new Vector3(0f, 1.35f, 7.6f);
        goCamSendero.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        camPortalSendero = goCamSendero.AddComponent<Camera>();
        camPortalSendero.nearClipPlane = 0.1f;
        camPortalSendero.farClipPlane = 120f;
        camPortalSendero.fieldOfView = 40f;
        camPortalSendero.enabled = false;
        var uacSendero = goCamSendero.AddComponent<UniversalAdditionalCameraData>();
        uacSendero.renderPostProcessing = false;

        estrellasNocturnasRoot = new GameObject("Estrellas_Nocturnas");
        estrellasNocturnasRoot.transform.SetParent(root.transform, false);

        // Domo de estrellas 3D alrededor del Santuario del Sendero para inmersión astral
        CrearCampoEstelarEnZona(root.transform, "Campo_Estelar_Sendero", new Vector3(0f, 0f, 4.5f), 65f, 110);

        // Domo de estrellas nocturnas en Zona 3 (El Río) y la estrella de madera de 5 puntas ligeramente torcida
        // Colocada hacia el horizonte sur (Z = -28, Y = 18) sobre el agua despejada de árboles
        CrearCampoEstelarEnZona(estrellasNocturnasRoot.transform, "Campo_Estelar_Rio", CentroZonaRio, 85f, 140);
        estrellaTorcidaGo = CrearEstrellaDeMaderaTorcida(estrellasNocturnasRoot.transform, CentroZonaRio + new Vector3(0f, 18f, -28f));
        estrellaTorcidaGo.transform.localScale = Vector3.one * 2.2f;
        estrellaTorcidaGo.SetActive(true);

        LimpiarCollidersYTriggersDecorativos(root);
        AsignarCapaSueloRecursivo(root.transform, floorLayer);

        return root;
    }

    private static void LimpiarObjetosFlotantesSobreAltar(Transform cosmicRoot, Vector3 centroAltar, float radioLibre)
    {
        if (cosmicRoot == null) return;
        float rSq = radioLibre * radioLibre;
        var renderers = cosmicRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            string n = r.gameObject.name.ToLowerInvariant();
            Vector3 p = r.bounds.center;
            float dx = p.x - centroAltar.x;
            float dz = p.z - centroAltar.z;
            float distSq = dx * dx + dz * dz;

            // Quitar árboles, setas, rocas o islas flotantes cercanas al eje de cámara del altar
            if (distSq < rSq && (n.Contains("tree") || n.Contains("mushroom") || n.Contains("rock") || n.Contains("island") || n.Contains("plant") || n.Contains("bush") || p.y > 1.5f))
            {
                r.gameObject.SetActive(false);
            }
        }
    }

    private static void AplicarMaterialLuminosoPortal(GameObject quadGo, Color colorPortal)
    {
        if (quadGo == null) return;
        var mr = quadGo.GetComponent<MeshRenderer>();
        if (mr == null) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh)
        {
            color = colorPortal
        };
        mat.SetFloat("_Cull", 0f);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private static void CrearCampoEstelarEnZona(Transform parent, string nombre, Vector3 centro, float radio, int cantidad)
    {
        var domoRoot = new GameObject(nombre);
        domoRoot.transform.SetParent(parent, false);
        domoRoot.transform.position = centro;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var matDorada = new Material(sh) { color = new Color(1.0f, 0.94f, 0.72f, 1.0f) };
        var matCeleste = new Material(sh) { color = new Color(0.72f, 0.88f, 1.0f, 1.0f) };
        var matBlanca = new Material(sh) { color = new Color(0.96f, 0.98f, 1.0f, 1.0f) };

        UnityEngine.Random.InitState(19970824 + nombre.GetHashCode());
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            if (dir.y < 0.06f) dir.y = Mathf.Abs(dir.y) + 0.08f;
            dir.Normalize();

            var estrella = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            estrella.name = $"Estrella_{i}";
            UnityEngine.Object.DestroyImmediate(estrella.GetComponent<Collider>());
            estrella.transform.SetParent(domoRoot.transform, false);
            float dist = radio * UnityEngine.Random.Range(0.78f, 1.15f);
            estrella.transform.localPosition = dir * dist;
            float escala = UnityEngine.Random.Range(0.16f, 0.42f);
            estrella.transform.localScale = Vector3.one * escala;

            var mr = estrella.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = (i % 3 == 0) ? matCeleste : ((i % 2 == 0) ? matDorada : matBlanca);
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    private static void LimpiarCollidersYTriggersDecorativos(GameObject zonaRoot)
    {
        if (zonaRoot == null) return;

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

        foreach (var col in zonaRoot.GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            string n = col.gameObject.name;
            // Mantener solo el suelo navegable activo para que ningún monolito, portal ni roca bloquee cámaras ni NavMesh
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
        // Orientada hacia la cámara de la orilla (que mira hacia el sur -Z y arriba +Y)
        starRoot.transform.rotation = Quaternion.Euler(32f, 0f, 11f);

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var matNucleo = new Material(sh) { color = new Color(1.0f, 0.97f, 0.82f, 1.0f) };
        var matPuntas = new Material(sh) { color = new Color(1.0f, 0.90f, 0.52f, 1.0f) };
        var matHalo = new Material(sh) { color = new Color(1.0f, 0.86f, 0.42f, 0.45f) };

        // Núcleo esférico brillante
        var nucleo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nucleo.name = "Nucleo_Estrella";
        UnityEngine.Object.DestroyImmediate(nucleo.GetComponent<Collider>());
        nucleo.transform.SetParent(starRoot.transform, false);
        nucleo.transform.localScale = new Vector3(0.75f, 0.75f, 0.35f);
        var mrN = nucleo.GetComponent<MeshRenderer>();
        mrN.sharedMaterial = matNucleo;
        mrN.shadowCastingMode = ShadowCastingMode.Off;

        // 5 puntas talladas a mano (con una punta ligeramente torcida como en la novela)
        for (int i = 0; i < 5; i++)
        {
            float ang = i * 72f + (i == 1 ? 12f : (i == 3 ? -6f : 0f));
            var pivote = new GameObject($"Pivote_Punta_{i}");
            pivote.transform.SetParent(starRoot.transform, false);
            pivote.transform.localRotation = Quaternion.Euler(0f, 0f, ang);

            var punta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            punta.name = $"Punta_{i}";
            UnityEngine.Object.DestroyImmediate(punta.GetComponent<Collider>());
            punta.transform.SetParent(pivote.transform, false);
            punta.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            punta.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            float largo = (i == 0) ? 1.55f : (i == 1 ? 1.25f : 1.42f);
            punta.transform.localScale = new Vector3(0.42f, 0.42f * largo, 0.16f);
            var mrP = punta.GetComponent<MeshRenderer>();
            mrP.sharedMaterial = matPuntas;
            mrP.shadowCastingMode = ShadowCastingMode.Off;
        }

        var luzEstrella = new GameObject("Luz_Estrella_Will");
        luzEstrella.transform.SetParent(starRoot.transform, false);
        var light = luzEstrella.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.92f, 0.62f);
        light.intensity = 12f;
        light.range = 40f;

        return starRoot;
    }

    private static void CopiarZonasDesdeMainWorld(
        Scene escenaDestino,
        Transform rootZonaPiedra,
        Transform rootZonaRio,
        int floorLayer,
        GameObject portalSenderoModelo,
        out Transform piedraAncestralTransform,
        out Transform orillaAguaTransform,
        out GameObject portalPiedraGo,
        out GameObject ventanaPortalSenderoGo,
        out Camera camPortalPiedra,
        out Light luzNocheRioGrupo)
    {
        piedraAncestralTransform = rootZonaPiedra;
        orillaAguaTransform = rootZonaRio;
        portalPiedraGo = null;
        ventanaPortalSenderoGo = null;
        camPortalPiedra = null;
        luzNocheRioGrupo = null;

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
            Transform santuario = null;

            foreach (var go in escenaMain.GetRootGameObjects())
            {
                if (terreno == null)
                    terreno = go.GetComponentInChildren<Terrain>(true);

                if (go.name.ToUpperInvariant().Contains("WORLD") && worldRoot == null)
                    worldRoot = go;

                if (santuario == null)
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Contains("Santuario ancestral"))
                        {
                            santuario = t;
                            break;
                        }
                    }
                }
            }

            // 1. Generar malla de terreno fiel a MainWorld para Zona 2 (Piedra Ancestral)
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
            }

            // 1B. Generar malla de ribera escénica para Zona 3 (El Río / Laguna):
            // Pradera llana en Z = -1.2 .. +18.0 (Y = 0.0m) y cuenca de río/laguna en Z = -1.5 .. -36.0 (Y = -0.85m)
            CrearMallaTerrenoRiberaEscenica(
                96f,
                80,
                rootZonaRio,
                floorLayer,
                $"{CarpetaMallasGeneradas}/Terreno_RiberaRio.asset");

            // 2A. Santuario de la Piedra Ancestral
            if (santuario != null)
            {
                var copiaSantuario = UnityEngine.Object.Instantiate(santuario.gameObject, rootZonaPiedra);
                copiaSantuario.name = "Santuario_Piedra_Ancestral";
                copiaSantuario.transform.position = santuario.position - OrigenMainWorldPiedra + CentroZonaPiedra + new Vector3(0f, 0f, -4.0f);

                Transform monolito = null;
                foreach (Transform t in copiaSantuario.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("monolito") || t.name.Contains("Monolito"))
                    {
                        monolito = t;
                        break;
                    }
                }
                if (monolito != null)
                {
                    piedraAncestralTransform = monolito;
                }

                // Desactivar cualquier collider que no sea el suelo y limpiar pilares/bloques del patio frente al monolito
                foreach (var col in copiaSantuario.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;
                    string nc = col.gameObject.name.ToLowerInvariant();
                    if (nc.Contains("suelo") || nc.Contains("floor") || nc.Contains("plataforma") || nc.Contains("anillo"))
                        col.gameObject.layer = floorLayer;
                    else
                        col.enabled = false;
                }

                foreach (var r in copiaSantuario.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    string nr = r.gameObject.name.ToLowerInvariant();
                    if (nr.Contains("monolito") || nr.Contains("suelo") || nr.Contains("floor") || nr.Contains("plataforma") || nr.Contains("anillo"))
                        continue;
                    Vector3 c = r.bounds.center;
                    if (c.x > CentroZonaPiedra.x - 16.0f && c.x < CentroZonaPiedra.x + 16.0f &&
                        c.z > CentroZonaPiedra.z - 2.0f && c.z < CentroZonaPiedra.z + 18.0f &&
                        r.bounds.size.y >= 0.30f)
                    {
                        r.gameObject.SetActive(false);
                    }
                }
            }

            if (worldRoot != null)
            {
                CopiarObjetosCercanosDeMainWorld(
                    worldRoot.transform,
                    OrigenMainWorldPiedra,
                    42f,
                    CentroZonaPiedra,
                    rootZonaPiedra,
                    excluirSubarbol: santuario,
                    radioMinimoLibre: 22f);
            }

            // 2B. Vestir la Ribera Escénica del Río (Zona 3) con lámina de agua cristalina, juncos, nenúfares,
            // piedras de orilla, flores silvestres y arboleda en la pradera
            ConstruirEscenarioRiberaRio(rootZonaRio, out orillaAguaTransform, out luzNocheRioGrupo);

            // 2C. Umbral de La Piedra Ancestral (portal en Z=0.4 frente al monolito en Z=-2.5)
            if (portalSenderoModelo != null)
            {
                portalPiedraGo = UnityEngine.Object.Instantiate(portalSenderoModelo, rootZonaPiedra);
                portalPiedraGo.name = "Umbral_Piedra_Ancestral";
                portalPiedraGo.transform.localPosition = new Vector3(0f, 0f, 0.4f);
                portalPiedraGo.transform.localRotation = Quaternion.identity;

                foreach (var c in portalPiedraGo.GetComponentsInChildren<Collider>(true))
                    if (c != null) c.enabled = false;
                foreach (var mr in portalPiedraGo.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr != null) mr.enabled = false;

                var quadSendero = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadSendero.name = "Ventana_Portal_Sendero";
                UnityEngine.Object.DestroyImmediate(quadSendero.GetComponent<Collider>());
                quadSendero.transform.SetParent(portalPiedraGo.transform, false);
                quadSendero.transform.localPosition = new Vector3(0f, 1.65f, 0.05f);
                quadSendero.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                quadSendero.transform.localScale = new Vector3(2.1f, 2.8f, 1.0f);
                AplicarMaterialLuminosoPortal(quadSendero, new Color(0.38f, 0.78f, 1.0f, 0.92f));
                quadSendero.SetActive(false);
                ventanaPortalSenderoGo = quadSendero;

                var prefabPortalVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab");
                if (prefabPortalVfx != null)
                {
                    var vfxUmbralPiedra = (GameObject)PrefabUtility.InstantiatePrefab(prefabPortalVfx, portalPiedraGo.transform);
                    vfxUmbralPiedra.name = "VFX_Portal_Piedra";
                    vfxUmbralPiedra.transform.localPosition = new Vector3(0f, 1.6f, 0.08f);
                    vfxUmbralPiedra.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                portalPiedraGo = new GameObject("Umbral_Piedra_Ancestral");
                portalPiedraGo.transform.SetParent(rootZonaPiedra, false);
                portalPiedraGo.transform.localPosition = new Vector3(0f, 0f, 2.3f);
                ventanaPortalSenderoGo = null;
            }

            var goCamPiedra = new GameObject("CamPortal_Piedra");
            goCamPiedra.transform.SetParent(rootZonaPiedra, false);
            goCamPiedra.transform.localPosition = new Vector3(0f, 1.35f, 2.6f);
            goCamPiedra.transform.localRotation = Quaternion.identity;
            camPortalPiedra = goCamPiedra.AddComponent<Camera>();
            camPortalPiedra.nearClipPlane = 0.1f;
            camPortalPiedra.farClipPlane = 120f;
            camPortalPiedra.fieldOfView = 40f;
            camPortalPiedra.enabled = false;
            var uacPiedra = goCamPiedra.AddComponent<UniversalAdditionalCameraData>();
            uacPiedra.renderPostProcessing = false;

            // Bóveda de estrellas nocturnas sobre el santuario de La Piedra Ancestral
            CrearCampoEstelarEnZona(rootZonaPiedra, "Campo_Estelar_Piedra", CentroZonaPiedra, 80f, 120);

            var goLuzPiedra = new GameObject("Luz_Nocturna_Santuario");
            goLuzPiedra.transform.SetParent(rootZonaPiedra, false);
            goLuzPiedra.transform.localPosition = new Vector3(0f, 3.8f, 5.2f);
            var lPiedra = goLuzPiedra.AddComponent<Light>();
            lPiedra.type = LightType.Point;
            lPiedra.color = new Color(0.76f, 0.86f, 1.0f);
            lPiedra.intensity = 2.2f;
            lPiedra.range = 24f;

            var goLuzMonolito = new GameObject("Luz_Monolito_Portal");
            goLuzMonolito.transform.SetParent(rootZonaPiedra, false);
            goLuzMonolito.transform.localPosition = new Vector3(0f, 2.2f, 2.8f);
            var lMono = goLuzMonolito.AddComponent<Light>();
            lMono.type = LightType.Point;
            lMono.color = new Color(0.55f, 0.82f, 1.0f);
            lMono.intensity = 1.8f;
            lMono.range = 14f;

            LimpiarCollidersYTriggersDecorativos(rootZonaPiedra.gameObject);
            LimpiarCollidersYTriggersDecorativos(rootZonaRio.gameObject);
        }
        finally
        {
            EditorSceneManager.CloseScene(escenaMain, true);
            SceneManager.SetActiveScene(escenaDestino);
        }
    }

    private static void CrearMallaTerrenoRiberaEscenica(
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
        int[] triangles = new int[resolucion * resolucion * 6];

        float mitad = tamanoMetros * 0.5f;
        float paso = tamanoMetros / resolucion;

        for (int z = 0; z < vertsPerSide; z++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                float offsetX = -mitad + x * paso;
                float offsetZ = -mitad + z * paso;

                // Orilla recta y nivelada en Z = -1.2 (Y = 0.0m en toda la pradera Z >= -1.2)
                float curvaOrilla = -1.2f;
                float altura;
                if (offsetZ >= curvaOrilla)
                {
                    // Pradera 100% llana en Z = -1.2 .. +14.0 (Y = 0.0m) para que el horizonte quede recto
                    float dNorte = offsetZ - curvaOrilla;
                    if (dNorte <= 15.0f)
                        altura = 0.0f;
                    else
                        altura = Mathf.SmoothStep(0f, 1.4f, (dNorte - 15.0f) / 28.0f);
                }
                else
                {
                    float dAgua = curvaOrilla - offsetZ;
                    if (dAgua <= 20.0f)
                    {
                        // Cuenca poco profunda del río (Y = -0.25m) bajo la lámina de agua (Y = -0.10m)
                        // Así Suelo(cam) para las cámaras sobre el río mantiene la altura natural de ojos
                        float tFondo = Mathf.Clamp01(dAgua / 1.8f);
                        altura = Mathf.Lerp(0.0f, -0.25f, tFondo);
                    }
                    else
                    {
                        // Orilla lejana al otro lado del río (Z < -21)
                        float tLejos = Mathf.Clamp01((dAgua - 20.0f) / 6.0f);
                        altura = Mathf.Lerp(-0.25f, 0.45f, tLejos);
                    }
                }

                int idx = z * vertsPerSide + x;
                vertices[idx] = new Vector3(offsetX, altura, offsetZ);
                uvs[idx] = new Vector2(x / (float)resolucion * 14f, z / (float)resolucion * 14f);
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

    private static void ConstruirEscenarioRiberaRio(
        Transform rootZonaRio,
        out Transform orillaAguaTransform,
        out Light luzNocheRioGrupo)
    {
        var contenedor = new GameObject("Decorado_Ribera_Laguna");
        contenedor.transform.SetParent(rootZonaRio, false);
        contenedor.transform.localPosition = Vector3.zero;

        // 1. Lámina de agua cristalina del río/laguna en Y = -0.10m
        var aguaGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        aguaGo.name = "Lamina_Agua_Rio";
        UnityEngine.Object.DestroyImmediate(aguaGo.GetComponent<Collider>());
        aguaGo.transform.SetParent(contenedor.transform, false);
        aguaGo.transform.localPosition = new Vector3(0f, -0.10f, -11.5f);
        aguaGo.transform.localScale = new Vector3(8.5f, 1f, 2.4f);

        var matAgua = AssetDatabase.LoadAssetAtPath<Material>("Assets/Escenario/Vestido/Recursos/AguaLaguna.mat")
            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Lake.mat");
        var mrAgua = aguaGo.GetComponent<MeshRenderer>();
        if (matAgua != null && mrAgua != null)
        {
            mrAgua.sharedMaterial = matAgua;
        }
        else if (mrAgua != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mrAgua.sharedMaterial = new Material(sh) { color = new Color(0.18f, 0.52f, 0.68f, 0.88f) };
        }

        // 2. Punto de caída al agua de Estela
        var marcadorOrilla = new GameObject("Punto_Agua_Salpicadura_Estela");
        marcadorOrilla.transform.SetParent(rootZonaRio, false);
        marcadorOrilla.transform.localPosition = new Vector3(-2.6f, -0.10f, -2.5f);
        orillaAguaTransform = marcadorOrilla.transform;

        // 3. Luz nocturna cálida para iluminar suavemente los rostros del grupo en el Acto 3 (Noche)
        var goLuzNoche = new GameObject("Luz_Noche_Grupo_Rio");
        goLuzNoche.transform.SetParent(rootZonaRio, false);
        goLuzNoche.transform.localPosition = new Vector3(0f, 2.4f, -2.2f);
        luzNocheRioGrupo = goLuzNoche.AddComponent<Light>();
        luzNocheRioGrupo.type = LightType.Point;
        luzNocheRioGrupo.color = new Color(0.82f, 0.88f, 1.0f);
        luzNocheRioGrupo.intensity = 2.4f;
        luzNocheRioGrupo.range = 14f;
        goLuzNoche.SetActive(false);

        // 4. Vegetación y rocas de ribera usando los prefabs de Fantasy Kingdom de MainWorld
        const string FKVeg = "Assets/Art/World/Fantasy_Kingdom_Pack/Prefabs/Vegetation/";
        const string FKRock = "Assets/Art/World/Fantasy_Kingdom_Pack/Prefabs/Rock/";

        // Rocas de orilla flanqueando la zona de pesca (fuera del eje de cámaras X = -3.8..+3.8)
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a01.prefab", new Vector3(-5.2f, -0.08f, -1.2f), 25f, 1.05f);
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a02.prefab", new Vector3(5.0f, -0.08f, -1.2f), -40f, 1.0f);
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a03.prefab", new Vector3(-6.4f, 0.0f, 1.5f), 80f, 1.15f);
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a01.prefab", new Vector3(6.2f, 0.0f, 1.8f), 140f, 1.1f);
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a02.prefab", new Vector3(-2.6f, 0.0f, 4.6f), 55f, 0.85f);
        ColocarPropDecorativo(contenedor.transform, FKRock + "Rock01_a03.prefab", new Vector3(2.8f, 0.0f, 4.8f), -70f, 0.85f);

        // Juncos y nenúfares en el agua (bajos < 0.35m o en los flancos para embellecer sin tapar)
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Grass01_a02.prefab", new Vector3(-4.6f, -0.12f, -1.6f), 15f, 1.0f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Grass01_a03.prefab", new Vector3(4.5f, -0.12f, -1.6f), 65f, 1.0f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Plant06_a01.prefab", new Vector3(-3.4f, -0.09f, -3.2f), 40f, 1.0f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Plant06_a02.prefab", new Vector3(3.2f, -0.09f, -3.5f), 120f, 1.0f);

        // Flores silvestres y hierba en la pradera detrás y alrededor de los personajes (Z = +1.0 .. +5.5)
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Flower02_b01.prefab", new Vector3(-3.2f, 0f, 1.4f), 0f, 0.95f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Flower05_a01.prefab", new Vector3(3.1f, 0f, 1.5f), 45f, 0.95f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Flower02_b02.prefab", new Vector3(-1.4f, 0f, 2.2f), 90f, 0.9f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Flower05_a02.prefab", new Vector3(1.5f, 0f, 2.3f), 130f, 0.9f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Flower02_b01.prefab", new Vector3(0.0f, 0f, 2.8f), 25f, 0.95f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Grass01_a02.prefab", new Vector3(-2.2f, 0f, 3.4f), 40f, 0.9f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Grass01_a03.prefab", new Vector3(2.3f, 0f, 3.5f), 110f, 0.9f);

        // Arboleda pintoresca al fondo de la pradera norte (Z = +5.2 .. +11.5) para que todos los planos
        // que miran desde el río hacia los personajes tengan un bosque frondoso y vivo de fondo
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_e01.prefab", new Vector3(-5.2f, 0.0f, 5.6f), 20f, 0.85f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_a03.prefab", new Vector3(5.4f, 0.0f, 5.5f), -35f, 0.85f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_d03.prefab", new Vector3(-2.4f, 0.0f, 6.4f), 50f, 0.82f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_b02.prefab", new Vector3(2.5f, 0.0f, 6.5f), 110f, 0.82f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_e02.prefab", new Vector3(0.0f, 0.0f, 7.6f), 15f, 0.88f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_a04.prefab", new Vector3(-7.8f, 0.0f, 8.2f), -25f, 0.95f);
        ColocarPropDecorativo(contenedor.transform, FKVeg + "Tree04_e01.prefab", new Vector3(7.8f, 0.0f, 8.4f), 65f, 0.95f);

            // 5. Puntos de asiento en el suelo (NPCWorldPoint con SitGround) bien espaciados en la orilla llana
        var asientoRioTobias = CrearAsientoDeSuelo(rootZonaRio, "Asiento_Rio_Tobias", new Vector3(0.3f, 0f, 0.0f), 175f);
        var asientoRioLiam = CrearAsientoDeSuelo(rootZonaRio, "Asiento_Rio_Liam", new Vector3(1.6f, 0f, 0.0f), 192f);

        CrearAsientoDeSuelo(rootZonaRio, "Asiento_Noche_Will", new Vector3(-1.8f, 0f, 0.0f), 176f);
        CrearAsientoDeSuelo(rootZonaRio, "Asiento_Noche_Estela", new Vector3(-0.6f, 0f, 0.0f), 179f);
        CrearAsientoDeSuelo(rootZonaRio, "Asiento_Noche_Tobias", new Vector3(0.6f, 0f, 0.0f), 181f);
        CrearAsientoDeSuelo(rootZonaRio, "Asiento_Noche_Liam", new Vector3(1.8f, 0f, 0.0f), 184f);

        // Marca de escenario M_* para que CapturaDeEscenario permita fotografiar la planta de las 3 zonas
        var marcaEscenario = new GameObject("M_Centro_Piedra");
        marcaEscenario.transform.SetParent(rootZonaRio.parent, false);
        marcaEscenario.transform.position = CentroZonaPiedra;
    }

    private static GameObject CrearAsientoDeSuelo(Transform parentZona, string nombre, Vector3 localPos, float rumboGrados)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(parentZona, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, rumboGrados, 0f);

        var wp = go.AddComponent<NPCWorldPoint>();
        wp.activityType = NPCAmbientActivity.SitGround;
        wp.interactionPoint = go.transform;
        wp.overrideFacing = true;
        return go;
    }

    private static void ColocarPropDecorativo(Transform parent, string rutaPrefab, Vector3 localPos, float yaw, float escala)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null) return;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.transform.localPosition = localPos;
        inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        inst.transform.localScale = Vector3.one * escala;
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
                // Explanada llana (Y = 0) en el radio escénico central para evitar que la colina corte el suelo del santuario
                float distCentro = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                float factorLlano = Mathf.Clamp01((distCentro - 16f) / 14f);
                altura *= factorLlano * factorLlano;

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
        Transform excluirSubarbol,
        float radioMinimoLibre = 0f)
    {
        var contenedor = new GameObject("Entorno_Copiado_MainWorld");
        contenedor.transform.SetParent(parentDestino, false);
        contenedor.transform.localPosition = Vector3.zero;

        float radioSq = radio * radio;
        float minSq = radioMinimoLibre * radioMinimoLibre;
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
            float distSq = dx * dx + dz * dz;
            if (distSq > radioSq || distSq < minSq)
                continue;

            Transform propRoot = ObtenerRaizDeProp(r.transform, worldRoot);
            if (propRoot == null || !raicesCopiadas.Add(propRoot))
                continue;

            float dxRoot = propRoot.position.x - centroOrigen.x;
            float dzRoot = propRoot.position.z - centroOrigen.z;
            if (dxRoot * dxRoot + dzRoot * dzRoot < minSq)
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
        Transform rootZonaRio,
        out GameObject willGo,
        out GameObject estelaGo,
        out GameObject liamGo,
        out GameObject archimagoGo,
        out GameObject tobiasGo,
        out GameObject cintaRojaWill,
        out GameObject canaWill,
        out GameObject canaTobias,
        out GameObject cuadernoTobias)
    {
        willGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_WILL.prefab",
            "Actor_Will",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_WILL.asset",
            new Vector3(0f, 0f, 4.8f),
            0f);

        estelaGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_ESTELA.prefab",
            "Actor_Estela",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_ESTELA.asset",
            CentroZonaPiedra + new Vector3(0f, 0f, 4.8f),
            180f);

        liamGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_LIAM.prefab",
            "Actor_Liam",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_LIAM.asset",
            new Vector3(-1.2f, 0f, 4.2f),
            15f);

        archimagoGo = InstanciarActor(
            parent,
            "Assets/Prefabs/_WILL_ORIGINAL.prefab",
            "Actor_Archimago",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_ARCHIMAGO.asset",
            CentroZonaPiedra + new Vector3(0f, 0.25f, 3.6f),
            0f);
        archimagoGo.SetActive(false);

        var spArchimago = new GameObject("Spawn_Archimago");
        spArchimago.transform.SetParent(parent, false);
        spArchimago.transform.position = CentroZonaPiedra + new Vector3(0f, 0.25f, 3.6f);
        spArchimago.AddComponent<NpcSpawnPoint>().spawnId = "Actor_Archimago";

        tobiasGo = InstanciarActor(
            parent,
            "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab",
            "Actor_Tobias",
            "Assets/_NPCs/_GrafoNarrativo/CHAR_TOBI.asset",
            CentroZonaRio + new Vector3(0.3f, 0f, 0.0f),
            175f);
        tobiasGo.SetActive(false);

        var spTobias = new GameObject("Spawn_Tobias");
        spTobias.transform.SetParent(parent, false);
        spTobias.transform.position = CentroZonaRio + new Vector3(0.3f, 0f, 0.0f);
        spTobias.transform.rotation = Quaternion.Euler(0f, 175f, 0f);
        spTobias.AddComponent<NpcSpawnPoint>().spawnId = "Actor_Tobias";

        // Cinta roja de Estela en la muñeca derecha de Will
        cintaRojaWill = CrearCintaRojaEnMuneca(willGo.transform);

        // Caña de pescar de Will sujeta en su mano derecha
        canaWill = CrearCanaDePescarEnMano(willGo.transform, "CanaPescar_Will");
        canaWill.SetActive(false);

        // Caña de pescar de Tobías apoyada en la hierba junto a él mientras dibuja
        canaTobias = CrearCanaApoyadaEnOrilla(rootZonaRio, "CanaPescar_Tobias", new Vector3(-1.05f, 0.04f, -0.35f), Quaternion.Euler(84f, 155f, 10f));
        canaTobias.SetActive(false);

        // Cuaderno de dibujo abierto en la mano de Tobías
        cuadernoTobias = CrearCuadernoDeDibujoEnMano(tobiasGo.transform, "Cuaderno_Dibujo_Tobias");
        cuadernoTobias.SetActive(false);
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

        var anims = instancia.GetComponentsInChildren<Animator>(true);
        Animator animValido = null;
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] != null && anims[i].gameObject.activeInHierarchy && anims[i].runtimeAnimatorController != null)
            {
                animValido = anims[i];
                break;
            }
        }
        if (animValido == null)
        {
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] != null && anims[i].runtimeAnimatorController != null)
                {
                    animValido = anims[i];
                    break;
                }
            }
        }

        var simpleAnim = instancia.GetComponent<NPCSimpleAnimator>();
        if (simpleAnim == null)
            simpleAnim = instancia.AddComponent<NPCSimpleAnimator>();

        if (animValido != null)
        {
            var soAnim = new SerializedObject(simpleAnim);
            soAnim.FindProperty("animator").objectReferenceValue = animValido;
            soAnim.ApplyModifiedPropertiesWithoutUndo();

            var rootAnim = instancia.GetComponent<Animator>();
            if (rootAnim != null && rootAnim != animValido)
            {
                rootAnim.enabled = false;
            }
        }

        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] == null) continue;
            if (anims[i].runtimeAnimatorController == null || (animValido != null && anims[i] != animValido))
            {
                anims[i].enabled = false;
            }
        }

        // Asegurar que todos los MeshRenderer de ojos y bocas tengan enabled=true y que la cara de reposo esté activa
        foreach (var tr in instancia.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null) continue;
            string nTr = tr.name;
            if (nTr.StartsWith("Eye0", StringComparison.Ordinal) || nTr.StartsWith("Eye1", StringComparison.Ordinal) ||
                nTr.StartsWith("Mouth0", StringComparison.Ordinal) || nTr.StartsWith("Mouth1", StringComparison.Ordinal))
            {
                var rTr = tr.GetComponent<Renderer>();
                if (rTr != null && !rTr.enabled) rTr.enabled = true;
            }
        }
        foreach (var emo in instancia.GetComponentsInChildren<NPCEmotionController>(true))
        {
            if (emo == null) continue;
            var soEmo = new SerializedObject(emo);
            var pOjos = soEmo.FindProperty("ojosDeReposo")?.objectReferenceValue as GameObject;
            var pBoca = soEmo.FindProperty("bocaDeReposo")?.objectReferenceValue as GameObject;
            if (pOjos != null) pOjos.SetActive(true);
            if (pBoca != null) pBoca.SetActive(true);
        }

        return instancia;
    }

    private static void DesactivarComponentesDeGameplayEnActor(GameObject actor)
    {
        foreach (var comp in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            string n = comp.GetType().Name;
            if (n.Contains("Input") ||
                n.Contains("Controller") && !n.Contains("Animator") && !n.Contains("AlertIcon") && !n.Contains("Emotion") ||
                n.Contains("Brain") ||
                n.Contains("BehaviourManager"))
            {
                comp.enabled = false;
            }
        }

        var anims = actor.GetComponentsInChildren<Animator>(true);
        bool controllerPrincipalEncontrado = false;
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;
            if (a.runtimeAnimatorController == null || !a.gameObject.activeInHierarchy)
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
        Transform parentHueso = null;
        var anim = willRoot.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            parentHueso = anim.GetBoneTransform(HumanBodyBones.RightHand) ?? anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }
        if (parentHueso == null)
        {
            // Búsqueda en rig genérico por nombre de hueso de la mano / antebrazo derecho
            foreach (var tr in willRoot.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == "hand_r" || tr.name == "lowerarm_r" || tr.name == "RightHand" || tr.name == "RightArm")
                {
                    parentHueso = tr;
                    break;
                }
            }
        }
        if (parentHueso == null)
        {
            // Si no se encuentra el hueso de la mano, NUNCA crear un cilindro en el suelo
            return null;
        }

        var cinta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cinta.name = "Cinta_Roja_Estela";
        UnityEngine.Object.DestroyImmediate(cinta.GetComponent<Collider>());
        cinta.transform.SetParent(parentHueso, false);
        cinta.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        cinta.transform.localScale = new Vector3(0.06f, 0.015f, 0.06f);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var matRojo = new Material(sh)
        {
            color = new Color(0.86f, 0.12f, 0.16f)
        };
        cinta.GetComponent<MeshRenderer>().sharedMaterial = matRojo;
        return cinta;
    }

    private static GameObject CrearCanaDePescarEnMano(Transform actorRoot, string nombre)
    {
        Transform parentHueso = actorRoot;
        var anim = actorRoot.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var mano = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (mano != null) parentHueso = mano;
        }

        var prefabFishingRod = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_FishingRod_01.prefab");
        GameObject root = new GameObject(nombre);
        root.transform.SetParent(parentHueso, false);
        if (parentHueso != actorRoot)
        {
            root.transform.localPosition = new Vector3(0.02f, 0.04f, 0.02f);
            root.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        }
        else
        {
            root.transform.localPosition = new Vector3(0.24f, 0.88f, 0.25f);
            root.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
        }

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
            vara.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            vara.transform.localScale = new Vector3(0.025f, 0.85f, 0.025f);
        }
        return root;
    }

    private static GameObject CrearCanaApoyadaEnOrilla(Transform parentZona, string nombre, Vector3 localPos, Quaternion localRot)
    {
        var prefabFishingRod = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_FishingRod_01.prefab");
        GameObject root = new GameObject(nombre);
        root.transform.SetParent(parentZona, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = localRot;

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
            vara.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            vara.transform.localScale = new Vector3(0.025f, 0.85f, 0.025f);
        }
        return root;
    }

    private static GameObject CrearCuadernoDeDibujoEnMano(Transform tobiasRoot, string nombre)
    {
        Transform parentHueso = tobiasRoot;
        var anim = tobiasRoot.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var manoIzq = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (manoIzq != null) parentHueso = manoIzq;
        }

        var cuaderno = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cuaderno.name = nombre;
        UnityEngine.Object.DestroyImmediate(cuaderno.GetComponent<Collider>());
        cuaderno.transform.SetParent(parentHueso, false);
        if (parentHueso != tobiasRoot)
        {
            cuaderno.transform.localPosition = new Vector3(0.06f, 0.04f, 0.05f);
            cuaderno.transform.localRotation = Quaternion.Euler(25f, 15f, -10f);
        }
        else
        {
            cuaderno.transform.localPosition = new Vector3(-0.12f, 0.48f, 0.35f);
            cuaderno.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        }
        cuaderno.transform.localScale = new Vector3(0.22f, 0.02f, 0.16f);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var matPapel = new Material(sh) { color = new Color(0.95f, 0.91f, 0.80f) };
        cuaderno.GetComponent<MeshRenderer>().sharedMaterial = matPapel;
        return cuaderno;
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
        Vector3 posPortalLuz = portalTr != null ? portalTr.position + Vector3.up * 1.6f : new Vector3(0f, 1.6f, 8.2f);

        vfxDeseoAltar = InstanciarVfx(parent, "VFX_DeseoAltar", "Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab", posAltarLuz);
        vfxPortal = InstanciarVfx(parent, "VFX_PortalUmbral", "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab", posPortalLuz);
        vfxPortal.SetActive(true);
        vfxEmpujeLuz = InstanciarVfx(parent, "VFX_EmpujeLuz", "Assets/_VFX/Prologo/VFX_LuzProtectora.prefab", posPortalLuz);

        // Usar Magic circle y FX_LightPillar en lugar de Healing circle / Buff.prefab para que NUNCA salgan iconos de flechas RPG [↑]
        vfxCirculoConjuro = InstanciarVfx(parent, "VFX_CirculoConjuroAnime", "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab", new Vector3(0f, 0.05f, 4.2f));
        vfxAuraConjuroWill = InstanciarVfx(parent, "VFX_AuraConjuroWill", "Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab", new Vector3(0f, 0.05f, 4.2f));
        vfxPilarResurreccion = InstanciarVfx(parent, "VFX_PilarResurreccion", "Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab", new Vector3(-1.2f, 0.05f, 4.2f));
        vfxResurreccion = InstanciarVfx(parent, "VFX_ResurreccionLiam", "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab", new Vector3(-1.2f, 0.1f, 4.2f));
        vfxColapsoSendero = InstanciarVfx(parent, "VFX_ColapsoSendero", "Assets/Prefabs/Effects/SpecialAttack/VFX_TormentaArcana.prefab", new Vector3(0f, 1.2f, 0.5f));
        vfxEspirituArchimago = new GameObject("VFX_EspirituArchimago");
        vfxEspirituArchimago.transform.SetParent(parent, false);
        vfxEspirituArchimago.SetActive(false);

        // Will regresa en persona bajo las estrellas; no se coloca humo ni hogueras de VFX de fuego procedural
        vfxLlamaWill = null;
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
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            if (col != null) col.enabled = false;
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
        Light luzNocheRioGrupo,
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
        GameObject cuadernoTobias,
        GameObject vfxSalpicaduraRio,
        GameObject estrellaTorcidaGo,
        GameObject estrellasNocturnasRoot,
        GameObject portalUmbralSendero,
        GameObject portalUmbralPiedra,
        GameObject ventanaPortalSendero,
        GameObject ventanaPortalPiedra,
        Camera camPortalSendero,
        Camera camPortalPiedra)
    {
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

        var soDriver = new SerializedObject(camDriver);
        var propCam = soDriver.FindProperty("_managedCamera");
        if (propCam != null) propCam.objectReferenceValue = cam;
        soDriver.ApplyModifiedPropertiesWithoutUndo();

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
        soModulo.FindProperty("_zonaSenderoRoot").objectReferenceValue = GameObject.Find("ZONA_1_EL_SENDERO");
        soModulo.FindProperty("_zonaPiedraRoot").objectReferenceValue = GameObject.Find("ZONA_2_PIEDRA_ANCESTRAL");
        soModulo.FindProperty("_zonaRioRoot").objectReferenceValue = GameObject.Find("ZONA_3_EL_RIO");

        soModulo.FindProperty("_vfxDeseoAltar").objectReferenceValue = vfxDeseoAltar;
        soModulo.FindProperty("_vfxPortal").objectReferenceValue = vfxPortal;
        soModulo.FindProperty("_vfxEmpujeLuz").objectReferenceValue = vfxEmpujeLuz;
        soModulo.FindProperty("_vfxCirculoConjuro").objectReferenceValue = vfxCirculoConjuro;
        soModulo.FindProperty("_vfxAuraConjuroWill").objectReferenceValue = vfxAuraConjuroWill;
        soModulo.FindProperty("_vfxPilarResurreccion").objectReferenceValue = vfxPilarResurreccion;
        soModulo.FindProperty("_vfxResurreccion").objectReferenceValue = vfxResurreccion;
        soModulo.FindProperty("_vfxColapsoSendero").objectReferenceValue = vfxColapsoSendero;

        soModulo.FindProperty("_vfxEspirituArchimago").objectReferenceValue = vfxEspirituArchimago;
        soModulo.FindProperty("_cintaRojaWill").objectReferenceValue = cintaRojaWill;

        soModulo.FindProperty("_canaPescarWill").objectReferenceValue = canaWill;
        soModulo.FindProperty("_canaPescarTobias").objectReferenceValue = canaTobias;
        var propCuaderno = soModulo.FindProperty("_cuadernoDibujoTobias");
        if (propCuaderno != null) propCuaderno.objectReferenceValue = cuadernoTobias;
        soModulo.FindProperty("_vfxSalpicaduraRio").objectReferenceValue = vfxSalpicaduraRio;
        soModulo.FindProperty("_luzSolar").objectReferenceValue = sunLight;
        var propLuzNoche = soModulo.FindProperty("_luzNocheRioGrupo");
        if (propLuzNoche != null) propLuzNoche.objectReferenceValue = luzNocheRioGrupo;

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

        soModulo.FindProperty("_portalUmbralSendero").objectReferenceValue = portalUmbralSendero;
        soModulo.FindProperty("_portalUmbralPiedra").objectReferenceValue = portalUmbralPiedra;
        soModulo.FindProperty("_ventanaPortalSendero").objectReferenceValue = ventanaPortalSendero;
        soModulo.FindProperty("_ventanaPortalPiedra").objectReferenceValue = ventanaPortalPiedra;
        soModulo.FindProperty("_camPortalSendero").objectReferenceValue = camPortalSendero;
        soModulo.FindProperty("_camPortalPiedra").objectReferenceValue = camPortalPiedra;
        soModulo.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.skybox = skyNoche;

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
            mat.SetColor("_ColorCenit", new Color(0.015f, 0.022f, 0.085f, 1.0f));
            mat.SetColor("_ColorHorizonte", new Color(0.045f, 0.065f, 0.18f, 1.0f));
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
        elem.FindPropertyRelative("emotionController").objectReferenceValue = null;
    }
}
#endif
