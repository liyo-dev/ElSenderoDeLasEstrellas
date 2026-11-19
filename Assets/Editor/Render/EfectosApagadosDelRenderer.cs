#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Un efecto del renderer (Renderer Feature) apagado pero aún en la lista rompe la build: URP quita
/// de la build los recursos de lo que nadie usa, el efecto intenta crearse igualmente al arrancar,
/// falla, y el pipeline entero no llega a crearse (pantalla en negro). Ver INC-1306.
///
/// - Menú: quita de todos los renderers en uso los efectos apagados.
/// - Al compilar la build: si queda alguno, la build se para con el nombre del renderer y del efecto.
public static class EfectosApagadosDelRenderer
{
    private const string Menu = "El Sendero/Render/Quitar efectos apagados de los renderers";

    [MenuItem(Menu)]
    public static void QuitarApagados()
    {
        var informe = new StringBuilder();
        int quitados = 0;

        foreach (var datos in RenderersEnUso())
        {
            var so = new SerializedObject(datos);
            var lista = so.FindProperty("m_RendererFeatures");
            var mapa = so.FindProperty("m_RendererFeatureMap");
            var aDestruir = new List<Object>();

            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                var elemento = lista.GetArrayElementAtIndex(i);
                if (elemento.objectReferenceValue is not ScriptableRendererFeature efecto || efecto.isActive) continue;

                aDestruir.Add(efecto);
                informe.Append(datos.name).Append(" ▸ ").Append(efecto.name).Append('\n');
                elemento.objectReferenceValue = null;
                lista.DeleteArrayElementAtIndex(i);
                if (mapa != null && i < mapa.arraySize) mapa.DeleteArrayElementAtIndex(i);
            }

            if (aDestruir.Count == 0) continue;

            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var efecto in aDestruir) Object.DestroyImmediate(efecto, true);
            EditorUtility.SetDirty(datos);
            quitados += aDestruir.Count;
        }

        if (quitados > 0) AssetDatabase.SaveAssets();

        string mensaje = quitados == 0
            ? "No había efectos apagados en los renderers en uso."
            : "Quitados " + quitados + ":\n" + informe;
        Debug.Log("[Render] " + mensaje);
        OrdenesDelEditor.Avisar("Efectos apagados", mensaje);
    }

    /// Los renderers que usa algún pipeline del proyecto (el de Graphics y el de cada nivel de calidad).
    private static IEnumerable<ScriptableRendererData> RenderersEnUso()
    {
        var pipelines = new HashSet<RenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline != null) pipelines.Add(GraphicsSettings.defaultRenderPipeline);
        for (int nivel = 0; nivel < QualitySettings.names.Length; nivel++)
        {
            var rp = QualitySettings.GetRenderPipelineAssetAt(nivel);
            if (rp != null) pipelines.Add(rp);
        }

        var vistos = new HashSet<ScriptableRendererData>();
        foreach (var rp in pipelines)
        {
            if (rp is not UniversalRenderPipelineAsset) continue;
            var lista = new SerializedObject(rp).FindProperty("m_RendererDataList");
            if (lista == null) continue;
            for (int i = 0; i < lista.arraySize; i++)
            {
                if (lista.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData datos && vistos.Add(datos))
                    yield return datos;
            }
        }
    }

    private sealed class ComprobacionAntesDeLaBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var apagados = new StringBuilder();
            foreach (var datos in RenderersEnUso())
            {
                foreach (var efecto in datos.rendererFeatures)
                {
                    if (efecto != null && !efecto.isActive)
                        apagados.Append(datos.name).Append(" ▸ ").Append(efecto.name).Append("; ");
                }
            }

            if (apagados.Length > 0)
                throw new BuildFailedException(
                    "Hay efectos del renderer apagados que dejarían la build en negro: " + apagados +
                    "Quítalos con «" + Menu + "».");
        }
    }
}
#endif
