using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Informe para revisar parpadeos y rendimiento de la escena abierta: materiales más usados con su shader,
/// su textura principal y cómo se importa (mipmaps, filtro, anisotropía, tamaño), más el recuento de
/// renderers, sombras y LODs. Escribe Guiones/_ordenes/render.md.
public static class InformeDeRender
{
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: materiales, texturas y render de la escena")]
    static void Informe()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        var sb = new StringBuilder($"# Render de la escena ({renderers.Length} renderers activos)\n\n");
        sb.Append($"- Proyectan sombra: {renderers.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)}\n");
        sb.Append($"- Con LODGroup: {Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude).Length}\n");
        sb.Append($"- Luces: {Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Length}\n\n");
        sb.Append("| Material | Shader | Renderers | Ejemplo | Textura | Mip | Filtro | Aniso | Tamaño |\n|---|---|---|---|---|---|---|---|---|\n");
        var porMaterial = renderers.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => (m, r)))
            .GroupBy(x => x.m).OrderByDescending(g => g.Count()).Take(60);
        foreach (var g in porMaterial)
        {
            var mat = g.Key;
            Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            string ruta = tex != null ? AssetDatabase.GetAssetPath(tex) : "";
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            sb.Append($"| {mat.name} | {mat.shader.name} | {g.Count()} | {g.First().r.name} | {Path.GetFileName(ruta)} | {(imp != null ? imp.mipmapEnabled.ToString() : "-")} | {(imp != null ? imp.filterMode.ToString() : "-")} | {(imp != null ? imp.anisoLevel.ToString() : "-")} | {(tex != null ? $"{tex.width}x{tex.height}" : "-")} |\n");
        }
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "render.md"), sb.ToString());
        Debug.Log("[Render] informe escrito");
    }
}
