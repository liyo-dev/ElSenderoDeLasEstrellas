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
            float4 _Opciones;   // x: grosor en píxeles, y: peso de los pliegues, z: escalón de capa (m), w: peso de las capas
            float4 _Trazo;      // x: suavidad del borde de la línea (1 = degradado ancho, 0,1 = trazo neto)

            bool EsCielo(float raw)
            {
            #if UNITY_REVERSED_Z
                return raw <= 1e-6;
            #else
                return raw >= 1.0 - 1e-6;
            #endif
            }

            // 1/profundidad lineal de un píxel (0 en el cielo, que está infinitamente lejos).
            float InversaDeProfundidad(int2 q, int2 tam)
            {
                float raw = LoadSceneDepth(clamp(q, int2(0, 0), tam - 1));
                return EsCielo(raw) ? 0.0 : 1.0 / LinearEyeDepth(raw, _ZBufferParams);
            }

            // Profundidad lineal de un píxel; el cielo cuenta como "muy lejos" para que las siluetas
            // contra él también se marquen.
            float Lejania(int2 q, int2 tam)
            {
                float raw = LoadSceneDepth(clamp(q, int2(0, 0), tam - 1));
                return EsCielo(raw) ? 1e6 : LinearEyeDepth(raw, _ZBufferParams);
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

                // Más gruesa de cerca, como el trazo de un dibujo: x1,5 a menos de 4 m, el grosor normal desde 14 m.
                int g = max(1, (int)round(_Opciones.x * lerp(1.5, 1.0, saturate((dC - 4.0) / 10.0))));
                int gd = max(1, (int)round(g * 0.7071));
                int2 pD = min(p + int2(g, 0), tam - 1);
                int2 pI = max(p - int2(g, 0), int2(0, 0));
                int2 pA = min(p + int2(0, g), tam - 1);
                int2 pB = max(p - int2(0, g), int2(0, 0));

                float dD = Lejania(pD, tam), dI = Lejania(pI, tam), dA = Lejania(pA, tam), dB = Lejania(pB, tam);
                // Las diagonales igualan el grosor de la línea en curvas y esquinas (sin ellas sale en escalera).
                float dDA = Lejania(p + int2(gd, gd), tam), dDB = Lejania(p + int2(gd, -gd), tam);
                float dIA = Lejania(p + int2(-gd, gd), tam), dIB = Lejania(p + int2(-gd, -gd), tam);

                float3 nC = LoadSceneNormals(p);
                float3 posWS = ComputeWorldSpacePosition(uv, rawC, UNITY_MATRIX_I_VP);
                float3 v = normalize(GetCameraPositionWS() - posWS);
                float nv = saturate(dot(nC, v));

                // Silueta en el lado cercano: algún vecino está claramente más lejos que este píxel.
                // En superficies muy oblicuas (suelo a lo lejos) el umbral crece para no rayarlas.
                float umbral = _Umbrales.x * dC * (1.0 + 4.0 * (1.0 - nv));
                float salto = max(max(max(dD, dI), max(dA, dB)), max(max(dDA, dDB), max(dIA, dIB))) - dC;
                float silueta = saturate((salto - umbral) / max(umbral * _Trazo.x, 1e-4));

                // Pliegue: la superficie gira de golpe. Solo cuenta un vecino de la misma superficie (a
                // casi la misma distancia): en una silueta, el fondo tiene otra normal y marcaba una segunda
                // línea por fuera, que ensuciaba el borde. Se mira hacia un solo lado, para que el pliegue
                // tenga el grosor de la línea y no el doble.
                float3 nD = LoadSceneNormals(pD), nA = LoadSceneNormals(pA);
                float giro = max((1.0 - dot(nC, nD)) * step(abs(dD - dC), umbral),
                                 (1.0 - dot(nC, nA)) * step(abs(dA - dC), umbral));
                float pliegue = saturate((giro - _Umbrales.y) / max((1.0 - _Umbrales.y) * _Trazo.x, 1e-4)) * _Opciones.y;

                // Escalón de capa: una pieza unos milímetros por delante de otra (peto sobre blusa, falda,
                // hombreras). Es demasiado poco para la silueta y su canto mide menos de un píxel, así que el
                // pliegue aparece y desaparece al moverse. Se mide en 1/profundidad, que sobre una cara plana
                // varía en línea recta por la pantalla: la recta de cada lado se prolonga hasta el medio de los
                // dos píxeles y se mira el hueco entre ambas. En una superficie continua (también en la arista
                // entre dos facetas de una malla de pocos polígonos) las dos rectas se encuentran y el hueco es
                // casi 0; en un escalón entre dos piezas, el hueco es su altura. Solo en el lado cercano, de
                // cerca, en este píxel y en el siguiente (línea de dos píxeles).
                // La línea de capa solo existe a menos de 15 m (se va entre 8 y 15): más lejos no se lee
                // la profundidad de los 16 vecinos.
                float capa = 0.0;
                if (dC < 15.0 && _Opciones.w > 0.0)
                {
                    float escalon = 0.0;
                    [unroll] for (int eje = 0; eje < 4; eje++)
                    {
                        int2 dir = eje == 0 ? int2(1, 0) : eje == 1 ? int2(-1, 0) : eje == 2 ? int2(0, 1) : int2(0, -1);
                        float i0 = InversaDeProfundidad(p - dir, tam);
                        float i1 = 1.0 / dC;
                        float i2 = InversaDeProfundidad(p + dir, tam);
                        float i3 = InversaDeProfundidad(p + dir * 2, tam);
                        float i4 = InversaDeProfundidad(p + dir * 3, tam);
                        // Escalón entre este píxel y el siguiente, y entre el siguiente y el otro.
                        float hueco0 = (i1 + 0.5 * (i1 - i0)) - (i2 + 0.5 * (i2 - i3));
                        float hueco1 = (i2 + 0.5 * (i2 - i1)) - (i3 + 0.5 * (i3 - i4));
                        escalon = max(escalon, max(hueco0, hueco1 * step(1.5, _Opciones.x)));
                    }
                    escalon *= dC * dC;
                    float umbralDeCapa = max(_Opciones.z, 0.0006 * dC);
                    capa = saturate((escalon - umbralDeCapa) / umbralDeCapa) * _Opciones.w
                         * (1.0 - saturate((dC - 8.0) / 7.0));
                }

                float borde = saturate(max(max(silueta, pliegue), capa)) * desvanecido * _Tinta.a;
                color.rgb = lerp(color.rgb, color.rgb * _Tinta.rgb, borde);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
