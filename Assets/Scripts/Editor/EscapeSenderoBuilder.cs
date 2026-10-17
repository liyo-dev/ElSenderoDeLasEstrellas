using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera el nivel jugable de huida del Sendero de las Estrellas mientras colapsa.
/// Clona la estética, cielo, iluminación cósmica y plataformas reales de Sendero.unity,
/// integrando el escape en sentido inverso desde el Altar Blanco (Z=266) hasta el Umbral (Z=-6).
/// Resuelve las 3 secciones con plataformeo atlético (sin vuelo), dianas en el aire que abren puentes,
/// elevador astral y porteo de Liam por parte de Estela.
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

    // VFX
    private const string VfxSalto = "Assets/VFX/Free Game VFX/Prefab/FX_Greenlight_shrink.prefab";

    // Materiales auténticos de Sendero
    private const string MatCristalPath = "Assets/Art/Materials/Sendero/Mat_Sendero_CristalAstral.mat";
    private const string MatOroPath = "Assets/Art/Materials/Sendero/Mat_OroAstral.mat";
    private const string MatAltarPath = "Assets/Art/Materials/Sendero/Mat_AltarBlancoPuro.mat";
    private const string MatBraseroPath = "Assets/Scenes/Test/CombatLabMaterials/Lab_Brasero.mat";

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

        // 2. Limpiar elementos de la historia previa y piezas que se han derrumbado
        LimpiarObjetoEnEscena(escena, "TRIGGERS_DE_PROGRESION");
        LimpiarObjetoEnEscena(escena, "SENDERO_PROGRESSION_CONTROLLER");
        LimpiarObjetoEnEscena(escena, "_WILL");
        LimpiarObjetoEnEscena(escena, "_ESTELA");
        LimpiarObjetoEnEscena(escena, "_LIAM");
        LimpiarObjetoEnEscena(escena, "CamaraDelJugador");
        LimpiarObjetoEnEscena(escena, "ESCAPE_MECANICAS");
        LimpiarObjetoEnEscena(escena, "ESCAPE_NIVEL_CONTROLLER");
        LimpiarObjetoEnEscena(escena, "BOOTSTRAP_ESCAPE");

        // Retirar los 6 tramos de calzada que han colapsado en el abismo para crear los 3 retos reales
        LimpiarObjetoEnEscena(escena, "Losa_Constelacion_1");
        LimpiarObjetoEnEscena(escena, "Losa_Constelacion_2");
        LimpiarObjetoEnEscena(escena, "Losa_Constelacion_3");
        LimpiarObjetoEnEscena(escena, "Losa_Constelacion_4");
        LimpiarObjetoEnEscena(escena, "Losa_Constelacion_5");
        LimpiarObjetoEnEscena(escena, "Suelo_05_CorteDelCamino");
        LimpiarObjetoEnEscena(escena, "Suelo_03_CaminoLiam_A");
        LimpiarObjetoEnEscena(escena, "Suelo_03_CaminoLiam_B");
        LimpiarObjetoEnEscena(escena, "Suelo_02_CaminoEstela_A");
        LimpiarObjetoEnEscena(escena, "Suelo_01_CaminoWill_A");

        // Materiales
        var matCristal = Cargar<Material>(MatCristalPath, avisos) ?? CrearMatFallback("Mat_Sendero_CristalAstral", new Color(0.75f, 0.85f, 1f));
        var matOro = Cargar<Material>(MatOroPath, avisos) ?? CrearMatFallback("Mat_OroAstral", new Color(1f, 0.85f, 0.4f));
        var matBrasero = Cargar<Material>(MatBraseroPath, avisos) ?? CrearMatFallback("Lab_Brasero", new Color(0.95f, 0.45f, 0.2f), new Color(0.8f, 0.3f, 0.1f));
        var vfxSaltoPrefab = Cargar<GameObject>(VfxSalto, avisos);

        // Raíces del controlador y mecánicas de escape
        var raizMecanicas = new GameObject("ESCAPE_MECANICAS");
        var nivelCtrlObj = new GameObject("ESCAPE_NIVEL_CONTROLLER");
        var nivelCtrl = nivelCtrlObj.AddComponent<NivelEscapeSendero>();

        // Checkpoints de Will a lo largo del Sendero (en sentido Altar Z=266 -> Umbral Z=-6)
        var cp0 = new GameObject("Checkpoint 0 (Altar)").transform;
        cp0.SetPositionAndRotation(new Vector3(0f, 2.30f, 258f), Quaternion.Euler(0f, 180f, 0f));
        cp0.SetParent(nivelCtrlObj.transform);

        var cp1 = new GameObject("Checkpoint 1 (Reconciliacion Z208)").transform;
        cp1.SetPositionAndRotation(new Vector3(0f, 1.60f, 208f), Quaternion.Euler(0f, 180f, 0f));
        cp1.SetParent(nivelCtrlObj.transform);

        var cp2 = new GameObject("Checkpoint 2 (Mirador Z128)").transform;
        cp2.SetPositionAndRotation(new Vector3(8f, 2.90f, 128f), Quaternion.Euler(0f, 180f, 0f));
        cp2.SetParent(nivelCtrlObj.transform);

        var cp3 = new GameObject("Checkpoint 3 (Llegada Z6.5)").transform;
        cp3.SetPositionAndRotation(new Vector3(0f, 0.10f, 6.5f), Quaternion.Euler(0f, 180f, 0f));
        cp3.SetParent(nivelCtrlObj.transform);

        // Puntos seguros para el avance de Estela con Liam
        var puntoEstela1 = new GameObject("Punto Estela 1 (Z208)").transform;
        puntoEstela1.SetPositionAndRotation(new Vector3(0f, 1.60f, 208f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela1.SetParent(nivelCtrlObj.transform);

        var puntoEstela2 = new GameObject("Punto Estela 2 (Z128)").transform;
        puntoEstela2.SetPositionAndRotation(new Vector3(8f, 2.90f, 128f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela2.SetParent(nivelCtrlObj.transform);

        var puntoEstela3 = new GameObject("Punto Estela 3 (Umbral Z-4)").transform;
        puntoEstela3.SetPositionAndRotation(new Vector3(0f, 0.10f, -4f), Quaternion.Euler(0f, 180f, 0f));
        puntoEstela3.SetParent(nivelCtrlObj.transform);

        // ── Sección 1: El Gran Abismo del Altar a la Terraza Astral (Z: 254 a 230) ───
        // Plataforma de salto en el borde del Altar con halo luminoso
        CrearLanzador("Lanzador 1 (Salto al Vacio Z253)", raizMecanicas.transform,
            new Vector3(0f, 2.30f, 253.5f), rotacionY: 180f, vertical: 18f, forward: 16f, vfxSaltoPrefab, matCristal, matOro);

        // Monolito de Luz con pedestal de cristal y núcleo brillante en Z=222
        var receptorLuz = CrearMonolitoReceptor("Monolito de Luz Astral", raizMecanicas.transform,
            new Vector3(0f, 1.50f, 222f), alturaColumna: 5.5f, escalaOrbe: 1.6f,
            matCristal, matOro, new Color(1f, 0.95f, 0.55f), MagicElement.Light);

        // Puente temporal de 3 grandes losas hexagonales de cristal que salvan el abismo
        var temporalesPuenteLuz = new List<PlataformaTemporal>();
        float[] zHex = { 247f, 239f, 231f };
        for (int i = 0; i < zHex.Length; i++)
        {
            var losa = CrearBloque($"Hexagono Luz {i + 1}", raizMecanicas.transform,
                new Vector3(0f, 2.05f - (i * 0.18f), zHex[i]), new Vector3(5f, 0.4f, 5f), matCristal, capaSuelo: true);
            var temp = losa.AddComponent<PlataformaTemporal>();
            temporalesPuenteLuz.Add(temp);
            UnityEventTools.AddPersistentListener(receptorLuz.alActivarse, temp.Mostrar);
            UnityEventTools.AddPersistentListener(receptorLuz.alApagarse, temp.Ocultar);
        }
        UnityEventTools.AddPersistentListener(receptorLuz.alActivarse, nivelCtrl.ActivarPuenteSeccion1);

        // ── Sección 2: El Desnivel, Pebeteros Elementales y Elevador (Z: 186 a 128) ──
        // Elevador de cristal para Estela y Liam
        var elevadorObj = CrearBloque("Elevador Astral Estela", raizMecanicas.transform,
            new Vector3(3f, 1.5f, 170f), new Vector3(6f, 0.5f, 6f), matCristal, capaSuelo: true);
        var elevador = elevadorObj.AddComponent<PlatformElevator>();
        var soElev = new SerializedObject(elevador);
        soElev.FindProperty("raiseHeight").floatValue = 5.0f;
        soElev.FindProperty("moveSpeed").floatValue = 3.0f;
        soElev.ApplyModifiedPropertiesWithoutUndo();

        // Peldaños de acceso para Will hacia las terrazas laterales
        CrearBloque("Paso Astral Izq", raizMecanicas.transform, new Vector3(-2.5f, 1.8f, 175f), new Vector3(3.2f, 0.4f, 3.2f), matCristal, capaSuelo: true);
        CrearBloque("Paso Astral Der", raizMecanicas.transform, new Vector3(4.5f, 2.0f, 175f), new Vector3(3.2f, 0.4f, 3.2f), matCristal, capaSuelo: true);

        // Terraza y Pebetero de Fuego en X=-5.5
        var pilarFuego = CrearMonolitoReceptor("Pebetero de Fuego", raizMecanicas.transform,
            new Vector3(-5.5f, 1.5f, 165f), alturaColumna: 4.5f, escalaOrbe: 1.4f,
            matCristal, matBrasero, new Color(1f, 0.45f, 0.15f), MagicElement.Fire);

        // Terraza y Molinete de Viento en X=+7.0
        var molineteViento = CrearMonolitoReceptor("Molinete de Viento", raizMecanicas.transform,
            new Vector3(7.0f, 2.0f, 165f), alturaColumna: 4.5f, escalaOrbe: 1.4f,
            matCristal, matOro, new Color(0.5f, 1f, 0.9f), MagicElement.Storm);

        nivelCtrl.ElevadorAstral = elevador;
        UnityEventTools.AddPersistentListener(pilarFuego.alActivarse, nivelCtrl.ActivarAvanceSeccion2);
        UnityEventTools.AddPersistentListener(molineteViento.alActivarse, nivelCtrl.ActivarAvanceSeccion2);

        // ── Sección 3: Islas de Cristal Aéreas hacia el Umbral (Z: 120 a -6) ────────
        CrearBloque("Isla Lanzador 3A", raizMecanicas.transform, new Vector3(4f, 2.2f, 96f), new Vector3(4.5f, 0.6f, 4.5f), matCristal, capaSuelo: true);
        CrearLanzador("Lanzador Aéreo 3A", raizMecanicas.transform, new Vector3(4f, 2.55f, 96f), 180f, 16f, 16f, vfxSaltoPrefab, matCristal, matOro);

        CrearBloque("Isla Lanzador 3B", raizMecanicas.transform, new Vector3(-2f, 1.6f, 62f), new Vector3(4.5f, 0.6f, 4.5f), matCristal, capaSuelo: true);
        CrearLanzador("Lanzador Aéreo 3B", raizMecanicas.transform, new Vector3(-2f, 1.95f, 62f), 180f, 16f, 16f, vfxSaltoPrefab, matCristal, matOro);

        CrearBloque("Isla Lanzador 3C", raizMecanicas.transform, new Vector3(1f, 0.8f, 28f), new Vector3(4.5f, 0.6f, 4.5f), matCristal, capaSuelo: true);
        CrearLanzador("Lanzador Aéreo 3C", raizMecanicas.transform, new Vector3(1f, 1.15f, 28f), 180f, 15f, 16f, vfxSaltoPrefab, matCristal, matOro);

        // Runa Maestra de Retorno frente al Umbral en Z=4
        var runaPuenteFinal = CrearMonolitoReceptor("Runa Maestra de Retorno", raizMecanicas.transform,
            new Vector3(2.5f, 0.1f, 4.0f), alturaColumna: 3.5f, escalaOrbe: 1.4f,
            matCristal, matOro, new Color(1f, 0.9f, 0.35f), MagicElement.Light);

        // Gran Calzada Dorada que se extiende cruzando el abismo para Estela
        var calzadaDorada = CrearBloque("Gran Calzada Dorada", raizMecanicas.transform,
            new Vector3(0f, 1.0f, 60f), new Vector3(5f, 0.4f, 126f), matOro, capaSuelo: true);
        calzadaDorada.SetActive(false);

        nivelCtrl.PuenteFinalVisual = calzadaDorada;
        UnityEventTools.AddPersistentListener(runaPuenteFinal.alActivarse, nivelCtrl.ActivarPuenteFinalUmbral);

        // Trigger de fin de nivel en Portal_00_UmbralTierra (Z=-6)
        var triggerMeta = new GameObject("Trigger_MetaUmbral");
        triggerMeta.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -6f), Quaternion.identity);
        triggerMeta.transform.SetParent(raizMecanicas.transform);
        var colMeta = triggerMeta.AddComponent<BoxCollider>();
        colMeta.isTrigger = true;
        colMeta.size = new Vector3(10f, 6f, 4f);
        var triggerEvento = triggerMeta.AddComponent<TriggerDeMeta>();
        triggerEvento.nivelCtrl = nivelCtrl;

        // ── Personajes y Porteo Calibrado en el Altar ────────────────────────────
        var jugador = (GameObject)PrefabUtility.InstantiatePrefab(willPrefab, escena);
        jugador.name = "_WILL";
        jugador.transform.SetPositionAndRotation(new Vector3(0f, 2.30f, 258f), Quaternion.Euler(0f, 180f, 0f));

        var estela = (GameObject)PrefabUtility.InstantiatePrefab(estelaPrefab, escena);
        estela.name = "_ESTELA";
        estela.transform.SetPositionAndRotation(new Vector3(-1.8f, 2.30f, 259f), Quaternion.Euler(0f, 180f, 0f));

        var liam = (GameObject)PrefabUtility.InstantiatePrefab(liamPrefab, escena);
        liam.name = "_LIAM";
        liam.transform.SetPositionAndRotation(new Vector3(-1.8f, 2.30f, 259f), Quaternion.Euler(0f, 180f, 0f));

        var porteo = estela.AddComponent<PorteoDeCompanero>();
        var soPorteo = new SerializedObject(porteo);
        soPorteo.FindProperty("cuerpo").objectReferenceValue = liam.transform;
        soPorteo.FindProperty("offsetRotacion").vector3Value = Vector3.zero;
        soPorteo.FindProperty("offsetPosicion").vector3Value = new Vector3(-0.08f, -0.16f, 0f);
        soPorteo.ApplyModifiedPropertiesWithoutUndo();

        // Cámara del jugador mirando al sur (siguiendo el escape hacia Z=-6)
        if (camaraPrefab != null)
        {
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camaraPrefab, escena);
            cam.name = "CamaraDelJugador";
            cam.transform.SetPositionAndRotation(new Vector3(0f, 4.0f, 264f), Quaternion.Euler(14f, 180f, 0f));
        }

        // Bootstrap limpio de Escape: registra a Will, configura habilidades sin vuelo y oculta HUD
        var inicio = new GameObject("BOOTSTRAP_ESCAPE").AddComponent<EscapeSenderoBootstrap>();
        var soInicio = new SerializedObject(inicio);
        soInicio.FindProperty("player").objectReferenceValue = jugador;
        soInicio.ApplyModifiedPropertiesWithoutUndo();

        // Conectar referencias en NivelEscapeSendero
        var soNivel = new SerializedObject(nivelCtrl);
        soNivel.FindProperty("jugador").objectReferenceValue = jugador.transform;
        soNivel.FindProperty("porteoEstela").objectReferenceValue = porteo;
        soNivel.FindProperty("elevadorAstral").objectReferenceValue = elevador;
        soNivel.FindProperty("puenteFinalVisual").objectReferenceValue = calzadaDorada;

        var puntosProp = soNivel.FindProperty("puntosSegurosEstela");
        puntosProp.arraySize = 3;
        puntosProp.GetArrayElementAtIndex(0).objectReferenceValue = puntoEstela1;
        puntosProp.GetArrayElementAtIndex(1).objectReferenceValue = puntoEstela2;
        puntosProp.GetArrayElementAtIndex(2).objectReferenceValue = puntoEstela3;

        var cpsProp = soNivel.FindProperty("checkpointsWill");
        cpsProp.arraySize = 4;
        cpsProp.GetArrayElementAtIndex(0).objectReferenceValue = cp0;
        cpsProp.GetArrayElementAtIndex(1).objectReferenceValue = cp1;
        cpsProp.GetArrayElementAtIndex(2).objectReferenceValue = cp2;
        cpsProp.GetArrayElementAtIndex(3).objectReferenceValue = cp3;

        soNivel.ApplyModifiedPropertiesWithoutUndo();

        // Guardar escena final clonada y enriquecida
        EditorSceneManager.SaveScene(escena, ScenePath);
        AssetDatabase.SaveAssets();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EscapeSenderoBuilder] ¡Nivel de Escape generado con éxito basado 100% en Sendero.unity!");
#endif
    }

    private static void LimpiarObjetoEnEscena(Scene escena, string nombre)
    {
        foreach (var root in escena.GetRootGameObjects())
        {
            if (root.name == nombre)
            {
                Object.DestroyImmediate(root);
                return;
            }
            var h = root.transform.Find(nombre);
            if (h != null)
            {
                Object.DestroyImmediate(h.gameObject);
                return;
            }
        }
    }

    private static GameObject CrearBloque(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat, bool capaSuelo = false)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = nombre;
        obj.transform.SetParent(padre);
        obj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        obj.transform.localScale = escala;
        var r = obj.GetComponent<Renderer>();
        if (r && mat) r.sharedMaterial = mat;
        if (capaSuelo) obj.layer = LayerMask.NameToLayer("Floor");
        return obj;
    }

    private static LanzadorDeSalto CrearLanzador(string nombre, Transform padre, Vector3 pos, float rotacionY, float vertical, float forward,
        GameObject vfxPrefab, Material matBase, Material matBorde)
    {
        var raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre);
        raiz.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotacionY, 0f));

        // Base estética de cristal
        var baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseVisual.name = "Base_Cristal";
        baseVisual.transform.SetParent(raiz.transform, false);
        baseVisual.transform.localScale = new Vector3(2.6f, 0.08f, 2.6f);
        if (matBase && baseVisual.GetComponent<Renderer>()) baseVisual.GetComponent<Renderer>().sharedMaterial = matBase;
        Object.DestroyImmediate(baseVisual.GetComponent<Collider>());

        // Anillo de oro astral
        var anillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anillo.name = "Anillo_Oro";
        anillo.transform.SetParent(raiz.transform, false);
        anillo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        anillo.transform.localScale = new Vector3(2.0f, 0.09f, 2.0f);
        if (matBorde && anillo.GetComponent<Renderer>()) anillo.GetComponent<Renderer>().sharedMaterial = matBorde;
        Object.DestroyImmediate(anillo.GetComponent<Collider>());

        // Trigger físico y lógica del lanzador
        var colTrigger = raiz.AddComponent<CapsuleCollider>();
        colTrigger.isTrigger = true;
        colTrigger.radius = 1.3f;
        colTrigger.height = 1.5f;
        colTrigger.center = new Vector3(0f, 0.75f, 0f);

        var lanzador = raiz.AddComponent<LanzadorDeSalto>();
        var so = new SerializedObject(lanzador);
        so.FindProperty("velocidadVertical").floatValue = vertical;
        so.FindProperty("empujeHaciaDelante").floatValue = forward;
        if (vfxPrefab) so.FindProperty("vfxDeImpulso").objectReferenceValue = vfxPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Luz tenue para señalizar la runa en el suelo
        var luz = raiz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.4f, 1f, 0.8f);
        luz.range = 4f;
        luz.intensity = 1.5f;

        return lanzador;
    }

    private static ReceptorDeMagia CrearMonolitoReceptor(string nombre, Transform padre, Vector3 pos, float alturaColumna, float escalaOrbe,
        Material matColumna, Material matOrbe, Color colorLuz, MagicElement elementoPrincipal)
    {
        var raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre);
        raiz.transform.SetPositionAndRotation(pos, Quaternion.identity);

        // Columna de cristal
        var columna = GameObject.CreatePrimitive(PrimitiveType.Cube);
        columna.name = "Columna_Cristal";
        columna.transform.SetParent(raiz.transform, false);
        columna.transform.localPosition = new Vector3(0f, alturaColumna * 0.5f, 0f);
        columna.transform.localScale = new Vector3(1.8f, alturaColumna, 1.8f);
        if (matColumna && columna.GetComponent<Renderer>()) columna.GetComponent<Renderer>().sharedMaterial = matColumna;

        // Anillo superior
        var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collar.name = "Collar_Oro";
        collar.transform.SetParent(raiz.transform, false);
        collar.transform.localPosition = new Vector3(0f, alturaColumna + 0.1f, 0f);
        collar.transform.localScale = new Vector3(2.4f, 0.25f, 2.4f);
        if (matOrbe && collar.GetComponent<Renderer>()) collar.GetComponent<Renderer>().sharedMaterial = matOrbe;

        // Orbe flotante receptor de magia
        var orbe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbe.name = "Orbe_Receptor";
        orbe.transform.SetParent(raiz.transform, false);
        orbe.transform.localPosition = new Vector3(0f, alturaColumna + 0.9f + (escalaOrbe * 0.5f), 0f);
        orbe.transform.localScale = Vector3.one * escalaOrbe;

        var r = orbe.GetComponent<Renderer>();
        if (r && matOrbe) r.sharedMaterial = matOrbe;

        var col = orbe.GetComponent<SphereCollider>();
        if (col) col.radius = 0.85f;

        // Luz de señalización
        var luzObj = new GameObject("Luz");
        luzObj.transform.SetParent(orbe.transform, false);
        var l = luzObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = colorLuz;
        l.range = 10f;
        l.intensity = 3f;

        var receptor = orbe.AddComponent<ReceptorDeMagia>();
        receptor.alActivarse ??= new UnityEvent();
        receptor.alApagarse ??= new UnityEvent();
        var so = new SerializedObject(receptor);

        // Acepta el elemento principal y también los demás básicos para garantizar que Will nunca se bloquee
        var elemArray = so.FindProperty("elementosAceptados");
        elemArray.ClearArray();
        elemArray.InsertArrayElementAtIndex(0);
        elemArray.GetArrayElementAtIndex(0).enumValueIndex = (int)elementoPrincipal;
        elemArray.InsertArrayElementAtIndex(1);
        elemArray.GetArrayElementAtIndex(1).enumValueIndex = (int)MagicElement.Light;
        elemArray.InsertArrayElementAtIndex(2);
        elemArray.GetArrayElementAtIndex(2).enumValueIndex = (int)MagicElement.Fire;
        elemArray.InsertArrayElementAtIndex(3);
        elemArray.GetArrayElementAtIndex(3).enumValueIndex = (int)MagicElement.Storm;

        so.FindProperty("modo").enumValueIndex = (int)ReceptorDeMagia.Modo.Permanente;
        so.FindProperty("segundosEncendido").floatValue = 0f;
        so.FindProperty("colorEncendido").colorValue = colorLuz;
        so.FindProperty("luz").objectReferenceValue = l;

        var brp = so.FindProperty("brillo");
        brp.ClearArray();
        brp.InsertArrayElementAtIndex(0);
        brp.GetArrayElementAtIndex(0).objectReferenceValue = r;

        so.ApplyModifiedPropertiesWithoutUndo();
        return receptor;
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
