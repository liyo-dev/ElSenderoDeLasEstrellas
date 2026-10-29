using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de arranque para pruebas: «quiero empezar a jugar AQUÍ». No hace nada en juego (pasa
/// de largo); su valor está en las herramientas de prueba, que listan los checkpoints de todos los
/// grafos y permiten arrancar en uno.
///
/// El id es estable aunque el grafo cambie, a diferencia del guid del nodo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Checkpoint (prueba)", "Punto desde el que se puede arrancar el juego en el menú de test.")]
[SavePoint("Instantáneo")]
public sealed class CheckpointNode : NarrativeNode
{
    [Tooltip("Identificador estable y legible: 'ANTES_DEL_PUENTE'.")]
    public string checkpointId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marca del mundo donde aparece el jugador al arrancar aquí.")]
    public string spawnAnchorId;

    [Tooltip("Escena de mundo que debe estar cargada (vacío = la escena por defecto del juego).")]
    public string sceneName;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line("⚑ " + (string.IsNullOrEmpty(checkpointId) ? "(sin id)" : checkpointId), NarrativeTextTone.Strong);
        if (!string.IsNullOrEmpty(spawnAnchorId)) d.Anchor(spawnAnchorId);
        if (!string.IsNullOrEmpty(sceneName)) d.Field("escena", sceneName);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrEmpty(checkpointId)) warnings.Add("checkpointId vacío");
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[CheckpointNode:{guid}] ⚑ {checkpointId}");
#endif
        ready?.Invoke();
    }
}
