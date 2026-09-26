using System;
using UnityEngine;

/// Un enemigo al que solo se le puede hacer daño cuando está expuesto. Si le golpean en otro
/// momento, en vez de perder vida la recupera: exactamente lo que le habría quitado el golpe
/// (multiplicado por 'curacionPorGolpe').
///
/// Raúl, 26 sep 2026: «el demonio solo puede recibir daño cuando sale el aro; si le damos y no
/// tiene el aro, se recupera vida». Quién dice cuándo está expuesto lo decide otro componente
/// (IExpuestoAlDano; en el Demonio, RuneCollar mientras el aro brilla), así que esta regla sirve
/// para cualquier jefe con su propia ventana. Va en el mismo GameObject que su Damageable.
/// Ver INC-469.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class SoloDanoCuandoExpuesto : MonoBehaviour, IFiltroDeDano
{
    [Tooltip("Qué parte del golpe recupera si le dan cuando no está expuesto. 1 = lo mismo que le habría quitado.")]
    [SerializeField, Min(0f)] private float curacionPorGolpe = 1f;

    [Tooltip("Efecto opcional sobre el enemigo cuando se cura por un golpe a destiempo.")]
    [SerializeField] private GameObject vfxCuracion;
    [SerializeField, Min(0.1f)] private float duracionVfx = 1.5f;

    /// Alguien ha golpeado a un enemigo fuera de su ventana y este se ha curado (quién, cuánto).
    /// Para que otros sistemas reaccionen (Eldran lo explica la primera vez).
    public static event Action<SoloDanoCuandoExpuesto, float> AlCurarsePorGolpe;

    private IExpuestoAlDano _ventana;

    void Awake()
    {
        _ventana = GetComponentInChildren<IExpuestoAlDano>(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_ventana == null)
            Debug.LogWarning($"[SoloDanoCuandoExpuesto:{name}] No hay nada que diga cuándo está expuesto " +
                             "(IExpuestoAlDano, p. ej. RuneCollar): se le puede hacer daño siempre.", this);
#endif
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AlCurarsePorGolpe = null;
#endif

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_ventana == null || _ventana.Expuesto) return cantidad;

        float cura = cantidad * curacionPorGolpe;
        if (cura <= 0f) return 0f;

        if (vfxCuracion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCuracion, transform.position + Vector3.up, Quaternion.identity, duracionVfx);

        AlCurarsePorGolpe?.Invoke(this, cura);
        return -cura;
    }
}
