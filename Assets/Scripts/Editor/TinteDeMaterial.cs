using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copias teñidas de un material, para los constructores de escenas (reparto de otro color, la ropa
/// de un atuendo de escena). Teñir es que la pieza tome el color del tinte conservando sus luces y
/// sus sombras: si el material tiene textura principal, se hace una copia de la textura con el tinte
/// por su luminancia; si no, se pone el tinte como color principal.
/// Hace falta así porque los shaders toon del proyecto (Quibli StylizedLit, el de los personajes y
/// de los packs de ithappy) no usan el color principal (_BaseColor): el color sale de la textura y
/// de la rampa de luz, y un _BaseColor cambiado no se ve. Ver INC-889.
/// </summary>
public static class TinteDeMaterial
{
    /// <summary>Luz con la que queda la parte más oscura de la textura (la más clara queda en 1).</summary>
    private const float SombraMinima = 0.55f;

    /// <summary>
    /// La copia de <paramref name="original"/> teñida de <paramref name="tinte"/> en <paramref name="ruta"/>
    /// (se crea la primera vez; después se pone al día con el original y el tinte). La textura teñida va
    /// junto al material (<c>&lt;material&gt;_Textura.png</c>) y solo se rehace si cambian el tinte o la textura.
    /// </summary>
    public static Material Copia(Material original, string ruta, Color tinte)
    {
        if (original == null) return null;
        var copia = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (copia == null)
        {
            AsegurarCarpeta(Path.GetDirectoryName(ruta).Replace('\\', '/'));
            copia = new Material(original);
            AssetDatabase.CreateAsset(copia, ruta);
        }
        else
        {
            copia.shader = original.shader;
            copia.CopyPropertiesFromMaterial(original);
        }

        var textura = original.mainTexture as Texture2D;
        if (textura != null)
            copia.mainTexture = TexturaTenida(textura, Path.ChangeExtension(ruta, null) + "_Textura.png", tinte);
        else
            copia.color = tinte;
        EditorUtility.SetDirty(copia);
        return copia;
    }

    /// <summary>La textura con el tinte por su luminancia; se reutiliza si ya está hecha con este tinte y esta textura.</summary>
    private static Texture2D TexturaTenida(Texture2D origen, string ruta, Color tinte)
    {
        string firma = $"tinte={ColorUtility.ToHtmlStringRGBA(tinte)};origen={AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(origen))};" +
                       $"sombra={SombraMinima.ToString(CultureInfo.InvariantCulture)}";
        var importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
        var hecha = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (hecha != null && importador != null && importador.userData == firma) return hecha;

        var pixeles = LeerPixeles(origen);
        for (int i = 0; i < pixeles.Length; i++)
        {
            var c = pixeles[i];
            float luz = Mathf.Lerp(SombraMinima, 1f, 0.299f * c.r + 0.587f * c.g + 0.114f * c.b);
            pixeles[i] = new Color(tinte.r * luz, tinte.g * luz, tinte.b * luz, c.a);
        }
        var tex = new Texture2D(origen.width, origen.height, TextureFormat.RGBA32, false);
        tex.SetPixels(pixeles);
        tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(origen)) is TextureImporter delOrigen)
        {
            importador.filterMode = delOrigen.filterMode;
            importador.wrapMode = delOrigen.wrapMode;
            importador.mipmapEnabled = delOrigen.mipmapEnabled;
            importador.maxTextureSize = Mathf.Max(delOrigen.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(origen.width, origen.height)));
        }
        importador.userData = firma;
        importador.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (string.IsNullOrEmpty(ruta) || AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    /// <summary>Los píxeles de una textura aunque no sea legible: se copia a una RenderTexture y se lee de ahí.</summary>
    private static Color[] LeerPixeles(Texture2D origen)
    {
        var rt = RenderTexture.GetTemporary(origen.width, origen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var antes = RenderTexture.active;
        try
        {
            Graphics.Blit(origen, rt);
            RenderTexture.active = rt;
            var copia = new Texture2D(origen.width, origen.height, TextureFormat.RGBA32, false);
            copia.ReadPixels(new Rect(0, 0, origen.width, origen.height), 0, 0);
            copia.Apply();
            var pixeles = copia.GetPixels();
            Object.DestroyImmediate(copia);
            return pixeles;
        }
        finally
        {
            RenderTexture.active = antes;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
