using System;
using System.Collections;
using UnityEngine;

/// Espera a que llegue una franja del día (por defecto, la noche) con el jugador cerca de una marca.
/// El tiempo pasa solo con el ciclo de DayNightCycle o descansando en una hoguera
/// (CampfireRestInteractable). Si ya es esa franja y el jugador está en la marca, avanza enseguida.
/// Sin marca, basta con la hora.
[Serializable]
[NarrativeNodeInfo("Mundo", "Esperar la hora del día", "Espera a que sea una franja del día (p. ej. de noche) y, si hay marca, a que el jugador esté cerca de ella. El tiempo pasa solo o descansando en una hoguera.")]
[SavePoint("Seguro guardar mientras se espera la hora")]
public sealed class EsperarHoraDelDiaNode : NarrativeNode
{
    [Tooltip("Franja del día que se espera. Con Noche también cuenta la medianoche.")]
    public DayNightCycle.TimeOfDay franja = DayNightCycle.TimeOfDay.Night;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("anchorId del SpawnAnchor junto al que tiene que estar el jugador. Vacío = da igual dónde esté.")]
    public string anchorId = "";

    [Min(1f)]
    [Tooltip("Metros alrededor de la marca que cuentan como estar en ella.")]
    public float radio = 18f;

    [NonSerialized] private Coroutine _rutina;
    [NonSerialized] private NarrativeRunner _runner;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (!string.IsNullOrWhiteSpace(anchorId)) d.Anchor(anchorId);
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx?.Runner == null || DayNightCycle.Instance == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[EsperarHoraDelDiaNode:{guid}] Sin runner o sin DayNightCycle: se avanza para no bloquear.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }
        _runner = ctx.Runner;
        _rutina = _runner.StartCoroutine(Co_Esperar(onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_rutina != null && _runner != null) _runner.StopCoroutine(_rutina);
        _rutina = null;
        _runner = null;
    }

    private IEnumerator Co_Esperar(Action listo)
    {
        var espera = new WaitForSeconds(0.5f);
        var marca = string.IsNullOrWhiteSpace(anchorId) ? null : SpawnAnchor.FindById(anchorId);
        while (!EsLaHora() || !EnLaMarca(marca))
            yield return espera;
        _rutina = null;
        listo?.Invoke();
    }

    private bool EsLaHora() => EsLaFranja(franja);

    /// Si ahora es esa franja del día (con Noche también cuenta la medianoche). Sin ciclo, sí.
    public static bool EsLaFranja(DayNightCycle.TimeOfDay franja)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null) return true;
        var ahora = ciclo.CurrentTimeOfDay;
        return ahora == franja || (franja == DayNightCycle.TimeOfDay.Night && ahora == DayNightCycle.TimeOfDay.Midnight);
    }

    private bool EnLaMarca(SpawnAnchor marca)
    {
        if (marca == null) return true;
        var jugador = PlayerService.PlayerTransform;
        if (jugador == null) return false;
        var d = jugador.position - marca.transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= radio * radio;
    }
}
