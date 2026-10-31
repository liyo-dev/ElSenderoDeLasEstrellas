// Cielo de luz: degradado sin estrellas ni sol entre suelo, horizonte y cénit. Es el fondo de
// los laberintos de espejos (la luz viene de todas partes y nadie tiene sombra). Los colores se
// pueden cambiar en juego para teñir el sitio según el momento.
Shader "Sendero/CieloDeLuz"
{
    Properties
    {
        [HDR] _ColorCenit ("Cénit", Color) = (0.96, 0.94, 0.89, 1)
        [HDR] _ColorHorizonte ("Horizonte", Color) = (1.0, 0.97, 0.91, 1)
        [HDR] _ColorSuelo ("Bajo el horizonte", Color) = (0.86, 0.83, 0.77, 1)
        _Suavidad ("Suavidad del horizonte", Range(0.01, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorCenit;
                half4 _ColorHorizonte;
                half4 _ColorSuelo;
                half _Suavidad;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float y = normalize(IN.dir).y;
                half3 arriba = lerp(_ColorHorizonte.rgb, _ColorCenit.rgb, smoothstep(0.0, 1.0, saturate(y / _Suavidad)));
                half3 abajo = lerp(_ColorHorizonte.rgb, _ColorSuelo.rgb, smoothstep(0.0, 1.0, saturate(-y / _Suavidad)));
                return half4(y >= 0 ? arriba : abajo, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
