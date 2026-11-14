// Espuma, salpicaduras y vapor de las cascadas: piezas sólidas de dibujo animado con dos tonos
// (luz fija respecto a la cámara, arriba a la izquierda) y borde. Acepta el color de vértice de las
// partículas (tinte y transparencia por recorte a mordiscos, sin fundidos).
Shader "Kits/Waterfalls/Foam"
{
    Properties
    {
        _ColorLit("Lado con luz", Color) = (1.0, 1.0, 0.98, 1)
        _ColorShade("Lado en sombra", Color) = (0.70, 0.86, 0.95, 1)
        _ColorRim("Borde", Color) = (0.55, 0.75, 0.90, 1)
        _ShadeBand("Corte de la sombra", Range(-1, 1)) = -0.05
        _RimBand("Corte del borde", Range(0, 1.01)) = 0.8
        _LightDirView("Luz respecto a la cámara", Vector) = (-0.55, 0.65, 0.5, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Foam"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLit, _ColorShade, _ColorRim;
                float _ShadeBand, _RimBand;
                float4 _LightDirView;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // La transparencia de la partícula se hace recortando a trama, no fundiendo.
                float2 pantalla = input.positionCS.xy;
                float trama = frac(dot(floor(pantalla), float2(0.5, 0.25)) + frac(pantalla.y * 0.5) * 0.5);
                clip(input.color.a - trama * 0.98 - 0.01);
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalize(_LightDirView.xyz)));
                half3 c = dot(n, l) < _ShadeBand ? _ColorShade.rgb : _ColorLit.rgb;
                if (1.0 - saturate(dot(n, v)) > _RimBand) c = _ColorRim.rgb;
                c *= input.color.rgb;
                c = MixFog(c, input.fog);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
