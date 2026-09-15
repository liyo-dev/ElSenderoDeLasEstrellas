using System.Collections;
using UnityEngine;

/// <summary>
/// Componente genérico: convierte un hecho de la escena (trigger físico, recogida,
/// interacción, muerte de un enemigo, o "nada más arrancar") en una señal del grafo
/// narrativo (<see cref="DefaultNarrativeSignals.RaiseCustom"/>).
///
/// Es el reemplazo aditivo — propuesto en
/// claude/propuesta-sistema-narrativo-unico-maqueta-2026-09-11.md § 2 punto 3, hueco real
/// nº 3 de claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md — para los 7
/// micro-componentes de quest legacy (<c>QuestStarterOnInteract</c>,
/// <c>QuestProgressOnTrigger</c>, <c>QuestProgressOnEvent</c>, <c>SimpleQuestPickup</c>,
/// <c>QuestKillContributor</c>, <c>QuestCompletionRelay</c>, <c>QuestObjectDisabler</c>):
/// ninguno de ellos conocía un <c>questId</c> ni un <c>stepIndex</c> por diseño — la escena
/// solo emite el hecho ("aquí pasó esto"), y es el grafo quien decide qué step de qué quest
/// significa esa señal (con <c>WaitCustomEventNode</c>, <c>RequireInventoryItemNode</c>,
/// <c>WaitForItemAddedNode</c>, <c>CompleteQuestStepsNode</c>...).
///
/// No sustituye a <see cref="InteractableToNarrativeEvent"/> (que ya cubre el caso más común
/// — interactuar → señal — y sigue en uso real en <c>Eldoria_Cap1/</c>): lo complementa con
/// los disparadores físicos que ese componente no cubre (trigger de zona, recogida, muerte de
/// enemigo). Para "solo interactuar", sigue siendo más simple usar
/// <see cref="InteractableToNarrativeEvent"/> directamente.
/// </summary>
[DisallowMultipleComponent]
public class SignalEmitter : MonoBehaviour
{
    public enum TriggerType
    {
        Manual,         // Solo por código/UnityEvent externo, llamando a Send().
        SendNow,        // Al arrancar la escena.
        PhysicsTrigger, // OnTriggerEnter de un Collider con el tag indicado.
        OnInteract,     // Interactable.OnInteract de este mismo GameObject.
        OnEnemyDied     // Damageable.OnDied de este mismo GameObject (prefab de enemigo).
    }

    [Tooltip("Clave de la señal que emitirá el grafo (la misma que espera un WaitCustomEventNode o similar).")]
    public string eventKey = "";

    [Tooltip("Qué hecho de la escena dispara la señal.")]
    public TriggerType trigger = TriggerType.OnInteract;

    [Tooltip("Solo para Physics Trigger: tag que debe tener el Collider que entra.")]
    public string requiredTag = "Player";

    [Tooltip("Si está marcado, solo se emite una vez (una segunda entrada/interacción/muerte no hace nada).")]
    public bool once = true;

    [Tooltip("Si está marcado, NO emite mientras nadie del grafo esté esperando esta clave todavía " +
             "(evita dejar el hecho 'pendiente' para que lo consuma un WaitCustomEventNode al que se " +
             "llegue mucho después, como si acabara de pasar). Dejar desmarcado (comportamiento normal, " +
             "sticky) para hechos que deben recordarse pase lo que pase, como contar muertes o recogidas.")]
    public bool onlyIfListening = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool _used;
    private DefaultNarrativeSignals _signals;
    private Interactable _interactable;
    private Damageable _damageable;

    private void Awake()
    {
        ResolveSignals();

        if (trigger == TriggerType.OnInteract)
            _interactable = GetComponent<Interactable>();
        else if (trigger == TriggerType.OnEnemyDied)
            _damageable = GetComponent<Damageable>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (trigger == TriggerType.OnInteract && _interactable == null)
            Debug.LogWarning($"[SignalEmitter:{name}] trigger=OnInteract pero no hay Interactable en este GameObject.");
        if (trigger == TriggerType.OnEnemyDied && _damageable == null)
            Debug.LogWarning($"[SignalEmitter:{name}] trigger=OnEnemyDied pero no hay Damageable en este GameObject.");
#endif
    }

    private void OnEnable()
    {
        if (_interactable != null)
            _interactable.OnInteract.AddListener(HandleInteract);
        if (_damageable != null)
            _damageable.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (_interactable != null)
            _interactable.OnInteract.RemoveListener(HandleInteract);
        if (_damageable != null)
            _damageable.OnDied -= HandleDied;
    }

    private void Start()
    {
        if (trigger == TriggerType.SendNow)
            StartCoroutine(SendWhenReady());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (trigger != TriggerType.PhysicsTrigger) return;
        if (_used && once) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        Send();
    }

    private void HandleInteract(GameObject _)
    {
        if (_used && once) return;
        Send();
    }

    private void HandleDied()
    {
        if (_used && once) return;
        Send();
    }

    private IEnumerator SendWhenReady()
    {
        // Espera breve a que los managers de Start se inicialicen (mismo patrón que
        // InteractableToNarrativeEvent.SendWhenReady).
        float timeout = 2f;
        while (_signals == null && timeout > 0f)
        {
            ResolveSignals();
            if (_signals != null) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_signals == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SignalEmitter:{name}] No hay DefaultNarrativeSignals tras esperar.");
#endif
            yield break;
        }

        Send();
    }

    private void ResolveSignals()
    {
        if (_signals != null) return;
        _signals = DefaultNarrativeSignals.EnsureInstance();
    }

    /// <summary>Emite la señal ahora mismo. Público para poder llamarlo también desde un UnityEvent existente.</summary>
    public void Send()
    {
        if (string.IsNullOrEmpty(eventKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SignalEmitter:{name}] eventKey vacío — no se emite nada.");
#endif
            return;
        }
        if (_used && once) return;

        if (_signals == null) ResolveSignals();
        if (_signals == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SignalEmitter:{name}] No hay DefaultNarrativeSignals.");
#endif
            return;
        }

        if (onlyIfListening && !_signals.HasCustomListener(eventKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLogs)
                Debug.Log($"[SignalEmitter:{name}] Ignorado: nadie espera '{eventKey}' todavía.");
#endif
            return;
        }

        _used = true;
        _signals.RaiseCustom(eventKey, name);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs)
            Debug.Log($"[SignalEmitter:{name}] Emite '{eventKey}' (trigger={trigger}).");
#endif
    }
}
