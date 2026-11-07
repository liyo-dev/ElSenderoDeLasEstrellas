// Los «restos de cielos distintos» del primer tramo del Sendero (novela XIX): paneles curvos que
// flotan a los lados del camino. _Modo 0 = amanecer (degradado cálido con su sol), 1 = noche llena
// de lunas. Los bordes se funden con el cielo del Sendero, como un trozo de cielo cosido al otro.
Shader "Sendero/CieloCosido"
{
    Properties
    {
        [Enum(Amanecer,0,NocheDeLunas,1)] _Modo ("Cielo", Float) = 0
        _Opacidad ("Opacidad", Range(0, 1)) = 0.9
        _Fundido ("Fundido de los bordes", Range(0.01, 0.5)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Modo, _Opacidad, _Fundido;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash21(float2 p) { p = frac(p * float2(234.34, 435.345)); p += dot(p, p + 34.23); return frac(p.x * p.y); }

            float luna(float2 uv, float2 centro, float radio, float fase)
            {
                float d = length(uv - centro);
                float disco = smoothstep(radio, radio * 0.92, d);
                float sombra = smoothstep(radio * 0.95, radio * 0.85, length(uv - centro - float2(fase * radio, radio * 0.1)));
                return disco * (1.0 - sombra * step(0.01, abs(fase)));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float bordes = smoothstep(0, _Fundido, uv.x) * smoothstep(0, _Fundido, 1 - uv.x) * smoothstep(0, _Fundido, uv.y) * smoothstep(0, _Fundido, 1 - uv.y);
                half3 col;
                if (_Modo < 0.5)
                {
                    half3 abajo = half3(0.85, 0.48, 0.28), medio = half3(0.72, 0.30, 0.40), arriba = half3(0.18, 0.20, 0.45);
                    col = lerp(lerp(abajo, medio, smoothstep(0.0, 0.45, uv.y)), arriba, smoothstep(0.45, 1.0, uv.y));
                    float sol = length((uv - float2(0.42, 0.2)) * float2(1.6, 1.0));
                    col += half3(1.6, 1.2, 0.7) * (smoothstep(0.09, 0.07, sol) + exp(-sol * 7.0) * 0.6);
                    float nubes = sin(uv.x * 9.0 + sin(uv.y * 14.0) * 1.5) * 0.5 + 0.5;
                    col = lerp(col, half3(1.0, 0.75, 0.7), smoothstep(0.7, 1.0, nubes) * smoothstep(0.55, 0.3, abs(uv.y - 0.35)) * 0.35);
                }
                else
                {
                    col = lerp(half3(0.05, 0.08, 0.2), half3(0.01, 0.02, 0.07), uv.y);
                    float2 g = floor(uv * float2(60, 36));
                    float h = hash21(g);
                    col += step(0.93, h) * smoothstep(0.35, 0.0, length(frac(uv * float2(60, 36)) - 0.5)) * 1.2;
                    half3 lunaColor = half3(1.4, 1.35, 1.15);
                    col += lunaColor * luna(uv, float2(0.25, 0.68), 0.11, 0.0);
                    col += lunaColor * 0.9 * luna(uv, float2(0.62, 0.78), 0.06, 0.6);
                    col += lunaColor * 0.8 * luna(uv, float2(0.78, 0.45), 0.08, -0.5);
                    col += half3(1.1, 0.8, 1.2) * 0.7 * luna(uv, float2(0.45, 0.32), 0.045, 0.4);
                    col += half3(0.4, 0.45, 0.7) * exp(-length(uv - float2(0.25, 0.68)) * 6.0) * 0.4;
                }
                return half4(col, bordes * _Opacidad);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
