Shader "El Sendero/Dibujo"
{
    // Aspecto de dibujo animado de todo el juego: dos tonos planos por superficie y sombra con color.
    // Cómo se ve (tinte de la sombra, borde, brillo del pelo) se ajusta para todo el juego en el
    // asset AspectoDeDibujo; el material solo dice de qué color es la pieza.
    Properties
    {
        [MainTexture] _BaseMap("Textura de color", 2D) = "white" {}
        _TextureImpact("Cuánto pinta la textura", Range(0, 1)) = 1.0
        _Tinte("Tinte", Color) = (1, 1, 1, 1)
        _Brillo("Brillo de pelo", Range(0, 1)) = 0.0
        [Toggle(DR_VERTEX_COLORS_ON)] _VertexColorsEnabled("Usar los colores de los vértices", Int) = 0
        [HDR] _EmissionColor("Color de emisión", Color) = (0, 0, 0)
        [NoScaleOffset] _EmissionMap("Textura de emisión", 2D) = "white" {}
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Recibe sombras", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Recorte por alfa", Float) = 0.0
        _Cutoff("Umbral del recorte", Range(0, 1)) = 0.5
        [Toggle(_SOMBRA_SIN_RECORTE)] _SombraSinRecorte("La sombra no usa el recorte (sombra estable)", Float) = 0.0
        [Toggle(_PINCEL)] _Pincel("Pinceladas (piezas de color liso)", Float) = 0.0

        [HideInInspector][MainColor] _BaseColor("Color (no tiñe)", Color) = (1, 1, 1, 1)
        [HideInInspector] _BumpMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento

            #pragma shader_feature_local DR_VERTEX_COLORS_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _PINCEL
            // La emisión se enciende en juego (ventanas de noche): la variante tiene que existir siempre.
            #pragma multi_compile_local_fragment _ _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "DibujoEntrada.hlsl"
            #include "DibujoLuz.hlsl"
            #include "DibujoPincel.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                #if defined(DR_VERTEX_COLORS_ON)
                float4 color : COLOR;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                #if defined(DR_VERTEX_COLORS_ON)
                half4 color : COLOR;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertice(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posicion = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posicion.positionCS;
                output.positionWS = posicion.positionWS;
                output.normalWS = normal.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #if defined(_FOG_FRAGMENT)
                output.fogFactor = 0;
                #else
                output.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                #endif
                #if defined(DR_VERTEX_COLORS_ON)
                output.color = input.color;
                #endif
                return output;
            }

            half4 Fragmento(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 textura = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                // El alfa del tinte solo cuenta en los transparentes (fantasmas): en los opacos vale 1.
                half alfa = textura.a * _Tinte.a;
                #if defined(_ALPHATEST_ON)
                clip(alfa - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half3 albedo = lerp(half3(1.0h, 1.0h, 1.0h), textura.rgb, _TextureImpact) * _Tinte.rgb;
                #if defined(DR_VERTEX_COLORS_ON)
                albedo *= input.color.rgb;
                #endif
                #if defined(_PINCEL)
                albedo *= Pincelada(input.positionWS, NormalizeNormalPerPixel(input.normalWS));
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                half3 color = ColorDeDibujo(inputData, albedo, _Brillo);
                #if defined(_EMISSION)
                color += SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
                #endif
                color = MixFog(color, inputData.fogCoord);
                return half4(color, OutputAlpha(alfa, IsSurfaceTypeTransparent(_Surface)));
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SOMBRA_SIN_RECORTE
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // Con "sombra sin recorte" la sombra es la silueta completa: no parpadea con el recorte.
            #if defined(_SOMBRA_SIN_RECORTE)
            #undef _ALPHATEST_ON
            #endif
            #include "DibujoEntrada.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "DibujoEntrada.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // El contorno de tinta y el SSAO leen las normales de este pase.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile_instancing
            #include "DibujoEntrada.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
