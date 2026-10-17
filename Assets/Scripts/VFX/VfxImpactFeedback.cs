using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Componente reutilizable para efectos visuales (VFX) que dispara feedbacks sensoriales
/// (sacudida de cámara, destello de pantalla, micro-hitstop, vibración y atenuación de luz dinámica).
/// Compatible con VfxPoolService (se activa en OnEnable cada vez que se toma del pool).
/// </summary>
public class VfxImpactFeedback : MonoBehaviour
{
    [Header("Sacudida de cámara")]
    [Tooltip("Intensidad del temblor (0 para desactivar).")]
    public float shakeIntensity = 0.7f;
    [Tooltip("Duración en segundos del temblor.")]
    public float shakeDuration = 0.45f;

    [Header("Destello de pantalla")]
    public bool enableScreenFlash = true;
    public Color flashColor = new Color(1f, 0.96f, 0.85f, 0.85f);
    public float flashDuration = 0.25f;

    [Header("Hit Stop (micro-pausa cinética)")]
    public bool enableHitStop = false;
    [Range(0.01f, 0.5f)] public float hitStopTimeScale = 0.04f;
    [Range(0.01f, 0.5f)] public float hitStopDuration = 0.08f;

    [Header("Vibración de mando")]
    public bool enableRumble = true;
    [Range(0f, 1f)] public float rumbleLow = 0.8f;
    [Range(0f, 1f)] public float rumbleHigh = 0.9f;
    public float rumbleDuration = 0.5f;

    [Header("Luz dinámica")]
    public Light dynamicLight;
    public float lightFadeDuration = 0.6f;

    private float _initialLightIntensity;
    private Coroutine _lightFadeRoutine;

    private void Awake()
    {
        if (dynamicLight == null)
            dynamicLight = GetComponentInChildren<Light>(true);

        if (dynamicLight != null)
            _initialLightIntensity = dynamicLight.intensity;
    }

    private void OnEnable()
    {
        // 1. Sacudida de cámara
        if (shakeIntensity > 0f && shakeDuration > 0f)
            FeedbackService.CameraShake(shakeIntensity, shakeDuration);

        // 2. Destello de pantalla
        if (enableScreenFlash && flashDuration > 0f && flashColor.a > 0.01f)
            FeedbackService.ScreenFlash(flashColor, flashDuration);

        // 3. Micro Hit Stop
        if (enableHitStop && hitStopDuration > 0f)
            FeedbackService.HitStop(hitStopTimeScale, hitStopDuration);

        // 4. Vibración
        if (enableRumble && rumbleDuration > 0f)
            FeedbackService.Vibrar(rumbleLow, rumbleHigh, rumbleDuration);

        // 5. Atenuación de luz
        if (dynamicLight != null)
        {
            if (_lightFadeRoutine != null)
            {
                StopCoroutine(_lightFadeRoutine);
                _lightFadeRoutine = null;
            }
            dynamicLight.intensity = _initialLightIntensity;
            dynamicLight.enabled = true;

            if (lightFadeDuration > 0f)
                _lightFadeRoutine = StartCoroutine(Co_FadeLight());
        }
    }

    private void OnDisable()
    {
        if (_lightFadeRoutine != null)
        {
            StopCoroutine(_lightFadeRoutine);
            _lightFadeRoutine = null;
        }

        if (dynamicLight != null)
        {
            dynamicLight.intensity = _initialLightIntensity;
            dynamicLight.enabled = false;
        }
    }

    private IEnumerator Co_FadeLight()
    {
        float elapsed = 0f;
        while (elapsed < lightFadeDuration && dynamicLight != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lightFadeDuration);
            dynamicLight.intensity = Mathf.Lerp(_initialLightIntensity, 0f, t * t);
            yield return null;
        }

        if (dynamicLight != null)
        {
            dynamicLight.intensity = 0f;
            dynamicLight.enabled = false;
        }
        _lightFadeRoutine = null;
    }
}
