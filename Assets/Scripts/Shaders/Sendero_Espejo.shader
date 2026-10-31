// Espejo del Sendero. Dos modos en el mismo material:
//  - Vivo (_Vivo = 1, lo pone EspejoVivo): proyecta la RenderTexture de la cámara del espejo,
//    leída volteada en horizontal porque esa cámara no invierte la geometría. En modo ventana
//    (_Ventana = 1) la cámara mira otro sitio a través del espejo y la textura se lee sin voltear.
//  - Plata (por defecto, y siempre dentro de otro reflejo): refleja la sonda de reflejos con
//    proyección de caja; sin personajes, casi sin coste.
// Encima de los dos: tinte, polvo y arañazos finos, brillo de borde (fresnel), sombra en el
// marco y, para los muros altos, degradado a luz con la altura.
Shader "Sendero/Espejo"
{
    Properties
    {
        [HideInInspector] _ReflejoTex ("Reflejo (lo pone EspejoVivo)", 2D) = "black" {}
        [HideInInspector] _Vivo ("Vivo", Float) = 0
        [HideInInspector] _Ventana ("Ventana", Float) = 0
        _Tinte ("Tinte", Color) = (0.94, 0.91, 0.86, 1)
        _FuerzaReflejo ("Fuerza del reflejo", Range(0, 1.5)) = 0.95
        _Rugosidad ("Rugosidad de la plata", Range(0, 0.5)) = 0.04
        _Defectos ("Defectos (R polvo y arañazos, GB desvío)", 2D) = "black" {}
        _FuerzaDefectos ("Fuerza del polvo", Range(0, 1)) = 0.35
        _ColorPolvo ("Color del polvo", Color) = (0.82, 0.8, 0.76, 1)
        _Distorsion ("Desvío por defectos", Range(0, 0.01)) = 0.0015
        [HDR] _ColorBorde ("Brillo de borde", Color) = (1.0, 0.9, 0.7, 1)
        _FuerzaFresnel ("Fuerza del brillo de borde", Range(0, 2)) = 0.35
        _AnchoMarco ("Ancho de la sombra del marco (UV)", Range(0, 0.25)) = 0.04
        _OscuroMarco ("Oscuridad junto al marco", Range(0, 1)) = 0.65
        [HDR] _ColorLuz ("Color de la luz de lo alto", Color) = (1.0, 0.97, 0.91, 1)
        _AlturaLuzInicio ("Altura donde empieza la luz (m)", Float) = 1000
        _AlturaLuzFin ("Altura donde todo es luz (m)", Float) = 1001
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Defectos_ST;
            half4 _Tinte;
            half _FuerzaReflejo;
            half _Rugosidad;
            half _FuerzaDefectos;
            half4 _ColorPolvo;
            half _Distorsion;
            half4 _ColorBorde;
            half _FuerzaFresnel;
            half _AnchoMarco;
            half _OscuroMarco;
            half4 _ColorLuz;
            float _AlturaLuzInicio;
            float _AlturaLuzFin;
            float _Vivo;
            float _Ventana;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Espejo"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #define _SCREENSPACEREFLECTIONS_OFF 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ReflejoTex); SAMPLER(sampler_ReflejoTex);
            TEXTURE2D(_Defectos);   SAMPLER(sampler_Defectos);
            float _EspejoEnReflejo;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 defectos = SAMPLE_TEXTURE2D(_Defectos, sampler_Defectos, IN.uv * _Defectos_ST.xy + _Defectos_ST.zw);
                float2 desvio = (defectos.gb - 0.5) * _Distorsion;

                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half NdotV = saturate(dot(N, V));

                // _Vivo va de 0 (plata) a 1 (reflejo vivo) con un fundido; dentro de otro reflejo, siempre plata.
                half vivo = _EspejoEnReflejo > 0.5 ? 0.0h : saturate(_Vivo);
                half3 reflejo = 0;
                if (vivo < 1.0h)
                {
                    float3 R = reflect(-V, N);
                    R = normalize(R + float3(desvio.x, desvio.y, 0) * 4.0);
                    float2 nsuv = GetNormalizedScreenSpaceUV(IN.positionCS);
                    reflejo = GlossyEnvironmentReflection(R, IN.positionWS, _Rugosidad, 1.0h, nsuv);
                }
                if (vivo > 0.0h)
                {
                    float2 suv = IN.screenPos.xy / IN.screenPos.w;
                    float2 ruv = float2(lerp(1.0 - suv.x, suv.x, _Ventana), suv.y) + desvio;
                    half3 vivoRGB = SAMPLE_TEXTURE2D_LOD(_ReflejoTex, sampler_ReflejoTex, ruv, 0).rgb;
                    reflejo = lerp(reflejo, vivoRGB, vivo);
                }

                half3 color = reflejo * _Tinte.rgb * _FuerzaReflejo;
                color = lerp(color, _ColorPolvo.rgb * max(Luminance(color), 0.15h), saturate(defectos.r * _FuerzaDefectos));
                color += _ColorBorde.rgb * pow(1.0h - NdotV, 4.0h) * _FuerzaFresnel;

                float borde = min(min(IN.uv.x, 1.0 - IN.uv.x), min(IN.uv.y, 1.0 - IN.uv.y));
                color *= lerp(1.0h - _OscuroMarco, 1.0h, smoothstep(0.0, max(_AnchoMarco, 1e-4), borde));

                float h = saturate((IN.positionWS.y - _AlturaLuzInicio) / max(_AlturaLuzFin - _AlturaLuzInicio, 0.01));
                color = lerp(color, _ColorLuz.rgb, h * h * (3.0 - 2.0 * h));

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertProfundidad
            #pragma fragment fragProfundidad

            float4 vertProfundidad(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragProfundidad() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertNormales
            #pragma fragment fragNormales
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct AtributosN { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryingsN  { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VaryingsN vertNormales(AtributosN IN)
            {
                VaryingsN OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 fragNormales(VaryingsN IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    float2 r = saturate(oct * 0.5 + 0.5);
                    return half4(PackFloat2To888(r), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
