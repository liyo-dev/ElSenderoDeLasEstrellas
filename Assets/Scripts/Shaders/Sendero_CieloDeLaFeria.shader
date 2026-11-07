// Cielo nocturno de la feria del Sendero (escena Parque): degradado violeta con el resplandor cálido
// de las luces de la feria en el horizonte, estrellas que titilan (más apagadas cerca del suelo, por la
// luz del parque), nubes bajas teñidas por la feria, fuegos artificiales lejanos y la luna que publica
// SolYLunaEnElCielo. Pensado para una franja fija de noche (sin clima).
Shader "Sendero/CieloDeLaFeria"
{
    Properties
    {
        [Header(Degradado)]
        [HDR] _ColorCenit ("Color del cénit", Color) = (0.018,0.014,0.07,1)
        [HDR] _ColorMedio ("Color de media altura", Color) = (0.08,0.035,0.17,1)
        [HDR] _ColorHorizonte ("Color del horizonte", Color) = (0.32,0.11,0.28,1)
        _ColorSuelo ("Color bajo el horizonte", Color) = (0.02,0.012,0.04,1)
        _CaidaDelDegradado ("Caída del degradado", Range(0.1,3)) = 0.55
        [Header(Resplandor de la feria)]
        [HDR] _ColorDelResplandor ("Color del resplandor", Color) = (1.1,0.45,0.3,1)
        _AlturaDelResplandor ("Altura del resplandor", Range(0.01,0.5)) = 0.1
        _FuerzaDelResplandor ("Fuerza del resplandor", Range(0,3)) = 0.9
        [Header(Estrellas)]
        _DensidadDeEstrellas ("Densidad", Range(20,200)) = 70
        _CantidadDeEstrellas ("Cantidad", Range(0,1)) = 0.18
        _BrilloDeEstrellas ("Brillo", Range(0,6)) = 2.2
        _VelocidadDelTitileo ("Velocidad del titileo", Range(0,10)) = 2.5
        _EstrellasSobreElHorizonte ("Altura donde aparecen", Range(0,0.6)) = 0.18
        [Header(Nubes bajas)]
        _CoberturaDeNubes ("Cobertura", Range(0,1)) = 0.32
        [HDR] _ColorDeNubes ("Nubes iluminadas por la feria", Color) = (0.42,0.16,0.32,1)
        _ColorDeNubesArriba ("Nubes en alto", Color) = (0.05,0.03,0.1,1)
        _EscalaDeNubes ("Escala", Range(0.2,5)) = 1.3
        _VelocidadDeNubes ("Velocidad", Range(0,0.2)) = 0.012
        [Header(Fuegos artificiales)]
        _FuerzaDeLosFuegos ("Fuerza", Range(0,4)) = 1.4
        _RitmoDeLosFuegos ("Segundos entre fuegos", Range(1,20)) = 5.5
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    struct appdata { float4 position : POSITION; float3 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 position : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
    float4 _ColorCenit, _ColorMedio, _ColorHorizonte, _ColorSuelo, _ColorDelResplandor, _ColorDeNubes, _ColorDeNubesArriba;
    float _CaidaDelDegradado, _AlturaDelResplandor, _FuerzaDelResplandor;
    float _DensidadDeEstrellas, _CantidadDeEstrellas, _BrilloDeEstrellas, _VelocidadDelTitileo, _EstrellasSobreElHorizonte;
    float _CoberturaDeNubes, _EscalaDeNubes, _VelocidadDeNubes, _FuerzaDeLosFuegos, _RitmoDeLosFuegos;
    // Globales de SolYLunaEnElCielo; sin publicar valen cero y la luna no se ve.
    float4 _CieloDireccionDeLaLuna, _CieloColorDeLaLuna;
    float _CieloTamanoDeLaLuna;

    float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
    float hash3(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
    float ruido(float2 p)
    {
        float2 q = floor(p), f = frac(p); f = f*f*(3-2*f);
        return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);
    }
    float fbm(float2 p)
    {
        float n = 0, peso = 0.5333333;
        [unroll] for (int j=0;j<4;j++) { n += ruido(p)*peso; p=p*2.03+float2(13.7,7.9); peso*=0.5; }
        return n;
    }
    float3 seguro(float3 v) { return v * rsqrt(max(dot(v,v),0.000001)); }
    float2 coordenadasDelAstro(float3 d, float3 eje)
    {
        float3 referencia = abs(eje.y)>0.95 ? float3(1,0,0) : float3(0,1,0);
        float3 derecha = seguro(cross(referencia,eje));
        return float2(dot(d,derecha),dot(d,cross(eje,derecha)));
    }

    // Una capa de estrellas: una como mucho por celda de una rejilla 3D sobre la dirección.
    float Estrellas(float3 d, float densidad, float cantidad, float tamano)
    {
        float3 p = d * densidad;
        float3 celda = floor(p);
        float h = hash3(celda);
        if (h > cantidad) return 0;
        float3 centro = float3(hash3(celda+1.31), hash3(celda+2.77), hash3(celda+5.13)) * 0.7 + 0.15;
        float distancia = length(frac(p) - centro);
        float punto = saturate(1 - distancia / tamano);
        float titileo = 0.55 + 0.45 * sin(_Time.y * _VelocidadDelTitileo * (0.4 + hash3(celda+7.7)) + h * 40);
        return punto * punto * titileo * (0.35 + 0.65 * hash3(celda+9.9));
    }

    // Un cohete lejano: estalla en un punto del cielo bajo, se abre en un anillo de chispas y se apaga.
    float3 Fuego(float3 d, float hueco)
    {
        float t = _Time.y / max(1, _RitmoDeLosFuegos) + hueco;
        float ronda = floor(t), fase = frac(t);
        float semilla = hash(float2(ronda, hueco * 17));
        float rumbo = semilla * 6.2831853, elevacion = 0.12 + 0.3 * hash(float2(hueco, ronda + 3.1));
        float3 centro = normalize(float3(cos(rumbo), elevacion, sin(rumbo)));
        float angulo = acos(clamp(dot(d, centro), -1, 1));
        if (angulo > 0.25) return 0;
        float abierto = 1 - pow(1 - saturate(fase * 2.2), 3);
        float radio = 0.11 * abierto * (0.7 + 0.6 * hash(float2(ronda, 9.3)));
        float2 c = coordenadasDelAstro(d, centro);
        float giro = dot(c, c) > 1e-12 ? atan2(c.y, c.x) : 0;
        float rayos = pow(abs(cos(giro * (7 + floor(semilla * 6)))), 18);
        float anillo = exp(-pow((angulo - radio) / 0.008, 2)) * rayos;
        float estela = smoothstep(radio, 0, angulo) * rayos * 0.25;
        float destello = exp(-angulo / 0.02) * saturate(1 - fase * 10) * 2;
        float apagado = pow(saturate(1 - fase * 1.4), 2);
        float3 tono = lerp(float3(1.4,0.35,0.8), float3(1.4,1.0,0.35), hash(float2(ronda, hueco)));
        tono = lerp(tono, float3(0.4,1.1,1.4), step(0.7, hash(float2(hueco, ronda + 8.8))));
        return tono * (anillo + estela + destello) * apagado;
    }

    v2f vert(appdata v)
    {
        v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.position=UnityObjectToClipPos(v.position); o.dir=v.texcoord; return o;
    }

    float4 frag(v2f i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float3 d = normalize(i.dir);
        float altura = saturate(d.y);

        float k = pow(altura, _CaidaDelDegradado);
        float3 cielo = k < 0.5 ? lerp(_ColorHorizonte.rgb, _ColorMedio.rgb, k * 2) : lerp(_ColorMedio.rgb, _ColorCenit.rgb, k * 2 - 1);
        cielo += _ColorDelResplandor.rgb * _FuerzaDelResplandor * exp(-altura / max(0.001, _AlturaDelResplandor));
        cielo = lerp(cielo, _ColorSuelo.rgb, saturate(-d.y * 6));

        float visibles = smoothstep(0.0, max(0.001, _EstrellasSobreElHorizonte), d.y);
        float estrellas = Estrellas(d, _DensidadDeEstrellas, _CantidadDeEstrellas, 0.09)
                        + 0.5 * Estrellas(d, _DensidadDeEstrellas * 2.3, _CantidadDeEstrellas * 1.6, 0.07);
        float3 luzDeEstrellas = float3(0.9, 0.92, 1.0) * estrellas * _BrilloDeEstrellas * visibles;

        float2 uv = d.xz / (max(d.y, 0) + 0.15) * _EscalaDeNubes + float2(_Time.y, _Time.y * 0.4) * _VelocidadDeNubes;
        float n = fbm(uv);
        float nubes = smoothstep(1 - _CoberturaDeNubes, 1 - _CoberturaDeNubes + 0.25, n) * (1 - smoothstep(0.05, 0.55, d.y));
        if (d.y < 0) nubes = 0;
        float3 colorDeNube = lerp(_ColorDeNubes.rgb, _ColorDeNubesArriba.rgb, saturate(d.y * 3)) * (0.75 + 0.5 * n);

        float3 fuegos = (Fuego(d, 0.0) + Fuego(d, 0.37) + Fuego(d, 0.71)) * _FuerzaDeLosFuegos * step(0, d.y);

        cielo += (luzDeEstrellas + fuegos) * (1 - nubes);
        cielo = lerp(cielo, colorDeNube, nubes * 0.85);
        cielo += fuegos * nubes * 0.25;

        float3 luna = seguro(_CieloDireccionDeLaLuna.xyz);
        float angLuna = acos(clamp(dot(d, luna), -1, 1));
        float radioLuna = max(0.0001, _CieloTamanoDeLaLuna);
        float2 disco = coordenadasDelAstro(d, luna) / radioLuna;
        float limbo = sqrt(saturate(1 - dot(disco, disco)));
        float crater = angLuna < radioLuna * 1.3 ? lerp(0.65, 1, fbm(disco * 6 + 4)) : 1;
        float borde = 1 - smoothstep(radioLuna * 0.94, radioLuna * 1.06, angLuna);
        float halo = exp(-angLuna / (radioLuna * 2.7));
        cielo += _CieloColorDeLaLuna.rgb * saturate(_CieloColorDeLaLuna.a) * (borde * crater * (0.6 + 0.65 * limbo) * (1 - nubes) + halo * 0.18);

        return float4(cielo, 1);
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
}
