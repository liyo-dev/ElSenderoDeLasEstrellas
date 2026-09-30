using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Grimorio, paso 2 (INC-497): proyectil mejorado. Crea los cuatro básicos que usan las opciones
/// nuevas de MagicSpellSO (rebote, abanico, zona al impactar y perforar):
/// Estrella Fugaz y Lluvia de Chispas (Will), Chispa Ígnea (Estela) y Eco (Liam).
/// Copia un básico parecido y le cambia los números. Idempotente: lo que ya existe no se toca.
/// Iconos provisionales hasta que haya arte propio.
/// </summary>
public static class GrimorioPaso2Builder
{
    private const string SpellFolder = "Assets/_SPELLS";
    private const string ZoneTemplate = "Assets/_SPELLS/SelloDelPacto.asset";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Icons = "Assets/Art/UI/Attacks/";

    [MenuItem("El Sendero/Archivo/Magia/Grimorio · paso 2: proyectil mejorado (INC-497)")]
    public static void Build()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();
        var created = new List<MagicSpellSO>();

        // Estrella Fugaz: rebota hasta 3 enemigos cercanos.
        var estrella = CreateBasic("EstrellaFugaz", "AuraEstelar", s =>
        {
            s.spellId = SpellId.EstrellaFugaz; s.displayName = "Estrella Fugaz";
            s.element = MagicElement.Light; s.caster = Slot.Will;
            s.damage = 14f; s.manaCost = 8f; s.initialSpeed = 16f;
            s.bounceCount = 3; s.bounceRange = 8f;
            SetImpact(s, Hovl + "Hits and explosions/Star hit.prefab", warnings);
            SetIcon(s, Icons + "aura_estelar.png");
        }, log, warnings);
        if (estrella != null) created.Add(estrella);

        // Lluvia de Chispas: abanico de 3.
        var chispas = CreateBasic("LluviaDeChispas", "BolaPrisma", s =>
        {
            s.spellId = SpellId.LluviaDeChispas; s.displayName = "Lluvia de Chispas";
            s.element = MagicElement.Light; s.caster = Slot.Will;
            s.damage = 9f; s.manaCost = 9f; s.initialSpeed = 18f;
            s.spreadCount = 3; s.spreadAngle = 30f;
            SetImpact(s, Hovl + "Sparks/Sparks explode yellow.prefab", warnings);
            SetIcon(s, Icons + "bola_prisma.png");
        }, log, warnings);
        if (chispas != null) created.Add(chispas);

        // Chispa Ígnea: deja fuego en el suelo 2 s al impactar.
        var fuego = CreateImpactZone(log, warnings);
        var ignea = CreateBasic("ChispaIgnea", "BolaFuego", s =>
        {
            s.spellId = SpellId.ChispaIgnea; s.displayName = "Chispa Ígnea";
            s.element = MagicElement.Fire; s.caster = Slot.Estela;
            s.damage = 10f; s.manaCost = 10f;
            s.impactZone = fuego;
            SetIcon(s, Icons + "bola_fuego.png");
        }, log, warnings);
        if (ignea != null) created.Add(ignea);

        // Eco: atraviesa a todos los enemigos en línea. Rápido y preciso.
        var eco = CreateBasic("Eco", "GarraDelPacto", s =>
        {
            s.spellId = SpellId.Eco; s.displayName = "Eco";
            s.element = MagicElement.Mind; s.caster = Slot.Liam;
            s.damage = 20f; s.manaCost = 12f; s.initialSpeed = 24f;
            s.pierceCount = -1;
            SetImpact(s, Hovl + "Hits and explosions/Electro hit.prefab", warnings);
            SetIcon(s, Icons + "Garra del pacto.png");
        }, log, warnings);
        if (eco != null) created.Add(eco);

        GrimorioPaso1Builder.AddToLibrary(created, log, warnings);
        AssetDatabase.SaveAssets();

        var final = new StringBuilder("=== Grimorio · paso 2 (INC-497) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static MagicSpellSO CreateBasic(string asset, string template, System.Action<MagicSpellSO> setup,
                                            StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/{asset}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;

        string templatePath = $"{SpellFolder}/{template}.asset";
        if (!AssetDatabase.CopyAsset(templatePath, path)) { warnings.Add($"No he podido copiar {templatePath} a {path}."); return null; }
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        spell.displayNameId = "";
        spell.kind = MagicKind.Projectile;
        spell.slotType = SpellSlotType.Any;
        spell.castStyle = MagicCastStyle.Hand;
        spell.comboSequence = new ComboButton[0];
        spell.bounceCount = 0; spell.pierceCount = 0; spell.spreadCount = 1; spell.impactZone = null;
        spell.destroyOnHit = true;
        setup(spell);
        EditorUtility.SetDirty(spell);
        log.AppendLine($"Creado {spell.displayName} ({spell.caster}, copia de {template}).");
        return spell;
    }

    private static MagicSpellSO CreateImpactZone(StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/ChispaIgneaFuego.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;

        var prefab = GrimorioPaso1Builder.CreateZonePrefab("ChispaIgneaFuego", Best + "FireEffect/FireEffect1.prefab", warnings);
        if (!AssetDatabase.CopyAsset(ZoneTemplate, path)) { warnings.Add($"No he podido copiar {ZoneTemplate}."); return null; }
        var zone = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        zone.spellId = SpellId.ChispaIgneaFuego;
        zone.displayName = "Chispa Ígnea (fuego en el suelo)";
        zone.displayNameId = "";
        zone.element = MagicElement.Fire;
        zone.caster = Slot.Estela;
        zone.kind = MagicKind.Zone;
        zone.slotType = SpellSlotType.SpecialOnly;
        zone.castStyle = MagicCastStyle.Hand;
        zone.comboSequence = new ComboButton[0];   // la plantilla trae la secuencia de Sello del Pacto
        zone.damage = 6f;
        zone.zoneTickInterval = 0.5f;
        zone.zoneRadius = 1.8f;
        zone.zoneDuration = 2f;
        zone.knockbackForce = 0f;
        zone.manaCost = 0f;
        if (prefab != null) zone.prefab = prefab;
        EditorUtility.SetDirty(zone);
        log.AppendLine("Creada la zona de fuego de Chispa Ígnea (6 por tick, radio 1,8, 2 s). No va en la SpellLibrary.");
        return zone;
    }

    private static void SetImpact(MagicSpellSO s, string vfxPath, List<string> warnings)
    {
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
        if (vfx != null) s.impactVFX = vfx;
        else warnings.Add($"No encuentro {vfxPath}; {s.displayName} se queda con el impacto de la plantilla.");
    }

    private static void SetIcon(MagicSpellSO s, string iconPath)
    {
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (icon != null) s.attackIcon = icon;
    }
}
