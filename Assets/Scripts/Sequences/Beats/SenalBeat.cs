using System;
using System.Collections;
using UnityEngine;

/// Emite una señal narrativa en este instante de la secuencia, para que el mundo reaccione dentro
/// del plano (un portal que se abre, una barrera que cae) en vez de al terminar la escena. Lo que
/// cambia lo decide quien consulta la señal (p. ej. ActivoEnTramo), no la secuencia.
[Serializable]
public class SenalBeat : SequenceBeat
{
    [Tooltip("Señal que se emite.")]
    public string senal;

    public override string Describe() => "Señal: " + senal;

    public override bool SoloColoca => true;
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        Emitir("[SenalBeat]");
        yield break;
    }

    /// La señal es un hecho de la historia: se emite también si el jugador salta la escena.
    public override void CumplirAlSaltar(SequenceContext ctx) => Emitir("[SenalBeat] al saltar");

    private void Emitir(string origen)
    {
        if (!string.IsNullOrWhiteSpace(senal))
            DefaultNarrativeSignals.EnsureInstance()?.RaiseCustom(senal, origen);
    }
}
