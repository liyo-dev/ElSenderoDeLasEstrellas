// Energía de dibujo animado con volumen: sombreado de dibujo con una luz fija respecto a la cámara
// (arriba a la izquierda): brillo hacia la luz, cuerpo, lado en sombra y borde de silueta, todo en
// bandas duras. Un ruido hace hervir los cortes a saltos (como animación «a doses»).
// Opaco y con pases de profundidad y normales, así que recibe el contorno de pantalla como el resto
// de la escena. La erosión lo hace aparecer y desaparecer a mordiscos, sin fundidos suaves.
// _Pulse (0-1) lo pone el componente EnergyPulse: agranda el brillo y aviva el color.
Shader "Kits/StylizedVfx/Energy"
{
    Properties
    {
        [HDR] _CoreColor("Brillo", Color) = (2.2, 2.1, 1.8, 1)
        [HDR] _MidColor("Cuerpo", Color) = (1.5, 0.9, 0.18, 1)
        [HDR] _ShadowColor("Lado en sombra", Color) = (1.0, 0.45, 0.1, 1)
        [HDR] _RimColor("Borde", Color) = (0.35, 1.2, 1.7, 1)
        _CoreBand("Corte del brillo (hacia la luz)", Range(-1, 1)) = 0.45
        _ShadowBand("Corte de la sombra", Range(-1, 1)) = -0.15
        _RimBand("Corte del borde", Range(0, 1.01)) = 0.72
        _LightDirView("Luz respecto a la cámara", Vector) = (-0.55, 0.65, 0.5, 0)
        _NoiseScale("Escala del ruido", Float) = 3
        _NoiseSpeed("Velocidad del ruido", Float) = 0.9
        _NoiseAmount("Cuánto mueve las bandas el ruido", Range(0, 0.5)) = 0.14
        _Steps("Saltos por segundo (0 = continuo)", Float) = 8
        _Erosion("Erosión", Range(0, 1)) = 0
        _Pulse("Latido", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CoreColor;
            half4 _MidColor;
            half4 _ShadowColor;
            half4 _RimColor;
            float4 _LightDirView;
            float _CoreBand;
            float _ShadowBand;
            float _RimBand;
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

        float SteppedTime()
        {
            float t = _Time.y;
            return _Steps > 0.0 ? floor(t * _Steps) / _Steps : t;
        }

        // Ruido en espacio de objeto: el dibujo no resbala cuando la pieza se mueve o crece.
        float EnergyNoise(float3 positionOS)
        {
            float t = SteppedTime() * _NoiseSpeed;
            float3 p = positionOS * _NoiseScale;
            return Noise3(p + float3(0.0, t, t * 0.7)) * 0.65 + Noise3(p * 2.1 - float3(t, 0.0, 0.0)) * 0.35;
        }

        void Erode(float3 positionOS)
        {
            if (_Erosion > 0.0)
                clip(EnergyNoise(positionOS * 0.7) - _Erosion);
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
        ENDHLSL

        Pass
        {
            Name "Energy"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                Erode(input.positionOS);
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalize(_LightDirView.xyz)));
                float ruido = (EnergyNoise(input.positionOS) - 0.5) * 2.0 * _NoiseAmount;
                float luz = dot(n, l) + ruido;
                float borde = 1.0 - saturate(dot(n, v)) + ruido * 0.5;
                half3 c = _MidColor.rgb;
                if (luz < _ShadowBand) c = _ShadowColor.rgb;
                if (luz > _CoreBand - _Pulse * 0.15) c = _CoreColor.rgb;
                if (borde > _RimBand) c = _RimColor.rgb;
                c *= 1.0 + _Pulse * 0.35;
                return half4(c, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half Frag(Varyings input) : SV_Target
            {
                Erode(input.positionOS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 Frag(Varyings input) : SV_Target
            {
                Erode(input.positionOS);
                float3 n = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
