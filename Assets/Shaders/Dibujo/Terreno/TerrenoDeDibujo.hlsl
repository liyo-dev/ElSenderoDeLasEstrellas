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
// Nieve de las cumbres (AspectoDeDibujo): x = cota desde la que hay nieve, y = cuánto sube y baja el borde.
float4 _DibujoNieve;
half4 _DibujoColorDeLaNieve;

// Nieve por altura: el borde es una curva suave (ondas de unos 60 a 130 m en planta), no el contorno del mapa de
// capas, que en las paredes sale en dientes.
half3 NieveDeLasCumbres(half3 albedo, float3 posicion)
{
    if (_DibujoNieve.x <= 0.0) return albedo;
    float ondas = sin(posicion.x * 0.055) + 0.8 * sin(posicion.z * 0.047 + 1.3) + 0.5 * sin((posicion.x - posicion.z) * 0.11 + 0.6);
    float borde = _DibujoNieve.x + _DibujoNieve.y * ondas * 0.5;
    return lerp(albedo, _DibujoColorDeLaNieve.rgb, (half)smoothstep(borde - 0.6, borde + 0.6, posicion.y));
}

// Todos los mapas de capas del terreno (4 capas cada uno; los pone SueloYAguaDeDibujo en el
// material, con cuántos hay en _MapasDeCapas): con ellos cada pasada sabe cuánto pesan todas las
// capas, también las de las demás pasadas, y el borde de pincel sale igual en todas. Un terreno con
// más de 8 capas se pinta en más de dos pasadas: si una pasada solo mirase a otra, las demás se
// sumarían encima y saldrían manchas dobles y transparentes.
TEXTURE2D(_ControlDeCapas0);
TEXTURE2D(_ControlDeCapas1);
TEXTURE2D(_ControlDeCapas2);
TEXTURE2D(_ControlDeCapas3);
TEXTURE2D(_ControlDeCapas4);
TEXTURE2D(_ControlDeCapas5);
float _MapasDeCapas;

// Mip tan alto que la muestra es el color medio de la textura de la capa.
#define MIP_DEL_COLOR_MEDIO 12.0

// Paredes de roca: toda superficie más empinada que _InclinacionDePared se pinta con la capa de
// pared (la pone SueloYAguaDeDibujo), esté donde esté y la pinte quien la pinte. El mapa de capas se
// lee en planta, así que en una pared cada texel ocupa metros de alto y el borde entre hierba y roca
// colgaría en dientes; aquí el borde sale de la inclinación real, con trazo de pincel. La textura de
// la pared se lee en su propia cara (proyección en X y en Z), no en planta, para que no se estire.
TEXTURE2D(_TexturaDePared);
SAMPLER(sampler_TexturaDePared);
float _ParedActiva;
float _InclinacionDePared;
float _TamanoDePared;
half4 _ColorDePared;

float AzarDelSuelo(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float RuidoDelSuelo(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(AzarDelSuelo(i), AzarDelSuelo(i + float2(1, 0)), f.x),
                lerp(AzarDelSuelo(i + float2(0, 1)), AzarDelSuelo(i + 1.0), f.x), f.y);
}

// Normal suave del terreno en este punto, la misma que usa la luz (InitializeInputData): sin ella
// la normal sale de derivadas de pantalla, que van por bloques de 2x2 píxeles y dibujan los
// triángulos del terreno.
half3 NormalDelTerreno(Varyings IN)
{
#if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
    return normalize(IN.normal.xyz);
#elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
    float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
    return TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
#else
    return normalize(IN.normal);
#endif
}

half CuantoDePared(half3 normal, float3 p)
{
    if (_ParedActiva < 0.5) return 0.0h;
    float umbral = cos(radians(_InclinacionDePared));
    float ruido = RuidoDelSuelo(float2(p.x + p.z, p.y) * 0.35) * 0.65 + RuidoDelSuelo(float2(p.x - p.z, p.y) * 1.3 + 7.1) * 0.35;
    float borde = umbral + (ruido - 0.5) * 0.18;
    return (half)(1.0 - smoothstep(borde - 0.015, borde + 0.015, abs(normal.y)));
}

half3 ColorDePared(half3 normal, float3 p, half cuantaTextura)
{
    float2 pesos = pow(abs(normal.xz) + 1e-3, 4.0);
    pesos /= pesos.x + pesos.y;
    float tamano = max(_TamanoDePared, 0.01);
    float2 uvX = p.zy / tamano, uvZ = p.xy / tamano;
    half3 medio = SAMPLE_TEXTURE2D_LOD(_TexturaDePared, sampler_TexturaDePared, uvX, MIP_DEL_COLOR_MEDIO).rgb * pesos.x
                + SAMPLE_TEXTURE2D_LOD(_TexturaDePared, sampler_TexturaDePared, uvZ, MIP_DEL_COLOR_MEDIO).rgb * pesos.y;
    half3 textura = SAMPLE_TEXTURE2D(_TexturaDePared, sampler_TexturaDePared, uvX).rgb * pesos.x
                  + SAMPLE_TEXTURE2D(_TexturaDePared, sampler_TexturaDePared, uvZ).rgb * pesos.y;
    return lerp(medio, textura, cuantaTextura) * _ColorDePared.rgb;
}

#ifndef TERRAIN_SPLAT_BASEPASS
// Capas con borde de pincel: la capa que más pesa se queda el sitio, con una franja estrecha de
// transición. El terreno con más de 4 capas se pinta en varias pasadas (4 capas cada una) que se
// suman: «otra» es el peso afilado de todas las capas de las demás pasadas, así el afilado cuenta
// todas y las pasadas siempre suman 1. Devuelve los pesos dentro de la pasada (suman 1) y en
// «cuanto» lo que se queda la pasada.
// En float: con media precisión las potencias altas de pesos pequeños se quedan en cero.
float4 Afilar(float4 x) { x = max(x, 0.0); x = x * x; x = x * x; x = x * x; return x * x; }

half4 PesosDePincel(half4 pesos, float otra, out half cuanto)
{
    float4 p = Afilar(pesos);
    float mia = dot(p, 1.0);
    cuanto = (half)saturate(mia / max(mia + otra, 1e-20));
    return (half4)(p / max(mia, 1e-20));
}

float AfiladoDelMapa(TEXTURE2D_PARAM(mapa, muestreo), float2 uv)
{
    return dot(Afilar(SAMPLE_TEXTURE2D(mapa, muestreo, uv)), 1.0);
}

// Peso afilado de las capas de las demás pasadas en este punto: todas las capas menos las de esta
// pasada (que es uno de los mapas, leído en el mismo punto). Sin los mapas puestos en el material
// se toma lo que falta para 1 como una sola capa.
float AfiladoDeLasOtrasPasadas(float2 uv, half4 pesos)
{
    float mia = dot(Afilar(pesos), 1.0);
    float total = AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas0, sampler_Control), uv);
    if (_MapasDeCapas > 1.5) total += AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas1, sampler_Control), uv);
    if (_MapasDeCapas > 2.5) total += AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas2, sampler_Control), uv);
    if (_MapasDeCapas > 3.5) total += AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas3, sampler_Control), uv);
    if (_MapasDeCapas > 4.5) total += AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas4, sampler_Control), uv);
    if (_MapasDeCapas > 5.5) total += AfiladoDelMapa(TEXTURE2D_ARGS(_ControlDeCapas5, sampler_Control), uv);
    if (_MapasDeCapas < 1.5 || total < mia * 0.5)
        return Afilar(float4(saturate(1.0 - dot(pesos, 1.0)), 0.0, 0.0, 0.0)).x;
    return max(total - mia, 0.0);
}

// 1 en lo llano y en las laderas normales; baja hasta 0 solo en las paredes (más de unos 65°).
float CuantoDeLlano(half3 normal)
{
    return saturate(abs(normal.y) * 1.6 - 0.35);
}

// Desplazamiento del punto donde se lee el mapa de capas (en texeles), para que las fronteras
// entre hierba, tierra y piedra salgan con borde de pincel y no en línea de cuadrícula. En las
// paredes mengua: allí un texel del mapa ocupa muchos metros de alto y el borde saldría en dientes
// largos que cuelgan. En lo llano es el mismo de siempre.
float2 DesplazamientoDePincel(float2 xz, float llano)
{
    float2 n = float2(RuidoDelSuelo(xz * 0.3) * 0.65 + RuidoDelSuelo(xz * 1.1 + 5.3) * 0.35,
                      RuidoDelSuelo(xz * 0.3 + 19.1) * 0.65 + RuidoDelSuelo(xz * 1.1 + 31.7) * 0.35);
    return (n - 0.5) * 3.0 * llano;
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
    splatUV += DesplazamientoDePincel(IN.positionWS.xz, CuantoDeLlano(NormalDelTerreno(IN))) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);
    half4 pesosDelMapa = splatControl;
    half alpha;

    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;
    // Deja splatControl normalizado: sirve también para el color medio.
    SplatmapMix(IN.uvMainAndLM, IN.uvSplat01, IN.uvSplat23, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS);

    // En las paredes la textura se estira (se lee en planta): ahí se queda solo el color de la capa.
    half cuantaTextura = (_DibujoActivo > 0.5h ? _DibujoTexturaDelSuelo : 0.35h) * (half)saturate(CuantoDeLlano(NormalDelTerreno(IN)) * 1.5);
    half4 pesosDePincel = PesosDePincel(pesosDelMapa, AfiladoDeLasOtrasPasadas(splatUV, pesosDelMapa), alpha);
    #ifdef TERRAIN_SPLAT_ADDPASS
    clip(alpha <= 0.005h ? -1.0h : 1.0h);
    #endif
    half3 albedo = lerp(ColorMedioDeLasCapas(IN.uvSplat01, IN.uvSplat23, pesosDePincel),
                        TexturaDeLasCapas(IN.uvSplat01, IN.uvSplat23, pesosDePincel), cuantaTextura);
#endif
    half pared = CuantoDePared(NormalDelTerreno(IN), IN.positionWS);
#ifdef TERRAIN_SPLAT_ADDPASS
    alpha *= 1.0h - pared;
#else
    if (pared > 0.0h)
    {
        half texturaDePared = _DibujoActivo > 0.5h ? _DibujoTexturaDelSuelo : 0.35h;
        half alphaFinal = lerp(alpha, 1.0h, pared);
        albedo = (albedo * alpha * (1.0h - pared) + ColorDePared(NormalDelTerreno(IN), IN.positionWS, texturaDePared) * pared)
                 / max(alphaFinal, 1e-3h);
        alpha = alphaFinal;
    }
#endif
    albedo = NieveDeLasCumbres(albedo, IN.positionWS);

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
