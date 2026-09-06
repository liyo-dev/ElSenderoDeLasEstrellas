using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ver diagnóstico completo en claude/analisis-montanas-mainworld-repeticion-y-color-2026-09-04.md
/// (proyecto de Claude). Además de repetir siempre la misma silueta (ver MountainVarietyBuilder),
/// las montañas de MainWorld usan el material del catálogo "CPT" del Low Poly Modular Terrain
/// Pack, cuya paleta (grises/tostados apagados) choca con el verde saturado del resto del terreno.
/// El mismo pack ya incluye un segundo catálogo, "MT", con geometría equivalente (misma
/// numeración de tamaño/familia/silueta) pero materiales de paleta distinta — entre ellos
/// MT_Terrain_01, un verde césped mucho más parecido al de la hierba del proyecto. Esta
/// herramienta prueba esa paleta sustituyendo cada montaña por su equivalente exacto del otro
/// catálogo (misma silueta exacta, solo cambia el material), conservando posición/rotación/escala
/// y la NavMeshObstacle añadida a mano en cada instancia (igual que MountainVarietyBuilder).
///
/// Uso: con MainWorld.unity abierta,
///   El Sendero → Mundo → Probar Paleta MT en Montañas de MainWorld
///   El Sendero → Mundo → Volver a Paleta CPT en Montañas de MainWorld
/// Compatible con MountainVarietyBuilder en cualquier orden: cada montaña se reconoce por su
/// nombre de prefab actual (tamaño/familia/número), así que si ya se varió la silueta con esa
/// herramienta, esta respeta esa silueta y solo cambia el catálogo de color.
/// Idempotente: solo toca las instancias que no estén ya en la paleta destino — si el resultado no
/// convence, basta con ejecutar el otro menú para volver atrás sin rastro.
/// </summary>
public static class MountainPaletteSwitcher
{
    private const string MountainsGroupName = "Mountains";
    private const string PrefabRoot = "Assets/Plugins/Low Poly Modular Terrain Pack/Terrain_Assets/Prefabs/Mountains";

    private static readonly Regex NombrePrefab = new Regex(@"^(CPT|MT)_Mountain_(S|M|L)_([a-e])_(\d+)$");

    [MenuItem("El Sendero/Mundo/Probar Paleta MT en Montañas de MainWorld")]
    public static void ProbarPaletaMT() => CambiarPaleta("MT");

    [MenuItem("El Sendero/Mundo/Volver a Paleta CPT en Montañas de MainWorld")]
    public static void VolverAPaletaCPT() => CambiarPaleta("CPT");

    private static void CambiarPaleta(string catalogoDestino)
    {
        GameObject grupo = BuscarEnEscenaActiva(MountainsGroupName);
        if (grupo == null)
        {
            Debug.LogError($"[MountainPaletteSwitcher] No se encontró un GameObject llamado '{MountainsGroupName}' en la escena activa. Abre MainWorld.unity antes de ejecutar esto.");
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

            GameObject fuenteActual = PrefabUtility.GetCorrespondingObjectFromSource(original);
            string nombreFuente = fuenteActual != null ? fuenteActual.name : original.name;
            Match match = NombrePrefab.Match(nombreFuente);
            if (!match.Success)
            {
                Debug.LogWarning($"[MountainPaletteSwitcher] '{original.name}' (prefab base '{nombreFuente}') no tiene el patrón de nombre esperado (CPT|MT_Mountain_S|M|L_a-e_NN). Se deja sin tocar.");
                conError++;
                continue;
            }

            string catalogoActual = match.Groups[1].Value;
            string tamano = match.Groups[2].Value;
            string letra = match.Groups[3].Value;
            string numero = match.Groups[4].Value;

            if (catalogoActual == catalogoDestino)
            {
                sinTocar++;
                continue;
            }

            string nombrePrefabDestino = $"{catalogoDestino}_Mountain_{tamano}_{letra}_{numero}";
            string rutaPrefab = $"{PrefabRoot}/{catalogoDestino}/NoLOD/{tamano}/{nombrePrefabDestino}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[MountainPaletteSwitcher] No se encontró el prefab '{rutaPrefab}' (equivalente de '{original.name}'). Se deja sin tocar.");
                conError++;
                continue;
            }

            // Capturamos del original lo que no viene de la prefab base y hay que trasladar a mano.
            string nombreOriginal = original.name;
            int navMeshArea = GameObjectUtility.GetNavMeshArea(original);
            StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(original);
            NavMeshObstacle obstaculoOriginal = original.GetComponent<NavMeshObstacle>();

            GameObject nueva = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, grupo.transform);
            Undo.RegisterCreatedObjectUndo(nueva, "Cambiar Paleta de Montañas de MainWorld");

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

        Debug.Log($"[MountainPaletteSwitcher] Listo en '{scene.name}': {cambiadas} montañas pasadas a paleta '{catalogoDestino}', {sinTocar} ya estaban en esa paleta, {conError} con error (revisa los warnings de arriba). Total del grupo: {total}. Recuerda guardar la escena.");
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
}
