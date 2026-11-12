// Ventana con forma de nube de humo que enseña lo que ve otra cámara (una visión en una bola de
// cristal, un recuerdo, un sueño). La forma son varios círculos blandos que se mueven despacio;
// el borde se tiñe con _ColorDelBorde. _Aparicion (0-1) la abre y la cierra desde el centro.
Shader "Sendero/VentanaDeHumo"
{
    Properties
    {
        _MainTex ("Lo que se ve", 2D) = "black" {}
        [HDR] _ColorDelBorde ("Color del borde", Color) = (1.4, 0.8, 2.2, 1)
        _Tinte ("Tinte de la imagen", Color) = (1, 0.95, 1.08, 1)
        _Aparicion ("Aparición", Range(0, 1)) = 1
        _Ondulacion ("Ondulación del humo", Range(0, 0.2)) = 0.025
        _AnchoDelBorde ("Ancho del borde", Range(0.01, 0.4)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ColorDelBorde, _Tinte;
                half _Aparicion, _Ondulacion, _AnchoDelBorde;
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

            // Cuánto de dentro de la nube está el punto (1 dentro, 0 fuera, suave en el borde).
            half Nube(float2 p, float t)
            {
                // Círculos unidos con un mínimo suave: una sola nube con el borde ondulado, sin huecos.
                const int N = 7;
                float2 c[N] = { float2(0.0, 0.0), float2(-0.2, 0.02), float2(0.2, -0.01), float2(-0.1, 0.12),
                                float2(0.11, 0.11), float2(-0.1, -0.11), float2(0.1, -0.1) };
                float r[N] = { 0.24, 0.17, 0.17, 0.16, 0.16, 0.15, 0.15 };
                float d = 10;
                [unroll] for (int i = 0; i < N; i++)
                {
                    float2 m = c[i] + _Ondulacion * float2(sin(t * 0.7 + i * 1.7), cos(t * 0.6 + i * 2.3));
                    float di = length(p - m) - r[i] * _Aparicion;
                    float h = saturate(0.5 + 0.5 * (di - d) / 0.08);
                    d = lerp(di, d, h) - 0.08 * h * (1 - h);
                }
                return d;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float d = Nube(p, _Time.y);
                half dentro = 1 - smoothstep(-_AnchoDelBorde, 0.0, d);
                half borde = (1 - smoothstep(0.0, _AnchoDelBorde * 0.5, abs(d + _AnchoDelBorde * 0.35)));
                half4 img = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                half3 c = lerp(_ColorDelBorde.rgb * 0.6, img.rgb * _Tinte.rgb, dentro);
                c += _ColorDelBorde.rgb * borde * 0.6;
                half a = saturate(max(dentro, borde * 0.8)) * saturate(_Aparicion * 3);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
