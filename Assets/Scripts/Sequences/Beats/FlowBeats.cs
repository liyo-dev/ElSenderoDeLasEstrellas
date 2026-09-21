using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

// Beats de control de la escena: tiempo, pantalla, ramas y enganche con la mecánica de juego.
//
// Todos son genéricos a propósito. La regla para añadir algo a este archivo es que sirva a
// cualquier secuencia futura; lo que solo vale para UNA escena concreta (un panic input, un
// proyectil con reglas propias) no va aquí, va en un SequenceModule de esa escena.

/// Cambia la velocidad del tiempo del juego: cámara lenta y vuelta a la normalidad.
///
/// El SequencePlayer restaura el tiempo a 1 al terminar la secuencia y también si se salta, así
/// que no hay forma de dejar la partida en cámara lenta por olvidar el beat de vuelta — que es
/// exactamente el tipo de error que este sistema existe para hacer imposible.
[Serializable]
public class TimeScaleBeat : SequenceBeat
{
    [Tooltip("Velocidad del tiempo. 1 = normal, 0,2 = cámara lenta marcada, 0 = congelado " +
             "(cuidado: con 0 cualquier espera por tiempo escalado no avanza nunca).")]
    [Range(0f, 2f)]
    public float timeScale = 1f;

    [Tooltip("Segundos REALES que tarda en llegar a esa velocidad. 0 = de golpe. Una rampa de " +
             "medio segundo al entrar y de casi uno al salir es lo que hace que la cámara lenta " +
             "se sienta como un efecto y no como un tirón.")]
    public float rampDuration = 0f;

    public override string Describe()
        => $"Tiempo: ×{timeScale}" + (rampDuration > 0f ? $" en {rampDuration}s" : " (de golpe)");

    public override IEnumerator Run(SequenceContext ctx)
    {
        float target = Mathf.Clamp(timeScale, 0f, 2f);

        if (rampDuration <= 0f)
        {
            Time.timeScale = target;
            yield break;
        }

        float start = Time.timeScale;
        float elapsed = 0f;
        while (elapsed < rampDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(start, target, elapsed / rampDuration);
            yield return null;
        }
        Time.timeScale = target;
    }
}

/// Cubre la pantalla de un color, o la descubre.
///
/// Se usa para tapar un cambio que no debe verse (mover a alguien de sitio, aparecer un objeto) y
/// para los remates a negro. Las transiciones de ENTRADA y SALIDA de la secuencia ya las hace el
/// SequencePlayer: este beat es para los fundidos de en medio.
[Serializable]
public class ScreenFadeBeat : SequenceBeat
{
    [Tooltip("Marcado = la pantalla se cubre. Desmarcado = se descubre.")]
    public bool fadeIn = true;

    [Tooltip("Color con el que se cubre. Negro para casi todo; blanco para un fogonazo largo.")]
    public Color color = Color.black;

    [Tooltip("Duración en segundos reales.")]
    public float duration = 0.25f;

    [Tooltip("Esperar a que termine antes de seguir. Desmarcarlo sirve para que la escena siga " +
             "mientras la pantalla se va cubriendo.")]
    public bool waitForEnd = true;

    public override string Describe()
        => (fadeIn ? "Pantalla: cubrir" : "Pantalla: descubrir") + $" ({duration}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var routine = FeedbackService.ScreenFadeAsync(color, duration, fadeIn);
        if (waitForEnd)
        {
            yield return routine;
            yield break;
        }

        // Registrada, como las demás corrutinas sueltas del sistema. Sin esto, un fundido lanzado
        // "sin esperar" sobrevive al cierre y sigue subiendo el alpha mientras el cierre intenta
        // descubrir la pantalla: gana el que escriba último y la pantalla se puede quedar en negro
        // para siempre, que es literalmente el INC-208 que este sistema existe para no repetir.
        var player = ctx?.Player;
        if (player != null) player.TrackBackgroundRoutine(player.StartCoroutine(routine));
    }
}

/// Fogonazo: la pantalla se tiñe de un color y se va sola. Para explosiones e impactos.
[Serializable]
public class ScreenFlashBeat : SequenceBeat
{
    public Color color = new(1f, 0.6f, 0.2f, 1f);

    [Tooltip("Duración en segundos.")]
    public float duration = 0.25f;

    public override string Describe() => $"Fogonazo ({duration}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        FeedbackService.ScreenFlash(color, duration);
        yield break;
    }
}

/// Deja puesta una marca con nombre, que las fases siguientes pueden mirar para ejecutarse o
/// saltarse (ver SequencePhase.onlyIfFlag / skipIfFlag).
///
/// Es lo que permite que una secuencia tenga dos finales sin dejar de ser una lista ordenada de
/// fases. Normalmente no hace falta ponerlo a mano: quien deja la marca es el beat que resuelve
/// algo — un SequenceModule que devuelve si el jugador acertó, por ejemplo.
[Serializable]
public class SetFlagBeat : SequenceBeat
{
    [Tooltip("Nombre de la marca (p. ej. 'panicSuperado').")]
    public string flag;

    [Tooltip("Valor que se le deja.")]
    public bool value = true;

    public override string Describe() => $"Marca: {flag} = {(value ? "sí" : "no")}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        ctx?.SetFlag(flag, value);
        yield break;
    }
}

/// Termina la secuencia AQUÍ, saltándose todas las fases que queden.
///
/// Para una rama que acaba antes que la otra. El cierre normal (soltar actores, restaurar el
/// tiempo y la música, levantar la señal de salida) ocurre igual: esto adelanta el final, no se
/// lo salta.
[Serializable]
public class EndSequenceBeat : SequenceBeat
{
    [Tooltip("Señal que se levanta al cerrar, en vez de la señal de salida normal del asset. " +
             "Vacío = la normal. Es lo que permite que una secuencia con dos finales mande al " +
             "grafo por una rama o por la otra.")]
    public string signalOutOverride;

    [Tooltip("Cómo queda la pantalla al cerrar por aquí. 'Como el asset' usa lo que diga la " +
             "definición; las otras dos lo fuerzan. Hace falta cuando las dos ramas de una " +
             "secuencia terminan distinto: la que encadena con otra cinemática se queda en negro " +
             "y deja que la siguiente revele, y la que devuelve el control al jugador descubre.")]
    public SequenceEndScreen screen = SequenceEndScreen.ComoElAsset;

    public override string Describe()
        => "Terminar la secuencia" + (string.IsNullOrEmpty(signalOutOverride) ? "" : $" → {signalOutOverride}");

    public override IEnumerator Run(SequenceContext ctx)
    {
        ctx?.Player?.RequestEnd(signalOutOverride, screen);
        yield break;
    }
}

/// Cómo queda la pantalla cuando una secuencia cierra.
public enum SequenceEndScreen
{
    /// Lo que diga el campo 'endStayBlack' de la SequenceDefinition.
    ComoElAsset = 0,

    /// Descubrir la pantalla y devolver el control. Es lo normal.
    Revelar = 1,

    /// Dejarla cubierta: lo que venga detrás traerá su propia entrada.
    QuedarseEnNegro = 2,
}

/// Cambia la música a mitad de secuencia, o la para.
///
/// La música de la secuencia entera ya la pone el asset (campo 'musicId') y se restaura sola al
/// terminar. Este beat es para los cambios de en medio: parar la música antes de un silencio
/// dramático, o pasar a otra pieza cuando la escena gira.
[Serializable]
public class MusicBeat : SequenceBeat
{
    [Tooltip("ID de regla de secuencia del AudioGraphProfile. Vacío = PARAR la música.")]
    public string musicId;

    [Tooltip("Segundos de desvanecido al parar. Solo se usa cuando 'musicId' está vacío.")]
    public float fadeOut = 0.5f;

    public override string Describe()
        => string.IsNullOrEmpty(musicId) ? $"Música: parar ({fadeOut}s)" : $"Música: {musicId}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var player = ctx?.Player;
        if (player == null) yield break;

        if (string.IsNullOrWhiteSpace(musicId)) player.StopMusicNow(fadeOut);
        else player.PlayMusic(musicId);

        yield break;
    }
}

/// Ejecuta una rutina con nombre de un SequenceModule de la escena.
///
/// ── Por qué existe, y cuándo NO usarlo ────────────────────────────────────────────────────────
/// El catálogo de beats cubre lo que hacen todas las secuencias: hablar, gesticular, moverse,
/// cortar de plano, lanzar un efecto. Pero alguna escena tiene mecánica de JUEGO que solo existe
/// ahí — el panic input del Despertar de la Estrella, con su proyectil y su contraataque, es el
/// único caso real hoy. Meter eso en el catálogo común significaría cargar para siempre con seis
/// beats que usa una sola escena.
///
/// Este beat es la salida para ese caso: la mecánica vive en un componente de la escena y el asset
/// la invoca por nombre, sin dejar de mandar en el montaje (cámaras, diálogo, ritmo), que es lo
/// que de verdad se va a querer retocar.
///
/// El límite es importante: un módulo implementa MECÁNICA, nunca puesta en escena. En el momento
/// en que un módulo mueva la cámara, mueva a un NPC o eche un candado por su cuenta, ha vuelto el
/// problema que este sistema venía a resolver — y esa es, literalmente, la historia de cómo nació
/// Co_RunTo. Si falta un beat, la respuesta es añadir el beat.
[Serializable]
public class ModuleBeat : SequenceBeat
{
    [Tooltip("Nombre de la rutina, tal como la anuncia el módulo (p. ej. 'LanzarProyectil').")]
    public string routine;

    [Tooltip("Esperar a que la rutina termine antes de seguir. Desmarcado, arranca y la secuencia " +
             "sigue — para algo que debe correr de fondo durante varias fases.")]
    public bool waitForEnd = true;

    public override string Describe()
        => $"Mecánica: {routine}" + (waitForEnd ? "" : " (de fondo)");

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (string.IsNullOrWhiteSpace(routine)) yield break;

        var module = ctx?.Stage != null ? ctx.Stage.GetModuleFor(routine) : null;
        if (module == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ModuleBeat] Ningún módulo de esta escena sabe ejecutar la rutina " +
                $"'{routine}'. Comprueba que el SequenceModule está en la lista del SequenceStage y " +
                "que el nombre coincide con los que anuncia.");
#endif
            yield break;
        }

        var body = module.Run(routine, ctx);
        if (body == null) yield break;

        if (waitForEnd) yield return body;
        else ctx.Player.TrackBackgroundRoutine(ctx.Player.StartCoroutine(body));
    }
}
