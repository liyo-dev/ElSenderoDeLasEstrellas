using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Beats que tocan el MUNDO durante una cinemática: el tiempo atmosférico y el movimiento de un
// actor por sitios donde no hay NavMesh. Los dos nacen del storyboard del prólogo del 19 sep 2026
// («algo cambia en el cielo» y «la llegada»), pero ninguno es específico de esa escena.

/// Enciende o apaga un fenómeno atmosférico mientras dura la cinemática.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El sistema de tormenta, viento, lluvia y niebla ya estaba entero en DayNightCycle — con su
/// destello de rayo, su oscurecimiento y su transición. Lo único que faltaba era poder pedírselo
/// desde una secuencia. Esto es esa llamada, y nada más: no reimplementa nada.
///
/// ── Y por qué se restaura solo ────────────────────────────────────────────────────────────────
/// Por el mismo motivo que CinematicTimeOfDay: una pesadilla no puede dejar el mundo con tormenta.
/// El estado previo se guarda la primera vez que un beat de estos toca el tiempo, y lo devuelve
/// CinematicTimeOfDay.Restore(), al que ya llama el SequencePlayer al terminar, al saltar y al
/// fallar — nunca un beat final, porque un beat final no se ejecuta cuando el jugador se salta la
/// escena.
/// Dos nubes que se abren DELANTE DE LA LENTE (INC-373).
///
/// «El prólogo abre con una aérea y quiero meter las nubes abriéndose.» El primer intento fueron
/// dos nubes colocadas en la escena sobre la plaza, y no se vieron: la cámara del plano la resuelve
/// el buscador, así que dónde cae exactamente no lo sabe nadie de antemano y cualquier coordenada
/// escrita a mano es una apuesta.
///
/// Esto no apuesta: las cuelga de la PROPIA CÁMARA de la cinemática, a los metros que se le digan
/// por delante, una a cada lado. Estén donde estén los personajes y resuelva el plano donde
/// resuelva, las nubes tapan el cuadro y se abren. Se destruyen solas al terminar.
[Serializable]
public class NubesDeAperturaBeat : SequenceBeat
{
    [Tooltip("Prefab de nube. Se instancian dos, una a cada lado.")]
    public GameObject nube;

    [Tooltip("Metros por delante de la lente.")]
    public float distancia = 6f;

    [Tooltip("Separación inicial respecto al centro del cuadro, en metros. Pequeña: tienen que " +
             "taparlo casi todo al empezar.")]
    public float separacionInicial = 2.2f;

    [Tooltip("Separación final: a dónde se van al abrirse.")]
    public float separacionFinal = 16f;

    [Tooltip("Escala de cada nube. Solo se usa si 'escalaAutomatica' está desmarcado.")]
    public float escala = 7f;

    [Tooltip("Calcular el tamaño y la separación a partir de lo que de verdad SE VE por la lente " +
             "(el campo de visión de la cámara y el tamaño real del prefab), en vez de fiarse de " +
             "los números de arriba. Con esto, dos nubes tapan la pantalla entera se use el prefab " +
             "que se use.")]
    public bool escalaAutomatica = true;

    [Tooltip("Segundos que tarda en abrirse.")]
    public float segundos = 3.4f;

    [Tooltip("Esperar a que acabe de abrirse antes de seguir. Desmarcado, la escena sigue " +
             "mientras se abren, que es lo normal: la cámara ya está bajando.")]
    public bool esperar = false;

    public override string Describe() => $"Nubes que se abren ({segundos:F1}s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        // LA CÁMARA DE VERDAD (INC-385). `ActiveCamera` es el DRIVER, y su transform es el
        // GameObject del sequencer — que vive en MainWorld. Colgadas de ahí, las dos nubes
        // aparecían flotando en la aldea, a cuatro metros de un objeto que no es la lente: es
        // literalmente lo que dijo Raúl («las nubes del principio creo que están saliendo en
        // MainWorld»). Lo que hay que usar es la Camera que mueve ese driver.
        var lente = ctx?.Player != null
            ? (ctx.Player.CachedCamera != null ? ctx.Player.CachedCamera.transform : null)
            : null;
        if (lente == null && ctx?.Player != null && ctx.Player.ActiveCamera != null)
            lente = ctx.Player.ActiveCamera.transform;

        if (nube == null || lente == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[NubesDeApertura] Sin prefab de nube o sin cámara cinemática ({note}).");
#endif
            yield break;
        }

        var rutina = ctx.Player.StartCoroutine(Co_Abrir(ctx, lente, ctx.Player.CachedCamera));
        ctx.Player.TrackBackgroundRoutine(rutina);
        if (esperar) yield return rutina;
    }

    private IEnumerator Co_Abrir(SequenceContext ctx, Transform camara, Camera lente)
    {
        var izq = UnityEngine.Object.Instantiate(nube, camara);
        var der = UnityEngine.Object.Instantiate(nube, camara);
        izq.name = "_NubeIzquierda";
        der.name = "_NubeDerecha";
        izq.transform.localRotation = Quaternion.identity;
        der.transform.localRotation = Quaternion.identity;
        izq.transform.localScale = Vector3.one;
        der.transform.localScale = Vector3.one;

        // ── Que TAPEN la pantalla (INC-394) ──────────────────────────────────────────────────
        //
        // «No salen las nubes, se ve azul nada más; lo que sí veo aparecer es una nube al rato.»
        // Estaban puestas, colgadas de la lente y con material: lo que fallaba es el TAMAÑO. Una
        // escala fija (9) no significa nada sin saber cuánto mide el prefab ni cuánto se ve por
        // la lente a esa distancia — y este prefab es un «MeshCarrier», que a escala 1 mide poco
        // más de un metro. Dos nubes de nueve metros a cuatro metros y medio de la cámara tapan
        // un trozo del centro, no el cuadro.
        //
        // Así que se mide: lo que se ve de alto y de ancho a esa distancia sale del campo de
        // visión de la cámara, y el tamaño real del prefab, de su Renderer. Con esas dos cosas,
        // la escala y la separación salen solas y da igual qué nube se use.
        float dist = Mathf.Max(distancia, lente != null ? lente.nearClipPlane * 3f : 0.3f);
        float escalaFinal = escala;
        float sepInicial = separacionInicial;
        float sepFinal = separacionFinal;

        var pinta = izq.GetComponentInChildren<Renderer>();

        // Tamaño y centro EN LOS EJES DE LA NUBE, no del mundo (INC-395). `bounds` es una caja
        // alineada con el mundo: con la cámara mirando hacia abajo (la vista de pájaro), la nube
        // va girada con ella y esa caja sale bastante más grande que la nube de verdad — así que
        // la escala calculada se quedaba corta. `localBounds` es la caja del propio mesh.
        //
        // Y el centro importa porque este mesh no tiene el pivote en el medio: colocando el
        // pivote a ±x, una nube quedaba más cerca del centro que la otra y por el hueco se veía
        // el valle desde el primer fotograma. Se descuenta al colocarlas.
        Vector3 tamUnidad = Vector3.one;
        Vector3 centroUnidad = Vector3.zero;
        if (pinta != null)
        {
            if (pinta.transform == izq.transform)
            {
                tamUnidad = pinta.localBounds.size;
                centroUnidad = pinta.localBounds.center;
            }
            else
            {
                tamUnidad = camara.InverseTransformVector(pinta.bounds.size);
                tamUnidad = new Vector3(Mathf.Abs(tamUnidad.x), Mathf.Abs(tamUnidad.y), Mathf.Abs(tamUnidad.z));
                centroUnidad = camara.InverseTransformVector(pinta.bounds.center - izq.transform.position);
            }
        }

        if (escalaAutomatica && lente != null && pinta != null && tamUnidad.x > 0.001f)
        {
            float alto = 2f * dist * Mathf.Tan(lente.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float ancho = alto * lente.aspect;

            // Cada nube cubre bastante más de media pantalla de ancho y la pantalla entera de
            // alto, y juntas SOLAPAN en el centro: el borde de este mesh es deshilachado, y con
            // un solape justo se colaba el cielo por los flecos (INC-395, «un tramo azul»).
            escalaFinal = Mathf.Max((ancho * 0.85f) / tamUnidad.x, (alto * 1.5f) / tamUnidad.y);
            sepInicial = tamUnidad.x * escalaFinal * 0.28f;
            sepFinal = ancho * 0.5f + tamUnidad.x * escalaFinal * 0.65f;  // fuera del cuadro del todo
        }

        // Aplastadas en profundidad (INC-395). El mesh es casi tan hondo como ancho: escalado para
        // tapar la pantalla, medía más de fondo que la distancia a la lente, así que la cámara
        // quedaba DENTRO de la nube y lo que se veía era su interior, un azul plano. Una cortina
        // de nube no necesita fondo: se deja en un grosor que nunca llegue a la lente.
        float fondo = tamUnidad.z * escalaFinal;
        float aplastado = fondo > 0.001f ? Mathf.Clamp01((dist * 1.1f) / fondo) : 1f;

        Vector3 escalaNube = new Vector3(escalaFinal, escalaFinal, escalaFinal * aplastado);
        izq.transform.localScale = escalaNube;
        der.transform.localScale = escalaNube;
        Vector3 desfase = Vector3.Scale(centroUnidad, escalaNube);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnostico (INC-383): si las nubes no se ven, esta linea dice si es que no se han
        // creado, si se han creado detras de la camara o si se han creado sin nada que pintar.
        Debug.Log($"[NubesDeApertura] '{nube.name}' x2 en '{camara.name}' a {dist:F1} m. " +
                  $"El prefab mide {tamUnidad.ToString("F2")} a escala 1 → escala {escalaFinal:F1} " +
                  $"(fondo x{aplastado:F2}), separación {sepInicial:F1}→{sepFinal:F1} en {segundos:F1}s. " +
                  $"Pinta: {(pinta != null ? pinta.sharedMaterial != null ? pinta.sharedMaterial.name : "SIN MATERIAL" : "SIN RENDERER")}. " +
                  $"Lente: fov {(lente != null ? lente.fieldOfView : 0f):F0}, near {(lente != null ? lente.nearClipPlane : 0f):F2}.");
#endif

        // Se registran para que el cierre de la secuencia (o el salto) las barra igual que todo
        // lo demás: nadie se puede quedar con dos nubes pegadas a la cara.
        ctx.Player.RegisterCleanup(() =>
        {
            if (izq != null) UnityEngine.Object.Destroy(izq);
            if (der != null) UnityEngine.Object.Destroy(der);
        });

        float t = 0f;
        float duracion = Mathf.Max(0.1f, segundos);

        // Se colocan cerradas ANTES del primer yield: así el primer fotograma que se pinta ya es
        // de nube, nunca de valle ni de cielo.
        Colocar(izq, der, sepInicial, 0f, dist, desfase);

        while (t < duracion)
        {
            if (izq == null || der == null) yield break;

            // Se abren DESPACIO y por igual (INC-395, «cuando se separan, más lentito»). Antes la
            // curva era rápida al principio: en el primer medio segundo ya se había ido una cuarta
            // parte, y lo que se veía era un tirón. Con arranque y frenada suaves se ve abrirse.
            float k = t / duracion;
            k = k * k * (3f - 2f * k);
            float x = Mathf.Lerp(sepInicial, sepFinal, k);
            float subida = Mathf.Lerp(0f, escalaFinal * tamUnidad.y * 0.12f, k);

            Colocar(izq, der, x, subida, dist, desfase);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (izq != null) UnityEngine.Object.Destroy(izq);
        if (der != null) UnityEngine.Object.Destroy(der);
    }

    /// Las dos nubes a ±x del centro del cuadro, contando con que su centro no está en el pivote.
    private static void Colocar(GameObject izq, GameObject der, float x, float subida, float dist,
        Vector3 desfase)
    {
        izq.transform.localPosition = new Vector3(-x, subida, dist) - desfase;
        der.transform.localPosition = new Vector3(x, subida, dist) - desfase;
    }
}

[Serializable]
public class WeatherBeat : SequenceBeat
{
    public enum Fenomeno
    {
        Tormenta = 0,
        Viento = 1,
        Lluvia = 2,
        Niebla = 3,
    }

    [Tooltip("Qué fenómeno se toca.")]
    public Fenomeno fenomeno = Fenomeno.Tormenta;

    [Tooltip("Marcado = encender. Desmarcado = apagar.")]
    public bool encender = true;

    [Tooltip("Sin transición: el cielo cambia de golpe. Para un corte dramático (un trueno que " +
             "abre la escena) suele ser lo que se quiere; para que el tiempo 'vaya cambiando' " +
             "mientras alguien habla, dejarlo desmarcado.")]
    public bool immediate = false;

    public override string Describe() => $"Tiempo: {(encender ? "encender" : "apagar")} {fenomeno}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        CinematicWeather.Apply(fenomeno, encender, immediate);

        // Igual que el beat de hora: el clima MULTIPLICA sobre el cielo de la franja horaria, y es
        // esa multiplicacion la que lo puede dejar negro. Que quede dicho en el log.
        //
        // Ojo al leerlo: si immediate esta desmarcado, el cielo tarda unos segundos en nublarse,
        // asi que este valor es el de ANTES de la transicion. El valor que importa es el que
        // imprime el beat de hora siguiente, ya con todo aplicado.
        CinematicTimeOfDay.Diagnostico($"al {(encender ? "encender" : "apagar")} {fenomeno}",
            ctx?.Player?.CachedCamera);

        yield break;
    }
}

/// El tiempo atmosférico mientras manda una cinemática. Gemelo de CinematicTimeOfDay: guarda lo que
/// había la primera vez que se toca y lo devuelve al terminar.
public static class CinematicWeather
{
    /// Lo que dura una tormenta o una lluvia puesta por una cinemática: más que cualquier escena.
    private const float DuracionDeEscena = 3600f;

    private static bool _guardado;
    private static bool _lluviaPrevia;
    private static bool _nieblaPrevia;
    private static bool _vientoPrevio;
    private static bool _tormentaPrevia;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _guardado = false; }
#endif

    public static void Apply(WeatherBeat.Fenomeno fenomeno, bool encender, bool immediate)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CinematicWeather] No hay ningún DayNightCycle cargado, así que el " +
                "tiempo de esta cinemática no se puede cambiar. (Normal en una escena de prueba " +
                "abierta suelta; en partida no debería pasar.)");
#endif
            return;
        }

        Guardar(ciclo);
        DayNightCycle.SorteoDeClimaEnPausa = true;

        switch (fenomeno)
        {
            // Duración larga a propósito (INC-409): sin ella la tormenta sorteaba la suya y en
            // prologo17 dejó de llover a mitad del duelo. La quita Restore() al acabar la escena.
            case WeatherBeat.Fenomeno.Tormenta:
                if (encender) ciclo.StartThunderstorm(DuracionDeEscena, immediate); else ciclo.StopThunderstorm();
                break;

            case WeatherBeat.Fenomeno.Viento:
                if (encender) ciclo.StartWind(); else ciclo.StopWind();
                break;

            case WeatherBeat.Fenomeno.Lluvia:
                if (encender) ciclo.StartRain(DuracionDeEscena, immediate); else ciclo.StopRain();
                break;

            case WeatherBeat.Fenomeno.Niebla:
                if (encender) ciclo.StartMist(); else ciclo.StopMist();
                break;
        }
    }

    private static void Guardar(DayNightCycle ciclo)
    {
        if (_guardado) return;

        _lluviaPrevia = ciclo.IsRaining;
        _nieblaPrevia = ciclo.IsMisty;
        _vientoPrevio = ciclo.IsWindy;
        _tormentaPrevia = ciclo.IsThunderstorm;
        _guardado = true;
    }

    /// Devuelve el tiempo que había antes de la cinemática. La llama CinematicTimeOfDay.Restore().
    public static void Restore()
    {
        if (!_guardado) return;
        _guardado = false;

        var ciclo = DayNightCycle.Instance;
        if (ciclo == null) return;

        if (ciclo.IsThunderstorm != _tormentaPrevia)
        {
            if (_tormentaPrevia) ciclo.StartThunderstorm(null, immediate: true); else ciclo.StopThunderstorm();
        }

        if (ciclo.IsRaining != _lluviaPrevia)
        {
            if (_lluviaPrevia) ciclo.StartRain(null, immediate: true); else ciclo.StopRain();
        }

        if (ciclo.IsWindy != _vientoPrevio)
        {
            if (_vientoPrevio) ciclo.StartWind(); else ciclo.StopWind();
        }

        if (ciclo.IsMisty != _nieblaPrevia)
        {
            if (_nieblaPrevia) ciclo.StartMist(); else ciclo.StopMist();
        }
    }
}

/// Lleva a un actor andando por una lista de marcas, SIN usar el NavMesh.
///
/// ── Por qué NO lo hace MoveToBeat ─────────────────────────────────────────────────────────────
/// MoveToBeat delega en SequenceMovement.MoveTo, que navega de verdad: pide ruta al NavMeshAgent y
/// lee su velocidad real para alimentar la animación. Eso es lo correcto para moverse por el
/// pueblo, y su propio comentario explica por qué se negó a caer en un Lerp a mano: reintroduciría
/// el patinaje de INC-209.
///
/// Pero hay un caso que ese camino no puede cubrir: **bajar de una montaña**. La ladera no está
/// bakeada (ni tiene por qué estarlo: no es terreno de juego), así que el agente no tiene ruta y
/// MoveTo se rinde colocando al actor en el destino de un salto. En el prólogo eso es justo el
/// plano que queremos: el Mago Oscuro bajando hacia la villa.
///
/// ── Cómo evita el patinaje ────────────────────────────────────────────────────────────────────
/// El patinaje de INC-209 no venía de mover el transform a mano: venía de que el ciclo de andar no
/// casaba con el avance real. Aquí se alimenta SetMovementSpeed con **la misma normalización que
/// usa el agente** (velocidad ÷ velocidad máxima del propio agente del personaje), así que el paso
/// y el avance van al mismo ritmo. Y se avisa al animator con AllowManualMovement, que existe
/// exactamente para esto: «un sistema externo tiene el control».
///
/// Se pega al suelo con un raycast por frame, así que sirve para cualquier relieve sin preparar
/// nada — y por eso hay que poner las marcas en planta: la altura la calcula él.
[Serializable]
public class WalkPathBeat : SequenceBeat
{
    [Tooltip("Quién anda.")]
    public string actorId;

    [Tooltip("Las marcas por las que pasa, en orden. Solo importan su X y su Z: la altura la " +
             "calcula el propio beat pegándose al suelo.")]
    public List<string> markNames = new();

    [Tooltip("Velocidad en metros por segundo. 1,2 es un paseo; 1,8 un paso decidido; 3 una " +
             "carrera. Bajarla hace el plano más solemne, que suele ser lo que se quiere cuando " +
             "alguien baja de una montaña.")]
    public float speed = 1.4f;

    [Tooltip("Pegar al suelo con un raycast en cada frame. Desmarcado, la altura se interpola " +
             "entre marcas — solo para volar o flotar.")]
    public bool stickToGround = true;

    [Tooltip("Cuánto se levanta del suelo, en metros. Para un personaje normal, 0.")]
    public float groundOffset = 0f;

    [Tooltip("Girar hacia donde avanza. Desmarcado, conserva la orientación que traía — para " +
             "retroceder sin dar la espalda.")]
    public bool faceTravelDirection = true;

    [Tooltip("Mover las piernas. Marcado (lo normal) el actor anda; desmarcado se desplaza sin " +
             "tocar su animación, que es lo que hace falta para volar, para flotar o para " +
             "cualquier desplazamiento en el que la pose la ponga otro beat.")]
    public bool animarAndando = true;

    [Tooltip("SIN PASAR POR IDLE. El estado del Animator al que se encadena en cuanto termina el " +
             "recorrido, en vez de volver a idle (p. ej. 'JumpStart_InPlace_NoWeapon').\n\n" +
             "Existe porque al acabar de andar el beat manda al actor a idle, y el beat siguiente " +
             "—el salto, un gesto— cruza DESDE idle: se ve un fotograma parado en mitad de una " +
             "carrera. «Cuando el Archimago corre y se pone de espaldas hay un momento donde se " +
             "para; debe correr y hacer la animación sin pasar por idle.» Con esto la pose se pone " +
             "aquí, antes de soltar el control, y el beat siguiente se la encuentra ya puesta " +
             "(HoldPose es idempotente, así que no se reinicia).")]
    public string encadenarCon = "";

    [Tooltip("Segundos que espera antes de echar a andar. Para la vida de fondo: varios vecinos " +
             "paseando en el mismo Parallel no salen todos a la vez como en un desfile.")]
    public float retraso = 0f;

    /// Tope de seguridad por tramo. Ningún beat puede colgar la secuencia (regla del sistema).
    private const float TopePorTramo = 30f;

    /// Velocidad máxima de referencia cuando el actor no tiene agente del que sacarla.
    private const float VelocidadMaximaPorDefecto = 3.5f;

    /// Cuánto puede subir un personaje de un paso al pegarse al suelo: un escalón, una rampa, el
    /// tablero de un puente. Lo que esté más alto que esto no es suelo, es algo que tiene encima.
    /// Aquí el puente sube 1,2 m y la viga de la plaza está a 2,6: metro y medio separa los dos.
    internal const float AlturaQueSePuedeSubir = 1.5f;

    // Buffer pre-alocado: el pegado al suelo corre una vez por frame (CLAUDE.md § 2).
    private static readonly RaycastHit[] s_hits = new RaycastHit[8];

    public override string Describe()
        => $"Andar: {actorId} por {(markNames == null ? 0 : markNames.Count)} marca(s) a {speed} m/s";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx?.GetActor(actorId);
        if (actor?.Transform == null || markNames == null || markNames.Count == 0) yield break;

        if (retraso > 0f) yield return new WaitForSeconds(retraso);

        var anim = actor.NpcAnimator;
        var agent = actor.Agent;

        // La referencia para normalizar el paso es UNA CONSTANTE, igual para todos (INC-317).
        //
        // Antes salía de `agent.speed`, la velocidad del NavMeshAgent de cada personaje. La idea
        // era que el paso casara con el avance, pero aquí no se aplica: este beat mueve el
        // transform a mano y APAGA el agente tres líneas más abajo, así que su velocidad no
        // interviene en nada. Lo único que hacía era que dos personajes andando a la misma
        // velocidad real movieran las piernas a ritmos distintos, porque `agent.speed` es un
        // ajuste de pathfinding y cada prefab trae el suyo.
        //
        // Es «cuando vamos hacia el puente Liora no está caminando como deben caminar los NPCs,
        // sin embargo el Archimago sí camina bien»: misma marca, misma velocidad, distinto
        // divisor. Con una constante, la misma velocidad da el mismo paso a todo el mundo, que es
        // lo que pide un blend tree compartido.
        const float velocidadMaxima = VelocidadMaximaPorDefecto;

        // El agente estorba: intentaría corregir la posición contra un NavMesh que aquí no existe.
        if (agent != null && agent.enabled) agent.enabled = false;

        if (anim != null)
        {
            anim.AllowManualMovement = true;
            anim.AllowManualRotation = true;
        }

        try
        {
            // Salir de cualquier pose de conversación antes de echar a andar, igual que hace
            // SequenceMovement: si no, un bocadillo anterior deja la capa de tronco congelada.
            anim?.SetBattleMode(false);
            anim?.SetTalking(false);
            anim?.EndInteraction();
            if (animarAndando) anim?.TransitionToLocomotion();

            float v = Mathf.Max(0.2f, speed);
            float paso = Mathf.Clamp01(v / velocidadMaxima);

            CargarObstaculos(ctx);

            for (int i = 0; i < markNames.Count; i++)
            {
                var marca = ctx.Stage != null ? ctx.Stage.GetMark(markNames[i]) : null;
                if (marca == null) continue;   // el aviso ya lo ha dado el stage

                // Si en línea recta se atraviesa algo que está en medio (la carreta), primero se
                // va al punto que lo rodea. Ver Rodeo().
                if (stickToGround && Rodeo(actor.Transform.position, marca.position, out Vector3 rodeo))
                {
                    yield return Tramo(actor, anim, rodeo, v, paso, stickToGround,
                        groundOffset, faceTravelDirection, TopePorTramo, markNames[i] + " (rodeo)",
                        animarAndando);
                }

                yield return Tramo(actor, anim, marca.position, v, paso, stickToGround,
                    groundOffset, faceTravelDirection, TopePorTramo, markNames[i], animarAndando);
            }
        }
        finally
        {
            // ── LA REGLA DE INC-210 ──────────────────────────────────────────────────────────
            //
            // Aquí ponía `SetMovementSpeed(0f)`, y eso es EXACTAMENTE lo que INC-210 prohibió el
            // 16 de septiembre. Esa sobrecarga escribe el parámetro del Animator CON AMORTIGUACIÓN
            // (`animator.SetFloat(hash, valor, dampTime, deltaTime)`): no asigna el valor, lo
            // acerca. Una sola llamada mueve `InputMagnitude` un 15 % hacia 0 y ahí se queda,
            // porque durante la secuencia nadie vuelve a escribirlo cada frame.
            //
            // Resultado: el parámetro se congela en ~0,85, el estado de idle cae solo en el blend
            // tree de locomoción (su única transición de salida no tiene condiciones) y el actor
            // reproduce la animación de andar CON EL CUERPO PARADO. Y como el blend tree también
            // lee dirección, a veces sale andando de lado o de espaldas.
            //
            // Es la mitad de la lista de la novena grabación: el Archimago hablando quieto y
            // andando, Liora en «¿y tú?», los vecinos del fondo, el Mago Oscuro diciendo lo de
            // arrodillaos. Un solo sitio, ocho síntomas.
            //
            // `ResetMovement()` escribe el 0 SIN amortiguar, que es para lo que existe.
            anim?.ResetMovement();

            // Y volver a una pose de verdad — pero solo si esto era andar. En un vuelo la pose la
            // pone el gesto que corre en paralelo, y mandarle a idle aquí se la comería.
            //
            // Y si el montaje dice con qué se encadena, se pone ESO y no idle: ver 'encadenarCon'.
            if (!string.IsNullOrEmpty(encadenarCon)) anim?.HoldPose(encadenarCon);
            else if (animarAndando) anim?.TransitionToIdle();

            if (anim != null)
            {
                anim.AllowManualMovement = false;
                anim.AllowManualRotation = false;

                // Y que se quede mirando donde ha acabado. Al soltar AllowManualRotation vuelve a
                // correr ApplySmoothRotation, que tira hacia `_targetRotation` — la rotación que
                // el animador tenía apuntada ANTES de la caminata. Sin esta línea el personaje
                // llega a su sitio y acto seguido se gira solo hacia donde miraba antes de andar,
                // que es una buena parte de los "de lado" y "de espaldas" de la quinta grabación
                // (INC-299).
                anim.SyncTargetRotation();
            }
        }
    }

    // ── Rodear lo que está en medio ─────────────────────────────────────────────────────────
    //
    // «La gente se tropieza con la carreta en lugar de esquivarla.» Este beat anda en línea recta
    // entre marcas, sin NavMesh, así que cualquier cosa que quede entre dos marcas se atraviesa:
    // en la grabación 11 los vecinos salen de la plaza hacia el puente pasando POR DENTRO de la
    // carreta, que Raúl ha colocado en el camino.
    //
    // Las marcas no se pueden mover para cada caso —el que sale de la plaza está donde esté—, así
    // que el rodeo se calcula aquí: cada prop del SequenceStage marcado `esObstaculo` es un
    // círculo en planta, y si el tramo lo corta se añade un punto tangente por el lado más corto.
    // Una vez por beat, no por frame, y con listas estáticas (CLAUDE.md § 2).

    /// Margen alrededor del obstáculo: lo que ocupa un personaje y un poco de aire.
    private const float MargenAlRodear = 0.7f;

    private static readonly List<Vector3> s_obstaculoCentro = new();
    private static readonly List<float> s_obstaculoRadio = new();

    private static void CargarObstaculos(SequenceContext ctx)
    {
        s_obstaculoCentro.Clear();
        s_obstaculoRadio.Clear();
        if (ctx?.Stage == null) return;

        foreach (var prop in ctx.Stage.Props)
        {
            if (!prop.esObstaculo || prop.target == null || !prop.target.gameObject.activeInHierarchy)
                continue;

            var renders = prop.target.GetComponentsInChildren<Renderer>();
            bool hay = false;
            Bounds caja = default;
            foreach (var r in renders)
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (!hay) { caja = r.bounds; hay = true; }
                else caja.Encapsulate(r.bounds);
            }
            if (!hay) continue;

            Vector3 c = caja.center; c.y = 0f;
            s_obstaculoCentro.Add(c);
            s_obstaculoRadio.Add(Mathf.Max(caja.extents.x, caja.extents.z) + MargenAlRodear);
        }
    }

    /// ¿El tramo de A a B corta algún obstáculo? Si sí, devuelve el punto por el que rodearlo.
    private static bool Rodeo(Vector3 a, Vector3 b, out Vector3 punto)
    {
        punto = default;
        Vector2 A = new(a.x, a.z), B = new(b.x, b.z);
        Vector2 ab = B - A;
        float largo = ab.magnitude;
        if (largo < 0.01f) return false;
        Vector2 dir = ab / largo;

        float mejorT = float.MaxValue;
        for (int i = 0; i < s_obstaculoCentro.Count; i++)
        {
            Vector2 C = new(s_obstaculoCentro[i].x, s_obstaculoCentro[i].z);
            float r = s_obstaculoRadio[i];

            // Si la salida o la llegada están DENTRO del obstáculo no hay rodeo que valga: la marca
            // está mal puesta, y rodear solo daría una vuelta rara.
            if ((A - C).sqrMagnitude < r * r || (B - C).sqrMagnitude < r * r) continue;

            float t = Vector2.Dot(C - A, dir);
            if (t <= 0f || t >= largo) continue;          // queda antes o después del tramo
            Vector2 cercano = A + dir * t;
            Vector2 aparte = cercano - C;
            if (aparte.sqrMagnitude >= r * r) continue;   // pasa sin tocarlo
            if (t >= mejorT) continue;                    // hay otro antes en el camino

            // Por el lado por el que el tramo ya pasaba más cerca del borde: es el rodeo corto.
            Vector2 lado = aparte.sqrMagnitude > 0.0001f
                ? aparte.normalized
                : new Vector2(-dir.y, dir.x);
            Vector2 p = C + lado * (r + 0.05f);

            mejorT = t;
            punto = new Vector3(p.x, Mathf.Lerp(a.y, b.y, t / largo), p.y);
        }

        return mejorT < float.MaxValue;
    }

    /// Lleva a un actor andando hasta un punto, sin NavMesh. Es el mismo paseo que usa este beat,
    /// expuesto para que SequenceMovement pueda caer aquí cuando el agente no sirve — antes, en ese
    /// caso, el actor se teletransportaba al destino.
    public static IEnumerator AndarHasta(SequenceActor actor, Vector3 destino, float velocidad,
        float tope)
    {
        if (actor?.Transform == null) yield break;

        var anim = actor.NpcAnimator;
        var agent = actor.Agent;

        // Misma constante para todos: ver la explicación larga en WalkPathBeat (INC-317).
        const float velocidadMaxima = VelocidadMaximaPorDefecto;

        // Se apaga el agente para conducir a mano, y hay que ACORDARSE de si estaba encendido: ver
        // el finally. Dejarlo apagado es lo que hacia que el Mago Oscuro, despues de bajar la
        // montana con un WalkPathBeat, llegara al duelo sin agente utilizable -- y a partir de ahi
        // TODOS sus MoveToBeat volvian a caer aqui, a la linea recta, en vez de navegar.
        bool agenteEstabaEncendido = agent != null && agent.enabled;
        if (agenteEstabaEncendido) agent.enabled = false;

        if (anim != null)
        {
            anim.AllowManualMovement = true;
            anim.AllowManualRotation = true;
        }

        try
        {
            anim?.SetBattleMode(false);
            anim?.SetTalking(false);
            anim?.EndInteraction();
            anim?.TransitionToLocomotion();

            float v = Mathf.Max(0.2f, velocidad);

            yield return Tramo(actor, anim, destino, v, Mathf.Clamp01(v / velocidadMaxima),
                true, 0f, true, Mathf.Max(1f, tope), "su destino");
        }
        finally
        {
            // Mismo caso que arriba: la regla de INC-210. Aquí importa todavía más, porque este es
            // el camino por el que pasan los MoveToBeat cuyo actor se ha quedado sin agente.
            anim?.ResetMovement();
            anim?.TransitionToIdle();

            if (anim != null)
            {
                anim.AllowManualMovement = false;
                anim.AllowManualRotation = false;

                // Y que se quede mirando donde ha acabado. Al soltar AllowManualRotation vuelve a
                // correr ApplySmoothRotation, que tira hacia `_targetRotation` — la rotación que
                // el animador tenía apuntada ANTES de la caminata. Sin esta línea el personaje
                // llega a su sitio y acto seguido se gira solo hacia donde miraba antes de andar,
                // que es una buena parte de los "de lado" y "de espaldas" de la quinta grabación
                // (INC-299).
                anim.SyncTargetRotation();
            }

            DevolverElAgente(actor, agent, agenteEstabaEncendido);
        }
    }

    /// Vuelve a dejar al actor a cargo de su NavMeshAgent despues de un paseo a mano.
    ///
    /// No basta con encenderlo: si el paseo ha terminado fuera del NavMesh (la ladera de la montana
    /// del prologo no esta bakeada, por ejemplo), el agente arranca en un sitio que no es del mapa
    /// de navegacion, `isOnNavMesh` sigue siendo false, y el siguiente MoveToBeat vuelve a caer al
    /// paseo a mano sin que nadie sepa por que. Asi que se le busca el suelo caminable mas cercano
    /// y se le teletransporta ahi -- son centimetros, no se ve -- antes de devolverselo.
    private static void DevolverElAgente(SequenceActor actor, NavMeshAgent agent, bool estabaEncendido)
    {
        if (agent == null || !estabaEncendido) return;

        Vector3 donde = actor != null && actor.Transform != null ? actor.Transform.position : agent.transform.position;

        agent.enabled = true;

        if (agent.isOnNavMesh) return;

        // Radios crecientes, igual que hace el wiring de las marcas. 9 m es generoso a proposito:
        // mas vale un salto de medio metro que un personaje que ya no sabe navegar en toda la escena.
        foreach (float radio in new[] { 1f, 2.5f, 5f, 9f })
        {
            if (!NavMesh.SamplePosition(donde, out var hit, radio, NavMesh.AllAreas)) continue;
            if (agent.Warp(hit.position)) return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[WalkPathBeat] '{actor?.Id}' ha terminado el paseo fuera del NavMesh y no " +
            "hay suelo caminable a menos de 9 m. Se queda sin agente utilizable, asi que sus " +
            "movimientos siguientes tambien iran en linea recta. Lo que hay que mirar es el bakeado " +
            "del NavMesh en esa zona, no el beat.");
#endif
    }

    /// Un tramo del paseo: de donde esté el actor hasta 'destino'.
    /// Grados por segundo a los que un personaje se gira hacia donde anda.
    ///
    /// Empezó en 720 (media vuelta en un cuarto de segundo) y se quedaba seco: Raúl, tras la
    /// novena, «cuando cambian de dirección se ve muy brusco». A 300 un giro de 90° tarda tres
    /// décimas, que es lo que tarda una persona en cambiar de rumbo andando.
    private const float GiroAlAndar = 300f;

    private static IEnumerator Tramo(SequenceActor actor, NPCSimpleAnimator anim, Vector3 destino,
        float v, float paso, bool pegarAlSuelo, float alturaExtra, bool mirarAlAvance, float tope,
        string aDonde, bool animarAndando = true)
    {
        float transcurrido = 0f;

        while (transcurrido < tope)
        {
            transcurrido += Time.deltaTime;

            Vector3 pos = actor.Transform.position;
            Vector3 plano = destino - pos;
            plano.y = 0f;

            if (plano.magnitude <= 0.2f) break;

            Vector3 dir = plano.normalized;
            Vector3 siguiente = pos + dir * (v * Time.deltaTime);

            // Espacio personal (INC-406): apartarse de lado de quien tenga delante, en vez de
            // atravesarle. Solo andando por el suelo.
            Vector3 aparte = (pegarAlSuelo && animarAndando)
                ? Apartarse(actor.Transform, pos, dir) * (v * Time.deltaTime)
                : Vector3.zero;

            if (pegarAlSuelo)
            {
                Vector3 conApartarse = PegarAlSuelo(actor.Transform, siguiente + aparte);
                // Apartarse nunca vale si es para caerse de algo (el borde del puente).
                siguiente = aparte != Vector3.zero && conApartarse.y > pos.y - 0.4f
                    ? conApartarse
                    : PegarAlSuelo(actor.Transform, siguiente);
            }
            else siguiente.y = Mathf.MoveTowards(pos.y, destino.y, v * Time.deltaTime);

            siguiente.y += alturaExtra;
            actor.Transform.position = siguiente;

            // Volando NO se toca la animación. Si se sigue escribiendo la velocidad de
            // locomoción, SetMovementSpeed vuelve a llamar a TransitionToLocomotion en cuanto el
            // gesto de vuelo termina su ciclo — y lo que se ve es un mago cruzando el cielo a
            // pasitos, que es literalmente lo que salió en la novena grabación.
            if (animarAndando) anim?.SetMovementSpeed(paso);

            // ── Girar hacia donde se anda ─────────────────────────────────────────────────────
            //
            // Esto ESCRIBE la rotación. Antes llamaba a `anim.FaceDirection(dir)`, que no gira a
            // nadie: solo apunta `_targetRotation`, y quien gira es `ApplySmoothRotation()` en el
            // Update del animador — que empieza con
            //
            //     if (_disableAutoRotation || AllowManualRotation) return;
            //
            // y `AllowManualRotation` lo pone a true este mismo beat, al empezar a andar. O sea
            // que **durante toda la caminata no giraba nadie**: el personaje conservaba la
            // rotación que tuviera al arrancar y se deslizaba por el camino.
            //
            // Es «los NPCs caminan de espaldas, el Archimago también, el Mago Oscuro también»
            // (INC-313), y es el tercer sitio donde aparece el mismo fallo de fondo: una API que
            // PROPONE una rotación en vez de aplicarla, combinada con la bandera que desactiva a
            // quien la aplicaría. Los otros dos fueron `SequenceActor.Face` y el arrastre de
            // vuelta al terminar de andar (INC-299).
            //
            // Se gira deprisa pero no de golpe: 720°/s da media vuelta en un cuarto de segundo,
            // que se lee como girarse y echar a andar, no como un salto de rotación.
            if (mirarAlAvance)
            {
                actor.Transform.rotation = Quaternion.RotateTowards(
                    actor.Transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    GiroAlAndar * Time.deltaTime);

                // Y que el animador no lo arrastre de vuelta en cuanto se suelte la bandera.
                anim?.SyncTargetRotation();
            }

            yield return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (transcurrido >= tope)
            Debug.LogWarning($"[WalkPathBeat] '{actor.Id}' no llegó a '{aDonde}' en {tope}s. La " +
                "secuencia sigue desde donde esté. Suele significar que está demasiado lejos para " +
                "la velocidad puesta.");
#endif
    }

    // ── Espacio personal ──────────────────────────────────────────────────────────────────────
    //
    // «Los NPCs se siguen atravesando», «Liora atraviesa a la gente en el puente» (prologo16).
    // Este beat mueve el transform a mano con el agente apagado — a propósito, para que el montaje
    // mande sobre el sitio exacto y la hora de llegada — y por eso el esquive del NavMesh (INC-400)
    // no le alcanza: quien andaba con WalkPathBeat cruzaba a través de los demás.
    //
    // Esto es lo mínimo que lo evita sin quitarle al montaje el control: cuando alguien queda
    // delante o al lado a menos de un metro, el que anda se desvía DE LADO, más cuanto más cerca.
    // Nunca frena ni retrocede, así que llega a su marca a la misma hora que antes.

    private const float RadioPersonal = 1.0f;
    private static readonly Collider[] s_vecinos = new Collider[16];

    /// Dirección lateral (sin normalizar, 0..~1) en la que apartarse de quien estorbe.
    private static Vector3 Apartarse(Transform yo, Vector3 pos, Vector3 dir)
    {
        // Con disparadores incluidos: hay NPCs cuyo único collider es el de interacción. Lo que
        // cuenta después es la distancia a SU posición, no el tamaño del collider.
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, RadioPersonal, s_vecinos, ~0,
            QueryTriggerInteraction.Collide);
        if (n == 0) return Vector3.zero;

        Vector3 perp = new Vector3(dir.z, 0f, -dir.x);   // a la derecha del avance
        float lateral = 0f;
        for (int i = 0; i < n; i++)
        {
            var c = s_vecinos[i];
            if (c == null || c.transform.IsChildOf(yo)) continue;
            // Personajes y geometría viven en la misma capa: lo que distingue a un personaje es
            // el NPCSimpleAnimator (CLAUDE.md § 2).
            var otro = c.GetComponentInParent<NPCSimpleAnimator>();
            if (otro == null || otro.transform == yo) continue;

            Vector3 rel = otro.transform.position - pos;
            rel.y = 0f;
            float d = rel.magnitude;
            if (d > RadioPersonal || d < 0.001f) continue;
            if (Vector3.Dot(rel, dir) < -0.2f * d) continue;   // ya lo ha dejado atrás

            float fuerza = 1f - d / RadioPersonal;
            // Si el otro queda a la derecha, a la izquierda; y al revés. De frente, a la derecha.
            float lado = Vector3.Dot(rel, perp);
            lateral += (lado >= 0f ? -1f : 1f) * fuerza;
        }
        return perp * Mathf.Clamp(lateral, -0.9f, 0.9f);
    }

    /// Baja el punto hasta el suelo que tenga debajo. El propio actor no cuenta como suelo.
    private static Vector3 PegarAlSuelo(Transform actor, Vector3 punto)
    {
        int n = Physics.RaycastNonAlloc(punto + Vector3.up * 3f, Vector3.down, s_hits, 12f, ~0,
            QueryTriggerInteraction.Ignore);

        float mejor = float.NegativeInfinity;

        // Techo de búsqueda: el suelo está ABAJO. Sin esto, cualquier cosa que haya por encima
        // —una viga, un alero, un toldo— cuenta como suelo, porque de todos los impactos se coge
        // el más alto, y un vecino que pasa por debajo se sube encima (INC-315).
        //
        // El margen empezó en 0,5 m y era DEMASIADO CORTO: el tablero del puente está 1,2 m por
        // encima de la plaza, así que quedaba excluido y los vecinos cruzaban el río por debajo
        // del puente, andando sobre el agua (INC-319 — regresión mía del mismo día). Metro y
        // medio deja subir a un puente, a una rampa o a un escalón alto, y sigue dejando fuera la
        // viga de la plaza, que está a 2,6 m.
        float techo = punto.y + AlturaQueSePuedeSubir;

        for (int i = 0; i < n; i++)
        {
            var hit = s_hits[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.root == actor) continue;   // no pisarse a sí mismo
            if (hit.point.y > techo) continue;

            if (hit.point.y > mejor) mejor = hit.point.y;
        }

        if (float.IsNegativeInfinity(mejor)) return punto;   // sin suelo debajo: no tocar la altura

        punto.y = mejor;
        return punto;
    }
}

/// Un salto: sube desde DONDE ESTÁ, se sostiene arriba y baja.
///
/// ── Por qué hace falta un beat y no valen dos marcas ──────────────────────────────────────────
/// Hasta ahora un salto se montaba como un viaje entre marcas aéreas puestas a mano: `JumpStart`
/// y después andar, sin pegarse al suelo, hasta un punto del aire. Eso tiene tres problemas y los
/// tres se ven en el prólogo:
///
///   · El salto va HACIA LA MARCA, no hacia arriba. Raúl, sobre el esquive del Archimago: «salta
///     para esquivar un ataque en lugar de saltar hacia arriba en el eje Y, y parece que salta
///     para otro lado». Es literal — iba a donde estuviera esa coordenada.
///   · La caída va a otra marca, así que el personaje aterriza donde diga el número y no donde
///     está el suelo. En la sexta grabación el Archimago termina **de pie encima de un tejado**.
///   · Cada trozo había que colocarlo a mano en la escena, y un cambio de posición del personaje
///     deja las marcas mintiendo sin que nada avise.
///
/// Aquí el arco es RELATIVO: sube `altura` metros sobre donde esté, se aparta `desplazamiento`
/// metros en la dirección que se le diga, y baja hasta el suelo que tenga debajo, buscándolo con
/// un raycast. No hay ninguna coordenada que mantener.
[Serializable]
public class SaltoBeat : SequenceBeat
{
    [Tooltip("Quién salta.")]
    public string actorId;

    [Tooltip("Metros que sube sobre donde está. 0 = NO sube: solo cae desde donde esté hasta el " +
             "suelo, que es lo que hace alguien al que acaban de alcanzar en el aire.")]
    public float altura = 3.5f;

    [Tooltip("Metros que se aparta, en horizontal. Positivo = hacia donde mira; negativo = hacia " +
             "atrás, que es lo que hace un esquive. 0 = salta en vertical y cae donde estaba.")]
    public float desplazamiento = 0f;

    [Tooltip("Apartarse hacia el LADO en vez de hacia delante o atrás. Un esquive lateral se lee " +
             "mucho mejor en cámara que uno hacia atrás, porque no acorta en perspectiva.")]
    public bool haciaElLado = false;

    [Tooltip("Segundos de subida.")]
    public float subida = 0.45f;

    [Tooltip("Segundos que se queda arriba. Es donde cabe una frase dicha en el aire.")]
    public float sostener = 0f;

    [Tooltip("Segundos de bajada. 0 = no baja: se queda arriba y lo que venga después decide.")]
    public float caida = 0.5f;

    [Tooltip("Pose del impulso, mientras sube.")]
    public string poseSubida = "JumpStart_InPlace_NoWeapon";

    [Tooltip("Pose de estar en el aire. Se MANTIENE todo el rato que esté arriba.")]
    public string poseAire = "JumpAir_InPlace_NoWeapon";

    [Tooltip("Pose de tomar tierra. Se deja vacía si no baja.")]
    public string poseCaida = "JumpEnd_InPlace_NoWeapon";

    [Tooltip("Marcado, el desplazamiento se reparte entre la subida y la bajada y va a velocidad " +
             "constante, así que el recorrido es una PARÁBOLA: lo que hace alguien al que un golpe " +
             "lanza por los aires. Desmarcado (lo de siempre), se aparta al subir y cae en vertical, " +
             "que es lo que hace alguien que salta.")]
    public bool parabola = false;

    [Tooltip("Metros por encima del punto de salida que puede tener el sitio donde aterriza. Con " +
             "el valor de siempre (-1 = el de WalkPathBeat) se puede acabar de pie encima de una " +
             "carreta, que es lo que pasaba al salir despedido en la plaza. Bajarlo a 0,3 obliga a " +
             "caer al suelo de verdad.")]
    public float alturaMaximaDeAterrizaje = -1f;

    [Tooltip("Pose que se queda puesta al tocar el suelo, en vez de volver a idle. Para acabar " +
             "tirado en el suelo ('Die01Stay_NoWeapon') y que el beat siguiente le levante.")]
    public string poseEnElSuelo = "";

    // Buffer propio: el de WalkPathBeat es suyo y esta es otra clase. Se busca el suelo una vez
    // por salto, no por frame, pero un array pre-alocado cuesta lo mismo y cumple CLAUDE.md § 2.
    private static readonly RaycastHit[] s_impactos = new RaycastHit[8];

    public override string Describe()
        => $"Salto: {actorId} {altura:F1} m" + (Mathf.Abs(desplazamiento) > 0.01f
            ? $" y {desplazamiento:F1} m {(haciaElLado ? "de lado" : "al frente")}" : "");

    /// Que un salto no acabe DENTRO de nada (INC-405). «Cuando el Mago Oscuro dispara al
    /// Archimago, este se mete dentro del carro»: el salto de lado y el retroceso al romperse el
    /// escudo iban una distancia fija en una dirección fija, sin mirar qué había allí. Ahora se
    /// comprueba con el NavMesh — que ya tiene talladas la carreta, las casas y las farolas — si
    /// se puede llegar andando del sitio de salida al de caída. Si no, se prueba hacia el otro
    /// lado; si tampoco, más corto; y si nada cabe, se salta en el sitio.
    private static Vector3 LadoLibre(Vector3 salida, Vector3 lado, string quien)
    {
        if (!UnityEngine.AI.NavMesh.SamplePosition(salida, out var enSuelo, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
            return lado;   // no está sobre el NavMesh (vuela, o fuera): no hay con qué comprobar

        Vector3[] intentos = { lado, -lado, lado * 0.5f, -lado * 0.5f, lado * 0.25f, -lado * 0.25f };
        foreach (var v in intentos)
        {
            Vector3 destino = enSuelo.position + v;
            if (!UnityEngine.AI.NavMesh.SamplePosition(destino, out var hit, 0.35f, UnityEngine.AI.NavMesh.AllAreas))
                continue;
            if (UnityEngine.AI.NavMesh.Raycast(enSuelo.position, hit.position, out _, UnityEngine.AI.NavMesh.AllAreas))
                continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (v != lado)
                Debug.Log($"[SaltoBeat] '{quien}' iba a caer dentro de algo; salta {v.magnitude:F1} m " +
                          $"{(Vector3.Dot(v, lado) < 0f ? "hacia el otro lado" : "más corto")}.");
#endif
            return v;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SaltoBeat] '{quien}' no tiene sitio libre a ningún lado: salta en el sitio.");
#endif
        return Vector3.zero;
    }

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        var anim = actor.NpcAnimator;
        var agent = actor.Agent;

        // El agente pega al personaje al NavMesh: con él encendido no hay salto que valga.
        bool agenteEstabaEncendido = agent != null && agent.enabled;
        if (agenteEstabaEncendido) agent.enabled = false;

        if (anim != null) anim.AllowManualMovement = true;

        Vector3 salida = actor.Transform.position;

        Vector3 lado = Vector3.zero;
        if (Mathf.Abs(desplazamiento) > 0.01f)
        {
            Vector3 frente = actor.Transform.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
            frente.Normalize();
            lado = (haciaElLado ? Vector3.Cross(Vector3.up, frente) : frente) * desplazamiento;
            lado = LadoLibre(salida, lado, actor.Id);
        }

        // `altura` a 0 (o menos) significa NO SUBIR: esto es una CAÍDA, no un salto. Se usa cuando
        // el personaje ya está en el aire y lo que tiene que hacer es venirse abajo — «cuando baja
        // yo no lo bajaría volando, dejaría que cayera; cuando juegas con el player, si caes hace
        // la animación de falling» (INC-322). Todo lo demás —buscar el suelo de verdad, la pose de
        // aire, la de tomar tierra— ya estaba aquí y vale igual.
        bool soloCae = altura <= 0.001f;
        // En parábola, la mitad del desplazamiento se hace subiendo y la otra mitad cayendo.
        Vector3 ladoArriba = parabola ? lado * 0.5f : lado;
        Vector3 ladoAbajo = parabola ? lado * 0.5f : Vector3.zero;

        Vector3 cima = soloCae
            ? salida + ladoArriba
            : salida + Vector3.up * altura + ladoArriba;

        try
        {
            if (!soloCae)
            {
                if (anim != null && !string.IsNullOrEmpty(poseSubida)) anim.HoldPose(poseSubida);
                yield return Arco(actor, salida, cima, Mathf.Max(0.05f, subida), true, parabola);
            }

            if (anim != null && !string.IsNullOrEmpty(poseAire)) anim.HoldPose(poseAire);

            if (sostener > 0f) yield return new WaitForSeconds(sostener);

            if (caida > 0f)
            {
                // El suelo se BUSCA, no se supone. Es lo que impide aterrizar sobre un tejado.
                Vector3 abajo = cima + ladoAbajo;
                abajo.y = SueloBajo(abajo, salida.y, alturaMaximaDeAterrizaje);

                if (anim != null && !string.IsNullOrEmpty(poseCaida)) anim.HoldPose(poseCaida);

                yield return Arco(actor, cima, abajo, caida, false, parabola);

                if (anim != null)
                {
                    if (!string.IsNullOrEmpty(poseEnElSuelo)) anim.HoldPose(poseEnElSuelo);
                    else anim.ReleasePose(true);
                }
            }
        }
        finally
        {
            if (anim != null) anim.AllowManualMovement = false;
            actor.SyncRotation();

            // El agente vuelve solo si el personaje ha aterrizado. Si se queda arriba, encenderlo
            // lo teletransportaría al NavMesh más cercano — o sea al suelo, de golpe.
            if (agenteEstabaEncendido && caida > 0f && agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(actor.Transform.position);
            }
        }
    }

    /// Mueve al actor de A a B suavizando la entrada o la salida, según suba o baje. Tiempo real:
    /// un salto no se estira con la cámara lenta.
    private static IEnumerator Arco(SequenceActor actor, Vector3 desde, Vector3 hasta,
        float segundos, bool subiendo, bool horizontalConstante = false)
    {
        float t = 0f;
        while (t < segundos)
        {
            if (actor.Transform == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / segundos);

            // Subir frena al final (se agota el impulso); caer acelera. Es la diferencia entre un
            // salto y un ascensor.
            k = subiendo ? 1f - (1f - k) * (1f - k) : k * k;

            Vector3 p = Vector3.Lerp(desde, hasta, k);

            // En parábola, en horizontal va a velocidad constante y solo la altura acelera o
            // frena. Con las dos cosas suavizadas a la vez, un cuerpo lanzado se para en el aire
            // a mitad de camino y arranca otra vez: se lee como un ascensor, no como un golpe.
            if (horizontalConstante)
            {
                float lineal = Mathf.Clamp01(t / segundos);
                Vector3 h = Vector3.Lerp(desde, hasta, lineal);
                p.x = h.x;
                p.z = h.z;
            }

            actor.Transform.position = p;
            yield return null;
        }

        if (actor.Transform != null) actor.Transform.position = hasta;
    }

    /// La Y del suelo bajo un punto. Si no encuentra nada, devuelve la altura de la que salió: es
    /// mejor volver de donde vino que caer al vacío.
    private static float SueloBajo(Vector3 punto, float porDefecto, float margen = -1f)
    {
        // Todos los impactos, no solo el primero: el primero puede ser un tejado o una viga entre
        // la cima del salto y el suelo. Vale el más alto que NO esté por encima de donde despegó
        // — aterrizar más arriba de donde saltaste es, por definición, aterrizar sobre algo que
        // no es el suelo (INC-315).
        int n = Physics.RaycastNonAlloc(punto + Vector3.up * 0.5f, Vector3.down, s_impactos, 200f,
            ~0, QueryTriggerInteraction.Ignore);

        // Cualificada: la constante vive en WalkPathBeat y esto es otra clase.
        float techo = porDefecto + (margen >= 0f ? margen : WalkPathBeat.AlturaQueSePuedeSubir);
        float mejor = float.NegativeInfinity;

        for (int i = 0; i < n; i++)
        {
            var hit = s_impactos[i];
            if (hit.collider == null) continue;
            if (hit.point.y > techo) continue;
            if (hit.point.y > mejor) mejor = hit.point.y;
        }

        return float.IsNegativeInfinity(mejor) ? porDefecto : mejor;
    }
}

/// El sol, donde lo quiere el plano (INC-408).
///
/// «Quiero que en el plano cuando Liora y el Archimago hablan en el río se vea el sol al fondo
/// poniéndose, rollo atardecer.» El ciclo día/noche tiene cuatro franjas y en la de «Atardecer» la
/// luz está casi vertical (rotación X 95°): el color es de atardecer, pero el sol no está en el
/// horizonte, y además cae donde cae, no detrás de nadie.
///
/// Este beat COLOCA el sol durante un rato: a una altura dada sobre el horizonte y en una
/// dirección del mundo (mismo criterio de grados que el eje de acción: 0 = +Z, 90 = +X). Con la
/// cámara del lado de `ladoDeLaCamara`, se pone justo enfrente — detrás de los personajes. No
/// pelea con el ciclo: se aplica en LateUpdate encima de lo que el ciclo haya puesto, y al
/// soltarlo se funde de vuelta, así que la transición a la noche que viene después sale sola.
[Serializable]
public class SolDeFondoBeat : SequenceBeat
{
    [Tooltip("Marcado = colocar el sol. Desmarcado = soltarlo (se funde de vuelta al ciclo).")]
    public bool colocar = true;

    [Tooltip("De qué lado está la cámara, en grados del mundo (el mismo valor que el " +
             "SetActionAxisBeat de la escena). El sol se pone en el lado CONTRARIO: al fondo.")]
    [Range(0f, 360f)]
    public float ladoDeLaCamara = 0f;

    [Tooltip("Grados sobre el horizonte. 4-10 es un sol poniéndose que entra en un plano medio.")]
    public float elevacion = 7f;

    [Tooltip("Segundos que tarda en colocarse (o en soltarse).")]
    public float segundos = 2f;

    [Header("En cuadro (INC-411)")]
    [Tooltip("Colocar el sol respecto a la CÁMARA que rueda, no al mundo: dentro del encuadre, " +
             "cerca del borde de arriba, y bajando por el cuadro hasta que lo tapan las montañas. " +
             "Se recoloca en cada corte. Si está marcado, ladoDeLaCamara y elevacion no se usan.")]
    public bool enCuadro = false;

    [Tooltip("Dónde, a lo ancho del encuadre: 0 = borde izquierdo, 1 = derecho.")]
    [Range(0f, 1f)] public float posicionX = 0.72f;

    [Tooltip("A qué altura del encuadre sale: 0 = borde de abajo, 1 = de arriba. Cerca de arriba, " +
             "que en un plano de conversación siempre es cielo.")]
    [Range(0f, 1f)] public float alturaDeSalida = 0.84f;

    [Tooltip("Hasta qué altura del encuadre baja. Por el camino lo esconden las montañas.")]
    [Range(0f, 1f)] public float alturaDePuesta = 0.42f;

    [Tooltip("Segundos que tarda en ponerse.")]
    public float puesta = 20f;

    public override string Describe() => !colocar
        ? "Sol: devolverlo al ciclo"
        : enCuadro
            ? $"Sol: en cuadro, poniéndose en {puesta:F0} s"
            : $"Sol: al fondo, a {elevacion:F0}° (cámara a {ladoDeLaCamara:F0}°)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var luz = DayNightCycle.Sun;
        if (luz == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SolDeFondo] No hay sol del ciclo día/noche (¿MainWorld cargada?).");
#endif
            yield break;
        }

        var forzado = luz.GetComponent<SolForzado>();
        if (!colocar)
        {
            if (forzado != null) forzado.Soltar(segundos);
            yield break;
        }

        if (forzado == null) forzado = luz.gameObject.AddComponent<SolForzado>();
        ctx?.Player?.RegisterCleanup(() => { if (forzado != null) forzado.Soltar(0f); });

        if (enCuadro)
        {
            forzado.ColocarEnCuadro(posicionX, alturaDeSalida, alturaDePuesta, puesta, segundos);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SolDeFondo] Sol en cuadro (x={posicionX:F2}), poniéndose en {puesta:F0} s.");
#endif
            yield break;
        }

        // El sol, en el lado contrario a la cámara; la luz apunta DESDE él, así que su forward
        // va hacia la cámara y hacia abajo.
        float yawSol = ladoDeLaCamara + 180f;
        Vector3 haciaElSol = Quaternion.Euler(-elevacion, yawSol, 0f) * Vector3.forward;
        forzado.Colocar(Quaternion.LookRotation(-haciaElSol, Vector3.up), segundos);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SolDeFondo] Sol a {elevacion:F0}° sobre el horizonte, hacia {Mathf.Repeat(yawSol, 360f):F0}°.");
#endif
    }
}
