using UnityEngine;

/// <summary>
/// Frase de un actor cuando el jugador le habla y el grafo no está esperando esa conversación
/// (p. ej. quien encargó una misión, mientras sigue en curso). Recorre los nodos de los grafos
/// cargados que implementan <see cref="INarrativeStandingLine"/> y se queda con el primero que
/// valga ahora. Solo se consulta al pulsar hablar, nunca por fotograma.
/// </summary>
public static class NarrativeStandingLines
{
    /// <summary>Busca la frase que tiene ahora el actor. False si no tiene ninguna.</summary>
    public static bool TryFind(string actorId, out DialogueAsset dialogue)
    {
        dialogue = null;
        if (string.IsNullOrEmpty(actorId)) return false;

        var hub = NarrativeGraphHub.Instance;
        var signals = DefaultNarrativeSignals.Instance;
        if (hub == null || signals == null) return false;

        var runners = hub.GetAllRunners();
        if (runners == null) return false;

        foreach (var runner in runners)
        {
            var nodes = runner != null && runner.graph != null ? runner.graph.nodes : null;
            if (nodes == null) continue;

            foreach (var node in nodes)
            {
                if (node is not INarrativeStandingLine line) continue;
                if (line.StandingActorId != actorId || line.StandingDialogue == null) continue;
                if (!line.IsStandingLineActive(signals)) continue;

                dialogue = line.StandingDialogue;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Si el actor tiene frase ahora y no hay otro diálogo abierto, la dice (con la cámara de
    /// diálogo sobre <paramref name="speaker"/>). True si la ha dicho.
    /// </summary>
    public static bool TryPlay(string actorId, Transform speaker)
    {
        var dialogueManager = DialogueManager.Instance;
        if (dialogueManager == null || dialogueManager.IsOpen) return false;
        if (!TryFind(actorId, out var dialogue)) return false;

        dialogueManager.StartDialogue(dialogue, speaker);
        return true;
    }
}
