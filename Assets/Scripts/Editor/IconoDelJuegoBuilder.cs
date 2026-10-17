using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// Aplica el icono compartido al ejecutable y a los tamaños que pide Windows.
public static class IconoDelJuegoBuilder
{
    private const string Ruta = "Assets/Art/UI/Iconos/IconoDelJuego.png";

    [MenuItem("El Sendero/Build/Aplicar icono del juego")]
    public static void Aplicar()
    {
        AssetDatabase.ImportAsset(Ruta, ImportAssetOptions.ForceSynchronousImport);
        var importador = AssetImporter.GetAtPath(Ruta) as TextureImporter;
        if (importador == null) throw new System.InvalidOperationException("No se encuentra el icono: " + Ruta);
        importador.textureType = TextureImporterType.Default;
        importador.alphaIsTransparency = true;
        importador.mipmapEnabled = false;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.maxTextureSize = 1024;
        importador.SaveAndReimport();
        var icono = AssetDatabase.LoadAssetAtPath<Texture2D>(Ruta);
        Poner(NamedBuildTarget.Unknown, icono, IconKind.Any);
        Poner(NamedBuildTarget.Standalone, icono, IconKind.Application);
        AssetDatabase.SaveAssets();
        Debug.Log("[IconoDelJuego] Estrella y sendero aplicados al icono predeterminado y al ejecutable de escritorio.");
    }

    private static void Poner(NamedBuildTarget grupo, Texture2D icono, IconKind tipo)
    {
        var tamanos = UnityEditor.PlayerSettings.GetIconSizes(grupo, tipo);
        var iconos = new Texture2D[tamanos.Length];
        for (int i = 0; i < iconos.Length; i++) iconos[i] = icono;
        UnityEditor.PlayerSettings.SetIcons(grupo, iconos, tipo);
    }
}
