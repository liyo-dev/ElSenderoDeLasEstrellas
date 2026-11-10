using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Dos actores se cogen (o se sueltan) de la mano: <see cref="DeLaMano"/> en quien guía. Lo pone el
/// guion horneado con «X de_la_mano Y» (al empezar y al soltarse), pero se puede usar suelto.
/// </summary>
[Serializable]
public class DeLaManoBeat : SequenceBeat
{
    [Tooltip("Quien guía (el adulto).")]
    public string guiaId;
    [Tooltip("Quien va cogido de su mano.")]
    public string quienId;
    [Tooltip("true = se cogen; false = se sueltan.")]
    public bool coger = true;
    [Tooltip("Lo que tardan las manos en llegar (o en soltarse).")]
    [Min(0f)] public float segundos = 0.35f;

    public override string Describe() => $"De la mano: {guiaId} {(coger ? "coge a" : "suelta a")} {quienId}";
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var guia = ctx.GetActor(guiaId);
        var quien = ctx.GetActor(quienId);
        if (guia?.Transform == null || quien?.Transform == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[DeLaManoBeat] Falta {guiaId} o {quienId} en la escena. La secuencia sigue.");
#endif
            yield break;
        }
        var mano = guia.Transform.GetComponent<DeLaMano>();
        if (coger)
        {
            if (mano == null) mano = guia.Transform.gameObject.AddComponent<DeLaMano>();
            mano.Coger(quien.Transform, segundos);
            ctx.Player?.RegisterCleanup(() => { if (mano != null) mano.Soltar(0f); });
        }
        else if (mano != null) mano.Soltar(segundos);
    }
}
