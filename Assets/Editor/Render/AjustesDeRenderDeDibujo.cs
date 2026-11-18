using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Ajustes de URP y de texturas pensados para el aspecto de dibujo (Raúl, 18 nov: parpadeos y rendimiento).
/// - Sombras: el dibujo corta la luz en dos tonos, así que el «acné» de sombra (bias bajo) sale como rayas
///   duras que bailan al moverse la cámara o el sol. Bias a los valores de URP, mapa 2048, suave en calidad
///   media y sin sombras de luces puntuales (ninguna las proyecta).
/// - Texturas del mundo: trilineal y anisotropía 4, para que las texturas con dibujo fino (copas de los
///   árboles, maderas) no «salten» de nivel de mip ni centelleen en ángulos rasantes.
/// Informe en Guiones/_ordenes/ajustes_render.md. Copia del asset de URP en _ClaudeBackups.
public static class AjustesDeRenderDeDibujo
{
    static readonly string[] ShadersDelMundo = { "El Sendero/Dibujo", "El Sendero/Terreno de dibujo" };

    [MenuItem("El Sendero/Aspecto de dibujo/Render: sombras estables y texturas sin parpadeo")]
    static void Aplicar()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Render] Sal de Play antes."); return; }
        var sb = new StringBuilder("# Ajustes de render para el dibujo\n\n");
        var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            var so = new SerializedObject(urp);
            sb.Append(Fijar(so, "m_MainLightShadowmapResolution", 2048));
            sb.Append(Fijar(so, "m_SoftShadowQuality", 2)); // Medium
            sb.Append(Fijar(so, "m_AdditionalLightShadowsSupported", 0));
            sb.Append(Fijar(so, "m_ShadowDepthBias", 1f));
            sb.Append(Fijar(so, "m_ShadowNormalBias", 0.8f));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        int texturas = 0;
        var vistas = new System.Collections.Generic.HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || !ShadersDelMundo.Contains(mat.shader.name)) continue;
            foreach (string nombre in mat.GetTexturePropertyNames())
            {
                var tex = mat.GetTexture(nombre) as Texture2D;
                if (tex == null || tex.width < 256) continue;
                string ruta = AssetDatabase.GetAssetPath(tex);
                if (!vistas.Add(ruta)) continue;
                if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp) || !imp.mipmapEnabled) continue;
                if (imp.filterMode == FilterMode.Trilinear && imp.anisoLevel >= 4) continue;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
                texturas++;
            }
        }
        sb.Append($"- Texturas del mundo a trilineal + anisotropía 4: {texturas}\n");
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "ajustes_render.md"), sb.ToString());
        Debug.Log("[Render] " + sb);
    }

    static string Fijar(SerializedObject so, string campo, float valor)
    {
        var p = so.FindProperty(campo);
        if (p == null) return $"- {campo}: no existe\n";
        string antes;
        if (p.propertyType == SerializedPropertyType.Float) { antes = p.floatValue.ToString("0.##"); p.floatValue = valor; }
        else if (p.propertyType == SerializedPropertyType.Boolean) { antes = p.boolValue.ToString(); p.boolValue = valor > 0.5f; }
        else { antes = p.intValue.ToString(); p.intValue = (int)valor; }
        return $"- {campo}: {antes} → {valor}\n";
    }
}
