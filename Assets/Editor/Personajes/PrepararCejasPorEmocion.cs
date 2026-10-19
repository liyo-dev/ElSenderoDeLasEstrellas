using UnityEditor;
using UnityEngine;

/// Añade el componente que mueve las cejas al pelo activo de un prefab de personaje y enlaza
/// sus referencias. El componente hace el resto en tiempo de ejecución.
public static class PrepararCejasPorEmocion
{
    private const string RutaWill = "Assets/Prefabs/_WILL.prefab";

    [MenuItem("El Sendero/Personajes/Cejas/Preparar en Will")]
    private static void PrepararWill() => Preparar(RutaWill);

    [MenuItem("El Sendero/Personajes/Cejas/Preparar en el prefab seleccionado")]
    private static void PrepararSeleccionado()
    {
        GameObject seleccionado = Selection.activeGameObject;
        if (seleccionado == null)
        {
            Debug.LogWarning("[Cejas] Selecciona un prefab de personaje en el Project.");
            return;
        }
        string ruta = AssetDatabase.GetAssetPath(seleccionado);
        if (string.IsNullOrEmpty(ruta) || !ruta.EndsWith(".prefab"))
        {
            Debug.LogWarning("[Cejas] El objeto seleccionado no es un prefab.");
            return;
        }
        Preparar(ruta);
    }

    private static void Preparar(string ruta)
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        if (raiz == null)
        {
            Debug.LogError($"[Cejas] No se pudo abrir {ruta}.");
            return;
        }

        try
        {
            Transform cabeza = Buscar(raiz.transform, "head");
            if (cabeza == null)
            {
                Debug.LogError($"[Cejas] {ruta} no tiene un hueso «head».");
                return;
            }

            Transform pelo = null;
            MeshFilter filtro = null;
            foreach (Transform hijo in cabeza)
            {
                if (!hijo.name.StartsWith("Hair") || !hijo.gameObject.activeSelf) continue;
                var candidato = hijo.GetComponent<MeshFilter>();
                if (candidato == null || candidato.sharedMesh == null) continue;
                pelo = hijo;
                filtro = candidato;
                break;
            }
            if (pelo == null)
            {
                Debug.LogError($"[Cejas] {ruta} no tiene un pelo activo con malla.");
                return;
            }

            NPCEmotionController controlador = raiz.GetComponentInChildren<NPCEmotionController>(true);
            if (controlador == null)
            {
                Debug.LogError($"[Cejas] {ruta} no tiene NPCEmotionController.");
                return;
            }

            var componente = pelo.GetComponent<CejasPorEmocion>();
            if (componente == null) componente = pelo.gameObject.AddComponent<CejasPorEmocion>();

            foreach (var otro in raiz.GetComponentsInChildren<CejasPorEmocion>(true))
                if (otro != componente) Object.DestroyImmediate(otro, true);

            var so = new SerializedObject(componente);
            so.FindProperty("controlador").objectReferenceValue = controlador;
            so.FindProperty("mallaDelPelo").objectReferenceValue = filtro;
            so.FindProperty("ojosDeReferencia").objectReferenceValue = BuscarOjos(cabeza);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(componente);

            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            Debug.Log($"[Cejas] Preparado {ruta}: pelo {pelo.name}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static Transform Buscar(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform encontrado = Buscar(hijo, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static Renderer BuscarOjos(Transform cabeza)
    {
        foreach (var renderer in cabeza.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
            if (renderer.name.StartsWith("Eye")) return renderer;
        }
        return null;
    }
}
