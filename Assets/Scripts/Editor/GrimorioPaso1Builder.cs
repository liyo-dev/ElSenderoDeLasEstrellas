using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Grimorio, paso 1 (INC-496): los cinco hechizos que ya se pueden hacer con lo que hay.
/// Meteoro (Will), Ráfaga, Muro de Fuego y Tormenta de Fuego (Estela), Juicio del Pacto (Liam).
/// Crea los assets (copiando Sello del Pacto para las zonas y Tornado para el proyectil), sus
/// prefabs de zona, los mete en la SpellLibrary y pone los combos en las fichas. Idempotente: lo que
/// ya existe no se toca (así no se pisan retoques a mano).
/// Iconos provisionales: reutilizan los de otros hechizos hasta que haya arte propio.
/// </summary>
public static class GrimorioPaso1Builder
{
    private const string SpellFolder = "Assets/_SPELLS";
    private const string PrefabFolder = "Assets/_SPELLS/Prefabs";
    private const string LibraryPath = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";
    private const string ZoneTemplate = "Assets/_SPELLS/SelloDelPacto.asset";
    private const string ProjectileTemplate = "Assets/_SPELLS/Tornado.asset";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Icons = "Assets/Art/UI/Attacks/";

    private struct ZoneSpec
    {
        public string asset, name, vfx, icon;
        public SpellId id; public Slot caster; public MagicElement element; public MagicCastStyle style;
        public ComboButton[] seq;
        public float damagePerTick, radius, duration, mana, range;
    }

    [MenuItem("El Sendero/Archivo/Magia/Grimorio · paso 1: cinco hechizos nuevos (INC-496)")]
    public static void Build()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        var zones = new[]
        {
            new ZoneSpec { asset = "Meteoro", name = "Meteoro", id = SpellId.Meteoro, caster = Slot.Will, element = MagicElement.Light,
                style = MagicCastStyle.Area, seq = new[] { ComboButton.X, ComboButton.A, ComboButton.Y },
                vfx = Hovl + "AoE effects/Meteors AOE.prefab", icon = Icons + "llama_astral.png",
                damagePerTick = 15f, radius = 4f, duration = 3f, mana = 30f, range = 9f },
            new ZoneSpec { asset = "MuroDeFuego", name = "Muro de Fuego", id = SpellId.MuroDeFuego, caster = Slot.Estela, element = MagicElement.Fire,
                style = MagicCastStyle.TwoHanded, seq = new[] { ComboButton.Y, ComboButton.X, ComboButton.X },
                vfx = Best + "FlameEmissionEffect/FlameEmissionEffect.prefab", icon = Icons + "bola_fuego.png",
                damagePerTick = 10f, radius = 3f, duration = 4f, mana = 25f, range = 4f },
            new ZoneSpec { asset = "TormentaDeFuego", name = "Tormenta de Fuego", id = SpellId.TormentaDeFuego, caster = Slot.Estela, element = MagicElement.Fire,
                style = MagicCastStyle.Area, seq = new[] { ComboButton.Y, ComboButton.X, ComboButton.B, ComboButton.A },
                vfx = Best + "FireEffect/FireEffect3.prefab", icon = Icons + "bola_fuego.png",
                damagePerTick = 18f, radius = 6f, duration = 4f, mana = 40f, range = 8f },
            new ZoneSpec { asset = "JuicioDelPacto", name = "Juicio del Pacto", id = SpellId.JuicioDelPacto, caster = Slot.Liam, element = MagicElement.Mind,
                style = MagicCastStyle.Area, seq = new[] { ComboButton.B, ComboButton.X, ComboButton.A, ComboButton.Y },
                vfx = Hovl + "AoE effects/Laser AOE.prefab", icon = Icons + "Sello del pacto.png",
                damagePerTick = 20f, radius = 6f, duration = 3f, mana = 40f, range = 8f },
        };

        var created = new List<MagicSpellSO>();
        foreach (var z in zones)
        {
            var spell = CreateZoneSpell(z, log, warnings);
            if (spell != null) created.Add(spell);
        }
        var rafaga = CreateRafaga(log, warnings);
        if (rafaga != null) created.Add(rafaga);

        // Sello del Pacto es de Liam.
        var sello = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(ZoneTemplate);
        if (sello != null && sello.caster != Slot.Liam) { sello.caster = Slot.Liam; EditorUtility.SetDirty(sello); log.AppendLine("Sello del Pacto: de Liam."); }

        AddToLibrary(created, log, warnings);
        AddCombosToFicha("Assets/_PERSONAJES/Ficha_Estela.asset", new[] { "MuroDeFuego", "TormentaDeFuego" }, log, warnings);
        AddCombosToFicha("Assets/_PERSONAJES/Ficha_Liam.asset", new[] { "SelloDelPacto", "JuicioDelPacto" }, log, warnings);

        AssetDatabase.SaveAssets();
        ComboWiring.ValidateMenu(); // ninguna secuencia puede ser el principio de otra

        var final = new StringBuilder("=== Grimorio · paso 1 (INC-496) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static MagicSpellSO CreateZoneSpell(ZoneSpec z, StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/{z.asset}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;

        var prefab = CreateZonePrefab(z.asset, z.vfx, warnings);
        if (!AssetDatabase.CopyAsset(ZoneTemplate, path)) { warnings.Add($"No he podido copiar {ZoneTemplate} a {path}."); return null; }
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        spell.spellId = z.id;
        spell.displayName = z.name;
        spell.displayNameId = "";
        spell.element = z.element;
        spell.caster = z.caster;
        spell.castStyle = z.style;
        spell.comboSequence = z.seq;
        spell.kind = MagicKind.Zone;
        spell.slotType = SpellSlotType.SpecialOnly;
        spell.damage = z.damagePerTick;
        spell.zoneRadius = z.radius;
        spell.zoneDuration = z.duration;
        spell.zoneRange = z.range;
        spell.manaCost = z.mana;
        if (prefab != null) spell.prefab = prefab;
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(z.icon);
        if (icon != null) spell.attackIcon = icon;
        EditorUtility.SetDirty(spell);
        log.AppendLine($"Creado {z.name} ({z.caster}, {string.Join(" ", z.seq)}, gesto {z.style}).");
        return spell;
    }

    internal static GameObject CreateZonePrefab(string name, string vfxPath, List<string> warnings)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
        if (visual == null) warnings.Add($"No encuentro el efecto {vfxPath}; la zona de {name} se crea sin visual.");

        var root = new GameObject(name);
        root.AddComponent<MagicZoneEffect>();
        if (visual != null)
        {
            var v = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            foreach (var ps in v.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = true; // la zona dura varios segundos; el efecto suelto es de una pasada
            }
        }
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static MagicSpellSO CreateRafaga(StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/Rafaga.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.CopyAsset(ProjectileTemplate, path)) { warnings.Add($"No he podido copiar {ProjectileTemplate}."); return null; }
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        spell.spellId = SpellId.Rafaga;
        spell.displayName = "Ráfaga";
        spell.displayNameId = "";
        spell.element = MagicElement.Storm;
        spell.caster = Slot.Estela;
        spell.castStyle = MagicCastStyle.Hand;
        spell.comboSequence = new ComboButton[0];
        spell.slotType = SpellSlotType.Any;
        spell.damage = 15f;
        spell.knockbackForce = 14f;
        spell.manaCost = 8f;
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>(Hovl + "AoE effects/AoE slash blue.prefab");
        if (impact != null) spell.impactVFX = impact;
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "Huracan.png");
        if (icon != null) spell.attackIcon = icon;
        EditorUtility.SetDirty(spell);
        log.AppendLine("Creada Ráfaga (Estela, básico con empuje).");
        return spell;
    }

    internal static void AddToLibrary(List<MagicSpellSO> spells, StringBuilder log, List<string> warnings)
    {
        var library = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(LibraryPath);
        if (library == null) { warnings.Add($"No encuentro {LibraryPath}."); return; }
        var so = new SerializedObject(library);
        var list = so.FindProperty("spells");
        int added = 0;
        foreach (var s in spells)
        {
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { present = true; break; }
            if (present) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (added > 0) log.AppendLine($"SpellLibrary: {added} hechizo(s) añadidos.");
    }

    internal static void AddCombosToFicha(string fichaPath, string[] spellAssets, StringBuilder log, List<string> warnings)
    {
        var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(fichaPath);
        if (ficha == null) { warnings.Add($"No encuentro {fichaPath}."); return; }
        var so = new SerializedObject(ficha);
        var list = so.FindProperty("combos");
        int added = 0;
        foreach (var a in spellAssets)
        {
            var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"{SpellFolder}/{a}.asset");
            if (s == null) continue;
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { present = true; break; }
            if (present) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (added > 0) log.AppendLine($"{ficha.name}: {added} combo(s).");
    }
}
