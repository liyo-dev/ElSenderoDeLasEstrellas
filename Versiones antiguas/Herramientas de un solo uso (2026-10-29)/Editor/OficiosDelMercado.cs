#if UNITY_EDITOR
using Game.NPC;
using Game.NPC.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara el oficio ambiental de Victoria y Eldran en el mercado del Cap. 1:
/// sus <see cref="NPCAmbientConfig"/> y las dos <see cref="NPCWorkRoute"/> de MainWorld.
///
/// Es idempotente: si una config o una ruta ya existe, no la duplica. Las posiciones de las
/// rutas son un punto de partida calculado desde los spawns; los dos puntos se ajustan a mano
/// en la Scene View. El forward de cada punto debe mirar hacia su pila.
/// </summary>
public static class OficiosDelMercado
{
    private const string CarpetaAmbient = "Assets/_NPCs/Ambient";
    private const string RutaPrefabVictoria = "Assets/_NPCs/Victoria.prefab";
    private const string RutaPrefabEldran = "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab";
    private const string RutaPrefabOliver = "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab";
    private const string RutaConfigVictoria = CarpetaAmbient + "/NPC_Ambient_Victoria.asset";
    private const string RutaConfigEldran = CarpetaAmbient + "/NPC_Ambient_Eldran.asset";
    private const string RutaConfigOliver = CarpetaAmbient + "/NPC_Ambient_Oliver.asset";
    private const string RutaMaterialCaja = "Assets/Art/Materials/Sendero/Mat_MaderaCajaMisteriosa.mat";
    private const string RutaPrefabCaja = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Goods03_a02.prefab";
    private const string NombrePadre = "Oficios del mercado";

    private const string PuestoVictoria = "mercado_victoria";
    private const string PuestoEldran = "mercado_eldran";
    private const string PuestoOliver = "mercado_oliver";

    // La caja real Goods03_a02 mide ~0,69 m de lado. Con 2 cajas y separación horizontal de 0,72 m,
    // se apilan a modo de mostrador (una al lado de la otra).
    private const int CajasPorPila = 2;
    private static readonly Vector2 SeparacionCajas = new(0.72f, 0.70f);
    private static readonly Vector3 OffsetPila = new(0f, 0f, 0.8f);
    private const string PoseDeCarga = "CarryMoveIdle_NoWeapon";
    private static readonly Vector3 OffsetCargaFrontal = new(0f, 0.65f, 0.42f);
    private static readonly Vector3 RotacionCargaFrontal = Vector3.zero;
    private static readonly Vector3 OffsetEnMano = new(0f, 0.04f, 0.06f);
    private static readonly Vector3 GiroEnMano = new(0f, 90f, 0f);

    [MenuItem("El Sendero/NPCs/Oficios/1 · Configurar Victoria, Eldran y Oliver")]
    public static void ConfigurarPrefabs()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[OficiosDelMercado] Sal de Play antes de configurar los prefabs.");
            return;
        }

        var configVictoria = CrearOActualizarConfig(RutaConfigVictoria, PuestoVictoria);
        var configEldran = CrearOActualizarConfig(RutaConfigEldran, PuestoEldran);
        var configOliver = CrearOActualizarConfig(RutaConfigOliver, PuestoOliver, enableWander: true);

        AsignarConfig(RutaPrefabVictoria, configVictoria);
        AsignarConfig(RutaPrefabEldran, configEldran);
        AsignarConfig(RutaPrefabOliver, configOliver);

        AssetDatabase.SaveAssets();
        Debug.Log("[OficiosDelMercado] Configs de Victoria, Eldran y Oliver creadas y asignadas a sus prefabs.");
    }

    [MenuItem("El Sendero/NPCs/Oficios/2 · Montar rutas en MainWorld")]
    public static void MontarRutas()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[OficiosDelMercado] Sal de Play antes de montar las rutas.");
            return;
        }

        Scene escena = BuscarEscenaMainWorld();
        if (!escena.IsValid())
        {
            Debug.LogError("[OficiosDelMercado] Abre MainWorld antes de montar las rutas.");
            return;
        }

        Transform padre = BuscarOCrearPadre(escena);
        if (padre == null) return;

        Transform victoria = BuscarBase("NPC_Victoria", "SPAWN_Victoria", escena);
        Transform eldran = BuscarBase("NPC_Eldran", "SPAWN_Eldran", escena);
        Transform oliver = BuscarBase("NPC_Oliver", "SPAWN_Oliver", escena);

        if (victoria == null && eldran == null && oliver == null)
        {
            Debug.LogError("[OficiosDelMercado] No encuentro a Victoria, Eldran ni Oliver en MainWorld, " +
                           "ni sus NpcSpawnPoint (SPAWN_Victoria / SPAWN_Eldran / SPAWN_Oliver).");
            return;
        }

        if (victoria != null)
            CrearOActualizarRuta(padre, "Oficio_Victoria", PuestoVictoria, victoria, -1f, null, null, "WILL_MISSION1");
        else
            Debug.LogWarning("[OficiosDelMercado] No encuentro a Victoria; no creo su ruta.");

        if (eldran != null)
            CrearOActualizarRuta(padre, "Oficio_Eldran", PuestoEldran, eldran, 1f, "ELDRAN_SIGUEME", "ESTRELLA_COMPRADA", "WILL_MISSION1");
        else
            Debug.LogWarning("[OficiosDelMercado] No encuentro a Eldran; no creo su ruta.");

        if (oliver != null)
            CrearOActualizarRuta(padre, "Oficio_Oliver", PuestoOliver, oliver, 2f, "ELDRAN_SIGUEME", "ESTRELLA_COMPRADA", "WILL_MISSION1");
        else
            Debug.LogWarning("[OficiosDelMercado] No encuentro a Oliver; no creo su ruta.");

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();

        Debug.Log($"[OficiosDelMercado] Rutas montadas en '{escena.name}'. " +
                  "Ajusta los puntos de cada ruta en la Scene View: el forward de cada punto debe mirar a su pila.");
    }

    // ── Configs ─────────────────────────────────────────────────────────────────

    private static NPCAmbientConfig CrearOActualizarConfig(string ruta, string puestoId, bool enableWander = false)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaAmbient))
            AssetDatabase.CreateFolder("Assets/_NPCs", "Ambient");

        var config = AssetDatabase.LoadAssetAtPath<NPCAmbientConfig>(ruta);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<NPCAmbientConfig>();
            AssetDatabase.CreateAsset(config, ruta);
        }

        config.enableWander = enableWander;
        config.ambientActivity = NPCAmbientActivity.None;
        config.canSeekShelter = false;
        config.permitirTrabajo = true;
        config.puestoId = puestoId;
        config.walkSpeed = 1.5f;
        config.minIdleTime = 1.2f;
        config.maxIdleTime = 3f;

        EditorUtility.SetDirty(config);
        return config;
    }

    private static void AsignarConfig(string rutaPrefab, NPCAmbientConfig config)
    {
        var contents = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var manager = contents.GetComponent<NPCBehaviourManagerV2>();
            if (manager == null)
            {
                Debug.LogError($"[OficiosDelMercado] {rutaPrefab} no tiene NPCBehaviourManagerV2.");
                return;
            }

            var so = new SerializedObject(manager);
            var configuracion = so.FindProperty("configuration");
            if (configuracion == null)
            {
                Debug.LogError($"[OficiosDelMercado] No encuentro 'configuration' en {rutaPrefab}.");
                return;
            }

            var ambient = configuracion.FindPropertyRelative("ambientConfig");
            if (ambient == null)
            {
                Debug.LogError($"[OficiosDelMercado] No encuentro 'ambientConfig' en {rutaPrefab}.");
                return;
            }

            ambient.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, rutaPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ── Rutas ───────────────────────────────────────────────────────────────────

    private static Transform BuscarOCrearPadre(Scene escena)
    {
        var existente = GameObject.Find(NombrePadre);
        if (existente != null && existente.scene == escena)
            return existente.transform;

        var go = new GameObject(NombrePadre);
        Undo.RegisterCreatedObjectUndo(go, "Crear oficios del mercado");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, escena);
        return go.transform;
    }

    private static void CrearOActualizarRuta(Transform padre, string nombre, string puestoId,
        Transform baseNpc, float lado, string dejaDeSiQuest, string dejaDeSiSenal, string soloSiQuest = null)
    {
        var prefabCaja = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabCaja);
        if (prefabCaja == null)
            Debug.LogWarning($"[OficiosDelMercado] No encuentro la caja real '{RutaPrefabCaja}'; se usará el cubo provisional.");

        foreach (var rutaExistente in padre.GetComponentsInChildren<NPCWorkRoute>(true))
        {
            if (rutaExistente == null || rutaExistente.puestoId != puestoId) continue;

            // La ruta ya está montada: se actualizan las cajas y el ritmo, pero NO se tocan los
            // puntos, que el diseñador ha podido ajustar a mano en la Scene View.
            bool veniaDeMockup = rutaExistente.prefabCaja == null;

            rutaExistente.prefabCaja = prefabCaja;
            rutaExistente.cajasPorPila = CajasPorPila;
            rutaExistente.cajasEnDestinoAlEmpezar = 0;
            rutaExistente.terminarAlVaciarOrigen = true;
            rutaExistente.separacionCajas = SeparacionCajas;
            rutaExistente.materialCaja = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCaja);
            rutaExistente.soloSiQuest = soloSiQuest;
            rutaExistente.dejaDeSiQuest = dejaDeSiQuest;
            rutaExistente.dejaDeSiSenal = dejaDeSiSenal;
            rutaExistente.offsetCargaFrontal = OffsetCargaFrontal;
            rutaExistente.rotacionCargaFrontal = RotacionCargaFrontal;
            if (string.IsNullOrEmpty(rutaExistente.poseDeCarga))
                rutaExistente.poseDeCarga = PoseDeCarga;
            if (veniaDeMockup || (rutaExistente.offsetEnMano == Vector3.zero &&
                                  rutaExistente.giroEnMano == Vector3.zero))
            {
                rutaExistente.offsetEnMano = OffsetEnMano;
                rutaExistente.giroEnMano = GiroEnMano;
            }
            if (veniaDeMockup) rutaExistente.offsetPila = OffsetPila;
            EditorUtility.SetDirty(rutaExistente);
            Debug.Log($"[OficiosDelMercado] '{puestoId}' actualizada con la caja frontal y condiciones narrativas; los puntos no se han tocado.");
            return;
        }

        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        Undo.RegisterCreatedObjectUndo(go, "Crear ruta de oficio");

        var ruta = go.AddComponent<NPCWorkRoute>();
        ruta.puestoId = puestoId;
        ruta.prefabCaja = prefabCaja;
        ruta.cajasPorPila = CajasPorPila;
        ruta.cajasEnDestinoAlEmpezar = 0;
        ruta.terminarAlVaciarOrigen = true;
        ruta.separacionCajas = SeparacionCajas;
        ruta.offsetPila = OffsetPila;
        ruta.materialCaja = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCaja);
        ruta.soloSiQuest = soloSiQuest;
        ruta.dejaDeSiQuest = dejaDeSiQuest;
        ruta.dejaDeSiSenal = dejaDeSiSenal;
        ruta.offsetCargaFrontal = OffsetCargaFrontal;
        ruta.rotacionCargaFrontal = RotacionCargaFrontal;
        ruta.poseDeCarga = PoseDeCarga;
        ruta.offsetEnMano = OffsetEnMano;
        ruta.giroEnMano = GiroEnMano;

        Vector3 derecha = baseNpc.right;
        Vector3 centro = baseNpc.position + derecha * (lado * 1.2f) + baseNpc.forward * 1.5f;

        Vector3 posA = SobreNavMesh(centro + derecha * 1.5f);
        Vector3 posB = SobreNavMesh(centro - derecha * 1.5f);

        ruta.puntoRecogida = CrearPunto(go.transform, "PuntoRecogida", posA, derecha);
        ruta.puntoEntrega = CrearPunto(go.transform, "PuntoEntrega", posB, -derecha);

        EditorUtility.SetDirty(ruta);
    }

    private static Transform CrearPunto(Transform padre, string nombre, Vector3 posicion, Vector3 adelante)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = posicion;
        go.transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        Undo.RegisterCreatedObjectUndo(go, "Crear punto de oficio");
        return go.transform;
    }

    private static Vector3 SobreNavMesh(Vector3 posicion)
    {
        if (NavMesh.SamplePosition(posicion, out var hit, 2f, NavMesh.AllAreas))
            return hit.position;

        Debug.LogWarning($"[OficiosDelMercado] No hay NavMesh cerca de {posicion}; el punto queda donde está. " +
                         "Ajusta el punto en la Scene View para que el NPC pueda llegar.");
        return posicion;
    }

    // ── Búsqueda de escena y NPCs ───────────────────────────────────────────────

    private static Scene BuscarEscenaMainWorld()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (escena.isLoaded && escena.path.EndsWith("MainWorld.unity"))
                return escena;
        }

        if (System.IO.File.Exists("Assets/Scenes/Worlds/MainWorld.unity"))
            return EditorSceneManager.OpenScene("Assets/Scenes/Worlds/MainWorld.unity", OpenSceneMode.Additive);

        return default;
    }

    private static Transform BuscarBase(string persistenceId, string spawnId, Scene escena)
    {
        foreach (var npc in Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include))
        {
            if (npc == null || npc.gameObject.scene != escena) continue;
            if (npc.PersistenceId == persistenceId) return npc.transform;
        }

        foreach (var punto in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include))
        {
            if (punto == null || punto.gameObject.scene != escena) continue;
            if (punto.spawnId == spawnId) return punto.transform;
        }

        return null;
    }
}
#endif
