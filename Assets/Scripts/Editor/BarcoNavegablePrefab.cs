using UnityEditor;
using UnityEngine;

/// <summary>
/// El barco navegable como prefab: así los sitios de Will, Estela y Liam se pueden colocar a
/// mano (en el prefab) y no los vuelve a calcular el generador cada vez que se regenera el LAB
/// o el mundo.
///
/// Menú: *El Sendero ▸ Combate ▸ Barco: guardar el de la escena como prefab* (con el barco
/// seleccionado en la jerarquía, o el que encuentre).
/// </summary>
public static class BarcoNavegablePrefab
{
    public const string Ruta = "Assets/Prefabs/BarcoNavegable.prefab";

    /// <summary>El prefab del barco, o null si todavía no se ha guardado.</summary>
    public static GameObject Cargar()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Ruta);
        if (prefab == null)
        {
            Debug.LogWarning($"[Barco] Todavía no hay prefab en «{Ruta}». Se usará el modelo suelto.");
        }
        return prefab;
    }

    [MenuItem("El Sendero/Combate/Barco: guardar el de la escena como prefab")]
    public static void Guardar()
    {
        var barco = BuscarEnLaEscena();
        if (barco == null)
        {
            EditorUtility.DisplayDialog("Barco navegable",
                "No hay ningún barco con el componente BarcoNavegable en la escena abierta.", "Ok");
            return;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(barco, Ruta);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Barco navegable", "No se pudo guardar el prefab.", "Ok");
            return;
        }

        // El prefab no puede quedarse dentro de la escena.
        if (barco.scene.IsValid()) Object.DestroyImmediate(barco);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Barco] Prefab guardado en «{Ruta}». A partir de ahora el LAB y el mundo usan este, " +
                  "con los sitios de Will, Estela y Liam donde los hayas dejado.");
    }

    /// <summary>El barco de la escena abierta: el seleccionado, o el primero que encuentre.</summary>
    public static GameObject BuscarEnLaEscena()
    {
        var seleccionado = Selection.activeGameObject;
        if (seleccionado != null)
        {
            var propio = seleccionado.GetComponent<BarcoNavegable>();
            if (propio != null) return seleccionado;
            var enHijos = seleccionado.GetComponentInChildren<BarcoNavegable>();
            if (enHijos != null) return enHijos.gameObject;
        }

        var todos = Object.FindObjectsByType<BarcoNavegable>(FindObjectsSortMode.None);
        if (todos.Length == 0) return null;
        return todos[0].gameObject;
    }
}
