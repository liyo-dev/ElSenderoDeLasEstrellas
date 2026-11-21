#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Ajustes del aspecto de dibujo puestos el 20 nov 2026 (INC-1346, INC-1347), con su vuelta atrás:
/// suavizado de bordes, línea de tinta de los personajes, luz de personaje, piezas de la cara dentro del
/// contorno y pelo de Will de una sola masa.
public static class AjustesDelAspectoDeDibujo
{
    private const string Personajes = "Assets/Art/Characters";

    // ── Suavizado de bordes (la línea de tinta es geometría y lo aprovecha) ────────────────────────
    private static void Msaa(int muestras)
    {
        var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        asset.msaaSampleCount = muestras;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto", $"MSAA x{asset.msaaSampleCount}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Suavizado de bordes MSAA x4")] private static void Msaa4() => Msaa(4);
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Sin suavizado de bordes")] private static void Msaa1() => Msaa(1);

    // ── Línea de tinta de los personajes ───────────────────────────────────────────────────────────
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Línea de tinta: 2,5 px marrón, se afina de 8 a 25 m")]
    private static void AjustesDeLaLinea()
    {
        var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        var lista = new SerializedObject(asset).FindProperty("m_RendererDataList");
        int n = 0;
        for (int i = 0; i < lista.arraySize; i++)
        {
            var datos = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (datos == null) continue;
            foreach (var f in datos.rendererFeatures.OfType<Kits.InkOutline.InkOutlineFeature>())
            {
                var so = new SerializedObject(f);
                so.FindProperty("widthPixels").floatValue = 2.5f;
                so.FindProperty("thinFrom").floatValue = 8f;
                so.FindProperty("thinTo").floatValue = 25f;
                so.FindProperty("farWidthFraction").floatValue = 0.4f;
                so.FindProperty("color").colorValue = new Color(0.30f, 0.18f, 0.13f, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f); EditorUtility.SetDirty(datos);
                n++;
            }
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto", $"línea de tinta ajustada en {n} renderer(s)");
    }

    // ── Luz de personaje: sombra pequeña y clara ───────────────────────────────────────────────────
    private static void LuzDePersonaje(float valor)
    {
        int n = 0;
        foreach (var m in MaterialesDeDibujo())
        {
            m.SetFloat("_LuzDePersonaje", valor); EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto", $"luz de personaje {valor} en {n} materiales");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Luz de personaje")] private static void LuzDePersonaje1() => LuzDePersonaje(1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Sin luz de personaje")] private static void LuzDePersonaje0() => LuzDePersonaje(0f);

    // ── Piezas de la cara dentro del contorno (stencil) ────────────────────────────────────────────
    // El cuerpo de los personajes escribe la marca 2 y las piezas de la cara (Mat_Caras_*, Mat_Ceja*)
    // solo se pintan donde está, después del cuerpo.
    private static void CaraDentroDelContorno(bool si)
    {
        int cuerpo = 0, cara = 0;
        foreach (var m in MaterialesDeDibujo())
        {
            bool esCara = m.name.StartsWith("Mat_Caras_") || m.name.StartsWith("Mat_Ceja");
            if (!si) { m.SetInt("_StencilRef", 0); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilPass", 0f); if (esCara) m.renderQueue = -1; }
            else if (esCara) { m.SetInt("_StencilRef", 2); m.SetFloat("_StencilComp", 3f); m.SetFloat("_StencilPass", 0f); m.renderQueue = m.IsKeywordEnabled("_ALPHATEST_ON") ? 2460 : 2010; cara++; }
            else { m.SetInt("_StencilRef", 2); m.SetFloat("_StencilComp", 8f); m.SetFloat("_StencilPass", 2f); cuerpo++; }
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto", si ? $"cara dentro del contorno: {cuerpo} de cuerpo, {cara} de cara" : "cara dentro del contorno: quitado");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Piezas de la cara dentro del contorno")] private static void CaraDentro() => CaraDentroDelContorno(true);
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Piezas de la cara sin recortar")] private static void CaraComoAntes() => CaraDentroDelContorno(false);

    // ── Pelo de Will de una sola masa ──────────────────────────────────────────────────────────────
    private static void TonoClaroDelPelo(Color tinte)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Personajes + "/Protagonistas/Will/Materiales/Mat_Hair16_Heroe_PeloClaro.mat");
        m.SetColor("_Tinte", tinte); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto", $"tono claro del pelo de Will: {tinte}");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Pelo de Will de una sola masa")] private static void PeloDeUnaMasa() => TonoClaroDelPelo(new Color(0.89f, 0.81f, 0.70f, 1f));
    [MenuItem("El Sendero/Aspecto de dibujo/Ajustes/Pelo de Will con los picos claros")] private static void PeloComoAntes() => TonoClaroDelPelo(Color.white);

    private static System.Collections.Generic.IEnumerable<Material> MaterialesDeDibujo()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Personajes }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.shader != null && m.shader.name == "El Sendero/Dibujo") yield return m;
        }
    }
}
#endif
