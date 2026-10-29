using System;
using System.Collections.Generic;

/// <summary>
/// La partida de El Sendero vista por el grafo narrativo. Los flags narrativos viven en
/// <c>Partida.Preset.flags</c> (la misma lista que QUEST_* y CINEMATIC_SEEN:*), con la clave de
/// <see cref="NarrativeFlags.Key"/>, así que se guardan y cargan con el resto de la partida. Los
/// blackboards guardados vienen de <c>Partida.Preset.narrativeBlackboards</c>.
/// </summary>
public sealed class PartidaNarrativeData : INarrativeFlagStore, INarrativeSaveData
{
    /// <summary>Se suscribe a la carga de partidas. Lo llama <see cref="NarrativeGameServices"/>.</summary>
    public void Install()
    {
        GameBootService.OnProfileReady -= AlCargarPartida;
        GameBootService.OnProfileReady += AlCargarPartida;
    }

    void AlCargarPartida() => LoadCount++;

    // ── INarrativeFlagStore ──────────────────────────────────────────────────────────────────

    static List<string> Flags
    {
        get
        {
            if (GameBootService.Profile == null) return null;
            var preset = Partida.Preset;
            if (preset == null) return null;
            if (preset.flags == null) preset.flags = new List<string>();
            return preset.flags;
        }
    }

    public bool IsAvailable => Flags != null;

    public bool Has(string flag)
    {
        var flags = Flags;
        return flags != null && flags.Contains(NarrativeFlags.Key(flag));
    }

    public void Set(string flag, bool value)
    {
        var flags = Flags;
        if (flags == null) return;
        var key = NarrativeFlags.Key(flag);
        if (value)
        {
            if (!flags.Contains(key)) flags.Add(key);
        }
        else
        {
            flags.Remove(key);
        }
    }

    public IEnumerable<string> All()
    {
        var flags = Flags;
        if (flags == null) yield break;
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (!string.IsNullOrEmpty(f) && f.StartsWith(NarrativeFlags.Prefix, StringComparison.Ordinal))
                yield return f.Substring(NarrativeFlags.Prefix.Length);
        }
    }

    // ── INarrativeSaveData ───────────────────────────────────────────────────────────────────

    public bool IsLoaded => GameBootService.IsAvailable;

    public int LoadCount { get; private set; }

    public IReadOnlyList<NarrativeBlackboardRecord> GetSavedBlackboards()
    {
        if (GameBootService.Profile == null) return null;
        var preset = Partida.Preset;
        if (preset == null) return null;

        var records = new List<NarrativeBlackboardRecord>();
        if (preset.narrativeBlackboards != null)
            foreach (var snapshot in preset.narrativeBlackboards)
                records.Add(new NarrativeBlackboardRecord(snapshot.graphLabel, snapshot.blackboardData));
        return records;
    }
}

/// <summary>
/// Guardar y cargar los blackboards del grafo en el formato de partida de El Sendero
/// (<see cref="PlayerSaveData.NarrativeBlackboardSnapshot"/>).
/// </summary>
public static class NarrativeGraphHubPartidaExtensions
{
    public static List<PlayerSaveData.NarrativeBlackboardSnapshot> CaptureBlackboards(this NarrativeGraphHub hub)
    {
        var snapshots = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        if (hub == null) return snapshots;
        foreach (var record in hub.CaptureBlackboardRecords())
        {
            snapshots.Add(new PlayerSaveData.NarrativeBlackboardSnapshot
            {
                graphLabel = record.GraphLabel,
                blackboardData = record.Entries
            });
        }
        return snapshots;
    }

    public static void RestoreBlackboards(this NarrativeGraphHub hub, List<PlayerSaveData.NarrativeBlackboardSnapshot> snapshots)
    {
        if (hub == null) return;
        if (snapshots == null)
        {
            hub.RestoreBlackboardRecords(null);
            return;
        }
        var records = new List<NarrativeBlackboardRecord>(snapshots.Count);
        foreach (var snapshot in snapshots)
            records.Add(new NarrativeBlackboardRecord(snapshot.graphLabel, snapshot.blackboardData));
        hub.RestoreBlackboardRecords(records);
    }
}
