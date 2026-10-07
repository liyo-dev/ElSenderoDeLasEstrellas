using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera el LAB (escena Lab): el jugador y enemigos reales del proyecto, con la arena de combate
/// en el centro y, alrededor, plataformeo, puzles, agua, vuelo (CombatLabBuilder.Zonas.cs),
/// tiendas (CombatLabBuilder.Economia.cs) y la zona de jefes (CombatLabBuilder.Jefes.cs). No
/// modifica MainWorld, el perfil de arranque ni los prefabs de combate.
/// </summary>
public static partial class CombatLabBuilder
{
    private const string ScenePath = "Assets/Scenes/Test/Lab.unity";
    private const string EscenaAntigua = "Assets/Scenes/Test/CombatLab.unity";
    private const string MaterialsPath = "Assets/Scenes/Test/CombatLabMaterials";

    [MenuItem("El Sendero/Combate/Crear o regenerar el LAB")]
    public static void CrearEscena()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog(
                "Regenerar el LAB",
                "Se reemplazará Lab.unity y se perderán los cambios manuales hechos dentro de esa escena. Los prefabs y niveles del juego no se modifican.",
                "Regenerar", "Cancelar"))
            return;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab");
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("LAB", "No se encontró Assets/Prefabs/_WILL.prefab. No se creó ni modificó ninguna escena.", "Aceptar");
            return;
        }

        AsegurarCarpetaMateriales();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightObject = new GameObject("Luz de CombatLab");
        var sceneLight = lightObject.AddComponent<Light>();
        sceneLight.type = LightType.Directional;
        sceneLight.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -25f, 0f);

        var geometry = new GameObject("LAB_GEOMETRIA_Y_NAVMESH");
        var floorMaterial = GetMaterial("Lab_Suelo", new Color(0.19f, 0.22f, 0.28f));
        var coverMaterial = GetMaterial("Lab_Cobertura", new Color(0.35f, 0.40f, 0.47f));
        var markerMaterial = GetMaterial("Lab_Marcadores", new Color(0.20f, 0.48f, 0.70f));

        CrearCubo("Suelo de pruebas", geometry.transform, new Vector3(0f, -0.25f, 1f),
            new Vector3(28f, 0.5f, 36f), floorMaterial, "Floor");
        // Cada muro deja un hueco en el centro para pasar a la zona de ese lado.
        CrearMuroConHueco("Muro norte", geometry.transform, new Vector3(0f, 1.5f, 19f), 28f, true, coverMaterial);
        CrearMuroConHueco("Muro sur", geometry.transform, new Vector3(0f, 1.5f, -17f), 28f, true, coverMaterial);
        CrearMuroConHueco("Muro este", geometry.transform, new Vector3(14f, 1.5f, 1f), 36f, false, coverMaterial);
        CrearMuroConHueco("Muro oeste", geometry.transform, new Vector3(-14f, 1.5f, 1f), 36f, false, coverMaterial);

        CrearCubo("Cobertura baja izquierda", geometry.transform, new Vector3(-5f, 0.65f, 3f),
            new Vector3(2f, 1.3f, 1.4f), coverMaterial);
        CrearCubo("Cobertura baja derecha", geometry.transform, new Vector3(5f, 0.65f, 5f),
            new Vector3(2f, 1.3f, 1.4f), coverMaterial);
        CrearCubo("Pilar de arena", geometry.transform, new Vector3(0f, 1.5f, 11f),
            new Vector3(1.5f, 3f, 1.5f), coverMaterial);

        CrearMarcador("Puesto 1 — base", geometry.transform, new Vector3(-9f, 0.02f, -12f), markerMaterial);
        CrearMarcador("Puesto 2 — duelo", geometry.transform, new Vector3(-3f, 0.02f, -12f), markerMaterial);
        CrearMarcador("Puesto 3 — grupo", geometry.transform, new Vector3(3f, 0.02f, -12f), markerMaterial);
        CrearMarcador("Puesto 4 — jefe", geometry.transform, new Vector3(9f, 0.02f, -12f), markerMaterial);

        CrearZonas(geometry.transform, floorMaterial, coverMaterial);
        var entradaJefes = CrearZonaDeJefes(geometry.transform, floorMaterial, coverMaterial);

        var navMesh = geometry.AddComponent<NavMeshSurface>();
        navMesh.collectObjects = CollectObjects.Children;
        navMesh.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navMesh.BuildNavMesh();

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "LAB_JUGADOR_WILL";
        player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -8f), Quaternion.identity);

        var camaraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CamaraDelJugador.prefab");
        if (camaraPrefab != null)
        {
            var camara = (GameObject)PrefabUtility.InstantiatePrefab(camaraPrefab, scene);
            camara.transform.SetPositionAndRotation(new Vector3(0f, 2.5f, -12f), Quaternion.identity);
        }

        var grupoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GrupoDelJugador.prefab");
        if (grupoPrefab != null) PrefabUtility.InstantiatePrefab(grupoPrefab, scene);

        var bootstrapObject = new GameObject("LAB_INICIALIZACION");
        var bootstrap = bootstrapObject.AddComponent<CombatLabBootstrap>();
        SerializedObject bootstrapSerialized = new SerializedObject(bootstrap);
        bootstrapSerialized.FindProperty("player").objectReferenceValue = player;
        bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
        var stationsRoot = new GameObject("LAB_ESCENARIOS");
        var stations = new GameObject[4];
        stations[0] = CrearEstacion("F1_Base", stationsRoot.transform);
        var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        dummy.name = "Blanco de práctica — sin IA";
        dummy.transform.SetParent(stations[0].transform);
        dummy.transform.SetPositionAndRotation(new Vector3(0f, 1f, 5f), Quaternion.identity);
        dummy.AddComponent<Damageable>();
        CrearTexto("Base: blanco quieto", stations[0].transform, new Vector3(0f, 3f, 5f));

        stations[1] = CrearEstacion("F2_Duelo", stationsRoot.transform);
        InstanciarEnemigo("Assets/Prefabs/Enemy/Spider1.prefab", "Duelo_Spider", stations[1].transform, new Vector3(0f, 0f, 6f));
        CrearTexto("Duelo: un enemigo", stations[1].transform, new Vector3(0f, 4f, 6f));

        stations[2] = CrearEstacion("F3_Grupo", stationsRoot.transform);
        InstanciarEnemigo("Assets/Prefabs/Enemy/Spider1.prefab", "Grupo_Spider_1", stations[2].transform, new Vector3(-4f, 0f, 7f));
        InstanciarEnemigo("Assets/Prefabs/Enemy/Demon.prefab", "Grupo_Demonio", stations[2].transform, new Vector3(0f, 0f, 9f));
        InstanciarEnemigo("Assets/Prefabs/Enemy/Spider1.prefab", "Grupo_Spider_2", stations[2].transform, new Vector3(4f, 0f, 7f));
        CrearTexto("Grupo: tres enemigos", stations[2].transform, new Vector3(0f, 4f, 8f));

        stations[3] = CrearEstacion("F4_Jefe", stationsRoot.transform);
        InstanciarEnemigo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "Jefe_Golem", stations[3].transform, new Vector3(0f, 0f, 8f));
        CrearTexto("Jefe: Gólem", stations[3].transform, new Vector3(0f, 5f, 8f));

        var directorObject = new GameObject("LAB_CONTROL");
        var director = directorObject.AddComponent<CombatLabDirector>();
        SerializedObject directorSerialized = new SerializedObject(director);
        SerializedProperty stationArray = directorSerialized.FindProperty("stations");
        stationArray.arraySize = stations.Length;
        for (int i = 0; i < stations.Length; i++)
            stationArray.GetArrayElementAtIndex(i).objectReferenceValue = stations[i];
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (var aviso in CrearZonaEconomia(scene))
            Debug.LogWarning("[CombatLab] " + aviso);

        foreach (var aviso in CrearZonaMovilidad(scene))
            Debug.LogWarning("[CombatLab] " + aviso);

        CrearDestinos(entradaJefes);

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Test");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RetirarEscenaAntigua();
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LAB creado (Assets/Scenes/Test/Lab.unity). Tab / Select abre el panel del LAB.");
    }

    /// Destinos del viaje rápido (pestaña Zonas del panel).
    private static void CrearDestinos(Transform entradaJefes)
    {
        var raiz = new GameObject("LAB_DESTINOS");
        var viaje = raiz.AddComponent<ViajeDelLab>();
        var lista = new (string nombre, string queHay, Vector3 pos, float giro)[]
        {
            ("Combate (centro)", "Escenarios F1–F4: blanco, duelo, grupo y Gólem.", new Vector3(0f, 0.1f, -8f), 0f),
            ("Plataformeo (oeste)", "Saltos, doble salto, viga, rampa y escalada.", new Vector3(-18f, 0.1f, 1f), -90f),
            ("Puzles (este)", "Placas, puerta, plataforma que eleva, fuego y runas.", new Vector3(18f, 0.1f, 1f), 90f),
            ("Agua (norte)", "Nadar en lo hondo y en lo poco hondo.", new Vector3(0f, 0.1f, 21f), 0f),
            ("Tiendas y Esencia (sur)", "Renard, Tomasa y placas de +100.", new Vector3(0f, 0.1f, -20f), 180f),
            ("Vuelo (sur)", "Torres y plataformas flotantes a 10, 18 y 26 m.", new Vector3(0f, 0.1f, -35f), 180f),
            ("Arañas y contratos (sur)", "Corral de arañas que reaparecen.", new Vector3(25f, 0.1f, -68f), 180f),
        };
        var so = new SerializedObject(viaje);
        var destinos = so.FindProperty("destinos");
        destinos.arraySize = lista.Length + (entradaJefes != null ? 1 : 0);
        for (int i = 0; i < lista.Length; i++)
        {
            var punto = new GameObject("Destino — " + lista[i].nombre).transform;
            punto.SetParent(raiz.transform);
            punto.SetPositionAndRotation(lista[i].pos, Quaternion.Euler(0f, lista[i].giro, 0f));
            Destino(destinos.GetArrayElementAtIndex(i), lista[i].nombre, lista[i].queHay, punto);
        }
        if (entradaJefes != null)
            Destino(destinos.GetArrayElementAtIndex(lista.Length), "Zona de jefes (oeste)", "Demonio 1, Demonio 2, Gólem y Mago Oscuro.", entradaJefes);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Destino(SerializedProperty d, string nombre, string queHay, Transform punto)
    {
        d.FindPropertyRelative("nombre").stringValue = nombre;
        d.FindPropertyRelative("queHay").stringValue = queHay;
        d.FindPropertyRelative("punto").objectReferenceValue = punto;
    }

    /// La escena se llamaba CombatLab: se quita del build y del proyecto al crear Lab.
    private static void RetirarEscenaAntigua()
    {
        var escenas = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (escenas.RemoveAll(e => e.path == EscenaAntigua) > 0) EditorBuildSettings.scenes = escenas.ToArray();
        if (File.Exists(EscenaAntigua)) AssetDatabase.DeleteAsset(EscenaAntigua);
    }

    private static GameObject CrearEstacion(string name, Transform parent)
    {
        var station = new GameObject(name);
        station.transform.SetParent(parent);
        station.SetActive(false);
        return station;
    }

    private static void InstanciarEnemigo(string path, string instanceName, Transform parent, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[CombatLab] No se encontró el prefab de prueba: {path}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = instanceName;
        instance.transform.position = position;
    }

    /// 'estatico' a false para lo que se mueve (puertas, ascensores, placas): un objeto estático
    /// se agrupa con el resto de la geometría y su malla ya no sigue a su transform.
    private static GameObject CrearCubo(string name, Transform parent, Vector3 position, Vector3 scale, Material material,
                                        string layerName = null, bool estatico = true)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        if (!string.IsNullOrEmpty(layerName))
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) cube.layer = layer;
        }
        cube.transform.SetParent(parent);
        cube.transform.SetPositionAndRotation(position, Quaternion.identity);
        cube.transform.localScale = scale;
        cube.isStatic = estatico;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    /// Muro a lo largo de X ('aLoLargoDeX') o de Z con un hueco de 4 m en el centro.
    private static void CrearMuroConHueco(string name, Transform parent, Vector3 centro, float largo, bool aLoLargoDeX, Material material)
    {
        const float hueco = 4f;
        float tramo = (largo - hueco) * 0.5f;
        Vector3 eje = aLoLargoDeX ? Vector3.right : Vector3.forward;
        Vector3 tamano = aLoLargoDeX ? new Vector3(tramo, 3f, 0.5f) : new Vector3(0.5f, 3f, tramo);
        float desplazamiento = (hueco + tramo) * 0.5f;
        CrearCubo(name + " (1)", parent, centro - eje * desplazamiento, tamano, material);
        CrearCubo(name + " (2)", parent, centro + eje * desplazamiento, tamano, material);
    }

    private static void CrearMarcador(string name, Transform parent, Vector3 position, Material material)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name;
        marker.transform.SetParent(parent);
        marker.transform.SetPositionAndRotation(position, Quaternion.identity);
        marker.transform.localScale = new Vector3(3f, 0.025f, 3f);
        var collider = marker.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        marker.GetComponent<Renderer>().sharedMaterial = material;
    }

    /// 'yaw': hacia dónde mira quien lo lee (0 = hacia +Z, 90 = hacia +X).
    private static void CrearTexto(string text, Transform parent, Vector3 position, float yaw = 0f)
    {
        var textObject = new GameObject("Rótulo — " + text);
        textObject.transform.SetParent(parent);
        textObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        textObject.transform.localScale = Vector3.one * 0.18f;
        var label = textObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 32;
        label.characterSize = 0.5f;
        label.color = Color.white;
    }

    private static Material GetMaterial(string name, Color color)
    {
        string path = MaterialsPath + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AsegurarCarpetaMateriales()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Test");
        if (!AssetDatabase.IsValidFolder(MaterialsPath))
            AssetDatabase.CreateFolder("Assets/Scenes/Test", "CombatLabMaterials");
    }

    private static void AddToBuildSettings(string scenePath)
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
}
