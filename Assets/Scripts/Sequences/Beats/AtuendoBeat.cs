using System;
using System.Collections;
using UnityEngine;

/// Pone o quita un atuendo de escena (<see cref="AtuendoDeEscena"/>) en un instante de la secuencia:
/// un accesorio que se pierde en una caída, una prenda que se tuerce. Lo pone con una clave de esta
/// secuencia y lo quita al terminar (también si se salta): lo que viste un guion no sale de la
/// cinemática. Para ropa que dura un tramo de la historia, <see cref="AtuendoEnTramo"/>.
[Serializable]
public sealed class AtuendoBeat : SequenceBeat
{
    [Tooltip("Atuendo que se pone o se quita.")]
    public AtuendoDeEscena atuendo;

    [Tooltip("Marcado, se pone; desmarcado, se quita el que puso esta secuencia.")]
    public bool poner = true;

    public override string Describe()
        => (poner ? "Atuendo: poner " : "Atuendo: quitar ") + (atuendo != null ? atuendo.name : "(ninguno)");

    public override bool SoloColoca => true;
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var registro = CharacterAppearanceRegistry.Instance;
        if (registro == null || atuendo == null || ctx?.Player == null) yield break;
        string clave = $"secuencia:{ctx.Player.GetEntityId()}:{atuendo.name}";
        if (!poner)
        {
            registro.QuitarAtuendo(clave);
            yield break;
        }
        registro.PonerAtuendo(clave, atuendo);
        ctx.Player.RegisterCleanup(() => CharacterAppearanceRegistry.Instance?.QuitarAtuendo(clave));
    }
}
