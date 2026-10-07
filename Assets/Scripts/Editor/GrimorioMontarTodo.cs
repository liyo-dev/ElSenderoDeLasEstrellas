using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Monta los hechizos y sus interfaces desde una única opción del Editor. En orden:
///
///   1. Grimorio de cada personaje (INC-503): Bola de Fuego de Estela deja de compartir id con
///      Llama Astral; Bola de Fuego, Cycloneburst y Aura Estelar entran en la SpellLibrary.
///   2. Paso 4, apoyo (INC-500): Brisa Sanadora (Estela, Y A A, cura) y Cúpula Estelar (Will,
///      X B A, escudo de grupo).
///   3. Paso 5, zona en el lanzador (INC-501): Nova de Luz (Will, X Y A B, aparta a todos).
///   4. Paso 6, paso corto (INC-502): Paso Sombrío (Liam, B Y B, teletransporte de 6 m).
///   5. Iconos propios de los 16 hechizos nuevos (Assets/Art/UI/Attacks/Grimorio).
///   6. Página del grimorio (INC-503): prefab y dos páginas en el laboratorio de combate
///      (Estrella Fugaz para Will y Brisa Sanadora para Estela).
///   7. Menú de Start: delega los controles, las monedas y el registro de objetos en su builder.
///   8. El grimorio en libro (INC-506): el libro y el botón «Grimorio» de la pestaña Hechizos.
///   9. Comprueba que ninguna secuencia de combo sea el principio de otra.
///
/// Idempotente: lo que ya existe no se toca.
/// </summary>
public static class GrimorioMontarTodo
{
    private const string SpellFolder = "Assets/_SPELLS";
    private const string ZoneTemplate = "Assets/_SPELLS/SelloDelPacto.asset";
    private const string LibraryPath = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";
    private const string FichaEstela = "Assets/_PERSONAJES/Ficha_Estela.asset";
    private const string FichaLiam = "Assets/_PERSONAJES/Ficha_Liam.asset";
    private const string IconFolder = "Assets/Art/UI/Attacks/Grimorio/";
    private const string PagePrefabPath = PaginaDelGrimorioBuilder.PrefabPath;
    private const string CombatLabPath = "Assets/Scenes/Test/CombatLab.unity";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";

    [MenuItem("El Sendero/Magia/Grimorio · montar todo lo que queda (INC-500 a INC-506)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new StringBuilder();
        var warnings = new List<string>();

        Step("1. Grimorio de cada personaje", log, () => GrimorioPorPersonaje(log, warnings));
        Step("2. Paso 4: apoyo", log, () => Apoyo(log, warnings));
        Step("3. Paso 5: Nova de Luz", log, () => Nova(log, warnings));
        Step("4. Paso 6: Paso Sombrío", log, () => PasoSombrio(log, warnings));
        AssetDatabase.SaveAssets();
        Step("5. Iconos", log, () => Iconos(log, warnings));
        AssetDatabase.SaveAssets();
        Step("6. Páginas del grimorio", log, () => Paginas(log, warnings));
        Step("7. Controles en el menú de Start", log, () => MenuDeStartBuilder.Montar(log, warnings));
        Step("8. Grimorio en libro", log, () => GrimorioLibroBuilder.Montar(log, warnings));
        AssetDatabase.SaveAssets();
        Step("9. VFX propios (ninguno repetido)", log, () => VfxDeHechizos.Aplicar(log, warnings));
        AssetDatabase.SaveAssets();
        ComboWiring.ValidateMenu();

        var final = new StringBuilder("=== Grimorio · montar todo (INC-500 a INC-506) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static void Step(string name, StringBuilder log, System.Action action)
    {
        log.AppendLine($"— {name}");
        try { action(); }
        catch (System.Exception e)
        {
            log.AppendLine($"   ERROR: {e.Message}");
            Debug.LogException(e);
        }
    }

    // ── 1. Grimorio de cada personaje ─────────────────────────────────────

    private static void GrimorioPorPersonaje(StringBuilder log, List<string> warnings)
    {
        var bola = Spell("BolaFuego");
        if (bola != null && bola.spellId == SpellId.Fireball)
        {
            bola.spellId = SpellId.BolaDeFuegoEstela;
            EditorUtility.SetDirty(bola);
            log.AppendLine("   Bola de Fuego (Estela): id propio (antes compartía Fireball con Llama Astral).");
        }
        var list = new List<MagicSpellSO>();
        foreach (var a in new[] { "BolaFuego", "Tornado", "AuraEstelar" })
        {
            var s = Spell(a);
            if (s != null) list.Add(s); else warnings.Add($"No encuentro {a}.");
        }
        GrimorioPaso1Builder.AddToLibrary(list, log, warnings);
    }

    // ── 2. Apoyo ──────────────────────────────────────────────────────────

    private static void Apoyo(StringBuilder log, List<string> warnings)
    {
        var brisa = CreateZone("BrisaSanadora", Hovl + "Magic circles/Healing circle.prefab", z =>
        {
            z.spellId = SpellId.BrisaSanadora; z.displayName = "Brisa Sanadora";
            z.element = MagicElement.Storm; z.caster = Slot.Estela;
            z.castStyle = MagicCastStyle.Area;
            z.comboSequence = new[] { ComboButton.Y, ComboButton.A, ComboButton.A };
            z.zoneOnCaster = true;
            z.damage = 0f; z.healPerTick = 8f; z.zoneTickInterval = 0.5f; z.zoneDuration = 4f; z.zoneRadius = 5f;
            z.manaCost = 30f; z.teamGaugeGain = 0.34f;
        }, log, warnings);

        var cupula = CreateZone("CupulaEstelar", Hovl + "Character auras/Star aura.prefab", z =>
        {
            z.spellId = SpellId.CupulaEstelar; z.displayName = "Cúpula Estelar";
            z.element = MagicElement.Light; z.caster = Slot.Will;
            z.castStyle = MagicCastStyle.Area;
            z.comboSequence = new[] { ComboButton.X, ComboButton.B, ComboButton.A };
            z.zoneOnCaster = true;
            z.damage = 0f; z.zoneTickInterval = 1.5f; z.zoneDuration = 1.2f; z.zoneRadius = 7f;
            z.groupShieldSeconds = 6f; z.groupShieldDamageFactor = 0.3f;
            z.groupShieldVFX = Load<GameObject>(Hovl + "Magic shields/Magic shield yellow.prefab", warnings);
            z.manaCost = 30f; z.teamGaugeGain = 0.34f;
        }, log, warnings);

        GrimorioPaso1Builder.AddToLibrary(new List<MagicSpellSO> { brisa, cupula }.Where(s => s != null).ToList(), log, warnings);
        // Brisa Sanadora no va en la ficha de Estela: se aprende con una página del grimorio (ver paso 6).
    }

    // ── 3. Nova de Luz ────────────────────────────────────────────────────

    private static void Nova(StringBuilder log, List<string> warnings)
    {
        var nova = CreateZone("NovaDeLuz", Best + "HolyEffect/HolyEffect.prefab", z =>
        {
            z.spellId = SpellId.NovaDeLuz; z.displayName = "Nova de Luz";
            z.element = MagicElement.Light; z.caster = Slot.Will;
            z.castStyle = MagicCastStyle.Omni;
            z.comboSequence = new[] { ComboButton.X, ComboButton.Y, ComboButton.A, ComboButton.B };
            z.zoneOnCaster = true;
            z.damage = 30f; z.zoneTickInterval = 1.5f; z.zoneDuration = 1.2f; z.zoneRadius = 5f;
            z.statusEffect = EstadoDeCombate.Empujar; z.statusDuration = 0.35f; z.statusStrength = 12f;
            z.manaCost = 30f;
        }, log, warnings);
        if (nova != null) GrimorioPaso1Builder.AddToLibrary(new List<MagicSpellSO> { nova }, log, warnings);
    }

    // ── 4. Paso Sombrío ───────────────────────────────────────────────────

    private static void PasoSombrio(StringBuilder log, List<string> warnings)
    {
        string path = $"{SpellFolder}/PasoSombrio.asset";
        var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (s == null)
        {
            const string template = SpellFolder + "/GarraDelPacto.asset";
            if (!AssetDatabase.CopyAsset(template, path)) { warnings.Add($"No he podido copiar {template}."); return; }
            s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
            var tp = Load<GameObject>(Hovl + "Environment/Teleport.prefab", warnings);
            s.spellId = SpellId.PasoSombrio; s.displayName = "Paso Sombrío"; s.displayNameId = "";
            s.element = MagicElement.Mind; s.caster = Slot.Liam;
            s.kind = MagicKind.Teleport; s.slotType = SpellSlotType.SpecialOnly;
            s.castStyle = MagicCastStyle.Call;
            s.comboSequence = new[] { ComboButton.B, ComboButton.Y, ComboButton.B };
            s.damage = 0f; s.knockbackForce = 0f; s.manaCost = 20f; s.teleportDistance = 6f;
            s.bounceCount = 0; s.pierceCount = 0; s.spreadCount = 1; s.impactZone = null;
            s.statusEffect = EstadoDeCombate.Ninguno;
            if (tp != null) { s.prefab = tp; s.spawnVFX = tp; }
            s.vfxLifetime = 2f;
            EditorUtility.SetDirty(s);
            log.AppendLine("   Creado Paso Sombrío (Liam, B Y B, invocación, teletransporte de 6 m).");
        }
        GrimorioPaso1Builder.AddToLibrary(new List<MagicSpellSO> { s }, log, warnings);
        GrimorioPaso1Builder.AddCombosToFicha(FichaLiam, new[] { "PasoSombrio" }, log, warnings);
    }

    // ── 5. Iconos ─────────────────────────────────────────────────────────

    private static readonly (string asset, string icon)[] IconMap =
    {
        ("EstrellaFugaz", "estrella_fugaz"), ("LluviaDeChispas", "lluvia_de_chispas"), ("Meteoro", "meteoro"),
        ("CupulaEstelar", "cupula_estelar"), ("NovaDeLuz", "nova_de_luz"), ("Rafaga", "rafaga"),
        ("ChispaIgnea", "chispa_ignea"), ("MuroDeFuego", "muro_de_fuego"), ("TormentaDeFuego", "tormenta_de_fuego"),
        ("Remolino", "remolino"), ("BrisaSanadora", "brisa_sanadora"), ("DardoMental", "dardo_mental"),
        ("Eco", "eco"), ("CadenasDelPacto", "cadenas_del_pacto"), ("PasoSombrio", "paso_sombrio"),
        ("JuicioDelPacto", "juicio_del_pacto"),
    };

    private static void Iconos(StringBuilder log, List<string> warnings)
    {
        int n = 0;
        foreach (var (asset, icon) in IconMap)
        {
            string iconPath = IconFolder + icon + ".png";
            if (AssetImporter.GetAtPath(iconPath) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 512;
                ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            var spell = Spell(asset);
            if (sprite == null) { warnings.Add($"No encuentro el icono {iconPath}."); continue; }
            if (spell == null) { warnings.Add($"No encuentro el hechizo {asset} para su icono."); continue; }
            if (spell.attackIcon == sprite) continue;
            spell.attackIcon = sprite;
            EditorUtility.SetDirty(spell);
            n++;
        }
        log.AppendLine($"   {n} icono(s) asignados.");
    }

    // ── 6. Páginas del grimorio ───────────────────────────────────────────

    private static void Paginas(StringBuilder log, List<string> warnings)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PagePrefabPath);
        if (prefab == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PagePrefabPath));
            var root = new GameObject("PaginaDelGrimorio");
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1.2f; col.center = new Vector3(0f, 1f, 0f);
            root.AddComponent<PaginaDelGrimorio>();
            PaginaDelGrimorioBuilder.Montar(root, warnings);

            prefab = PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            Object.DestroyImmediate(root);
            log.AppendLine($"   Creado el prefab {PagePrefabPath}.");
        }

        // Dos páginas de prueba en el laboratorio de combate.
        var scene = SceneManager.GetSceneByPath(CombatLabPath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            if (!File.Exists(CombatLabPath)) { warnings.Add("No encuentro el laboratorio de combate."); return; }
            scene = EditorSceneManager.OpenScene(CombatLabPath, OpenSceneMode.Additive);
            opened = true;
        }

        bool changed = false;
        changed |= PlacePage(scene, prefab, SpellId.EstrellaFugaz, "LAB_PAGINA_ESTRELLA_FUGAZ", new Vector3(-3f, 0f, -5f), log);
        changed |= PlacePage(scene, prefab, SpellId.BrisaSanadora, "LAB_PAGINA_BRISA_SANADORA", new Vector3(3f, 0f, -5f), log);
        if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool PlacePage(Scene scene, GameObject prefab, SpellId spell, string id, Vector3 pos, StringBuilder log)
    {
        foreach (var go in scene.GetRootGameObjects())
            foreach (var p in go.GetComponentsInChildren<PaginaDelGrimorio>(true))
                if (p.idUnico == id) return false;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        inst.name = $"Página del grimorio — {spell}";
        inst.transform.position = pos;
        var page = inst.GetComponent<PaginaDelGrimorio>();
        page.hechizo = spell;
        page.idUnico = id;
        EditorUtility.SetDirty(page);
        log.AppendLine($"   Página de {spell} en el laboratorio ({pos.x:0}, {pos.z:0}).");
        return true;
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    private static MagicSpellSO Spell(string asset) => AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"{SpellFolder}/{asset}.asset");

    private static T Load<T>(string path, List<string> warnings) where T : Object
    {
        var o = AssetDatabase.LoadAssetAtPath<T>(path);
        if (o == null) warnings.Add($"No encuentro {path}.");
        return o;
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
        z.statusEffect = EstadoDeCombate.Ninguno;
        z.healPerTick = 0f; z.groupShieldSeconds = 0f; z.teamGaugeGain = 0f; z.zoneOnCaster = false;
        setup(z);
        if (prefab != null) z.prefab = prefab;
        EditorUtility.SetDirty(z);
        log.AppendLine($"   Creado {z.displayName} ({z.caster}, {string.Join(" ", z.comboSequence)}, gesto {z.castStyle}).");
        return z;
    }
}
