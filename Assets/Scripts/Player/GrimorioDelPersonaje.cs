using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Qué magia tiene cada personaje (INC-503). El grimorio es uno solo para todo el grupo
/// (<c>PlayerPresetSO.unlockedSpells</c>): cada hechizo dice de quién es (<c>MagicSpellSO.caster</c>).
///
/// - Will: sus básicos equipados son <c>basicSpellIds</c> del preset; lo que sabe, lo desbloqueado
///   que sea suyo.
/// - Estela y Liam: saben lo de su ficha (lo que traen de serie) más lo desbloqueado que sea suyo.
///   Sus básicos equipados se guardan en <c>PlayerPresetSO.companionBasics</c>; si aún no hay nada
///   guardado, llevan los de su ficha.
///
/// Los combos no se equipan: salen todos con la Y cuando el personaje va al mando.
/// </summary>
public static class GrimorioDelPersonaje
{
    /// El último hechizo aprendido en esta sesión. El grimorio del menú se abre por él.
    public static SpellId UltimoAprendido { get; set; } = SpellId.None;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => UltimoAprendido = SpellId.None;
#endif

    // ── Datos ─────────────────────────────────────────────────────────────

    public static SpellLibrarySO Biblioteca
    {
        get
        {
            PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: true, allowSceneLookup: true);
            return pps != null ? pps.SpellLibrary : null;
        }
    }

    public static MagicSpellSO Hechizo(SpellId id)
    {
        if (id == SpellId.None) return null;
        var lib = Biblioteca;
        return lib != null && lib.TryGet(id, out var s) ? s : null;
    }

    /// La ficha de Estela o Liam (la de su cuerpo en escena; si no está, la del proyecto).
    public static FichaDePersonaje Ficha(Slot slot)
    {
        if (slot == Slot.Will) return null;
        var party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
        var member = party != null ? party.GetMemberByName(slot == Slot.Liam ? "Liam" : "Estela") : null;
        var personaje = member != null ? member.GetComponent<Personaje>() : null;
        if (personaje != null && personaje.Ficha != null) return personaje.Ficha;
        return FichaDePersonaje.Buscar(slot);
    }

    static PlayerPresetSO Preset => UnlockService.GetActivePreset();

    /// Es magia del grupo, con página en el grimorio. Los hechizos de enemigos (Mago Oscuro,
    /// Huracán) y las piezas internas de otro hechizo (el fuego que deja Chispa Ígnea) comparten
    /// biblioteca, pero ni se aprenden ni se equipan.
    public static bool EsDelGrimorio(MagicSpellSO s) =>
        s != null && s.spellId != SpellId.None
        && s.spellId != SpellId.MagoOscuroGolpe && s.spellId != SpellId.MagoOscuroGrieta
        && s.spellId != SpellId.Huracan && s.spellId != SpellId.ChispaIgneaFuego;

    /// Se equipa como básico (X, rota con LB): proyectiles y haces del grimorio. Zonas,
    /// teletransporte y levitación no.
    public static bool EsBasico(MagicSpellSO s) =>
        EsDelGrimorio(s) && s.slotType != SpellSlotType.SpecialOnly
        && s.kind != MagicKind.Zone && s.kind != MagicKind.Teleport && s.kind != MagicKind.Levitation;

    // ── Lo que sabe ───────────────────────────────────────────────────────

    /// Básicos que el personaje puede equipar.
    public static List<MagicSpellSO> BasicosDisponibles(Slot slot)
    {
        var result = new List<MagicSpellSO>();
        if (slot != Slot.Will)
        {
            var ficha = Ficha(slot);
            if (ficha != null)
                foreach (var s in ficha.Basicos) Add(result, s, soloBasicos: true);
        }
        foreach (var s in Desbloqueados(slot)) Add(result, s, soloBasicos: true);
        return result;
    }

    /// Combos (Y) que el personaje conoce.
    public static List<MagicSpellSO> CombosDisponibles(Slot slot)
    {
        var result = new List<MagicSpellSO>();
        if (slot != Slot.Will)
        {
            var ficha = Ficha(slot);
            if (ficha != null && ficha.Combos != null)
                foreach (var s in ficha.Combos)
                    if (s != null && s.HasCombo && !result.Contains(s)) result.Add(s);
        }
        foreach (var s in Desbloqueados(slot))
            if (s.HasCombo && !result.Contains(s)) result.Add(s);
        return result;
    }

    /// Hechizos desbloqueados en el grimorio que son de este personaje.
    public static IEnumerable<MagicSpellSO> Desbloqueados(Slot slot)
    {
        var preset = Preset;
        var lib = Biblioteca;
        if (preset == null || preset.unlockedSpells == null || lib == null) yield break;
        foreach (var id in preset.unlockedSpells)
            if (lib.TryGet(id, out var s) && EsDelGrimorio(s) && s.caster == slot)
                yield return s;
    }

    static void Add(List<MagicSpellSO> list, MagicSpellSO s, bool soloBasicos)
    {
        if (s == null || list.Contains(s)) return;
        if (soloBasicos && !EsBasico(s)) return;
        list.Add(s);
    }

    // ── Lo que lleva equipado (Estela y Liam) ─────────────────────────────

    /// Ids de los básicos equipados de un compañero; null si nunca se han cambiado (lleva los de la ficha).
    public static List<SpellId> IdsEquipados(PlayerPresetSO preset, Slot slot, bool crear)
    {
        if (preset == null || slot == Slot.Will) return null;
        preset.companionBasics ??= new List<PlayerPresetSO.BasicosDeCompanero>();
        foreach (var e in preset.companionBasics)
            if (e != null && e.personaje == slot)
            {
                e.hechizos ??= new List<SpellId>();
                return e.hechizos;
            }
        if (!crear) return null;

        // La primera vez parte de los de su ficha.
        var nueva = new PlayerPresetSO.BasicosDeCompanero { personaje = slot, hechizos = new List<SpellId>() };
        var ficha = Ficha(slot);
        if (ficha != null)
            foreach (var s in ficha.Basicos)
                if (s != null && nueva.hechizos.Count < MagicCaster.BasicSlotCount) nueva.hechizos.Add(s.spellId);
        preset.companionBasics.Add(nueva);
        return nueva.hechizos;
    }

    /// Básicos que lleva equipados un compañero (hasta 4, en orden de LB, sin huecos).
    public static List<MagicSpellSO> BasicosEquipados(Slot slot)
    {
        var result = new List<MagicSpellSO>(MagicCaster.BasicSlotCount);
        if (slot == Slot.Will) return result;

        var ids = IdsEquipados(Preset, slot, crear: false);
        if (ids == null)
        {
            var ficha = Ficha(slot);
            if (ficha != null) result.AddRange(ficha.Basicos);
            return result;
        }

        var disponibles = BasicosDisponibles(slot);
        foreach (var id in ids)
        {
            if (id == SpellId.None) continue;
            var s = disponibles.Find(x => x.spellId == id);
            if (s != null && !result.Contains(s) && result.Count < MagicCaster.BasicSlotCount) result.Add(s);
        }
        return result;
    }

    /// Pone en juego los básicos equipados de ese personaje: en el MagicCaster si va al mando, en
    /// su IA si no.
    public static void Aplicar(Slot slot)
    {
        if (slot == Slot.Will) return; // Will: PlayerPresetService.ApplyCurrentPreset
        var basicos = BasicosEquipados(slot);
        Slot activo = PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;

        if (activo == slot)
        {
            if (PlayerService.TryGetComponent<MagicCaster>(out var caster, includeInactive: true, allowSceneLookup: true) && caster != null)
                caster.SetBasicSpells(basicos);
        }

        var party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
        var member = party != null ? party.GetMemberByName(slot == Slot.Liam ? "Liam" : "Estela") : null;
        if (member != null)
        {
            var ficha = Ficha(slot);
            member.SetRuntimeBasics(basicos, ficha != null ? ficha.Hechizo(2) : null);
        }
    }

    /// Al aprender un básico de Estela o Liam, se equipa solo si le queda hueco.
    public static void EquiparSiHayHueco(MagicSpellSO spell)
    {
        if (spell == null || spell.caster == Slot.Will || !EsBasico(spell)) return;
        var ids = IdsEquipados(Preset, spell.caster, crear: true);
        if (ids == null || ids.Contains(spell.spellId)) return;
        int count = 0;
        foreach (var id in ids) if (id != SpellId.None) count++;
        if (count >= MagicCaster.BasicSlotCount) return;
        int hueco = ids.IndexOf(SpellId.None);
        if (hueco >= 0) ids[hueco] = spell.spellId; else ids.Add(spell.spellId);
        Aplicar(spell.caster);
    }

    /// Lo que hace de especial, en una línea (rebota, ralentiza, cura...). Para el grimorio (INC-506).
    public static string Efectos(MagicSpellSO s)
    {
        if (s == null) return "";
        string L(string key, string fallback) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;
        var parts = new List<string>();
        if (s.bounceCount > 0) parts.Add(string.Format(L("SPELL_FX_BOUNCE", "Rebota {0} veces"), s.bounceCount));
        if (s.pierceCount < 0) parts.Add(L("SPELL_FX_PIERCE_ALL", "Atraviesa a todos"));
        else if (s.pierceCount > 0) parts.Add(string.Format(L("SPELL_FX_PIERCE", "Atraviesa {0}"), s.pierceCount));
        if (s.spreadCount > 1) parts.Add(string.Format(L("SPELL_FX_SPREAD", "Abanico de {0}"), s.spreadCount));
        if (s.impactZone != null) parts.Add(L("SPELL_FX_IMPACT_ZONE", "Deja fuego al impactar"));
        switch (s.statusEffect)
        {
            case EstadoDeCombate.Ralentizar: parts.Add(L("SPELL_FX_SLOW", "Ralentiza")); break;
            case EstadoDeCombate.Inmovilizar: parts.Add(L("SPELL_FX_ROOT", "Inmoviliza")); break;
            case EstadoDeCombate.Atraer: parts.Add(L("SPELL_FX_PULL", "Atrae al centro")); break;
            case EstadoDeCombate.Empujar: parts.Add(L("SPELL_FX_PUSH", "Aparta a los enemigos")); break;
        }
        if (s.knockbackForce > 12f && s.kind == MagicKind.Projectile) parts.Add(L("SPELL_FX_KNOCKBACK", "Empuja"));
        if (s.healPerTick > 0f) parts.Add(L("SPELL_FX_HEAL", "Cura al grupo"));
        if (s.groupShieldSeconds > 0f) parts.Add(L("SPELL_FX_SHIELD", "Escudo al grupo"));
        if (s.zoneOnCaster) parts.Add(L("SPELL_FX_ON_CASTER", "Alrededor de quien lo lanza"));
        if (s.kind == MagicKind.Teleport) parts.Add(string.Format(L("SPELL_FX_BLINK", "Teletransporte de {0} m"), s.teleportDistance));
        return string.Join(" · ", parts);
    }

    /// "Básico de Estela" / "Combo de Liam: Y · B · X" — para avisos y el grimorio.
    public static string Nombre(Slot slot) => slot switch
    {
        Slot.Liam => "Liam",
        Slot.Estela => "Estela",
        _ => "Will"
    };
}
