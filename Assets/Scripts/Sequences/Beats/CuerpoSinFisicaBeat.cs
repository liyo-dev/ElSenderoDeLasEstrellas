using System;
using System.Collections;
using UnityEngine;

/// Deja a un actor sin física (Rigidbody cinemático y sin gravedad) hasta que acaba la secuencia: el
/// guion lo lleva por donde no hay suelo (subido a una silla, cayendo, saliendo de una fuente) sin que
/// la gravedad lo baje ni le acumule velocidad entre fotograma y fotograma. Al terminar o saltar la
/// secuencia se le devuelve la física que tenía, con la velocidad a cero.
[Serializable]
public sealed class CuerpoSinFisicaBeat : SequenceBeat
{
    [Tooltip("Id del actor (Player para el jugador).")]
    public string actorId = SequenceActor.PlayerId;

    public override string Describe() => "Sin física: " + actorId;

    public override bool SoloColoca => true;
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var cuerpo = ctx?.GetActor(actorId)?.Transform;
        if (cuerpo == null || ctx.Player == null || !cuerpo.TryGetComponent<Rigidbody>(out var rb) || rb.isKinematic) yield break;
        bool gravedad = rb.useGravity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        ctx.Player.RegisterCleanup(() =>
        {
            if (rb == null) return;
            rb.isKinematic = false;
            rb.useGravity = gravedad;
            rb.linearVelocity = Vector3.zero;
        });
    }
}
