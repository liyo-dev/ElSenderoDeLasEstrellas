#ifndef EL_SENDERO_DIBUJO_PINCEL_INCLUDED
#define EL_SENDERO_DIBUJO_PINCEL_INCLUDED

// Pinceladas para las piezas de color liso (modelos propios sin textura): manchas alargadas de tres tonos
// muy cercanos, puestas en el mundo (no dependen de las UV), para que convivan con los packs de textura pintada.
// Cuánto se notan y su tamaño son globales (AspectoDeDibujo ▸ Pinceladas).
half _DibujoPincel;          // fuerza: cuánto aclara u oscurece cada mancha
half _DibujoPincelEscala;    // metros por mancha

float AzarDePincel(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float RuidoDePincel(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(AzarDePincel(i), AzarDePincel(i + float2(1, 0)), u.x),
                lerp(AzarDePincel(i + float2(0, 1)), AzarDePincel(i + float2(1, 1)), u.x), u.y);
}

// Multiplicador del color de la pieza en este punto (1 = sin cambio).
half Pincelada(float3 posicionWS, half3 normalWS)
{
    half fuerza = _DibujoActivo > 0.5h ? _DibujoPincel : 0.12h;
    float escala = _DibujoPincelEscala > 0.0 ? _DibujoPincelEscala : 0.6;
    half3 w = abs(normalWS); w /= max(w.x + w.y + w.z, 0.001h);
    float3 p = posicionWS / escala;
    // Cada cara toma el plano que más la mira; las manchas se estiran en horizontal, como trazos.
    float r = RuidoDePincel(p.zy * float2(0.35, 1.0)) * w.x
            + RuidoDePincel(p.xz * float2(0.35, 1.0)) * w.y
            + RuidoDePincel(p.xy * float2(0.35, 1.0)) * w.z;
    half banda = floor(saturate(r) * 2.999h) * 0.5h - 0.5h;    // tres tonos: -0,5, 0, +0,5
    return 1.0h + banda * fuerza;
}

#endif
