Shader "El Sendero/Agua de dibujo"
{
    // Agua con el aspecto de dibujo animado: bandas planas de color según la hondura (orilla, medio,
    // hondo), espuma de borde duro donde toca la orilla con una línea de tinta, y destellos blancos
    // que se mueven por la superficie, más vivos al sol, y oleaje suave (la superficie sube y baja y
    // pasan lomos de ola más claros). Ritmo lento: antes parecía cámara rápida (INC-1160). La luz es la misma que la del resto del mundo (dos tonos con
    // sombra de color, ajustes en AspectoDeDibujo). Necesita la textura de profundidad de la cámara.
    Properties
    {
        [Header(Color segun la hondura)]
        _ColorOrilla("Orilla", Color) = (0.42, 0.80, 0.78, 1)
        _ColorMedio("Medio", Color) = (0.22, 0.60, 0.70, 1)
        _ColorHondo("Hondo", Color) = (0.12, 0.38, 0.56, 1)
        _Profundidad("Metros hasta el hondo", Float) = 4
        _CorteMedio("Dónde empieza el medio (0-1)", Range(0.05, 0.95)) = 0.25
        _CorteHondo("Dónde empieza el hondo (0-1)", Range(0.05, 0.95)) = 0.6
        _OpacidadEnLaOrilla("Opacidad en la orilla", Range(0, 1)) = 0.7

        [Header(Espuma)]
        _ColorEspuma("Color", Color) = (0.96, 0.95, 0.88, 1)
        _AnchoDeEspuma("Ancho (m)", Float) = 0.35
        _OndulacionDeEspuma("Ondulación", Range(0, 1)) = 0.5
        _VelocidadDeEspuma("Velocidad", Float) = 0.18
        _GrosorDeTinta("Línea de tinta tras la espuma (m)", Float) = 0.06
        _ColorTinta("Tinta", Color) = (0.20, 0.15, 0.22, 1)

        [Header(Destellos)]
        _ColorDestellos("Color", Color) = (1, 1, 1, 1)
        _EscalaDeDestellos("Tamaño de las celdas (m)", Float) = 3
        _GrosorDeDestellos("Grosor", Range(0.01, 0.3)) = 0.07
        _CantidadDeDestellos("Cantidad", Range(0, 1)) = 0.35
        _VelocidadDeDestellos("Velocidad", Float) = 0.25
        _MasDestellosAlSol("Más destellos con sol (0-1)", Range(0, 1)) = 0.6
        _Corriente("Corriente (dirección XZ, m/s)", Vector) = (0, 0, 0, 0)

        [Header(Oleaje)]
        _AlturaDeOla("Altura (m)", Float) = 0.05
        _LargoDeOla("Largo (m)", Float) = 7
        _VelocidadDeOla("Velocidad (m/s)", Float) = 0.45
        _DireccionDeOla("Dirección (XZ)", Vector) = (1, 0.35, 0, 0)
        _ColorLomo("Lomo de la ola", Color) = (1, 1, 1, 0.16)
        _AnchoDeLomo("Ancho del lomo (0-1)", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Shaders/Dibujo/DibujoLuz.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _ColorOrilla;
            half4 _ColorMedio;
            half4 _ColorHondo;
            float _Profundidad;
            half _CorteMedio;
            half _CorteHondo;
            half _OpacidadEnLaOrilla;
            half4 _ColorEspuma;
            float _AnchoDeEspuma;
            half _OndulacionDeEspuma;
            float _VelocidadDeEspuma;
            float _GrosorDeTinta;
            half4 _ColorTinta;
            half4 _ColorDestellos;
            float _EscalaDeDestellos;
            half _GrosorDeDestellos;
            half _CantidadDeDestellos;
            float _VelocidadDeDestellos;
            float4 _Corriente;
            half _MasDestellosAlSol;
            float _AlturaDeOla;
            float _LargoDeOla;
            float _VelocidadDeOla;
            float4 _DireccionDeOla;
            half4 _ColorLomo;
            half _AnchoDeLomo;
            CBUFFER_END

            // Fase de la ola en un punto del mundo (radianes): una ola larga y otra más corta cruzada.
            float FaseDeOla(float2 xz, float t, out float fase2)
            {
                float2 d = normalize(_DireccionDeOla.xy + float2(1e-4, 0));
                float k = 6.2831 / max(_LargoDeOla, 0.1);
                fase2 = dot(xz, float2(-d.y, d.x) * 0.6 + d * 0.8) * k * 1.7 - t * _VelocidadDeOla * k * 1.3;
                return dot(xz, d) * k - t * _VelocidadDeOla * k;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float profundidadOjo : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertice(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Oleaje: la superficie sube y baja despacio (se nota donde la malla tiene vértices).
                float3 mundo = TransformObjectToWorld(input.positionOS.xyz);
                float fase2;
                float fase = FaseDeOla(mundo.xz, _Time.y, fase2);
                mundo.y += _AlturaDeOla * (sin(fase) + 0.4 * sin(fase2));
                input.positionOS.xyz = TransformWorldToObject(mundo);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.profundidadOjo = -p.positionVS.z;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            float2 Azar2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Azar2(i).x, b = Azar2(i + float2(1, 0)).x, c = Azar2(i + float2(0, 1)).x, d = Azar2(i + float2(1, 1)).x;
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Distancia al borde entre celdas que se mueven: las líneas finas son los destellos.
            float BordeDeCeldas(float2 p, float t)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = 0.5 + 0.5 * sin(t + 6.2831 * Azar2(i + g));
                    float d = length(g + o - f);
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) { d2 = d; }
                }
                return d2 - d1;
            }

            // Hondura del fondo en metros de ojo; vale también con cámaras ortográficas (minimapa, maqueta).
            float ProfundidadDelFondo(float2 uvPantalla)
            {
                float bruto = SampleSceneDepth(uvPantalla);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                    bruto = 1.0 - bruto;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, bruto);
                }
                return LinearEyeDepth(bruto, _ZBufferParams);
            }

            half4 Fragmento(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y;
                float2 xz = input.positionWS.xz - _Corriente.xy * t;
                float2 uvPantalla = GetNormalizedScreenSpaceUV(input.positionCS);
                float hondura = max(ProfundidadDelFondo(uvPantalla) - input.profundidadOjo, 0.0);

                // Bandas de color planas.
                half k = saturate(hondura / max(_Profundidad, 0.01));
                half3 color = _ColorOrilla.rgb;
                color = lerp(color, _ColorMedio.rgb, step(_CorteMedio, k));
                color = lerp(color, _ColorHondo.rgb, step(_CorteHondo, k));
                half alfa = lerp(_OpacidadEnLaOrilla, 1.0h, step(_CorteMedio, k));

                // Lomos de ola: bandas claras y finas que pasan despacio (solo donde ya no es orilla).
                float fase2;
                float fase = FaseDeOla(input.positionWS.xz, t, fase2);
                half lomo = step(1.0h - _AnchoDeLomo, 0.5h + 0.5h * sin(fase) * (0.8h + 0.2h * sin(fase2))) * step(_CorteMedio, k);
                color = lerp(color, _ColorLomo.rgb, lomo * _ColorLomo.a);

                // Sol: con luz directa fuerte y sin sombra hay más destellos (en días nublados o de
                // noche, menos).
                float4 coordSombra = TransformWorldToShadowCoord(input.positionWS);
                Light sol = GetMainLight(coordSombra);
                half alSol = saturate(dot(sol.color, half3(0.3h, 0.59h, 0.11h)) - 0.35h) * sol.shadowAttenuation;
                half cantidad = saturate(_CantidadDeDestellos * (1.0h + _MasDestellosAlSol * (2.0h * alSol - 0.6h)));

                // Destellos: bordes de celdas, solo donde la mancha de ruido lo permite (no por todas partes).
                float2 pDestellos = xz / max(_EscalaDeDestellos, 0.05);
                float borde = BordeDeCeldas(pDestellos, t * _VelocidadDeDestellos);
                float mancha = Ruido(pDestellos * 0.37 + t * 0.03);
                half destello = (1.0h - step(_GrosorDeDestellos, borde)) * step(1.0h - cantidad, mancha) * step(_CorteMedio * 0.5h, k);
                color = lerp(color, _ColorDestellos.rgb, destello);

                // Espuma de la orilla con borde ondulado y la línea de tinta detrás.
                float ondulacion = lerp(1.0, 0.55 + 0.9 * Ruido(xz * 1.7 + t * _VelocidadDeEspuma), _OndulacionDeEspuma);
                float anchoEspuma = _AnchoDeEspuma * ondulacion;
                half espuma = 1.0h - step(anchoEspuma, hondura);
                half tinta = (1.0h - step(anchoEspuma + _GrosorDeTinta, hondura)) * (1.0h - espuma) * step(0.001, _GrosorDeTinta);
                color = lerp(color, _ColorTinta.rgb, tinta);
                color = lerp(color, _ColorEspuma.rgb, espuma);
                alfa = max(alfa, max(espuma, tinta));

                // La misma luz de dos tonos que el resto del mundo (normal de la superficie: plana).
                InputData datos = (InputData)0;
                datos.positionWS = input.positionWS;
                datos.normalWS = normalize(input.normalWS);
                datos.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                datos.normalizedScreenSpaceUV = uvPantalla;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                datos.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                datos.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                // La espuma y los destellos no se oscurecen con la sombra de dos tonos: son brillo.
                half3 iluminado = ColorDeDibujo(datos, color, 0.0h);
                color = lerp(iluminado, color * max(LuminanciaDeDibujo(iluminado) / max(LuminanciaDeDibujo(color), 0.001h), 0.85h), max(espuma, destello) * 0.5h);

                color = MixFog(color, input.fogFactor);
                return half4(color, alfa);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
