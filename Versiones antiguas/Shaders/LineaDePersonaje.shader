Shader "El Sendero/Línea de personaje"
{
    // Línea de tinta de los personajes: una funda del modelo, un poco más grande y vuelta del revés,
    // que solo se ve por fuera de la silueta. Grosor constante en píxeles, color plano.
    Properties
    {
        _ColorDeLinea("Color de la línea", Color) = (0.16, 0.10, 0.08, 1)
        _GrosorDeLinea("Grosor (píxeles a 1080p)", Range(0, 6)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Linea"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _ColorDeLinea;
            float _GrosorDeLinea;
            CBUFFER_END

            struct Atributos { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Variables { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Variables Vertice(Atributos input)
            {
                Variables output = (Variables)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 cs = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float2x3)UNITY_MATRIX_VP, normalWS);
                // Grosor en píxeles de una pantalla de 1080 de alto, sea cual sea la resolución.
                float2 pixel = float2(_ScreenParams.y / _ScreenParams.x, 1.0) * (2.0 / 1080.0);
                cs.xy += normalize(normalCS + 1e-6) * pixel * _GrosorDeLinea * cs.w;
                output.positionCS = cs;
                return output;
            }

            half4 Fragmento(Variables input) : SV_Target
            {
                return half4(_ColorDeLinea.rgb, 1);
            }
            ENDHLSL
        }
    }
}
