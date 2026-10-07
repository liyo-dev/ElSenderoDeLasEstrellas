using System;
using UnityEngine;

/// Coloca a un actor en una marca del mundo al instante y sigue.
///
/// Sirve para dejar escrito en el grafo dónde está alguien cuando lo que le movió no lo dice
/// (un salto de tiempo, un fundido, un cambio de capítulo), y para que al arrancar desde un nodo
/// posterior el actor aparezca donde le toca. Se usa fuera de cámara o detrás de un corte.
[Serializable]
[NarrativeNodeInfo("NPCs", "Colocar actor", "Pone a un actor en una marca (SpawnAnchor) al instante, mirando hacia donde mira la marca. Para usar fuera de cámara o tras un corte. También deja constancia de dónde está al arrancar desde un nodo posterior.")]
public sealed class PlaceActorNode : NarrativeNode, INarrativeStateEffect
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del actor, o 'Player' para el jugador.")]
    public string actorId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("anchorId del SpawnAnchor donde se coloca.")]
    public string markId;

    [Tooltip("Para Player, usa TeleportService sin fundido y aplica el entorno del anchor destino.")]
    public bool applyPlayerEnvironment;

    [Tooltip("Muestra el actor colocado, también si viene de una zona exterior oculta.")]
    public bool showActor;

    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(actorId) && !string.IsNullOrWhiteSpace(markId))
            state.PlaceActor(actorId, NarrativeLocation.World(markId));
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var mark = string.IsNullOrWhiteSpace(markId) ? null : SpawnAnchor.FindById(markId);
        if (mark != null && SequenceActor.TryResolve(actorId, out var actor))
        {
            if (applyPlayerEnvironment && actorId == SequenceActor.PlayerId)
                TeleportService.TeleportToAnchor(actor.Transform.gameObject, markId, false);
            else
                SequenceMovement.PlaceAt(actor, mark.transform.position, mark.GetCharacterRotation());
            if (showActor) actor.SetRenderingVisible(true);
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
            Debug.LogWarning($"[PlaceActorNode:{guid}] No se coloca a '{actorId}': marca '{markId}'={(mark != null ? "ok" : "NO")}. Se sigue.");
#endif
        onReadyToAdvance?.Invoke();
    }
}
