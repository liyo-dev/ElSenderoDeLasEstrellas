using System.Collections;
using UnityEngine;
using Core.InputGlyphs;
using Game.NPC.Common;

/// La mecánica de juego del Despertar de la Estrella: el proyectil entrante, el panic input y el
/// contraataque real de Will.
///
/// ── Qué hay aquí y qué no ─────────────────────────────────────────────────────────────────────
/// Aquí está SOLO lo que es mecánica y no se repite en ninguna otra secuencia del juego. Todo el
/// montaje — planos, bocadillos, caras, gestos, cámara lenta, fundidos, música, orden de las
/// fases, el cierre y el skip — está en SEQ_StarAwakening.asset y lo ejecuta el SequencePlayer,
/// que es el mismo para todas las secuencias.
///
/// Esta es la parte que costaba: el sequencer viejo mezclaba las dos cosas en 757 líneas, así que
/// cambiar una frase de diálogo o un plano obligaba a tocar el mismo archivo donde vive el
/// spawneo de proyectiles. Ahora son dos cosas separadas y solo esta necesita recompilar.
///
/// ── Cómo se comunica con el asset ─────────────────────────────────────────────────────────────
///   · El asset invoca rutinas por nombre, con beats de tipo "Mecánica".
///   · Este módulo deja marcas (ctx.SetFlag) que las fases siguientes miran para ramificarse.
///   · Y registra el proyectil como actor ("Proyectil"), para que los beats de cámara puedan
///     encuadrarlo y Will pueda girarse hacia él sin que el asset sepa nada de proyectiles.
[AddComponentMenu("Cinematics/Módulos/Despertar de la Estrella")]
[DisallowMultipleComponent]
public class StarAwakeningModule : SequenceModule
{
    /// El id con el que el asset se refiere al proyectil entrante. Es el que se escribe en los
    /// beats de cámara ("siguiendo a Proyectil") y en los de mirar.
    public const string ProjectileActorId = "Proyectil";

    /// La marca que queda puesta si el jugador supera el panic input. Las fases del desenlace
    /// bueno llevan onlyIfFlag con este nombre; las del malo, skipIfFlag.
    public const string FlagPanicSuperado = "panicSuperado";

    [Header("Proyectil entrante")]
    [SerializeField] private SlowMotionFireProjectile incomingProjectilePrefab;
    [Tooltip("Desde dónde llega el proyectil. Si se deja vacío, se calcula con 'spawnDistance' y " +
             "'spawnAngle' alrededor de Will — que es lo recomendable, porque así funciona " +
             "dondequiera que el jugador esté cuando arranque la escena, igual que los planos.")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("A qué distancia de Will aparece el proyectil, en metros.")]
    [SerializeField] private float spawnDistance = 18f;
    [Tooltip("Desde qué ángulo llega, medido respecto a hacia dónde mira Will: 0 = de frente, " +
             "90 = por su derecha, 180 = por su espalda.\n\n" +
             "Va por la ESPALDA a propósito (160, un poco escorado para que no sea exactamente " +
             "detrás y se le vea entrar en cuadro). Es lo que hace que la escena funcione: Will no " +
             "puede verlo, así que el aviso de Eldran es lo único que hay, y el giro de Will es un " +
             "momento de verdad y no un trámite. Con el proyectil de frente, Will estaría mirándolo " +
             "todo el rato sin reaccionar.")]
    [SerializeField] private float spawnAngle = 160f;
    [Tooltip("Altura a la que vuela, sobre el suelo de Will.")]
    [SerializeField] private float spawnHeight = 1.5f;
    [Tooltip("Segundos reales que puede vivir el proyectil durante la cinemática. Sustituye al " +
             "tope del prefab, que está calibrado para combate y aquí se queda corto.")]
    [SerializeField] private float cinematicProjectileLifetime = 120f;
    [SerializeField] private GameObject explosionVFX;

    [Header("Contraataque de Will")]
    [Tooltip("MagicProjectileSpawner del jugador. Se dispara de verdad, con su sistema real. " +
             "Opcional. Se deja vacío a propósito: Will lo instancia SpawnManager en runtime, así " +
             "que no existe en la escena a la hora de arrastrarlo. Se resuelve solo desde el actor " +
             "Player al arrancar la secuencia.")]
    [SerializeField] private MagicProjectileSpawner playerSpawner;
    [SerializeField] private MagicSlot castSlot = MagicSlot.Right;
    [Tooltip("Hechizo de reserva: se usa si el jugador no tiene nada equipado en ese slot o no ha " +
             "desbloqueado la magia todavía. Nunca falla, ignora maná y recargas.")]
    [SerializeField] private MagicSpellSO cinematicSpellFallback;
    [Tooltip("Punto del que sale el hechizo (normalmente la mano de Will). Vacío = su pivot.")]
    [SerializeField] private Transform willCastOrigin;
    [Tooltip("Cuanto se sube el punto del que sale el hechizo, en metros. Sin esto sale a la altura " +
             "de la mano de Will, que con estas proporciones de personaje queda casi a ras de suelo " +
             "y el hechizo se ve rastrero. Subirlo hasta que salga a la altura del pecho.")]
    [SerializeField] private float castHeightOffset = 0.45f;
    [Tooltip("Cuanto se adelanta el punto del que sale el hechizo en la direccion a la que mira " +
             "Will, en metros. Sin esto sale pegado al origen (la mano/arma), que con weapon_r de " +
             "referencia queda casi encima del propio Will. Adelantarlo para que nazca ya despegado " +
             "del cuerpo.")]
    [SerializeField] private float castForwardOffset = 0.5f;

    [SerializeField] private string castAnimState = "MagicRight";
    [SerializeField] private int willUpperBodyLayer = 1;
    [Tooltip("Segundos de tiempo ESCALADO entre que arranca la animación de lanzamiento y sale el " +
             "proyectil. En tiempo escalado a propósito: así sigue cuadrando con el frame de " +
             "release de la animación también en cámara lenta.")]
    [SerializeField] private float castAnimDelay = 0.3f;
    [Tooltip("Segundos reales máximos esperando a que los dos proyectiles choquen antes de forzar " +
             "la explosión. Es una red de seguridad: sin ella, un proyectil que falla el blanco " +
             "deja la secuencia colgada y con ella el input del jugador.")]
    [SerializeField] private float collisionWaitUnscaled = 3f;

    [Header("Panic input")]
    [SerializeField] private PanicInputDetector panicInputDetector;
    [SerializeField] private PanicInputUI panicInputUI;
    [Tooltip("Último respaldo del icono del botón, si InputGlyphService no devuelve nada.")]
    [SerializeField] private Sprite panicButtonSprite;
    [Tooltip("Respaldo intermedio. OJO: este set solo tiene el icono de Interactuar (E), que NO es " +
             "el botón del panic input — panicAction está atado a AttackMagicWest. El icono bueno " +
             "lo da InputGlyphService; esto es solo para que no salga un hueco si falla.")]
    [SerializeField] private InteractionHintIconSet interactIconSet;

    [Header("Efectos de shock")]
    [SerializeField] private ShockEffectsController shockEffects;

    [Header("Tensión mientras el proyectil se acerca")]
    [Tooltip("Sacudida nada más aparecer el proyectil (sutil).")]
    [SerializeField] private float approachShakeMin = 0.02f;
    [Tooltip("Sacudida justo antes del impacto. Mantenerla por debajo de la de la explosión: si el " +
             "aviso golpea más fuerte que el golpe, el golpe decepciona.")]
    [SerializeField] private float approachShakeMax = 0.15f;
    [SerializeField] private float approachShakeRampSeconds = 4f;
    [SerializeField] private float approachShakeInterval = 0.15f;

    // ── Estado ────────────────────────────────────────────────────────────────

    private SlowMotionFireProjectile _projectile;
    private GameObject _pendingFireball;
    private Transform _collisionPoint;
    private bool _collisionTriggered;
    private bool _panicResolved;
    private bool _panicSucceeded;
    private Coroutine _tension;
    private SequenceContext _ctx;

    public override string[] Routines => new[]
    {
        "LanzarProyectil",
        "PanicInput",
        "WillContraataca",
        "Aturdimiento",
        "Tinnitus",
        "RetirarProyectil",
        "PausarProyectil",
        "ReanudarProyectil",
    };

    public override IEnumerator Run(string routine, SequenceContext ctx)
    {
        _ctx = ctx;

        return routine.Trim().ToLowerInvariant() switch
        {
            "lanzarproyectil" => Co_LanzarProyectil(ctx),
            "panicinput" => Co_PanicInput(ctx),
            "willcontraataca" => Co_WillContraataca(ctx),
            "pausarproyectil" => Co_PausarProyectil(),
            "reanudarproyectil" => Co_ReanudarProyectil(),
            "aturdimiento" => Co_Aturdimiento(),
            "tinnitus" => Co_Tinnitus(),
            "retirarproyectil" => Co_RetirarProyectil(ctx),
            _ => null,
        };
    }

    // ── Rutinas ───────────────────────────────────────────────────────────────

    /// Trae el proyectil enemigo y lo lanza contra Will. No espera a nada: a partir de aquí el
    /// proyectil vuela solo mientras la escena sigue.
    ///
    /// La posición de salida se calcula alrededor de Will en vez de ser un punto fijo del mapa —
    /// mismo motivo que los planos de cámara: un punto fijo deja de valer en cuanto la escena
    /// ocurre en otro sitio, y eso ya pasó una vez ("se ve por debajo del mundo", 14 sept 2026).
    private IEnumerator Co_LanzarProyectil(SequenceContext ctx)
    {
        var will = ctx.GetActor(SequenceActor.PlayerId);
        if (will?.Transform == null || incomingProjectilePrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[StarAwakeningModule] No se puede lanzar el proyectil: falta Will o el prefab.");
#endif
            yield break;
        }

        Vector3 origin;
        if (projectileSpawnPoint != null)
        {
            origin = projectileSpawnPoint.position;
        }
        else
        {
            Vector3 dir = Quaternion.AngleAxis(spawnAngle, Vector3.up) * will.Transform.forward;
            origin = will.Transform.position + dir.normalized * spawnDistance + Vector3.up * spawnHeight;
        }

        Vector3 target = will.Transform.position + Vector3.up * (will.EyeHeight * 0.85f);
        Quaternion rotation = Quaternion.LookRotation((target - origin).normalized, Vector3.up);

        _collisionTriggered = false;
        _projectile = Instantiate(incomingProjectilePrefab, origin, rotation);
        _projectile.OnHitByPlayerFireball += OnPhysicsCollision;

        // El tope de vida del prefab está pensado para combate (12 s reales). Aquí el proyectil
        // vive una escena entera en cámara lenta y encima espera a que el jugador reaccione, así
        // que con el tope de combate se autodestruye a mitad de secuencia — sin explosión, sin
        // aviso, y dejando al jugador sin nada a lo que reaccionar.
        _projectile.SetMaxLifetime(cinematicProjectileLifetime);

        _projectile.Launch(will.Transform);

        AudioService.Instance?.PlaySFX("Star_ProjectileIncoming", 1f, origin);

        // El proyectil pasa a ser un actor más de la escena: a partir de aquí el asset puede
        // encuadrarlo ("siguiendo a Proyectil") y Will puede girarse hacia él, sin que ningún
        // beat tenga que saber qué es un proyectil.
        ctx.RegisterActor(ProjectileActorId, _projectile.transform);

        if (_tension == null) _tension = StartCoroutine(Co_Tension());
    }

    /// Da el control al jugador: aparece el botón y hay que pulsarlo a tiempo. Deja puesta la
    /// marca 'panicSuperado' con el resultado y devuelve el control a la secuencia.
    private IEnumerator Co_PanicInput(SequenceContext ctx)
    {
        if (panicInputDetector == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[StarAwakeningModule] Sin PanicInputDetector asignado: no hay forma de " +
                "superar la prueba, así que se da por buena para no dejar la escena colgada.");
#endif
            ctx.SetFlag(FlagPanicSuperado, true);
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico (17 sep 2026). La prueba se quedaba colgada aquí sin decir una palabra: la
        // secuencia entraba en la fase del botón y no volvía a pasar nada. En vez de adivinar, esto
        // dice en una línea qué referencias hay y, sobre todo, si el detector está ACTIVO — porque
        // su cuenta atrás vive en un Update(), y un GameObject desactivado no ejecuta Update: ni
        // aparece el botón ni salta el fallo por tiempo, así que la espera no termina nunca.
        Debug.Log($"[StarAwakeningModule] Arrancando panic input — detector='{panicInputDetector.name}' " +
            $"(componente {(panicInputDetector.enabled ? "activo" : "DESACTIVADO")}, " +
            $"GameObject {(panicInputDetector.gameObject.activeInHierarchy ? "activo" : "DESACTIVADO EN JERARQUÍA")}), " +
            $"UI={(panicInputUI != null ? panicInputUI.name : "se creará ahora")}, " +
            $"spawner={(ResolverSpawner(ctx) != null ? "sí" : "NO ENCONTRADO")}.");

        if (!panicInputDetector.gameObject.activeInHierarchy || !panicInputDetector.enabled)
            Debug.LogError("[StarAwakeningModule] El PanicInputDetector NO está activo. Su cuenta " +
                "atrás corre en Update(), así que no va a escuchar el botón ni a fallar por tiempo: " +
                "el botón de la X no llega a salir. ARREGLO: ejecuta 'El Sendero ▸ Secuencias ▸ " +
                "Montar Despertar de la Estrella (sistema nuevo)' y guarda la escena — el montaje " +
                "detecta el detector varado en un GameObject apagado, lo copia a este mismo objeto " +
                "con sus ajustes y reapunta la referencia.", panicInputDetector);

        // INC-224 (17 sep 2026): el detector puede estar perfectamente activo y el botón seguir
        // sin verse — 'panicInputUI' es un Canvas aparte y puede quedarse igual de varado que el
        // detector estaba en INC-221. Aquí no hay excepción que avisar: 'Activate()' más abajo se
        // ejecuta entero sin fallar, solo que sobre un Canvas que Unity no dibuja porque algún
        // antecesor suyo está apagado.
        if (panicInputUI != null && !panicInputUI.gameObject.activeInHierarchy)
            Debug.LogError("[StarAwakeningModule] El PanicInputUI existe pero su GameObject (o " +
                "algún antecesor suyo) está DESACTIVADO EN JERARQUÍA. Activate() se va a ejecutar " +
                "sin dar ningún error, pero el icono de la X NO se va a ver. ARREGLO: ejecuta " +
                "'El Sendero ▸ Secuencias ▸ Montar Despertar de la Estrella (sistema nuevo)' y " +
                "guarda la escena — el montaje reparenta ese Canvas a un sitio activo.",
                panicInputUI);
#endif

        // El spawner del jugador se apaga mientras dura la prueba: si no, el mismo botón del panic
        // input le dispara un hechizo de verdad que revienta el proyectil enemigo antes de tiempo.
        var spawner = ResolverSpawner(ctx);
        if (spawner != null) spawner.enabled = false;

        // El icono correcto lo da InputGlyphService por familia de dispositivo activa. El set de
        // interacción solo tiene el icono de "E", que es de otra acción distinta (ver el campo).
        var icon = InputGlyphService.GetSprite(InputGlyphNames.West)
                   ?? (interactIconSet != null ? interactIconSet.GetSprite(InputGlyphService.CurrentFamily) : null)
                   ?? panicButtonSprite;

        if (panicInputUI == null) panicInputUI = PanicInputUI.GetOrCreate(icon);
        else panicInputUI.SetIcon(icon);

        _panicResolved = false;
        _panicSucceeded = false;

        panicInputDetector.OnSuccess += OnPanicSuccess;
        panicInputDetector.OnFailure += OnPanicFailure;

        panicInputUI?.Activate(panicInputDetector);
        panicInputDetector.StartListening();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[StarAwakeningModule] Panic input escuchando. Quedan " +
            $"{panicInputDetector.TimeRemaining:F1} s reales para pulsar el botón.");

        // Diagnóstico (17 sept 2026): Raúl ve el icono de la X ocupando toda la
        // pantalla tras el rescate de INC-224 v2. No se puede inspeccionar el Canvas real desde
        // fuera del Editor (vive en la escena binaria), así que esto deja en el log, cada vez
        // que se activa, exactamente los datos que hacen falta para saber si es un problema de
        // escala (reparentar cambió el lossyScale del Canvas) o de modo de render (un Canvas
        // que se quedó anidado bajo otro y, al reparentarlo, pasó a ser raíz con su propio
        // CanvasScaler sin configurar).
        if (panicInputUI != null)
        {
            var canvas = panicInputUI.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                Debug.Log($"[StarAwakeningModule] Diagnóstico tamaño PanicInputUI — " +
                    $"Canvas='{canvas.name}' renderMode={canvas.renderMode} " +
                    $"isRootCanvas={canvas.isRootCanvas} sortingOrder={canvas.sortingOrder} " +
                    $"transform.localScale={canvas.transform.localScale} " +
                    $"transform.lossyScale={canvas.transform.lossyScale} " +
                    $"rect={canvas.GetComponent<RectTransform>()?.rect} " +
                    $"scaler={(scaler != null ? $"modo={scaler.uiScaleMode} refRes={scaler.referenceResolution} matchWidthOrHeight={scaler.matchWidthOrHeight}" : "SIN CanvasScaler")}");
            }
            else
            {
                Debug.Log("[StarAwakeningModule] Diagnóstico tamaño PanicInputUI — " +
                    "no se encontró ningún Canvas en sus antecesores (raro: debería haber uno).");
            }
        }
#endif

        // Tope de seguridad. El detector debería resolver solo —acierto, o fallo al agotarse su
        // ventana— pero si por lo que sea no lo hace, esta espera no tenía salida: la secuencia se
        // queda colgada con el input bloqueado y la única salida es cerrar el juego. Pasó el 17
        // sep. El tope da margen de sobra sobre la ventana real del detector y, si salta, lo dice.
        float limite = Mathf.Max(panicInputDetector.TimeRemaining, 1f) + 5f;
        float esperado = 0f;

        while (!_panicResolved && esperado < limite)
        {
            yield return null;
            esperado += Time.unscaledDeltaTime;
        }

        if (!_panicResolved)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[StarAwakeningModule] El panic input no ha resuelto en {limite:F0} s. " +
                "Lo normal es que el PanicInputDetector no esté activo (su cuenta atrás corre en " +
                "Update) o que su 'panicAction' esté sin asignar. Se da la prueba por FALLADA para " +
                "no dejar la secuencia colgada, que es lo que pasaba antes de este tope.");
#endif
            _panicSucceeded = false;
        }

        UnsubscribePanic();
        panicInputDetector.StopListening();
        panicInputUI?.Deactivate();

        ctx.SetFlag(FlagPanicSuperado, _panicSucceeded);
    }

    /// Will devuelve el golpe con su sistema de magia real, y los dos proyectiles chocan.
    /// Resuelve el lanzador de hechizos de Will.
    ///
    /// FIX (17 sep 2026): este campo llevaba VACÍO desde siempre — también en el sequencer viejo,
    /// según avisó el montaje —, y vacío significa que el contraataque de Will NO EXISTE: la
    /// animación de lanzar se reproduce, pero no sale ningún hechizo, la espera de colisión agota
    /// sus 3 segundos y el proyectil enemigo acaba alcanzando a Will, que se come 10 de daño en
    /// mitad de una cinemática.
    ///
    /// Y no se podía arreglar arrastrándolo en el Inspector: a Will lo instancia SpawnManager en
    /// runtime, así que a la hora de montar la escena no hay ningún objeto que arrastrar. Por eso
    /// se busca en el actor Player, que es quien sí existe cuando la secuencia arranca.
    private MagicProjectileSpawner ResolverSpawner(SequenceContext ctx)
    {
        if (playerSpawner != null) return playerSpawner;

        var will = ctx?.GetActor(SequenceActor.PlayerId);
        if (will?.Transform == null) return null;

        playerSpawner = will.Transform.GetComponentInChildren<MagicProjectileSpawner>(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (playerSpawner == null)
            Debug.LogError("[StarAwakeningModule] No encuentro el MagicProjectileSpawner de Will ni " +
                "en el Inspector ni colgando del actor Player. Sin él Will no contraataca: la " +
                "animación se ve, pero no sale ningún hechizo y el proyectil enemigo le acaba " +
                "dando.", this);
#endif
        return playerSpawner;
    }

    private IEnumerator Co_WillContraataca(SequenceContext ctx)
    {
        var will = ctx.GetActor(SequenceActor.PlayerId);
        if (will?.Transform == null) yield break;

        // El spawner sigue apagado a propósito: SpawnForCinematic no lo necesita, y volver a
        // encenderlo aquí haría que un botón mantenido del panic input disparase un hechizo
        // accidental que destruye el proyectil enemigo antes del que sí cuenta.

        Transform willT = will.Transform;
        Vector3 castDir = willT.forward;

        if (_projectile != null)
        {
            Vector3 toProjectile = _projectile.transform.position - willT.position;
            toProjectile.y = 0f;
            if (toProjectile.sqrMagnitude > 0.001f)
            {
                castDir = toProjectile.normalized;
                willT.rotation = Quaternion.LookRotation(castDir, Vector3.up);
            }
        }

        var animator = willT.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetLayerWeight(willUpperBodyLayer, 1f);
            animator.Play(castAnimState, willUpperBodyLayer);
        }

        AudioService.Instance?.PlaySFX("Star_SpellCast", 1f, willT.position);

        if (castAnimDelay > 0f) yield return new WaitForSeconds(castAnimDelay);
        else yield return null;

        // El spawner recibe un Transform, no una posicion, asi que para poder subir el punto de
        // salida se usa un transform auxiliar colocado sobre el original. Es el mismo truco que el
        // punto de impacto de mas abajo.
        Transform baseOrigin = willCastOrigin != null ? willCastOrigin : willT;
        Transform castOrigin = EnsureCastOrigin();
        // OJO: el adelanto usa 'castDir' (hacia donde mira Will de verdad / hacia el proyectil),
        // no 'baseOrigin.forward' -- ese es el eje local del hueso/arma (weapon_r), que no tiene
        // por que apuntar hacia delante del personaje.
        castOrigin.SetPositionAndRotation(
            baseOrigin.position + Vector3.up * castHeightOffset + castDir * castForwardOffset,
            baseOrigin.rotation);

        var spawner = ResolverSpawner(ctx);
        GameObject fireball = spawner != null
            ? spawner.SpawnForCinematic(castSlot, cinematicSpellFallback, castOrigin, castDir)
            : null;
        _pendingFireball = fireball;

        if (fireball != null && fireball.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
        {
            float speed = rb.linearVelocity.magnitude;
            if (speed < 0.1f) speed = 10f;
            rb.linearVelocity = castDir * speed;
        }

        // Espera a que choquen, con tope de tiempo: un proyectil que falla el blanco no puede
        // dejar la secuencia — y con ella el input del jugador — colgada para siempre.
        float elapsed = 0f;
        while (elapsed < collisionWaitUnscaled && !_collisionTriggered)
        {
            if (fireball != null && _projectile != null)
            {
                float dist = Vector3.Distance(fireball.transform.position, _projectile.transform.position);
                if (dist < 1.5f)
                {
                    EnsureCollisionPoint().position =
                        (fireball.transform.position + _projectile.transform.position) * 0.5f;
                    TriggerExplosion();
                    break;
                }
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_collisionTriggered) TriggerExplosion();

        if (fireball != null) Destroy(fireball);
        _pendingFireball = null;
    }

    /// Congela el proyectil donde esté.
    ///
    /// Sirve para los tramos en los que la cámara está en otra cosa — Eldran huyendo, el primer
    /// plano del despertar — y no queremos que la amenaza se coma la distancia mientras nadie la
    /// mira. Es una licencia de puesta en escena de toda la vida: el peligro espera a que la
    /// escena esté lista para él. Sin esto, alargar cualquier momento anterior significa que el
    /// proyectil llega antes de tiempo y hay que recortar la escena para que quepa.
    ///
    /// Pause() además le quita el collider, así que tampoco puede detonar por accidente.
    private IEnumerator Co_PausarProyectil()
    {
        _projectile?.Pause();
        yield break;
    }

    /// Lo vuelve a poner en marcha.
    private IEnumerator Co_ReanudarProyectil()
    {
        _projectile?.Resume();
        yield break;
    }

    /// Deja los efectos de shock (visión estrechada, sordera) en su punto máximo.
    private IEnumerator Co_Aturdimiento()
    {
        shockEffects?.HoldAt(1f);
        yield break;
    }

    /// El pitido en los oídos del remate.
    private IEnumerator Co_Tinnitus()
    {
        shockEffects?.PlayTinnitus();
        yield break;
    }

    /// Retira el proyectil sin explosión. Es lo que pasa en la rama de fallo: la escena corta a
    /// negro y el proyectil simplemente deja de existir.
    private IEnumerator Co_RetirarProyectil(SequenceContext ctx)
    {
        DespawnProjectile(ctx);
        if (playerSpawner != null) playerSpawner.enabled = true;
        yield break;
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    /// Sacudida sutil y creciente mientras el proyectil está en vuelo, para que la amenaza se
    /// sienta acercarse en vez de que el único golpe de cámara sea el de la explosión.
    private IEnumerator Co_Tension()
    {
        float elapsed = 0f;
        while (_projectile != null && !_collisionTriggered)
        {
            elapsed += approachShakeInterval;
            float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, approachShakeRampSeconds));
            Sendero.Core.Feedback.FeedbackService.CameraShake(
                Mathf.Lerp(approachShakeMin, approachShakeMax, k), approachShakeInterval * 1.5f);
            yield return new WaitForSecondsRealtime(approachShakeInterval);
        }
        _tension = null;
    }

    private void OnPanicSuccess()
    {
        _panicSucceeded = true;
        _panicResolved = true;
    }

    private void OnPanicFailure()
    {
        _panicSucceeded = false;
        _panicResolved = true;
    }

    private void OnPhysicsCollision()
    {
        if (_projectile != null) EnsureCollisionPoint().position = _projectile.transform.position;
        TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        if (_collisionTriggered) return;
        _collisionTriggered = true;

        Vector3 point = _collisionPoint != null ? _collisionPoint.position : transform.position;

        // VFX de un solo uso SIEMPRE por el pool, nunca Instantiate + Destroy (CLAUDE.md § 2).
        if (explosionVFX != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(explosionVFX, point, Quaternion.identity, 3f);

        AudioService.Instance?.PlaySFX("Star_Collision", 1f, point);

        if (_projectile != null)
        {
            _projectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            _projectile.ForceCollide();
            _projectile = null;
            _ctx?.UnregisterActor(ProjectileActorId);
        }
    }

    private Transform _castOrigin;

    private Transform EnsureCastOrigin()
    {
        if (_castOrigin == null)
        {
            var go = new GameObject("__OrigenDelHechizo") { hideFlags = HideFlags.HideAndDontSave };
            _castOrigin = go.transform;
        }
        return _castOrigin;
    }

    private Transform EnsureCollisionPoint()
    {
        if (_collisionPoint == null)
        {
            var go = new GameObject("__PuntoDeImpacto") { hideFlags = HideFlags.HideAndDontSave };
            _collisionPoint = go.transform;
        }
        return _collisionPoint;
    }

    private void DespawnProjectile(SequenceContext ctx)
    {
        if (_projectile == null) return;
        _projectile.OnHitByPlayerFireball -= OnPhysicsCollision;
        Destroy(_projectile.gameObject);
        _projectile = null;
        ctx?.UnregisterActor(ProjectileActorId);
    }

    private void UnsubscribePanic()
    {
        if (panicInputDetector == null) return;
        panicInputDetector.OnSuccess -= OnPanicSuccess;
        panicInputDetector.OnFailure -= OnPanicFailure;
    }

    /// Se llama SIEMPRE al cerrar: final normal, skip, o si algo revienta por el camino.
    ///
    /// El sequencer viejo documenta por qué esto importa tanto: sus corrutinas se lanzaban sueltas
    /// desde los callbacks del panic input, así que el StopCoroutine del skip no las tocaba y
    /// seguían vivas en pleno gameplay — spawneando un hechizo real y levantando las dos señales
    /// de salida del grafo a la vez. Aquí todo cuelga del módulo y se corta de una vez.
    public override void OnSequenceCleanup()
    {
        StopAllCoroutines();
        _tension = null;

        UnsubscribePanic();

        // Y cerrar la prueba de verdad, no solo dejar de escucharla. Sin esto, saltar la cinemática
        // durante el panic input dejaba su overlay (una pantalla oscurecida al 55% con el icono del
        // botón y la barra) encima del gameplay hasta que el detector agotaba su ventana, y la
        // acción de magia forzada a habilitada mientras tanto. Los dos métodos son idempotentes.
        panicInputDetector?.StopListening();
        panicInputUI?.Deactivate();

        if (_pendingFireball != null) { Destroy(_pendingFireball); _pendingFireball = null; }
        DespawnProjectile(_ctx);

        if (playerSpawner != null) playerSpawner.enabled = true;
        shockEffects?.ForceEnd();

        // La capa de cuerpo superior se queda abierta si el skip corta durante el lanzamiento, y
        // Will se queda con el brazo levantado el resto de la partida.
        var player = PlayerLocator.ResolvePlayer();
        var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (animator != null && willUpperBodyLayer < animator.layerCount)
            animator.SetLayerWeight(willUpperBodyLayer, 0f);

        _panicResolved = false;
        _collisionTriggered = false;
        _ctx = null;
    }

    private void OnDestroy()
    {
        if (_collisionPoint != null) Destroy(_collisionPoint.gameObject);
        if (_castOrigin != null) Destroy(_castOrigin.gameObject);
    }
}
