#ifndef EL_SENDERO_DIBUJO_FOLLAJE_INCLUDED
#define EL_SENDERO_DIBUJO_FOLLAJE_INCLUDED

// Follaje de dibujo (copas, matas, setos): en vez del dibujo fino de la textura del pack (bandas, escamas,
// las líneas del atlas), cada zona de follaje se pinta con su color medio, encima van golpes de hoja de un
// tono más claro con el borde duro (dos tonos, como una copa pintada a mano) y cada árbol lleva su propio
// matiz. Los golpes se ponen en el mundo (no dependen de las UV del pack). La máscara del material dice qué
// parte de la textura es follaje (la corteza o las setas de un atlas compartido no llevan golpes).
// Fuerza, tamaño y variación son globales (AspectoDeDibujo ▸ Follaje). Necesita DibujoPincel.hlsl.

TEXTURE2D(_DibujoHojas);        SAMPLER(sampler_DibujoHojas);
TEXTURE2D(_MascaraDeFollaje);   SAMPLER(sampler_MascaraDeFollaje);
half4 _DibujoFollaje;           // x: fuerza de los golpes · y: metros por tesela · z: variación por árbol

float AzarDeFollaje(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

// Gira el matiz de un color alrededor del eje de los grises (conserva su luminosidad aproximada).
half3 GirarMatiz(half3 c, half angulo)
{
    const half3 k = half3(0.57735h, 0.57735h, 0.57735h);
    half co = cos(angulo), si = sin(angulo);
    return c * co + cross(k, c) * si + k * dot(k, c) * (1.0h - co);
}

// Semilla de cada árbol: su posición (cada pieza se dibuja con su matriz). En un lote estático la matriz es
// la identidad: entonces vale un ruido suave del mundo, que cambia de un árbol a otro sin cortar una copa.
float SemillaDelArbol(float3 posicionWS, out float3 origen)
{
    origen = GetObjectToWorldMatrix()._m03_m13_m23;
    if (dot(origen, origen) > 0.01) return AzarDeFollaje(floor(origen * 2.0));
    origen = float3(0, 0, 0);
    return RuidoDePincel(posicionWS.xz / 14.0);
}

// Color de follaje para este punto. colorDeTextura: lo que pinta la textura tal cual; mip: nivel del que se
// toma el color medio de la zona (lo calcula la herramienta según el tamaño de la textura).
half3 ColorDeFollaje(float2 uv, half3 colorDeTextura, half mip, float3 posicionWS, half3 normalWS)
{
    half fuerza = _DibujoActivo > 0.5h ? _DibujoFollaje.x : 0.0h;     // sin ajustes no hay textura de golpes
    float tesela = _DibujoActivo > 0.5h && _DibujoFollaje.y > 0.0h ? _DibujoFollaje.y : 6.0;
    half variacion = _DibujoActivo > 0.5h ? _DibujoFollaje.z : 0.5h;

    half mascara = SAMPLE_TEXTURE2D(_MascaraDeFollaje, sampler_MascaraDeFollaje, uv).r;
    // Color medio de la zona en un mip fijo: igual de cerca que de lejos (con un sesgo de mip, de lejos se
    // mezclaría el atlas entero y una copa roja saldría parda).
    half3 zona = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, mip).rgb;
    // Junto a una costura de UV ese mip mezcla la isla vecina del atlas (rayas de otro color): donde el matiz
    // se aparta del de la propia zona vista más de cerca, manda la de cerca. Las bandas del pack solo cambian
    // la luz, no el matiz, así que siguen fuera.
    half3 cerca = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, max(mip - 4.0h, 0.0h)).rgb;
    half3 matizLejos = zona / max(zona.r + zona.g + zona.b, 0.02h);
    half3 matizCerca = cerca / max(cerca.r + cerca.g + cerca.b, 0.02h);
    zona = lerp(zona, cerca * (dot(zona, half3(1.0h, 1.0h, 1.0h)) / max(dot(cerca, half3(1.0h, 1.0h, 1.0h)), 0.02h)), smoothstep(0.04h, 0.1h, distance(matizLejos, matizCerca)));

    float3 origen;
    float semilla = SemillaDelArbol(posicionWS, origen);
    half v1 = semilla * 2.0h - 1.0h;
    half v2 = frac(semilla * 7.13h) * 2.0h - 1.0h;
    half3 base = GirarMatiz(zona, v1 * variacion * 0.3h) * (1.0h + v2 * variacion * 0.12h);
    base = max(base, 0.0h);

    // Golpes en tres planos, mezclados antes del corte: así no se ven dos dibujos superpuestos en las caras
    // inclinadas. La punta de cada golpe mira hacia arriba en las caras de lado.
    half3 w = pow(abs(normalWS), 4.0h); w /= max(w.x + w.y + w.z, 0.001h);
    float3 p = (posicionWS - origen * 0.37) / tesela;
    half r = SAMPLE_TEXTURE2D(_DibujoHojas, sampler_DibujoHojas, p.zy).r * w.x
           + SAMPLE_TEXTURE2D(_DibujoHojas, sampler_DibujoHojas, p.xz).r * w.y
           + SAMPLE_TEXTURE2D(_DibujoHojas, sampler_DibujoHojas, p.xy).r * w.z;
    half borde = max(fwidth(r), 0.01h);
    half golpe = smoothstep(0.5h - borde, 0.5h + borde, r);
    half3 claro = min(base * (1.0h + fuerza * 2.2h) + fuerza * half3(0.06h, 0.06h, 0.0h), 1.0h);
    half3 follaje = lerp(base, claro, golpe);

    return lerp(colorDeTextura, follaje, mascara);
}

#endif
