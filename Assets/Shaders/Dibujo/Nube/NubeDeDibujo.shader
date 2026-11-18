// Nube 3D de dibujo: la misma idea que las nubes del cielo (cuerpo claro, panza lila, corte duro).
// La malla es un racimo de cuadritos con normales de esfera: cada cuadrito se abre de cara a la cámara
// y recorta una mota redonda; la luz se calcula con la normal de la esfera, así el racimo se lee como
// una sola nube con volumen. _AlphaThreshold hace aparecer (1 → 0,5) y desaparecer la nube.
Shader "El Sendero/Nube de dibujo"
{
    Properties
    {
        _Color("Cuerpo", Color) = (1, 0.99, 0.96, 1)
        _ColorEnSombra("Panza", Color) = (0.66, 0.66, 0.86, 1)
        _CorteDeLuz("Corte de luz", Range(-1, 1)) = -0.45
        _TamanoDeMota("Tamaño de cada mota (m)", Float) = 2.2
        _Abultar("Abultar hacia fuera (m)", Float) = 0.4
        _AlphaThreshold("Umbral (1 = invisible, 0,5 = formada)", Range(0, 1)) = 0.5
        _LuzDelMundo("Cuánto le afecta la luz del momento", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _ColorEnSombra;
            float _CorteDeLuz, _TamanoDeMota, _Abultar, _AlphaThreshold, _LuzDelMundo;
        CBUFFER_END

        struct Entrada { float4 posicion : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Salida { float4 posicionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float semilla : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

        Salida Vert(Entrada e)
        {
            Salida s;
            UNITY_SETUP_INSTANCE_ID(e);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(s);
            float3 normalWS = TransformObjectToWorldNormal(e.normal);
            float escala = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
            float3 posWS = TransformObjectToWorld(e.posicion.xyz) + normalWS * _Abultar * escala;
            // De cara a la cámara: cada esquina se abre por los ejes de la vista.
            float3 derecha = UNITY_MATRIX_V[0].xyz, arriba = UNITY_MATRIX_V[1].xyz;
            float2 esquina = (e.uv - 0.5) * _TamanoDeMota * escala;
            posWS += derecha * esquina.x + arriba * esquina.y;
            s.posicionCS = TransformWorldToHClip(posWS);
            s.uv = e.uv;
            s.normalWS = normalWS;
            s.semilla = frac(sin(dot(floor(e.posicion.xyz * 3.1), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            return s;
        }

        // Mota redonda con el borde un poco ondulado; 1 en el centro, 0 en el borde.
        float Mota(float2 uv, float semilla)
        {
            float2 p = uv * 2 - 1;
            float a = atan2(p.y, p.x);
            float onda = 0.06 * sin(a * 5 + semilla * 6.283) + 0.04 * sin(a * 3 - semilla * 9.0);
            return saturate(1 - length(p) / (1 + onda));
        }
        ENDHLSL

        Pass
        {
            Name "NubeDeDibujo"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Salida s) : SV_Target
            {
                clip(Mota(s.uv, s.semilla) - _AlphaThreshold);
                Light sol = GetMainLight();
                float3 n = normalize(s.normalWS);
                // Como las del cielo: arriba el cuerpo claro y abajo la panza, con un poco de sol de lado.
                float nl = dot(n, normalize(sol.direction * 0.35 + float3(0, 1, 0)));
                float ancho = max(fwidth(nl) * 1.5, 0.01);
                half3 color = lerp(_ColorEnSombra.rgb, _Color.rgb, smoothstep(_CorteDeLuz, _CorteDeLuz + ancho, nl));
                // La luz del momento (día, atardecer, noche, lluvia) tiñe la nube sin apagarla del todo.
                half3 luz = saturate(sol.color * 0.75 + SampleSH(n) * 0.5);
                color *= lerp(half3(1, 1, 1), luz, _LuzDelMundo);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragProfundidad
            #pragma multi_compile_instancing
            half FragProfundidad(Salida s) : SV_Target { clip(Mota(s.uv, s.semilla) - _AlphaThreshold); return 0; }
            ENDHLSL
        }
    }
}
