Shader "El Sendero/PortalCelestialVelo"
{
    Properties
    {
        [Header(Colores del Velo Portal)]
        [HDR] _CoreColor ("Color del Núcleo", Color) = (1.0, 0.9, 0.6, 1.0)
        [HDR] _OuterColor ("Color Exterior", Color) = (0.9, 0.5, 0.1, 1.0)
        [HDR] _RimColor ("Color del Halo Exterior", Color) = (1.5, 1.2, 0.8, 1.0)

        [Header(Animacion y Vórtice)]
        _SwirlSpeed ("Velocidad de Vórtice", Float) = 0.8
        _DistortStrength ("Fuerza de Distorsión", Float) = 0.35
        _WaveFrequency ("Frecuencia de Ondas", Float) = 5.0
        _RimPower ("Poder del Halo (Fresnel)", Range(0.5, 6)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend One One // Aditivo para resplandor estelar
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _CoreColor;
            float4 _OuterColor;
            float4 _RimColor;
            float _SwirlSpeed;
            float _DistortStrength;
            float _WaveFrequency;
            float _RimPower;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 center = IN.uv - 0.5;
                float dist = length(center) * 2.0; // 0 en centro, 1 en borde
                float angle = atan2(center.y, center.x);

                // Vórtice espiral en rotación
                float spiral = angle + dist * 3.0 - _Time.y * _SwirlSpeed;
                float waves = sin(dist * _WaveFrequency - _Time.y * 2.0 + spiral) * _DistortStrength;

                float coreMask = saturate(1.0 - (dist + waves));
                float outerMask = saturate(1.0 - abs(dist - 0.6) * 2.0);

                // Halo fresnel con la vista
                float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normalWS = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(abs(dot(normalWS, viewDirWS))), _RimPower);

                // Composición de color aditivo
                float3 col = _CoreColor.rgb * (coreMask * 1.5);
                col += _OuterColor.rgb * (outerMask * 1.2);
                col += _RimColor.rgb * (fresnel * 1.8);

                float alpha = saturate(coreMask + outerMask + fresnel);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
