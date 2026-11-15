using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Sombras del sol con menos dientes de sierra en el suelo (Ver INC-1010): mapa de 4096 y tres
/// cascadas, con la primera cubriendo los primeros 6 m, donde están los personajes en los planos.
/// Se puede ejecutar varias veces.
public static class SombrasMasFinas
{
    private const string Pipeline = "Assets/Settings/PC_RPAsset.asset";

    [MenuItem("El Sendero/Archivo/Render: sombras del sol más finas (INC-1010)")]
    private static void Aplicar()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        if (pipeline == null) { Debug.LogError("[INC-1010] No encuentro " + Pipeline); return; }
        pipeline.mainLightShadowmapResolution = 4096;
        pipeline.shadowCascadeCount = 3;
        pipeline.cascade3Split = new Vector2(0.12f, 0.4f);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        Debug.Log($"[INC-1010] Sombras: mapa {pipeline.mainLightShadowmapResolution}, {pipeline.shadowCascadeCount} cascadas, distancia {pipeline.shadowDistance} m.");
    }
}
