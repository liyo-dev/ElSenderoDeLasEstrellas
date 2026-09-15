using System;
using UnityEngine;
using Game.NPC;

/// <summary>
/// Espera a que el jugador entregue por proximidad, llevándolo en brazos, un objeto concreto a
/// un NPC — y dispara la secuencia de "recibir el objeto" antes de dejar que el grafo continúe.
///
/// Generaliza y sustituye, para cualquier misión de "llévale este objeto a un NPC", al sistema
/// legacy <c>NPCItemDetector</c> (atado a <c>NPCQuestConfig.enableItemDetection</c>, congelado
/// en los NPCs migrados al grafo — ver
/// claude/incidencia-caja-eldran-no-desaparece-sin-interrogacion-2026-09-14.md). A diferencia de
/// ese sistema, este nodo NO completa la quest al detectar la entrega — solo resuelve la
/// entrega física (objeto, VFX/SFX, gesto del NPC, icono de "habla conmigo"). Completar la quest
/// de verdad es cosa del siguiente nodo del grafo (normalmente un <see cref="WaitNpcInteractionNode"/>
/// de turn-in), así el icono que deja puesto este nodo significa, literalmente, "habla conmigo".
///
/// Ver claude/propuesta-mejora-secuencia-entrega-objetos-npc-2026-09-15.md en el Project para el
/// diseño completo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("NPCs", "Esperar entrega de objeto", "Espera a que el jugador le entregue por proximidad un objeto llevado en brazos a un NPC: el objeto desaparece con VFX/SFX, el NPC se gira y hace un gesto, y queda el icono de 'habla conmigo'.")]
[SavePoint("Seguro guardar mientras espera")]
public sealed class WaitItemDeliveryNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC que recibe el objeto (el mismo que usa NPCBehaviourManagerV2).")]
    public string npcId;

    [Tooltip("Tipo de objeto que se espera entregar (ObjectType del PickupObject que lleva el jugador).")]
    public ObjectType expectedItemType = ObjectType.Caja;

    [Min(0.5f)]
    [Tooltip("Radio de detección de la entrega, en metros, alrededor del NPC.")]
    public float detectionRadius = 3f;

    [Tooltip("VFX de un solo uso al desaparecer el objeto (se reproduce vía VfxPoolService, nunca Instantiate/Destroy directo — ver CLAUDE.md § 2).")]
    public GameObject deliveryVfxPrefab;

    [Min(0.1f)]
    [Tooltip("Vida del VFX de entrega, en segundos.")]
    public float vfxLifetime = 2f;

    [Tooltip("Clave de SFX en AudioGraphProfile a reproducir al entregar el objeto (ej: 'Chest').")]
    public string deliverySfxKey = "Chest";

    [Tooltip("Nombre del estado de animación del gesto que hace el NPC al recibir el objeto (ej: 'HandClap01', ya presente en NPC_NoWeapon.controller).")]
    public string deliveryGesture = "HandClap01";

    [Tooltip("Icono a mostrar sobre la cabeza del NPC tras la entrega, si quieres uno distinto al icono por defecto del NPC. Vacío = usa el 'Default Quest Icon Prefab' del NarrativeActor (recomendado, mismo criterio que WaitNpcInteractionNode.questIcon).")]
    public GameObject questIcon;

    [NonSerialized] private NpcItemDeliveryDetector _detector;
    [NonSerialized] private Action _ready;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning($"[WaitItemDeliveryNode:{guid}] npcId vacío → avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        if (npc == null)
        {
            Debug.LogWarning($"[WaitItemDeliveryNode:{guid}] NPC '{npcId}' no encontrado en NPCRegistry → avanzando.");
            onReadyToAdvance?.Invoke();
            return;
        }

        _ready = onReadyToAdvance;

        _detector = npc.gameObject.AddComponent<NpcItemDeliveryDetector>();
        _detector.Setup(detectionRadius, expectedItemType, OnDelivered);

        // El icono de "vuelve a hablarme" se muestra desde YA, en cuanto el jugador lleva el
        // objeto y se acerca — no solo al terminar la entrega (fix 15 sept 2026, ver comentario
        // de Raúl en la propuesta): antes se mostraba únicamente en OnDelivered(), así que entre
        // recoger el objeto y llegar hasta el NPC no había ningún icono indicando que había que
        // volver aquí. Es el mismo icono que se deja puesto tras la entrega (ShowQuestIcon es
        // idempotente si ya está mostrando el mismo prefab), así que no hay parpadeo al entregar.
        npc.GetComponent<NarrativeActor>()?.ShowQuestIcon(questIcon);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WaitItemDeliveryNode:{guid}] Esperando entrega de '{expectedItemType}' a '{npcId}' (radio {detectionRadius}m)...");
#endif
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_detector != null)
        {
            UnityEngine.Object.Destroy(_detector);
            _detector = null;
        }
        _ready = null;
    }

    private void OnDelivered(GameObject itemObject)
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        var actor = npc != null ? npc.GetComponent<NarrativeActor>() : null;

        // 1. Congelar el estado de "llevar en brazos" sin soltar físicamente el objeto todavía
        //    (mismo truco que usaba NPCItemDetector.ForceStopCarrying).
        if (PlayerService.TryGetComponent(out PlayerCarrySystem carrySystem)
            && carrySystem.IsCarrying && carrySystem.CarriedObject == itemObject)
        {
            carrySystem.CancelCarrySilently();
        }

        // 2. El NPC mira al jugador y hace el gesto de recibir el objeto.
        if (actor != null)
        {
            var playerTransform = PlayerService.PlayerTransform;
            if (playerTransform != null)
                actor.Face(playerTransform.position);

            if (!string.IsNullOrEmpty(deliveryGesture))
                actor.PlayGesture(deliveryGesture);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning($"[WaitItemDeliveryNode:{guid}] '{npcId}' no tiene NarrativeActor — no se puede girar ni hacer el gesto de recibir el objeto.");
        }
#endif

        // 3. El objeto desaparece con VFX + SFX.
        if (itemObject != null)
        {
            Vector3 vfxPos = itemObject.transform.position;

            if (deliveryVfxPrefab != null && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(deliveryVfxPrefab, vfxPos, Quaternion.identity, vfxLifetime);

            if (!string.IsNullOrEmpty(deliverySfxKey) && AudioService.Instance != null)
                AudioService.Instance.PlaySFX(deliverySfxKey, 1f, vfxPos);

            UnityEngine.Object.Destroy(itemObject);
        }

        // 4. Icono "habla conmigo" — mismo sistema que ya usa WaitNpcInteractionNode, así el
        //    siguiente nodo de turn-in no tiene que volver a pintarlo.
        actor?.ShowQuestIcon(questIcon);

        var ready = _ready;
        _ready = null;
        ready?.Invoke();
    }
}
