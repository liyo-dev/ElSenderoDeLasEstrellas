using System;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;

/// Espera a que el jugador entregue por proximidad, llevándolo en brazos, un objeto concreto a un
/// NPC, y hace la entrega: el objeto desaparece con VFX y SFX, el NPC se gira y hace un gesto, y
/// queda el icono de «habla conmigo». No completa la misión: eso lo hace el paso siguiente
/// (normalmente esperar la charla con el NPC), así el icono significa literalmente «habla conmigo».
[Serializable]
[NarrativeNodeInfo("NPCs", "Esperar entrega de objeto", "Espera a que el jugador le entregue por proximidad un objeto llevado en brazos a un NPC: el objeto desaparece con VFX/SFX, el NPC se gira y hace un gesto, y queda el icono de 'habla conmigo'.")]
public sealed class PedirEntregaDeObjeto : NarrativeRequest, INarrativeStateEffect
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC que recibe el objeto.")]
    public string npcId;

    [Tooltip("Tipo de objeto que se espera (ObjectType del PickupObject que lleva el jugador).")]
    public ObjectType expectedItemType = ObjectType.Caja;

    [Min(0.5f)]
    [Tooltip("Radio de detección de la entrega, en metros, alrededor del NPC.")]
    public float detectionRadius = 3f;

    [Tooltip("VFX de un solo uso al desaparecer el objeto (vía VfxPoolService).")]
    public GameObject deliveryVfxPrefab;

    [Min(0.1f)]
    [Tooltip("Vida del VFX de entrega, en segundos.")]
    public float vfxLifetime = 2f;

    [Tooltip("Clave de SFX en AudioGraphProfile al entregar el objeto (ej: 'Chest').")]
    public string deliverySfxKey = "Chest";

    [Tooltip("Estado de animación del gesto que hace el NPC al recibir el objeto (ej: 'HandClap01').")]
    public string deliveryGesture = "HandClap01";

    [Tooltip("Icono sobre la cabeza del NPC durante y tras la entrega. Vacío = el 'Default Quest Icon Prefab' de su NarrativeActor.")]
    public GameObject questIcon;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "recibe");
        d.Field("Objeto", expectedItemType.ToString());
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC que recibe el objeto");
    }

    /// El objeto llevado en brazos desaparece del mundo al entregarlo.
    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(npcId)) state.SetFact(HechosDelSendero.Entrega, npcId, expectedItemType.ToString());
    }
}

public sealed class EntregaDeObjetoEnCurso : NarrativeRequestRun<PedirEntregaDeObjeto>
{
    NpcItemDeliveryDetector _detector;

    protected override void Start()
    {
        var npc = !string.IsNullOrWhiteSpace(R.npcId) && NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(R.npcId) : null;
        if (npc == null)
        {
            Debug.LogWarning($"[Entrega:{StepGuid}] NPC '{R.npcId}' no está en NPCRegistry → sigue para no bloquear.");
            Finish(0);
            return;
        }
        _detector = npc.gameObject.AddComponent<NpcItemDeliveryDetector>();
        _detector.Setup(R.detectionRadius, R.expectedItemType, AlEntregar);
        // El icono sale ya, mientras el jugador lleva el objeto hacia el NPC; es el mismo que queda tras la entrega.
        npc.GetComponent<NarrativeActor>()?.ShowQuestIcon(R.questIcon);
    }

    protected override void Stop()
    {
        if (_detector != null) UnityEngine.Object.Destroy(_detector);
        _detector = null;
    }

    void AlEntregar(GameObject objeto)
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(R.npcId) : null;
        var actor = npc != null ? npc.GetComponent<NarrativeActor>() : null;

        // Se congela el «llevar en brazos» sin soltar físicamente el objeto todavía.
        if (PlayerService.TryGetComponent(out PlayerCarrySystem carga) && carga.IsCarrying && carga.CarriedObject == objeto)
            carga.CancelCarrySilently();

        // El NPC mira al jugador y hace el gesto de recibir el objeto.
        if (actor != null)
        {
            var jugador = PlayerService.PlayerTransform;
            if (jugador != null) actor.Face(jugador.position);
            if (!string.IsNullOrEmpty(R.deliveryGesture)) actor.PlayGesture(R.deliveryGesture);
        }

        // El objeto desaparece con VFX y SFX.
        if (objeto != null)
        {
            Vector3 donde = objeto.transform.position;
            if (R.deliveryVfxPrefab != null && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(R.deliveryVfxPrefab, donde, Quaternion.identity, R.vfxLifetime);
            if (!string.IsNullOrEmpty(R.deliverySfxKey) && AudioService.Instance != null)
                AudioService.Instance.PlaySFX(R.deliverySfxKey, 1f, donde);
            ObjetoPersistente.Retirar(objeto);
        }

        // Icono de «habla conmigo»: el paso siguiente (la charla) no tiene que volver a pintarlo.
        actor?.ShowQuestIcon(R.questIcon);
        Finish(0);
    }
}
