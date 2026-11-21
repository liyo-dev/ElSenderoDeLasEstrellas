Shader "Kits/Ink Outline"
{
    // Funda de la línea de tinta: la malla empujada hacia fuera en pantalla y vuelta del revés, de modo
    // que solo asoma por fuera de la silueta. Grosor constante en píxeles. La usa InkOutlineFeature.
    Properties
    {
        _InkColor("Color", Color) = (0.16, 0.10, 0.08, 1)
        _InkWidth("Grosor (píxeles a 1080p)", Range(0, 6)) = 2.0
        _InkFade("Afinado: x desde (m), y hasta (m), z fracción de grosor lejos", Vector) = (6, 30, 0.4, 0)
        _InkPush("Empuje hacia el fondo (m)", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "InkOutline"
            Cull Front
            ZWrite On
            ZTest LEqual
            // El bit 128 del stencil lo marca un cuerpo que no se pinta (invisible): dentro de su
            // silueta la funda no se dibuja y de él solo queda el contorno.
            Stencil { Ref 128 ReadMask 128 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _InkColor;
            float _InkWidth;
            float4 _InkFade;
            float _InkPush;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // La funda se empuja unos centímetros hacia el fondo: así queda tapada por el propio personaje
                // en los huecos (entre los mechones) y solo asoma por la silueta, contra lo que hay detrás.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += normalize(positionWS - GetCameraPositionWS()) * _InkPush;
                float4 positionCS = TransformWorldToHClip(positionWS);
                // Con tangente w = 2 la malla trae normales suavizadas para la línea (InkOutlineSmoothNormals):
                // la funda se infla continua y no se abre en las aristas de las mallas facetadas.
                float3 normalOS = input.tangentOS.w > 1.5 ? input.tangentOS.xyz : input.normalOS;
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float2 normalCS = mul((float2x3)UNITY_MATRIX_VP, normalWS);
                float2 pixel = float2(_ScreenParams.y / _ScreenParams.x, 1.0) * (2.0 / 1080.0);
                // Más fina a lo lejos: un personaje pequeño al fondo no lleva el trazo del de primer plano.
                float lejos = saturate((positionCS.w - _InkFade.x) / max(_InkFade.y - _InkFade.x, 0.001));
                float grosor = _InkWidth * lerp(1.0, _InkFade.z, lejos);
                positionCS.xy += normalize(normalCS + 1e-6) * pixel * grosor * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_InkColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
