using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// <summary>
/// Lo que se siente al acertar la B en el momento justo (devolver un proyectil o desviar un
/// golpe, <see cref="PlayerShieldController.AlContraatacar"/>):
/// <list type="number">
/// <item>Congelado de imagen un instante y cámara lenta que vuelve suave a la velocidad normal
/// (<see cref="TimeScaleArbiterService"/>).</item>
/// <item>Destello de pantalla, sacudida de cámara y vibración del mando.</item>
/// <item>Capa de sonido y onda en los pies del jugador.</item>
/// </list>
/// El escudo sigue con su propio efecto y sonido en el punto del choque; esto es la presentación.
/// Todo en tiempo real (sin escalar), para que la cámara lenta no estire el propio efecto.
/// Ver INC-670.
/// </summary>
[DisallowMultipleComponent]
public class PresentacionDelContraataque : MonoBehaviour
{
    [Header("Tiempo")]
    [Tooltip("Segundos de imagen congelada justo al acertar.")]
    [SerializeField, Min(0f)] private float congelado = 0.07f;
    [Tooltip("Velocidad del juego durante la cámara lenta (1 = normal).")]
    [SerializeField, Range(0.05f, 1f)] private float camaraLenta = 0.25f;
    [Tooltip("Segundos de cámara lenta antes de empezar a recuperar la velocidad.")]
    [SerializeField, Min(0f)] private float duracionLenta = 0.18f;
    [Tooltip("Segundos que tarda en volver a la velocidad normal.")]
    [SerializeField, Min(0.01f)] private float vuelta = 0.25f;

    [Header("Pantalla y cámara")]
    [SerializeField] private Color colorDelDestello = new Color(1f, 0.92f, 0.65f, 0.35f);
    [SerializeField, Min(0f)] private float duracionDelDestello = 0.15f;
    [SerializeField, Min(0f)] private float sacudida = 0.4f;
    [SerializeField, Min(0f)] private float duracionDeLaSacudida = 0.2f;

    [Header("Mando")]
    [SerializeField, Range(0f, 1f)] private float vibracionGrave = 0.5f;
    [SerializeField, Range(0f, 1f)] private float vibracionAguda = 0.9f;
    [SerializeField, Min(0f)] private float duracionDeLaVibracion = 0.18f;

    [Header("Sonido y efecto")]
    [Tooltip("Capa de sonido sobre la del escudo. Vacío = ninguna.")]
    [SerializeField] private string sfxDelAcierto = "Star_Collision";
    [Tooltip("Onda en los pies del jugador (pool de VFX). Vacío = ninguna.")]
    [SerializeField] private GameObject ondaEnLosPies;
    [SerializeField, Min(0.1f)] private float duracionDeLaOnda = 2f;

    private PlayerShieldController _escudo;
    private Coroutine _tiempo;

    void Awake() => _escudo = GetComponentInParent<PlayerShieldController>();

    void OnEnable()
    {
        if (_escudo != null) _escudo.AlContraatacar += Presentar;
    }

    void OnDisable()
    {
        if (_escudo != null) _escudo.AlContraatacar -= Presentar;
        if (_tiempo != null) { StopCoroutine(_tiempo); _tiempo = null; }
        TimeScaleArbiterService.Release(this);
    }

    private void Presentar(Vector3 donde)
    {
        if (_tiempo != null) StopCoroutine(_tiempo);
        _tiempo = StartCoroutine(Co_Tiempo());

        if (duracionDelDestello > 0f) FeedbackService.ScreenFlash(colorDelDestello, duracionDelDestello);
        if (sacudida > 0f) FeedbackService.CameraShake(sacudida, duracionDeLaSacudida);
        FeedbackService.Vibrar(vibracionGrave, vibracionAguda, duracionDeLaVibracion);

        if (!string.IsNullOrEmpty(sfxDelAcierto) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxDelAcierto, 1f, donde);
        if (ondaEnLosPies != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(ondaEnLosPies, transform.position + Vector3.up * 0.05f, Quaternion.identity, duracionDeLaOnda);
    }

    /// Congelado, cámara lenta y vuelta suave a la velocidad normal, en tiempo real.
    private IEnumerator Co_Tiempo()
    {
        if (congelado > 0f)
        {
            TimeScaleArbiterService.Request(this, 0f);
            yield return Esperar(congelado);
        }

        TimeScaleArbiterService.Request(this, camaraLenta);
        yield return Esperar(duracionLenta);

        float t = 0f;
        while (t < vuelta)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / vuelta);
            TimeScaleArbiterService.Request(this, Mathf.Lerp(camaraLenta, 1f, k));
            yield return null;
        }

        TimeScaleArbiterService.Release(this);
        _tiempo = null;
    }

    private static IEnumerator Esperar(float segundos)
    {
        float fin = Time.unscaledTime + segundos;
        while (Time.unscaledTime < fin) yield return null;
    }
}
