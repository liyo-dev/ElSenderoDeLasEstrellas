using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de arranque: «aquí el jugador está así». En juego no hace nada (pasa de largo); al probar
/// desde cualquier paso, el editor toma los puntos anteriores, en orden, y aplica lo que fija cada
/// uno sobre la ficha del jugador del juego (dónde aparece, vida, lo que sea que tenga el juego).
///
/// El id es estable aunque el grafo cambie, a diferencia del guid del nodo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Punto de arranque", "Cómo está el jugador al llegar aquí; al probar desde un paso se usa el punto anterior más cercano.")]
[SavePoint("Instantáneo")]
[NarrativeNoWorldEffect]
public sealed class CheckpointNode : NarrativeNode
{
    [Tooltip("Identificador estable y legible: 'ANTES_DEL_PUENTE'.")]
    public string checkpointId;

    [Tooltip("Lo que fija este punto en la ficha del jugador. Se edita en la pestaña Arranque del editor.")]
    public NarrativePlayerValues player = new();

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line("◆ " + (string.IsNullOrEmpty(checkpointId) ? "(sin id)" : checkpointId), NarrativeTextTone.Strong);
        if (player != null && !player.IsEmpty) d.Field("jugador", string.Join(", ", player.fields));
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrEmpty(checkpointId)) warnings.Add("checkpointId vacío");
    }

    public override void Enter(NarrativeContext ctx, Action ready) => ready?.Invoke();
}
