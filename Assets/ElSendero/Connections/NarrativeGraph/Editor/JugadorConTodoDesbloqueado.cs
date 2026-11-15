using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Jugador de prueba «Todo desbloqueado» (Arranque › Nuevo ▾): todas las habilidades, todos los
    /// hechizos que el jugador puede aprender (los que da la historia y los de las páginas del
    /// grimorio de las escenas) y toda la ropa que puede llevar Will. Lo demás (sitio, vida,
    /// básicos equipados…) sale de la historia hasta el paso desde el que se prueba.
    public sealed class JugadorConTodoDesbloqueado : INarrativeTestPlayerPreset
    {
        public string Name => "Todo desbloqueado";

        public IEnumerable<string> Fields => new[]
        {
            nameof(PlayerPresetSO.abilities), nameof(PlayerPresetSO.unlockedSpells), nameof(PlayerPresetSO.unlockedWardrobeIds),
        };

        public Object Build(NarrativePlayerSheet sheet)
        {
            var datos = ScriptableObject.CreateInstance<PlayerPresetSO>();
            datos.abilities = new PlayerAbilities { swim = true, jump = true, magic = true, fly = true, sprint = true, shield = true };
            datos.unlockedSpells = HechizosQueSePuedenAprender().ToList();
            datos.unlockedWardrobeIds = AssetDatabase.FindAssets("t:" + nameof(WardrobeItemSO))
                .Select(g => AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.PuedeLlevarla(PartyControlManager.CharacterSlot.Will))
                .Select(w => w.WardrobeId).Distinct().OrderBy(id => id).ToList();
            return datos;
        }

        /// Los que da la historia («Desbloquear habilidades») y los de las páginas del grimorio que
        /// hay en las escenas.
        static IEnumerable<SpellId> HechizosQueSePuedenAprender()
        {
            var hechizos = new SortedSet<SpellId>();
            foreach (var g in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g));
                if (grafo?.nodes == null) continue;
                foreach (var paso in grafo.nodes.OfType<GameStepNode>())
                    if (paso.request is PedirDesbloqueo d) foreach (var h in d.spellsToUnlock) hechizos.Add(h);
            }

            var script = AssetDatabase.FindAssets("t:MonoScript " + nameof(PaginaDelGrimorio))
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == nameof(PaginaDelGrimorio));
            if (script != null)
            {
                var pagina = new Regex(@"m_Script: \{fileID: 11500000, guid: " + AssetDatabase.AssetPathToGUID(script) + @"[^\n]*\n(?:(?!--- !u!)[\s\S])*?\n  hechizo: (\d+)");
                foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    var ruta = AssetDatabase.GUIDToAssetPath(g);
                    string texto;
                    try { texto = File.ReadAllText(ruta); } catch (IOException) { continue; }
                    if (!texto.Contains(AssetDatabase.AssetPathToGUID(script))) continue;
                    foreach (Match m in pagina.Matches(texto)) hechizos.Add((SpellId)int.Parse(m.Groups[1].Value));
                }
            }
            hechizos.Remove(SpellId.None);
            return hechizos;
        }
    }
}
