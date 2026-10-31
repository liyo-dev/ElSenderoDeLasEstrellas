// La superficie de una puerta del Sendero: un remolino de luz de su color. Con _Encendida a 0 se
// queda en un velo apagado (la prueba ya está hecha o aún no toca); PuertaDelSendero la sube y
// baja con un MaterialPropertyBlock. UV: (0,0) abajo a la izquierda, (1,1) arriba a la derecha.
Shader "Sendero/PuertaDeLuz"
{
    Properties
    {
        [HDR] _Color ("Color de la puerta", Color) = (2.2, 1.6, 0.5, 1)
        [HDR] _ColorApagada ("Color apagada", Color) = (0.05, 0.07, 0.12, 1)
        _Encendida ("Encendida (0-1)", Range(0, 1)) = 1
        _Remolino ("Fuerza del remolino", Range(0, 12)) = 5
        _Velocidad ("Velocidad", Range(0, 3)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _ColorApagada;
                half _Encendida, _Remolino, _Velocidad;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash21(float2 p) { p = frac(p * float2(234.34, 435.345)); p += dot(p, p + 34.23); return frac(p.x * p.y); }
            float ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), f.x), f.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 c = (IN.uv - float2(0.5, 0.45)) * float2(1.0, 1.25);
                float r = length(c);
                float ang = atan2(c.y, c.x);
                float t = _Time.y * _Velocidad;
                float giro = ang + r * _Remolino - t * 2.0;
                float hilos = ruido(float2(giro * 1.6, r * 6.0 - t)) * 0.7 + ruido(float2(giro * 3.3, r * 11.0 + t)) * 0.3;
                float centro = exp(-r * 3.0);
                float borde = smoothstep(0.62, 0.35, r);
                float luz = (hilos * 0.8 + centro * 1.2) * borde;
                half3 encendida = _Color.rgb * luz;
                half3 apagada = _ColorApagada.rgb * (0.4 + hilos * 0.6) * borde;
                half3 col = lerp(apagada, encendida, _Encendida);
                half a = saturate(borde * lerp(0.5, 0.95, _Encendida));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
