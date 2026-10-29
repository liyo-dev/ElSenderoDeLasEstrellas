using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Planos de foco del grafo en El Sendero: corta la cámara del jugador a un
/// <see cref="CameraFocusPoint"/>, la reclama a <see cref="CameraDirectorService"/> mientras dura y
/// la mueve directamente (como DeathCameraEffect).
///
/// Mientras dura el plano se apaga el DayNightCycle (para que no reescriba la niebla), se quita la
/// niebla y se esconden las nubes sueltas de AmbientCloudDirector, por si alguna tapa el punto.
/// Al terminar todo vuelve como estaba.
/// </summary>
public sealed class CameraFocusNarrativeAdapter : INarrativeCameraFocus
{
    public IEnumerator Focus(string focusId, NarrativeCameraShot shot)
    {
        var point = FindFocusPoint(focusId);
        return point != null ? Co_Focus(point, shot) : null;
    }

    static CameraFocusPoint FindFocusPoint(string focusId)
    {
        if (string.IsNullOrEmpty(focusId)) return null;
        foreach (var p in Object.FindObjectsByType<CameraFocusPoint>())
            if (p.focusId == focusId) return p;
        return null;
    }

    static IEnumerator Co_Focus(CameraFocusPoint target, NarrativeCameraShot shot)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CameraFocusNarrativeAdapter] Camera.main no encontrada. Asegúrate de que la cámara del jugador está taggeada como MainCamera.");
            yield break;
        }

        float zoomFactor = Mathf.Clamp(shot.ZoomFactor, 0.3f, 1.5f);
        float originalFOV = cam.fieldOfView;
        float targetFOV = originalFOV * zoomFactor;
        bool doZoom = Mathf.Abs(shot.ZoomFactor - 1f) > 0.01f;
        var owner = new object();

        var dayNight = Object.FindAnyObjectByType<DayNightCycle>();
        bool dayNightWasEnabled = dayNight != null && dayNight.enabled;
        if (dayNight != null) dayNight.enabled = false;

        bool fogWasEnabled = RenderSettings.fog;
        RenderSettings.fog = false;

        var ambientClouds = Object.FindAnyObjectByType<AmbientCloudDirector>();
        if (ambientClouds != null) ambientClouds.SetAmbientCloudsVisible(false);

        CameraDirectorService.Claim(owner);
        cam.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

        // Si la pantalla llega tapada (una cinemática que acabó en negro a propósito), el corte ya
        // ha ocurrido a oscuras y es este plano el que destapa.
        if (FeedbackService.IsScreenFaded && shot.RevealFade > 0f)
            yield return FeedbackService.ScreenFadeAsync(Color.black, shot.RevealFade, fadeIn: false);

        // Un fotograma para que se procese el transform nuevo.
        yield return null;

        if (doZoom && shot.ZoomInSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < shot.ZoomInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.fieldOfView = Mathf.Lerp(originalFOV, targetFOV, Mathf.SmoothStep(0f, 1f, elapsed / shot.ZoomInSeconds));
                yield return null;
            }
            cam.fieldOfView = targetFOV;
        }
        else if (doZoom)
        {
            cam.fieldOfView = targetFOV;
            yield return null;
        }

        if (shot.Hold > 0f)
            yield return new WaitForSecondsRealtime(shot.Hold);

        if (doZoom && shot.ZoomOutSeconds > 0f)
        {
            float elapsed = 0f;
            float startFOV = cam.fieldOfView;
            while (elapsed < shot.ZoomOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.fieldOfView = Mathf.Lerp(startFOV, originalFOV, Mathf.SmoothStep(0f, 1f, elapsed / shot.ZoomOutSeconds));
                yield return null;
            }
        }

        cam.fieldOfView = originalFOV;

        RenderSettings.fog = fogWasEnabled;
        if (ambientClouds != null) ambientClouds.SetAmbientCloudsVisible(true);
        CameraDirectorService.Release(owner);
        if (dayNight != null && dayNightWasEnabled) dayNight.enabled = true;
    }
}
