using System;
using UnityEngine;

/// Bifurca según la franja del día en este momento: Sí si es esa franja (con Noche también cuenta
/// la medianoche), No si no.
[Serializable]
[NarrativeNodeInfo("Mundo", "Según la hora del día", "Bifurca Sí/No según si ahora es una franja del día (p. ej. de noche).")]
[SavePoint("Solo consulta la hora; seguro para guardar")]
public sealed class SegunHoraDelDiaNode : NarrativeNode
{
    [Tooltip("Franja del día que se comprueba. Con Noche también cuenta la medianoche.")]
    public DayNightCycle.TimeOfDay franja = DayNightCycle.TimeOfDay.Night;

    static readonly string[] Ports = { "Sí", "No" };
    public override string[] GetOutputPorts() => Ports;

    public override string DescribeDecision()
        => $"Según la hora \"{displayTitle}\" (guid {guid}): ¿es {franja}? Salidas: Sí / No.";

    public override void Enter(NarrativeContext ctx, Action ready)
        => AdvanceThrough(ctx, ready, EsperarHoraDelDiaNode.EsLaFranja(franja) ? 0 : 1);
}
