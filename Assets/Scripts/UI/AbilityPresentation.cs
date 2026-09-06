using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Información de presentación para cada habilidad (nombre, descripción e icono).
/// Se usa tanto en el inventario como en los popups de desbloqueo.
/// </summary>
[System.Serializable]
public class AbilityPresentation
{
    public AbilityId abilityId;
    public string title;
    [TextArea]
    public string description;
    public Sprite icon;
}

public static class AbilityPresentationLookup
{
    private static readonly Dictionary<AbilityId, AbilityPresentation> Defaults = new()
    {
        { AbilityId.PhysicalAttack, new AbilityPresentation { abilityId = AbilityId.PhysicalAttack, title = "Ataque físico", description = "Golpe básico cuerpo a cuerpo." } },
        { AbilityId.MagicAttack,    new AbilityPresentation { abilityId = AbilityId.MagicAttack,    title = "Magia",          description = "Permite canalizar y usar hechizos." } },
        { AbilityId.Dash,           new AbilityPresentation { abilityId = AbilityId.Dash,           title = "Impulso",        description = "Esquiva rápida para evitar daño." } },
        { AbilityId.Block,          new AbilityPresentation { abilityId = AbilityId.Block,          title = "Bloqueo",        description = "Levanta la guardia para reducir daño." } },
    };

    // Claves de localización (ui_es.json/ui_en.json): ABILITY_<ID EN MAYÚSCULAS>_TITLE / _DESC.
    // Los valores de arriba (Defaults) se usan solo como fallback en español si LocalizationManager
    // todavía no está listo o falta la clave — mismo patrón ya usado en el resto de la UI de
    // inventario (ver PlayerEquipmentMenuController, clase SpellView.Loc()/EquipmentView).
    static string Loc(string key, string fallback) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;

    /// <summary>
    /// Devuelve la presentación para una habilidad usando primero la lista personalizada proporcionada.
    /// Si no hay coincidencia se usan valores por defecto (localizados) o el id como título.
    /// </summary>
    public static AbilityPresentation Resolve(AbilityId abilityId, IList<AbilityPresentation> custom)
    {
        if (custom != null)
        {
            for (int i = 0; i < custom.Count; i++)
            {
                var entry = custom[i];
                if (entry != null && entry.abilityId == abilityId)
                {
                    return entry;
                }
            }
        }

        if (Defaults.TryGetValue(abilityId, out var preset))
        {
            string keyBase = "ABILITY_" + abilityId.ToString().ToUpperInvariant();
            return new AbilityPresentation
            {
                abilityId = preset.abilityId,
                title = Loc(keyBase + "_TITLE", preset.title),
                description = Loc(keyBase + "_DESC", preset.description),
                icon = preset.icon
            };
        }

        return new AbilityPresentation
        {
            abilityId = abilityId,
            title = abilityId.ToString(),
            description = string.Empty,
            icon = null
        };
    }
}
