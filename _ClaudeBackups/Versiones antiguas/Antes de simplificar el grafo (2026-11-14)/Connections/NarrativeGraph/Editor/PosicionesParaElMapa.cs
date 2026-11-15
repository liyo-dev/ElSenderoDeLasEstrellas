using NarrativeGraphEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Dónde está cada NPC cuando la historia no dice nada: el punto de su reparto (NpcRosterSO:
    /// persistenceId → spawnId → NpcSpawnPoint de las escenas abiertas). Las marcas se buscan por
    /// nombre (el objeto se llama como su anchorId).
    public sealed class PosicionesParaElMapa : INarrativeWorldPositions
    {
        public bool TryMark(string markId, out Vector3 position) { position = default; return false; }

        public bool TryActorHome(string actorId, out Vector3 position)
        {
            position = default;
            foreach (var reparto in Resources.LoadAll<NpcRosterSO>("NpcRosters"))
                foreach (var e in reparto.entries)
                {
                    if (e == null || e.persistenceId != actorId || string.IsNullOrEmpty(e.spawnId)) continue;
                    foreach (var punto in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include))
                        if (punto.spawnId == e.spawnId) { position = punto.transform.position; return true; }
                }
            return false;
        }
    }
}
