using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Informe para revisar parpadeos y rendimiento de la escena abierta: materiales más usados con su shader,
/// su textura principal y cómo se importa (mipmaps, filtro, anisotropía, tamaño), más el recuento de
/// renderers, sombras y LODs, y los detalles del terreno (flores y hierba) con su shader. Escribe Guiones/_ordenes/render.md.
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
        InformeDelDetalleDelTerreno(sb);
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "render.md"), sb.ToString());
        Debug.Log("[Render] informe escrito");
    }

    /// Flores y hierba que pinta el terreno (detalles): cómo se dibuja cada una y con qué shader.
    static void InformeDelDetalleDelTerreno(StringBuilder sb)
    {
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            var datos = t.terrainData;
            if (datos == null) continue;
            sb.Append($"\n## Detalle del terreno «{t.name}» (distancia {t.detailObjectDistance} m, densidad {t.detailObjectDensity})\n\n");
            sb.Append($"- Material de los detalles de hierba del terreno: {(t.materialTemplate != null ? t.materialTemplate.shader.name : "-")}\n\n");
            sb.Append("| # | Prototipo | Modo | Instanciado | Materiales (shader) | Sombra del prototipo |\n|---|---|---|---|---|---|\n");
            var protos = datos.detailPrototypes;
            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];
                string nombre = p.usePrototypeMesh ? (p.prototype != null ? AssetDatabase.GetAssetPath(p.prototype) : "(sin malla)") : (p.prototypeTexture != null ? p.prototypeTexture.name : "(sin textura)");
                string mats = "-", sombra = "-";
                if (p.usePrototypeMesh && p.prototype != null)
                {
                    var rs = p.prototype.GetComponentsInChildren<Renderer>(true);
                    mats = string.Join(", ", rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
                        .Select(m => $"{m.name} ({m.shader.name}, recorte {m.IsKeywordEnabled("_ALPHATEST_ON")}, recibe {!m.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")})"));
                    sombra = string.Join(", ", rs.Select(r => r.shadowCastingMode.ToString()).Distinct());
                }
                sb.Append($"| {i} | {nombre} | {p.renderMode} | {p.useInstancing} | {mats} | {sombra} |\n");
            }
        }
    }
}
