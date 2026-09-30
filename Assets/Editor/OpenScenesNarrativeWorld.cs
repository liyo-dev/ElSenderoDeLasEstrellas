using System;
using Game.NPC;
using UnityEditor;
using UnityEngine;

/// Resuelve lugares y actores del estado proyectado contra las escenas abiertas en el Editor
/// (sin Play Mode). Lo usa Quick Test para convertir «Eldran está en la marca X» en la posición
/// que se guarda en el preset.
///  - Lugar World: SpawnAnchor con ese anchorId.
///  - Lugar Scoped: marca del SequenceStage del SequencePlayer que corresponde al contexto (el que
///    reproduce una secuencia con ese nombre, o el GameObject con ese nombre); si no hay, el del
///    contexto de reserva.
/// Los actores se buscan en las escenas abiertas y, si no están (los que instancia NpcSpawner al
/// arrancar no existen en el Editor), en los NpcRosterSO del proyecto. Una marca solo se resuelve
/// si la escena que la contiene está abierta.
public sealed class OpenScenesNarrativeWorld : INarrativeLocationResolver
{
    public bool TryResolve(NarrativeLocation location, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (!location.IsValid) return false;

        if (location.Kind == NarrativeLocation.LocationKind.World)
        {
            foreach (var anchor in UnityEngine.Object.FindObjectsByType<SpawnAnchor>())
            {
                if (anchor == null || !string.Equals(anchor.anchorId, location.Mark, StringComparison.Ordinal)) continue;
                position = anchor.transform.position;
                rotation = anchor.GetCharacterRotation();
                return true;
            }
            return false;
        }

        var players = UnityEngine.Object.FindObjectsByType<SequencePlayer>();
        var player = FindPlayer(players, location.Scope) ?? FindPlayer(players, location.FallbackScope);
        var stage = player == null ? null : player.Stage != null ? player.Stage : player.GetComponent<SequenceStage>();
        var mark = stage != null ? stage.GetMark(location.Mark) : null;
        if (mark == null) return false;
        position = mark.position;
        rotation = mark.rotation;
        return true;
    }

    static SequencePlayer FindPlayer(SequencePlayer[] players, string scope)
    {
        if (string.IsNullOrEmpty(scope)) return null;
        foreach (var p in players)
            if (p != null && p.Definition != null && p.Definition.name == scope) return p;
        foreach (var p in players)
            if (p != null && p.name == scope) return p;
        return null;
    }

    /// Nombre del GameObject del NPC con ese persistenceId (es la clave de PlayerPresetSO.npcPositions).
    public bool TryGetActorObjectName(string actorId, out string objectName)
    {
        objectName = null;
        if (string.IsNullOrEmpty(actorId)) return false;

        foreach (var npc in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>())
        {
            if (npc == null || npc.PersistenceId != actorId) continue;
            objectName = npc.gameObject.name;
            return true;
        }

        // Mismo criterio que NpcSpawner: el persistenceId de la entrada, o si está vacío el del prefab.
        foreach (var guid in AssetDatabase.FindAssets("t:NpcRosterSO"))
        {
            var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (roster == null || roster.entries == null) continue;
            foreach (var entry in roster.entries)
            {
                if (entry == null || !entry.enabled || string.IsNullOrEmpty(entry.gameObjectName)) continue;
                string id = !string.IsNullOrEmpty(entry.persistenceId)
                    ? entry.persistenceId
                    : entry.prefab != null && entry.prefab.TryGetComponent<NPCBehaviourManagerV2>(out var brain) ? brain.PersistenceId : null;
                if (id != actorId) continue;
                objectName = entry.gameObjectName;
                return true;
            }
        }
        return false;
    }
}
