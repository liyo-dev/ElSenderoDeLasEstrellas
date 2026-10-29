using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using UnityEngine.AI;

/// Posición, giro y estado activo de los NPCs que se recuerdan entre partidas
/// (<see cref="NPCBehaviourManagerV2.persistLastPosition"/>).
public static partial class Partida
{
    /// Margen tras restaurar un NPC en el que FollowPlayerState no lo pone a andar, para que
    /// se vea la pose restaurada antes de que siga al jugador (el jugador solo vuelve a un ancla).
    private const float GraciaDeSeguirTrasCargar = 1.5f;

    /// <summary>
    /// Apunta en el preset dónde está cada NPC que se recuerda. Las entradas de NPCs que no están
    /// en esta escena se conservan (pueden estar en otra); se quitan las de los que ya no se recuerdan.
    /// </summary>
    private static void CapturarPosicionesDeNpcs(PlayerPresetSO p)
    {
        p.npcPositions ??= new List<PlayerPresetSO.NpcPosEntry>();
        var indice = new Dictionary<string, int>();
        for (int i = 0; i < p.npcPositions.Count; i++)
            if (!string.IsNullOrEmpty(p.npcPositions[i].npcId)) indice[p.npcPositions[i].npcId] = i;

        var npcs = ServiceLocator.GetAll<NPCBehaviourManagerV2>();
        var porNombre = new Dictionary<string, NPCBehaviourManagerV2>(npcs.Count);
        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            string id = npc.gameObject.name;
            if (!string.IsNullOrEmpty(id)) porNombre[id] = npc;
            if (!npc.persistLastPosition) continue;

            var pos = npc.lastPosition != default ? npc.lastPosition : npc.transform.position;
            if (pos == default) continue;

            if (indice.TryGetValue(id, out int i))
            {
                var e = p.npcPositions[i];
                if (Vector3.Distance(e.position, pos) > 0.01f) e.position = pos;
                e.rotation = npc.transform.rotation;
                e.hasRotation = true;
                p.npcPositions[i] = e;
            }
            else
            {
                p.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                {
                    npcId = id, position = pos, rotation = npc.transform.rotation,
                    hasRotation = true, hasActiveState = false, isActive = true
                });
                indice[id] = p.npcPositions.Count - 1;
            }
        }

        for (int i = p.npcPositions.Count - 1; i >= 0; i--)
        {
            var id = p.npcPositions[i].npcId;
            if (string.IsNullOrEmpty(id) || (porNombre.TryGetValue(id, out var npc) && npc != null && !npc.persistLastPosition))
                p.npcPositions.RemoveAt(i);
        }
    }

    /// <summary>Coloca los NPCs de la escena donde dice el preset.</summary>
    public static void AplicarPosicionesDeNpcs(PlayerPresetSO p)
    {
        if (p == null || p.npcPositions == null || p.npcPositions.Count == 0) return;
        var npcs = ServiceLocator.GetAll<NPCBehaviourManagerV2>();
        if (npcs == null || npcs.Count == 0) return;

        var mapa = new Dictionary<string, PlayerPresetSO.NpcPosEntry>(p.npcPositions.Count);
        foreach (var e in p.npcPositions)
            if (!string.IsNullOrEmpty(e.npcId) && !mapa.ContainsKey(e.npcId)) mapa[e.npcId] = e;

        foreach (var npc in npcs)
        {
            if (npc == null || !mapa.TryGetValue(npc.gameObject.name, out var e) || e.position == default) continue;

            // Una posición guardada en mal momento (un NPC levitado, en el aire) se baja al suelo.
            Vector3 destino;
            if (NavMesh.SamplePosition(e.position, out var navHit, 3f, NavMesh.AllAreas)) destino = navHit.position;
            else if (Physics.Raycast(e.position + Vector3.up * 5f, Vector3.down, out var suelo, 20f)) destino = suelo.point;
            else destino = npc.transform.position;

            // Warp siempre: tras un arranque en frío el agente aún puede decir isOnNavMesh=false, y
            // mover solo el transform lo dejaría fuera del NavMesh para siempre.
            var agente = npc.GetComponent<NavMeshAgent>();
            if (agente != null)
            {
                agente.Warp(destino);
                if (!agente.isOnNavMesh) npc.transform.position = destino;
            }
            else
            {
                npc.transform.position = destino;
            }

            // Un guardado antiguo sin hasRotation trae la rotación vacía (0,0,0,0) si nunca se capturó.
            if (e.hasRotation || e.rotation != default(Quaternion))
            {
                npc.transform.rotation = e.rotation;
                if (agente != null && agente.isOnNavMesh) agente.nextPosition = npc.transform.position;
                npc.GetComponent<NPCSimpleAnimator>()?.SyncTargetRotation();
                npc.Context.FollowGraceUntil = Time.time + GraciaDeSeguirTrasCargar;
            }

            if (e.hasActiveState) npc.gameObject.SetActive(e.isActive);
            npc.lastPosition = e.position;
        }
    }
}
