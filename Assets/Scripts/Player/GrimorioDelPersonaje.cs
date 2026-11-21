using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;
using Liyodev.NarrativeGraph;

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
///
/// Sets fijos (decidido por Raúl, 20 nov 2026): cada personaje tiene 4 rápidos (X) y 4 combos (Y)
/// de sus elementos, los de su ficha (también la de Will), y aparte un especial que se aprende con
/// una misión (<see cref="MagicSpellSO.especialConMision"/>). Ya no se eligen: los rápidos que sabe
/// van siempre puestos, en el orden de la ficha (<see cref="BasicosEquipados"/>). Will los va
/// aprendiendo con la historia; Estela y Liam los traen todos.
/// </summary>
public static class GrimorioDelPersonaje
{
    /// El último hechizo aprendido en esta sesión. El grimorio del menú se abre por él.
    public static SpellId UltimoAprendido { get; set; } = SpellId.None;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => UltimoAprendido = SpellId.None;
#endif

    /// Flag de la partida que dice que Will ya tiene el grimorio: se lo da Erika en el Cap. 2.
    /// Hasta entonces el libro no se abre ni sale en la pestaña Hechizos. Ver INC-961.
    public const string FlagGrimorioRecibido = "GRIMORIO_RECIBIDO";

    /// Will tiene el grimorio: se lo ha dado Erika o el Cap. 2 ya está cerrado (partidas guardadas
    /// antes de que existiera el flag).
    public static bool TieneGrimorio =>
        NarrativeFlags.Has(FlagGrimorioRecibido) || NarrativeFlags.Has("CAP2_COMPLETADO");

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
        var member = ControlDelJugador.MiembroDe(slot);
        var personaje = member != null ? member.GetComponent<Personaje>() : null;
        if (personaje != null && personaje.Ficha != null) return personaje.Ficha;
        return FichaDePersonaje.Buscar(slot);
    }

    static PlayerPresetSO Preset => UnlockService.GetActivePreset();

    /// Es magia del grupo, con página en el grimorio. Los hechizos de enemigos (Mago Oscuro,
    /// Huracán) y las piezas internas de otro hechizo (el fuego que deja Chispa Ígnea) comparten
    /// biblioteca, pero ni se aprenden ni se equipan.
    public static bool EsDelGrimorio(MagicSpellSO s) =>
        s != null && s.spellId != SpellId.None && s.enGrimorio;

    /// Se equipa como básico (X, rota con LB): proyectiles y haces del grimorio. Zonas,
    /// teletransporte y levitación no.
    /// La levitación sí (es un rápido de Liam: se mantiene la X).
    public static bool EsBasico(MagicSpellSO s) =>
        EsDelGrimorio(s) && s.slotType != SpellSlotType.SpecialOnly && !s.especialConMision
        && s.kind != MagicKind.Zone && s.kind != MagicKind.Teleport && s.kind != MagicKind.Invisibility;

    // ── El set fijo de cada uno (4 rápidos, 4 combos y el especial) ───────

    /// La ficha del personaje, también la de Will (para el orden de su set).
    public static FichaDePersonaje FichaDelSet(Slot slot) => slot == Slot.Will ? FichaDePersonaje.De(slot) : Ficha(slot);

    /// Los 4 rápidos del set del personaje, en su orden (los sepa ya o no).
    public static List<MagicSpellSO> RapidosDelSet(Slot slot)
    {
        var result = new List<MagicSpellSO>(MagicCaster.BasicSlotCount);
        var ficha = FichaDelSet(slot);
        if (ficha != null)
            foreach (var s in ficha.Basicos) Add(result, s, soloBasicos: true);
        return result;
    }

    /// Los 4 combos del set del personaje, en su orden (los sepa ya o no). Sin el especial.
    public static List<MagicSpellSO> CombosDelSet(Slot slot)
    {
        var result = new List<MagicSpellSO>(4);
        var ficha = FichaDelSet(slot);
        if (ficha != null && ficha.Combos != null)
            foreach (var s in ficha.Combos)
                if (s != null && s.HasCombo && !s.especialConMision && EsDelGrimorio(s) && !result.Contains(s)) result.Add(s);
        return result;
    }

    /// El especial con misión del personaje (Will: Amanecer), lo sepa ya o no. Null si aún no tiene.
    public static MagicSpellSO EspecialDe(Slot slot)
    {
        var lib = Biblioteca;
        if (lib == null) return null;
        foreach (var s in lib.Spells)
            if (s != null && s.especialConMision && s.caster == slot && EsDelGrimorio(s)) return s;
        return null;
    }

    /// Ya lo sabe: Will, si lo ha desbloqueado; Estela y Liam, si viene en su ficha o lo han desbloqueado.
    public static bool LoSabe(Slot slot, MagicSpellSO s)
    {
        if (s == null) return false;
        if (slot != Slot.Will)
        {
            var ficha = Ficha(slot);
            if (ficha != null)
            {
                foreach (var b in ficha.Basicos) if (b == s) return true;
                if (ficha.Combos != null) foreach (var c in ficha.Combos) if (c == s) return true;
            }
        }
        var preset = Preset;
        return preset != null && preset.unlockedSpells != null && preset.unlockedSpells.Contains(s.spellId) && s.caster == slot;
    }

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

    /// Rápidos que lleva puestos (hasta 4, en orden de LB, sin huecos): los de su set que ya sabe,
    /// en el orden de la ficha; si sabe alguno más de los suyos (partidas antiguas), detrás.
    public static List<MagicSpellSO> BasicosEquipados(Slot slot)
    {
        var result = new List<MagicSpellSO>(MagicCaster.BasicSlotCount);
        foreach (var s in RapidosDelSet(slot))
            if (LoSabe(slot, s) && result.Count < MagicCaster.BasicSlotCount) result.Add(s);
        var otros = slot == Slot.Will ? new List<MagicSpellSO>(Desbloqueados(slot)) : BasicosDisponibles(slot);
        foreach (var s in otros)
            if (EsBasico(s) && !result.Contains(s) && result.Count < MagicCaster.BasicSlotCount) result.Add(s);
        return result;
    }

    /// Pone en el cuerpo de ese personaje los básicos que lleva equipados: en su MagicCaster (para
    /// cuando lo lleve el jugador) y en su IA.
    public static void Aplicar(Slot slot)
    {
        if (slot == Slot.Will) return; // Will: PlayerPresetService.ApplyCurrentPreset
        var cuerpo = ControlDelJugador.PilotoDe(slot);
        if (cuerpo == null) return;
        var basicos = BasicosEquipados(slot);

        var caster = cuerpo.GetComponent<MagicCaster>();
        if (caster != null) caster.SetBasicSpells(basicos);

        var ia = cuerpo.GetComponent<Game.NPC.NPCPartyMember>();
        if (ia != null)
        {
            var ficha = Ficha(slot);
            ia.SetRuntimeBasics(basicos, ficha != null ? ficha.Hechizo(2) : null);
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
            case EstadoDeCombate.Aturdir: parts.Add(L("SPELL_FX_STUN", "Aturde")); break;
            case EstadoDeCombate.Dormir: parts.Add(L("SPELL_FX_SLEEP", "Duerme (despierta al recibir un golpe)")); break;
        }
        if (s.knockbackForce > 12f && s.kind == MagicKind.Projectile) parts.Add(L("SPELL_FX_KNOCKBACK", "Empuja"));
        if (s.healPerTick > 0f) parts.Add(L("SPELL_FX_HEAL", "Cura al grupo"));
        if (s.groupShieldSeconds > 0f) parts.Add(L("SPELL_FX_SHIELD", "Escudo al grupo"));
        if (s.zoneOnCaster) parts.Add(L("SPELL_FX_ON_CASTER", "Alrededor de quien lo lanza"));
        if (s.kind == MagicKind.Teleport) parts.Add(string.Format(L("SPELL_FX_BLINK", "Teletransporte de {0} m"), s.teleportDistance));
        if (s.kind == MagicKind.Invisibility) parts.Add(string.Format(L("SPELL_FX_INVISIBLE", "Invisible {0} s (se rompe al atacar)"), s.invisibilidadSegundos));
        return string.Join(" · ", parts);
    }

    /// "Básico de Estela" / "Combo de Liam: Y · B · X" — para avisos y el grimorio. El nombre es
    /// el de <see cref="ControlDelJugador.Nombre"/>.
    public static string Nombre(Slot slot) => ControlDelJugador.Nombre(slot);
}
