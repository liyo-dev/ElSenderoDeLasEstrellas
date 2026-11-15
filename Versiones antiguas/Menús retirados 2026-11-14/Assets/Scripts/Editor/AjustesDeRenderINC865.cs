using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Aplica la tanda de render de INC-865 sobre los assets (vía AssetDatabase, nunca editando su
/// YAML por fuera): métricas de GPU en F9, MSAA fuera y SMAA en la cámara del jugador, DoF Bokeh
/// fuera del perfil de juego, contorno de tinta en PC_Renderer, renderer ligero para los retratos
/// vivos y el shader de la sombra de contacto incluido en las builds. Se puede ejecutar varias
/// veces: lo que ya está hecho no se repite.
public static class AjustesDeRenderINC865
{
    private const string Pipeline = "Assets/Settings/PC_RPAsset.asset";
    private const string Renderer = "Assets/Settings/PC_Renderer.asset";
    private const string RendererDeRetratos = "Assets/Settings/" + Sendero.UI.EstudioDeRetratos.RendererDeRetratos + ".asset";
    private const string Camara = "Assets/Prefabs/CamaraDelJugador.prefab";
    private const string PerfilDeJuego = "Assets/Settings/Volumenes/Volumen Profile.asset";
    private const string ShaderDelContorno = "Hidden/El Sendero/Contorno de tinta";
    private const string ShaderDeLaSombra = "El Sendero/Sombra de contacto";

    [MenuItem("El Sendero/Archivo/Render: aplicar contorno de tinta, sombras de contacto y ajustes de rendimiento (INC-865)")]
    private static void Aplicar()
    {
        var informe = new StringBuilder("[INC-865] Ajustes de render:\n");

        var jugador = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/ProjectSettings.asset"));
        var tiempos = jugador.FindProperty("enableFrameTimingStats");
        if (tiempos != null && !tiempos.boolValue)
        {
            tiempos.boolValue = true;
            jugador.ApplyModifiedPropertiesWithoutUndo();
            informe.AppendLine("· Frame Timing Stats activado (F9 ya puede medir ms de GPU y CPU).");
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        if (pipeline == null) { Debug.LogError("[INC-865] No encuentro " + Pipeline); return; }
        if (pipeline.msaaSampleCount != 1)
        {
            pipeline.msaaSampleCount = 1;
            EditorUtility.SetDirty(pipeline);
            informe.AppendLine("· MSAA desactivado en PC_RPAsset.");
        }

        using (var prefab = new PrefabUtility.EditPrefabContentsScope(Camara))
        {
            foreach (var datos in prefab.prefabContentsRoot.GetComponentsInChildren<UniversalAdditionalCameraData>(true))
            {
                if (datos.renderType != CameraRenderType.Base) continue;
                if (datos.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing &&
                    datos.antialiasingQuality == AntialiasingQuality.High) continue;
                datos.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                datos.antialiasingQuality = AntialiasingQuality.High;
                informe.AppendLine("· SMAA alto en " + datos.name + " (antes FXAA + MSAA 4x).");
            }
        }

        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PerfilDeJuego);
        if (perfil != null && perfil.TryGet<DepthOfField>(out var dof) && dof.active)
        {
            dof.active = false;
            EditorUtility.SetDirty(dof);
            EditorUtility.SetDirty(perfil);
            informe.AppendLine("· Depth of Field (Bokeh) apagado en Volumen Profile: el desenfoque de fondo lo hace Stylized Detail.");
        }
        AvisarDeOtrosBokeh(informe);

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer);
        if (renderer == null) { Debug.LogError("[INC-865] No encuentro " + Renderer); return; }
        CrearRendererDeRetratos(pipeline, informe);
        AnadirContorno(renderer, informe);
        IncluirShaderEnBuilds(ShaderDeLaSombra, informe);

        AssetDatabase.SaveAssets();
        informe.AppendLine("Hecho. Prueba en Play: contorno, sombras bajo los personajes y retratos del HUD.");
        Debug.Log(informe.ToString());
    }

    private static void AvisarDeOtrosBokeh(StringBuilder informe)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta == PerfilDeJuego || ruta.StartsWith("Assets/Plugins/")) continue;
            var otro = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            if (otro != null && otro.TryGet<DepthOfField>(out var d) && d.active && d.mode.overrideState &&
                d.mode.value == DepthOfFieldMode.Bokeh)
                informe.AppendLine("  (revisar) DoF Bokeh activo también en " + ruta);
        }
    }

    private static void AnadirContorno(UniversalRendererData renderer, StringBuilder informe)
    {
        bool hay = false;
        foreach (var f in renderer.rendererFeatures)
            if (f is ContornoDeTintaFeature) hay = true;

        if (!hay)
        {
            var shader = Shader.Find(ShaderDelContorno);
            if (shader == null) { Debug.LogError("[INC-865] No encuentro el shader " + ShaderDelContorno); return; }
            var feature = ScriptableObject.CreateInstance<ContornoDeTintaFeature>();
            feature.name = "ContornoDeTinta";
            var so = new SerializedObject(feature);
            so.FindProperty("shader").objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            informe.AppendLine("· Contorno de tinta añadido a PC_Renderer.");
        }
        RehacerMapaDeFeatures(renderer);
    }

    /// El mapa de features guarda el id local de cada una, en el mismo orden que la lista.
    private static void RehacerMapaDeFeatures(ScriptableRendererData renderer)
    {
        var datos = new SerializedObject(renderer);
        var mapa = datos.FindProperty("m_RendererFeatureMap");
        var features = renderer.rendererFeatures;
        mapa.arraySize = features.Count;
        for (int i = 0; i < features.Count; i++)
        {
            long id = 0;
            if (features[i] != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(features[i], out string _, out id);
            mapa.GetArrayElementAtIndex(i).longValue = id;
        }
        datos.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
    }

    private static void CrearRendererDeRetratos(UniversalRenderPipelineAsset pipeline, StringBuilder informe)
    {
        var retratos = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDeRetratos);
        if (retratos == null)
        {
            if (!AssetDatabase.CopyAsset(Renderer, RendererDeRetratos)) { Debug.LogError("[INC-865] No pude copiar PC_Renderer."); return; }
            retratos = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDeRetratos);
            var sobrantes = new List<ScriptableRendererFeature>(retratos.rendererFeatures);
            retratos.rendererFeatures.Clear();
            foreach (var f in sobrantes)
            {
                if (f == null) continue;
                AssetDatabase.RemoveObjectFromAsset(f);
                Object.DestroyImmediate(f, true);
            }
            var so = new SerializedObject(retratos);
            so.FindProperty("m_RendererFeatureMap").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            retratos.SetDirty();
            EditorUtility.SetDirty(retratos);
            informe.AppendLine("· Creado Retratos_Renderer (sin SSAO ni post) para las cámaras de los retratos vivos.");
        }

        foreach (var r in pipeline.rendererDataList)
            if (r == retratos) return;
        var datos = new SerializedObject(pipeline);
        var lista = datos.FindProperty("m_RendererDataList");
        lista.arraySize++;
        lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = retratos;
        datos.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        informe.AppendLine("· Retratos_Renderer añadido a la lista de renderers de PC_RPAsset.");
    }

    private static void IncluirShaderEnBuilds(string nombre, StringBuilder informe)
    {
        var shader = Shader.Find(nombre);
        if (shader == null) { Debug.LogError("[INC-865] No encuentro el shader " + nombre); return; }
        var ajustes = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(ajustes);
        var lista = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        lista.arraySize++;
        lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.AppendLine("· Shader de la sombra de contacto incluido en las builds.");
    }
}
