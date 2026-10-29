// Ventana de un portal: enseña lo que ve la cámara del otro lado (una RenderTexture) muestreada en
// coordenadas de pantalla, así que se ve el otro mundo «a través» del portal con su perspectiva y
// no como una foto pegada. Recorta un óvalo con borde luminoso, y _Apertura lo cierra (0) o lo abre
// (1). _Destello lo lava hacia el color del borde (una explosión al otro lado).
Shader "El Sendero/Ventana de portal"
{
    Properties
    {
        _BaseMap ("Vista del otro lado", 2D) = "black" {}
        [HDR] _Tinte ("Tinte de la vista", Color) = (1,1,1,1)
        [HDR] _ColorDelBorde ("Color del borde", Color) = (0.9,1.6,2.6,1)
        _GrosorDelBorde ("Grosor del borde", Range(0.005, 0.4)) = 0.12
        _Apertura ("Apertura", Range(0, 1)) = 1
        _Destello ("Destello", Range(0, 1)) = 0
        _Ondulacion ("Ondulación del borde", Range(0, 0.2)) = 0.035
        [Toggle] _EnPantalla ("Muestrear en coordenadas de pantalla", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "VentanaDePortal"
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tinte;
                float4 _ColorDelBorde;
                float _GrosorDelBorde;
                float _Apertura;
                float _Destello;
                float _Ondulacion;
                float _EnPantalla;
            CBUFFER_END

            struct Entrada { float4 posicion : POSITION; float2 uv : TEXCOORD0; };
            struct Salida { float4 posicion : SV_POSITION; float2 uv : TEXCOORD0; float4 pantalla : TEXCOORD1; };

            Salida vert(Entrada i)
            {
                Salida o;
                o.posicion = TransformObjectToHClip(i.posicion.xyz);
                o.uv = i.uv;
                o.pantalla = ComputeScreenPos(o.posicion);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                float2 centro = (i.uv - 0.5) * 2.0;
                float angulo = atan2(centro.y, centro.x + 1e-5);
                float onda = sin(angulo * 7.0 + _Time.y * 2.3) * _Ondulacion + sin(angulo * 3.0 - _Time.y * 1.7) * _Ondulacion * 0.6;
                float r = length(centro);
                float limite = _Apertura * (1.0 + onda * _Apertura);
                clip(limite - r);

                float2 uvVista = lerp(i.uv, i.pantalla.xy / max(i.pantalla.w, 1e-5), _EnPantalla);
                half4 vista = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvVista) * _Tinte;

                float grosor = max(_GrosorDelBorde * max(_Apertura, 0.25), 1e-3);
                float borde = smoothstep(limite - grosor, limite, r);
                half3 color = lerp(vista.rgb, _ColorDelBorde.rgb, borde);
                color = lerp(color, _ColorDelBorde.rgb * 1.6, saturate(_Destello));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
