using UnityEngine;
using Game.NPC;
using Game.NPC.Common;

/// <summary>
/// Adaptador entre un NPC de escena y el grafo narrativo.
///
/// NO es una identidad nueva: el ActorId de aquí es siempre el mismo <c>persistenceId</c> que
/// ya usa <see cref="NPCBehaviourManagerV2"/> (el mismo que ya resuelve
/// <c>NarrativeKeyKind.Actor</c> en el editor y que ya usa <c>WaitNpcInteractionNode</c> vía la
/// señal <c>NPC_INTERACT_&#123;persistenceId&#125;</c> que emite <c>NPCBrain.HandleInteraction()</c>).
/// Este componente no registra nada nuevo en <c>NPCRegistry</c> — se apoya por completo en el
/// registro que ya hace <c>NPCBehaviourManagerV2.RegisterNarrativeIdentity()</c>. Un NPC del
/// grafo no necesita este componente para que <c>WaitNpcInteractionNode</c> funcione: le basta
/// con tener <c>persistenceId</c> puesto y no tener asignados <c>questConfig</c> ni
/// <c>interactiveNarrativeConfig</c> (para que el grafo sea la única voz que decide qué pasa al
/// hablar con él).
///
/// Lo que SÍ aporta este componente es un punto único, reutilizable, para las acciones que el
/// futuro <c>NpcActionNode</c> del grafo necesitará (mirar, hablar, gesticular...), envolviendo
/// <see cref="NPCSimpleAnimator"/> tal cual existe hoy. Aditivo — no sustituye ni modifica nada
/// de la identidad NPC existente.
///
/// Ver claude/propuesta-sistema-narrativo-unico-maqueta-2026-09-11.md § 2 (punto 1) en el
/// Project para el diseño completo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NPCBehaviourManagerV2))]
public class NarrativeActor : MonoBehaviour
{
    [Header("Detección Narrativa (legacy)")]
    [Tooltip("Solo lo usan NPCInteractiveNarrativeExecutor.DetectPlayerRoutine() y el icono de alerta " +
             "de ese mismo sistema (congelado, ver TDD § 10 / catalogo-sistemas-legacy-vs-grafo-nuevo). " +
             "Movido aquí desde NPCBehaviourManagerV2 el 12 sept 2026 para que ese archivo deje de " +
             "mostrar campos que solo tienen sentido para el sistema Interactive antiguo. Ningún nodo " +
             "del grafo nuevo lee estos campos (WaitNpcInteractionNode no hace detección por proximidad, " +
             "reacciona a que el jugador interactúe) — quedan aquí solo mientras MainWorld_old siga " +
             "usando el sistema Interactive. Rango de detección del jugador para narrativas con " +
             "autoStartOnDetection=true.")]
    [Min(1f)]
    [SerializeField] private float narrativeDetectionRange = 10f;

    [Tooltip("¿El NPC camina hacia el jugador durante la alerta narrativa? (legacy, ver arriba)")]
    [SerializeField] private bool walkTowardsPlayerOnAlert = true;

    [Tooltip("Distancia mínima para detenerse al acercarse al jugador (legacy, ver arriba)")]
    [Min(0.5f)]
    [SerializeField] private float stopDistanceFromPlayer = 2f;

    [Tooltip("Icono que aparece sobre el NPC al detectar al jugador, solo para el sistema Interactive " +
             "antiguo (legacy, ver arriba). El icono de quest del grafo nuevo usa ShowQuestIcon()/" +
             "HideQuestIcon() más abajo, que no depende de este campo.")]
    [SerializeField] private GameObject narrativeAlertIconPrefab;

    [Header("Icono de quest (grafo nuevo)")]
    [Tooltip("Icono por defecto que se muestra sobre la cabeza de este NPC y en el minimapa cuando " +
             "un WaitNpcInteractionNode del grafo señala que tiene algo pendiente con el jugador, si " +
             "ESE nodo en concreto no trae su propio icono asignado (ver ShowQuestIcon más abajo). " +
             "Se pone aquí, en el NPC, UNA sola vez: cualquier nodo de cualquier grafo (Cap1, Cap2, " +
             "MainNarrative...) que referencie a este NPC sale ya con el icono correcto sin tener que " +
             "repetir la asignación cada vez que se crea un nodo nuevo. Antes cada WaitNpcInteractionNode " +
             "necesitaba su propio icono asignado a mano, así que un grafo que se creaba después (Cap1) " +
             "se quedaba sin icono aunque otro grafo (MainNarrative) ya lo tuviera arreglado — ver " +
             "claude/incidencia-minimapa-sin-indicaciones-maqueta-eldoria-2026-09-12.md.")]
    [SerializeField] private GameObject defaultQuestIconPrefab;

    public float NarrativeDetectionRange => narrativeDetectionRange;
    public bool WalkTowardsPlayerOnAlert => walkTowardsPlayerOnAlert;
    public float StopDistanceFromPlayer => stopDistanceFromPlayer;
    public GameObject NarrativeAlertIconPrefab => narrativeAlertIconPrefab;
    public GameObject DefaultQuestIconPrefab => defaultQuestIconPrefab;

    private NPCBehaviourManagerV2 _manager;
    private NPCAlertIconController _iconController;

    /// <summary>El persistenceId de <see cref="NPCBehaviourManagerV2"/> — no hay dos identidades.</summary>
    public string ActorId => _manager != null ? _manager.PersistenceId : null;

    public NPCBehaviourManagerV2 Manager => _manager;

    private void Awake()
    {
        _manager = GetComponent<NPCBehaviourManagerV2>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_manager != null && string.IsNullOrEmpty(_manager.PersistenceId))
        {
            Debug.LogWarning($"[NarrativeActor:{name}] Este NPC no tiene persistenceId — el grafo no podrá referenciarlo por actorId.");
        }
#endif
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

    // TODO (pendiente del punto 4 del plan — NpcActionNode): MoveToAnchor / TeleportToAnchor /
    // JoinParty / LeaveParty. No se exponen todavía porque requieren revisar primero
    // MoveToPositionSequence/LeadPlayerToAnchorSequence y NPCPartyMember con más detalle — mejor
    // no adivinar su contrato. Se añaden en la Fase 2, cuando el NpcActionNode los necesite de
    // verdad.

    /// <summary>
    /// Muestra un icono persistente sobre la cabeza del NPC y su marcador en el minimapa.
    /// Es el reemplazo, derivado del grafo (usado por <c>WaitNpcInteractionNode</c>), del icono
    /// que antes decidía en solitario <c>NPCQuestConfig</c>/<c>NPCQuestIconManager</c> (sistema
    /// legacy, congelado — ver TDD § 10). No depende de <c>NPCBehaviourManagerV2.Configuration
    /// .questConfig</c> ni del flag <c>NPCBehaviourType.Quest</c>: el propio nodo del grafo decide
    /// cuándo mostrarlo y con qué prefab, así el grafo sigue siendo la única voz narrativa del NPC.
    /// Reutiliza los mismos componentes genéricos (no específicos de quests) que ya usaba el
    /// sistema viejo para pintar: <see cref="NPCAlertIconController"/> (icono de cabeza) y
    /// <see cref="MinimapMarker"/> (marcador de minimapa).
    ///
    /// <paramref name="iconPrefab"/> es el override que trae el nodo concreto (puede ser
    /// <c>null</c>): si no se especifica, se cae a <see cref="defaultQuestIconPrefab"/> — así un
    /// NPC con el icono por defecto puesto en su prefab sale correcto en cualquier nodo de
    /// cualquier grafo sin tener que repetir la asignación en cada uno.
    /// </summary>
    public void ShowQuestIcon(GameObject iconPrefab)
    {
        var resolvedPrefab = iconPrefab != null ? iconPrefab : defaultQuestIconPrefab;
        if (resolvedPrefab == null) return;
        iconPrefab = resolvedPrefab;

        if (_iconController == null)
        {
            // Hijo dedicado para no compartir controlador con otros sistemas de icono del NPC
            // (p. ej. el de alerta de combate) — mismo motivo que ya documentaba
            // NPCQuestIconManager: si comparten controlador, uno puede ocultar el icono del otro.
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

    /// <summary>Oculta el icono de cabeza y el marcador de minimapa mostrados por <see cref="ShowQuestIcon"/>.</summary>
    public void HideQuestIcon()
    {
        if (_iconController != null && _iconController.HasPersistentIcon)
            _iconController.HideAlertIcon();
        GetComponent<MinimapMarker>()?.SetVisible(false);
    }
}
