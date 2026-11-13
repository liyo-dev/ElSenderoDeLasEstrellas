#ifndef EL_SENDERO_DIBUJO_ENTRADA_INCLUDED
#define EL_SENDERO_DIBUJO_ENTRADA_INCLUDED

// Entrada común a todos los pases de «El Sendero/Dibujo». Las propiedades por material van en un
// único CBUFFER (el mismo en todos los pases) para que el SRP Batcher agrupe.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;       // No tiñe: solo su alfa cuenta en el recorte (lo leen los pases de URP).
half4 _Tinte;
half4 _EmissionColor;
half _Cutoff;
half _SombraSinRecorte;
half _Surface;
half _TextureImpact;
half _Brillo;
half _Pincel;
CBUFFER_END

#endif
