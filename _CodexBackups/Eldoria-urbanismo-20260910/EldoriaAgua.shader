Shader "El Sendero/Eldoria/Agua de maqueta"
{
    Properties
    {
        _Fondo ("Altura del lecho", 2D) = "black" {}
        _Claro ("Agua somera", Color) = (0.12,0.52,0.47,1)
        _Profundo ("Agua profunda", Color) = (0.035,0.22,0.31,1)
        _Espuma ("Espuma de orilla", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "Agua"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Fondo); SAMPLER(sampler_Fondo);
            CBUFFER_START(UnityPerMaterial)
                half4 _Claro;
                half4 _Profundo;
                half _Espuma;
            CBUFFER_END
            struct Entrada { float4 positionOS : POSITION; };
            struct Salida { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Salida Vert(Entrada i)
            {
                Salida o;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 Frag(Salida i) : SV_Target
            {
                // Batimetría propia: independiente de la cámara y de la textura de profundidad global.
                float2 uv=saturate((i.positionWS.xz+650)/1300);
                float lecho=SAMPLE_TEXTURE2D(_Fondo,sampler_Fondo,uv).r*300-25;
                float fuera=max(abs(i.positionWS.x)-650,abs(i.positionWS.z)-650);
                lecho=lerp(lecho,-12,saturate(fuera/20));
                float profundidad=max(0,i.positionWS.y-lecho);
                float2 p=i.positionWS.xz;
                float ondas=sin(p.x*.19+p.y*.11+_Time.y*.55)*sin(p.y*.27-p.x*.07-_Time.y*.38);
                float detalle=sin(p.x*.53+p.y*.36+_Time.y*.7);
                half3 color=lerp(_Claro.rgb,_Profundo.rgb,saturate(profundidad/9));
                color*=1+ondas*.065+detalle*.018;
                half espuma=(1-smoothstep(.08,.65,profundidad))*_Espuma;
                color=lerp(color,half3(.72,.86,.77),espuma);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
