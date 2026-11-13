// Burbuja de energía de dibujo animado (escudos, columnas de luz): transparente por dentro y con el
// borde de silueta opaco, en bandas duras de opacidad; un brillo en media luna hacia la luz fija
// respecto a la cámara (arriba a la izquierda) le da volumen. No escribe profundidad, así que no
// lleva contorno de tinta ni tapa lo que hay dentro. Erosión y latido como «Kits/StylizedVfx/Energy».
Shader "Kits/StylizedVfx/EnergyShell"
{
    Properties
    {
        [HDR] _CoreColor("Brillo", Color) = (2.2, 2.1, 1.8, 1)
        [HDR] _MidColor("Cuerpo", Color) = (1.5, 0.9, 0.18, 1)
        [HDR] _RimColor("Borde", Color) = (0.35, 1.2, 1.7, 1)
        _InnerAlpha("Opacidad del centro", Range(0, 1)) = 0.12
        _MidAlpha("Opacidad del cuerpo", Range(0, 1)) = 0.35
        _MidBand("Corte del cuerpo", Range(0, 1)) = 0.45
        _RimBand("Corte del borde", Range(0, 1.01)) = 0.75
        _CoreBand("Corte del brillo (hacia la luz)", Range(-1, 1.01)) = 0.82
        _LightDirView("Luz respecto a la cámara", Vector) = (-0.55, 0.65, 0.5, 0)
        _NoiseScale("Escala del ruido", Float) = 3
        _NoiseSpeed("Velocidad del ruido", Float) = 0.9
        _NoiseAmount("Cuánto mueve las bandas el ruido", Range(0, 0.5)) = 0.08
        _Steps("Saltos por segundo (0 = continuo)", Float) = 8
        _Erosion("Erosión", Range(0, 1)) = 0
        _Pulse("Latido", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "EnergyShell"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _RimColor;
                float4 _LightDirView;
                float _InnerAlpha;
                float _MidAlpha;
                float _MidBand;
                float _RimBand;
                float _CoreBand;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseAmount;
                float _Steps;
                float _Erosion;
                float _Pulse;
            CBUFFER_END

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float EnergyNoise(float3 positionOS)
            {
                float t = _Time.y;
                t = (_Steps > 0.0 ? floor(t * _Steps) / _Steps : t) * _NoiseSpeed;
                float3 p = positionOS * _NoiseScale;
                return Noise3(p + float3(0.0, t, t * 0.7)) * 0.65 + Noise3(p * 2.1 - float3(t, 0.0, 0.0)) * 0.35;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float ruido = EnergyNoise(input.positionOS);
                if (_Erosion > 0.0) clip(ruido - _Erosion);
                ruido = (ruido - 0.5) * 2.0 * _NoiseAmount;

                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalize(_LightDirView.xyz)));
                float borde = 1.0 - saturate(dot(n, v)) + ruido;
                float luz = dot(n, l) + ruido;

                half4 c = half4(_MidColor.rgb, _InnerAlpha);
                if (borde > _MidBand) c.a = _MidAlpha;
                if (borde > _RimBand - _Pulse * 0.08) c = half4(_RimColor.rgb, 0.9);
                if (luz > _CoreBand && borde > _MidBand) c = half4(_CoreColor.rgb, 0.95);
                c.rgb *= 1.0 + _Pulse * 0.3;
                return c;
            }
            ENDHLSL
        }
    }
}
