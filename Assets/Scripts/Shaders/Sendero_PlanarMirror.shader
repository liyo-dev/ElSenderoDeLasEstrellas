// Espejo plano en tiempo real para la Prueba de Will (laberinto de espejos). El componente
// MirrorReflection.cs vuelca la reflexión de la cámara a _ReflectionTex cada frame; este shader
// simplemente proyecta esa textura en pantalla y la mezcla con un tinte frío (motivo visual del
// Sendero: espejos / niebla / luz sin sombra).
Shader "Sendero/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "white" {}
        _TintColor ("Tint", Color) = (0.55, 0.68, 0.78, 1)
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            float4 _TintColor;
            float _ReflectionStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;
                half4 refl = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv);
                half4 col = lerp(_TintColor, refl, _ReflectionStrength);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
