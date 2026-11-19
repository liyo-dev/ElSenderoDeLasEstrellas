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

    /// Flores y hierba del terreno (detalles) a 400 m eran miles de puntitos lejanos que centelleaban y costaban
    /// mucho; con el dibujo, de lejos se lee mejor el color del suelo. Y el terreno se teselaba a 0,5 px de error.
    [MenuItem("El Sendero/Aspecto de dibujo/Render: terreno (distancia de flores y detalle) (escenas abiertas)")]
    static void TerrenoEnLasAbiertas() => Terreno(false);

    [MenuItem("El Sendero/Aspecto de dibujo/Render: terreno (distancia de flores y detalle) (todas las escenas jugables)")]
    static void TerrenoEnLasJugables() => Terreno(true);

    const float DistanciaDeFlores = 90f;
    const float ErrorDelTerreno = 3f;

    static void Terreno(bool todasLasJugables)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Render] Sal de Play antes."); return; }
        var sb = new StringBuilder("# Terreno\n\n");
        EscenasJugables.ParaCada(todasLasJugables, TerrenoEn, sb);
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "terreno.md"), sb.ToString());
    }

    static bool TerrenoEn(UnityEngine.SceneManagement.Scene escena, StringBuilder sb)
    {
        bool cambia = false;
        int terrenos = 0;
        foreach (var t in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)))
        {
            terrenos++;
            if (Mathf.Approximately(t.detailObjectDistance, DistanciaDeFlores) && Mathf.Approximately(t.heightmapPixelError, ErrorDelTerreno))
            {
                sb.Append($"- {escena.name} / {t.name}: ya estaba (flores {DistanciaDeFlores} m, error {ErrorDelTerreno})\n");
                continue;
            }
            Undo.RecordObject(t, "Terreno de dibujo");
            sb.Append($"- {escena.name} / {t.name}: flores {t.detailObjectDistance} → {DistanciaDeFlores} m, error {t.heightmapPixelError} → {ErrorDelTerreno}\n");
            t.detailObjectDistance = DistanciaDeFlores;
            t.heightmapPixelError = ErrorDelTerreno;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            EditorUtility.SetDirty(t);
            cambia = true;
        }
        if (terrenos == 0) sb.Append($"- {escena.name}: sin terreno\n");
        return cambia;
    }

    /// Borde de luz en los materiales de los personajes: los de los SkinnedMeshRenderer de las escenas abiertas
    /// y de los prefabs de Assets/Prefabs y Assets/_NPCs.
    [MenuItem("El Sendero/Aspecto de dibujo/Render: borde de luz en los personajes")]
    static void BordeDeLuz()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Render] Sal de Play antes."); return; }
        var materiales = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)) materiales.UnionWith(r.sharedMaterials.Where(m => m != null));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/_NPCs" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) materiales.UnionWith(r.sharedMaterials.Where(m => m != null));
        }
        int n = 0;
        foreach (var m in materiales)
        {
            if (m.shader.name != "El Sendero/Dibujo" || !AssetDatabase.Contains(m)) continue;
            m.SetFloat("_BordeDeLuz", 0.45f);
            EditorUtility.SetDirty(m);
            n++;
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "borde_de_luz.md"), $"# Borde de luz\n\n- Materiales de personaje con borde: {n}\n");
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
