#ifndef EL_SENDERO_TERRENO_DE_DIBUJO_INCLUDED
#define EL_SENDERO_TERRENO_DE_DIBUJO_INCLUDED

// Pase de color del terreno con el aspecto de dibujo: mezcla las capas como el terreno de URP y
// las ilumina con la misma luz de dos tonos que «El Sendero/Dibujo». Las texturas de las capas se
// acercan a su color medio (global _DibujoTexturaDelSuelo, en AspectoDeDibujo) para que el suelo
// se lea como manchas de color y no como foto. Entre capas no hay degradado: el paso de hierba a
// tierra es un borde de pincel, irregular pero duro (PesosDePincel). Los mapas de normales se ignoran a propósito: con
// ellos la sombra de dos tonos y el contorno de tinta salen moteados.
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
#include "Assets/Shaders/Dibujo/DibujoLuz.hlsl"

half _DibujoTexturaDelSuelo;

// Los dos mapas de capas del terreno (los pone SueloYAguaDeDibujo en el material): con ellos cada
// pasada sabe cuánto pesa cada una de las 8 capas y el borde de pincel sale igual en las dos.
TEXTURE2D(_ControlDeCapas0);
TEXTURE2D(_ControlDeCapas1);
float4 _ControlDeCapas1_TexelSize;

// Mip tan alto que la muestra es el color medio de la textura de la capa.
#define MIP_DEL_COLOR_MEDIO 12.0

#ifndef TERRAIN_SPLAT_BASEPASS
float AzarDelSuelo(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float RuidoDelSuelo(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(AzarDelSuelo(i), AzarDelSuelo(i + float2(1, 0)), f.x),
                lerp(AzarDelSuelo(i + float2(0, 1)), AzarDelSuelo(i + 1.0), f.x), f.y);
}

// Capas con borde de pincel: la capa que más pesa se queda el sitio, con una franja estrecha de
// transición. El terreno con más de 4 capas se pinta en dos pasadas (4 capas cada una) que se
// suman: «otras» son los pesos de las capas de la otra pasada (leídos del otro mapa de capas), así
// el afilado cuenta las 8 y las dos pasadas siempre suman 1. Sin el otro mapa se toma lo que falta
// para 1 como una sola capa. Devuelve los pesos dentro de la pasada (suman 1) y en «cuanto» lo que
// se queda la pasada.
// En float: con media precisión las potencias altas de pesos pequeños se quedan en cero.
float4 Afilar(float4 x) { x = max(x, 0.0); x = x * x; x = x * x; x = x * x; return x * x; }

half4 PesosDePincel(half4 pesos, half4 otras, out half cuanto)
{
    float4 p = Afilar(pesos);
    float mia = dot(p, 1.0), otra = dot(Afilar(otras), 1.0);
    cuanto = (half)saturate(mia / max(mia + otra, 1e-20));
    return (half4)(p / max(mia, 1e-20));
}

// Pesos de las capas de la otra pasada en este punto.
half4 PesosDeLaOtraPasada(float2 uv, half4 pesos)
{
    if (_ControlDeCapas1_TexelSize.z < 8.0)
        return half4(saturate(1.0h - dot(pesos, 1.0h)), 0.0h, 0.0h, 0.0h);
#ifdef TERRAIN_SPLAT_ADDPASS
    return SAMPLE_TEXTURE2D(_ControlDeCapas0, sampler_Control, uv);
#else
    return SAMPLE_TEXTURE2D(_ControlDeCapas1, sampler_Control, uv);
#endif
}

// Desplazamiento del punto donde se lee el mapa de capas (en texeles), para que las fronteras
// entre hierba, tierra y piedra salgan con borde de pincel y no en línea de cuadrícula.
float2 DesplazamientoDePincel(float2 xz)
{
    float2 n = float2(RuidoDelSuelo(xz * 0.3) * 0.65 + RuidoDelSuelo(xz * 1.1 + 5.3) * 0.35,
                      RuidoDelSuelo(xz * 0.3 + 19.1) * 0.65 + RuidoDelSuelo(xz * 1.1 + 31.7) * 0.35);
    return (n - 0.5) * 3.0;
}

half3 TexturaDeLasCapas(float4 uvSplat01, float4 uvSplat23, half4 pesos)
{
    half3 c = half3(0.0h, 0.0h, 0.0h);
    c += SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uvSplat01.xy).rgb * _DiffuseRemapScale0.rgb * pesos.r;
    c += SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uvSplat01.zw).rgb * _DiffuseRemapScale1.rgb * pesos.g;
    c += SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uvSplat23.xy).rgb * _DiffuseRemapScale2.rgb * pesos.b;
    c += SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uvSplat23.zw).rgb * _DiffuseRemapScale3.rgb * pesos.a;
    return c;
}

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
    splatUV += DesplazamientoDePincel(IN.positionWS.xz) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);
    half4 pesosDelMapa = splatControl;
    half alpha;

    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;
    // Deja splatControl normalizado: sirve también para el color medio.
    SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS);

    half cuantaTextura = _DibujoActivo > 0.5h ? _DibujoTexturaDelSuelo : 0.35h;
    half4 pesosDePincel = PesosDePincel(pesosDelMapa, PesosDeLaOtraPasada(splatUV, pesosDelMapa), alpha);
    #ifdef TERRAIN_SPLAT_ADDPASS
    clip(alpha <= 0.005h ? -1.0h : 1.0h);
    #endif
    half3 albedo = lerp(ColorMedioDeLasCapas(IN.uvSplat01, IN.uvSplat23, pesosDePincel),
                        TexturaDeLasCapas(IN.uvSplat01, IN.uvSplat23, pesosDePincel), cuantaTextura);
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
