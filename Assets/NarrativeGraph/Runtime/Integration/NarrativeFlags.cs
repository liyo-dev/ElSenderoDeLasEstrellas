using System;
using System.Collections.Generic;

/// <summary>
/// Flags narrativos persistentes: hechos del mundo ("Will ya despertó", "puerta del castillo
/// abierta") que sobreviven al guardado. Viven en <c>preset.flags</c> (la misma lista que ya
/// usan QUEST_* y CINEMATIC_SEEN:*), con el prefijo <c>NARRATIVE_FLAG:</c>, así que se
/// guardan y cargan sin tocar GameBootProfile.
///
/// Es la única fuente de verdad para SetFlagNode / BranchFlagNode. No confundir con las
/// señales (DefaultNarrativeSignals): una señal es un evento que ocurre; un flag es un estado
/// que se consulta.
/// </summary>
public static class NarrativeFlags
{
    public const string Prefix = "NARRATIVE_FLAG:";

    static List<string> Flags
    {
        get
        {
            var profile = GameBootService.Profile;
            if (profile == null) return null;
            var preset = profile.GetActivePresetResolved();
            if (preset == null) return null;
            if (preset.flags == null) preset.flags = new List<string>();
            return preset.flags;
        }
    }

    public static string Key(string flag) => Prefix + flag;

    public static bool Has(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag)) return false;
        var flags = Flags;
        return flags != null && flags.Contains(Key(flag));
    }

    public static void Set(string flag, bool value)
    {
        if (string.IsNullOrWhiteSpace(flag)) return;
        var flags = Flags;
        if (flags == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[NarrativeFlags] No hay perfil activo (GameBootService.Profile == null); el flag '{flag}' no se guarda. ¿Play sin la escena Start?");
#endif
            return;
        }
        var key = Key(flag);
        if (value)
        {
            if (!flags.Contains(key)) flags.Add(key);
        }
        else
        {
            flags.Remove(key);
        }
    }

    /// <summary>Todos los flags narrativos activos, sin prefijo (para debug/editor).</summary>
    public static IEnumerable<string> All()
    {
        var flags = Flags;
        if (flags == null) yield break;
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (!string.IsNullOrEmpty(f) && f.StartsWith(Prefix, StringComparison.Ordinal))
                yield return f.Substring(Prefix.Length);
        }
    }
}
