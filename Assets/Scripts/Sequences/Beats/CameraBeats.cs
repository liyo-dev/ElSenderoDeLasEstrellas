using System;
using System.Collections;
using UnityEngine;

// Beats de cámara con plano CALCULADO.
//
// CutBeat y MoveCameraBeat (en StageBeats.cs) siguen existiendo y siguen funcionando: cortan a un
// plano que alguien colocó a mano en el SequenceStage. Este archivo añade lo otro — describir el
// plano en vez de colocarlo — y las dos formas conviven a propósito:
//
//   · Los planos DE AUTOR (el general bonito del pueblo, el del despertar de la estrella) se
//     colocan a mano. Son pocos, son los que dan carácter, y no se rompen porque miran al
//     paisaje, no a un personaje.
//   · Los planos DE CONVERSACIÓN Y REACCIÓN — el 80% del metraje, y el 100% de lo que se rompe
//     cuando un NPC deriva medio metro o el jugador entra por otro lado — se calculan.

/// Un plano de cámara descrito en vez de colocado.
///
/// Es el beat de cámara que conviene usar por defecto. En vez de apuntar a un GameObject de la
/// escena, dice qué se quiere ver ("primer plano de Oliver, con Will como referencia") y
/// ShotComposer lo resuelve con los actores donde estén en ese momento. No hay nada que colocar,
/// y nada que recolocar cuando la escena cambia.
[Serializable]
public class ShotBeat : SequenceBeat
{
    [Tooltip("Plano colocado a mano, por su nombre en el SequenceStage. Si se rellena, manda sobre " +
             "'framing' y este beat se comporta igual que un CutBeat de toda la vida. Dejarlo vacío " +
             "es lo normal.")]
    public string shotName;

    [Tooltip("La descripción del plano, para cuando no hay nombre: qué tipo de plano y sobre quién.")]
    public ShotFraming framing = new();

    [Header("Cómo se llega")]
    [Tooltip("Marcado = la cámara viaja hasta el plano nuevo. Desmarcado = corte seco. El corte es " +
             "lo normal en una conversación; el movimiento se guarda para cuando el propio " +
             "desplazamiento significa algo (revelar lo que hay detrás, acompañar a alguien).")]
    public bool smooth = false;

    [Tooltip("Duración del movimiento, en segundos reales. Solo si 'smooth' está marcado.")]
    public float duration = 1.4f;

    [Tooltip("Esperar a que la cámara llegue antes de seguir con el beat siguiente. Desmarcado, " +
             "la cámara sigue viajando mientras la escena avanza — útil para un travelling de " +
             "fondo mientras alguien habla.")]
    public bool waitForArrival = true;

    [Tooltip("Solo con 'smooth'. Marcado, la cámara sale ya en marcha desde el primer fotograma " +
             "y solo frena al llegar, en vez de arrancar despacio. Es para una apertura que viene " +
             "bajando desde el cielo (INC-395): con el arranque suave, el primer segundo de un " +
             "travelling largo parece un plano quieto.")]
    public bool arrancaLanzado = false;

    [Tooltip("Solo con 'smooth'. Si la línea recta hasta el plano atraviesa el decorado, en vez " +
             "de hacer corte seco la cámara entra DESDE ARRIBA: una curva que pasa por encima " +
             "del destino y cae sobre él. Para aperturas que bajan del cielo a la plaza (INC-397).")]
    public bool entrarDesdeArriba = false;

    [Header("Sujetos en movimiento")]
    [Tooltip("Recalcula el plano CADA FRAME, para seguir a alguien que se mueve (un personaje " +
             "corriendo, un proyectil en vuelo). Se mantiene hasta el siguiente beat de cámara o " +
             "hasta el final de la secuencia. Para un plano de gente parada NO hace falta: gasta " +
             "de más y puede temblar si el actor se mueve poco.")]
    public bool live = false;

    public override string Describe()
        => "Plano: " + (string.IsNullOrWhiteSpace(shotName)
            ? framing?.Describe() ?? "SIN DESCRIBIR"
            : $"'{shotName}' (colocado a mano)")
           + (live ? " [siguiendo]" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var player = ctx?.Player;
        if (player == null) yield break;

        // Cualquier plano nuevo cancela el seguimiento del anterior: si no, dos planos 'live'
        // seguidos pelearían por la cámara cada frame.
        player.StopShotTracking();

        var driver = player.ActiveCamera;
        if (driver == null) yield break;

        // Camino A: plano colocado a mano. Se comporta igual que siempre.
        if (!string.IsNullOrWhiteSpace(shotName))
        {
            var shot = ctx.Stage != null ? ctx.Stage.GetShot(shotName) : null;
            if (shot == null) yield break; // el aviso ya lo ha dado el stage

            if (smooth)
            {
                driver.MoveTo(shot, duration);
                if (waitForArrival && duration > 0f)
                    yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                driver.Cut(shot);
            }
            yield break;
        }

        // Camino B: plano calculado.
        player.NotifyCurrentShot(framing, framing.Describe());

        // ── Encarar ANTES de resolver ────────────────────────────────────────────────────────
        //
        // El encuadre de tres cuartos —que es el de casi todos los planos— coloca la cámara hacia
        // el interlocutor dando por hecho que el sujeto lo está mirando. Nadie garantizaba esa
        // suposición: `PlaceAtMarkBeat` hereda la rotación de la marca (y las marcas se crean sin
        // rotación, o sea mirando al +Z del mundo), `WalkPathBeat` deja mirando en la dirección de
        // avance, y `SayBeat` no gira a nadie. Contra 74 planos había 21 `FaceBeat` sueltos que la
        // siguiente caminata pisaba. Resultado: espaldas, perfiles, y la conversación del río con
        // los dos hombro con hombro mirando al frente (INC-293).
        //
        // Ponerlo aquí lo arregla de raíz en vez de a parches: el plano YA declara quién mira a
        // quién en `subjectId` y `secondaryId`, así que se aplica esa declaración y la suposición
        // pasa a ser verdadera por construcción.
        //
        // El giro va antes de resolver a propósito: si fuera después, el solver habría calculado
        // la posición con la orientación vieja.
        yield return Co_Encarar(ctx);

        if (!ShotComposer.TrySolve(ctx, framing, player.CameraAspect, out ShotSolution shotSolution))
            yield break; // el aviso ya lo ha dado el composer; la cámara se queda donde estaba

        // Un movimiento de cámara solo es posible si el camino está despejado. Entre dos planos
        // lejanos del mismo pueblo casi nunca lo está: en la grabación del 19 sep, el travelling
        // de la despedida (segundo 0:51) sale del plano anterior, atraviesa el suelo, pasa por
        // dentro de una carreta y aterriza en el plano siguiente. Un corte no puede atravesar
        // nada, así que cuando el trayecto choca, esto deja de ser un movimiento y pasa a ser un
        // corte — que además es lo que pide una conversación.
        bool viaja = smooth && ShotComposer.IsPathClear(driver.CurrentPosition, shotSolution.position);

        // Entrar desde arriba (INC-397). En la grabación del 24 sep la bajada de la apertura se
        // quedó en corte seco: la recta desde el cielo hasta el Archimago rozaba un tejado. Una
        // bajada no tiene por qué ser recta — se prueba una curva que pasa por encima del
        // destino y cae sobre él, que además es como se «entra» en una escena.
        Vector3? porEncima = null;
        if (smooth && !viaja && entrarDesdeArriba)
        {
            porEncima = BuscarCurvaPorArriba(driver.CurrentPosition, shotSolution);
            viaja = porEncima.HasValue;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (smooth && !viaja)
            Debug.Log($"[ShotBeat] El movimiento hasta '{framing.Describe()}' atravesaría el " +
                "escenario, así que se hace corte seco. Si el movimiento importaba, hay que " +
                "acercar los dos planos o poner uno intermedio.");
        else if (porEncima.HasValue)
            Debug.Log($"[ShotBeat] La recta hasta '{framing.Describe()}' chocaba: se entra desde " +
                      $"arriba, pasando por {porEncima.Value.ToString("F1")}.");
#endif

        if (viaja)
        {
            driver.MoveTo(shotSolution.position, shotSolution.rotation, shotSolution.fieldOfView, duration,
                arrancaLanzado, porEncima);
            if (waitForArrival && duration > 0f)
                yield return new WaitForSecondsRealtime(duration);
        }
        else
        {
            driver.Cut(shotSolution.position, shotSolution.rotation, shotSolution.fieldOfView);
        }

        // 'live' es la intención del plano; LivePreviewEnabled es el modo de ajuste, que refresca
        // TODOS los planos para poder ver el efecto del deslizador de distancia sin recompilar ni
        // volver a lanzar la secuencia.
        if (live || player.LivePreviewEnabled) player.StartShotTracking(framing);
    }

    /// Un punto de control para una curva que llega al plano desde arriba, o null si ninguna
    /// de las candidatas está despejada. La curva es una Bézier cuadrática (la misma que dibuja
    /// el driver), y se comprueba tramo a tramo contra el decorado.
    private static Vector3? BuscarCurvaPorArriba(Vector3 desde, ShotSolution destino)
    {
        Vector3 hasta = destino.position;
        float alto = Mathf.Max(desde.y, hasta.y + 6f);
        Vector3 atras = destino.rotation * Vector3.back;
        atras.y = 0f;
        atras = atras.sqrMagnitude > 0.001f ? atras.normalized : Vector3.zero;

        // 1) Justo encima del destino: la cámara llega por el cielo y cae en vertical.
        // 2) Encima y un poco por detrás: cae siguiendo la dirección en la que mira el plano.
        // 3) Lo mismo, más alto.
        Vector3[] candidatas =
        {
            new Vector3(hasta.x, alto, hasta.z),
            new Vector3(hasta.x, alto, hasta.z) + atras * 6f,
            new Vector3(hasta.x, alto + 8f, hasta.z) + atras * 3f,
        };

        foreach (var c in candidatas)
            if (CurvaDespejada(desde, c, hasta)) return c;

        return null;
    }

    private static bool CurvaDespejada(Vector3 a, Vector3 c, Vector3 b)
    {
        const int tramos = 16;
        Vector3 previo = a;
        for (int i = 1; i <= tramos; i++)
        {
            float t = i / (float)tramos;
            float u = 1f - t;
            Vector3 punto = u * u * a + 2f * u * t * c + t * t * b;
            if (!ShotComposer.IsPathClear(previo, punto)) return false;
            previo = punto;
        }
        return true;
    }

    /// Gira al sujeto hacia el secundario, si el encuadre lo pide y tiene a quién mirar.
    ///
    /// Con corte seco el giro es instantáneo: la cámara cambia de sitio en el mismo fotograma, así
    /// que nadie ve el giro. Con movimiento de cámara se hace en tres décimas, porque ahí sí se
    /// ve y un personaje que cambia de orientación de golpe delante del espectador se lee como un
    /// fallo. Es el mismo criterio que ya usa `FaceBeat.turnDuration`.
    private IEnumerator Co_Encarar(SequenceContext ctx)
    {
        if (framing == null || !framing.DebeEncarar) yield break;

        var sujeto = ctx.GetActor(framing.subjectId);
        var secundario = ctx.GetActor(framing.secondaryId);
        if (sujeto?.Transform == null || secundario?.Transform == null) yield break;

        Vector3 destino = secundario.Transform.position;

        // Si el secundario está casi encima (o casi debajo), no hay hacia dónde girar: al aplanar
        // la dirección queda un vector diminuto y el personaje acaba mirando a cualquier sitio.
        // Es lo que hacía el Archimago en la sexta grabación — lanzaba el hechizo mirando al globo,
        // que es lo correcto, y se giraba hacia la nada para decir «con cuidado», porque el globo
        // estaba ya veinte metros más arriba. Un plano contra algo que está en vertical no pide
        // ningún giro: el personaje ya está mirando donde tiene que mirar.
        Vector3 enPlano = destino - sujeto.Transform.position;
        float desnivel = Mathf.Abs(enPlano.y);
        enPlano.y = 0f;

        // (21 sep) Antes esto era `< 1,5 m` a secas, y se comía justo el caso más común de todos:
        // dos personajes hablando a metro y pico. Ahí NO se giraba nadie, y el plano de tres
        // cuartos se calculaba contra un personaje que miraba hacia donde fuera — la espalda de
        // Oliver en «ay, que me ahogo» (llega corriendo y se para a 1,2 m de Will), el Archimago
        // de espaldas en la despedida. La guarda es para lo que está ENCIMA, así que ahora exige
        // las dos cosas: estar cerca en planta Y a otra altura. A la misma altura, basta con que
        // no esté literalmente pegado.
        bool encima = enPlano.magnitude < 1.5f && desnivel > 1.2f;
        if (encima || enPlano.magnitude < 0.3f) yield break;

        if (!smooth)
        {
            sujeto.Face(destino);
            yield break;
        }

        Vector3 dir = destino - sujeto.Transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Quaternion desde = sujeto.Transform.rotation;
        Quaternion hasta = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // Si ya está prácticamente mirando, no se toca: un slerp de dos grados es ruido.
        if (Quaternion.Angle(desde, hasta) < 5f) yield break;

        const float giro = 0.3f;
        float t = 0f;
        while (t < giro)
        {
            if (sujeto.Transform == null) yield break;
            t += Time.unscaledDeltaTime;
            sujeto.Transform.rotation = Quaternion.Slerp(desde, hasta, Mathf.Clamp01(t / giro));
            sujeto.SyncRotation();
            yield return null;
        }

        sujeto.Transform.rotation = hasta;
        sujeto.SyncRotation();
    }
}

/// Deja de seguir a un sujeto en movimiento, congelando la cámara donde esté.
///
/// Normalmente no hace falta: el siguiente beat de cámara ya corta el seguimiento, y el final de
/// la secuencia también. Existe para cuando se quiere que la cámara se quede quieta un rato y lo
/// siguiente que pase no sea un plano.
[Serializable]
public class StopTrackingBeat : SequenceBeat
{
    public override string Describe() => "Cámara: dejar de seguir";

    public override IEnumerator Run(SequenceContext ctx)
    {
        ctx?.Player?.StopShotTracking();
        yield break;
    }
}

/// Olvida el lado del eje de acción elegido, para que el próximo plano vuelva a decidirlo.
///
/// La regla de los 180 grados dice que todos los planos de una escena se ruedan del mismo lado de
/// la línea que une a los personajes, y el sistema la cumple solo. Pero cuando la escena se muda
/// de sitio — cambio de localización, o entra otra pareja de personajes a hablar — arrastrar el
/// eje anterior deja de tener sentido: ya no es la misma escena. Poner este beat en ese punto es
/// el equivalente de decir "aquí empieza una secuencia nueva".
///
/// Si se usa en mitad de una conversación, el resultado es justo el fallo que la regla evita: los
/// personajes parecerán haberse intercambiado el sitio.
/// Fija el lado del eje de acción a mano, con una dirección del mundo.
///
/// Por defecto el lado lo decide el primer plano mirando dónde estaba la cámara de juego, que es
/// lo correcto para una escena que arranca delante del jugador. Para una secuencia que ocurre en
/// OTRO sitio del mapa (el prólogo pasa en un valle mientras Will duerme en su cama) ese criterio
/// no significa nada: el montaje saldría rodado por un lado o por el otro según hacia dónde
/// estuviera mirando el jugador al acostarse. Con este beat, puesto antes del primer plano, el
/// decorado se puede construir sabiendo por dónde va a entrar la cámara.
[Serializable]
public class SetActionAxisBeat : SequenceBeat
{
    [Tooltip("Dirección del mundo, en grados, del lado desde el que se rueda la escena: 0 = +Z, " +
             "90 = +X, 180 = -Z, 270 = -X. Es de qué lado de la línea que une a los personajes se " +
             "pone la cámara.")]
    [Range(0f, 360f)]
    public float sideDegrees = 0f;

    public override string Describe() => $"Cámara: fijar el eje de acción a {sideDegrees:F0}°";

    public override IEnumerator Run(SequenceContext ctx)
    {
        float rad = sideDegrees * Mathf.Deg2Rad;
        ctx?.SetActionSide(new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)));
        yield break;
    }
}

[Serializable]
public class ResetActionAxisBeat : SequenceBeat
{
    public override string Describe() => "Cámara: reiniciar el eje de acción";

    public override IEnumerator Run(SequenceContext ctx)
    {
        ctx?.ResetActionAxis();
        yield break;
    }
}
