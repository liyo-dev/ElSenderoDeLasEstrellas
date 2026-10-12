Shader "El Sendero/Cielo"
{
    Properties
    {
        [HDR] _ColorCenit ("Color del cénit", Color) = (0.25,0.52,0.92,1)
        [HDR] _ColorHorizonte ("Color del horizonte", Color) = (0.72,0.86,0.98,1)
        _ColorSuelo ("Color del suelo", Color) = (0.60,0.68,0.75,1)
        _CaidaDelHorizonte ("Caída del horizonte", Float) = 0.65
        [HDR] _ColorDeBrumaDelHorizonte ("Bruma del horizonte", Color) = (0.8,0.88,1,1)
        _GrosorDeLaBruma ("Grosor de bruma", Float) = 0.08
        [HDR] _ColorDelResplandor ("Color del resplandor", Color) = (1,0.95,0.85,1)
        _TamanoDelResplandor ("Tamaño del resplandor", Float) = 0.45
        _FuerzaDelResplandor ("Fuerza del resplandor", Float) = 0.15
        _NubosidadBase ("Nubosidad de buen tiempo", Float) = 0.3
        [HDR] _ColorDeNubes ("Nubes iluminadas", Color) = (1,1,1,1)
        _ColorDeNubesEnSombra ("Nubes en sombra", Color) = (0.68,0.74,0.86,1)
        _EscalaDeNubes ("Escala de nubes", Float) = 1.8
        _SuavidadDeNubes ("Suavidad de nubes", Float) = 0.18
        _VelocidadDeNubes ("Velocidad de nubes", Float) = 0.025
        _Nubosidad ("Nubosidad del clima", Float) = 0
        [HDR] _ColorDeNubesDeClima ("Nubes de clima", Color) = (0.52,0.58,0.66,1)
        _ColorDeNubesDeClimaEnSombra ("Sombra de clima", Color) = (0.34,0.38,0.43,1)
        _AvanceDelFrente ("Avance del frente", Float) = 0
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    struct appdata { float4 position : POSITION; float3 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 position : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
    float4 _ColorCenit, _ColorHorizonte, _ColorSuelo, _ColorDeBrumaDelHorizonte;
    float4 _ColorDelResplandor, _ColorDeNubes, _ColorDeNubesEnSombra;
    float4 _ColorDeNubesDeClima, _ColorDeNubesDeClimaEnSombra;
    float _CaidaDelHorizonte, _GrosorDeLaBruma, _TamanoDelResplandor, _FuerzaDelResplandor;
    float _NubosidadBase, _EscalaDeNubes, _SuavidadDeNubes, _VelocidadDeNubes, _Nubosidad, _AvanceDelFrente;
    // Las globales sin configurar valen cero y mantienen invisibles ambos astros.
    float4 _CieloDireccionDelSol, _CieloDireccionDeLaLuna, _CieloColorDelSol, _CieloColorDeLaLuna;
    float _CieloTamanoDelSol, _CieloTamanoDeLaLuna;
    float4 _CieloViento, _CieloDireccionDelFrente;
    float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
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
    v2f vert(appdata v)
    {
        v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.position=UnityObjectToClipPos(v.position); o.dir=v.texcoord; return o;
    }
    float4 frag(v2f i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float3 d=normalize(i.dir), sol=seguro(_CieloDireccionDelSol.xyz), luna=seguro(_CieloDireccionDeLaLuna.xyz);
        float altura=saturate(d.y);
        float3 cielo=lerp(_ColorHorizonte.rgb,_ColorCenit.rgb,pow(altura,max(0.01,_CaidaDelHorizonte)));
        cielo=lerp(cielo,_ColorSuelo.rgb,saturate(-d.y*5));
        cielo=lerp(cielo,_ColorDeBrumaDelHorizonte.rgb,exp(-abs(d.y)/max(0.001,_GrosorDeLaBruma))*0.4);
        float angSol=acos(clamp(dot(d,sol),-1,1));
        float bajo=1-saturate(sol.y);
        float resplandor=exp(-angSol/max(0.01,_TamanoDelResplandor))*_FuerzaDelResplandor*(0.3+1.7*bajo)*saturate(_CieloColorDelSol.a);
        cielo+=_ColorDelResplandor.rgb*resplandor;
        float2 uv=d.xz/(max(d.y,0.0)+0.12)*max(0.01,_EscalaDeNubes)+_CieloViento.xy*_Time.y*_VelocidadDeNubes;
        float n=fbm(uv);
        float c=saturate(max(_NubosidadBase,_Nubosidad));
        float2 horizontal=d.xz*rsqrt(max(dot(d.xz,d.xz),0.000001));
        float local=saturate(c+_AvanceDelFrente*dot(horizontal,_CieloDireccionDelFrente.xy)*0.5);
        float densidad=smoothstep(1-local,1-local+max(0.001,_SuavidadDeNubes),n);
        // La cobertura total conserva relieve de color y no deja huecos en el cénit.
        densidad=lerp(densidad,1,smoothstep(0.9,1,local));
        densidad=lerp(densidad,c,exp(-altura*18));
        if(d.y<0) densidad=c;
        float3 luz=lerp(_ColorDeNubes.rgb,_ColorDeNubesDeClima.rgb,saturate(_Nubosidad));
        float3 sombra=lerp(_ColorDeNubesEnSombra.rgb,_ColorDeNubesDeClimaEnSombra.rgb,saturate(_Nubosidad));
        float3 nube=lerp(sombra,luz,smoothstep(0.15,0.85,n));
        nube+=_ColorDelResplandor.rgb*exp(-angSol*8)*densidad*(1-densidad)*2*saturate(_CieloColorDelSol.a);
        cielo=lerp(cielo,nube,densidad);
        float radioSol=max(0.0001,_CieloTamanoDelSol);
        float2 discoSol=coordenadasDelAstro(d,sol);
        float angulo=atan2(discoSol.y,discoSol.x)+_Time.y*0.008;
        float rayos=pow(abs(cos(angulo*4)),28)*exp(-angSol/(radioSol*4))*smoothstep(radioSol*0.5,radioSol*1.4,angSol);
        float nucleo=1-smoothstep(radioSol*0.8,radioSol*1.15,angSol);
        float corona=exp(-angSol/(radioSol*2.5));
        float transparencia=pow(1-densidad,3);
        cielo+=_CieloColorDelSol.rgb*saturate(_CieloColorDelSol.a)*((nucleo*3+rayos*0.7)*transparencia+corona*0.3*lerp(1,0.15,densidad));
        float angLuna=acos(clamp(dot(d,luna),-1,1));
        float radioLuna=max(0.0001,_CieloTamanoDeLaLuna);
        float2 disco=coordenadasDelAstro(d,luna)/radioLuna;
        float limbo=sqrt(saturate(1-dot(disco,disco)));
        float crater=1;
        if (angLuna < radioLuna*1.3) crater=lerp(0.65,1,fbm(disco*6+4));
        float borde=1-smoothstep(radioLuna*0.94,radioLuna*1.06,angLuna);
        float halo=exp(-angLuna/(radioLuna*2.7));
        cielo+=_CieloColorDeLaLuna.rgb*saturate(_CieloColorDeLaLuna.a)*(borde*crater*(0.6+0.65*limbo)*transparencia+halo*0.15*lerp(1,0.15,densidad));
        return float4(cielo,1);
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
