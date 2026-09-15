using System;
using UnityEngine;

/// <summary>
/// Punto de arranque para pruebas: "quiero empezar a jugar AQUÍ". No hace nada en juego (pasa
/// de largo); su valor está en el menú de test, que lista todos los checkpoints de todos los
/// grafos y permite arrancar en uno con un Loadout (equipo/inventario/hechizos) elegido.
///
/// El id es estable aunque el grafo cambie, a diferencia de los GUIDs de nodo que usaban los
/// presets de testing antiguos.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Checkpoint (prueba)", "Punto desde el que se puede arrancar el juego en el menú de test.")]
[SavePoint("Instantáneo")]
public sealed class CheckpointNode : NarrativeNode
{
    [Tooltip("Identificador estable y legible: 'CAP1_ANTES_DE_LA_CAJA'.")]
    public string checkpointId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("SpawnAnchor donde aparece el jugador al arrancar aquí.")]
    public string spawnAnchorId;

    [Tooltip("Escena de mundo que debe estar cargada (vacío = la escena por defecto del perfil).")]
    public string sceneName;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CheckpointNode:{guid}] ⚑ {checkpointId}");
#endif
        ready?.Invoke();
    }
}
