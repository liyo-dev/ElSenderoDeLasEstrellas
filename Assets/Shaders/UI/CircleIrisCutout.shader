// Sendero/UI/CircleIrisCutout — 12/09/2026
//
// Shader UI mínimo para el "iris circular" pedido por Raúl para "Will, ¡DESPIERTA!"
// (ver DramaticTextOverlayUI.CircleIrisExit / DramaticExitAnimation.CircleIris).
//
// Recorta un agujero circular centrado en el Image al que se aplica: dentro del radio, alpha 0
// (transparente, deja ver lo que hay detrás — en este caso la cámara real del juego, ya en el
// plano cenital de SleepTrigger.sleepCameraAnchor); fuera del radio, opaco (el color del Image,
// normalmente negro). _Radius anima de pequeño a grande para crecer el círculo hasta cubrir la
// pantalla entera. _Aspect corrige que la pantalla no sea cuadrada (si no, el "círculo" saldría
// ovalado). No requiere ningún Renderer Feature ni cámara/RenderTexture extra — es autocontenido.
//
// Uso: crear un Material en el Editor con este Shader, asignarlo a
// DramaticTextOverlayUI._irisMaterial (Inspector). El script lo instancia en runtime (Instantiate),
// nunca modifica el asset compartido.
Shader "Sendero/UI/CircleIrisCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (no usada, solo para compatibilidad con UI)", 2D) = "white" {}
        _Radius   ("Radius (espacio UV, 0 = agujero cerrado)", Range(0, 2)) = 0
        _Softness ("Suavizado del borde (unidades UV)", Range(0.001, 0.3)) = 0.02
        _Aspect   ("Aspect ratio (ancho/alto de pantalla)", Float) = 1.7777778
        // FIX (12/09/2026, pedido de Raúl "el circulo debe enfocar la cara de will que esta un
        // poco mas arriba"): antes el centro estaba fijo en (0.5,0.5) = centro exacto de pantalla,
        // así que el "ojo de cerradura" nunca coincidía con donde cae la cara de Will en el plano
        // cenital de sleepCameraAnchor (más arriba en el encuadre). _Center es ahora ajustable —
        // Y mayor = agujero más arriba en pantalla (UV: 0=abajo, 1=arriba).
        _Center   ("Centro del iris (UV, 0.5,0.5 = centro pantalla)", Vector) = (0.5, 0.5, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            float _Radius;
            float _Softness;
            float _Aspect;
            float4 _Center;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color    = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Centro del Image ajustable vía _Center (por defecto 0.5,0.5 = centro pantalla);
                // corregido por aspect ratio para que el agujero sea un círculo de verdad y no un
                // óvalo en pantallas no cuadradas.
                float2 uv = IN.texcoord - _Center.xy;
                uv.x *= _Aspect;
                float dist = length(uv);

                // Fuera de _Radius: opaco (alpha del color del Image). Dentro: transparente.
                // smoothstep da un borde suave de anchura _Softness en vez de un corte duro.
                float mask = smoothstep(_Radius - _Softness, _Radius + _Softness, dist);

                fixed4 col = IN.color;
                col.a *= mask;
                return col;
            }
            ENDCG
        }
    }
}
