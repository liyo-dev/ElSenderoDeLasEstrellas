using System;
using System.Collections.Generic;

/// <summary>
/// Un tramo de la historia descrito con señales y misiones: empieza cuando se cumple cualquiera de
/// sus condiciones de inicio y acaba cuando se cumple cualquiera de las de fin. Sirve a cualquier
/// componente del mundo que solo deba hacer algo durante una parte de la historia (un oficio, una
/// discusión, una barrera) sin escribir las condiciones en su código.
///
/// Las señales se CONSULTAN (si se han emitido alguna vez), nunca se escuchan: así no se le roba
/// una señal pendiente a quien la está esperando.
/// </summary>
[Serializable]
public class TramoDeHistoria
{
    [UnityEngine.Tooltip("Empieza en cuanto se ha emitido alguna de estas señales. Vacío (y sin misiones) = desde el principio.")]
    public List<string> empiezaConSenal = new();
    [UnityEngine.Tooltip("Empieza en cuanto alguna de estas misiones está en curso.")]
    public List<string> empiezaConMisionActiva = new();
    [UnityEngine.Tooltip("Acaba en cuanto se ha emitido alguna de estas señales.")]
    public List<string> acabaConSenal = new();
    [UnityEngine.Tooltip("Acaba en cuanto alguna de estas misiones ha empezado (en curso o completada).")]
    public List<string> acabaConMisionIniciada = new();
    [UnityEngine.Tooltip("Acaba en cuanto alguna de estas misiones está completada.")]
    public List<string> acabaConMisionCompletada = new();

    public bool TieneCondicionDeInicio => Hay(empiezaConSenal) || Hay(empiezaConMisionActiva);

    /// Ha empezado y todavía no ha acabado. Sin señales disponibles, solo vale si no pide nada para empezar.
    public bool EnCurso()
    {
        var s = DefaultNarrativeSignals.Instance ?? DefaultNarrativeSignals.EnsureInstance();
        if (s == null) return !TieneCondicionDeInicio;
        return HaEmpezado(s) && !HaAcabado(s);
    }

    public bool HaAcabado()
    {
        var s = DefaultNarrativeSignals.Instance ?? DefaultNarrativeSignals.EnsureInstance();
        return s != null && HaAcabado(s);
    }

    private bool HaEmpezado(DefaultNarrativeSignals s)
    {
        if (!TieneCondicionDeInicio) return true;
        foreach (var k in empiezaConSenal)
            if (!string.IsNullOrEmpty(k) && s.HasEverRaised(k)) return true;
        foreach (var q in empiezaConMisionActiva)
            if (!string.IsNullOrEmpty(q) && s.GetQuestState(q) == NarrativeQuestState.Active) return true;
        return false;
    }

    private bool HaAcabado(DefaultNarrativeSignals s)
    {
        foreach (var k in acabaConSenal)
            if (!string.IsNullOrEmpty(k) && s.HasEverRaised(k)) return true;
        foreach (var q in acabaConMisionIniciada)
        {
            if (string.IsNullOrEmpty(q)) continue;
            var e = s.GetQuestState(q);
            if (e == NarrativeQuestState.Active || e == NarrativeQuestState.Completed) return true;
        }
        foreach (var q in acabaConMisionCompletada)
            if (!string.IsNullOrEmpty(q) && s.GetQuestState(q) == NarrativeQuestState.Completed) return true;
        return false;
    }

    private static bool Hay(List<string> l)
    {
        if (l == null) return false;
        foreach (var x in l) if (!string.IsNullOrEmpty(x)) return true;
        return false;
    }
}
