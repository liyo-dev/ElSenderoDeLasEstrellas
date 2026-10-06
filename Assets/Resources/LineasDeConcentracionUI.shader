Shader "UI/LineasDeConcentracion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Textura", 2D) = "white" {}
        _HuecoCentral ("Hueco central", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Salida { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float _HuecoCentral;
            Salida vert(Entrada v)
            {
                Salida o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                float2 direccion = (i.uv - 0.5) * 2;
                float radio = length(direccion);
                if (radio <= _HuecoCentral || _HuecoCentral >= 1) return 0;
                // Reubica las puntas de las cuñas sin estirar los píxeles del borde.
                float radioTextura = 0.35 + (radio - _HuecoCentral) * 0.65 / (1 - _HuecoCentral);
                float2 uv = 0.5 + direccion / max(radio, 0.0001) * min(radioTextura, 1) * 0.5;
                return tex2D(_MainTex, uv) * i.color;
            }
            ENDHLSL
        }
    }
}
