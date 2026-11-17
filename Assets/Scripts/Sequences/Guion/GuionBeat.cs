using System;
using System.Collections;
using UnityEngine;

/// Reproduce un guion horneado entero (ver GuionHorneado). Es el único beat que necesita una
/// secuencia hecha con guion: el SequencePlayer sigue poniendo lo de siempre (señales, telón,
/// candado de los NPCs, saltar la cinemática) y este beat hace todo lo de dentro.
[Serializable]
public class GuionBeat : SequenceBeat
{
    [Tooltip("El guion ya horneado. Se genera con «El Sendero → Cinemáticas → Guion».")]
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

    /// Segundo por el que empieza el guion. En el juego, 0; en el editor, lo que diga el campo de
    /// pruebas o lo que pida la herramienta que lo lanza (el Director, al probar desde el timeline).
    private float InicioEn(SequencePlayer player)
    {
        if (guion == null) return 0f;
#if UNITY_EDITOR
        if (player != null && player.EmpezarEnOverride.HasValue &&
            (string.IsNullOrEmpty(player.EmpezarEnGuion) || player.EmpezarEnGuion == guion.nombre))
            return Mathf.Clamp(player.EmpezarEnOverride.Value, 0f, guion.duracion);
#endif
        return Inicio;
    }

    public override string Describe()
        => guion == null ? "Guion (sin asignar)" : $"Guion «{guion.nombre}» ({guion.duracion:0.0} s)";

    /// El guion compone la cámara de apertura con su primer plano, para que la escena aparezca ya
    /// encuadrada al abrirse el telón.
    public override bool ColocarCamaraDeApertura(SequencePlayer player)
    {
        var driver = player != null ? player.ActiveCamera : null;
        if (driver == null || guion == null) return false;
        if (!guion.PoseDeCamara(InicioEn(player), out var pos, out var rot, out var fov)) return false;
        driver.Cut(pos, rot, fov);
        return true;
    }

    private ReproductorDeGuion _preparado;

    /// Monta el guion con la pantalla cubierta: deja a los actores en su sitio y con la pose del
    /// segundo inicial, para que la entrada descubra la escena ya compuesta. Reproducir reutiliza
    /// este mismo reproductor en vez de volver a colocar a todo el mundo a la vista.
    public override void Preparar(SequenceContext ctx)
    {
        if (guion == null || ctx == null || _preparado != null) return;
        _preparado = new ReproductorDeGuion(guion, ctx, InicioEn(ctx != null ? ctx.Player : null));
        _preparado.Preparar();
        ctx.Player?.RegisterCleanup(_preparado.Terminar);
    }

    /// Un guion que no ha llegado a empezar cumple lo de todos sus efectos. Si ya estaba montado,
    /// lo cumple su reproductor al cerrarse.
    public override void CumplirAlSaltar(SequenceContext ctx)
    {
        if (_preparado == null) ReproductorDeGuion.CumplirAlSaltar(guion, ctx, 0);
    }

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (guion == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[GuionBeat] No hay guion horneado asignado. Usa «El Sendero → Cinemáticas → Guion».");
#endif
            yield break;
        }

        var reproductor = _preparado;
        _preparado = null;
        if (reproductor == null)
        {
            reproductor = new ReproductorDeGuion(guion, ctx, InicioEn(ctx != null ? ctx.Player : null));
            ctx.Player?.RegisterCleanup(reproductor.Terminar);
        }

        yield return reproductor.Reproducir();
        reproductor.Terminar();
    }
}
