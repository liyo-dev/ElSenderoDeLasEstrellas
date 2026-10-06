Shader "Sendero/LuzProtectora"
{
    Properties
    {
        [HDR] _ColorCentro ("Centro", Color) = (1.6,1.75,2.2,1)
        [HDR] _ColorBorde ("Borde", Color) = (0.45,0.75,2.4,1)
        _Intensidad ("Intensidad HDR", Float) = 2.5
        _Velocidad ("Velocidad del remolino", Float) = 0.6
        _Opacidad ("Opacidad", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "LuzProtectora"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorCentro, _ColorBorde;
                float _Intensidad, _Velocidad, _Opacidad;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                float a = frac(sin(dot(i, float2(127.1,311.7)))*43758.5453);
                float b = frac(sin(dot(i+float2(1,0), float2(127.1,311.7)))*43758.5453);
                float c = frac(sin(dot(i+float2(0,1), float2(127.1,311.7)))*43758.5453);
                float d = frac(sin(dot(i+1, float2(127.1,311.7)))*43758.5453);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y * _Velocidad;
                float frente = saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))));
                float borde = pow(1 - frente, 2.5);
                float ruido = Ruido(i.uv * float2(14, 7) + float2(t, -t * .6));
                // Centro casi blanco y borde azul que respira: la luz que sale del Archimago.
                float3 color = lerp(_ColorCentro.rgb, _ColorBorde.rgb, borde) * (_Intensidad * (.75 + .5 * ruido));
                float alpha = saturate(.25 + frente * .55 + borde * .6) * _Opacidad;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
