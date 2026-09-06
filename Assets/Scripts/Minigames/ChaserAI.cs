using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA simple de persecución para el minijuego "Pilla Pilla".
/// Persigue al jugador constantemente usando NavMeshAgent.
/// 
/// ANIMACIONES:
/// - Si el personaje tiene NPCSimpleAnimator (como Estela), lo usa automáticamente.
/// - Si no, usa el Animator directamente con el parámetro configurado.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ChaserAI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private NavMeshAgent agent;
    
    [Header("Configuración")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float catchDistance = 1.2f;
    [SerializeField] private float updatePathInterval = 0.2f;

    [Header("Animación (Solo si NO tiene NPCSimpleAnimator)")]
    [Tooltip("Solo se usa si el personaje NO tiene NPCSimpleAnimator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runAnimParam = "IsRunning";
    
    // Evento cuando atrapa al jugador
    public System.Action OnCaughtPlayer;
    
    // ✅ Propiedades públicas para configurar desde TagMinigameController
    public float ChaseSpeed
    {
        get => chaseSpeed;
        set
        {
            chaseSpeed = value;
            if (agent) agent.speed = chaseSpeed;
        }
    }
    
    public float CatchDistance
    {
        get => catchDistance;
        set => catchDistance = value;
    }
    
    public float Acceleration
    {
        get => agent != null ? agent.acceleration : 8f;
        set { if (agent) agent.acceleration = value; }
    }

    // Referencias internas
    private NPCSimpleAnimator _npcAnimator;
    private bool _useNpcAnimator;
    private bool _isChasing;
    private float _lastPathUpdate;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        
        // Intentar obtener NPCSimpleAnimator (usado por Estela y otros NPCs)
        _npcAnimator = GetComponent<NPCSimpleAnimator>();
        _useNpcAnimator = _npcAnimator != null;
        
        // Solo buscar Animator si no tiene NPCSimpleAnimator
        if (!_useNpcAnimator && !animator)
        {
            animator = GetComponent<Animator>();
        }
        
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        
        if (_useNpcAnimator)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChaserAI] Usando NPCSimpleAnimator para animaciones de {name}");
#endif
        }
        else if (animator)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChaserAI] Usando Animator directo con parámetro '{runAnimParam}' para {name}");
#endif
        }
    }

    void Start()
    {
        // Buscar al jugador si no está asignado (usando PlayerService para mejor rendimiento)
        if (!target)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
            }
        }

        if (agent)
        {
            agent.speed = chaseSpeed;
            if (agent.isOnNavMesh)
                agent.isStopped = true;
        }
    }

    void Update()
    {
        if (!_isChasing || !target || !agent) return;

        // Actualizar destino periódicamente
        if (Time.time - _lastPathUpdate >= updatePathInterval)
        {
            agent.SetDestination(target.position);
            _lastPathUpdate = Time.time;
        }
        
        // Actualizar animación de movimiento (para NPCSimpleAnimator)
        if (_useNpcAnimator && _npcAnimator != null)
        {
            float normalizedSpeed = agent.velocity.magnitude / chaseSpeed;
            _npcAnimator.SetMovementSpeed(Mathf.Clamp01(normalizedSpeed));
        }

        // Verificar si atrapó al jugador
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= catchDistance)
        {
            CatchPlayer();
        }
    }

    /// <summary>
    /// Inicia la persecución
    /// </summary>
    public void StartChasing()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ChaserAI] 🏃 StartChasing() llamado en {name}");
#endif
        
        // ✅ FIX: Asegurar que tenemos el NavMeshAgent (puede haberse añadido dinámicamente)
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[ChaserAI] ❌ No hay NavMeshAgent en {name}. Añadiendo uno...");
#endif
                agent = gameObject.AddComponent<NavMeshAgent>();
            }
        }
        
        // ✅ FIX: Asegurar que tenemos el sistema de animación
        if (_npcAnimator == null)
        {
            _npcAnimator = GetComponent<NPCSimpleAnimator>();
            _useNpcAnimator = _npcAnimator != null;
        }
        if (!_useNpcAnimator && animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        if (!target)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ChaserAI] ✅ Target encontrado: {target.name}");
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[ChaserAI] ❌ No se encontró al jugador para perseguir");
#endif
                return;
            }
        }

        _isChasing = true;
        
        if (agent)
        {
            // Verificar que el NavMeshAgent esté en NavMesh válido
            if (!agent.isOnNavMesh)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ChaserAI] ⚠️ Agent no está en NavMesh. Intentando warpar...");
#endif
                // Intentar colocar en NavMesh
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[ChaserAI] ✅ Warpado a posición válida de NavMesh: {hit.position}");
#endif
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[ChaserAI] ❌ No se encontró NavMesh cerca del perseguidor");
#endif
                    return;
                }
            }
            
            // ✅ FIX: Asegurar que el agente escribe la posición calculada al Transform.
            // Si el NPC quedó "congelado" en un estado previo de su FSM (p.ej.
            // FollowPlayerState parado junto al jugador) con updatePosition en false,
            // el NavMeshAgent puede seguir moviéndose internamente sin que el personaje
            // se mueva visualmente (se queda "pillado"). Restaurar aquí es la garantía
            // final del contrato de traspaso de control descrito en TDD.md.
            // NOTA: NO tocar updateRotation — si hay NPCSimpleAnimator (caso de Estela),
            // este ya controla la rotación en su propio LateUpdate (ApplySmoothRotation);
            // poner updateRotation=true haría que el NavMeshAgent y el animator se
            // peleen por rotar el Transform cada frame (mismo patrón de bug que el
            // temblor de rotación corregido en TabernaSequencer).
            agent.updatePosition = true;
            agent.isStopped = false;
            agent.SetDestination(target.position);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChaserAI] ✅ NavMeshAgent configurado. Speed: {agent.speed}, Destination: {target.position}");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ChaserAI] ❌ No hay NavMeshAgent asignado");
#endif
        }

        // Activar animación de correr
        if (_useNpcAnimator && _npcAnimator != null)
        {
            _npcAnimator.SetMovementSpeed(1f); // Velocidad máxima
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ChaserAI] 🎬 Animación via NPCSimpleAnimator activada");
#endif
        }
        else if (animator && !string.IsNullOrEmpty(runAnimParam))
        {
            animator.SetBool(runAnimParam, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChaserAI] 🎬 Animación via Animator activada (param: {runAnimParam})");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[ChaserAI] ⚠️ No hay sistema de animación disponible");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ChaserAI] ¡Comenzó la persecución!");
#endif
    }

    /// <summary>
    /// Detiene la persecución
    /// </summary>
    public void StopChasing()
    {
        _isChasing = false;

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Detener animación
        if (_useNpcAnimator && _npcAnimator != null)
        {
            _npcAnimator.SetMovementSpeed(0f);
        }
        else if (animator && !string.IsNullOrEmpty(runAnimParam))
        {
            animator.SetBool(runAnimParam, false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ChaserAI] Persecución detenida.");
#endif
    }

    /// <summary>
    /// Reinicia el perseguidor a su posición inicial
    /// </summary>
    public void ResetToStart()
    {
        StopChasing();
        
        if (agent)
        {
            agent.Warp(_startPosition);
        }
        else
        {
            transform.position = _startPosition;
        }
        
        transform.rotation = _startRotation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ChaserAI] Reiniciado a posición inicial.");
#endif
    }

    /// <summary>
    /// Establece una nueva posición inicial
    /// </summary>
    public void SetStartPosition(Vector3 position, Quaternion rotation)
    {
        _startPosition = position;
        _startRotation = rotation;
    }
    
    /// <summary>
    /// Teletransporta al perseguidor a una posición específica (sin detener la persecución)
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        bool wasChasing = _isChasing;
        
        if (agent)
        {
            agent.isStopped = true;
            agent.Warp(position);
            
            // Si estaba persiguiendo, continuar
            if (wasChasing)
            {
                agent.isStopped = false;
                if (target != null)
                {
                    agent.SetDestination(target.position);
                }
            }
        }
        else
        {
            transform.position = position;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ChaserAI] ⚡ Teletransportado a {position}");
#endif
    }

    private void CatchPlayer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ChaserAI] ¡Jugador atrapado!");
#endif
        StopChasing();
        OnCaughtPlayer?.Invoke();
    }

    /// <summary>
    /// Asigna el objetivo a perseguir
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Indica si está persiguiendo activamente
    /// </summary>
    public bool IsChasing => _isChasing;
    
    /// <summary>
    /// Reinicializa el NavMeshAgent después de un cambio de escena.
    /// Útil cuando el perseguidor es DontDestroyOnLoad y necesita funcionar en nuevas escenas.
    /// </summary>
    public void ReinitializeAfterSceneChange(Transform newTarget = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ChaserAI] 🔄 ReinitializeAfterSceneChange llamado en {name}");
#endif
        
        bool wasChasing = _isChasing;
        
        // Actualizar target si se proporciona uno nuevo
        if (newTarget != null)
        {
            target = newTarget;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ChaserAI] ✅ Target actualizado a: {target.name}");
#endif
        }
        
        // Buscar el jugador si no hay target
        if (target == null)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                target = playerGo.transform;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ChaserAI] ✅ Target encontrado via PlayerService: {target.name}");
#endif
            }
        }
        
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        
        if (agent != null)
        {
            // Verificar que el NavMeshAgent esté en un NavMesh válido
            if (!agent.isOnNavMesh)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ChaserAI] ⚠️ Agent no está en NavMesh después del cambio de escena");
#endif
                
                // Buscar una posición válida en NavMesh
                if (NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas))
                {
                    agent.enabled = false; // Deshabilitar temporalmente para poder mover
                    transform.position = hit.position;
                    agent.enabled = true;
                    
                    // Forzar el warp
                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(hit.position);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[ChaserAI] ✅ Warpado a NavMesh válido: {hit.position}");
#endif
                    }
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[ChaserAI] ❌ No se encontró NavMesh cerca de {transform.position}");
#endif
                }
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ChaserAI] ✅ Agent ya está en NavMesh en posición: {transform.position}");
#endif
            }
            
            // Configurar velocidad
            agent.speed = chaseSpeed;
            
            // Si estaba persiguiendo, continuar
            if (wasChasing && target != null)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ChaserAI] 🏃 Persecución retomada hacia: {target.position}");
#endif
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Mostrar distancia de captura
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}
