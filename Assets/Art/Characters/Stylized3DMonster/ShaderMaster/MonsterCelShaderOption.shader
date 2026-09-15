Shader "PVFX/PVFX_URP_Low_CelShader1.0"
{
    Properties
    {
        [Header(Base Map)]
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)

        [Header(Outline Settings)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Float) = 0.005

        [Header(Rim Lighting)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,10)) = 3
        _RimIntensity ("Rim Intensity", Range(0,3)) = 1

        [Header(Metallic and Emission)]
        _MetallicStrength ("Metallic Strength", Range(0,1)) = 0
        _MetallicColor ("Metallic Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        [Header(Cel Shading)]
        _ShadeThreshold ("Shade Threshold", Range(0,1)) = 0
        _ShadeIntensity ("Shade Intensity", Range(0,1)) = 0
        _ShadeColor ("Shade Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        // ------------------------------------------------------------
        // PASS 1: OUTLINE
        // ------------------------------------------------------------
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            float _OutlineWidth;
            fixed4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                float4 pos = v.vertex;
                pos.xyz += norm * _OutlineWidth;
                o.pos = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // ------------------------------------------------------------
        // PASS 2: BASE LIGHTING & EFFECTS
        // ------------------------------------------------------------
        Pass
        {
            Name "BASE"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST; 
            fixed4 _Color;

            fixed4 _RimColor;
            float _RimPower;
            float _RimIntensity;

            float _MetallicStrength;
            fixed4 _MetallicColor;
            fixed4 _EmissionColor;

            float _ShadeThreshold;
            float _ShadeIntensity;
            fixed4 _ShadeColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * _Color;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = saturate(dot(normal, lightDir));

                // Cel Shading Logic
                float shadeMask = step(NdotL, _ShadeThreshold);
                float litMask = 1.0 - shadeMask;

                fixed3 litCol = baseCol.rgb;
                // Shadow color darkened by Intensity
                fixed3 shadeCol = baseCol.rgb * _ShadeColor.rgb * (1.0 - _ShadeIntensity);
                fixed3 lightingCol = (litCol * litMask) + (shadeCol * shadeMask);

                // Rim Light
                float rim = pow(1.0 - saturate(dot(viewDir, normal)), _RimPower) * _RimIntensity;
                fixed3 rimCol = _RimColor.rgb * rim;

                // Specular
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), 32) * _MetallicStrength;
                fixed3 specCol = _MetallicColor.rgb * spec;

                fixed3 finalCol = lightingCol + rimCol + specCol + _EmissionColor.rgb;
                
                return fixed4(finalCol, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}