using System;
using System.Collections;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// Cambia la franja del día en la escena. Sigue en el acto; la luz y el cielo cambian en paralelo.
[Serializable]
[NarrativeNodeInfo("Mundo", "Hora del día", "Pasa a una franja del día (con transición de luz, o al instante).")]
public sealed class PedirHoraDelDia : NarrativeRequest, INarrativeStateEffect
{
    [Tooltip("Periodo del día al que transicionar.")]
    public DayNightCycle.TimeOfDay targetTime = DayNightCycle.TimeOfDay.Night;

    [Tooltip("Si true, aplica el cambio instantáneamente sin transición suave de luz.")]
    public bool immediate = false;

    public override void Describe(INarrativeNodeDescriber d)
        => d.Line($"Pasa a {targetTime}{(immediate ? " (al instante)" : string.Empty)}", NarrativeTextTone.Strong);

    public void Project(INarrativeStateWriter state)
        => state.SetFact(HechosDelSendero.HoraDelDia, HechosDelSendero.Actual, targetTime.ToString());
}

public sealed class HoraDelDiaEnCurso : NarrativeRequestRun<PedirHoraDelDia>
{
    protected override void Start()
    {
        var cycle = DayNightCycle.Instance;
        if (cycle != null) cycle.SetTimeOfDay(R.targetTime, R.immediate);
        else Debug.LogWarning("[Hora del día] No hay DayNightCycle en la escena.");
        Finish(0);
    }
}

/// Pregunta la franja del día: sale por «Sí» si ahora es esa franja (con Noche también cuenta la
/// medianoche) y por «No» si no.
[Serializable]
[NarrativeNodeInfo("Mundo", "Según la hora del día", "Bifurca Sí/No según si ahora es una franja del día (p. ej. de noche).")]
public sealed class PedirSegunHoraDelDia : NarrativeRequest, INarrativeRouteEffect
{
    static readonly string[] Salidas = { "Sí", "No" };

    [Tooltip("Franja del día que se comprueba. Con Noche también cuenta la medianoche.")]
    public DayNightCycle.TimeOfDay franja = DayNightCycle.TimeOfDay.Night;

    public override string[] Exits => Salidas;

    /// Es una decisión: al rehacer la historia no se adivina la rama.
    public override int ProjectionExit => NarrativeNode.ProjectionStops;

    public override void Describe(INarrativeNodeDescriber d) => d.Line($"¿Es {franja}?", NarrativeTextTone.Strong);

    /// Si la historia siguió por «Sí», era esa franja.
    public void ProjectRoute(INarrativeStateWriter state, int chosenPort)
    {
        if (chosenPort == 0) state.SetFact(HechosDelSendero.HoraDelDia, HechosDelSendero.Actual, franja.ToString());
    }

    public override string DescribeDecision(NarrativeNode step)
        => $"Según la hora \"{step?.DisplayTitle}\" (guid {step?.Guid}): ¿es {franja}? Salidas: Sí / No.";

    /// Si ahora es esa franja del día (con Noche también cuenta la medianoche). Sin ciclo, sí.
    public static bool EsLaFranja(DayNightCycle.TimeOfDay franja)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null) return true;
        var ahora = ciclo.CurrentTimeOfDay;
        return ahora == franja || (franja == DayNightCycle.TimeOfDay.Night && ahora == DayNightCycle.TimeOfDay.Midnight);
    }
}

public sealed class SegunHoraDelDiaEnCurso : NarrativeRequestRun<PedirSegunHoraDelDia>
{
    protected override void Start() => Finish(PedirSegunHoraDelDia.EsLaFranja(R.franja) ? 0 : 1);
}

/// Espera a que llegue una franja del día (por defecto, la noche) con el jugador cerca de una marca.
/// El tiempo pasa solo con el ciclo de DayNightCycle o descansando en una hoguera
/// (CampfireRestInteractable). Si ya es esa franja y el jugador está en la marca, sigue enseguida.
/// Sin marca, basta con la hora.
[Serializable]
[SavePoint("Seguro guardar mientras espera la hora")]
[NarrativeNodeInfo("Mundo", "Esperar la hora del día", "Espera a que sea una franja del día (p. ej. de noche) y, si hay marca, a que el jugador esté cerca de ella. El tiempo pasa solo o descansando en una hoguera.")]
public sealed class PedirEsperarHoraDelDia : NarrativeRequest, INarrativeStateEffect
{
    [Tooltip("Franja del día que se espera. Con Noche también cuenta la medianoche.")]
    public DayNightCycle.TimeOfDay franja = DayNightCycle.TimeOfDay.Night;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("anchorId del SpawnAnchor junto al que tiene que estar el jugador. Vacío = da igual dónde esté.")]
    public string anchorId = "";

    [Min(1f)]
    [Tooltip("Metros alrededor de la marca que cuentan como estar en ella.")]
    public float radio = 18f;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line($"Espera a que sea {franja}", NarrativeTextTone.Strong);
        if (!string.IsNullOrWhiteSpace(anchorId)) d.Anchor(anchorId);
    }

    /// Al seguir, ya es esa franja.
    public void Project(INarrativeStateWriter state)
        => state.SetFact(HechosDelSendero.HoraDelDia, HechosDelSendero.Actual, franja.ToString());
}

public sealed class EsperarHoraDelDiaEnCurso : NarrativeRequestRun<PedirEsperarHoraDelDia>
{
    Coroutine _rutina;

    protected override void Start()
    {
        if (Runner == null || DayNightCycle.Instance == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Esperar la hora:{StepGuid}] Sin runner o sin DayNightCycle: sigue para no bloquear.");
#endif
            Finish(0);
            return;
        }
        _rutina = Runner.StartCoroutine(Co_Esperar());
    }

    protected override void Stop()
    {
        if (_rutina != null && Runner != null) Runner.StopCoroutine(_rutina);
        _rutina = null;
    }

    IEnumerator Co_Esperar()
    {
        var espera = new WaitForSeconds(0.5f);
        var marca = string.IsNullOrWhiteSpace(R.anchorId) ? null : SpawnAnchor.FindById(R.anchorId);
        while (!PedirSegunHoraDelDia.EsLaFranja(R.franja) || !EnLaMarca(marca))
            yield return espera;
        _rutina = null;
        Finish(0);
    }

    bool EnLaMarca(SpawnAnchor marca)
    {
        if (marca == null) return true;
        var jugador = PlayerService.PlayerTransform;
        if (jugador == null) return false;
        var d = jugador.position - marca.transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= R.radio * R.radio;
    }
}
