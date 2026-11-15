using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pone BordesProporcionales en los Image Sliced que usan un sprite de píldora (puntas redondeadas:
/// borde lateral mucho mayor que el vertical). Lo usan los constructores de UI que montan un prefab
/// desde cero, antes de guardarlo.
/// </summary>
public static class PildorasProporcionadasBuilder
{
    static readonly string[] CarpetasExcluidas = { "Assets/Plugins", "Assets/TextMesh Pro", "Assets/Art/World" };

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

    static string RutaEnJerarquia(Transform t)
    {
        var partes = new List<string>();
        for (; t != null; t = t.parent) partes.Add(t.name);
        partes.Reverse();
        return string.Join("/", partes);
    }
}
