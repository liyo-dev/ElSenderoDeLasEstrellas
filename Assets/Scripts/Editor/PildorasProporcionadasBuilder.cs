using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pone BordesProporcionales en todos los Image Sliced del proyecto que usan un sprite de píldora
/// (puntas redondeadas: borde lateral mucho mayor que el vertical), en prefabs y escenas. Las
/// instancias de prefab se saltan: se arreglan en su prefab. Se puede repetir sin efectos.
/// </summary>
public static class PildorasProporcionadasBuilder
{
    static readonly string[] CarpetasExcluidas = { "Assets/Plugins", "Assets/TextMesh Pro", "Assets/Art/World" };

    [MenuItem("El Sendero/UI/Píldoras · bordes proporcionales en todo el proyecto")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Los bordes proporcionales se aplican fuera de Play."); return; }

        var texturas = TexturasDePildora();
        if (texturas.Count == 0) { Debug.Log("No hay sprites de píldora en el proyecto."); return; }
        var tocados = new List<string>();
        try
        {
            var prefabs = Rutas("t:Prefab").Where(r => DependeDe(r, texturas)).ToList();
            for (int i = 0; i < prefabs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Píldoras", prefabs[i], i / (float)Mathf.Max(1, prefabs.Count));
                var raiz = PrefabUtility.LoadPrefabContents(prefabs[i]);
                try
                {
                    var propias = raiz.GetComponentsInChildren<Image>(true).Where(img => !PrefabUtility.IsPartOfPrefabInstance(img));
                    int n = Arreglar(propias, texturas, prefabs[i], tocados);
                    if (n > 0) PrefabUtility.SaveAsPrefabAsset(raiz, prefabs[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }

            foreach (var ruta in Rutas("t:Scene").Where(r => r.StartsWith("Assets/Scenes") && DependeDe(r, texturas)))
            {
                var escena = SceneManager.GetSceneByPath(ruta);
                bool abiertaAqui = !escena.isLoaded;
                if (abiertaAqui) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
                var imagenes = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Image>(true))
                    .Where(img => !PrefabUtility.IsPartOfPrefabInstance(img)).ToArray();
                if (Arreglar(imagenes, texturas, ruta, tocados) > 0)
                {
                    EditorSceneManager.MarkSceneDirty(escena);
                    EditorSceneManager.SaveScene(escena);
                }
                if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        Debug.Log(tocados.Count == 0
            ? "Píldoras: ya estaba todo con bordes proporcionales."
            : $"Píldoras: {tocados.Count} imágenes con bordes proporcionales:\n" + string.Join("\n", tocados));
    }

    /// Para constructores de UI que generan un prefab desde cero: deja sus píldoras con bordes
    /// proporcionales antes de guardarlo. Devuelve cuántas ha tocado.
    public static int AplicarEn(GameObject raiz, string donde, List<string> tocados)
    {
        if (raiz == null) return 0;
        return Arreglar(raiz.GetComponentsInChildren<Image>(true), TexturasDePildora(), donde, tocados ?? new List<string>());
    }

    static int Arreglar(IEnumerable<Image> imagenes, HashSet<string> texturas, string donde, List<string> tocados)
    {
        int n = 0;
        foreach (var imagen in imagenes)
        {
            if (imagen == null || imagen.sprite == null || imagen.type != Image.Type.Sliced) continue;
            if (!texturas.Contains(AssetDatabase.GetAssetPath(imagen.sprite))) continue;
            if (imagen.GetComponent<BordesProporcionales>() != null) continue;
            imagen.gameObject.AddComponent<BordesProporcionales>();
            EditorUtility.SetDirty(imagen.gameObject);
            tocados.Add($"{donde} ▸ {RutaEnJerarquia(imagen.transform)}");
            n++;
        }
        return n;
    }

    /// Texturas cuyo sprite tiene puntas redondeadas: el borde lateral es al menos el doble del
    /// vertical y ocupa más de un tercio del alto.
    static HashSet<string> TexturasDePildora()
    {
        var resultado = new HashSet<string>();
        foreach (var ruta in Rutas("t:Texture2D"))
        {
            if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp) || imp.textureType != TextureImporterType.Sprite) continue;
            var b = imp.spriteBorder; // x izquierda, y abajo, z derecha, w arriba
            float lateral = Mathf.Min(b.x, b.z), vertical = Mathf.Max(b.y, b.w);
            if (lateral <= 0f) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (sprite == null) continue;
            if (lateral >= 2f * vertical && lateral >= sprite.rect.height / 3f) resultado.Add(ruta);
        }
        return resultado;
    }

    static IEnumerable<string> Rutas(string filtro) =>
        AssetDatabase.FindAssets(filtro, new[] { "Assets" }).Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(r => !CarpetasExcluidas.Any(c => r.StartsWith(c))).Distinct();

    static bool DependeDe(string ruta, HashSet<string> texturas) =>
        AssetDatabase.GetDependencies(ruta, false).Any(texturas.Contains);

    static string RutaEnJerarquia(Transform t)
    {
        var partes = new List<string>();
        for (; t != null; t = t.parent) partes.Add(t.name);
        partes.Reverse();
        return string.Join("/", partes);
    }
}
