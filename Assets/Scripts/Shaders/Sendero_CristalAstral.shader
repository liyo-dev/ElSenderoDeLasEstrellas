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

            // Red de constelaciones Voronoi
            float voronoiLines(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);

                float minDist1 = 8.0;
                float minDist2 = 8.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = float2(hash21(g + lattice), hash21(g + lattice + 15.7));
                        float2 p = lattice + offset - f;
                        float d = dot(p, p);

                        if (d < minDist1)
                        {
                            minDist2 = minDist1;
                            minDist1 = d;
                        }
                        else if (d < minDist2)
                        {
                            minDist2 = d;
                        }
                    }
                }

                // Las líneas son la frontera entre celdas de Voronoi
                float lineVal = abs(sqrt(minDist2) - sqrt(minDist1));
                return 1.0 - smoothstep(0.02, 0.12, lineVal);
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

                // 1. Fresnell y borde del camino
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _EdgeFadePower);

                // 2. Respiración de constelaciones y runas
                float pulse = sin(_Time.y * _RunePulseSpeed + IN.positionWS.x * 0.2 + IN.positionWS.z * 0.2) * 0.5 + 0.5;
                float runes = voronoiLines(IN.positionWS.xz * (_RuneScale * 0.1));
                float3 emission = _RuneEmission.rgb * (runes * _RuneIntensity * lerp(0.6, 1.4, pulse));

                // 3. Brillo y reflejo especular suave
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _Smoothness * 64.0) * _Smoothness;

                // 4. Color final compuesto
                float3 col = _BaseColor.rgb + spec * _SpecularColor.rgb + emission;
                col += _EdgeGlowColor.rgb * (fresnel * 0.5);

                float alpha = saturate(_BaseColor.a + fresnel * 0.3 + runes * 0.2);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
