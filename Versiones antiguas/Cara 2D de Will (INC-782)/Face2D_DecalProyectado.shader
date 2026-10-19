// Cara 2D de los personajes como decal proyectado de URP: el dibujo se pinta sobre la piel de la
// cabeza, así que de perfil la cara gira con ella en vez de verse el canto de una cartulina.
// Se dibuja el cubo de la caja del proyector y, por cada píxel, se reconstruye la piel desde la
// profundidad y se pregunta si cae dentro: lo que esté por delante (flequillo, pelo) tapa la cara.
Shader "El Sendero/Personajes/Face2D_DecalProyectado"
{
    Properties
    {
        [MainTexture] _BaseMap ("Dibujo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tinte", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            // Los decales de URP se dibujan en el rango opaco: con cola «Transparent» el pase los
            // descarta por completo (el pase filtra con RenderQueueRange.opaque).
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // Proyector de decal en la técnica de espacio de pantalla (funciona con cualquier shader de
        // piel, no sólo con los que leen la DBuffer).
        Pass
        {
            Name "DecalScreenSpaceProjector"
            Tags { "LightMode" = "DecalScreenSpaceProjector" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // El cubo se dibuja con la matriz de la caja del proyector ya puesta en unity_ObjectToWorld.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Tal cual la plantilla de decales de URP: la profundidad se carga con la coordenada
                // volteada (en D3D el origen de pantalla está arriba) y la reconstrucción del mundo
                // usa la UV sin voltear, que es la que espera ComputeWorldSpacePosition.
                float2 pixelVolteado = input.positionCS.xy;
                TransformScreenUV(pixelVolteado, _ScreenSize.y);
                float profundidad = LoadSceneDepth(pixelVolteado.xy);
                float2 uvPantalla = input.positionCS.xy * _ScreenSize.zw;
                float3 posicionMS = ComputeWorldSpacePosition(uvPantalla, profundidad, UNITY_MATRIX_I_VP);

                // Al espacio de la caja: el cubo va de -0.5 a 0.5 y su cara XZ es el dibujo.
                float3 posicionCaja = TransformWorldToObject(posicionMS);
                float dentro = 0.5 - max(abs(posicionCaja.x), max(abs(posicionCaja.y), abs(posicionCaja.z)));
                clip(dentro);

                half4 dibujo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, posicionCaja.xz + 0.5) * _BaseColor;
                clip(dibujo.a - 0.004);
                return dibujo;
            }
            ENDHLSL
        }

        // Mismo trabajo para el caso de decal de malla.
        Pass
        {
            Name "DecalScreenSpaceMesh"
            Tags { "LightMode" = "DecalScreenSpaceMesh" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Tal cual la plantilla de decales de URP: la profundidad se carga con la coordenada
                // volteada (en D3D el origen de pantalla está arriba) y la reconstrucción del mundo
                // usa la UV sin voltear, que es la que espera ComputeWorldSpacePosition.
                float2 pixelVolteado = input.positionCS.xy;
                TransformScreenUV(pixelVolteado, _ScreenSize.y);
                float profundidad = LoadSceneDepth(pixelVolteado.xy);
                float2 uvPantalla = input.positionCS.xy * _ScreenSize.zw;
                float3 posicionMS = ComputeWorldSpacePosition(uvPantalla, profundidad, UNITY_MATRIX_I_VP);

                float3 posicionCaja = TransformWorldToObject(posicionMS);
                float dentro = 0.5 - max(abs(posicionCaja.x), max(abs(posicionCaja.y), abs(posicionCaja.z)));
                clip(dentro);

                half4 dibujo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, posicionCaja.xz + 0.5) * _BaseColor;
                clip(dibujo.a - 0.004);
                return dibujo;
            }
            ENDHLSL
        }

        // Vista previa al seleccionar el proyector en la escena: el dibujo tal cual.
        Pass
        {
            Name "DecalPreview"
            Tags { "LightMode" = "DecalPreview" }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // El cubo de la caja va de -0.5 a 0.5: el dibujo cae en su cara XZ.
                half4 dibujo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.positionOS.xz + 0.5) * _BaseColor;
                clip(dibujo.a - 0.004);
                return dibujo;
            }
            ENDHLSL
        }
    }
}
