#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Activa mipmaps (con conservar cobertura del alfa) en el atlas de árboles y los atlas de flores del pack
// Fantasy Kingdom. Sus .meta son de un formato antiguo que Unity 6 ya no lee: SaveAndReimport los reescribe
// en el formato actual. Idempotente.
public static class MipmapsFantasyKingdom
{
    const string Carpeta = "Assets/Art/World/Fantasy_Kingdom_Pack/Textures";
    static readonly string[] Nombres =
    {
        "Tree_D", "Flower01_D", "Flower02_D", "Flower03_D", "Flower04_D", "Flower06_D", "Flower07_D",
        "Flower08_D", "Flower09_D", "Flower10_D", "Flower11_D", "Flower13_D", "Flower14_D"
    };

    [MenuItem("El Sendero/Archivo/Texturas: mipmaps del pack Fantasy Kingdom")]
    public static void Aplicar()
    {
        int hechas = 0;
        foreach (string nombre in Nombres)
        {
            string ruta = $"{Carpeta}/{nombre}.tga";
            var importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importador == null) { Debug.LogWarning($"[Mipmaps] No encuentro {ruta}"); continue; }
            importador.mipmapEnabled = true;
            importador.mipMapsPreserveCoverage = importador.DoesSourceTextureHaveAlpha();
            importador.alphaTestReferenceValue = 0.5f;
            importador.SaveAndReimport();
            hechas++;
        }
        Debug.Log($"[Mipmaps] Pack Fantasy Kingdom: {hechas}/{Nombres.Length} texturas con mipmaps y .meta reescrito por Unity.");
    }
}
#endif
