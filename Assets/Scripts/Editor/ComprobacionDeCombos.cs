using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Comprueba las secuencias del combo mágico de la Y: que ningún hechizo repita la secuencia de
/// otro ni empiece igual que una más larga (la larga no saldría nunca). Solo informa. Ver INC-494.
/// </summary>
public static class ComprobacionDeCombos
{
    [MenuItem("El Sendero/Combate/Comprobar secuencias de combo")]
    public static void Comprobar()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();
        Validate(log, warnings);
        Report("Secuencias de combo", log, warnings);
    }

    private static void Validate(StringBuilder log, List<string> warnings)
    {
        var spells = AssetDatabase.FindAssets("t:MagicSpellSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null && s.HasCombo).ToList();
        int problems = 0;
        for (int i = 0; i < spells.Count; i++)
            for (int j = 0; j < spells.Count; j++)
            {
                if (i == j) continue;
                var a = spells[i].comboSequence; var b = spells[j].comboSequence;
                if (a.Length > b.Length) continue;
                bool prefix = true;
                for (int k = 0; k < a.Length; k++) if (a[k] != b[k]) { prefix = false; break; }
                if (!prefix) continue;
                if (a.Length == b.Length && i > j) continue; // iguales: se avisa una vez
                warnings.Add(a.Length == b.Length
                    ? $"{spells[i].name} y {spells[j].name} tienen la misma secuencia ({string.Join(" ", a)})."
                    : $"La secuencia de {spells[i].name} ({string.Join(" ", a)}) es el principio de la de {spells[j].name} ({string.Join(" ", b)}): {spells[j].name} no saldría nunca.");
                problems++;
            }
        log.AppendLine($"Secuencias comprobadas: {spells.Count} combos, {problems} problema(s).");
    }

    private static void Report(string title, StringBuilder log, List<string> warnings)
    {
        var final = new StringBuilder($"=== {title} ===\n");
        final.Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }
}
