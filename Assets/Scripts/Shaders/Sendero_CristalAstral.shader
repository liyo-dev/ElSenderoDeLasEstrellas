Shader "El Sendero/CristalAstral"
{
    Properties
    {
        [Header(Color Base y Apariencia)]
        _BaseColor ("Color Base del Cristal", Color) = (0.06, 0.08, 0.16, 0.92)
        _SpecularColor ("Brillo Especular", Color) = (0.75, 0.85, 1.0, 1.0)
        _Smoothness ("Suavidad Superficial", Range(0, 1)) = 0.92

        [Header(Constelaciones y Runas Internas)]
        [HDR] _RuneEmission ("Color Emisivo de Runas", Color) = (0.45, 0.75, 1.2, 1.0)
        _RuneScale ("Escala de Runas", Float) = 4.0
        _RunePulseSpeed ("Velocidad de Respiración", Float) = 1.2
        _RuneIntensity ("Intensidad de Runas", Range(0, 5)) = 1.8

        [Header(Disipacion en Bordes)]
        _EdgeFadePower ("Poder de Desvanecimiento en Borde", Range(0.5, 6)) = 2.5
        _EdgeGlowColor ("Brillo del Borde Estelar", Color) = (0.3, 0.6, 1.0, 1.0)
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
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            float4 _BaseColor;
            float4 _SpecularColor;
            float _Smoothness;
            float4 _RuneEmission;
            float _RuneScale;
            float _RunePulseSpeed;
            float _RuneIntensity;
            float _EdgeFadePower;
            float4 _EdgeGlowColor;

            // Función pseudo-aleatoria
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Campo estelar puntual limpio y brillante (estrellas bajo el cristal astral)
            float starField(float2 uv, float threshold, float twinkleSpeed)
            {
                float2 g = floor(uv);
                float2 f = frac(uv) - 0.5;
                float h = hash21(g);
                if (h < threshold) return 0.0;

                float2 offset = float2(hash21(g + 13.5), hash21(g + 47.2)) - 0.5;
                float dist = length(f - offset * 0.7);
                float star = smoothstep(0.08, 0.01, dist);

                float twinkle = sin(_Time.y * twinkleSpeed + h * 6.28) * 0.35 + 0.65;
                return star * twinkle;
            }

            // Velo suave de nebulosa cósmica (ondas etéreas suaves, sin aristas)
            float nebulaField(float2 p)
            {
                float n1 = sin(p.x * 0.25 + sin(p.y * 0.20 + _Time.y * 0.12));
                float n2 = cos(p.y * 0.30 + cos(p.x * 0.22 - _Time.y * 0.09));
                return (n1 + n2) * 0.25 + 0.5;
            }

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

                float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normalWS = normalize(IN.normalWS);

                // 1. Fresnell y borde suave del camino
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _EdgeFadePower);

                // 2. Manto de estrellas bajo el cristal astral
                float2 posXZ = IN.positionWS.xz * (_RuneScale * 0.35);
                float stars1 = starField(posXZ * 3.0, 0.45, 2.2);
                float stars2 = starField(posXZ * 1.6 + 23.4, 0.70, 1.4);
                float totalStars = stars1 * 0.6 + stars2 * 1.3;

                // 3. Velo de nebulosa suave
                float neb = nebulaField(IN.positionWS.xz * 0.12);
                float3 colNebula = lerp(float3(0.015, 0.030, 0.075), float3(0.045, 0.020, 0.085), neb);

                // 4. Emisión estelar viva
                float pulse = sin(_Time.y * _RunePulseSpeed + IN.positionWS.x * 0.1 + IN.positionWS.z * 0.1) * 0.2 + 0.8;
                float3 emission = _RuneEmission.rgb * (totalStars * _RuneIntensity * pulse);

                // 5. Brillo y reflejo especular suave
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _Smoothness * 64.0) * _Smoothness;

                // 6. Color final compuesto
                float3 col = _BaseColor.rgb + colNebula + spec * _SpecularColor.rgb + emission;
                col += _EdgeGlowColor.rgb * (fresnel * 0.45);

                float alpha = saturate(_BaseColor.a + fresnel * 0.25);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
