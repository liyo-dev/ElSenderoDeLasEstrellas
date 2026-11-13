#ifndef EL_SENDERO_TERRENO_DE_DIBUJO_INCLUDED
#define EL_SENDERO_TERRENO_DE_DIBUJO_INCLUDED

// Pase de color del terreno con el aspecto de dibujo: mezcla las capas como el terreno de URP y
// las ilumina con la misma luz de dos tonos que «El Sendero/Dibujo». Las texturas de las capas se
// acercan a su color medio (global _DibujoTexturaDelSuelo, en AspectoDeDibujo) para que el suelo
// se lea como manchas de color y no como foto. Los mapas de normales se ignoran a propósito: con
// ellos la sombra de dos tonos y el contorno de tinta salen moteados.
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
#include "Assets/Shaders/Dibujo/DibujoLuz.hlsl"

half _DibujoTexturaDelSuelo;

// Mip tan alto que la muestra es el color medio de la textura de la capa.
#define MIP_DEL_COLOR_MEDIO 12.0

#ifndef TERRAIN_SPLAT_BASEPASS
half3 ColorMedioDeLasCapas(float4 uvSplat01, float4 uvSplat23, half4 pesos)
{
    half3 c = half3(0.0h, 0.0h, 0.0h);
    c += SAMPLE_TEXTURE2D_LOD(_Splat0, sampler_Splat0, uvSplat01.xy, MIP_DEL_COLOR_MEDIO).rgb * _DiffuseRemapScale0.rgb * pesos.r;
    c += SAMPLE_TEXTURE2D_LOD(_Splat1, sampler_Splat0, uvSplat01.zw, MIP_DEL_COLOR_MEDIO).rgb * _DiffuseRemapScale1.rgb * pesos.g;
    c += SAMPLE_TEXTURE2D_LOD(_Splat2, sampler_Splat0, uvSplat23.xy, MIP_DEL_COLOR_MEDIO).rgb * _DiffuseRemapScale2.rgb * pesos.b;
    c += SAMPLE_TEXTURE2D_LOD(_Splat3, sampler_Splat0, uvSplat23.zw, MIP_DEL_COLOR_MEDIO).rgb * _DiffuseRemapScale3.rgb * pesos.a;
    return c;
}
#endif

void FragmentoTerrenoDeDibujo(
    Varyings IN
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef _ALPHATEST_ON
    ClipHoles(IN.uvMainAndLM.xy);
#endif

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
#ifdef TERRAIN_SPLAT_BASEPASS
    // De lejos el terreno usa el mapa base ya horneado: la textura ya llega promediada por distancia.
    half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMainAndLM.xy).rgb;
    half alpha = 1.0h;
#else
    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);
    half alpha = dot(splatControl, 1.0h);

    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;
    // Deja splatControl normalizado: sirve también para el color medio.
    SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS);

    half cuantaTextura = _DibujoActivo > 0.5h ? _DibujoTexturaDelSuelo : 0.35h;
    half3 albedo = lerp(ColorMedioDeLasCapas(IN.uvSplat01, IN.uvSplat23, splatControl), mixedDiffuse.rgb, cuantaTextura);
#endif

    InputData inputData;
    InitializeInputData(IN, normalTS, inputData);

    half4 color = half4(ColorDeDibujo(inputData, albedo, 0.0h), alpha);
    SplatmapFinalColor(color, inputData.fogCoord);
    outColor = half4(color.rgb, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
