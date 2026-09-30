using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Sendero.Core.Feedback;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Damageable))]
public class ImpDemonAI : MonoBehaviour, IJefeConFases, IInicioDeCombate
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Damageable damageable;
    [SerializeField] private NavMeshAgent agent;

    [Header("Configuración General")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float projectileRange = 10f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Ataques")]
    [SerializeField] private float slashDamage = 15f;
    [SerializeField] private float stabDamage = 20f;
    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private GameObject spellEffectPrefab;
    [Tooltip("Propuesta identidad de fase (30 ago 2026): aviso en el suelo antes de que salga un "
             + "proyectil normal (Fase 2+), mismo patron visual que las sombras de RainAttack — antes "
             + "el proyectil solo tenia un breve giro hacia el jugador, sin telegrafia real. "
             + "Si se deja vacio, reutiliza rainShadowPrefab (misma sombra que ya usa la lluvia).")]
    [SerializeField] private GameObject rangedTelegraphPrefab;
    [SerializeField] private float rangedTelegraphDuration = 0.4f;

    [Header("Cooldowns")]
    [SerializeField] private float slashCooldown = 2f;
    [SerializeField] private float stabCooldown = 3f;
    [SerializeField] private float projectileCooldown = 4f;
    [SerializeField] private float spellCooldown = 8f;
    [SerializeField] private float undergroundCooldown = 15f;

    [Header("Fases")]
    [SerializeField] private float phase2HealthPercent = 0.66f;
    [SerializeField] private float phase3HealthPercent = 0.33f;
    [Tooltip("Propuesta identidad de fase (30 ago 2026): VFX opcional que se activa al entrar en "
             + "Fase 3 y se queda encima del demonio mientras dure el combate — el 'enrage' antes solo "
             + "se notaba en la camara (shake/flash) y en el multiplicador de velocidad, nunca en el "
             + "propio demonio. Se deja vacio por defecto: sin asset asignado en el Inspector no pasa "
             + "nada (mismo guard que el resto de VFX opcionales de este script).")]
    [SerializeField] private GameObject enrageAuraVFXPrefab;
    private GameObject _enrageAuraInstance;

    [Header("Segunda Aparición")]
    [Tooltip("Actívalo en el prefab/instancia del segundo encuentro. No afecta al primero.")]
    public bool isSecondEncounter = false;
    [Tooltip("Multiplicador de cooldowns (0.65 = 35% más rápido).")]
    [SerializeField] private float secondCooldownMultiplier = 0.65f;
    [Tooltip("La fase berserk se activa antes (50% en lugar del 33%).")]
    [SerializeField] private float phase3HealthPercentSecond = 0.50f;

    [Header("Lluvia de sombras (fase 3; en la revancha desde la fase 2)")]
    [Tooltip("Prefab con un quad/decal semitransparente que hace de sombra de aviso.")]
    [SerializeField] private GameObject rainShadowPrefab;
    [Tooltip("Efecto de explosión/impacto que aparece tras el aviso.")]
    [SerializeField] private GameObject rainImpactPrefab;
    [SerializeField] private int rainCount = 8;
    [SerializeField] private float rainRadius = 7f;
    [Tooltip("Tiempo que las sombras permanecen en el suelo antes del impacto.")]
    [SerializeField] private float rainWarningDuration = 1.5f;
    [SerializeField] private float rainImpactRadius = 1.8f;
    [SerializeField] private float rainDamage = 20f;
    [SerializeField] private float rainCooldown = 22f;

    [Header("Cadencia de Ataque")]
    [Tooltip("Tiempo minimo de 'respiro' tras CUALQUIER ataque antes de poder iniciar el siguiente. "
             + "Antes las funciones Decide*/Try* se evaluaban cada frame (tirada de moneda por frame), "
             + "asi que un ataque podia encadenar con otro de tipo distinto sin ninguna pausa perceptible. "
             + "Este valor fuerza un hueco minimo entre ataques para que el combate tenga un pulso legible.")]
    [SerializeField] private float attackRecoveryBeat = 0.45f;
    private float _lastAttackEndTime = -999f;

    [Header("Ventana del aro: aviso + agotado")]
    [Tooltip("Segundos que se queda quieto y expuesto (el aro sigue brillando) al terminar cada " +
             "ataque, por fase (1, 2, 3). Es el momento de castigarle: primero se esquiva el golpe " +
             "y después se dispara. Baja en cada fase para que el combate apriete.")]
    [SerializeField] private float[] agotamientoPorFase = { 1.6f, 1.25f, 0.9f };
    [Tooltip("Golpes encajados en una misma ventana a partir de los cuales, al terminarla, se " +
             "aparta de Will antes de volver a atacar.")]
    [SerializeField, Min(1)] private int golpesParaRecular = 2;
    [SerializeField] private float distanciaRecular = 4f;
    [SerializeField] private float duracionRecular = 0.6f;

    [Header("Embestida")]
    [Tooltip("Si Will se queda lejos, marca en el suelo dónde va a caer y embiste en línea recta " +
             "hacia allí. Durante el aviso y después el aro brilla: también hay ventana para quien " +
             "pelea de lejos.")]
    [SerializeField] private float dashDamage = 25f;
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashCooldown = 7f;
    [Tooltip("Segundos de aviso (marca en el suelo) antes de embestir.")]
    [SerializeField] private float preparacionEmbestida = 0.7f;
    [Tooltip("Segundos persiguiendo a Will sin alcanzarle antes de embestir.")]
    [SerializeField] private float persecucionAntesDeEmbestir = 1.5f;
    [Tooltip("Por debajo de esta distancia no embiste: ya está a tiro de garra.")]
    [SerializeField] private float distanciaMinimaEmbestida = 5f;
    [Tooltip("Metros que sigue de largo pasada la marca.")]
    [SerializeField] private float pasadaEmbestida = 1.5f;

    [Header("Cambio de fase (efectos en TransicionDeFaseDeJefe)")]
    [Tooltip("Multiplicador de la velocidad de movimiento en cada fase (1, 2, 3).")]
    [SerializeField] private float[] velocidadPorFase = { 1f, 1.2f, 1.5f };
    private float _persiguiendoDesde = -1f;

    [Header("DEBUG")]
    [SerializeField] private bool debugLogAnimator = false;

    [Header("Combat Control")]
    [Tooltip("Permite iniciar el combate. Se activa externamente después de la presentación.")]
    public bool canStartCombat = false;

    public void EmpezarCombate() => canStartCombat = true;

    [Header("Aro de runas / final alternativo (Paso 6 del refactor Tramo 1)")]
    [Tooltip("Al romper RuneCollar el demonio queda 'caído' en vez de morir (ver " +
             "ForceFallenByCollarBreak): se atenúa el color de todos sus materiales y se apaga " +
             "cualquier emisión, vía MaterialPropertyBlock -- no toca los materiales compartidos " +
             "del prefab, así que es seguro aunque el shader no tenga alguna de las propiedades " +
             "buscadas (_Color/_BaseColor/_EmissionColor). 1 = sin cambio, 0 = negro.")]
    [SerializeField, Range(0f, 1f)] private float fallenDarkenFactor = 0.35f;

    // Estado interno
    private enum BossPhase { Phase1, Phase2, Phase3 }
    private enum BossState { Idle, Chasing, Attacking, CastingSpell, Underground, TakingDamage, Dead }

    private BossPhase currentPhase = BossPhase.Phase1;
    private BossState currentState = BossState.Idle;
    private float lastSlashTime = -999f;
    private float lastStabTime = -999f;
    private float lastProjectileTime = -999f;
    private float lastSpellTime = -999f;
    private float lastUndergroundTime = -999f;
    private float lastDashTime = -999f;
    private float lastRainTime = -999f;
    private bool isAttacking = false;
    private bool hasSpawned = false;
    private bool isDead = false;
    private bool _registeredInCombat = false;

    // Ventana del aro y movimiento
    private bool _expuesto;
    private bool _agotado;
    private int _golpesEnVentana;
    private float _volverAIdleEn = -1f;
    private int _ladoRodeo = 1;
    private float _siguienteCambioRodeo;
    private float _velocidadBase;
    private float[] _umbrales;
    private Coroutine _accionActual;
    private TransicionDeFaseDeJefe _transicion;

    // Cooldowns efectivos calculados en Awake
    private float _effSlashCooldown;
    private float _effStabCooldown;
    private float _effProjectileCooldown;
    private float _effSpellCooldown;
    private float _effUndergroundCooldown;
    private float _effDashCooldown;

    private static readonly Collider[] _overlapBuffer = new Collider[16];
    private float _targetRefreshTimer;

    // Hashes de animaciones
    private static readonly int AnimIdle            = Animator.StringToHash("Idle");
    private static readonly int AnimFlyForward      = Animator.StringToHash("Fly Forward");
    private static readonly int AnimSlashAttack     = Animator.StringToHash("Slash Attack");
    private static readonly int AnimStabAttack      = Animator.StringToHash("Stab Attack");
    private static readonly int AnimProjectileAttack = Animator.StringToHash("Projectile Attack");
    private static readonly int AnimCastSpell       = Animator.StringToHash("Cast Spell");
    private static readonly int AnimUnderground     = Animator.StringToHash("Underground");
    private static readonly int AnimTakeDamage      = Animator.StringToHash("Take Damage");
    private static readonly int AnimDie             = Animator.StringToHash("Die");
    private static readonly int AnimSpawn           = Animator.StringToHash("Spawn");

    private static readonly System.Collections.Generic.Dictionary<int, string> AnimNameMap;

    private struct AnimInfo { public int layer; public int clipHash; }
    private System.Collections.Generic.Dictionary<int, AnimInfo> _animLookup;

    static ImpDemonAI()
    {
        AnimNameMap = new System.Collections.Generic.Dictionary<int, string>
        {
            { AnimIdle,             "Idle" },
            { AnimFlyForward,       "Fly Forward" },
            { AnimSlashAttack,      "Slash Attack" },
            { AnimStabAttack,       "Stab Attack" },
            { AnimProjectileAttack, "Projectile Attack" },
            { AnimCastSpell,        "Cast Spell" },
            { AnimUnderground,      "Underground" },
            { AnimTakeDamage,       "Take Damage" },
            { AnimDie,              "Die" },
            { AnimSpawn,            "Spawn" }
        };
    }

    void Awake()
    {
        if (!animator)  animator  = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!agent)     agent     = GetComponent<NavMeshAgent>();

        if (!player && PlayerService.Player != null)
            player = PlayerService.Player.transform;

        float m = isSecondEncounter ? secondCooldownMultiplier : 1f;
        _effSlashCooldown       = slashCooldown       * m;
        _effStabCooldown        = stabCooldown        * m;
        _effProjectileCooldown  = projectileCooldown  * m;
        _effSpellCooldown       = spellCooldown       * m;
        _effUndergroundCooldown = undergroundCooldown * m;
        _effDashCooldown        = dashCooldown        * m;

        _velocidadBase = agent ? agent.speed : 3.5f;
        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        if (_transicion == null) _transicion = gameObject.AddComponent<TransicionDeFaseDeJefe>();
        _umbrales = new[] { phase2HealthPercent, isSecondEncounter ? phase3HealthPercentSecond : phase3HealthPercent };

        BuildAnimatorLookup();

        if (debugLogAnimator)
            LogAnimatorSetup();
    }

    private void BuildAnimatorLookup()
    {
        _animLookup = new System.Collections.Generic.Dictionary<int, AnimInfo>();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        int layers = animator.layerCount;
        var clips = animator.runtimeAnimatorController.animationClips;

        foreach (var kv in AnimNameMap)
        {
            int animHash = kv.Key;
            string baseName = kv.Value;
            AnimInfo info = new AnimInfo { layer = -1, clipHash = animHash };

            for (int l = 0; l < layers; l++)
            {
                if (animator.HasState(l, animHash))
                {
                    info.layer = l;
                    info.clipHash = animHash;
                    break;
                }
            }

            if (info.layer == -1 && clips != null)
            {
                foreach (var clip in clips)
                {
                    if (clip == null) continue;
                    if (clip.name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int clipHash = Animator.StringToHash(clip.name);
                        for (int l = 0; l < layers; l++)
                        {
                            if (animator.HasState(l, clipHash))
                            {
                                info.layer = l;
                                info.clipHash = clipHash;
                                break;
                            }
                        }
                        if (info.layer >= 0) break;
                    }
                }
            }

            _animLookup[animHash] = info;
        }
    }

    [ContextMenu("Log Animator Info")]
    private void LogAnimatorSetup()
    {
        if (animator == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ImpDemonAI] No hay Animator asignado para inspeccionar.");
#endif
            return;
        }

        var controller = animator.runtimeAnimatorController;
        string ctrlName = controller != null ? controller.name : "<null>";
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ImpDemonAI] Animator Controller: {ctrlName}");
        Debug.Log($"[ImpDemonAI] Layer count: {animator.layerCount}");
#endif

        if (controller != null)
        {
            var clips = controller.animationClips;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[ImpDemonAI] Animation Clips ({(clips != null ? clips.Length : 0)}):");
#endif
            if (clips != null)
            {
                foreach (var c in clips)
                {
                    if (c == null) continue;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($" - {c.name}");
#endif
                }
            }
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[ImpDemonAI] Mapeo de animaciones usadas:");
#endif
        foreach (var kv in AnimNameMap)
        {
            int hash = kv.Key;
            string animLabel = kv.Value;
            int layer = AnimatorLayerContainingState(hash);
            if (layer >= 0)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($" - '{animLabel}' -> encontrada en capa {layer}");
                #endif
            }
            else
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($" - '{animLabel}' -> NO encontrada");
                #endif
            }

            if (controller != null)
            {
                var clips = controller.animationClips;
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        if (c == null) continue;
                        if (c.name.IndexOf(animLabel, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                            Debug.Log($"    Clip coincidente: {c.name}");
                            #endif
                        }
                    }
                }
            }
        }
    }

    void Start()
    {
        if (damageable)
        {
            damageable.OnDamaged += OnDamageTaken;
            damageable.OnDied    += OnDeath;
        }

        StartCoroutine(SpawnSequence());
    }

    void OnDestroy()
    {
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied    -= OnDeath;
        }

        UnregisterFromCombatRegistry();
    }

    void Update()
    {
        SyncCombatRegistryState();
        if (!hasSpawned || isDead || !canStartCombat) return;

        // FIX (petición Raúl, 1 sep 2026): congelar IA hostil mientras hay un diálogo abierto en
        // cualquier parte del mundo (ver mismo fix en Spider1AI.Update). No corta un ataque que ya
        // esté a mitad de ejecución (eso vive en corrutinas propias de UpdateBehavior), solo evita
        // encadenar movimiento/decisiones nuevas mientras el jugador está bloqueado por el diálogo.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            if (agent && agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
            return;
        }

        _targetRefreshTimer += Time.deltaTime;
        if (_targetRefreshTimer >= 0.5f)
        {
            _targetRefreshTimer = 0f;
            var playerTransform = CombatTargetProvider.GetNearestTarget(transform.position);
            if (playerTransform != null) player = playerTransform;
        }

        if (!player) return;

        UpdatePhase();
        UpdateBehavior();
    }

    private void SyncCombatRegistryState()
    {
        if (isDead)
        {
            UnregisterFromCombatRegistry();
            return;
        }

        if (canStartCombat && !_registeredInCombat)
        {
            ActiveCombatRegistry.RegisterNPC(gameObject);
            _registeredInCombat = true;
        }
        else if (!canStartCombat && _registeredInCombat)
        {
            UnregisterFromCombatRegistry();
        }
    }

    private void UnregisterFromCombatRegistry()
    {
        if (!_registeredInCombat) return;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        _registeredInCombat = false;
    }

    private IEnumerator SpawnSequence()
    {
        currentState = BossState.Idle;

        if (agent && agent.isOnNavMesh)
            agent.isStopped = true;

        PlayAnimation(AnimSpawn);
        yield return new WaitForSeconds(2f);

        hasSpawned = true;

        if (agent && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    private void UpdatePhase()
    {
        if (!damageable) return;

        float healthPercent = damageable.Current / damageable.Max;
        float p3Threshold = isSecondEncounter ? phase3HealthPercentSecond : phase3HealthPercent;

        BossPhase newPhase;
        if (healthPercent <= p3Threshold)
            newPhase = BossPhase.Phase3;
        else if (healthPercent <= phase2HealthPercent)
            newPhase = BossPhase.Phase2;
        else
            newPhase = BossPhase.Phase1;

        // Las fases solo avanzan. Si se cura (golpes sin el aro) por encima del umbral, sigue en
        // la fase a la que llegó: volver atrás repetiría el rugido y desharía lo aprendido.
        // Ver INC-487.
        if (newPhase > currentPhase)
        {
            currentPhase = newPhase;
            OnPhaseChanged();
        }
    }

    private void OnPhaseChanged()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ImpDemonAI] Cambiando a {currentPhase}");
#endif
        if (agent) agent.speed = _velocidadBase * ValorDeFase(velocidadPorFase, 1f);

        // El rugido manda: corta lo que estuviera haciendo (el cambio llega casi siempre a mitad
        // de una ventana, porque solo ahí recibe daño).
        if (_accionActual != null) StopCoroutine(_accionActual);
        _expuesto = false;
        _agotado = false;

        if (currentPhase == BossPhase.Phase3) SpawnEnrageAura();
        _accionActual = StartCoroutine(TransicionDeFase());
        AlCambiarDeFase?.Invoke(Fase);
    }

    // Propuesta identidad de fase (30 ago 2026): la Fase 3 ('enrage') se notaba solo en el shake
    // de camara, el flash y el multiplicador de velocidad — nada distinto en el propio demonio.
    // Guard con _enrageAuraInstance: OnPhaseChanged() solo llama aqui al ENTRAR en Fase 3 (la vida
    // no sube, no debería reentrar), pero el guard evita duplicar el VFX si algún día lo hiciera.
    private void SpawnEnrageAura()
    {
        if (_enrageAuraInstance || !enrageAuraVFXPrefab) return;
        _enrageAuraInstance = Instantiate(enrageAuraVFXPrefab, transform.position, transform.rotation, transform);
    }

    /// Cambio de fase: ruge con el aro apagado; los efectos (invulnerable, cámara lenta, onda,
    /// color) los pone TransicionDeFaseDeJefe. Tiene que leerse como «ahora pelea distinto».
    private IEnumerator TransicionDeFase()
    {
        currentState = BossState.CastingSpell;
        isAttacking = true;
        _expuesto = false;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        bool furia = currentPhase == BossPhase.Phase3;

        // Carga: ruge mientras tiembla el suelo.
        PlayAnimation(AnimCastSpell);
        _transicion.Cargar();
        yield return WaitFacingPlayer(0.7f);

        // Estallido.
        _transicion.Estallar(Fase, furia);
        if (spellEffectPrefab && projectileSpawnPoint && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(spellEffectPrefab, projectileSpawnPoint.position, Quaternion.identity, 3f);

        yield return WaitFacingPlayer(1f);
        EndAttack();

        // La revancha abre su fase final con un combo: teletransporte a la espalda + golpe + lluvia.
        if (isSecondEncounter && furia)
        {
            yield return UndergroundAttack();
            // Agotarse() ya bajó isAttacking: se vuelve a subir para que UpdateBehavior no
            // meta un ataque suelto en el respiro del combo.
            isAttacking = true;
            yield return new WaitForSeconds(0.4f);
            yield return RainAttack();
        }

        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    private void UpdateBehavior()
    {
        if (isAttacking || currentState == BossState.TakingDamage) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > detectionRange)
        {
            currentState = BossState.Idle;
            _persiguiendoDesde = -1f;
            PlayAnimation(AnimIdle);
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        LookAtPlayer();
        bool puedeAtacar = CanStartNewAttack();

        if (puedeAtacar && TryAtaqueEspecial(distanceToPlayer)) return;

        bool melee = puedeAtacar && MeleeDisponible();

        // Cuerpo a cuerpo: si puede, ataca; si no, rodea a Will en vez de quedarse plantado.
        if (distanceToPlayer <= attackRange && melee)
        {
            _persiguiendoDesde = -1f;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideMeleeAttack();
            return;
        }
        if (distanceToPlayer <= attackRange * 1.5f && !melee)
        {
            _persiguiendoDesde = -1f;
            Rodear();
            return;
        }

        // Media distancia (fase 2+): fuego.
        if (distanceToPlayer <= projectileRange && currentPhase != BossPhase.Phase1
            && puedeAtacar && DistanciaDisponible())
        {
            _persiguiendoDesde = -1f;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            DecideRangedAttack();
            return;
        }

        // Lejos: persigue y, si Will no se deja alcanzar, embiste.
        Perseguir();
        if (_persiguiendoDesde < 0f) _persiguiendoDesde = Time.time;

        if (puedeAtacar
            && distanceToPlayer >= distanciaMinimaEmbestida
            && Time.time >= lastDashTime + _effDashCooldown
            && Time.time - _persiguiendoDesde >= persecucionAntesDeEmbestir)
        {
            _persiguiendoDesde = -1f;
            Lanzar(Embestida());
        }
    }

    private void Lanzar(IEnumerator accion) => _accionActual = StartCoroutine(accion);

    private bool MeleeDisponible()
        => Time.time >= lastSlashTime + _effSlashCooldown || Time.time >= lastStabTime + _effStabCooldown;

    private bool DistanciaDisponible()
        => Time.time >= lastProjectileTime + _effProjectileCooldown
        || (currentPhase == BossPhase.Phase3 && Time.time >= lastSpellTime + _effSpellCooldown);

    private void Perseguir()
    {
        currentState = BossState.Chasing;
        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);

            if (agent.velocity.sqrMagnitude > 0.1f)
                LookAtDirection(agent.velocity.normalized);
            else
                LookAtPlayer();
        }
        PlayAnimation(AnimFlyForward);
    }

    /// Mientras no puede atacar, da vueltas alrededor de Will cambiando de lado de vez en cuando.
    private void Rodear()
    {
        currentState = BossState.Chasing;
        if (!agent || !agent.isOnNavMesh) { PlayAnimation(AnimIdle); return; }

        if (Time.time >= _siguienteCambioRodeo)
        {
            _ladoRodeo = Random.value < 0.5f ? -1 : 1;
            _siguienteCambioRodeo = Time.time + Random.Range(1.2f, 2.2f);
        }

        Vector3 desdeWill = transform.position - player.position;
        desdeWill.y = 0f;
        if (desdeWill.sqrMagnitude < 0.01f) desdeWill = -player.forward;
        Vector3 dir = Quaternion.Euler(0f, 40f * _ladoRodeo, 0f) * desdeWill.normalized;

        agent.isStopped = false;
        agent.SetDestination(player.position + dir * attackRange * 1.2f);
        PlayAnimation(AnimFlyForward);
    }

    /// Tras encajar varios golpes en una ventana, se aparta de Will antes de volver a la carga.
    private IEnumerator Recular()
    {
        if (!player || !agent || !agent.isOnNavMesh) yield break;

        Vector3 lejos = transform.position - player.position;
        lejos.y = 0f;
        if (lejos.sqrMagnitude < 0.01f) lejos = -transform.forward;
        Vector3 destino = transform.position + lejos.normalized * distanciaRecular;
        if (NavMesh.SamplePosition(destino, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            destino = hit.position;

        float velocidad = agent.speed;
        agent.speed = velocidad * 1.6f;
        agent.isStopped = false;
        agent.SetDestination(destino);
        PlayAnimation(AnimFlyForward);

        float t = 0f;
        while (t < duracionRecular)
        {
            LookAtPlayer();
            t += Time.deltaTime;
            yield return null;
        }

        if (agent) agent.speed = velocidad;
    }

    /// Ataques propios de las fases avanzadas: la lluvia de sombras (fase 3; en la revancha desde
    /// la fase 2) y el paso bajo tierra (fase 3, para cerrar distancia). Sin prefab de sombra no
    /// hay lluvia: el daño caería sin aviso.
    private bool TryAtaqueEspecial(float distancia)
    {
        bool lluviaPermitida = rainShadowPrefab != null &&
            (currentPhase == BossPhase.Phase3 || (isSecondEncounter && currentPhase != BossPhase.Phase1));
        if (lluviaPermitida && Time.time >= lastRainTime + rainCooldown)
        {
            Lanzar(RainAttack());
            return true;
        }

        if (currentPhase == BossPhase.Phase3 && distancia > attackRange
            && Time.time >= lastUndergroundTime + _effUndergroundCooldown)
        {
            Lanzar(UndergroundAttack());
            return true;
        }

        return false;
    }

    private void DecideMeleeAttack()
    {
        bool canSlash = Time.time >= lastSlashTime + _effSlashCooldown;
        bool canStab  = Time.time >= lastStabTime  + _effStabCooldown;

        // Combo (zarpazo + estocada) en la fase final y en toda la revancha.
        bool comboPermitido = isSecondEncounter || currentPhase == BossPhase.Phase3;
        if (comboPermitido && canSlash && canStab && Random.value > 0.4f)
        {
            Lanzar(MeleeCombo());
            return;
        }

        if (canSlash && canStab)
            Lanzar(Random.value > 0.5f ? SlashAttack() : StabAttack());
        else if (canSlash)
            Lanzar(SlashAttack());
        else if (canStab)
            Lanzar(StabAttack());
    }

    private void DecideRangedAttack()
    {
        bool canProjectile = Time.time >= lastProjectileTime + _effProjectileCooldown;
        bool canSpell      = Time.time >= lastSpellTime      + _effSpellCooldown && currentPhase == BossPhase.Phase3;

        if (canSpell && (!canProjectile || Random.value > 0.7f))
            Lanzar(CastSpellAttack());
        else if (canProjectile)
            Lanzar(ProjectileAttack());
    }

    /// Principio de cualquier ataque. 'expuesto' = el aro se enciende ya en el aviso.
    private void EmpezarAtaque(bool expuesto)
    {
        isAttacking = true;
        _expuesto = expuesto;
        _golpesEnVentana = 0;
    }

    /// Final de cualquier ataque: se queda quieto, jadeando, con el aro encendido. Es la ventana
    /// para castigarle. Si en ella ha encajado varios golpes, se aparta antes de volver.
    private IEnumerator Agotarse()
    {
        _expuesto = true;
        _agotado = true;
        currentState = BossState.Attacking;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;
        PlayAnimation(AnimIdle);

        float fin = Time.time + ValorDeFase(agotamientoPorFase, 1f);
        while (Time.time < fin)
        {
            if (_volverAIdleEn > 0f && Time.time >= _volverAIdleEn)
            {
                _volverAIdleEn = -1f;
                PlayAnimation(AnimIdle);
            }
            yield return null;
        }

        _agotado = false;
        _expuesto = false;
        if (_golpesEnVentana >= golpesParaRecular) yield return Recular();

        EndAttack();
        if (agent && agent.isOnNavMesh) agent.isStopped = false;
    }

    private float ValorDeFase(float[] valores, float porDefecto)
        => valores != null && valores.Length > 0 ? valores[Mathf.Min((int)currentPhase, valores.Length - 1)] : porDefecto;

    // ========== ATAQUES ==========

    private IEnumerator SlashAttack()
    {
        EmpezarAtaque(true);
        currentState = BossState.Attacking;
        lastSlashTime = Time.time;

        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.5f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(slashDamage);

        yield return new WaitForSeconds(0.5f);
        yield return Agotarse();
    }

    private IEnumerator StabAttack()
    {
        EmpezarAtaque(true);
        currentState = BossState.Attacking;
        lastStabTime = Time.time;

        PlayAnimation(AnimStabAttack);
        yield return new WaitForSeconds(0.6f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(stabDamage);

        yield return new WaitForSeconds(0.4f);
        yield return Agotarse();
    }

    // Zarpazo + estocada encadenados sin pausa completa (fase 3 y toda la revancha)
    private IEnumerator MeleeCombo()
    {
        EmpezarAtaque(true);
        currentState = BossState.Attacking;
        lastSlashTime = Time.time;
        lastStabTime  = Time.time;

        PlayAnimation(AnimSlashAttack);
        yield return new WaitForSeconds(0.5f);
        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(slashDamage);

        yield return new WaitForSeconds(0.15f);

        PlayAnimation(AnimStabAttack);
        yield return new WaitForSeconds(0.5f);
        if (player && Vector3.Distance(transform.position, player.position) <= attackRange)
            DamagePlayer(stabDamage);

        yield return new WaitForSeconds(0.35f);
        yield return Agotarse();
    }

    // En 2ª aparición (Fase 2+): triple proyectil en abanico con 2-3 rondas seguidas
    private IEnumerator ProjectileAttack()
    {
        EmpezarAtaque(true);
        currentState = BossState.Attacking;
        lastProjectileTime = Time.time;

        PlayAnimation(AnimProjectileAttack);

        // Propuesta identidad de fase (30 ago 2026): este ataque solo se usa en Fase 2+ (ver
        // DecideRangedAttack/UpdateBehavior, currentPhase != Phase1) — antes el unico aviso era el
        // giro hacia el jugador, sin telegrafia real. Ahora reutiliza el mismo patron visual que
        // RainAttack (sombra que crece antes del impacto), aplicado a un unico punto de aviso.
        GameObject telegraphPrefab = rangedTelegraphPrefab ? rangedTelegraphPrefab : rainShadowPrefab;
        if (telegraphPrefab && player)
            yield return SpawnRangedTelegraph(player.position, rangedTelegraphDuration);
        else
            yield return WaitFacingPlayer(0.5f);

        if (projectilePrefab && projectileSpawnPoint && player)
        {
            bool triple = isSecondEncounter && currentPhase != BossPhase.Phase1;
            int count = triple ? 3 : 1;
            float spreadAngle = 20f;
            int volleys = triple ? Random.Range(2, 4) : 1;

            for (int v = 0; v < volleys; v++)
            {
                if (v > 0) yield return WaitFacingPlayer(0.65f);

                Vector3 aimPos = player.position + Vector3.up * 1f;
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0f : Mathf.Lerp(-spreadAngle, spreadAngle, i / (float)(count - 1));
                    Vector3 baseDir = (aimPos - projectileSpawnPoint.position).normalized;
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDir;

                    GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(direction));
                    var proj = projectile.GetComponent<EnemyProjectile>()
                               ?? projectile.GetComponentInChildren<EnemyProjectile>();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    if (!proj) Debug.LogError($"[ImpDemonAI] El prefab '{projectilePrefab.name}' no tiene componente EnemyProjectile en la raíz ni en hijos.");
#endif
                    if (proj) proj.Initialize(direction, projectileDamage);
                }
            }
        }

        yield return new WaitForSeconds(0.5f);
        yield return Agotarse();
    }

    private IEnumerator CastSpellAttack()
    {
        EmpezarAtaque(true);
        currentState = BossState.CastingSpell;
        lastSpellTime = Time.time;

        PlayAnimation(AnimCastSpell);
        yield return WaitFacingPlayer(1f);

        if (spellEffectPrefab && player)
        {
            Instantiate(spellEffectPrefab, player.position, Quaternion.identity);

            int hitCount = Physics.OverlapSphereNonAlloc(player.position, 5f, _overlapBuffer, playerLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var dmg = _overlapBuffer[i].GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    dmg.TakeDamage(projectileDamage * 1.5f);
            }
        }

        yield return new WaitForSeconds(0.5f);
        yield return Agotarse();
    }

    // En 2ª aparición: teleporta detrás del jugador en lugar de posición aleatoria
    private IEnumerator UndergroundAttack()
    {
        EmpezarAtaque(false);
        currentState = BossState.Underground;
        lastUndergroundTime = Time.time;

        PlayAnimation(AnimUnderground);
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        yield return new WaitForSeconds(1f);

        if (player)
        {
            Vector3 newPosition;
            if (isSecondEncounter)
                newPosition = player.position - player.forward * 2f;
            else
                newPosition = player.position + (Random.insideUnitSphere * 3f);

            newPosition.y = transform.position.y;

            if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                transform.position = hit.position;
        }

        PlayAnimation(AnimSpawn);
        _expuesto = true; // al salir del suelo vuelve a brillar
        yield return new WaitForSeconds(0.5f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange * 1.5f)
            DamagePlayer(stabDamage * 1.5f);

        yield return new WaitForSeconds(0.5f);
        yield return Agotarse();
    }

    /// Marca en el suelo el sitio donde está Will, espera el aviso y embiste en línea recta hasta
    /// ahí (y un poco más). No corrige el rumbo: se esquiva apartándose de la marca. El aro brilla
    /// desde el aviso hasta el final del agotamiento.
    private IEnumerator Embestida()
    {
        EmpezarAtaque(true);
        currentState = BossState.Attacking;
        lastDashTime = Time.time;
        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        if (!player) { yield return Agotarse(); yield break; }

        Vector3 objetivo = player.position;
        PlayAnimation(AnimIdle);
        yield return SpawnRangedTelegraph(objetivo, preparacionEmbestida);

        Vector3 dir = objetivo - transform.position;
        dir.y = 0f;
        float recorrido = dir.magnitude + pasadaEmbestida;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

        PlayAnimation(AnimStabAttack);
        bool golpeado = false;
        float hecho = 0f, tiempo = 0f;
        while (hecho < recorrido && tiempo < 1.5f)
        {
            float paso = dashSpeed * Time.deltaTime;
            if (agent && agent.isOnNavMesh) agent.Move(dir * paso);
            else transform.position += dir * paso;
            LookAtDirection(dir);
            hecho += paso;
            tiempo += Time.deltaTime;

            if (!golpeado && player && Vector3.Distance(transform.position, player.position) <= attackRange * 0.8f)
            {
                DamagePlayer(dashDamage);
                FeedbackService.CameraShake(0.4f, 0.25f);
                golpeado = true;
            }
            yield return null;
        }

        yield return Agotarse();
    }

    // Lluvia de sombras: dos olas escalonadas. El aro solo brilla en el agotamiento del final:
    // mientras cae la lluvia toca esquivar.
    private IEnumerator RainAttack()
    {
        EmpezarAtaque(false);
        currentState = BossState.CastingSpell;
        lastRainTime = Time.time;

        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        PlayAnimation(AnimCastSpell);
        yield return new WaitForSeconds(0.5f);

        // Ola 1: amplia, sigue al jugador mientras avisa
        Vector3 center = player ? player.position : transform.position;
        yield return SpawnRainWave(center, rainCount, rainRadius, rainWarningDuration, trackPlayer: true);

        yield return new WaitForSeconds(0.35f);

        // Ola 2: más concentrada en donde el jugador se refugió, sin seguimiento
        center = player ? player.position : transform.position;
        int wave2Count = rainCount / 2 + 2;
        yield return SpawnRainWave(center, wave2Count, rainRadius * 0.55f, rainWarningDuration * 0.6f, trackPlayer: false);

        yield return new WaitForSeconds(0.5f);
        yield return Agotarse();
    }

    // Propuesta identidad de fase (30 ago 2026): aviso de un unico proyectil normal (Fase 2+),
    // mismo patron visual que SpawnRainWave (sombra que crece antes del impacto) pero para un solo
    // punto. Usa VfxPoolService (regla no negociable de VFX de un solo uso) en vez de
    // Instantiate/Destroy manual — el propio Play() gestiona el despawn al pasar rangedTelegraphDuration.
    private IEnumerator SpawnRangedTelegraph(Vector3 targetPosition, float duration)
    {
        GameObject telegraphPrefab = rangedTelegraphPrefab ? rangedTelegraphPrefab : rainShadowPrefab;
        if (!telegraphPrefab || VfxPoolService.Instance == null)
        {
            yield return WaitFacingPlayer(duration);
            yield break;
        }
        Quaternion rot = Quaternion.Euler(90f, 0f, 0f);
        Transform telegraph = VfxPoolService.Instance.Play(telegraphPrefab, targetPosition, rot, duration);

        if (!telegraph)
        {
            yield return WaitFacingPlayer(duration);
            yield break;
        }

        Vector3 endScale = telegraph.localScale;
        telegraph.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            LookAtPlayer();
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            // El objeto puede haber sido despawneado por el pool si duration es muy corto y este
            // bucle tarda un frame de mas en salir — comprobar antes de tocar el transform.
            if (!telegraph) yield break;
            telegraph.localScale = endScale * t;
            yield return null;
        }
    }

    // Genera una oleada de sombras que crecen, luego impactan
    private IEnumerator SpawnRainWave(Vector3 center, int count, float radius, float warningDuration, bool trackPlayer)
    {
        Vector3[] positions = new Vector3[count];
        Transform[] shadowTrans = new Transform[count];
        GameObject[] shadowGOs = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 rand2D = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(rand2D.x, 0f, rand2D.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                positions[i] = navHit.position;
            else
                positions[i] = candidate;

            if (rainShadowPrefab)
            {
                // Rotación forzada para que el quad quede plano en el suelo
                Quaternion rot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                shadowGOs[i] = Instantiate(rainShadowPrefab, positions[i], rot);
                shadowTrans[i] = shadowGOs[i].transform;
                shadowTrans[i].localScale = Vector3.zero;
            }
        }

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / warningDuration);

            for (int i = 0; i < count; i++)
            {
                if (shadowTrans[i])
                    shadowTrans[i].localScale = Vector3.one * t;
            }

            if (trackPlayer && player)
            {
                Vector3 newCenter = player.position;
                for (int i = 0; i < count; i++)
                {
                    Vector3 offset = positions[i] - center;
                    positions[i] = newCenter + offset;
                    if (shadowTrans[i])
                        shadowTrans[i].position = positions[i];
                }
                center = newCenter;
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (shadowGOs[i]) Destroy(shadowGOs[i]);

            if (rainImpactPrefab)
                Instantiate(rainImpactPrefab, positions[i], Quaternion.identity);

            DamagePlayerInRadius(positions[i], rainImpactRadius, rainDamage);

            yield return new WaitForSeconds(0.08f);
        }
    }

    private void DamagePlayerInRadius(Vector3 center, float radius, float damage)
    {
        if (!player) return;
        if (Vector3.Distance(center, player.position) <= radius)
            DamagePlayer(damage);
    }

    // ========== UTILIDADES ==========

    // Cadencia de ataque (ver "Cadencia de Ataque" en el Inspector): true si ha pasado el
    // hueco minimo de respiro desde que termino el ultimo ataque. Envuelve las llamadas a
    // Decide*/Try* en UpdateBehavior() para que no se encadenen ataques sin pausa perceptible.
    private bool CanStartNewAttack() => Time.time >= _lastAttackEndTime + attackRecoveryBeat;

    // Sustituye a "isAttacking = false;" suelto: ademas de bajar el flag, marca el instante en
    // que termino el ataque para que CanStartNewAttack() pueda exigir el respiro minimo.
    private void EndAttack()
    {
        isAttacking = false;
        _lastAttackEndTime = Time.time;
    }

    private void DamagePlayer(float damage)
    {
        if (!player) return;

        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return;
        }

        var playerDamageable = player.GetComponent<IDamageable>();
        if (playerDamageable != null && playerDamageable.IsAlive)
            playerDamageable.TakeDamage(damage);
    }

    private void LookAtPlayer()
    {
        if (!player) return;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    // FIX INC-115: gira hacia el player durante los ataques a distancia (proyectil/hechizo).
    // Antes UpdateBehavior() (unico sitio que llamaba a LookAtPlayer) se saltaba entero mientras
    // isAttacking=true, asi que durante toda la corrutina de ProjectileAttack/CastSpellAttack
    // (windup + rondas + cooldown, varios segundos en la 2a aparicion con rafagas triples) el
    // demonio se quedaba congelado mirando hacia donde estuviera antes de empezar a lanzar.
    private IEnumerator WaitFacingPlayer(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            LookAtPlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void LookAtDirection(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 7.5f);
        }
    }

    // FIX A11 (auditoría 2026-08-07): último (hash, capa) reproducido, para no reiniciar la misma
    // animación en la misma capa en cada llamada. Antes PlayAnimation() llamaba a animator.Play()
    // sin ningún guard — como varios estados llaman a PlayAnimation(AnimIdle) (u otras) en cada
    // frame que se re-evalúan (ver p. ej. el bucle de vuelo), la animación se reiniciaba al frame 0
    // constantemente: animación visualmente congelada en la primera pose, más el coste de
    // Animator.Play() cada frame. Mismo guard que ya usa Spider1AI.PlayAnimation, portado aquí.
    private int _lastPlayedAnimHash = -1;
    private int _lastPlayedLayer = -1;

    private void PlayAnimation(int animHash)
    {
        if (!animator) return;

        if (_animLookup != null && _animLookup.TryGetValue(animHash, out var info) && info.layer >= 0)
        {
            if (_lastPlayedAnimHash == animHash && _lastPlayedLayer == info.layer) return;
            try
            {
                animator.Play(info.clipHash, info.layer, 0f);
                _lastPlayedAnimHash = animHash;
                _lastPlayedLayer = info.layer;
                return;
            }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación mapeada hash={animHash}: {ex.Message}");
#endif
            }
        }

        int layerIndex = AnimatorLayerContainingState(animHash);
        if (layerIndex >= 0)
        {
            if (_lastPlayedAnimHash == animHash && _lastPlayedLayer == layerIndex) return;
            try
            {
                animator.Play(animHash, layerIndex, 0f);
                _lastPlayedAnimHash = animHash;
                _lastPlayedLayer = layerIndex;
            }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[ImpDemonAI] Error al reproducir animación hash={animHash} en capa={layerIndex}: {ex.Message}");
#endif
            }
            return;
        }

        string animName = AnimNameMap.TryGetValue(animHash, out var n) ? n : animHash.ToString();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[ImpDemonAI] Estado '{animName}' no encontrado. Reproduciendo Idle como fallback.");
#endif
        int idleLayer = AnimatorLayerContainingState(AnimIdle);
        if (idleLayer >= 0)
        {
            animator.Play(AnimIdle, idleLayer, 0f);
            _lastPlayedAnimHash = AnimIdle;
            _lastPlayedLayer = idleLayer;
        }
    }

    private int AnimatorLayerContainingState(int animHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return -1;
        int layers = animator.layerCount;
        for (int i = 0; i < layers; i++)
        {
            if (animator.HasState(i, animHash)) return i;
        }

        string baseName = AnimNameMap.TryGetValue(animHash, out var n) ? n : null;
        if (string.IsNullOrEmpty(baseName)) return -1;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips != null)
        {
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.IndexOf(baseName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int clipHash = Animator.StringToHash(clip.name);
                    for (int i = 0; i < layers; i++)
                    {
                        if (animator.HasState(i, clipHash)) return i;
                    }
                }
            }
        }

        return -1;
    }

    private void OnDamageTaken(float amount)
    {
        if (isDead) return;
        if (_expuesto) _golpesEnVentana++;

        // Agotado: acusa el golpe sin salir de la ventana.
        if (_agotado)
        {
            PlayAnimation(AnimTakeDamage);
            _volverAIdleEn = Time.time + 0.35f;
            return;
        }

        if (isAttacking) return;
        StartCoroutine(TakeDamageSequence());
    }

    private IEnumerator TakeDamageSequence()
    {
        currentState = BossState.TakingDamage;
        bool wasAttacking = isAttacking;

        PlayAnimation(AnimTakeDamage);
        yield return new WaitForSeconds(0.3f);

        if (!wasAttacking)
            currentState = BossState.Idle;
    }

    private void OnDeath()
    {
        if (isDead) return;

        isDead = true;
        currentState = BossState.Dead;
        StopCombatAndPlayDeathPose();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[ImpDemonAI] Boss derrotado!");
#endif
    }

    /// Final alternativo del combate (Paso 6 del refactor Tramo 1, RuneCollar, análisis
    /// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §6): al romper el aro de runas
    /// a base de disparos precisos durante la ventana de ataque, el demonio queda "caído" en vez
    /// de morir -- misma parada de IA/animación que OnDeath() (StopCombatAndPlayDeathPose,
    /// compartido), pero deliberadamente SIN pasar por Damageable.Kill()/Die(): así OrbDropper
    /// (que escucha Damageable.OnDied) no suelta orbes -- este final es de compasión, no de
    /// saqueo -- y Damageable.Die() nunca decide destruir el GameObject, así que el demonio se
    /// queda en escena, tumbado, para que Eldran narre lo que había debajo. Idempotente: si ya
    /// estaba muerto (por HP normal) no hace nada -- gana quien llegue primero.
    public void ForceFallenByCollarBreak()
    {
        if (isDead) return;

        isDead = true;
        currentState = BossState.Dead;

        // Se desconecta de Damageable ANTES de nada: si algún proyectil en vuelo todavía impacta
        // este mismo frame, no debe disparar OnDamageTaken/OnDeath por detrás de este camino.
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied    -= OnDeath;
        }

        StopCombatAndPlayDeathPose();
        _transicion.Tintar(new Color(fallenDarkenFactor, fallenDarkenFactor, fallenDarkenFactor, 1f), apagarEmision: true);

        var healthBar = GetComponent<BossHealthBar>();
        if (healthBar) healthBar.Hide();

        // La arena/batalla no se entera de esto por Damageable.OnDied (nunca se dispara aquí) --
        // hay que avisarla explícitamente para que abra la salida, pare la música de jefe y marque
        // la batalla como ganada exactamente igual que con una muerte normal.
        var arena = FindAnyObjectByType<BossArenaController>();
        if (arena != null) arena.NotifyBossDefeatedByAlternateEnding();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else Debug.LogWarning("[ImpDemonAI] Aro roto pero no se encontró ningún BossArenaController en la escena -- la arena no se desbloqueará.");
#endif

        OnFellByCollarBreak?.Invoke();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[ImpDemonAI] Aro de runas roto -- demonio caído (final alternativo, sin orbes).");
#endif
    }

    /// Se dispara justo después de ForceFallenByCollarBreak(), por si algo más (p. ej. una
    /// secuencia de Eldran narrando lo que había debajo) necesita reaccionar sin sondear isDead.
    public event System.Action OnFellByCollarBreak;

    /// El aro brilla: está preparando un ataque, atacando o agotado después. Es el único momento
    /// en que se le puede hacer daño (RuneCollar → SoloDanoCuandoExpuesto). Nunca durante el
    /// rugido de cambio de fase ni bajo tierra.
    public bool Expuesto => _expuesto && !isDead;

    // ── IJefeConFases ─────────────────────────────────────────────────────
    public int Fase => (int)currentPhase;
    public System.Collections.Generic.IReadOnlyList<float> UmbralesDeFase => _umbrales;
    public event System.Action<int> AlCambiarDeFase;

    /// true en cuanto el combate termina, por CUALQUIER camino (HP a 0 vía OnDeath, o el aro roto
    /// vía ForceFallenByCollarBreak). RuneCollar lo consulta para dejar de trabajar en cuanto el
    /// jefe cae por el otro camino -- gana quien llegue primero, sin necesitar un evento propio.
    public bool IsDead => isDead;

    /// Detiene IA/física/colisiones y reproduce la pose de "Die" -- compartido por una muerte
    /// normal (OnDeath) y por el final alternativo del aro roto (ForceFallenByCollarBreak). El
    /// único estado que cada llamante gestiona por separado es qué pasa con Damageable/OrbDropper
    /// y con el GameObject (destruirlo o dejarlo tumbado en escena).
    private void StopCombatAndPlayDeathPose()
    {
        UnregisterFromCombatRegistry();

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StopAllCoroutines();
        PlayAnimation(AnimDie);

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        if (_enrageAuraInstance)
        {
            Destroy(_enrageAuraInstance);
            _enrageAuraInstance = null;
        }
    }

    // ========== DEBUG ==========

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        if (isSecondEncounter)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f); // naranja semitransparente
            Gizmos.DrawWireSphere(transform.position, rainRadius);
        }
    }
}
