using System;
using UnityEditor;
using UnityEngine;

internal static class AsignacionDeLaHojaDeAvistamientos
{
    private const string RutaIcono = "Assets/Art/UI/Items/hoja_de_avistamientos.png";
    private const string RutaObjeto = "Assets/_ITEMS/IT_HojaDeAvistamientos.asset";

    [InitializeOnLoadMethod]
    private static void Programar() => EditorApplication.delayCall += AplicarSiFalta;

    private static void AplicarSiFalta()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged += AlCambiarDeModo;
            return;
        }

        var objeto = AssetDatabase.LoadAssetAtPath<ItemData>(RutaObjeto);
        if (objeto != null && objeto.icon == null && AssetImporter.GetAtPath(RutaIcono) != null)
            Aplicar();
    }

    private static void AlCambiarDeModo(PlayModeStateChange modo)
    {
        if (modo != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= AlCambiarDeModo;
        Programar();
    }

    [MenuItem("El Sendero/Historia/Asignar icono a la hoja de avistamientos")]
    public static void Aplicar()
    {
        if (AssetImporter.GetAtPath(RutaIcono) is not TextureImporter importador)
            throw new InvalidOperationException("Falta la imagen de la hoja de avistamientos.");

        if (importador.textureType != TextureImporterType.Sprite ||
            importador.spriteImportMode != SpriteImportMode.Single ||
            !importador.alphaIsTransparency || importador.mipmapEnabled)
        {
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.alphaIsTransparency = true;
            importador.mipmapEnabled = false;
            importador.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaIcono);
        var objeto = AssetDatabase.LoadAssetAtPath<ItemData>(RutaObjeto);
        if (sprite == null || objeto == null)
            throw new InvalidOperationException("No se pudo cargar el sprite o el objeto de la hoja de avistamientos.");

        if (objeto.icon != sprite)
        {
            Undo.RecordObject(objeto, "Asignar icono a la hoja de avistamientos");
            objeto.icon = sprite;
            EditorUtility.SetDirty(objeto);
            AssetDatabase.SaveAssetIfDirty(objeto);
        }
    }
}
