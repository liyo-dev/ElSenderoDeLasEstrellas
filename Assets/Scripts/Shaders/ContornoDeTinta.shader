// Contorno de tinta: oscurece el color de la escena en siluetas y pliegues. Lo dibuja
// ContornoDeTintaFeature; los ajustes viven en el Volume ContornoDeTinta. Ver INC-865.
Shader "Hidden/El Sendero/Contorno de tinta"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ContornoDeTinta"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _Tinta;      // rgb: color por el que se multiplica; a: intensidad
            float4 _Umbrales;   // x: silueta (relativa), y: pliegue, z: inicio del desvanecido, w: fin
            float4 _Opciones;   // x: grosor en píxeles, y: peso de los pliegues

            bool EsCielo(float raw)
            {
            #if UNITY_REVERSED_Z
                return raw <= 1e-6;
            #else
                return raw >= 1.0 - 1e-6;
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);

                int2 tam = int2(_CameraDepthTexture_TexelSize.zw);
                int2 p = clamp(int2(uv * _CameraDepthTexture_TexelSize.zw), int2(0, 0), tam - 1);

                float rawC = LoadSceneDepth(p);
                if (EsCielo(rawC)) return color;

                float dC = LinearEyeDepth(rawC, _ZBufferParams);
                float desvanecido = 1.0 - saturate((dC - _Umbrales.z) / (_Umbrales.w - _Umbrales.z));
                if (desvanecido <= 0.0) return color;

                int g = max(1, (int)_Opciones.x);
                int2 pD = min(p + int2(g, 0), tam - 1);
                int2 pI = max(p - int2(g, 0), int2(0, 0));
                int2 pA = min(p + int2(0, g), tam - 1);
                int2 pB = max(p - int2(0, g), int2(0, 0));

                float rD = LoadSceneDepth(pD), rI = LoadSceneDepth(pI), rA = LoadSceneDepth(pA), rB = LoadSceneDepth(pB);
                // El cielo cuenta como "muy lejos" para que las siluetas contra él también se marquen.
                float dD = EsCielo(rD) ? 1e6 : LinearEyeDepth(rD, _ZBufferParams);
                float dI = EsCielo(rI) ? 1e6 : LinearEyeDepth(rI, _ZBufferParams);
                float dA = EsCielo(rA) ? 1e6 : LinearEyeDepth(rA, _ZBufferParams);
                float dB = EsCielo(rB) ? 1e6 : LinearEyeDepth(rB, _ZBufferParams);

                float3 nC = LoadSceneNormals(p);
                float3 posWS = ComputeWorldSpacePosition(uv, rawC, UNITY_MATRIX_I_VP);
                float3 v = normalize(GetCameraPositionWS() - posWS);
                float nv = saturate(dot(nC, v));

                // Silueta en el lado cercano: algún vecino está claramente más lejos que este píxel.
                // En superficies muy oblicuas (suelo a lo lejos) el umbral crece para no rayarlas.
                float umbral = _Umbrales.x * dC * (1.0 + 4.0 * (1.0 - nv));
                float salto = max(max(dD, dI), max(dA, dB)) - dC;
                float silueta = saturate((salto - umbral) / max(umbral, 1e-4));

                float3 nD = LoadSceneNormals(pD), nI = LoadSceneNormals(pI), nA = LoadSceneNormals(pA), nB = LoadSceneNormals(pB);
                float giro = 1.0 - min(min(dot(nC, nD), dot(nC, nI)), min(dot(nC, nA), dot(nC, nB)));
                float pliegue = saturate((giro - _Umbrales.y) / max(1.0 - _Umbrales.y, 1e-4)) * _Opciones.y;

                float borde = saturate(max(silueta, pliegue)) * desvanecido * _Tinta.a;
                color.rgb = lerp(color.rgb, color.rgb * _Tinta.rgb, borde);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
