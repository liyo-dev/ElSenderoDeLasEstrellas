using UnityEngine;

/// <summary>
/// Marcador de escena que indica DÓNDE debe aparecer un NPC. No lleva el prefab ni la
/// configuración: solo un id de texto que el <see cref="NpcRosterSO"/> usa para emparejarlo
/// con el personaje que le toca.
///
/// Es el equivalente para NPCs de <see cref="SpawnAnchor"/> (el marcador del jugador), y sigue
/// deliberadamente su MISMO patrón: se registra solo en OnEnable y se da de baja en OnDisable,
/// para que herede su ciclo de vida y sus garantías de orden de arranque.
///
/// Ver claude/propuesta-sistema-spawn-npcs-por-datos-2026-09-16.md § 2.1
/// </summary>
[DisallowMultipleComponent]
public class NpcSpawnPoint : MonoBehaviour
{
    [Tooltip("Id único de este punto de aparición (ej: 'NPC_Oliver_PuebloInicio'). Es la clave " +
             "que busca el NpcRosterSO para saber qué personaje instanciar aquí.")]
    public string spawnId;

    [Tooltip("Si está activado, el NPC aparecerá mirando en la dirección Z de este marcador " +
             "(la flecha azul del gizmo). Si no, conserva la rotación del prefab.")]
    public bool applyRotation = true;

    private void OnEnable()  => NpcSpawnRegistry.Register(this);
    private void OnDisable() => NpcSpawnRegistry.Unregister(this);

    /// <summary>Rotación con la que debe aparecer el NPC colocado en este marcador.</summary>
    public Quaternion GetSpawnRotation()
        => Quaternion.LookRotation(transform.forward, Vector3.up);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);

        if (applyRotation)
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.9f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.2f);
        }

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.1f,
            string.IsNullOrEmpty(spawnId) ? "<sin spawnId>" : spawnId);
    }
#endif
}
