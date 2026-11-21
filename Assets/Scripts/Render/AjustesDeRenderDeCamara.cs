using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Copia de una cámara a otra todo lo que decide cómo se ve la imagen: renderer, postprocesado,
/// antialias, volúmenes que escucha, capas que dibuja, planos de corte y texturas que pide. Lo usan
/// las cámaras que relevan a la de juego un rato (diálogos, planos aéreos…) para que el juego se vea
/// igual con cualquiera de ellas: mismo contorno, mismo postprocesado, misma lluvia. Ver INC-1325.
public static class AjustesDeRenderDeCamara
{
    private static readonly FieldInfo CampoDelRenderer =
        typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void Copiar(Camera desde, Camera hacia)
    {
        if (desde == null || hacia == null || desde == hacia) return;
        hacia.cullingMask = desde.cullingMask;
        hacia.nearClipPlane = desde.nearClipPlane;
        hacia.farClipPlane = desde.farClipPlane;
        hacia.allowHDR = desde.allowHDR;
        hacia.allowMSAA = desde.allowMSAA;
        hacia.useOcclusionCulling = desde.useOcclusionCulling;

        var a = desde.GetUniversalAdditionalCameraData();
        var b = hacia.GetUniversalAdditionalCameraData();
        if (a == null || b == null) return;
        if (CampoDelRenderer != null && CampoDelRenderer.GetValue(a) is int indice) b.SetRenderer(indice);
        b.renderPostProcessing = a.renderPostProcessing;
        b.antialiasing = a.antialiasing;
        b.antialiasingQuality = a.antialiasingQuality;
        b.stopNaN = a.stopNaN;
        b.dithering = a.dithering;
        b.renderShadows = a.renderShadows;
        b.requiresDepthTexture = a.requiresDepthTexture;
        b.requiresColorTexture = a.requiresColorTexture;
        b.volumeLayerMask = a.volumeLayerMask;
        // El disparador de volúmenes locales es la propia cámara que mira (sin disparador fijo).
        b.volumeTrigger = null;
    }
}
