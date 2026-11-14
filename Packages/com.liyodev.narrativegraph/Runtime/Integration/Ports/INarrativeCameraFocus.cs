using System.Collections;

/// <summary>Cómo enfoca la cámara un punto de interés.</summary>
public struct NarrativeCameraShot
{
    /// <summary>Segundos en el punto.</summary>
    public float Hold;
    /// <summary>Fundido de reaparición si la pantalla llega tapada (0 = ninguno).</summary>
    public float RevealFade;
    /// <summary>Campo de visión relativo durante el plano: 1 = sin zoom, 0.7 = acercar al 70 %.</summary>
    public float ZoomFactor;
    public float ZoomInSeconds;
    public float ZoomOutSeconds;
}

/// <summary>Corta la cámara a un punto de interés con nombre y la devuelve al terminar.</summary>
public interface INarrativeCameraFocus
{
    /// <summary>
    /// Rutina del plano completo (corte, zoom, espera, vuelta), o null si no existe el punto
    /// <paramref name="focusId"/>.
    /// </summary>
    IEnumerator Focus(string focusId, NarrativeCameraShot shot);
}
