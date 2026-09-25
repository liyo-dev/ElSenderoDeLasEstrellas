using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sendero.Core.Feedback;

// Beats de puesta en escena: cámara, efectos, tiempo y control de flujo.

/// Corte seco a un plano de cámara del SequenceStage.
[Serializable]
public class CutBeat : SequenceBeat
{
    [Tooltip("Nombre del plano tal como está en el SequenceStage (o el 'label' de su CinematicShot).")]
    public string shotName;

    public override string Describe() => $"Cámara: corte a '{shotName}'";

    public override IEnumerator Run(SequenceContext ctx)
    {
        ctx.Player?.CutTo(shotName);
        yield break;
    }
}

/// Movimiento suave de la cámara hacia otro plano.
[Serializable]
public class MoveCameraBeat : SequenceBeat
{
    [Tooltip("Plano de destino, por su nombre en el SequenceStage.")]
    public string shotName;

    [Tooltip("Duración del movimiento en segundos.")]
    public float duration = 2f;

    [Tooltip("Si está marcado, el beat espera a que la cámara llegue. Si no, la cámara sigue " +
             "moviéndose mientras la secuencia avanza (útil para un travelling de fondo mientras " +
             "alguien habla).")]
    public bool waitForArrival = true;

    public override string Describe() => $"Cámara: mover a '{shotName}' ({duration}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var shot = ctx.Stage != null ? ctx.Stage.GetShot(shotName) : null;
        var driver = ctx.Player?.ActiveCamera;
        if (shot == null || driver == null) yield break;

        driver.MoveTo(shot, duration);
        if (waitForArrival && duration > 0f) yield return new WaitForSeconds(duration);
    }
}

/// Un VFX de un solo uso, siempre por VfxPoolService (regla no negociable de CLAUDE.md § 2:
/// nunca Instantiate + Destroy directo).
[Serializable]
public class VfxBeat : SequenceBeat
{
    [Tooltip("Prefab del efecto. Es la única referencia de asset que este beat necesita.")]
    public GameObject vfxPrefab;

    [Tooltip("Sobre qué actor aparece. Si se deja vacío, se usa 'markName'.")]
    public string atActorId;

    [Tooltip("Sobre qué marca de posición del SequenceStage aparece. Solo si 'atActorId' está vacío.")]
    public string markName;

    [Tooltip("Desplazamiento respecto a ese punto (p. ej. Y = 1 para que salga a la altura del pecho).")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Segundos que vive el efecto antes de volver al pool. 0 = usar la duración completa " +
             "del ParticleSystem del prefab.")]
    public float lifetime = 0f;

    [Tooltip("Segundos a restar de la duración calculada. Sirve para recoger el efecto antes de " +
             "que se vea su cola apagándose pegada al suelo. Solo aplica si 'lifetime' es 0.")]
    public float earlyDespawn = 0f;

    [Tooltip("Que el efecto ACOMPAÑE al actor mientras dura, en vez de quedarse donde él estaba " +
             "al encenderse.\n\n" +
             "Sin esto, el efecto se planta en un punto fijo del mundo. Con un personaje quieto no " +
             "se nota; con uno que salta, vuela o cae, el efecto se queda flotando donde estaba. Es " +
             "«el prefab de la protección se queda muy arriba» (INC-306): el Archimago lanza el " +
             "escudo en el aire y luego baja, y el escudo se queda arriba.\n\n" +
             "Se enciende en lo que va PEGADO a alguien —un escudo, un aura, el hechizo entre las " +
             "manos— y se deja apagado en lo que ocurre en un SITIO aunque se sitúe con un actor: " +
             "el polvo que levanta al despegar tiene que quedarse en el suelo, no subir con él.\n\n" +
             "Solo aplica si hay 'atActorId'.")]
    public bool seguirAlActor = false;

    public override string Describe()
        => $"VFX: {(vfxPrefab != null ? vfxPrefab.name : "SIN ASIGNAR")} en {(string.IsNullOrEmpty(atActorId) ? markName : atActorId)}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (vfxPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[VfxBeat] Sin prefab asignado ({note}) — no hay nada que reproducir.");
#endif
            yield break;
        }

        if (VfxPoolService.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[VfxBeat] VfxPoolService.Instance es null — el pool de VFX no está " +
                "listo en esta escena. ¿Arrancaste desde Start.unity?");
#endif
            yield break;
        }

        if (!TryResolvePoint(ctx, atActorId, markName, out Vector3 point, out Quaternion rotation))
            yield break;

        // A quién acompaña, si es que acompaña a alguien. El pool ya acepta un padre: al devolver
        // la instancia no la desemparenta, pero el siguiente Play la reasigna, así que es un uso
        // contemplado (ver el FIX A3 de VfxPoolService, que ya cubre el caso de morir con padre).
        Transform aQuienSigue = null;
        if (seguirAlActor && !string.IsNullOrEmpty(atActorId))
            aQuienSigue = ctx.GetActor(atActorId)?.Transform;

        float life = lifetime;
        if (life <= 0f)
        {
            var ps = vfxPrefab.GetComponentInChildren<ParticleSystem>(true);
            float full = ps != null ? ps.main.duration : 3f;
            life = Mathf.Max(0.1f, full - Mathf.Max(0f, earlyDespawn));
        }

        VfxPoolService.Instance.Play(vfxPrefab, point + offset, rotation, life, aQuienSigue);
        yield break;
    }

    /// Resuelve un punto del mundo a partir de un actor o de una marca. Compartido con SfxBeat.
    internal static bool TryResolvePoint(SequenceContext ctx, string actorId, string markName,
        out Vector3 point, out Quaternion rotation)
    {
        point = Vector3.zero;
        rotation = Quaternion.identity;

        if (!string.IsNullOrEmpty(actorId))
        {
            var actor = ctx.GetActor(actorId);
            if (actor?.Transform == null) return false;
            point = actor.Transform.position;
            rotation = actor.Transform.rotation;
            return true;
        }

        var mark = ctx.Stage != null ? ctx.Stage.GetMark(markName) : null;
        if (mark == null) return false;
        point = mark.position;
        rotation = mark.rotation;
        return true;
    }
}

/// Un efecto de sonido, por clave del AudioGraphProfile o por clip suelto.
[Serializable]
public class SfxBeat : SequenceBeat
{
    [Tooltip("Clave del evento de sonido en el AudioGraphProfile (p. ej. 'Chest'). Es la forma " +
             "preferida: se escribe como texto, sin arrastrar assets.")]
    public string eventKey;

    [Tooltip("Clip concreto, para cuando el sonido no tiene (todavía) una clave en el perfil. " +
             "Si hay 'eventKey', este campo se ignora.")]
    public AudioClip clip;

    [Tooltip("Dónde suena. Vacío en los dos campos = sonido plano, sin posición en el mundo.")]
    public string atActorId;
    public string markName;

    [Range(0f, 1f)] public float volume = 1f;

    public override string Describe() => $"SFX: {(string.IsNullOrEmpty(eventKey) ? (clip != null ? clip.name : "SIN ASIGNAR") : eventKey)}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (AudioService.Instance == null) yield break;

        bool positioned = VfxBeat.TryResolvePoint(ctx, atActorId, markName, out Vector3 point, out _);

        if (!string.IsNullOrEmpty(eventKey))
        {
            if (positioned) AudioService.Instance.PlaySFX(eventKey, volume, point);
            else AudioService.Instance.PlaySFX(eventKey, volume);
        }
        else if (clip != null)
        {
            if (positioned) AudioService.Instance.PlaySFXAt(clip, point, volume);
            else AudioService.Instance.PlaySFX(clip, volume);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning($"[SfxBeat] Sin eventKey ni clip ({note}) — no hay nada que sonar.");
        }
#endif
        yield break;
    }
}

/// Sacudida de cámara. Para golpes, explosiones y sustos.
[Serializable]
public class ShakeBeat : SequenceBeat
{
    [Tooltip("Intensidad de la sacudida.")]
    public float intensity = 0.3f;

    [Tooltip("Duración en segundos.")]
    public float duration = 0.4f;

    [Tooltip("Si está marcado, el beat espera a que termine la sacudida antes de seguir.")]
    public bool waitForEnd = false;

    public override string Describe() => $"Sacudida de cámara ({intensity}, {duration}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        FeedbackService.CameraShake(intensity, duration);
        if (waitForEnd && duration > 0f) yield return new WaitForSeconds(duration);
    }
}

/// Pausa.
[Serializable]
public class WaitBeat : SequenceBeat
{
    [Tooltip("Segundos de espera.")]
    public float seconds = 1f;

    [Tooltip("Contar en segundos REALES, sin que les afecte la cámara lenta. Importa en las " +
             "secuencias que cambian la velocidad del tiempo: con el tiempo al 20%, una espera " +
             "normal de 2 segundos dura 10 de reloj. Marcarlo cuando lo que se quiere es un ritmo " +
             "fijo (el aire entre dos frases); dejarlo sin marcar cuando la pausa debe estirarse " +
             "con la cámara lenta, como el resto de la acción.")]
    public bool unscaled = false;

    public override string Describe()
        => $"Esperar {seconds}s" + (unscaled ? " (tiempo real)" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (seconds <= 0f) yield break;

        if (unscaled) yield return new WaitForSecondsRealtime(seconds);
        else yield return new WaitForSeconds(seconds);
    }
}

/// Levanta una señal narrativa a mitad de secuencia, sin esperar al final.
///
/// Útil para encadenar: que el grafo empiece a preparar lo siguiente mientras la escena todavía
/// está rodando, o para avisar a otro sistema de que ya puede actuar.
[Serializable]
public class SignalBeat : SequenceBeat
{
    [Tooltip("Nombre de la señal (el mismo que espera un WaitCustomEventNode del grafo).")]
    public string signal;

    public override string Describe() => $"Señal: {signal}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (!string.IsNullOrEmpty(signal)) ctx.Player?.Raise(signal);
        yield break;
    }
}

/// Varios beats A LA VEZ.
///
/// Es la pieza que permite que la escena deje de ser "habla uno, luego el otro". Un Say dentro de
/// un Parallel junto a Gesture y Emotion de OTRO actor es, exactamente, "el que escucha reacciona
/// mientras el otro habla" — que es lo que faltaba en todas las secuencias hasta ahora.
[Serializable]
public class ParallelBeat : SequenceBeat
{
    [Tooltip("Los beats que se lanzan a la vez.")]
    [SerializeReference]
    public List<SequenceBeat> beats = new();

    [Tooltip("Marcado = el beat termina cuando han terminado TODOS. Desmarcado = termina en cuanto " +
             "acaba el primero (los demás siguen corriendo por su cuenta y la secuencia avanza).")]
    public bool waitForAll = true;

    public override string Describe()
        => $"A la vez ({(beats != null ? beats.Count : 0)} beats"
           + (waitForAll ? ", esperar a todos" : ", esperar al primero") + ")";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (beats == null || beats.Count == 0) yield break;

        var runner = ctx.Player;
        if (runner == null) yield break;

        int finished = 0;
        int launched = 0;

        foreach (var beat in beats)
        {
            if (beat == null) continue;
            launched++;

            // Las hijas corren en el reproductor, no dentro de esta corrutina, así que el
            // StopCoroutine del skip global NO las alcanzaría: seguirían vivas en pleno gameplay.
            // Registrándolas, el cierre de la secuencia las corta con todo lo demás. Importa
            // especialmente desde que un Parallel puede llevar dentro un beat de mecánica.
            runner.TrackBackgroundRoutine(runner.StartCoroutine(RunChild(beat, ctx, () => finished++)));
        }

        if (launched == 0) yield break;

        int needed = waitForAll ? launched : 1;
        yield return new WaitUntil(() => finished >= needed);
    }

    private static IEnumerator RunChild(SequenceBeat beat, SequenceContext ctx, Action onDone)
    {
        yield return beat.Run(ctx);
        onDone?.Invoke();
    }
}

/// Varios beats UNO DETRÁS DE OTRO, como un bloque (INC-407). Sirve sobre todo dentro de un
/// ParallelBeat: «mientras corren, espera un segundo y corta a la plaza» es un Wait y un Shot en
/// serie que tienen que ir a la vez que la carrera, y un Parallel solo sabe lanzar cosas a la vez.
[Serializable]
public class SerieBeat : SequenceBeat
{
    [Tooltip("Los beats, en orden.")]
    [SerializeReference]
    public List<SequenceBeat> beats = new();

    public override string Describe() => $"En serie ({(beats != null ? beats.Count : 0)} beats)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (beats == null) yield break;
        foreach (var beat in beats)
            if (beat != null) yield return beat.Run(ctx);
    }
}


/// Cambia la hora del día durante la cinemática: amanece, atardece, anochece.
///
/// No toca la luz a mano: se lo pide al DayNightCycle, que es la única fuente de verdad del sol,
/// el cielo y la niebla de este juego, y que además sabe hacer la transición. Ver
/// CinematicTimeOfDay para por qué no vale con ponerlo en la escena.
///
/// Lo que había antes se restaura solo al terminar la secuencia — también si el jugador se la
/// salta. Una pesadilla no puede dejar el mundo anocheciendo.
[Serializable]
public class TimeOfDayBeat : SequenceBeat
{
    [Tooltip("La hora del día a la que pasa la escena a partir de aquí.")]
    public DayNightCycle.TimeOfDay timeOfDay = DayNightCycle.TimeOfDay.Morning;

    [Tooltip("De golpe (para el primer plano de la secuencia, donde no hay nada que fundir) o con " +
             "la transición del propio ciclo (para que el atardecer entre mientras la escena sigue).")]
    public bool immediate = false;

    [Tooltip("Esperar a que termine la transición antes de seguir con el siguiente beat. Lo normal " +
             "es NO esperar: la luz cambia por debajo mientras los personajes siguen hablando, que " +
             "es justo el efecto que se busca.")]
    public bool waitForTransition = false;

    [Tooltip("Segundos que se espera si 'waitForTransition' está puesto.")]
    public float transitionSeconds = 2f;

    [Tooltip("Marcado, esta es la hora con la que se QUEDA el mundo cuando acabe la cinemática, " +
             "en vez de la que había antes. Para el prólogo: la pesadilla acaba de noche y Will " +
             "se despierta al amanecer, así que el mundo tiene que amanecer con él.")]
    public bool esLaHoraDeVolver = false;

    public override string Describe() => $"Hora del día: {timeOfDay}" + (immediate ? " (de golpe)" : "")
        + (esLaHoraDeVolver ? " (y es con la que se queda el mundo)" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (esLaHoraDeVolver) CinematicTimeOfDay.HoraAlVolver = timeOfDay;
        CinematicTimeOfDay.Apply(timeOfDay, immediate);

        // El cielo se vuelve a contar despues de cada cambio de hora: la franja horaria y el clima
        // se MULTIPLICAN sobre el material, y es esa multiplicacion la que puede dejarlo negro.
        // Con el valor real en el log, se ve en que beat pasa.
        CinematicTimeOfDay.Diagnostico($"tras poner la hora {timeOfDay}", ctx?.Player?.CachedCamera);

        if (waitForTransition && transitionSeconds > 0f)
            yield return new WaitForSeconds(transitionSeconds);
    }
}


/// Enciende o apaga un objeto del decorado dado de alta en el SequenceStage.
///
/// El prólogo lo necesita para el incendio: el valle arde a partir de la fase 3, no antes, y un
/// pueblo en llamas es media docena de sistemas de partículas y otras tantas luces que no tiene
/// sentido tener corriendo durante la escena de la mañana. También sirve para lo contrario —
/// hacer desaparecer el globo cuando el Archimago lo suelta.
///
/// Usa la misma lista de objetos que los planos (SequenceStage.Props), así que un objeto que la
/// cámara ya sabe encuadrar se puede además encender y apagar sin declararlo dos veces.
[Serializable]
public class SetPropActiveBeat : SequenceBeat
{
    [Tooltip("El id del objeto en la lista 'Objetos que la cámara puede encuadrar' del SequenceStage.")]
    public string propId;

    [Tooltip("Encenderlo (marcado) o apagarlo.")]
    public bool active = true;

    public override string Describe() => (active ? "Encender: " : "Apagar: ") + propId;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(propId);
        if (actor?.Transform == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SetPropActiveBeat] No hay ningún objeto '{propId}' en la lista de " +
                "objetos encuadrables del SequenceStage. La secuencia sigue.");
#endif
            yield break;
        }

        var go = actor.Transform.gameObject;
        if (go.activeSelf != active) go.SetActive(active);
        yield break;
    }
}

/// Mueve y/o gira un objeto del decorado, en relativo a donde esté.
///
/// ── Por qué hace falta ────────────────────────────────────────────────────────────────────────
/// Raúl, cuarta grabación del prólogo: «se ve como hace la magia pero no se ve lo que arregla».
/// Y era literal: la carreta volcada estaba modelada DE PIE, y el globo enganchado en el
/// campanario se quedaba enganchado. El Archimago lanzaba un hechizo, salía un destello, y el
/// mundo seguía exactamente igual. Un favor que no cambia nada no es un favor, es un efecto.
///
/// Con esto la carreta se vuelca de verdad al empezar la mañana y se endereza cuando él la
/// levanta, y el globo sube. Va en relativo (deltas) a propósito: así el beat no necesita saber
/// dónde está puesto el objeto en la escena, y mover el decorado no rompe la secuencia.
[Serializable]
public class PropMoveBeat : SequenceBeat
{
    [Tooltip("El id del objeto en la lista 'Objetos que la cámara puede encuadrar' del SequenceStage.")]
    public string propId;

    [Tooltip("Cuánto se desplaza, en metros de mundo, desde donde esté ahora.")]
    public Vector3 deltaPosicion = Vector3.zero;

    [Tooltip("Cuánto gira, en grados, desde como esté ahora. El giro es sobre los ejes del mundo.")]
    public Vector3 deltaRotacion = Vector3.zero;

    [Tooltip("Cuánto tarda. 0 = instantáneo, que es lo que se usa para DEJAR PUESTO el estado " +
             "inicial (la carreta volcada) sin que se vea el movimiento.")]
    public float segundos = 1f;

    [Tooltip("Suavizar la entrada y la salida. Un objeto pesado que se endereza no arranca de " +
             "golpe; una carreta que se levanta con magia, tampoco.")]
    public bool suavizar = true;

    [Tooltip("Esperar a que termine antes de seguir con el beat siguiente. Desmarcado, el " +
             "movimiento sigue de fondo — que es lo que se quiere para el globo, que sube " +
             "mientras la cámara ya está en otro sitio.")]
    public bool esperar = true;

    [Tooltip("CONTAR DESDE COMO ESTABA. Con esto marcado, 'deltaPosicion' y 'deltaRotacion' no " +
             "se suman a donde esté el objeto AHORA, sino a la posición y la rotación que tenía " +
             "cuando empezó la secuencia. Con los dos a cero, el objeto vuelve exactamente a su " +
             "sitio.\n\n" +
             "Es lo que hay que usar para posar algo que se ha levantado. Restar los mismos grados " +
             "que se sumaron parece equivalente y no lo es: el estado final depende de que TODOS " +
             "los beats intermedios se hayan ejecutado, y el día que uno no lo hace el objeto se " +
             "queda torcido el resto de la escena sin un solo error en consola. Así el sitio no se " +
             "calcula: se recuerda.")]
    public bool desdeDondeEstaba = false;

    [Tooltip("APOYADO EN EL SUELO. Después de calcular dónde y cómo queda el objeto, lo sube o lo " +
             "baja lo justo para que su parte más baja toque el suelo que tiene debajo.\n\n" +
             "Hace falta en cuanto hay un GIRO: la carreta gira sobre su pivot, que está en la base, " +
             "así que al volcarla 62 grados media carreta se mete en la tierra — y no hay número " +
             "fijo que lo arregle, porque depende de cómo esté colocada. Esto no adivina: mide.")]
    public bool apoyarEnElSuelo = false;

    public override string Describe()
        => $"Decorado: mover {propId}" + (segundos > 0f ? $" en {segundos:F1}s" : " (al instante)");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(propId);
        if (actor?.Transform == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[PropMoveBeat] No hay ningún objeto '{propId}' en la lista de " +
                "objetos encuadrables del SequenceStage. La secuencia sigue.");
#endif
            yield break;
        }

        var t = actor.Transform;
        Vector3 hasta;
        Quaternion giroHasta;
        if (desdeDondeEstaba && ctx.TryGetPoseInicial(propId, out Vector3 sitio, out Quaternion pose))
        {
            hasta = sitio + deltaPosicion;
            giroHasta = Quaternion.Euler(deltaRotacion) * pose;
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (desdeDondeEstaba)
                Debug.LogWarning($"[PropMoveBeat] '{propId}' quiere contar desde su pose inicial, " +
                    "pero no se guardó (¿no está en la lista de objetos encuadrables del " +
                    "SequenceStage?). Se mueve con los deltas desde donde esté.");
#endif
            hasta = t.position + deltaPosicion;
            giroHasta = Quaternion.Euler(deltaRotacion) * t.rotation;
        }

        if (apoyarEnElSuelo) hasta = ApoyarEnElSuelo(t, hasta, giroHasta);

        IEnumerator rutina = MoverA(t, hasta, giroHasta, Mathf.Max(0f, segundos), suavizar);

        if (esperar) yield return ctx.Player.StartCoroutine(rutina);
        else ctx.Player.StartCoroutine(rutina);
    }

    private static readonly RaycastHit[] s_suelo = new RaycastHit[16];

    /// Devuelve `hasta` corregido en altura para que, con ese giro, la parte más baja del objeto
    /// toque el suelo que tiene debajo. Se coloca un instante en la pose final para medir sus
    /// Renderer (con giro, la caja cambia) y se devuelve a donde estaba: nadie lo ve, es el mismo
    /// fotograma. Una vez por beat, no por frame.
    private static Vector3 ApoyarEnElSuelo(Transform t, Vector3 hasta, Quaternion giroHasta)
    {
        t.GetPositionAndRotation(out Vector3 antes, out Quaternion giroAntes);
        t.SetPositionAndRotation(hasta, giroHasta);

        // Solo el CUERPO: las piezas que le cuelgan como adorno (Carreta_Heno, Carreta_Verdura...)
        // están medio metidas en la hierba a propósito, y si contaran el objeto entero acabaría
        // flotando para que el heno no toque el suelo. Es el mismo fallo que INC-331 en el Editor.
        string prefijoPieza = t.name + "_";
        var renders = t.GetComponentsInChildren<Renderer>();
        bool hay = false;
        Bounds caja = default;
        foreach (var r in renders)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            if (EsPiezaColgada(r.transform, t, prefijoPieza)) continue;
            if (!hay) { caja = r.bounds; hay = true; }
            else caja.Encapsulate(r.bounds);
        }

        t.SetPositionAndRotation(antes, giroAntes);
        if (!hay) return hasta;

        // El suelo, buscado desde encima de la caja y saltándose el propio objeto.
        Vector3 origen = new Vector3(caja.center.x, caja.max.y + 1f, caja.center.z);
        int n = Physics.RaycastNonAlloc(origen, Vector3.down, s_suelo, caja.size.y + 6f,
            ~0, QueryTriggerInteraction.Ignore);

        float suelo = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var c = s_suelo[i].collider;
            if (c == null || c.transform.IsChildOf(t)) continue;
            // Lo que esté por ENCIMA del objeto (una rama, un alero) no es su suelo.
            if (s_suelo[i].point.y > caja.max.y) continue;
            if (s_suelo[i].point.y > suelo) suelo = s_suelo[i].point.y;
        }

        if (float.IsNegativeInfinity(suelo))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[PropMoveBeat] No encuentro suelo debajo de '{t.name}' para apoyarlo. " +
                "Se queda a la altura que dicen los deltas.");
#endif
            return hasta;
        }

        return hasta + Vector3.up * (suelo - caja.min.y);
    }

    private static bool EsPiezaColgada(Transform x, Transform raiz, string prefijo)
    {
        while (x != null && x != raiz)
        {
            if (x.name.StartsWith(prefijo, System.StringComparison.Ordinal)) return true;
            x = x.parent;
        }
        return false;
    }

    private static IEnumerator MoverA(Transform t, Vector3 hasta, Quaternion giroHasta,
        float duracion, bool suave)
    {
        Vector3 desde = t.position;
        Quaternion giroDesde = t.rotation;

        if (duracion <= 0.001f)
        {
            t.SetPositionAndRotation(hasta, giroHasta);
            yield break;
        }

        float pasado = 0f;
        while (pasado < duracion)
        {
            // Sin escalar: un beat de cámara lenta no tiene por qué frenar también el decorado,
            // y cuando sí hace falta ya está el TimeScaleBeat delante.
            pasado += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(pasado / duracion);
            if (suave) k = k * k * (3f - 2f * k);
            t.SetPositionAndRotation(Vector3.Lerp(desde, hasta, k),
                Quaternion.Slerp(giroDesde, giroHasta, k));
            yield return null;
        }

        t.SetPositionAndRotation(hasta, giroHasta);
    }
}
