#ifndef EL_SENDERO_DIBUJO_PROFUNDIDAD_INCLUDED
#define EL_SENDERO_DIBUJO_PROFUNDIDAD_INCLUDED

// Pases de sombra, profundidad y normales de «El Sendero/Dibujo». Recortan exactamente igual que el pase
// de color (alfa de la textura por el alfa del tinte, mismas UV): así el contorno de tinta y las sombras
// ven la misma silueta que se pinta. Ver INC-1346.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct AtributosDeProfundidad
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VariablesDeProfundidad
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// El mismo recorte que el pase de color.
void RecortarComoElColor(float2 uv, float4 positionCS)
{
    #if defined(_ALPHATEST_ON)
    half alfa = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _Tinte.a;
    clip(alfa - _Cutoff);
    #endif
    #if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(positionCS);
    #endif
}

VariablesDeProfundidad PrepararProfundidad(AtributosDeProfundidad input, float4 positionCS)
{
    VariablesDeProfundidad output = (VariablesDeProfundidad)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

// ── Profundidad y normales ──────────────────────────────────────────────────────────────────────

VariablesDeProfundidad VerticeDeProfundidad(AtributosDeProfundidad input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    return PrepararProfundidad(input, TransformObjectToHClip(input.positionOS.xyz));
}

half FragmentoDeProfundidad(VariablesDeProfundidad input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    RecortarComoElColor(input.uv, input.positionCS);
    return input.positionCS.z;
}

half4 FragmentoDeNormales(VariablesDeProfundidad input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    RecortarComoElColor(input.uv, input.positionCS);
    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
    #else
    return half4(normalWS, 0.0);
    #endif
}

// ── Sombra ──────────────────────────────────────────────────────────────────────────────────────

float3 _LightDirection;
float3 _LightPosition;

VariablesDeProfundidad VerticeDeSombra(AtributosDeProfundidad input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 direccionDeLaLuz = normalize(_LightPosition - positionWS);
    #else
    float3 direccionDeLaLuz = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direccionDeLaLuz));
    #if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return PrepararProfundidad(input, positionCS);
}

half4 FragmentoDeSombra(VariablesDeProfundidad input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    RecortarComoElColor(input.uv, input.positionCS);
    return 0;
}

#endif
