using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Saca el post-procesado de Quibli del juego (Raúl, 18 nov: «no quiero tener nada de Quibli»).
/// - En cada perfil de volumen, el Color Grading de Quibli pasa a los de URP: contraste y saturación
///   a Color Adjustments; los tintes de sombras y luces a Split Toning. Después se quita.
/// - El Stylized Detail de Quibli no tiene equivalente en URP: se quita y se apunta en el informe.
/// - El renderer feature QuibliPostProcess sale de los renderers de URP.
/// Copia de lo tocado en «Versiones antiguas/Postprocesado de Quibli (fecha)». Informe en Guiones/_ordenes/quibli.md.
/// Se lee Quibli por reflexión, sin referenciar sus tipos: así compila igual cuando se borre el pack.
public static class QuitarQuibliDelPostproceso
{
    const string NombreColorGrading = "ColorGrading";
    const string NombreDetalle = "StylizedDetail";
    const string EspacioDeQuibli = "CompoundRendererFeature.PostProcess";
    const string FeatureDeQuibli = "QuibliPostProcess";

    [MenuItem("El Sendero/Aspecto de dibujo/Postprocesado: quitar Quibli y pasar su color a URP")]
    static void Quitar()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Quibli] Sal de Play antes."); return; }
        string copia = Path.Combine("Versiones antiguas", $"Postprocesado de Quibli ({System.DateTime.Now:yyyy-MM-dd})");
        Directory.CreateDirectory(copia);
        var informe = new StringBuilder("# Quibli fuera del post-procesado\n\n");

        foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.Contains("/Quibli/")) continue;
            var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            var deQuibli = perfil.components.Where(EsDeQuibli).ToList();
            if (deQuibli.Count == 0) continue;
            File.Copy(ruta, Path.Combine(copia, Path.GetFileName(ruta)), true);
            informe.Append($"## {ruta}\n");
            foreach (var c in deQuibli)
            {
                if (c.GetType().Name == NombreColorGrading) informe.Append(PasarColor(perfil, c));
                else if (c.GetType().Name == NombreDetalle)
                    informe.Append(c.active ? "- Stylized Detail activo: se quita (sin equivalente en URP).\n" : "- Stylized Detail apagado: se quita.\n");
                perfil.components.Remove(c);
                AssetDatabase.RemoveObjectFromAsset(c);
                Object.DestroyImmediate(c, true);
            }
            EditorUtility.SetDirty(perfil);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var datos = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(ruta);
            var feature = datos.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == FeatureDeQuibli);
            if (feature == null) continue;
            File.Copy(ruta, Path.Combine(copia, Path.GetFileName(ruta)), true);
            var so = new SerializedObject(datos);
            var lista = so.FindProperty("m_RendererFeatures");
            var mapa = so.FindProperty("m_RendererFeatureMap");
            int i = datos.rendererFeatures.IndexOf(feature);
            lista.DeleteArrayElementAtIndex(i);
            if (lista.arraySize > datos.rendererFeatures.Count - 1) lista.DeleteArrayElementAtIndex(i);
            mapa.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.RemoveObjectFromAsset(feature);
            Object.DestroyImmediate(feature, true);
            EditorUtility.SetDirty(datos);
            informe.Append($"## {ruta}\n- Renderer feature {FeatureDeQuibli} quitado.\n");
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "quibli.md"), informe.ToString());
        Debug.Log("[Quibli] " + informe);
    }

    /// Qué assets de las escenas de la build y de Resources salen de las carpetas de Quibli.
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: qué del juego depende aún de Quibli")]
    static void Informe()
    {
        var raices = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToList();
        raices.AddRange(AssetDatabase.FindAssets("", new[] { "Assets/Resources" }).Select(AssetDatabase.GUIDToAssetPath));
        var porRaiz = raices.ToDictionary(r => r, r => new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(r, true)));
        var deps = porRaiz.Values.SelectMany(d => d).Distinct();
        var deQuibli = deps.Where(d => d.Contains("/Quibli/") || d.Contains("Quibli")).OrderBy(d => d).ToList();
        var texto = new StringBuilder($"# Dependencias de Quibli en la build ({deQuibli.Count})\n\n");
        foreach (string d in deQuibli)
        {
            var quien = raices.Where(r => porRaiz[r].Contains(d)).Take(3);
            texto.Append($"- {d}  ← {string.Join(", ", quien)}\n");
        }
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "quibli_dependencias.md"), texto.ToString());
        Debug.Log("[Quibli] " + texto);
    }

    static bool EsDeQuibli(VolumeComponent c) => c != null && c.GetType().Namespace == EspacioDeQuibli;

    static float Valor(VolumeComponent c, string campo)
    {
        var f = c.GetType().GetField(campo);
        return f?.GetValue(c) is VolumeParameter<float> p && p.overrideState ? p.value : 0f;
    }

    static string PasarColor(VolumeProfile perfil, VolumeComponent cg)
    {
        float fuerza = Valor(cg, "intensity");
        if (!cg.active || fuerza <= 0f) return "- Color Grading apagado: se quita.\n";
        float contraste = Valor(cg, "contrast") * 65f * fuerza;
        float saturacion = (Valor(cg, "saturation") * 0.5f + Valor(cg, "vibrance") * 0.3f) * 100f * fuerza;
        if (!perfil.TryGet(out ColorAdjustments ajustes)) ajustes = perfil.Add<ColorAdjustments>();
        ajustes.active = true;
        if (contraste != 0f) { ajustes.contrast.overrideState = true; ajustes.contrast.value = Mathf.Clamp(ajustes.contrast.value + contraste, -100f, 100f); }
        if (saturacion != 0f) { ajustes.saturation.overrideState = true; ajustes.saturation.value = Mathf.Clamp(ajustes.saturation.value + saturacion, -100f, 100f); }
        string texto = $"- Color Grading → Color Adjustments: contraste +{contraste:0.#}, saturación +{saturacion:0.#}.\n";

        float azul = Valor(cg, "blueShadows") * fuerza, verde = Valor(cg, "greenShadows") * fuerza, rojo = Valor(cg, "redHighlights") * fuerza;
        if (azul + verde + rojo > 0f && !perfil.Has<SplitToning>())
        {
            var tono = perfil.Add<SplitToning>();
            tono.active = true;
            tono.shadows.overrideState = true;
            tono.shadows.value = new Color(0.5f, 0.5f + 0.25f * verde, 0.5f + 0.25f * azul);
            tono.highlights.overrideState = true;
            tono.highlights.value = new Color(0.5f + 0.25f * rojo, 0.5f, 0.5f);
            texto += $"- Tintes (azul {azul:0.##}, verde {verde:0.##}, rojo {rojo:0.##}) → Split Toning.\n";
        }
        return texto;
    }
}
