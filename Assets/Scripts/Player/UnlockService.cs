using System.Collections.Generic;
using UnityEngine;

/// Utilidades para modificar el preset activo del jugador (runtimePreset) de forma segura/idempotente.
public static class UnlockService
{
    public static event System.Action<AbilityId> OnAbilityUnlocked;
    public static event System.Action<AbilityKey> OnAbilityUnlockedKey;
    public static event System.Action<SpellId> OnSpellUnlocked;

    /// Intenta obtener el preset activo (runtime) del GameBootProfile.
    public static PlayerPresetSO GetActivePreset()
    {
        if (!GameBootService.IsAvailable || GameBootService.Profile == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[UnlockService] GameBootService no está disponible todavía");
#endif
            return null;
        }
        return Partida.Preset;
    }

    /// Desbloquea una habilidad en el preset activo. Devuelve true si se modificó el preset (lista u otros datos asociados).
    public static bool UnlockAbility(AbilityId ability)
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.unlockedAbilities == null)
            preset.unlockedAbilities = new List<AbilityId>();

        bool changed = false;

        bool isNewUnlock = false;
        if (!preset.unlockedAbilities.Contains(ability))
        {
            preset.unlockedAbilities.Add(ability);
            changed = true;
            isNewUnlock = true;
            OnAbilityUnlocked?.Invoke(ability);
        }

        switch (ability)
        {
            case AbilityId.MagicAttack:
                // Garantizar que el preset permite usar magia (habilita PlayerActionManager.AllowMagic)
                if (preset.abilities == null)
                    preset.abilities = new PlayerAbilities();

                if (!preset.abilities.magic)
                {
                    preset.abilities.magic = true;
                    changed = true;
                }

                // Asegurar que exista una reserva mínima de maná y RELLENAR AL 100%
                const float minMagicMaxMp = 50f;
                float desiredMax = Mathf.Max(preset.maxMP, minMagicMaxMp);
                float desiredCurrent = desiredMax;

                if (!Mathf.Approximately(preset.maxMP, desiredMax) || !Mathf.Approximately(preset.currentMP, desiredCurrent))
                {
                    preset.maxMP = desiredMax;
                    preset.currentMP = desiredCurrent;
                    changed = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[UnlockService] MagicAttack desbloqueado: Maná seteado a {desiredCurrent}/{desiredMax} (100%)");
#endif
                }

                // Propagar el unlock de Magic (AbilityKey) SOLO si es la primera vez
                if (isNewUnlock)
                    try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Magic); } catch { }
                break;
        }

        // CRÍTICO: Aplicar cambios al jugador inmediatamente
        if (changed)
        {
            ApplyPresetToPlayer();
        }

        return changed;
    }

    /// Desbloquea una de las habilidades booleanas principales (Swim, Jump, Magic, Fly, Sprint, Shield)
    /// para los tres personajes: la historia la enseña al grupo. Cada uno parte de las suyas
    /// (HabilidadesDelPersonaje), así que solo cambia quien aún no la tenía. La magia de Will,
    /// además, le pone una reserva mínima de maná y se la rellena.
    public static bool UnlockAbility(AbilityKey key)
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        bool changed = false;
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
            changed |= HabilidadesDelPersonaje.Aprender(preset, (PartyControlManager.CharacterSlot)i, key);

        if (key == AbilityKey.Magic)
        {
            const float minMagicMaxMp = 50f;
            float newMax = Mathf.Max(preset.maxMP, minMagicMaxMp);
            if (!Mathf.Approximately(preset.maxMP, newMax) || !Mathf.Approximately(preset.currentMP, newMax))
            {
                preset.maxMP = newMax;
                preset.currentMP = newMax;
                changed = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[UnlockService] Magic desbloqueada: maná de Will a {newMax}/{newMax}");
#endif
            }
        }

        if (changed)
        {
            try { OnAbilityUnlockedKey?.Invoke(key); } catch { }
            ApplyPresetToPlayer();
        }

        return changed;
    }

    /// Desbloquea un hechizo y opcionalmente lo asigna a un slot vacío.
    public static bool UnlockSpell(SpellId spell, bool assignToEmptySlot = true)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.unlockedSpells == null) preset.unlockedSpells = new List<SpellId>();

        bool changed = false;
        bool isNewUnlock = false;

        if (!preset.unlockedSpells.Contains(spell))
        {
            preset.unlockedSpells.Add(spell);
            changed = true;
            isNewUnlock = true;
            if (spell != SpellId.None)
            {
                GrimorioDelPersonaje.UltimoAprendido = spell;
                OnSpellUnlocked?.Invoke(spell);
            }
        }

        // La primera vez que se desbloquea un hechizo básico se equipa si queda hueco entre los
        // básicos. Al cargar partida (ya estaba desbloqueado) se respeta lo que eligió el jugador.
        // Los de solo especial no se equipan: son combos.
        if (assignToEmptySlot && isNewUnlock && spell != SpellId.None)
        {
            bool isSpecialOnly = false;
            MagicSpellSO spellSO = null;
            if (PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: false, allowSceneLookup: true))
            {
                var library = pps?.SpellLibrary;
                if (library != null && library.TryGet(spell, out spellSO))
                    isSpecialOnly = spellSO.slotType == SpellSlotType.SpecialOnly;
            }

            // Un básico de Estela o Liam va a sus ranuras, no a las de Will (INC-503).
            if (spellSO != null && spellSO.caster != PartyControlManager.CharacterSlot.Will)
            {
                if (!isSpecialOnly) GrimorioDelPersonaje.EquiparSiHayHueco(spellSO);
                return changed;
            }

            preset.basicSpellIds ??= new List<SpellId>();
            if (!isSpecialOnly && !preset.basicSpellIds.Contains(spell) && preset.basicSpellIds.Count < MagicCaster.BasicSlotCount)
            {
                preset.basicSpellIds.Add(spell);
                changed = true;
            }
        }

        // CRÍTICO: Aplicar cambios al jugador inmediatamente (para que MagicCaster sincronice los slots)
        if (changed)
        {
            ApplyPresetToPlayer();
        }

        return changed;
    }

    /// Asegura unos valores mínimos de maná en el preset activo.
    public static bool EnsureMana(float minMax, float minCurrent)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        float oldMax = preset.maxMP;
        float oldCur = preset.currentMP;
        preset.maxMP = Mathf.Max(preset.maxMP, minMax);
        preset.currentMP = Mathf.Clamp(Mathf.Max(preset.currentMP, minCurrent), 0f, preset.maxMP);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[UnlockService] EnsureMana(minMax={minMax}, minCurrent={minCurrent}) - Antes: {oldMax}/{oldCur} -> Después: {preset.maxMP}/{preset.currentMP}");
#endif
        return !Mathf.Approximately(oldMax, preset.maxMP) || !Mathf.Approximately(oldCur, preset.currentMP);
    }

    /// Añade un flag simple al preset (misiones/progresos).
    public static bool AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.flags == null) preset.flags = new List<string>();
        if (preset.flags.Contains(flag)) return false;
        preset.flags.Add(flag);
        return true;
    }

    /// Si el personaje que va al mando tiene esa habilidad (Swim, Jump, Fly…).
    public static bool HasAbility(AbilityKey key) => HasAbility(key, ControlDelJugador.Activo);

    /// Si ese personaje tiene esa habilidad.
    public static bool HasAbility(AbilityKey key, PartyControlManager.CharacterSlot personaje)
    {
        var preset = GetActivePreset();
        return preset && HabilidadesDelPersonaje.De(preset, personaje).Tiene(key);
    }

    /// Verifica si un flag existe en el preset activo.
    public static bool HasFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        var preset = GetActivePreset();
        if (!preset || preset.flags == null) return false;
        return preset.flags.Contains(flag);
    }

    /// Aplica el preset activo al jugador en escena (si existe).
    /// Esto actualiza ManaPool, hechizos, abilities, etc. inmediatamente.
    private static void ApplyPresetToPlayer()
    {
        // Buscar PlayerPresetService en el jugador activo
        if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: false, allowSceneLookup: true))
        {
            // CORREGIDO: NO incluir inventario al aplicar preset tras desbloquear abilities
            // El inventario debe conservarse con los items que el jugador ha recogido
            presetService.ApplyCurrentPreset(includeInventory: false, includeAbilities: true);
        }
    }
}

