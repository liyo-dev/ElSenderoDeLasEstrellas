using System;
using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>Pestaña «En vivo» del editor del grafo: quién va al mando, vida y maná del grupo, habilidades y hechizos.</summary>
    public sealed class PartyLiveSection : INarrativeLiveSection
    {
        // Orden en que se enseña el grupo (el mismo que los retratos del HUD).
        static readonly PartyControlManager.CharacterSlot[] Grupo =
        {
            PartyControlManager.CharacterSlot.Will,
            PartyControlManager.CharacterSlot.Liam,
            PartyControlManager.CharacterSlot.Estela,
        };

        public string Title => "Grupo";
        public int Order => 0;
        public string EmptyText => "Sin datos del grupo.";

        public void Collect(List<string> rows)
        {
            var active = ControlDelJugador.Activo;
            rows.Add($"Al mando: {active}");

            var salud = SaludDelGrupo.Instance;
            if (salud == null)
            {
                rows.Add("(SaludDelGrupo no disponible)");
            }
            else
            {
                foreach (var slot in Grupo)
                {
                    if (!ControlDelJugador.EnElGrupo(slot)) continue;
                    string mark = slot == active ? "▶ " : "";
                    rows.Add($"{mark}{slot} {salud.Vida(slot):0}/{salud.VidaMax(slot):0} · maná {salud.Mana(slot):0}/{salud.ManaMax(slot):0}");
                }
            }

            var preset = Partida.Preset;
            if (preset == null)
            {
                rows.Add("Habilidades y hechizos: (sin preset activo)");
                return;
            }

            var abilities = new List<string>();
            var a = preset.abilities ?? new PlayerAbilities();
            if (a.jump) abilities.Add("saltar");
            if (a.swim) abilities.Add("nadar");
            if (a.climb) abilities.Add("trepar");
            if (a.fly) abilities.Add("volar");
            if (a.sprint) abilities.Add("sprint");
            if (a.shield) abilities.Add("escudo");
            if (a.magic) abilities.Add("magia");
            var spells = preset.basicSpellIds ?? new List<SpellId>();
            var unlocked = preset.unlockedSpells ?? new List<SpellId>();
            rows.Add($"Habilidades: {(abilities.Count == 0 ? "ninguna" : string.Join(", ", abilities))}");
            rows.Add($"Hechizos equipados: {(spells.Count == 0 ? "ninguno" : string.Join(", ", spells))}");
            rows.Add($"Hechizos desbloqueados: {(unlocked.Count == 0 ? "ninguno" : string.Join(", ", unlocked))}");
        }
    }

    /// <summary>Pestaña «En vivo»: NPCs con NarrativeActor en las escenas cargadas y dónde están.</summary>
    public sealed class ActorsLiveSection : INarrativeLiveSection
    {
        public string Title => "NPCs en escena";
        public int Order => 1;
        public string EmptyText => "No hay NPCs con NarrativeActor en las escenas cargadas.";

        public void Collect(List<string> rows)
        {
            rows.AddRange(UnityEngine.Object.FindObjectsByType<NarrativeActor>(FindObjectsInactive.Exclude)
                .Where(a => a != null)
                .Select(a =>
                {
                    var id = string.IsNullOrEmpty(a.ActorId) ? a.name : a.ActorId;
                    var p = a.transform.position;
                    return $"{id}  ·  {a.gameObject.scene.name}  ·  ({p.x:0.0},{p.y:0.0},{p.z:0.0})";
                })
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>Pestaña «En vivo»: misiones del QuestManager y su estado.</summary>
    public sealed class QuestsLiveSection : INarrativeLiveSection
    {
        public string Title => "Quests";
        public int Order => 2;
        public string EmptyText => "No hay quests en curso.";

        public void Collect(List<string> rows)
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;
            var quests = new List<string>();
            foreach (var q in qm.GetAll())
            {
                if (q == null) continue;
                quests.Add($"{q.Data?.displayName ?? q.Id}  [{q.State}]");
            }
            quests.Sort(StringComparer.OrdinalIgnoreCase);
            rows.AddRange(quests);
        }
    }
}
