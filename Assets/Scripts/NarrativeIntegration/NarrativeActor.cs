using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.Common;

/// <summary>
/// Un NPC de escena visto por el grafo narrativo. No es una identidad nueva: su ActorId es el
/// <c>persistenceId</c> de <see cref="NPCBehaviourManagerV2"/>, el mismo que usan las claves de actor
/// del grafo y la señal <c>NPC_INTERACT_{persistenceId}</c> que emite NPCBrain al hablarle.
///
/// Aporta el icono de «tiene algo para ti» (cabeza y minimapa) que pide el grafo mientras espera
/// una charla con este NPC (<see cref="NpcNarrativeAdapter"/>), y acciones sencillas sobre
/// <see cref="NPCSimpleAnimator"/> (mirar, hablar, gestos, emociones).
///
/// También sirve para un actor sin IA (una figura de un pack que solo habla): sin
/// <see cref="NPCBehaviourManagerV2"/>, su id es <see cref="actorIdSinIA"/> y, al pulsar su
/// <see cref="Interactable"/> (modo Custom), sigue el mismo protocolo de charla que un NPC: avisa al
/// grafo (<see cref="AvisarDeLaCharla"/>) y, si el grafo no esperaba esa charla, dice su frase de pie
/// (<see cref="NarrativeStandingLines"/>: la de una misión en curso o la de su agenda). El grafo lo
/// encuentra y le pone el icono a través de <see cref="TryGetSinIA"/>.
/// </summary>
[DisallowMultipleComponent]
public class NarrativeActor : MonoBehaviour
{
    [Header("Detección narrativa (sistema Interactive, congelado)")]
    [Tooltip("Rango de detección del jugador para narrativas Interactive con autoStartOnDetection=true. " +
             "Solo lo usan NPCInteractiveNarrativeExecutor y su icono de alerta (sistema congelado, TDD § 10); " +
             "el grafo no lo lee.")]
    [Min(1f)]
    [SerializeField] private float narrativeDetectionRange = 10f;

    [Tooltip("¿El NPC camina hacia el jugador durante la alerta narrativa? (sistema Interactive)")]
    [SerializeField] private bool walkTowardsPlayerOnAlert = true;

    [Tooltip("Distancia mínima para detenerse al acercarse al jugador (sistema Interactive)")]
    [Min(0.5f)]
    [SerializeField] private float stopDistanceFromPlayer = 2f;

    [Tooltip("Icono que aparece sobre el NPC al detectar al jugador, solo para el sistema Interactive. " +
             "El icono que pide el grafo es el de abajo.")]
    [SerializeField] private GameObject narrativeAlertIconPrefab;

    [Header("Icono de quest (grafo)")]
    [Tooltip("Icono que se muestra sobre la cabeza de este NPC y en el minimapa cuando el grafo espera " +
             "que el jugador hable con él, si el nodo no trae uno propio. Se pone una sola vez aquí y " +
             "vale para cualquier nodo de cualquier grafo.")]
    [SerializeField] private GameObject defaultQuestIconPrefab;

    [Header("Actor sin IA")]
    [Tooltip("Id del actor cuando el objeto no es un NPC con IA (sin NPCBehaviourManagerV2): con él el grafo lo encuentra, " +
             "le pone el icono y le da sus frases como a un NPC. Hace falta un Interactable (modo Custom) en el mismo objeto. " +
             "En un NPC con IA se usa su persistenceId y esto se ignora.")]
    [SerializeField] private string actorIdSinIA;

    // Actores sin IA activos, por id (los NPCs con IA están en NPCRegistry).
    private static readonly Dictionary<string, NarrativeActor> s_sinIA = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_sinIA.Clear();
#endif

    public float NarrativeDetectionRange => narrativeDetectionRange;
    public bool WalkTowardsPlayerOnAlert => walkTowardsPlayerOnAlert;
    public float StopDistanceFromPlayer => stopDistanceFromPlayer;
    public GameObject NarrativeAlertIconPrefab => narrativeAlertIconPrefab;
    public GameObject DefaultQuestIconPrefab => defaultQuestIconPrefab;

    private NPCBehaviourManagerV2 _manager;
    private NPCAlertIconController _iconController;

    // El icono no se pinta directamente: ShowQuestIcon/HideQuestIcon guardan qué se quiere mostrar
    // y ApplyIconState decide si puede verse ahora. Mientras hay un diálogo, una cinemática o un
    // combate con este NPC, se queda oculto y vuelve solo al terminar si se sigue queriendo.
    private bool _iconWanted;
    private GameObject _lastRequestedIconPrefab;
    private bool _isInDialogue;
    private bool _cinematicActive;

    private bool IsIconSuppressed => _isInDialogue || _cinematicActive || (_manager != null && _manager.IsInCombatOrAlert);

    /// <summary>El persistenceId de <see cref="NPCBehaviourManagerV2"/> — no hay dos identidades —, o el id de actor sin IA.</summary>
    public string ActorId => _manager != null ? _manager.PersistenceId : actorIdSinIA;

    /// <summary>El actor sin IA activo con ese id (los NPCs con IA se buscan en <see cref="NPCRegistry"/>).</summary>
    public static bool TryGetSinIA(string actorId, out NarrativeActor actor)
    {
        actor = null;
        return !string.IsNullOrEmpty(actorId) && s_sinIA.TryGetValue(actorId, out actor) && actor != null;
    }

    /// <summary>
    /// Avisa al grafo de que el jugador habla con el actor (señal NPC_INTERACT_&lt;id&gt; de
    /// <see cref="WaitNpcInteractionNode"/>). True si el grafo estaba esperando esa charla y se queda con ella.
    /// </summary>
    public static bool AvisarDeLaCharla(string actorId, string quien)
    {
        if (string.IsNullOrEmpty(actorId)) return false;
        string clave = WaitNpcInteractionNode.SignalKeyFor(actorId);
        var senales = DefaultNarrativeSignals.Instance;
        // La reserva se consulta antes de emitir: el oyente puede retirarse al recibirla.
        bool reservada = senales != null && senales.HasCustomListener(clave);
        senales?.RaiseCustom(clave, $"Interacción con {quien}");
        return reservada;
    }

    private Interactable _interactable;

    public NPCBehaviourManagerV2 Manager => _manager;

    private void Awake()
    {
        _manager = GetComponent<NPCBehaviourManagerV2>();
        if (_manager == null) _interactable = GetComponent<Interactable>();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (string.IsNullOrEmpty(ActorId))
        {
            Debug.LogWarning($"[NarrativeActor:{name}] Sin id de actor (persistenceId del NPC o id de actor sin IA) — el grafo no podrá referenciarlo.");
        }
        else if (_manager == null && _interactable == null)
        {
            Debug.LogWarning($"[NarrativeActor:{name}] Actor sin IA sin Interactable: no se le puede hablar.");
        }
#endif
    }

    private void OnEnable()
    {
        // Estado inicial por si ya hay una cinemática en curso cuando este NPC se activa (p. ej.
        // un NPC que entra en escena mientras una cinemática global ya está corriendo).
        _cinematicActive = CinematicSequencerBase.AnySequenceActive;
        // Mismo motivo para el diálogo: si este NPC se activa con una conversación ya en pantalla,
        // el evento OnDialogueStarted ya pasó y no volverá — hay que leer el estado actual.
        _isInDialogue = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;

        DialogueManager.OnDialogueStarted += HandleDialogueStarted;
        DialogueManager.OnDialogueClosed += HandleDialogueClosed;
        CinematicSequencerBase.OnAnySequenceActiveChanged += HandleCinematicActiveChanged;

        if (_manager == null && !string.IsNullOrEmpty(actorIdSinIA))
        {
            s_sinIA[actorIdSinIA] = this;
            if (_interactable != null) _interactable.OnInteract.AddListener(AlHablarle);
        }
    }

    private void OnDisable()
    {
        DialogueManager.OnDialogueStarted -= HandleDialogueStarted;
        DialogueManager.OnDialogueClosed -= HandleDialogueClosed;
        CinematicSequencerBase.OnAnySequenceActiveChanged -= HandleCinematicActiveChanged;

        if (_manager == null && !string.IsNullOrEmpty(actorIdSinIA))
        {
            if (s_sinIA.TryGetValue(actorIdSinIA, out var registrado) && registrado == this) s_sinIA.Remove(actorIdSinIA);
            if (_interactable != null) _interactable.OnInteract.RemoveListener(AlHablarle);
        }
    }

    /// <summary>Charla con un actor sin IA: primero el grafo; si no la esperaba, su frase de pie.</summary>
    private void AlHablarle(GameObject quien)
    {
        if (AvisarDeLaCharla(ActorId, name)) return;
        NarrativeStandingLines.TryPlay(ActorId, transform);
    }

    private void HandleDialogueStarted(Transform npcInvolved)
    {
        // Cualquier diálogo abierto oculta el icono, sea con quien sea: el jugador no puede hablar
        // con dos NPCs a la vez, y así no depende de identificar bien al interlocutor.
        _isInDialogue = true;
        ApplyIconState();
    }

    private void HandleDialogueClosed(Transform npcInvolved)
    {
        _isInDialogue = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
        ApplyIconState();
    }

    private void HandleCinematicActiveChanged(bool active)
    {
        _cinematicActive = active;
        ApplyIconState();
    }

    /// <summary>
    /// Único punto que pinta/oculta de verdad el icono, según lo último pedido
    /// (_iconWanted/_lastRequestedIconPrefab) y si está suprimido ahora mismo.
    /// </summary>
    private void ApplyIconState()
    {
        if (_manager == null && _iconController == null && !_iconWanted)
            return; // nada que hacer todavía (aún no se pidió mostrar nada)

        if (IsIconSuppressed || !_iconWanted)
        {
            ExecuteHideQuestIcon();
        }
        else
        {
            ExecuteShowQuestIcon(_lastRequestedIconPrefab);
        }
    }

    /// <summary>
    /// Pide el icono persistente sobre la cabeza del NPC y su marcador en el minimapa (con
    /// <see cref="NPCAlertIconController"/> y <see cref="MinimapMarker"/>). Lo decide el grafo, que
    /// es la única voz narrativa del NPC. <paramref name="iconPrefab"/> es el icono propio del nodo
    /// (puede ser null): sin él se usa <see cref="defaultQuestIconPrefab"/>. Se ve cuando
    /// <see cref="ApplyIconState"/> lo permite.
    /// </summary>
    public void ShowQuestIcon(GameObject iconPrefab)
    {
        _iconWanted = true;
        _lastRequestedIconPrefab = iconPrefab;
        ApplyIconState();
    }

    /// <summary>Oculta el icono de cabeza y el marcador de minimapa mostrados por <see cref="ShowQuestIcon"/>.</summary>
    public void HideQuestIcon()
    {
        _iconWanted = false;
        ApplyIconState();
    }

    /// <summary>Pintado real del icono — solo debe llamarse desde <see cref="ApplyIconState"/>.</summary>
    private void ExecuteShowQuestIcon(GameObject iconPrefab)
    {
        var resolvedPrefab = iconPrefab != null ? iconPrefab : defaultQuestIconPrefab;
        if (resolvedPrefab == null) return;
        iconPrefab = resolvedPrefab;

        if (_iconController == null)
        {
            // Hijo dedicado para no compartir controlador con otros iconos del NPC (p. ej. el de
            // alerta de combate): si lo compartieran, uno podría ocultar el del otro.
            const string childName = "_GraphQuestIconController";
            var iconChild = transform.Find(childName);
            if (iconChild == null)
            {
                iconChild = new GameObject(childName).transform;
                iconChild.SetParent(transform, false);
            }
            _iconController = iconChild.GetComponent<NPCAlertIconController>()
                ?? iconChild.gameObject.AddComponent<NPCAlertIconController>();
        }
        _iconController.ShowPersistentIcon(iconPrefab);

        var marker = GetComponent<MinimapMarker>() ?? gameObject.AddComponent<MinimapMarker>();
        var sr = iconPrefab.GetComponentInChildren<SpriteRenderer>();
        marker.SetIcon(sr != null ? sr.sprite : null, Color.white);
        marker.SetVisible(true);
    }

    /// <summary>Ocultado real del icono — solo debe llamarse desde <see cref="ApplyIconState"/>.</summary>
    private void ExecuteHideQuestIcon()
    {
        if (_iconController != null && _iconController.HasPersistentIcon)
            _iconController.HideAlertIcon();
        GetComponent<MinimapMarker>()?.SetVisible(false);
    }

    /// <summary>Gira al NPC para mirar hacia un punto del mundo (usa NPCSimpleAnimator.FaceTarget).</summary>
    public void Face(Vector3 worldPosition) => _manager?.SimpleAnimator?.FaceTarget(worldPosition);

    /// <summary>Activa/desactiva la pose de "hablando" (usada durante diálogos del grafo).</summary>
    public void Talk(bool isTalking) => _manager?.SimpleAnimator?.SetTalking(isTalking);

    /// <summary>Reproduce un gesto social por nombre de estado de animación.</summary>
    public void PlayGesture(string stateName, System.Action onComplete = null) =>
        _manager?.SimpleAnimator?.PlaySocialGesture(stateName, onComplete);

    /// <summary>Reproduce una emoción corporal (ver NPCEmotion).</summary>
    public void SetEmotion(NPCEmotion emotion) => _manager?.SimpleAnimator?.PlayBodyEmotion(emotion);
}
