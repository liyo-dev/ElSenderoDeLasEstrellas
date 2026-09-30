using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpellPresentation
{
    public SpellId spellId;
    public string title;
    [TextArea]
    public string description;
    public Sprite icon;
}

public static class SpellPresentationLookup
{
    private static readonly Dictionary<SpellId, SpellPresentation> Defaults = new()
    {
        { SpellId.Fireball,     new SpellPresentation { spellId = SpellId.Fireball,     title = "Bola de fuego",  description = "Proyectil ardiente que abrasa a los enemigos." } },
        { SpellId.Plasmaball,   new SpellPresentation { spellId = SpellId.Plasmaball,   title = "Plasmaball",     description = "Orbe de energía pura de alto impacto." } },
        { SpellId.CorazonEstelar, new SpellPresentation { spellId = SpellId.CorazonEstelar, title = "Corazón Estelar", description = "Un haz de energía estelar pura." } },
        { SpellId.Levitation,   new SpellPresentation { spellId = SpellId.Levitation,   title = "Levitación",     description = "Permite flotar sobre el terreno." } },
        { SpellId.AuraEstelar,  new SpellPresentation { spellId = SpellId.AuraEstelar,  title = "Aura Estelar",   description = "Rodéate de un aura de energía estelar." } },
        { SpellId.Cycloneburst, new SpellPresentation { spellId = SpellId.Cycloneburst, title = "Ciclón",         description = "Explosión de viento devastadora en área." } },
    };

    public static SpellPresentation Resolve(SpellId spellId, IList<SpellPresentation> custom)
    {
        if (custom != null)
        {
            for (int i = 0; i < custom.Count; i++)
            {
                var entry = custom[i];
                if (entry != null && entry.spellId == spellId)
                    return entry;
            }
        }

        if (Defaults.TryGetValue(spellId, out var preset))
            return preset;

        // Hechizos del grimorio (INC-503): nombre, icono, de quién es y cómo se lanza, del propio hechizo.
        var spell = GrimorioDelPersonaje.Hechizo(spellId);
        if (spell != null)
            return new SpellPresentation
            {
                spellId = spellId,
                title = spell.GetLocalizedName(),
                description = DescribeForPopup(spell),
                icon = spell.attackIcon
            };

        return new SpellPresentation
        {
            spellId = spellId,
            title = spellId.ToString(),
            description = string.Empty,
            icon = null
        };
    }

    /// "Combo de Estela: Y · B · X" / "Básico de Liam. Equípalo en Equipo › Hechizos."
    static string DescribeForPopup(MagicSpellSO spell)
    {
        string quien = GrimorioDelPersonaje.Nombre(spell.caster);
        string Loc(string key, string fallback) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;
        if (spell.HasCombo)
        {
            return string.Format(Loc("SPELL_LEARNED_COMBO", "Combo de {0}: abre el círculo con {1} y teclea {2}."), quien,
                Core.InputGlyphs.ComboButtonGlyphs.Label(ComboButton.Y), Core.InputGlyphs.ComboButtonGlyphs.SequenceLabel(spell.comboSequence));
        }
        return string.Format(Loc("SPELL_LEARNED_BASIC", "Hechizo básico de {0}. Equípalo en Equipo › Hechizos."), quien);
    }
}
