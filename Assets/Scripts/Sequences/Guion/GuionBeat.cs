using System;
using System.Collections;
using UnityEngine;

/// Reproduce un guion horneado entero (ver GuionHorneado). Es el único beat que necesita una
/// secuencia hecha con guion: el SequencePlayer sigue poniendo lo de siempre (señales, telón,
/// candado de los NPCs, saltar la cinemática) y este beat hace todo lo de dentro.
[Serializable]
public class GuionBeat : SequenceBeat
{
    [Tooltip("El guion ya horneado. Se genera con «El Sendero → Guion → Hornear».")]
    public GuionHorneado guion;

#if UNITY_EDITOR
    [Tooltip("Solo para probar en el Editor: empezar en este segundo del guion (0 = desde el " +
             "principio). Lo que ya ha pasado se coloca en su sitio sin reproducirse.")]
    public float empezarEn;
#endif

    /// Segundo en que empieza (0 en el juego; en el Editor, «empezarEn»).
    public float Inicio
    {
        get
        {
#if UNITY_EDITOR
            return guion != null ? Mathf.Clamp(empezarEn, 0f, guion.duracion) : 0f;
#else
            return 0f;
#endif
        }
    }

    public override string Describe()
        => guion == null ? "Guion (sin asignar)" : $"Guion «{guion.nombre}» ({guion.duracion:0.0} s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (guion == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[GuionBeat] No hay guion horneado asignado. Usa «El Sendero → Guion → Hornear».");
#endif
            yield break;
        }

        float inicio = 0f;
#if UNITY_EDITOR
        inicio = Mathf.Clamp(empezarEn, 0f, guion.duracion);
#endif
        var reproductor = new ReproductorDeGuion(guion, ctx, inicio);
        ctx.Player?.RegisterCleanup(reproductor.Terminar);
        yield return reproductor.Reproducir();
        reproductor.Terminar();
    }
}
