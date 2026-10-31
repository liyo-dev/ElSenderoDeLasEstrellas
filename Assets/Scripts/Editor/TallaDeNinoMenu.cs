using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone o quita la talla de niño (<see cref="TallaDeNino"/>) a lo seleccionado: prefabs en la
/// ventana Project o personajes de la escena. Los prefabs se abren y se guardan con PrefabUtility.
/// </summary>
public static class TallaDeNinoMenu
{
    [MenuItem("El Sendero/Personajes/Marcar como niño (lo seleccionado)")]
    private static void Marcar() => Aplicar(true);

    [MenuItem("El Sendero/Personajes/Quitar la marca de niño (lo seleccionado)")]
    private static void Quitar() => Aplicar(false);

    [MenuItem("El Sendero/Personajes/Marcar como niño (lo seleccionado)", true)]
    [MenuItem("El Sendero/Personajes/Quitar la marca de niño (lo seleccionado)", true)]
    private static bool HaySeleccion() => Selection.gameObjects.Length > 0;

    private static void Aplicar(bool nino)
    {
        int cambiados = 0;
        foreach (var go in Selection.gameObjects)
        {
            string ruta = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(ruta))
            {
                var raiz = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    if (Cambiar(raiz, nino, false)) { PrefabUtility.SaveAsPrefabAsset(raiz, ruta); cambiados++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
            else if (Cambiar(go, nino, true)) cambiados++;
        }
        Debug.Log($"[TallaDeNino] {(nino ? "Marcados" : "Desmarcados")}: {cambiados}.");
    }

    private static bool Cambiar(GameObject go, bool nino, bool conUndo)
    {
        var talla = go.GetComponent<TallaDeNino>();
        if (nino)
        {
            if (talla != null) return false;
            if (conUndo) Undo.AddComponent<TallaDeNino>(go); else go.AddComponent<TallaDeNino>();
            go.transform.localScale = Vector3.one * TallaDeNino.Escala;
            return true;
        }
        if (talla == null) return false;
        if (conUndo)
        {
            Undo.RecordObject(go.transform, "Quitar talla de niño");
            Undo.DestroyObjectImmediate(talla);
        }
        else Object.DestroyImmediate(talla, true);
        go.transform.localScale = Vector3.one;
        return true;
    }
}
