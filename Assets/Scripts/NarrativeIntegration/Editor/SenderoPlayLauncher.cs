using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarrativeGraphEditor;

namespace Sendero.Narrative.Editor
{
    /// El arranque de pruebas de El Sendero: escribe una partida de prueba en el GameBootProfile y
    /// da Play directamente en la escena del ancla, sin intro ni menú (ver NarrativeTestLauncher).
    /// Sustituye al arranque básico y añade al resumen de Arranque la escena y cómo se entra.
    public sealed class SenderoPlayLauncher : INarrativePlayLauncher, INarrativeLaunchDetails
    {
        public string Name => "El Sendero (GameBootProfile, directo a la escena)";
        public int Priority => 100;

        public bool Launch(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices, out string message)
        {
            var cfg = NarrativeTestConfig.Load();
            if (choices != null) foreach (var pair in choices) cfg.SetRouteChoice(pair.Key, pair.Value);
            return NarrativeTestLauncher.Launch(graph, stepGuid, cfg, out message, out _);
        }

        public void Describe(NarrativeStartPlan plan, UnityEngine.Object player, List<StartLine> lines)
        {
            var ancla = (player as PlayerPresetSO)?.spawnAnchorId;
            var escena = NarrativeTestLauncher.EscenaDelAncla(ancla);
            lines.Add(new StartLine { Label = "Escena", Value = Path.GetFileNameWithoutExtension(escena), Source = "por el ancla" });
            lines.Add(new StartLine { Label = "Entrada", Value = "Directo a la escena, sin intro ni menú" });
            if (string.IsNullOrEmpty(NarrativeTestLauncher.DetectGraphLabel(plan.Graph)))
                lines.Add(new StartLine { Label = "Hub", Value = "Este grafo no está en el NarrativeGraphHub de Start.unity: no arrancará.", Warning = true });

            var preview = NarrativeTestLauncher.Preview(plan.Graph, plan.StepGuid, NarrativeTestConfig.Load());
            foreach (var aviso in preview.warnings.Take(4))
                lines.Add(new StartLine { Label = "Aviso", Value = aviso, Warning = true });
        }
    }
}
