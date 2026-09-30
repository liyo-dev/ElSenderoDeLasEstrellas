using UnityEngine;

/// Un enemigo al que solo se le puede hacer daño cuando está expuesto. Si le golpean en otro
/// momento, el golpe no le quita vida; si 'curacionPorGolpe' es mayor que 0, además la recupera
/// (esa parte de lo que le habría quitado). En los dos casos avisa de un golpe mal dado.
///
/// Raúl, 26 sep 2026: «el demonio solo puede recibir daño cuando sale el aro». El 27 sep 2026
/// quitó la curación del Demonio 1: «es el primer jefe, solo le matamos cuando se le enciende el
/// aro, pero no absorbe los ataques» (curacionPorGolpe = 0). Quién dice cuándo está expuesto lo decide otro componente
/// (IExpuestoAlDano; en el Demonio, RuneCollar mientras el aro brilla), así que esta regla sirve
/// para cualquier jefe con su propia ventana. Va en el mismo GameObject que su Damageable.
/// Ver INC-469.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class SoloDanoCuandoExpuesto : MonoBehaviour, IFiltroDeDano
{
    [Tooltip("Qué parte del golpe recupera si le dan cuando no está expuesto. 0 = el golpe no hace nada; 1 = recupera lo mismo que le habría quitado.")]
    [SerializeField, Min(0f)] private float curacionPorGolpe = 0f;

    [Tooltip("Efecto opcional sobre el enemigo cuando se cura por un golpe a destiempo.")]
    [SerializeField] private GameObject vfxCuracion;
    [SerializeField, Min(0.1f)] private float duracionVfx = 1.5f;

    private IExpuestoAlDano _ventana;

    void Awake()
    {
        _ventana = GetComponentInChildren<IExpuestoAlDano>(true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (_ventana == null)
            Debug.LogWarning($"[SoloDanoCuandoExpuesto:{name}] No hay nada que diga cuándo está expuesto " +
                             "(IExpuestoAlDano, p. ej. RuneCollar): se le puede hacer daño siempre.", this);
#endif
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_ventana == null || _ventana.Expuesto) return cantidad;

        AvisosDeCombate.GolpeMalDado(gameObject);

        float cura = cantidad * curacionPorGolpe;
        if (cura <= 0f) return 0f;

        if (vfxCuracion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCuracion, transform.position + Vector3.up, Quaternion.identity, duracionVfx);
        return -cura;
    }
}
