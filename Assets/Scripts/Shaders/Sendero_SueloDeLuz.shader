// Suelo de luz del Sendero: azul de noche con estrellas dentro, hilo dorado en los bordes, un pulso
// de luz que recorre el camino despacio y constelaciones que se encienden bajo los pasos. Las
// pisadas las publica PisadasEnElSendero en _SenderoPisadas (xyz = posición, w = momento).
// UV del camino: x de borde a borde (0-1), y = metros a lo largo del camino.
Shader "Sendero/SueloDeLuz"
{
    Properties
    {
        [HDR] _ColorBase ("Color del suelo", Color) = (0.035, 0.07, 0.16, 1)
        [HDR] _ColorProfundo ("Color al fondo del cristal", Color) = (0.06, 0.03, 0.12, 1)
        [HDR] _ColorBorde ("Brillo del borde", Color) = (2.2, 1.6, 0.7, 1)
        _AnchoBorde ("Ancho del brillo del borde (0-0.5)", Range(0.0, 0.5)) = 0.06
        [HDR] _ColorEstrellas ("Estrellas del suelo", Color) = (0.6, 0.85, 1.4, 1)
        _EscalaEstrellas ("Estrellas por metro", Range(0.1, 4)) = 0.9
        [HDR] _ColorPisada ("Constelación que se enciende", Color) = (3.0, 2.2, 1.0, 1)
        _RadioPisada ("Radio de la pisada (m)", Range(0.2, 4)) = 1.6
        _VidaPisada ("Segundos que dura encendida", Range(0.5, 10)) = 3.5
        [HDR] _ColorPulso ("Pulso que recorre el camino", Color) = (0.5, 0.45, 0.25, 1)
        _VelocidadPulso ("Velocidad del pulso (m/s)", Range(0, 20)) = 4
        _SeparacionPulso ("Metros entre pulsos", Range(5, 200)) = 45
        _LuzDeEscena ("Cuánto le afecta la luz de la escena", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBase, _ColorProfundo, _ColorBorde, _ColorEstrellas, _ColorPisada, _ColorPulso;
                half _AnchoBorde, _EscalaEstrellas, _RadioPisada, _VidaPisada, _VelocidadPulso, _SeparacionPulso, _LuzDeEscena;
            CBUFFER_END

            #define PISADAS 16
            float4 _SenderoPisadas[PISADAS];

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.fog = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float hash21(float2 p) { p = frac(p * float2(234.34, 435.345)); p += dot(p, p + 34.23); return frac(p.x * p.y); }
            float2 punto(float2 celda) { return celda + 0.2 + 0.6 * float2(hash21(celda + 13.5), hash21(celda + 47.2)); }

            float segmento(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Estrellas (una por celda, no todas) y las líneas que las unen con la vecina de la derecha
            // y la de arriba: la «constelación» que se enciende al pisar.
            void constelacion(float2 p, out float estrella, out float linea)
            {
                float2 c = floor(p);
                estrella = 0; linea = 0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 cc = c + float2(i, j);
                    float h = hash21(cc);
                    if (h < 0.35) continue;
                    float2 a = punto(cc);
                    estrella = max(estrella, smoothstep(0.09, 0.0, length(p - a)) * (0.5 + 0.5 * h));
                    float2 der = cc + float2(1, 0), arr = cc + float2(0, 1);
                    if (hash21(der) >= 0.35 && hash21(cc + 91.0) > 0.45) linea = max(linea, smoothstep(0.025, 0.0, segmento(p, a, punto(der))));
                    if (hash21(arr) >= 0.35 && hash21(cc + 57.0) > 0.45) linea = max(linea, smoothstep(0.025, 0.0, segmento(p, a, punto(arr))));
                }
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float arriba = saturate(n.y);
                float2 p = IN.positionWS.xz * _EscalaEstrellas;

                float estrella, linea;
                constelacion(p, estrella, linea);

                float encendido = 0;
                for (int k = 0; k < PISADAS; k++)
                {
                    float4 ps = _SenderoPisadas[k];
                    float edad = _Time.y - ps.w;
                    if (ps.w <= 0 || edad > _VidaPisada || edad < 0) continue;
                    float dist = distance(IN.positionWS.xz, ps.xz);
                    float vida = 1.0 - edad / _VidaPisada;
                    encendido = max(encendido, smoothstep(_RadioPisada, 0.0, dist) * vida * vida);
                }

                float profundidad = 0.5 + 0.5 * sin(p.x * 0.7 + p.y * 0.4) * cos(p.y * 0.5 - p.x * 0.3);
                float3 col = lerp(_ColorBase.rgb, _ColorProfundo.rgb, profundidad * 0.6);
                float titileo = 0.75 + 0.25 * sin(_Time.y * 2.0 + p.x * 3.1 + p.y * 1.7);
                col += _ColorEstrellas.rgb * estrella * titileo * 0.6;
                col += _ColorPisada.rgb * encendido * (estrella * 2.0 + linea * 1.2 + 0.08);

                float borde = min(IN.uv.x, 1.0 - IN.uv.x);
                float brilloBorde = _AnchoBorde > 0 ? smoothstep(_AnchoBorde, 0.0, borde) : 0;
                col += _ColorBorde.rgb * brilloBorde * arriba;

                float fase = frac((IN.uv.y - _Time.y * _VelocidadPulso) / _SeparacionPulso);
                col += _ColorPulso.rgb * exp(-pow((fase - 0.5) * _SeparacionPulso / 2.5, 2.0)) * arriba;

                // Los costados del camino, más oscuros.
                col = lerp(col * 0.35, col, arriba);

                Light luz = GetMainLight();
                col += col * _LuzDeEscena * saturate(dot(n, luz.direction)) * luz.color;
                col = MixFog(col, IN.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Unlit/DEPTHNORMALSONLY"
    }
    FallBack Off
}
