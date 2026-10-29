using System;
using UnityEngine;

/// Coloca a un actor en una marca del mundo al instante y sigue (INarrativeActorPlacement).
///
/// Sirve para dejar escrito en el grafo dónde está alguien cuando lo que le movió no lo dice
/// (un salto de tiempo, un fundido, un cambio de capítulo), y para que al arrancar desde un nodo
/// posterior el actor aparezca donde le toca. Se usa fuera de cámara o detrás de un corte.
[Serializable]
[NarrativeNodeInfo("NPCs", "Colocar actor", "Pone a un actor en una marca del mundo al instante, mirando hacia donde mira la marca. Para usar fuera de cámara o tras un corte. También deja constancia de dónde está al arrancar desde un nodo posterior.")]
public sealed class PlaceActorNode : NarrativeNode, INarrativeStateEffect
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Id persistente del actor, o 'Player' para el jugador.")]
    public string actorId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marca del mundo donde se coloca.")]
    public string markId;

    [Tooltip("Solo para el jugador: aplicar también lo que la marca dicte del lugar (entorno, interior…), sin fundido.")]
    public bool applyPlayerEnvironment;

    [Tooltip("Muestra el actor colocado, también si estaba oculto.")]
    public bool showActor;

    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(actorId) && !string.IsNullOrWhiteSpace(markId))
            state.PlaceActor(actorId, NarrativeLocation.World(markId));
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool colocado = !string.IsNullOrWhiteSpace(markId)
            && NarrativeServices.TryGet(out INarrativeActorPlacement placement, nameof(PlaceActorNode))
            && placement.TryPlace(actorId, markId, applyPlayerEnvironment, showActor);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (!colocado)
            Debug.LogWarning($"[PlaceActorNode:{guid}] No se coloca a '{actorId}' en la marca '{markId}'. Se sigue.");
#endif
        onReadyToAdvance?.Invoke();
    }
}
