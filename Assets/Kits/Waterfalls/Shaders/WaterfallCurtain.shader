// Cortina de una cascada con aspecto de dibujo animado: franjas claras y oscuras que bajan deprisa
// (cada columna a su ritmo), un labio de espuma arriba, espuma al pie y un borde con tinta a los
// lados. Lo que necesita de la malla lo pone el componente Waterfall: color R = cuánto cae (los
// tramos llanos no se ven), G = labio, B = pie; UV0 = (metros desde el centro, metros recorridos);
// UV1 = (ancho en tanto por uno, ancho en metros). Las franjas avanzan a saltos (dibujo «a doses»).
Shader "Kits/Waterfalls/Curtain"
{
    Properties
    {
        _ColorLight("Franjas claras", Color) = (0.78, 0.95, 1.0, 1)
        _ColorMid("Agua", Color) = (0.36, 0.72, 0.88, 1)
        _ColorDark("Franjas oscuras", Color) = (0.18, 0.46, 0.70, 1)
        _ColorFoam("Espuma", Color) = (1.0, 0.99, 0.95, 1)
        _ColorInk("Tinta del borde", Color) = (0.20, 0.15, 0.22, 1)
        _FallSpeed("Velocidad de caída (m/s)", Float) = 4.5
        _StreaksPerMeter("Franjas por metro de ancho", Float) = 1.3
        _StreakLength("Largo de las franjas (m)", Float) = 2.2
        _EdgeInk("Grosor de la tinta del borde (m)", Float) = 0.07
        _EdgeFoam("Espuma junto al borde (m)", Float) = 0.22
        _Steps("Saltos por segundo (0 = continuo)", Float) = 12
        _Opacity("Opacidad", Range(0, 1)) = 0.94
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Curtain"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLight, _ColorMid, _ColorDark, _ColorFoam, _ColorInk;
                float _FallSpeed, _StreaksPerMeter, _StreakLength, _EdgeInk, _EdgeFoam, _Steps, _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
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
                o.uv0 = input.uv0;
                o.uv1 = input.uv1;
                o.color = input.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i.x + i.y * 57.0), b = Hash(i.x + 1.0 + i.y * 57.0);
                float c = Hash(i.x + (i.y + 1.0) * 57.0), d = Hash(i.x + 1.0 + (i.y + 1.0) * 57.0);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half falling = input.color.r;
                clip(falling - 0.03h);
                float t = _Steps > 0.0 ? floor(_Time.y * _Steps) / _Steps : _Time.y;

                // Franjas: cada columna con su velocidad y su desfase; dentro de la columna, la franja
                // es más estrecha que la columna para que entre ellas se vea el agua.
                float columna = floor(input.uv0.x * _StreaksPerMeter);
                float azar = Hash(columna + 3.7);
                float dentro = frac(input.uv0.x * _StreaksPerMeter);
                float y = (input.uv0.y - t * _FallSpeed * (0.8 + 0.45 * azar)) / max(_StreakLength, 0.05) + azar * 13.0;
                float n = Noise(float2(columna * 1.7, y));
                float ancho = abs(dentro - 0.5);
                half3 c = _ColorMid.rgb;
                c = lerp(c, _ColorDark.rgb, step(n, 0.3) * step(ancho, 0.32));
                c = lerp(c, _ColorLight.rgb, step(0.62, n) * step(ancho, 0.22 + 0.12 * (n - 0.62)));

                // Espuma: labio, pie y junto a los bordes, con el borde de la espuma irregular.
                float metrosAlBorde = min(input.uv1.x, 1.0 - input.uv1.x) * input.uv1.y;
                float mella = Noise(float2(input.uv0.x * 2.3, input.uv0.y * 1.1 - t * _FallSpeed * 0.6)) - 0.5;
                half espuma = step(0.55, input.color.g + mella * 0.5) + step(0.5, input.color.b + mella * 0.6);
                espuma += step(metrosAlBorde, _EdgeFoam * (1.0 + mella));
                c = lerp(c, _ColorFoam.rgb, saturate(espuma));
                half tinta = step(metrosAlBorde, _EdgeInk);
                c = lerp(c, _ColorInk.rgb, tinta);

                half alfa = saturate(falling * 1.6) * max(_Opacity, saturate(espuma + tinta));
                c = MixFog(c, input.fog);
                return half4(c, alfa);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
