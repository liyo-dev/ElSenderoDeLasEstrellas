Shader "El Sendero/CosmosProfundo360"
{
    Properties
    {
        [Header(Colores del Espacio Profundo)]
        [HDR] _ColorCenit ("Color Cénit Cósmico", Color) = (0.05, 0.03, 0.15, 1)
        [HDR] _ColorNadir ("Color Nadir (Abismo 360)", Color) = (0.01, 0.01, 0.04, 1)
        [HDR] _ColorNebulosa1 ("Nebulosa Amatista", Color) = (0.45, 0.12, 0.65, 1)
        [HDR] _ColorNebulosa2 ("Nebulosa Oro Astral", Color) = (0.95, 0.62, 0.18, 1)
        [HDR] _ColorNebulosa3 ("Nebulosa Cian Profundo", Color) = (0.08, 0.65, 0.85, 1)

        [Header(Dinamica de Nebulosas)]
        _NebulaIntensity ("Intensidad de Nebulosas", Range(0, 3)) = 1.35
        _NebulaScale ("Escala de Nebulosas", Float) = 1.8
        _NebulaSpeed ("Velocidad de Flujo Cósmico", Float) = 0.012

        [Header(Campo de Estrellas 3D)]
        _StarDensity ("Densidad de Estrellas", Float) = 80.0
        _StarBrightness ("Brillo de Estrellas", Range(0, 8)) = 3.5
        _StarTwinkleSpeed ("Velocidad de Centelleo", Float) = 2.0
        _BigStarDensity ("Densidad de Grandes Estrellas", Float) = 18.0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
        float4 position : POSITION;
        float3 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 position : SV_POSITION;
        float3 dir : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    float4 _ColorCenit, _ColorNadir;
    float4 _ColorNebulosa1, _ColorNebulosa2, _ColorNebulosa3;
    float _NebulaIntensity, _NebulaScale, _NebulaSpeed;
    float _StarDensity, _StarBrightness, _StarTwinkleSpeed, _BigStarDensity;

    // Generador de ruido hash 3D
    float hash31(float3 p)
    {
        p = frac(p * 0.1031);
        p += dot(p, p.yzx + 33.33);
        return frac((p.x + p.y) * p.z);
    }

    float3 hash33(float3 p)
    {
        p = frac(p * float3(0.1031, 0.1030, 0.0973));
        p += dot(p, p.yxz + 33.33);
        return frac((p.xxy + p.yxx) * p.zyx);
    }

    // Ruido suave 3D interpolado
    float noise3D(float3 p)
    {
        float3 i = floor(p);
        float3 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);

        float n000 = hash31(i);
        float n100 = hash31(i + float3(1, 0, 0));
        float n010 = hash31(i + float3(0, 1, 0));
        float n110 = hash31(i + float3(1, 1, 0));
        float n001 = hash31(i + float3(0, 0, 1));
        float n101 = hash31(i + float3(1, 0, 1));
        float n011 = hash31(i + float3(0, 1, 1));
        float n111 = hash31(i + float3(1, 1, 1));

        return lerp(
            lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
            f.z
        );
    }

    // Movimiento browniano fractal 3D para nubes de gas estelar
    float fbm3D(float3 p)
    {
        float v = 0.0;
        float a = 0.5;
        [unroll]
        for (int i = 0; i < 4; i++)
        {
            v += a * noise3D(p);
            p = p * 2.02 + float3(4.2, 1.7, 9.1);
            a *= 0.5;
        }
        return v;
    }

    // Campo de estrellas en rejilla celular esférica
    float starField(float3 dir, float density, float twinkle)
    {
        float3 p = dir * density;
        float3 id = floor(p);
        float3 f = frac(p) - 0.5;

        float3 rnd = hash33(id);
        float2 offset = (rnd.xy - 0.5) * 0.75;
        float dist = length(f.xy - offset);

        // Centelleo armónico según semilla individual
        float t = sin(_Time.y * _StarTwinkleSpeed + rnd.z * 6.2831) * 0.5 + 0.5;
        float bright = rnd.z * lerp(0.4, 1.0, t);

        // Punto de estrella suave
        float star = smoothstep(0.08, 0.005, dist) * bright;
        return star;
    }

    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.position = UnityObjectToClipPos(v.position);
        o.dir = v.texcoord;
        return o;
    }

    float4 frag(v2f i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float3 dir = normalize(i.dir);

        // 1. Gradiente base en 360° (espacio profundo infinito sin corte de horizonte)
        float elevation = dir.y * 0.5 + 0.5;
        float3 color = lerp(_ColorNadir.rgb, _ColorCenit.rgb, pow(elevation, 1.2));

        // 2. Nebulosas volumétricas fluidas en 3D
        float3 flowPos = dir * _NebulaScale + float3(0.0, _Time.y * _NebulaSpeed * 0.5, _Time.y * _NebulaSpeed);
        float nebNoise1 = fbm3D(flowPos);
        float nebNoise2 = fbm3D(flowPos * 1.7 + float3(12.3, 7.1, 4.5));

        float mask1 = smoothstep(0.35, 0.78, nebNoise1);
        float mask2 = smoothstep(0.42, 0.85, nebNoise2);
        float mask3 = smoothstep(0.50, 0.90, nebNoise1 * nebNoise2 * 2.0);

        float3 nebColor = float3(0, 0, 0);
        nebColor += _ColorNebulosa1.rgb * mask1 * 1.2;
        nebColor += _ColorNebulosa2.rgb * mask2 * 0.9;
        nebColor += _ColorNebulosa3.rgb * mask3 * 1.5;

        color += nebColor * _NebulaIntensity;

        // 3. Campo de estrellas finas (polvo estelar distante)
        float starsFine = starField(dir, _StarDensity, 1.0);
        float3 starColFine = lerp(float3(0.85, 0.92, 1.0), _ColorNebulosa2.rgb, mask2 * 0.5);
        color += starColFine * starsFine * _StarBrightness;

        // 4. Campo de estrellas destacadas (con corona y brillo HDR para bloom)
        float starsBig = starField(dir + float3(31.4, 15.9, 26.5), _BigStarDensity, 1.5);
        float3 starColBig = lerp(float3(1.0, 0.95, 0.88), float3(0.8, 0.9, 1.0), hash31(floor(dir * _BigStarDensity)));
        color += starColBig * starsBig * (_StarBrightness * 2.2);

        return float4(color, 1.0);
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" }
        Pass
        {
            ZWrite Off
            Cull Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDCG
        }
    }
    FallBack Off
}
