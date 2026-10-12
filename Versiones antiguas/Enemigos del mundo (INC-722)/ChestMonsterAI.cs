using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA del "cofre mímico" (asset RPGMonsterPartnersPBRPolyart, prefab ChestMonster*Default):
/// se queda parado con la pinta de un cofre normal (animación "IdleChest", interactuable como
/// cualquier otro cofre) hasta que el jugador intenta abrirlo o lo golpea — momento en el que se
/// revela y pasa a comportarse como un enemigo cuerpo a cuerpo normal (persigue por NavMesh,
/// ataca, puede morir).
///
/// Estructura calcada de Spider1AI.cs (mismo patrón de persecución/ataque/daño/muerte, mismos
/// sistemas: Damageable, NavMeshAgent, Targetable, EnemyMarker, ActiveCombatRegistry,
/// CombatTargetProvider) — la diferencia es que no empieza activo: hasta que se "despierta", no
/// es Targetable ni cuenta como Enemy (para no delatarse con la barra de vida ni el marcador de
/// objetivo), no se mueve, y su único componente activo de cara al jugador es el Interactable que
/// ya usan los cofres normales (mismo icono "A", mismo prompt).
///
/// El disparo de "despertar" es doble a propósito: interactuar con él esperando el botín de
/// siempre (el gancho de diseño principal — el mímico clásico), o golpearlo directamente con un
/// ataque (para que intentar hacer trampa atacando primero no deje "pegar gratis" sin respuesta).
///
/// También cambia de layer al despertar: mientras está disfrazado vive en el layer "Interactable"
/// (igual que cualquier cofre — así el OverlapSphere de InteractionDetector lo detecta), y al
/// despertar pasa al layer "Enemy" (igual que Spider1/Demon/Golem) para que el resto de sistemas
/// de combate (cámara-lock, proyectiles, etc.) lo traten como un enemigo real a partir de ahí.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(Interactable))]
public class ChestMonsterAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Damageable damageable;
    [SerializeField] private Interactable interactable;
    [SerializeField] private NavMeshAgent agent;

    [Header("Disfraz / despertar")]
    [Tooltip("Si es true, un golpe recibido mientras sigue disfrazado también lo despierta (además de intentar interactuar con él). Evita que se pueda golpear gratis sin que reaccione.")]
    [SerializeField] private bool wakeOnDamage = true;
    [SerializeField] private float wakeAnimSeconds = 0.9f;

    [Header("Combate (una vez despierto)")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float damage = 14f;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField, Min(0f)] private float attackLungeDistance = 0.5f;
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float modelRotationOffset = 0f;

    [Header("Reacción al daño")]
    [SerializeField] private bool stopOnHit = true;
    [SerializeField, Min(0f)] private float hitStunSeconds = 0.3f;

    [Header("Recompensa (opcional)")]
    [Tooltip("Objeto (normalmente un WorldPickup ya con sus Effects configurados) que se activa al morir, igual que un cofre normal daría su botín. Se deja desactivado hasta la muerte. Si se deja vacío, no suelta nada.")]
    [SerializeField] private GameObject rewardOnDeath;

    // Estado interno
    private enum State { Disguised, Waking, Chasing, Attacking, TakingDamage, Dead }
    private State currentState = State.Disguised;

    private float lastAttackTime = -999f;
    private bool isAttacking = false;
    private bool isDead = false;
    private bool isAwake = false;
    private float originalSpeed;
    private float _targetRefreshTimer;
    private Coroutine _attackCoroutine;
    private Coroutine _wakeCoroutine;
    private Targetable _targetable;

    private static readonly int AnimIdleChest = Animator.StringToHash("IdleChest");
    private static readonly int AnimIdleBattle = Animator.StringToHash("IdleBattle");
    private static readonly int AnimSense = Animator.StringToHash("SenseSomethingST");
    private static readonly int AnimRun = Animator.StringToHash("Run");
    private static readonly int AnimAttack01 = Animator.StringToHash("Attack01");
    private static readonly int AnimAttack02 = Animator.StringToHash("Attack02");
    private static readonly int AnimGetHit = Animator.StringToHash("GetHit");
    private static readonly int AnimDie = Animator.StringToHash("Die");

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!interactable) interactable = GetComponent<Interactable>();
        if (!agent) agent = GetComponent<NavMeshAgent>();

        // A propósito NO se añade Targetable/EnemyMarker aquí (a diferencia de Spider1AI) — eso
        // pasa en WakeUp(), para que mientras está disfrazado no aparezca como objetivo de
        // combate ni se le pueda enganchar la cámara/L1-R1 como a un enemigo normal.

        if (agent)
        {
            // Se queda quieto y fuera del NavMesh mientras dura el disfraz — se activa en WakeUp().
            agent.enabled = false;
        }

        if (!player)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }
    }

    void Start()
    {
        if (damageable)
        {
            damageable.OnDamaged += OnDamageTaken;
            damageable.OnDied += OnDeath;
            // Este script controla su propia secuencia de muerte (animación Die + dejar el
            // cadáver inerte en la escena, con el rewardOnDeath ya revelado) — si Damageable
            // destruyera el GameObject de inmediato al llegar a 0 vida (comportamiento por
            // defecto), se cortaría la animación de muerte y se llevaría por delante cualquier
            // recompensa que colgara de este mismo objeto.
            damageable.SetDestroyOnDeath(false);
        }

        if (interactable)
        {
            interactable.OnInteract.AddListener(OnInteracted);
        }

        if (agent)
        {
            agent.speed = moveSpeed;
            originalSpeed = moveSpeed;
            agent.stoppingDistance = Mathf.Max(0.05f, attackRange * 0.9f);
        }

        if (rewardOnDeath != null) rewardOnDeath.SetActive(false);

        PlayAnimation(AnimIdleChest);
    }

    void OnDestroy()
    {
        if (damageable)
        {
            damageable.OnDamaged -= OnDamageTaken;
            damageable.OnDied -= OnDeath;
        }
        if (interactable)
        {
            interactable.OnInteract.RemoveListener(OnInteracted);
        }
        ActiveCombatRegistry.UnregisterNPC(gameObject);
    }

    void OnInteracted(GameObject interactor)
    {
        if (currentState != State.Disguised) return;
        WakeUp();
    }

    void Update()
    {
        if (isDead) return;

        if (currentState == State.Disguised || currentState == State.Waking) return;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            if (agent && agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
            return;
        }

        _targetRefreshTimer += Time.deltaTime;
        if (_targetRefreshTimer >= 0.5f)
        {
            _targetRefreshTimer = 0f;
            var nearest = CombatTargetProvider.GetNearestTarget(transform.position);
            if (nearest != null) player = nearest;
        }

        if (!player) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (currentState == State.TakingDamage) return;

        if (distanceToPlayer <= attackRange && !isAttacking)
        {
            _attackCoroutine = StartCoroutine(AttackPlayer());
        }
        else if (distanceToPlayer > attackRange)
        {
            ChasePlayer();
        }
    }

    /// <summary>Se revela: deja de ser un cofre y pasa a ser un enemigo real.</summary>
    void WakeUp()
    {
        if (isAwake || isDead) return;
        isAwake = true;
        currentState = State.Waking;

        if (interactable) interactable.EnableInteraction(false);

        // Cambia de layer "Interactable" (donde vive como cofre) a "Enemy" (donde vive como
        // enemigo real) — así el resto de sistemas de combate (cámara-lock, proyectiles enemigos,
        // detección por capa) lo tratan igual que a Spider1/Demon/Golem a partir de aquí.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) gameObject.layer = enemyLayer;

        _targetable = GetComponent<Targetable>();
        if (_targetable == null) _targetable = gameObject.AddComponent<Targetable>();
        if (GetComponent<EnemyMarker>() == null) gameObject.AddComponent<EnemyMarker>();
        _targetable.isInActiveCombat = true;
        ActiveCombatRegistry.RegisterNPC(gameObject, allowsCameraLock: true);

        if (agent)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.speed = originalSpeed;
                agent.isStopped = false;
            }
        }

        PlayAnimation(AnimSense);
        _wakeCoroutine = StartCoroutine(FinishWaking());
    }

    IEnumerator FinishWaking()
    {
        yield return new WaitForSeconds(wakeAnimSeconds);
        if (isDead) yield break;
        currentState = State.Chasing;
        PlayAnimation(AnimIdleBattle);
    }

    void ChasePlayer()
    {
        if (!player || !agent || !agent.isOnNavMesh) return;

        LookAtPlayer();
        agent.isStopped = false;
        agent.SetDestination(player.position);
        currentState = State.Chasing;
        PlayAnimation(AnimRun);
    }

    IEnumerator AttackPlayer()
    {
        if (Time.time < lastAttackTime + attackCooldown) yield break;

        isAttacking = true;
        currentState = State.Attacking;
        lastAttackTime = Time.time;

        if (agent && agent.isOnNavMesh) agent.isStopped = true;

        LookAtPlayer();
        // Alterna entre los dos ataques del asset para que no se vea siempre el mismo mordisco.
        int anim = Random.value < 0.5f ? AnimAttack01 : AnimAttack02;
        PlayAnimation(anim);

        if (attackLungeDistance > 0f && player)
        {
            Vector3 lungeDir = (player.position - transform.position); lungeDir.y = 0f;
            float currentDist = lungeDir.magnitude;
            if (currentDist > 0.0001f)
            {
                lungeDir /= currentDist;
                float lungeDist = Mathf.Clamp(currentDist - attackRange * 0.5f, 0f, attackLungeDistance);
                if (lungeDist > 0f)
                {
                    Vector3 lungeTarget = transform.position + lungeDir * lungeDist;
                    if (agent && agent.isOnNavMesh) agent.Warp(lungeTarget);
                    else transform.position = lungeTarget;
                }
            }
        }

        yield return new WaitForSeconds(0.35f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange * 1.2f)
        {
            DamagePlayer(damage);
        }

        yield return new WaitForSeconds(0.45f);

        isAttacking = false;
        currentState = (player && Vector3.Distance(transform.position, player.position) <= detectionRange)
            ? State.Chasing
            : State.Chasing; // sin "volver a disfrazarse": una vez despierto, se queda como enemigo
    }

    void DamagePlayer(float dmg)
    {
        if (!player) return;

        var playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(dmg);
            return;
        }

        var dmgable = player.GetComponent<IDamageable>();
        if (dmgable != null && dmgable.IsAlive)
        {
            dmgable.TakeDamage(dmg);
        }
    }

    void LookAtPlayer()
    {
        if (!player) return;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, modelRotationOffset, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    int _currentAnimHash = 0;
    void PlayAnimation(int animHash)
    {
        if (!animator) return;
        if (_currentAnimHash == animHash) return;
        _currentAnimHash = animHash;
        animator.Play(animHash, 0, 0f);
    }

    void OnDamageTaken(float amount)
    {
        if (isDead) return;

        if (currentState == State.Disguised)
        {
            // El primer golpe recibido disfrazado también lo despierta (si está permitido) — el
            // propio daño ya se ha aplicado en Damageable, aquí solo reaccionamos.
            if (wakeOnDamage) WakeUp();
            return;
        }

        if (currentState == State.Waking) return;

        isAttacking = false;
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (stopOnHit)
        {
            StartCoroutine(TakeDamageSequence());
        }
    }

    IEnumerator TakeDamageSequence()
    {
        currentState = State.TakingDamage;

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        PlayAnimation(AnimGetHit);

        float t = 0f;
        while (t < hitStunSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (isDead) yield break;

        if (agent && agent.isOnNavMesh) agent.isStopped = false;
        currentState = State.Chasing;
    }

    void OnDeath()
    {
        if (isDead) return;

        isDead = true;
        currentState = State.Dead;

        if (_targetable != null) _targetable.isInActiveCombat = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);

        if (_wakeCoroutine != null) StopCoroutine(_wakeCoroutine);

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StopAllCoroutines();
        PlayAnimation(AnimDie);

        if (rewardOnDeath != null) rewardOnDeath.SetActive(true);

        StartCoroutine(DisableAfterDeath());
    }

    IEnumerator DisableAfterDeath()
    {
        yield return new WaitForSeconds(1.2f);

        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
