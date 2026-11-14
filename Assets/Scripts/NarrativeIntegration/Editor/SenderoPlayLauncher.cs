using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarrativeGraphEditor;

namespace Sendero.Narrative.Editor
{
    /// El arranque de pruebas de El Sendero: escribe una partida de prueba en el GameBootProfile y
    /// da Play directamente en la escena del ancla, sin intro ni menú (ver NarrativeTestLauncher).
    /// Sustituye al arranque básico y añade al resumen de Arranque la escena y cómo se entra.
    public sealed class SenderoPlayLauncher : INarrativePlayLauncher, INarrativeLaunchDetails, INarrativeStoryOnPlayer
    {
        public string Name => "El Sendero (GameBootProfile, directo a la escena)";
        public int Priority => 100;

        public bool Launch(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices, out string message)
        {
            var cfg = NarrativeTestConfig.Load();
            if (choices != null) foreach (var pair in choices) cfg.SetRouteChoice(pair.Key, pair.Value);
            return NarrativeTestLauncher.Launch(graph, stepGuid, cfg, out message, out _);
        }

        public void ApplyStory(NarrativeStartPlan plan, UnityEngine.Object player)
        {
            if (player is PlayerPresetSO p) NarrativeTestLauncher.ProyectarHistoria(plan.Graph, plan.StepGuid, NarrativeTestConfig.Load(), p);
        }

        public void Describe(NarrativeStartPlan plan, UnityEngine.Object player, List<StartLine> lines)
        {
            var ancla = (player as PlayerPresetSO)?.spawnAnchorId;
            var escena = NarrativeTestLauncher.EscenaDelAncla(ancla);
            lines.Add(new StartLine { Label = "Escena", Value = Path.GetFileNameWithoutExtension(escena), Source = "por el ancla" });
            lines.Add(new StartLine { Label = "Entrada", Value = "Directo a la escena, sin intro ni menú" });
            if (string.IsNullOrEmpty(NarrativeTestLauncher.DetectGraphLabel(plan.Graph)))
                lines.Add(new StartLine { Label = "Hub", Value = "Este grafo no está en el NarrativeGraphHub de Start.unity: no arrancará.", Warning = true });

            AvisarDeReglasDelJuego(player as PlayerPresetSO, lines);
        }

        static Dictionary<SpellId, MagicSpellSO> _hechizos;

        static MagicSpellSO HechizoPorId(SpellId id)
        {
            _hechizos ??= UnityEditor.AssetDatabase.FindAssets("t:MagicSpellSO")
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<MagicSpellSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(h => h != null).GroupBy(h => h.spellId).ToDictionary(g => g.Key, g => g.First());
            return _hechizos.TryGetValue(id, out var h) ? h : null;
        }

        /// Reglas del juego que cambian los datos del jugador al cargar la partida, para que el
        /// resumen avise en vez de que pase en silencio.
        static void AvisarDeReglasDelJuego(PlayerPresetSO p, List<StartLine> lines)
        {
            if (p == null) return;
            var aprendidos = p.unlockedSpells ?? new List<SpellId>();
            IEnumerable<SpellId> equipados = p.basicSpellIds ?? new List<SpellId>();
            if (p.companionBasics != null) equipados = equipados.Concat(p.companionBasics.Where(c => c?.hechizos != null).SelectMany(c => c.hechizos));
            var sinAprender = equipados.Where(id => id != SpellId.None && !aprendidos.Contains(id)).Distinct().ToList();
            if (sinAprender.Count > 0)
                lines.Add(new StartLine { Label = "Hechizos", Value = $"{string.Join(", ", sinAprender)}: equipado sin aprender; al probar se da por aprendido.", Warning = true });
            var ajenos = (p.basicSpellIds ?? new List<SpellId>()).Select(HechizoPorId)
                .Where(h => h != null && (h.caster != PartyControlManager.CharacterSlot.Will || h.slotType == SpellSlotType.SpecialOnly)).ToList();
            foreach (var h in ajenos)
                lines.Add(new StartLine { Label = "Hechizos", Value = h.slotType == SpellSlotType.SpecialOnly
                    ? $"{h.name}: es de combo (solo especial), no va en los básicos de Will."
                    : $"{h.name}: es de {h.caster}, no de Will; va en sus básicos de compañero.", Warning = true });
            if ((p.basicSpellIds?.Count ?? 0) > 0 && p.abilities != null && !p.abilities.magic)
                lines.Add(new StartLine { Label = "Magia", Value = "Hay hechizos equipados pero la habilidad Magic está desactivada: no se podrán lanzar.", Warning = true });
        }
    }
}