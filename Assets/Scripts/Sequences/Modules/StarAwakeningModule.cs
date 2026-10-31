using System.Collections;
using UnityEngine;
using Game.NPC.Common;

/// La mecánica de juego del Despertar de la Estrella: el proyectil entrante, el contraataque real
/// de Will y, si no reacciona a tiempo, el impacto de la bola sobre él. La prueba del botón la
/// pide el guion con InputPromptBeat, que deja la marca «panicSuperado».
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
///   · Y registra el proyectil como actor ("Proyectil"), para que los beats de cámara puedan
///     encuadrarlo y Will pueda girarse hacia él sin que el asset sepa nada de proyectiles.
[AddComponentMenu("Cinematics/Módulos/Despertar de la Estrella")]
[DisallowMultipleComponent]
public class StarAwakeningModule : SequenceModule
{
    /// El id con el que el asset se refiere al proyectil entrante. Es el que se escribe en los
    /// beats de cámara ("siguiendo a Proyectil") y en los de mirar.
    public const string ProjectileActorId = "Proyectil";

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
    [Tooltip("Altura a la que vuela, sobre el suelo de Will. Si la bola es tan grande que a esta " +
             "altura tocaría el suelo, se sube lo justo para que no lo toque.")]
    [SerializeField] private float spawnHeight = 1.5f;
    [Tooltip("Si el camino hasta Will está tapado (una casa, un puesto, una farola), se prueban " +
             "ángulos a un lado y a otro de 'spawnAngle', de este tanto en este tanto, hasta " +
             "encontrar uno despejado. 0 = no buscar.")]
    [SerializeField] private float clearPathStepDegrees = 20f;
    [Tooltip("Cuántos pasos a cada lado se prueban como mucho.")]
    [SerializeField] private int clearPathMaxSteps = 4;
    [Tooltip("Segundos reales que puede vivir el proyectil durante la cinemática. Sustituye al " +
             "tope del prefab, que está calibrado para combate y aquí se queda corto.")]
    [SerializeField] private float cinematicProjectileLifetime = 120f;
    [SerializeField] private GameObject explosionVFX;
    [Tooltip("Segundos reales máximos esperando a que la bola alcance a Will cuando no reacciona. " +
             "Si se cumplen, explota donde esté: la escena no puede quedarse esperando.")]
    [SerializeField] private float impactWaitUnscaled = 6f;

    [Header("Contraataque de Will")]
    [Tooltip("MagicProjectileSpawner del jugador. Se dispara de verdad, con su sistema real. " +
             "Opcional. Se deja vacío a propósito: Will lo instancia SpawnManager en runtime, así " +
             "que no existe en la escena a la hora de arrastrarlo. Se resuelve solo desde el actor " +
             "Player al arrancar la secuencia.")]
    [SerializeField] private MagicProjectileSpawner playerSpawner;
    [Tooltip("Mano desde la que sale el hechizo de la cinemática.")]
    [SerializeField] private CastHand castSlot = CastHand.Right;
    [Tooltip("Hechizo que lanza Will en la cinemática. Ignora maná, enfriamientos y lo que tenga equipado.")]
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
    [Tooltip("Marca del escenario (o PUNTO del guion) donde chocan la bola de fuego y el proyectil.")]
    [SerializeField] private string clashMarkName = "choque";
    [Tooltip("Lo que tardan las dos bolas en encontrarse, en segundos reales.")]
    [SerializeField] private float clashTravelUnscaled = 0.7f;

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
    private float _flightHeight;
    private GameObject _pendingFireball;
    private Transform _collisionPoint;
    private bool _collisionTriggered;
    private Coroutine _tension;
    private SequenceContext _ctx;

    public override string[] Routines => new[]
    {
        "LanzarProyectil",
        "SinMagiaDeWill",
        "WillContraataca",
        "Aturdimiento",
        "Tinnitus",
        "ImpactoEnWill",
        "PausarProyectil",
        "ReanudarProyectil",
    };

    public override IEnumerator Run(string routine, SequenceContext ctx)
    {
        _ctx = ctx;

        return routine.Trim().ToLowerInvariant() switch
        {
            "lanzarproyectil" => Co_LanzarProyectil(ctx),
            "sinmagiadewill" => Co_SinMagiaDeWill(ctx),
            "willcontraataca" => Co_WillContraataca(ctx),
            "pausarproyectil" => Co_PausarProyectil(),
            "reanudarproyectil" => Co_ReanudarProyectil(),
            "aturdimiento" => Co_Aturdimiento(),
            "tinnitus" => Co_Tinnitus(),
            "impactoenwill" => Co_ImpactoEnWill(ctx),
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
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[StarAwakeningModule] No se puede lanzar el proyectil: falta Will o el prefab.");
#endif
            yield break;
        }

        _collisionTriggered = false;
        _projectile = Instantiate(incomingProjectilePrefab, will.Transform.position, Quaternion.identity);
        _projectile.OnHitByPlayerFireball += OnPhysicsCollision;

        // La altura depende del tamaño de la bola: el centro va lo bastante alto para que la
        // parte de abajo no toque el suelo, ni al salir ni al llegar a Will. Ver INC-458.
        float flightHeight = Mathf.Max(spawnHeight, will.EyeHeight * 0.85f, _projectile.MinCenterHeight);
        _flightHeight = flightHeight;
        _projectile.SetAimHeight(flightHeight);

        Vector3 target = will.Transform.position + Vector3.up * flightHeight;
        // Con guion horneado (INC-598) la salida es un punto del guion, «bola_salida»: así las
        // cámaras, que se calculan en el Editor, saben por dónde viene la bola.
        Transform marcaSalida = ctx.Stage != null ? ctx.Stage.GetMark("bola_salida") : null;
        Vector3 origin = marcaSalida != null
            ? new Vector3(marcaSalida.position.x, will.Transform.position.y + flightHeight, marcaSalida.position.z)
            : projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : BuscarSalidaDespejada(will.Transform, target, flightHeight, _projectile.Radius);

        _projectile.transform.SetPositionAndRotation(origin,
            Quaternion.LookRotation((target - origin).normalized, Vector3.up));
        _projectile.KeepAboveGround();
        origin = _projectile.transform.position;

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

    /// Punto de salida alrededor de Will a 'spawnDistance'. Empieza por 'spawnAngle' y, si entre
    /// ese punto y Will hay decorado, prueba ángulos a un lado y a otro hasta dar con un camino
    /// libre. Si no hay ninguno, se queda con 'spawnAngle'. Ver INC-458.
    private Vector3 BuscarSalidaDespejada(Transform will, Vector3 target, float flightHeight, float radius)
    {
        Vector3 PuntoEn(float angle)
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * will.forward;
            return will.position + dir.normalized * spawnDistance + Vector3.up * flightHeight;
        }

        Vector3 first = PuntoEn(spawnAngle);
        if (clearPathStepDegrees <= 0f || clearPathMaxSteps <= 0) return first;

        int mask = ~LayerMask.GetMask("Ignore Raycast", "UI");
        for (int step = 0; step <= clearPathMaxSteps; step++)
        {
            for (int side = 0; side < (step == 0 ? 1 : 2); side++)
            {
                float angle = spawnAngle + (side == 0 ? step : -step) * clearPathStepDegrees;
                Vector3 from = PuntoEn(angle);
                if (CaminoDespejado(from, target, radius, will, mask)) return from;
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning("[StarAwakeningModule] No hay ningún ángulo con el camino despejado hasta " +
            "Will; la bola sale por 'spawnAngle' y puede atravesar decorado.");
#endif
        return first;
    }

    private static readonly RaycastHit[] s_pathHits = new RaycastHit[16];

    /// Camino libre para la bola: un SphereCast desde la salida hasta poco antes de Will que
    /// ignora a Will, a los personajes y los triggers. Se usa medio radio: la bola es un efecto
    /// y rozar una rama con el borde no se nota.
    private static bool CaminoDespejado(Vector3 from, Vector3 to, float radius, Transform will, int mask)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude - radius - 1f;
        if (dist <= 0f) return true;

        int n = Physics.SphereCastNonAlloc(from, radius * 0.5f, delta.normalized, s_pathHits, dist,
            mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = s_pathHits[i];
            if (h.transform.IsChildOf(will)) continue;
            if (h.collider.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            return false;
        }
        return true;
    }

    /// Apaga el lanzador de hechizos de Will mientras dura la prueba del botón: el mismo botón le
    /// dispararía un hechizo de verdad que revienta el proyectil enemigo antes de tiempo. Se vuelve
    /// a encender al cerrar la secuencia (OnSequenceCleanup).
    private IEnumerator Co_SinMagiaDeWill(SequenceContext ctx)
    {
        var spawner = ResolverSpawner(ctx);
        if (spawner != null) spawner.enabled = false;
        yield break;
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

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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

        // El spawner sigue apagado a propósito (SinMagiaDeWill): SpawnForCinematic no lo necesita,
        // y volver a encenderlo aquí haría que un botón mantenido de la prueba disparase un hechizo
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

        // En tiempo real: el guion pone la cámara lenta justo aquí y con WaitForSeconds el gesto
        // tardaba cinco veces más de lo previsto (INC-602).
        if (castAnimDelay > 0f) yield return new WaitForSecondsRealtime(castAnimDelay);
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
            ? spawner.SpawnForCinematic(cinematicSpellFallback, castSlot, castOrigin, castDir)
            : null;
        _pendingFireball = fireball;

        if (fireball != null && fireball.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
        {
            float speed = rb.linearVelocity.magnitude;
            if (speed < 0.1f) speed = 10f;
            rb.linearVelocity = castDir * speed;
        }

        // El choque, siempre (INC-602). Si el guion ha puesto la marca «choque», las dos bolas se
        // encuentran ahí; si no, a medio camino. Se llevan a mano y en tiempo real: con la cámara
        // lenta del guion la bola de fuego tardaba tanto que saltaba el tope de espera, explotaba
        // junto a Will y el proyectil seguía de largo.
        if (fireball != null && _projectile != null)
        {
            Vector3 desde = fireball.transform.position;
            Vector3 proyectilDesde = _projectile.transform.position;
            float alto = willT.position.y + _flightHeight;
            Vector3 encuentro = (desde + proyectilDesde) * 0.5f;
            Transform marca = ctx.Stage != null ? ctx.Stage.BuscarMarca(clashMarkName) : null;
            if (marca != null) encuentro = new Vector3(marca.position.x, alto, marca.position.z);
            // Si el proyectil ya está más cerca de Will que la marca, el choque es donde está él.
            if (Plano(proyectilDesde - willT.position).sqrMagnitude < Plano(encuentro - willT.position).sqrMagnitude)
                encuentro = proyectilDesde;

            _projectile.Pause();
            if (fireball.TryGetComponent(out MagicProjectile mp)) mp.enabled = false;
            foreach (var col in fireball.GetComponentsInChildren<Collider>()) col.enabled = false;
            if (fireball.TryGetComponent(out Rigidbody rbf))
            {
                rbf.linearVelocity = Vector3.zero;
                rbf.isKinematic = true;
                rbf.interpolation = RigidbodyInterpolation.None;
            }

            float dura = Mathf.Max(0.1f, clashTravelUnscaled);
            for (float tt = 0f; tt < dura; tt += Time.unscaledDeltaTime)
            {
                if (fireball == null || _projectile == null) break;
                float k = tt / dura;
                fireball.transform.position = Vector3.Lerp(desde, encuentro, 1f - (1f - k) * (1f - k));
                _projectile.transform.position = Vector3.Lerp(proyectilDesde, encuentro, k);
                yield return null;
            }
            EnsureCollisionPoint().position = encuentro;
            TriggerExplosion();
        }
        else
        {
            // Sin bola de fuego (no hay spawner) o sin proyectil: espera corta y explosión igual,
            // para que la secuencia nunca se quede colgada.
            float elapsed = 0f;
            while (elapsed < collisionWaitUnscaled && !_collisionTriggered)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_collisionTriggered)
            {
                if (_projectile != null) EnsureCollisionPoint().position = _projectile.transform.position;
                TriggerExplosion();
            }
        }

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

    /// Rama de fallo: Will no ha reaccionado y la bola sigue hasta él y le explota encima. Lo que
    /// pasa después (la derrota) lo decide el asset. Ver INC-517.
    private IEnumerator Co_ImpactoEnWill(SequenceContext ctx)
    {
        var will = ctx.GetActor(SequenceActor.PlayerId);
        if (_projectile == null || will?.Transform == null) yield break;

        _projectile.Resume();

        // La bola apunta al pecho de Will; se da por alcanzado cuando su centro llega a media bola
        // de ese punto, que es cuando ya lo está tapando.
        float alcance = Mathf.Max(0.6f, _projectile.Radius * 0.5f);
        float esperado = 0f;
        while (_projectile != null && esperado < impactWaitUnscaled)
        {
            Vector3 pecho = will.Transform.position + Vector3.up * _flightHeight;
            if ((_projectile.transform.position - pecho).sqrMagnitude <= alcance * alcance) break;
            esperado += Time.unscaledDeltaTime;
            yield return null;
        }

        EnsureCollisionPoint().position = _projectile != null
            ? _projectile.transform.position
            : will.Transform.position + Vector3.up * _flightHeight;
        TriggerExplosion();
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

    private static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

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

    /// Se llama SIEMPRE al cerrar: final normal, skip, o si algo revienta por el camino. Todas las
    /// corrutinas cuelgan del módulo, así que se cortan de una vez y no siguen vivas en pleno juego.
    public override void OnSequenceCleanup()
    {
        StopAllCoroutines();
        _tension = null;

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

        _collisionTriggered = false;
        _ctx = null;
    }

    private void OnDestroy()
    {
        if (_collisionPoint != null) Destroy(_collisionPoint.gameObject);
        if (_castOrigin != null) Destroy(_castOrigin.gameObject);
    }
}
