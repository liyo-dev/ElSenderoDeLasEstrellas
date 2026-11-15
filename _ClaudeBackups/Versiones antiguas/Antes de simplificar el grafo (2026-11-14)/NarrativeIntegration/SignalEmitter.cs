using System.Collections;
using UnityEngine;

/// <summary>
/// Convierte un hecho de la escena (trigger físico, interacción, muerte de un enemigo, acercarse,
/// o «nada más arrancar») en una señal del grafo narrativo (<see cref="DefaultNarrativeSignals.RaiseCustom"/>).
///
/// La escena solo cuenta el hecho («aquí pasó esto»); qué paso de qué misión significa lo decide
/// el grafo (WaitCustomEventNode, CompleteQuestStepsNode…). Para «solo interactuar» basta con
/// <see cref="InteractableToNarrativeEvent"/>.
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

    [Tooltip("Si se rellena, solo emite mientras esa misión esté ACTIVA. Es la guarda buena para " +
             "un trigger de llegada: pasar por delante antes de tiempo no emite nada, y cuando la " +
             "misión está en marcha emite aunque el grafo todavía no haya llegado a esperarla " +
             "(INC-365).")]
    public string soloConLaMision = "";

    [Tooltip("Metros. Si es mayor que cero, además del trigger físico se mira la distancia al " +
             "jugador cuatro veces por segundo y se emite al acercarse. Es la red de seguridad " +
             "para un aviso de llegada: no depende de tags, ni de capas, ni de que el collider " +
             "del jugador dispare el OnTriggerEnter (INC-376).")]
    public float radioDeProximidad = 0f;

    [Header("Debug")]
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    [SerializeField] private bool debugLogs = false;
#endif

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

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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

        if (radioDeProximidad > 0.01f) StartCoroutine(Co_VigilarDistancia());
    }

    /// Mira la distancia al jugador cuatro veces por segundo. Se apaga sola en cuanto emite (o si
    /// el emisor ya está gastado), así que no cuesta nada el resto de la partida.
    private IEnumerator Co_VigilarDistancia()
    {
        var espera = new WaitForSeconds(0.25f);

        // Hay que llegar, no estar ya (ver INC-382): un aviso de llegada solo significa algo si
        // hay un acercamiento. La distancia de referencia se toma cuando el emisor empieza a contar
        // (la misión activa), y solo se arma cuando el jugador se ha acercado de verdad (dos metros
        // menos que donde estaba) o cuando ha estado lejos y vuelve. Mientras la misión no esté
        // activa no se guarda referencia, para no medir desde cualquier punto lejano del mapa.
        bool armado = false;
        float dReferencia = -1f;

        while (!(_used && once))
        {
            yield return espera;

            var jugador = PlayerService.Player;
            if (jugador == null) continue;

            bool cuenta = string.IsNullOrEmpty(soloConLaMision) || _signals == null
                          || _signals.GetQuestState(soloConLaMision) == NarrativeQuestState.Active;
            if (!cuenta) { armado = false; dReferencia = -1f; continue; }

            float d = Vector3.Distance(jugador.transform.position, transform.position);
            if (dReferencia < 0f) dReferencia = d;

            if (!armado)
            {
                if (d <= dReferencia - 2f || d >= radioDeProximidad * 2.5f)
                {
                    armado = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[SignalEmitter:{name}] Armado a {d:F1} m (empezó a {dReferencia:F1} m): " +
                              $"a partir de ahora, acercarse a {radioDeProximidad:F1} m emite '{eventKey}'.");
#endif
                }
                continue;
            }

            if (d <= radioDeProximidad) Send();
        }
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
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[SignalEmitter:{name}] eventKey vacío — no se emite nada.");
#endif
            return;
        }
        if (_used && once) return;

        if (_signals == null) ResolveSignals();
        if (_signals == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[SignalEmitter:{name}] No hay DefaultNarrativeSignals.");
#endif
            return;
        }

        if (!string.IsNullOrWhiteSpace(soloConLaMision)
            && _signals.GetQuestState(soloConLaMision) != NarrativeQuestState.Active)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (debugLogs)
                Debug.Log($"[SignalEmitter:{name}] Ignorado: '{soloConLaMision}' no está activa.");
#endif
            return;
        }

        if (onlyIfListening && !_signals.HasCustomListener(eventKey))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (debugLogs)
                Debug.Log($"[SignalEmitter:{name}] Ignorado: nadie espera '{eventKey}' todavía.");
#endif
            return;
        }

        _used = true;
        _signals.RaiseCustom(eventKey, name);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (debugLogs)
            Debug.Log($"[SignalEmitter:{name}] Emite '{eventKey}' (trigger={trigger}).");
#endif
    }
}
