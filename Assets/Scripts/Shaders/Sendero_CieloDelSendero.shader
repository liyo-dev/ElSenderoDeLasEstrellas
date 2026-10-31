// Cielo del Sendero: una esfera entera de estrellas (también bajo el camino, porque el Sendero
// está suspendido entre ellas). Tres capas de estrellas, nebulosas teal y violeta, una Vía Láctea,
// la estrella guía y alguna estrella fugaz lenta. El resplandor del horizonte (_Tinte) lo cambia
// TinteDelSendero según la puerta hacia la que se camina; las estrellas no se mueven nunca.
Shader "Sendero/CieloDelSendero"
{
    Properties
    {
        [HDR] _ColorFondo ("Fondo", Color) = (0.010, 0.018, 0.045, 1)
        [HDR] _ColorNebulosa ("Nebulosa teal", Color) = (0.05, 0.22, 0.30, 1)
        [HDR] _ColorNebulosa2 ("Nebulosa violeta", Color) = (0.09, 0.04, 0.16, 1)
        _FuerzaNebulosa ("Fuerza de la nebulosa", Range(0, 2)) = 0.5
        [HDR] _Tinte ("Resplandor del horizonte", Color) = (1.0, 0.78, 0.38, 1)
        _FuerzaTinte ("Fuerza del resplandor", Range(0, 3)) = 0.3
        _AnchoTinte ("Ancho del resplandor", Range(0.02, 1)) = 0.08
        _BrilloEstrellas ("Brillo de las estrellas", Range(0, 6)) = 2.2
        _Titileo ("Titileo", Range(0, 1)) = 0.35
        _DirViaLactea ("Normal del plano de la Vía Láctea", Vector) = (0.35, 0.55, -0.75, 0)
        _FuerzaViaLactea ("Fuerza de la Vía Láctea", Range(0, 3)) = 1.0
        _DirEstrellaGuia ("Dirección de la estrella guía", Vector) = (0.05, 0.42, 0.9, 0)
        [HDR] _ColorEstrellaGuia ("Estrella guía", Color) = (2.4, 2.1, 1.5, 1)
        _CadaFugaz ("Segundos entre estrellas fugaces", Range(2, 30)) = 9
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
                half4 _ColorFondo, _ColorNebulosa, _ColorNebulosa2, _Tinte, _ColorEstrellaGuia;
                half _FuerzaNebulosa, _FuerzaTinte, _AnchoTinte, _BrilloEstrellas, _Titileo, _FuerzaViaLactea, _CadaFugaz;
                float4 _DirViaLactea, _DirEstrellaGuia;
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

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ruido(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash31(i), b = hash31(i + float3(1, 0, 0)), c = hash31(i + float3(0, 1, 0)), d = hash31(i + float3(1, 1, 0));
                float e = hash31(i + float3(0, 0, 1)), g = hash31(i + float3(1, 0, 1)), h = hash31(i + float3(0, 1, 1)), k = hash31(i + float3(1, 1, 1));
                return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                for (int o = 0; o < 4; o++) { v += a * ruido(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            // Una capa de estrellas: un punto como mucho por celda 3D, solo si la celda «tiene» estrella.
            float3 capaDeEstrellas(float3 d, float escala, float cuantas, float radio, float t)
            {
                float3 p = d * escala;
                float3 celda = floor(p);
                float3 h = hash33(celda);
                if (h.x > cuantas) return 0;
                float3 punto = celda + 0.25 + 0.5 * hash33(celda + 17.7);
                float dist = length(p - punto);
                float luz = smoothstep(radio, 0.0, dist);
                luz *= luz;
                float titileo = 1.0 - _Titileo + _Titileo * (0.5 + 0.5 * sin(t * (1.5 + 3.0 * h.y) + h.z * 6.283));
                float3 color = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.92, 0.75), h.y);
                return color * luz * titileo * (0.4 + 0.6 * h.z);
            }

            float3 estrellaFugaz(float3 d, float t)
            {
                float ranura = floor(t / _CadaFugaz);
                float fase = frac(t / _CadaFugaz) * _CadaFugaz;
                if (fase > 1.4) return 0;
                float3 r = hash33(float3(ranura, 7.1, 3.3)) * 2.0 - 1.0;
                float3 a = normalize(float3(r.x, abs(r.y) * 0.6 + 0.15, r.z));
                float3 lado = normalize(cross(a, float3(0, 1, 0)) + float3(0, -0.35, 0));
                float avance = fase / 1.4;
                float3 cabeza = normalize(a + lado * (0.05 + 0.35 * avance));
                float3 cola = normalize(a + lado * (0.05 + 0.35 * max(avance - 0.25, 0.0)));
                float3 ab = cabeza - cola, ap = d - cola;
                float u = saturate(dot(ap, ab) / max(dot(ab, ab), 1e-5));
                float dist = length(ap - ab * u);
                float traza = smoothstep(0.0025, 0.0, dist) * u;
                float apagado = smoothstep(0.0, 0.15, avance) * (1.0 - smoothstep(0.7, 1.0, avance));
                return float3(1.0, 0.95, 0.85) * traza * apagado * 2.5;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float t = _Time.y;

                float3 nv = normalize(_DirViaLactea.xyz);
                float enBanda = exp(-pow(dot(d, nv) / 0.2, 2.0));
                float nube = fbm(d * 2.2 + 3.1);
                float nube2 = fbm(d * 3.7 + 11.4);

                float3 col = _ColorFondo.rgb;
                col += _ColorNebulosa.rgb * smoothstep(0.45, 0.85, nube) * _FuerzaNebulosa;
                col += _ColorNebulosa2.rgb * smoothstep(0.5, 0.9, nube2) * _FuerzaNebulosa * 0.8;
                col += float3(0.30, 0.34, 0.45) * enBanda * smoothstep(0.35, 0.8, fbm(d * 6.0)) * _FuerzaViaLactea * 0.35;

                float3 estrellas = capaDeEstrellas(d, 60.0, 0.10, 0.10, t);
                estrellas += capaDeEstrellas(d, 150.0, 0.22, 0.13, t * 1.3) * 0.75;
                estrellas += capaDeEstrellas(d, 380.0, 0.22 + 0.5 * enBanda * _FuerzaViaLactea, 0.2, t * 0.8) * 0.45;
                col += estrellas * _BrilloEstrellas;

                float3 g = normalize(_DirEstrellaGuia.xyz);
                float cg = dot(d, g);
                float nucleo = smoothstep(0.99997, 1.0, cg);
                float halo = pow(saturate(cg), 4000.0) * 0.5;
                float3 lx = normalize(cross(g, float3(0, 1, 0)));
                float3 ly = cross(lx, g);
                float2 q = float2(dot(d - g, lx), dot(d - g, ly));
                float rayos = (exp(-abs(q.x) * 900.0) * exp(-abs(q.y) * 45.0) + exp(-abs(q.y) * 900.0) * exp(-abs(q.x) * 45.0)) * step(0.0, cg);
                col += _ColorEstrellaGuia.rgb * (nucleo * 2.0 + halo + rayos * 0.35);

                // El horizonte se curva hacia arriba, como el borde de un cuenco: el resplandor sube
                // un poco más lejos del jugador y también se ve por debajo del camino.
                float banda = exp(-abs(d.y - 0.04) / _AnchoTinte);
                col += _Tinte.rgb * banda * _FuerzaTinte;

                col += estrellaFugaz(d, t);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
