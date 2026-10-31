// Luz sin sombra para las piezas del Sendero que son solo brillo: el hilo de los bordes, los arcos
// de las puertas, el halo del altar. _Intensidad la mueven en juego los componentes (puerta que se
// enciende, altar que respira) con un MaterialPropertyBlock.
Shader "Sendero/LuzSimple"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.0, 1.5, 0.7, 1)
        _Intensidad ("Intensidad", Range(0, 4)) = 1
        _Pulso ("Pulso (0 = fijo)", Range(0, 1)) = 0.15
        _VelocidadPulso ("Velocidad del pulso", Range(0, 6)) = 1.2
        [Enum(UnityEngine.Rendering.BlendMode)] _Origen ("Mezcla origen", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Destino ("Mezcla destino", Float) = 1
        [Toggle] _ZEscribe ("Escribe profundidad", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend [_Origen] [_Destino]
        ZWrite [_ZEscribe]
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensidad, _Pulso, _VelocidadPulso;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fase : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(ws);
                OUT.uv = IN.uv;
                OUT.fase = ws.x * 0.13 + ws.z * 0.07;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half pulso = 1.0 - _Pulso + _Pulso * (0.5 + 0.5 * sin(_Time.y * _VelocidadPulso + IN.fase));
                half3 c = _Color.rgb * _Intensidad * pulso;
                return half4(c, saturate(_Color.a * _Intensidad));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
