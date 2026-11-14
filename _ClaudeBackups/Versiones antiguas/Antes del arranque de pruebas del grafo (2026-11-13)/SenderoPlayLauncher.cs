using System.Collections.Generic;
using NarrativeGraphEditor;

namespace Sendero.Narrative.Editor
{
    /// El arranque de pruebas de El Sendero: escribe una partida de prueba en el GameBootProfile
    /// y arranca por la escena Start (ver NarrativeTestLauncher). Sustituye al arranque básico.
    public sealed class SenderoPlayLauncher : INarrativePlayLauncher
    {
        public string Name => "El Sendero (GameBootProfile + escena Start)";
        public int Priority => 100;

        public bool Launch(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices, out string message)
        {
            var cfg = NarrativeTestConfig.Load();
            if (choices != null) foreach (var pair in choices) cfg.SetRouteChoice(pair.Key, pair.Value);
            return NarrativeTestLauncher.Launch(graph, stepGuid, cfg, out message, out _);
        }
    }
}
