using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Atrezo hecho en Blender (Tools/Modelos/…): cambia cada material del FBX, por su nombre, por uno
/// liso de ese color con el estilo de un material del juego (por defecto el del pack de las casas,
/// para que se ilumine igual que el escenario). Se puede repetir: solo reimporta si algo cambia.
/// </summary>
public static class PiezaDeBlender
{
    /// <summary>Estilo de los materiales lisos del escenario: el del pack de las casas.</summary>
    public const string EstiloDelEscenario = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/FK01.mat";

    /// <summary>
    /// Materiales en <paramref name="carpetaMateriales"/> como «{prefijo}{material}.mat».
    /// Devuelve false (y lo apunta en el informe) si falta el FBX o el estilo.
    /// </summary>
    public static bool PrepararMateriales(string fbx, string carpetaMateriales, string prefijo,
        IEnumerable<(string material, Color color)> colores, StringBuilder informe, string estilo = EstiloDelEscenario)
    {
        var matEstilo = AssetDatabase.LoadAssetAtPath<Material>(estilo);
        if (matEstilo == null) { informe.AppendLine("No encuentro el material de estilo " + estilo); return false; }
        if (!(AssetImporter.GetAtPath(fbx) is ModelImporter imp)) { informe.AppendLine("Falta " + fbx); return false; }
        var ya = imp.GetExternalObjectMap();
        bool cambia = false;
        foreach (var (material, color) in colores)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), material);
            var mat = PersonajeHechoEnBlender.Liso(matEstilo, $"{carpetaMateriales}/{prefijo}{material}.mat", color);
            if (!ya.TryGetValue(id, out var antes) || antes != mat) { imp.AddRemap(id, mat); cambia = true; }
        }
        if (imp.importAnimation || imp.materialImportMode == ModelImporterMaterialImportMode.None) { imp.importAnimation = false; cambia = true; }
        if (cambia) imp.SaveAndReimport();
        return true;
    }
}
