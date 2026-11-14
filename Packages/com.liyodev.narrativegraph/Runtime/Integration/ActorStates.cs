using System.Collections.Generic;
using UnityEngine;

/// Aplica en el mundo lo que dicen las piezas «Personaje desde aquí» (<see cref="ActorStateNode"/>)
/// que valen ahora (<see cref="NarrativeActorStates"/>): retira, devuelve o coloca a cada personaje,
/// siempre sin que el jugador lo vea (<see cref="INarrativeActorPresence"/>). Revisa una vez por
/// segundo; si ahora se notaría, lo intenta en la siguiente. Una pieza «no está» se mantiene: si la
/// escena vuelve a crear al personaje, se retira otra vez.
///
/// También da la frase de la pieza cuando el jugador le habla (<see cref="NarrativeStandingLines"/>
/// la consulta después de las frases del grafo).
public static class ActorStates
{
    static readonly Dictionary<string, ActorStateNode> s_applied = new();
    static readonly Dictionary<string, int> s_nextLine = new();
    static readonly Dictionary<string, ActorStateNode> s_byGuid = new();
    static readonly List<string> s_actors = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_applied.Clear();
        s_nextLine.Clear();
        s_byGuid.Clear();
        s_actors.Clear();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        var go = new GameObject("[Personajes según la historia]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<ActorStatesClock>();
    }

    /// La pieza que vale ahora para el personaje, o null.
    public static ActorStateNode Current(string actorId)
    {
        var guid = NarrativeActorStates.Active(actorId);
        return string.IsNullOrEmpty(guid) ? null : Find(guid);
    }

    /// La frase que le toca decir ahora al personaje. No la gasta.
    public static bool TryLine(string actorId, out NarrativeDialogueAsset line)
    {
        line = null;
        var node = Current(actorId);
        if (node == null || node.lines == null || node.lines.Count == 0) return false;
        line = node.lines[Index(node)];
        return line != null;
    }

    /// Apunta que el personaje ha dicho <paramref name="line"/>: la próxima vez dirá la siguiente.
    public static void LineSaid(string actorId, NarrativeDialogueAsset line)
    {
        var node = Current(actorId);
        if (node == null || line == null || node.lines == null || node.lines.Count == 0) return;
        int i = Index(node);
        if (node.lines[i] == line) s_nextLine[node.guid] = (i + 1) % node.lines.Count;
    }

    static int Index(ActorStateNode node) => s_nextLine.TryGetValue(node.guid, out var i) ? i % node.lines.Count : 0;

    /// La pieza de los grafos cargados con ese guid, o null.
    static ActorStateNode Find(string guid)
    {
        if (s_byGuid.TryGetValue(guid, out var node) && node != null) return node;
        s_byGuid.Clear();
        var hub = NarrativeGraphHub.Instance;
        var runners = hub != null ? hub.GetAllRunners() : null;
        if (runners == null) return null;
        foreach (var runner in runners)
        {
            var nodes = runner != null && runner.graph != null ? runner.graph.nodes : null;
            if (nodes == null) continue;
            foreach (var n in nodes)
                if (n is ActorStateNode a && !string.IsNullOrEmpty(a.guid)) s_byGuid[a.guid] = a;
        }
        return s_byGuid.TryGetValue(guid, out node) ? node : null;
    }

    /// Aplica a los personajes cuya pieza ha cambiado. La llama <see cref="ActorStatesClock"/>.
    internal static void Review()
    {
        if (!NarrativeServices.TryGet(out INarrativeActorPresence presence, nameof(ActorStates))) return;

        s_actors.Clear();
        s_actors.AddRange(NarrativeActorStates.Actors());
        foreach (var id in s_applied.Keys) if (!s_actors.Contains(id)) s_actors.Add(id);

        foreach (var id in s_actors)
        {
            var node = Current(id);
            s_applied.TryGetValue(id, out var before);
            bool changed = node != before;
            bool wasAway = before != null && before.presence == ActorPresence.Away;

            if (node == null)
            {
                if (!changed) continue;
                if (wasAway && !presence.TrySetPresent(id, true)) continue;
                s_applied.Remove(id);
                continue;
            }

            switch (node.presence)
            {
                case ActorPresence.Away:
                    if (presence.IsPresent(id) && !presence.TrySetPresent(id, false)) continue;
                    break;
                case ActorPresence.AtMark:
                    if (changed && !presence.TryPlaceUnseen(id, node.markId)) continue;
                    break;
                default:
                    if (changed && wasAway && !presence.TrySetPresent(id, true)) continue;
                    break;
            }
            s_applied[id] = node;
        }
    }
}

/// Pide a <see cref="ActorStates"/> que revise a los personajes una vez por segundo. Lo crea
/// <see cref="ActorStates"/> al arrancar el juego; no se pone a mano en ninguna escena.
public sealed class ActorStatesClock : MonoBehaviour
{
    const float Interval = 1f;
    float _left;

    void Update()
    {
        _left -= Time.unscaledDeltaTime;
        if (_left > 0f) return;
        _left = Interval;
        ActorStates.Review();
    }
}
