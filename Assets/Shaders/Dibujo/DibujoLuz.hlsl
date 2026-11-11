#ifndef EL_SENDERO_DIBUJO_LUZ_INCLUDED
#define EL_SENDERO_DIBUJO_LUZ_INCLUDED

// Luz de dibujo animado: cada superficie tiene dos tonos planos (luz y sombra) con el borde duro;
// la sombra es el color iluminado multiplicado por un tinte, no un gris más oscuro.
// Los ajustes son globales (los pone AspectoDeDibujo): un solo sitio para todo el juego.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

half _DibujoActivo;
half4 _DibujoSombra;            // Tinte de la sombra (lineal).
half _DibujoUmbral;             // Dónde cae el borde entre luz y sombra (N·L).
half _DibujoSuavidad;           // Medio ancho del borde.
half _DibujoSaturacionSombra;
half _DibujoAmbiente;
half _DibujoTinteDeLuz;         // Cuánto tiñe el color de la luz (0 = solo su intensidad).
half _DibujoOclusion;           // Cuánto oscurece el SSAO.
half _DibujoBrilloUmbral;
half _DibujoBrilloFuerza;
half _DibujoLucesExtra;
half _DibujoTopeDeLuz;          // Luz máxima sobre una superficie: por encima, los colores se lavan.

struct AjustesDeDibujo
{
    half3 sombra;
    half umbral;
    half suavidad;
    half saturacionSombra;
    half ambiente;
    half tinteDeLuz;
    half oclusion;
    half brilloUmbral;
    half brilloFuerza;
    half lucesExtra;
    half topeDeLuz;
};

// Sin AspectoDeDibujo aplicado los globales valen 0: se usan los valores de fábrica.
AjustesDeDibujo LeerAjustes()
{
    AjustesDeDibujo a;
    if (_DibujoActivo > 0.5h)
    {
        a.sombra = _DibujoSombra.rgb;
        a.umbral = _DibujoUmbral;
        a.suavidad = max(_DibujoSuavidad, 0.001h);
        a.saturacionSombra = _DibujoSaturacionSombra;
        a.ambiente = _DibujoAmbiente;
        a.tinteDeLuz = _DibujoTinteDeLuz;
        a.oclusion = _DibujoOclusion;
        a.brilloUmbral = _DibujoBrilloUmbral;
        a.brilloFuerza = _DibujoBrilloFuerza;
        a.lucesExtra = _DibujoLucesExtra;
        a.topeDeLuz = _DibujoTopeDeLuz > 0.0h ? _DibujoTopeDeLuz : 1.0h;
    }
    else
    {
        a.sombra = half3(0.48h, 0.34h, 0.60h);
        a.umbral = 0.05h;
        a.suavidad = 0.02h;
        a.saturacionSombra = 1.15h;
        a.ambiente = 1.0h;
        a.tinteDeLuz = 0.35h;
        a.oclusion = 0.4h;
        a.brilloUmbral = 0.92h;
        a.brilloFuerza = 0.35h;
        a.lucesExtra = 1.0h;
        a.topeDeLuz = 1.0h;
    }
    return a;
}

half LuminanciaDeDibujo(half3 c) { return dot(c, half3(0.2126h, 0.7152h, 0.0722h)); }

// Una luz puntual o un foco: mancha plana de su color, con el borde del alcance algo suavizado.
half3 LuzExtraDeDibujo(Light luz, half3 normalWS)
{
    half cara = smoothstep(-0.02h, 0.06h, dot(normalWS, luz.direction));
    half alcance = smoothstep(0.0h, 0.02h, luz.distanceAttenuation);
    return luz.color * (lerp(0.35h, 1.0h, cara) * alcance * saturate(luz.shadowAttenuation * 10.0h));
}

half3 ColorDeDibujo(InputData inputData, half3 albedo, half brilloDelMaterial)
{
    AjustesDeDibujo a = LeerAjustes();
    half4 mascaraDeSombra = unity_ProbesOcclusion;
    Light principal = GetMainLight(inputData.shadowCoord, inputData.positionWS, mascaraDeSombra);

    half nl = dot(inputData.normalWS, principal.direction);
    half iluminado = smoothstep(a.umbral - a.suavidad, a.umbral + a.suavidad, nl);
    iluminado *= smoothstep(0.35h, 0.65h, principal.shadowAttenuation);

    half3 colorDeLuz = lerp(LuminanciaDeDibujo(principal.color).xxx, principal.color, a.tinteDeLuz) * principal.distanceAttenuation;
    // Término constante de la luz ambiente (armónicos esféricos sin la parte que depende de la normal):
    // el mismo tono en toda la pieza, sin degradados.
    half3 ambiente = max(half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 0.0h) * a.ambiente;

    #if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor oclusion = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half oscurecido = lerp(1.0h, oclusion.indirectAmbientOcclusion, a.oclusion);
    #else
    half oscurecido = 1.0h;
    #endif

    half3 tonoDeLuz = albedo;
    if (brilloDelMaterial > 0.0h)
    {
        half3 mitad = SafeNormalize(principal.direction + inputData.viewDirectionWS);
        half nh = dot(inputData.normalWS, mitad);
        half brillo = smoothstep(a.brilloUmbral, a.brilloUmbral + 0.02h, nh) * brilloDelMaterial * a.brilloFuerza;
        tonoDeLuz = lerp(albedo, min(albedo * 1.5h + 0.03h, 1.0h), brillo);
    }

    half3 tonoDeSombra = albedo * a.sombra;
    tonoDeSombra = max(lerp(LuminanciaDeDibujo(tonoDeSombra).xxx, tonoDeSombra, a.saturacionSombra), 0.0h);

    // De día el sol más el ambiente pasan de 1 y el tono iluminado se quema: con el tope, la parte
    // iluminada sale del color de la pieza. De noche o en interiores la luz queda por debajo y no cambia.
    half3 luzTotal = colorDeLuz + ambiente;
    luzTotal *= min(1.0h, a.topeDeLuz / max(LuminanciaDeDibujo(luzTotal), 0.001h));

    half3 color = lerp(tonoDeSombra, tonoDeLuz, iluminado) * luzTotal * oscurecido;

    #if defined(_ADDITIONAL_LIGHTS)
    half3 extra = half3(0.0h, 0.0h, 0.0h);
    uint cuantas = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light direccional = GetAdditionalLight(lightIndex, inputData.positionWS, mascaraDeSombra);
        extra += LuzExtraDeDibujo(direccional, inputData.normalWS);
    }
    #endif
    LIGHT_LOOP_BEGIN(cuantas)
        Light puntual = GetAdditionalLight(lightIndex, inputData.positionWS, mascaraDeSombra);
        extra += LuzExtraDeDibujo(puntual, inputData.normalWS);
    LIGHT_LOOP_END
    color += albedo * extra * a.lucesExtra;
    #endif

    return color;
}

#endif
