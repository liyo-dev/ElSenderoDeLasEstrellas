Shader "Sendero/AgujeroNegro"
{
    Properties
    {
        [HDR] _Violeta ("Violeta", Color) = (0.35,0.04,1,1)
        [HDR] _Magenta ("Magenta", Color) = (1,0.03,0.45,1)
        [HDR] _Naranja ("Naranja", Color) = (1,0.3,0.02,1)
        _Intensidad ("Intensidad HDR", Float) = 6
        _Fresnel ("Concentración del borde", Range(2,16)) = 8
        _Velocidad ("Velocidad del remolino", Float) = 0.7
        _Modo ("Modo: núcleo, disco, halo, partícula", Float) = 0
        _Opacidad ("Opacidad", Range(0,1)) = 1
        [HideInInspector] _SrcBlend ("Origen", Float) = 1
        [HideInInspector] _DstBlend ("Destino", Float) = 0
        [HideInInspector] _ZWrite ("Profundidad", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "AgujeroNegro"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Violeta, _Magenta, _Naranja;
                float _Intensidad, _Fresnel, _Velocidad, _Modo, _Opacidad;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
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
                o.color = v.color;
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
                if (_Modo > 1.5)
                {
                    float radial = saturate(1-length(i.uv*2-1));
                    float alpha = smoothstep(0,1,radial) * _Opacidad * i.color.a;
                    return half4(_Modo > 2.5 ? i.color.rgb * _Intensidad : float3(0,0,0), alpha);
                }
                float angle = i.uv.x * TWO_PI + t;
                float noise = Ruido(float2(cos(angle),sin(angle))*9 + i.uv.y*4);
                float wave = .5+.5*sin(angle*7+i.uv.y*18+noise*5);
                float3 color = lerp(_Violeta.rgb,_Magenta.rgb,wave);
                color = lerp(color,_Naranja.rgb,pow(wave,12)*noise);
                float edge;
                if (_Modo > .5)
                    edge = pow(saturate(1-abs(i.uv.y*2-1)),.6) * (.35+noise);
                else
                {
                    float fresnel = pow(1-saturate(abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)))),_Fresnel);
                    // El umbral deja el centro exactamente negro y opaco, incluso bajo bloom.
                    edge = smoothstep(.12,1,fresnel) * (.5+noise*.5);
                }
                return half4(color * edge * _Intensidad, _Modo > .5 ? edge*_Opacidad : 1);
            }
            ENDHLSL
        }
    }
}