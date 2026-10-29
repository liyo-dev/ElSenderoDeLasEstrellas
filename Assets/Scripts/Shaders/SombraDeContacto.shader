// Disco difuminado que oscurece (multiplica) el suelo bajo un personaje. Lo usa SombraDeContacto.
Shader "El Sendero/Sombra de contacto"
{
    Properties
    {
        _Color ("Color de la sombra", Color) = (0.32, 0.34, 0.46, 1)
        _Opacidad ("Opacidad", Range(0, 1)) = 0.55
        _Suavidad ("Suavidad del borde", Range(0.05, 1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend DstColor Zero
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "SombraDeContacto"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Suavidad;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PorSombra)
                UNITY_DEFINE_INSTANCED_PROP(half, _Opacidad)
            UNITY_INSTANCING_BUFFER_END(PorSombra)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float niebla : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.niebla = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half opacidad = UNITY_ACCESS_INSTANCED_PROP(PorSombra, _Opacidad);
                half d = length(input.uv * 2.0 - 1.0);
                half a = (1.0 - smoothstep(1.0 - _Suavidad, 1.0, d)) * opacidad;
                half3 c = lerp(half3(1, 1, 1), _Color.rgb, a);
                // Con niebla la sombra se funde hacia blanco (no oscurece lo que la niebla ya tapa).
                c = lerp(half3(1, 1, 1), c, ComputeFogIntensity(input.niebla));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
