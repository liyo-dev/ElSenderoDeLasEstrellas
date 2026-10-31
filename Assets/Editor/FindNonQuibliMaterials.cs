using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para localizar renderers en la escena abierta que
/// todavía usan un shader que NO es de Quibli (es decir, candidatos a
/// convertir para que encajen con el "look" de Quibli).
///
/// Instalación: coloca este archivo en cualquier carpeta llamada "Editor"
/// dentro de Assets (por ejemplo Assets/Editor/FindNonQuibliMaterials.cs).
///
/// Uso:
///   Tools > Quibli > Buscar materiales sin Quibli (en la escena)
///     -- lista TODO lo que no sea Quibli, incluyendo VFX/agua/partículas
///        (que normalmente NO necesitan shading toon a propósito).
///   Tools > Quibli > Buscar materiales sin Quibli (solo geometria, filtrado)
///     -- la misma búsqueda pero ignora ParticleSystem/Trail/Line renderers
///        y shaders de VFX/agua/partículas/niebla/fuego/cielo conocidos, para
///        quedarte solo con los candidatos reales a "pegote" visual (props,
///        personajes, terreno, edificios).
/// El resultado aparece en la consola, y también se selecciona en la
/// Hierarchy cada GameObject afectado para que sea fácil de revisar.
///
///   Tools > Quibli > Pasar a Quibli los materiales seleccionados (o carpetas)
///     -- convierte los materiales URP Lit / Simple Lit / Standard de la
///        selección (materiales sueltos o carpetas enteras) a Quibli
///        StylizedLit con los ajustes de la plantilla (Chuchelandia), y
///        conserva la textura, el color y la emisión de cada uno. Los demás
///        shaders (agua, cielo, partículas...) se dejan como están.
/// </summary>
public static class FindNonQuibliMaterials
{
    // Patrones de nombre de shader que se consideran "normales sin Quibli" a
    // propósito -- VFX, agua, niebla, partículas, cielo, etc. No suelen
    // llevar shading toon y no son la causa de "pegotes" de estilo.
    private static readonly Regex[] ExpectedNonQuibliShaderPatterns =
    {
        new Regex(@"particle", RegexOptions.IgnoreCase),
        new Regex(@"\bvfx\b", RegexOptions.IgnoreCase),
        new Regex(@"water", RegexOptions.IgnoreCase),
        new Regex(@"fog", RegexOptions.IgnoreCase),
        new Regex(@"fire", RegexOptions.IgnoreCase),
        new Regex(@"smoke", RegexOptions.IgnoreCase),
        new Regex(@"additive", RegexOptions.IgnoreCase),
        new Regex(@"alpha blended", RegexOptions.IgnoreCase),
        new Regex(@"skybox", RegexOptions.IgnoreCase),
        new Regex(@"sky", RegexOptions.IgnoreCase),
        new Regex(@"projector", RegexOptions.IgnoreCase),
        new Regex(@"glow", RegexOptions.IgnoreCase),
        new Regex(@"spark", RegexOptions.IgnoreCase),
        new Regex(@"plasma", RegexOptions.IgnoreCase),
        new Regex(@"portal", RegexOptions.IgnoreCase),
    };

    [MenuItem("Tools/Quibli/Buscar materiales sin Quibli (en la escena)")]
    private static void FindInScene()
    {
        Run(filtered: false);
    }

    [MenuItem("Tools/Quibli/Buscar materiales sin Quibli (solo geometria, filtrado)")]
    private static void FindInSceneFiltered()
    {
        Run(filtered: true);
    }

    private static void Run(bool filtered)
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

        var offenders = new List<(GameObject go, Material mat, string shaderPath)>();
        int skippedByComponent = 0;
        int skippedByShaderName = 0;

        foreach (var r in renderers)
        {
            if (filtered && IsExpectedVfxRenderer(r))
            {
                skippedByComponent++;
                continue;
            }

            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;

                string shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                string shaderName = mat.shader.name;
                bool isQuibli = (!string.IsNullOrEmpty(shaderPath) && shaderPath.Contains("/Quibli/")) ||
                                 shaderName.StartsWith("Quibli/") ||
                                 shaderName.Contains("Quibli");

                if (isQuibli) continue;

                if (filtered && IsExpectedNonQuibliShader(shaderName))
                {
                    skippedByShaderName++;
                    continue;
                }

                offenders.Add((r.gameObject, mat, string.IsNullOrEmpty(shaderPath) ? "(built-in)" : shaderPath));
            }
        }

        string modeLabel = filtered ? " (filtrado, solo geometría)" : "";

        if (offenders.Count == 0)
        {
            Debug.Log($"[Quibli Check{modeLabel}] No se encontró ningún renderer relevante en la escena usando un shader que no sea de Quibli. " +
                      (filtered ? $"(Descartados por ser VFX/partículas/agua/etc.: {skippedByComponent} renderers por componente, {skippedByShaderName} materiales por nombre de shader.)" : "Todo en orden."));
            return;
        }

        Debug.Log($"[Quibli Check{modeLabel}] Se encontraron {offenders.Count} referencias (GameObject + material) que NO usan un shader de Quibli" +
                  (filtered ? $" (tras descartar {skippedByComponent} renderers VFX y {skippedByShaderName} materiales de shaders esperables sin Quibli):" : ":"));

        // Agrupar por material para un resumen más legible.
        var byMaterial = offenders.GroupBy(o => o.mat);
        foreach (var group in byMaterial.OrderBy(g => g.Key.name))
        {
            var shaderPath = group.First().shaderPath;
            var shaderName = group.Key.shader != null ? group.Key.shader.name : "(sin shader)";
            var gos = string.Join(", ", group.Select(o => o.go.name).Distinct().Take(10));
            Debug.Log($"  · Material '{group.Key.name}' — shader: {shaderName} ({shaderPath}) — usado en: {gos}" +
                      (group.Select(o => o.go.name).Distinct().Count() > 10 ? " ..." : ""));
        }

        // Seleccionar todos los GameObjects afectados en la Hierarchy.
        Selection.objects = offenders.Select(o => (Object)o.go).Distinct().ToArray();
    }

    // Material Quibli de referencia: el look de Chuchelandia (Sweet_Land, ithappy).
    private const string RutaPlantillaQuibli = "Assets/Art/World/ithappy/Sweet_Land/Materials/Color.mat";

    // Shaders opacos de iluminación estándar que se pueden pasar a Quibli sin perder nada.
    private static readonly HashSet<string> ShadersConvertibles = new HashSet<string>
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Baked Lit",
        "Standard",
    };

    [MenuItem("Tools/Quibli/Pasar a Quibli los materiales seleccionados (o carpetas)")]
    private static void PasarSeleccionAQuibli()
    {
        var plantilla = AssetDatabase.LoadAssetAtPath<Material>(RutaPlantillaQuibli);
        if (plantilla == null || plantilla.shader == null)
        {
            Debug.LogError($"[Quibli] No se encuentra la plantilla en {RutaPlantillaQuibli}.");
            return;
        }

        var rutas = new SortedSet<string>();
        foreach (var obj in Selection.objects)
        {
            string ruta = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(ruta)) continue;
            if (AssetDatabase.IsValidFolder(ruta))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ruta }))
                    rutas.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else if (obj is Material)
            {
                rutas.Add(ruta);
            }
        }

        if (rutas.Count == 0)
        {
            Debug.LogWarning("[Quibli] Selecciona en el Project materiales o carpetas con materiales.");
            return;
        }

        var convertidos = new List<string>();
        var omitidos = new List<string>();
        foreach (var ruta in rutas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == null || mat.shader == plantilla.shader) continue;
            if (!ShadersConvertibles.Contains(mat.shader.name))
            {
                omitidos.Add($"{mat.name} ({mat.shader.name})");
                continue;
            }

            Undo.RecordObject(mat, "Pasar a Quibli");
            PasarAQuibli(mat, plantilla);
            EditorUtility.SetDirty(mat);
            convertidos.Add(mat.name);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Quibli] Pasados a Quibli: {convertidos.Count} ({string.Join(", ", convertidos)})." +
                  (omitidos.Count > 0 ? $" Sin tocar por su shader: {string.Join(", ", omitidos)}." : ""));
    }

    private static void PasarAQuibli(Material mat, Material plantilla)
    {
        Texture mapaBase = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
        Vector2 escala = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : mat.mainTextureScale;
        Vector2 desplazamiento = mat.HasProperty("_BaseMap") ? mat.GetTextureOffset("_BaseMap") : mat.mainTextureOffset;
        Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
        Color emision = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        Texture mapaEmision = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
        bool emite = mat.IsKeywordEnabled("_EMISSION") && emision.maxColorComponent > 0f;

        mat.shader = plantilla.shader;
        mat.CopyPropertiesFromMaterial(plantilla);

        mat.SetTexture("_BaseMap", mapaBase);
        mat.SetTextureScale("_BaseMap", escala);
        mat.SetTextureOffset("_BaseMap", desplazamiento);
        mat.SetColor("_BaseColor", color);

        if (emite)
        {
            mat.SetColor("_EmissionColor", emision);
            if (mat.HasProperty("_EmissionColorUI")) mat.SetColor("_EmissionColorUI", emision);
            if (mat.HasProperty("_EmissionScaleUI")) mat.SetFloat("_EmissionScaleUI", 1f);
            if (mapaEmision != null) mat.SetTexture("_EmissionMap", mapaEmision);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
    }

    private static bool IsExpectedVfxRenderer(Renderer r)
    {
        return r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer;
    }

    private static bool IsExpectedNonQuibliShader(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName)) return false;
        foreach (var pattern in ExpectedNonQuibliShaderPatterns)
        {
            if (pattern.IsMatch(shaderName)) return true;
        }
        return false;
    }
}
