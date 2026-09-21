using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Contexto que recibe cada beat al ejecutarse. Le da acceso a los actores (por ID), al escenario
/// (planos de cámara y marcas de posición), y al reproductor (cámara, bocadillos, señales).
public sealed class SequenceContext
{
    public SequencePlayer Player { get; }
    public SequenceStage Stage { get; }

    private readonly Dictionary<string, SequenceActor> _actors = new();

    public SequenceContext(SequencePlayer player, SequenceStage stage)
    {
        Player = player;
        Stage = stage;
    }

    /// Devuelve el actor con ese ID, resolviéndolo la primera vez y cacheándolo después.
    /// Devuelve null (con aviso ya logueado por SequenceActor) si no existe en esta escena.
    public SequenceActor GetActor(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        if (_actors.TryGetValue(actorId, out var cached)) return cached;

        SequenceActor.TryResolve(actorId, out var actor);
        _actors[actorId] = actor; // se cachea incluso si es null: no reintentar cada beat

        // Todo actor que la secuencia toque queda retenido automáticamente en cuanto se resuelve:
        // sale de su comportamiento ambiental (Wander/Idle) y no se pone a andar por su cuenta a
        // mitad de la escena. El SequencePlayer los suelta a todos al terminar. Así ninguna
        // secuencia tiene que acordarse de poner el candado a mano -- que es justo lo que se
        // olvidaba (o se copiaba mal) en los sequencers escritos uno a uno.
        actor?.Hold();

        return actor;
    }

    /// Todos los actores que esta secuencia ha resuelto hasta ahora (puede haber nulos: el
    /// diccionario cachea tambien los que no existen, para no reintentarlos en cada beat).
    ///
    /// Lo usa el solver de camara para no dejar la lente metida DENTRO de un vecino. No le vale
    /// con un raycast: un personaje puede no llevar collider, y el que lo lleva lo tiene en el
    /// cuerpo, no en el pelo -- y en este juego el pelo es media cabeza. Con los actores en la
    /// mano se puede comparar contra su volumen real, que SequenceActor mide de los Renderer.
    public Dictionary<string, SequenceActor>.ValueCollection Actores => _actors.Values;

    /// Da de alta un objeto como actor de la escena, con un nombre. A partir de ese momento
    /// cualquier beat puede referirse a él igual que a un personaje: encuadrarlo, seguirlo con la
    /// cámara, o hacer que alguien lo mire.
    ///
    /// Lo usan los SequenceModule para exponer lo que crean (el proyectil entrante del Despertar
    /// de la Estrella es el caso real). El id conviene que sea descriptivo, porque es lo que se
    /// escribe en el asset.
    ///
    /// 'eyeHeight' es la altura, sobre el pivot del objeto, a la que apuntan los planos: 0 para
    /// algo que ya está a la altura que le toca, como un proyectil en vuelo.
    public void RegisterActor(string id, Transform transform, float eyeHeight = 0f)
    {
        if (string.IsNullOrWhiteSpace(id) || transform == null) return;
        _actors[id] = SequenceActor.ForTransform(id, transform, eyeHeight);

        // Y se guarda CÓMO ESTABA antes de que la secuencia le toque nada. Ver PoseInicial.
        transform.GetPositionAndRotation(out Vector3 pos, out Quaternion rot);
        _poseInicial[id] = (pos, rot);
    }

    // ── Cómo estaba el decorado antes de empezar ─────────────────────────────
    //
    // Un objeto del decorado que la secuencia mueve (la carreta que se vuelca y se endereza, una
    // puerta que se abre) se mueve con DELTAS: "sube metro y medio", "gira 62 grados". Eso está
    // bien para el movimiento, pero es una forma pésima de volver al sitio: el estado final sale
    // de sumar y restar, y basta con que un beat no llegue a ejecutarse —una fase que se salta,
    // un corte, un cambio de montaje— para que el objeto se quede torcido o enterrado para
    // siempre, sin ningún error en consola.
    //
    // Pasó literalmente con la carreta del prólogo: se volcaba al empezar y se enderezaba con el
    // hechizo restando los mismos grados, y acababa tumbada en el suelo el resto del prólogo.
    //
    // Por eso se guarda la pose de cada objeto del escenario tal y como estaba al arrancar. Un
    // PropMoveBeat con 'volverAComoEstaba' no calcula: va EXACTAMENTE a donde estaba, que es lo
    // que el escenario dice que es su sitio (y lo que deja puesto PREPARAR TODO, que es quien lo
    // apoya en el suelo).
    private readonly Dictionary<string, (Vector3 pos, Quaternion rot)> _poseInicial = new();

    /// La posición y rotación que ese objeto tenía cuando empezó la secuencia.
    public bool TryGetPoseInicial(string id, out Vector3 pos, out Quaternion rot)
    {
        if (!string.IsNullOrWhiteSpace(id) && _poseInicial.TryGetValue(id, out var p))
        {
            pos = p.pos; rot = p.rot;
            return true;
        }
        pos = Vector3.zero; rot = Quaternion.identity;
        return false;
    }

    /// Da de baja un objeto registrado — cuando desaparece, para que ningún plano posterior
    /// intente encuadrar algo que ya no existe.
    public void UnregisterActor(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _actors.Remove(id);
    }

    /// ¿Hay ahora mismo un actor dado de alta con este ID? A diferencia de GetActor, no intenta
    /// resolverlo en la escena ni lo retiene: es para preguntar por algo efímero, como un hechizo
    /// en vuelo (SpellBeat.registrarComo), sin efectos secundarios.
    public bool EstaRegistrado(string id)
        => !string.IsNullOrWhiteSpace(id) && _actors.TryGetValue(id, out var a) && a?.Transform != null;

    // ── Eje de acción (la regla de los 180 grados) ───────────────────────────
    //
    // Entre dos personajes que hablan hay una línea imaginaria. Todos los planos de una escena
    // tienen que rodarse del MISMO lado de esa línea: si uno se cruza, el espectador ve a los dos
    // personajes intercambiados de sitio y siente que algo va mal aunque no sepa decir qué. Es la
    // regla de montaje más básica que hay y también la más barata de cumplir — es un signo.
    //
    // El lado se decide UNA vez, en el primer plano calculado de la escena, y lo heredan todos los
    // demás. Y se decide mirando dónde estaba la cámara de juego en ese momento: así el primer
    // corte de la cinemática cae del mismo lado desde el que el jugador venía mirando, y la
    // entrada no se siente como un salto.

    private Vector3 _actionSide;
    private bool _actionSideSet;

    /// Fija el lado del eje a mano, en vez de dejar que lo decida el primer plano.
    ///
    /// Hace falta cuando la cinemática NO ocurre donde está el jugador. El criterio normal
    /// ("el lado desde el que el jugador venía mirando") es el bueno para una escena que arranca
    /// delante de él; pero una secuencia que pasa en otro sitio del mapa — el prólogo, que ocurre
    /// en un valle a seis kilómetros mientras Will duerme — hereda la orientación de una cámara
    /// que no tiene nada que ver, y el resultado es que el mismo montaje sale rodado por un lado o
    /// por el otro según dónde estuviera mirando el jugador al acostarse. Con esto el decorado se
    /// puede construir sabiendo por dónde va a entrar la cámara.
    public void SetActionSide(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        _actionSide = worldDirection.normalized;
        _actionSideSet = true;
    }

    /// El lado del eje desde el que se rueda esta escena, para el par de actores dado.
    /// Devuelve un vector horizontal normalizado, perpendicular a la línea que los une.
    public Vector3 ResolveActionSide(SequenceActor subject, SequenceActor secondary)
    {
        if (subject?.Transform == null) return Vector3.right;

        Vector3 axis = secondary?.Transform != null
            ? secondary.Transform.position - subject.Transform.position
            : subject.Transform.forward;
        axis.y = 0f;

        if (axis.sqrMagnitude < 0.0001f) axis = subject.Transform.forward;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.forward;
        axis.Normalize();

        Vector3 perpendicular = Vector3.Cross(Vector3.up, axis).normalized;

        if (_actionSideSet)
        {
            // Ya hay un lado elegido para esta escena: se conserva. De los dos candidatos se coge
            // el que apunta hacia donde ya estábamos.
            return Vector3.Dot(perpendicular, _actionSide) >= 0f ? perpendicular : -perpendicular;
        }

        // Primer plano de la escena: se elige el lado en el que ya estaba la cámara de juego, para
        // no arrancar la cinemática con un salto.
        Vector3 hint = perpendicular;
        var cam = Player != null ? Player.CachedCamera : null;
        if (cam != null)
        {
            Vector3 midpoint = subject.Transform.position;
            if (secondary?.Transform != null)
                midpoint = (midpoint + secondary.Transform.position) * 0.5f;

            Vector3 toCamera = cam.transform.position - midpoint;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                hint = Vector3.Dot(perpendicular, toCamera) >= 0f ? perpendicular : -perpendicular;
        }

        _actionSide = hint;
        _actionSideSet = true;
        return hint;
    }

    /// Olvida el lado elegido: el siguiente plano vuelve a decidirlo desde cero. Sirve cuando la
    /// escena cambia de sitio o de pareja de personajes y arrastrar el eje anterior ya no
    /// significa nada.
    public void ResetActionAxis() => _actionSideSet = false;

    // ── Último plano montado ─────────────────────────────────────────────────
    //
    // Se guarda solo para poder avisar de dos cortes seguidos que apenas cambian de ángulo ni de
    // tamaño, que al verlos parecen un fallo de montaje. Ver ShotComposer.

    private ShotSolution _previousShot;
    private bool _hasPreviousShot;

    public void RememberShot(ShotSolution shot)
    {
        _previousShot = shot;
        _hasPreviousShot = true;
    }

    public bool TryGetPreviousShot(out ShotSolution shot)
    {
        shot = _previousShot;
        return _hasPreviousShot;
    }

    // ── Marcas (las ramas de la secuencia) ───────────────────────────────────
    //
    // Una marca es un sí/no con nombre que un beat deja puesto y que las fases siguientes pueden
    // mirar para ejecutarse o saltarse. Es lo que permite que una secuencia tenga dos finales —
    // el caso real es el panic input del Despertar de la Estrella, que sale por AWAKEN_DONE o por
    // AWAKEN_FAILED — sin que la SequenceDefinition deje de ser una lista ordenada y legible.
    //
    // Deliberadamente NO es un sistema de condiciones: un solo booleano por nombre, sin
    // operadores ni anidamiento. Una cinemática con lógica de verdad dentro ha dejado de ser una
    // cinemática.

    private readonly Dictionary<string, bool> _flags = new();

    public void SetFlag(string flag, bool value)
    {
        if (string.IsNullOrWhiteSpace(flag)) return;
        _flags[flag.Trim()] = value;
    }

    public bool GetFlag(string flag)
        => !string.IsNullOrWhiteSpace(flag) && _flags.TryGetValue(flag.Trim(), out bool v) && v;

    public bool HasFlag(string flag)
        => !string.IsNullOrWhiteSpace(flag) && _flags.ContainsKey(flag.Trim());

    /// Todos los actores resueltos hasta ahora — para la limpieza final (soltar candados).
    public IEnumerable<SequenceActor> ResolvedActors
    {
        get
        {
            foreach (var kv in _actors)
                if (kv.Value != null) yield return kv.Value;
        }
    }
}

/// Unidad mínima de una secuencia. Cada beat hace UNA cosa y sabe cuándo ha terminado.
///
/// Los beats son clases [Serializable] guardadas con [SerializeReference] dentro de una
/// SequenceDefinition — exactamente la misma técnica que usan los nodos del grafo narrativo
/// (NarrativeNode). Eso hace que el .asset resultante sea texto plano legible y editable sin abrir
/// Unity, que es lo que permite montar una secuencia entera describiéndola por escrito.
[Serializable]
public abstract class SequenceBeat
{
    [Tooltip("Etiqueta opcional para leer la secuencia de un vistazo en el Inspector y en los logs. " +
             "No afecta a nada en tiempo de ejecución.")]
    public string note;

    /// Resumen de una línea para el Inspector y los avisos de consola.
    public abstract string Describe();

    /// Ejecuta el beat. Debe terminar por sí solo: un beat que nunca acaba cuelga la secuencia
    /// entera (y con ella el input del jugador, ya bloqueado por la cinemática).
    public abstract IEnumerator Run(SequenceContext ctx);
}

/// Un tramo con nombre de la secuencia ("Saludo a distancia", "El hechizo fallido"...).
///
/// Las fases no cambian nada en tiempo de ejecución: los beats se ejecutan en orden igual que si
/// fueran una sola lista. Existen para tres cosas prácticas: leer la secuencia de un vistazo,
/// poder arrancar desde una fase concreta al probar (ver SequencePlayer._startAtPhase), y que un
/// aviso de consola diga "falló en la fase 'El hechizo fallido'" en vez de dar un índice suelto.
[Serializable]
public class SequencePhase
{
    [Tooltip("Nombre del tramo. Solo informativo, pero conviene que sea descriptivo: es lo que " +
             "sale en los logs y lo que se usa para arrancar la secuencia por la mitad al probar.")]
    public string name = "Fase";

    [Header("Condición (opcional — para secuencias con más de un final)")]
    [Tooltip("Esta fase solo se ejecuta si la marca con este nombre está puesta. Vacío = se " +
             "ejecuta siempre. Las marcas las dejan los beats de tipo 'Marca' o los módulos de " +
             "mecánica: así una secuencia puede ramificarse (el jugador acertó / falló) sin dejar " +
             "de ser una lista ordenada de fases que se lee de arriba abajo.")]
    public string onlyIfFlag;

    [Tooltip("Esta fase se SALTA si la marca con este nombre está puesta. Es la otra mitad de la " +
             "rama: la fase de 'salió bien' lleva onlyIfFlag, y la de 'salió mal' lleva skipIfFlag " +
             "con la misma marca.")]
    public string skipIfFlag;

    [SerializeReference]
    public List<SequenceBeat> beats = new();

    /// ¿Toca ejecutar esta fase con las marcas que hay puestas ahora mismo?
    public bool ShouldRun(SequenceContext ctx)
    {
        if (ctx == null) return true;
        if (!string.IsNullOrWhiteSpace(onlyIfFlag) && !ctx.GetFlag(onlyIfFlag)) return false;
        if (!string.IsNullOrWhiteSpace(skipIfFlag) && ctx.GetFlag(skipIfFlag)) return false;
        return true;
    }

    /// Por qué se salta, en palabras, para el log. Solo se llama cuando ShouldRun ha dicho que no.
    public string DescribeSkipReason(SequenceContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(onlyIfFlag) && (ctx == null || !ctx.GetFlag(onlyIfFlag)))
            return $"la marca '{onlyIfFlag}' no está puesta";
        return $"la marca '{skipIfFlag}' está puesta";
    }
}
