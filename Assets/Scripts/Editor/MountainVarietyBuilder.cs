using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Diagnóstico completo en claude/analisis-montanas-mainworld-repeticion-y-color-2026-09-04.md
/// (proyecto de Claude): las 25 montañas de MainWorld.unity (grupo "Mountains") son todas la
/// misma prefab (CPT_Mountain_L_a_13, de "Low Poly Modular Terrain Pack") con solo escala y
/// rotación distintas — de ahí que todas se vean iguales. Esta herramienta las sustituye por una
/// selección variada de siluetas "L" del mismo pack ya incluido en el proyecto (nada de compra
/// nueva), conservando posición/rotación/escala de cada instancia y, muy importante, la
/// NavMeshObstacle que cada una lleva añadida a mano (fuera de la propia prefab) para que las
/// montañas sigan bloqueando a los NPCs igual que antes — de lo contrario un intercambio ciego de
/// prefab la haría desaparecer sin avisar.
///
/// Uso: con MainWorld.unity abierta, El Sendero → Mundo → Variar Montañas de MainWorld.
/// Idempotente: la variante de cada montaña se elige de forma determinista a partir de su
/// posición original, así que volver a ejecutar el menú no vuelve a barajar ni duplica nada —
/// las que ya tengan la variante "correcta" de esa pasada se dejan tal cual.
/// </summary>
public static class MountainVarietyBuilder
{
    private const string MountainsGroupName = "Mountains";
    private const string PrefabFolder = "Assets/Plugins/Low Poly Modular Terrain Pack/Terrain_Assets/Prefabs/Mountains/CPT/NoLOD/L";

    // Cantidad real de variantes por familia de forma dentro de la carpeta de arriba (contadas a
    // mano en el pack: CPT_Mountain_L_<letra>_01 .. _<cantidad>). Si Raúl añade más assets a esa
    // carpeta en el futuro, basta con ajustar estos números.
    private static readonly (string letra, int cantidad)[] Familias =
    {
        ("a", 45),
        ("b", 27),
        ("c", 16),
        ("d", 7),
        ("e", 4),
    };

    [MenuItem("El Sendero/Archivo/Mundo/Variar Montañas de MainWorld")]
    public static void VariarMontañas()
    {
        GameObject grupo = BuscarEnEscenaActiva(MountainsGroupName);
        if (grupo == null)
        {
            Debug.LogError($"[MountainVarietyBuilder] No se encontró un GameObject llamado '{MountainsGroupName}' en la escena activa. Abre MainWorld.unity antes de ejecutar esto.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        int total = grupo.transform.childCount;
        int cambiadas = 0;
        int sinTocar = 0;
        int conError = 0;

        // De atrás hacia adelante porque vamos reemplazando (destruyendo/creando) hijos del grupo.
        for (int i = total - 1; i >= 0; i--)
        {
            Transform hijo = grupo.transform.GetChild(i);
            GameObject original = hijo.gameObject;

            string nombrePrefab = ElegirVariante(SemillaDesdePosicion(hijo.localPosition, i));
            string rutaPrefab = $"{PrefabFolder}/{nombrePrefab}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[MountainVarietyBuilder] No se encontró el prefab '{rutaPrefab}'. Se deja '{original.name}' sin tocar.");
                conError++;
                continue;
            }

            // Ejecución repetida: si ya es exactamente esta variante, no hacemos nada (idempotente).
            GameObject fuenteActual = PrefabUtility.GetCorrespondingObjectFromSource(original);
            if (fuenteActual == prefabAsset)
            {
                sinTocar++;
                continue;
            }

            // Capturamos del original lo que no viene de la prefab base y hay que trasladar a mano.
            string nombreOriginal = original.name;
            int navMeshArea = GameObjectUtility.GetNavMeshArea(original);
            StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(original);
            NavMeshObstacle obstaculoOriginal = original.GetComponent<NavMeshObstacle>();

            GameObject nueva = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, grupo.transform);
            Undo.RegisterCreatedObjectUndo(nueva, "Variar Montañas de MainWorld");

            nueva.transform.SetLocalPositionAndRotation(hijo.localPosition, hijo.localRotation);
            nueva.transform.localScale = hijo.localScale;
            nueva.transform.SetSiblingIndex(i);
            nueva.name = nombreOriginal;

            GameObjectUtility.SetNavMeshArea(nueva, navMeshArea);
            GameObjectUtility.SetStaticEditorFlags(nueva, staticFlags);

            if (obstaculoOriginal != null)
            {
                NavMeshObstacle obstaculoNuevo = Undo.AddComponent<NavMeshObstacle>(nueva);
                obstaculoNuevo.shape = obstaculoOriginal.shape;
                obstaculoNuevo.center = obstaculoOriginal.center;
                obstaculoNuevo.size = obstaculoOriginal.size;
                obstaculoNuevo.radius = obstaculoOriginal.radius;
                obstaculoNuevo.height = obstaculoOriginal.height;
                obstaculoNuevo.carving = obstaculoOriginal.carving;
                obstaculoNuevo.carveOnlyStationary = obstaculoOriginal.carveOnlyStationary;
                obstaculoNuevo.carvingMoveThreshold = obstaculoOriginal.carvingMoveThreshold;
                obstaculoNuevo.carvingTimeToStationary = obstaculoOriginal.carvingTimeToStationary;
            }

            Undo.DestroyObjectImmediate(original);
            cambiadas++;
        }

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[MountainVarietyBuilder] Listo en '{scene.name}': {cambiadas} montañas sustituidas por una variante distinta, {sinTocar} ya tenían la variante correcta de una pasada anterior, {conError} con error (revisa los warnings de arriba). Total del grupo: {total}. Recuerda guardar la escena.");
    }

    /// <summary>Busca un GameObject por nombre entre TODOS los objetos raíz de la escena activa (incluidos inactivos), recursivamente.</summary>
    private static GameObject BuscarEnEscenaActiva(string nombre)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            GameObject encontrado = BuscarEnHijos(raiz.transform, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static GameObject BuscarEnHijos(Transform actual, string nombre)
    {
        if (actual.name == nombre) return actual.gameObject;
        for (int i = 0; i < actual.childCount; i++)
        {
            GameObject encontrado = BuscarEnHijos(actual.GetChild(i), nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    /// <summary>
    /// Semilla determinista a partir de la posición local original de la montaña (más el índice,
    /// por si dos llegaran a compartir posición exacta). Así, ejecutar el menú varias veces siempre
    /// elige la misma variante para la misma montaña — no hay barajado nuevo en cada pasada.
    /// </summary>
    private static int SemillaDesdePosicion(Vector3 posicionLocal, int indice)
    {
        int hx = Mathf.RoundToInt(posicionLocal.x * 73f);
        int hy = Mathf.RoundToInt(posicionLocal.y * 179f);
        int hz = Mathf.RoundToInt(posicionLocal.z * 283f);
        return hx ^ hy ^ hz ^ indice;
    }

    private static string ElegirVariante(int semilla)
    {
        var rng = new System.Random(semilla);
        int totalVariantes = Familias.Sum(f => f.cantidad);
        int indice = rng.Next(totalVariantes);
        foreach (var (letra, cantidad) in Familias)
        {
            if (indice < cantidad)
            {
                return $"CPT_Mountain_L_{letra}_{(indice + 1):00}";
            }
            indice -= cantidad;
        }
        return "CPT_Mountain_L_a_01"; // no debería llegar aquí
    }
}
