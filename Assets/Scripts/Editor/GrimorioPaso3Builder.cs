using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Grimorio, paso 3 (INC-499): estados. Crea los tres hechizos que usan EstadosDeCombate:
/// Dardo Mental (Liam, básico que ralentiza; es su 4.º básico), Cadenas del Pacto (Liam, combo
/// B X Y, zona que inmoviliza) y Remolino (Estela, combo Y B X, zona que atrae al centro).
/// Idempotente: lo que ya existe no se toca. Iconos provisionales.
/// </summary>
public static class GrimorioPaso3Builder
{
    private const string SpellFolder = "Assets/_SPELLS";
    private const string ZoneTemplate = "Assets/_SPELLS/SelloDelPacto.asset";
    private const string FichaEstela = "Assets/_PERSONAJES/Ficha_Estela.asset";
    private const string FichaLiam = "Assets/_PERSONAJES/Ficha_Liam.asset";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Icons = "Assets/Art/UI/Attacks/";

    [MenuItem("El Sendero/Archivo/Magia/Grimorio · paso 3: estados (INC-499)")]
    public static void Build()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();
        var created = new List<MagicSpellSO>();

        var dardo = CreateDardoMental(log, warnings);
        if (dardo != null) created.Add(dardo);

        var cadenas = CreateZone("CadenasDelPacto", Best + "DarkEffect/DarkEffect1.prefab", z =>
        {
            z.spellId = SpellId.CadenasDelPacto; z.displayName = "Cadenas del Pacto";
            z.element = MagicElement.Mind; z.caster = Slot.Liam;
            z.castStyle = MagicCastStyle.Call;
            z.comboSequence = new[] { ComboButton.B, ComboButton.X, ComboButton.Y };
            z.damage = 4f; z.zoneRadius = 4f; z.zoneDuration = 3f; z.zoneRange = 8f; z.manaCost = 30f;
            z.statusEffect = EstadoDeCombate.Inmovilizar; z.statusDuration = 0.6f; z.statusStrength = 0f;
            z.statusVFX = Load(Hovl + "Character auras/Debuff.prefab", warnings);
            SetIcon(z, Icons + "Sello del pacto.png");
        }, log, warnings);
        if (cadenas != null) created.Add(cadenas);

        var remolino = CreateZone("Remolino", Hovl + "AoE effects/AoE slash green.prefab", z =>
        {
            z.spellId = SpellId.Remolino; z.displayName = "Remolino";
            z.element = MagicElement.Storm; z.caster = Slot.Estela;
            z.castStyle = MagicCastStyle.Omni;
            z.comboSequence = new[] { ComboButton.Y, ComboButton.B, ComboButton.X };
            z.damage = 6f; z.zoneRadius = 6f; z.zoneDuration = 2.5f; z.zoneRange = 7f; z.manaCost = 30f;
            z.statusEffect = EstadoDeCombate.Atraer; z.statusDuration = 0.6f; z.statusStrength = 5f;
            z.statusVFX = null;
            SetIcon(z, Icons + "tornado.png");
        }, log, warnings);
        if (remolino != null) created.Add(remolino);

        GrimorioPaso1Builder.AddToLibrary(created, log, warnings);
        GrimorioPaso1Builder.AddCombosToFicha(FichaLiam, new[] { "CadenasDelPacto" }, log, warnings);
        GrimorioPaso1Builder.AddCombosToFicha(FichaEstela, new[] { "Remolino" }, log, warnings);
        AddBasicToFicha(FichaLiam, dardo, log, warnings);

        AssetDatabase.SaveAssets();
        ComboWiring.ValidateMenu(); // ninguna secuencia puede ser el principio de otra

        var final = new StringBuilder("=== Grimorio · paso 3 (INC-499) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static MagicSpellSO CreateDardoMental(StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/DardoMental.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;

        const string template = SpellFolder + "/GarraDelPacto.asset";
        if (!AssetDatabase.CopyAsset(template, path)) { warnings.Add($"No he podido copiar {template}."); return null; }
        var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        s.spellId = SpellId.DardoMental;
        s.displayName = "Dardo Mental";
        s.displayNameId = "";
        s.element = MagicElement.Mind;
        s.caster = Slot.Liam;
        s.kind = MagicKind.Projectile;
        s.slotType = SpellSlotType.Any;
        s.castStyle = MagicCastStyle.Hand;
        s.comboSequence = new ComboButton[0];
        s.bounceCount = 0; s.pierceCount = 0; s.spreadCount = 1; s.impactZone = null;
        s.damage = 12f; s.manaCost = 7f; s.initialSpeed = 20f; s.knockbackForce = 0f;
        s.statusEffect = EstadoDeCombate.Ralentizar; s.statusDuration = 2f; s.statusStrength = 0.4f;
        s.statusVFX = Load(Hovl + "Character auras/Plexus.prefab", warnings);
        var impact = Load(Hovl + "AoE effects/Plexus AoE.prefab", warnings);
        if (impact != null) s.impactVFX = impact;
        SetIcon(s, Icons + "aura_estelar.png");
        EditorUtility.SetDirty(s);
        log.AppendLine("Creado Dardo Mental (Liam, básico: ralentiza al 40 % durante 2 s).");
        return s;
    }

    private static MagicSpellSO CreateZone(string asset, string vfxPath, System.Action<MagicSpellSO> setup,
                                           StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/{asset}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;

        var prefab = GrimorioPaso1Builder.CreateZonePrefab(asset, vfxPath, warnings);
        if (!AssetDatabase.CopyAsset(ZoneTemplate, path)) { warnings.Add($"No he podido copiar {ZoneTemplate}."); return null; }
        var z = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        z.displayNameId = "";
        z.kind = MagicKind.Zone;
        z.slotType = SpellSlotType.SpecialOnly;
        z.knockbackForce = 0f;
        setup(z);
        if (prefab != null) z.prefab = prefab;
        EditorUtility.SetDirty(z);
        log.AppendLine($"Creado {z.displayName} ({z.caster}, {string.Join(" ", z.comboSequence)}, gesto {z.castStyle}, {z.statusEffect}).");
        return z;
    }

    private static void AddBasicToFicha(string fichaPath, MagicSpellSO spell, StringBuilder log, List<string> warnings)
    {
        if (spell == null) return;
        var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(fichaPath);
        if (ficha == null) { warnings.Add($"No encuentro {fichaPath}."); return; }
        var so = new SerializedObject(ficha);
        var list = so.FindProperty("basicos");
        if (list == null) { warnings.Add($"{ficha.name} no tiene el campo 'basicos'."); return; }
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == spell) return;
        if (list.arraySize >= MagicCaster.BasicSlotCount) { warnings.Add($"{ficha.name} ya tiene 4 básicos; {spell.displayName} no cabe."); return; }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = spell;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"{ficha.name}: {spell.displayName} como básico n.º {list.arraySize}.");
    }

    private static GameObject Load(string path, List<string> warnings)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) warnings.Add($"No encuentro {path}.");
        return go;
    }

    private static void SetIcon(MagicSpellSO s, string iconPath)
    {
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (icon != null) s.attackIcon = icon;
    }
}
