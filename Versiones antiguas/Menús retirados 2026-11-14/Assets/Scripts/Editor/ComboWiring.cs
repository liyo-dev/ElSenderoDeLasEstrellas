using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sendero.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta el combo mágico de la Y (INC-494). Idempotente.
/// <list type="bullet">
/// <item>Animator de Will (Invector@BasicLocomotion): estados ComboEnter, ComboIdle (en bucle),
/// ComboExit y ComboBreak en UpperBody/Magic con las animaciones Casting* de Kevin Iglesias.</item>
/// <item>Secuencias iniciales: Corazón Estelar = X Y B; Sello del Pacto = B A X (solo si están vacías).</item>
/// <item>_WILL.prefab: ComboCastController junto a MagicCaster.</item>
/// <item>Start.unity: ComboPanel (ComboPanelUI) encima de la cruz de combate.</item>
/// </list>
/// Incluye la comprobación de que ninguna secuencia es el principio de otra.
/// </summary>
public static class ComboWiring
{
    private const string ControllerPath = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string ClipFolder = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/";
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string ScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string SegmentBgPath = "Assets/Art/UI/HUD/segmento_equipo_fondo.png";
    private const string SegmentFillPath = "Assets/Art/UI/HUD/segmento_equipo_relleno.png";

    [MenuItem("El Sendero/Archivo/Combate/Montar combo mágico de la Y (INC-494)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        WireAnimator(log, warnings);
        SetDefaultSequences(log, warnings);
        WireWill(log, warnings);
        WireHud(log, warnings);
        Validate(log, warnings);

        Report("Combo mágico (INC-494)", log, warnings);
    }

    [MenuItem("El Sendero/Combate/Comprobar secuencias de combo")]
    public static void ValidateMenu()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();
        Validate(log, warnings);
        Report("Secuencias de combo", log, warnings);
    }

    // ── Animator ────────────────────────────────────────────────────────────────────

    private static void WireAnimator(StringBuilder log, List<string> warnings)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { warnings.Add($"No encuentro {ControllerPath}."); return; }

        var layer = ctrl.layers.FirstOrDefault(l => l.name == "UpperBody");
        if (layer == null) { warnings.Add("El Animator no tiene capa UpperBody."); return; }
        var magic = layer.stateMachine.stateMachines.Select(c => c.stateMachine).FirstOrDefault(m => m.name == "Magic");
        if (magic == null) { warnings.Add("UpperBody no tiene la submáquina Magic."); return; }

        var enterClip = LoadClip("HumanM@CastingEnter01.fbx", warnings);
        var idleClip = LoadClip("HumanM@CastingIdle01.fbx", warnings);
        var exitClip = LoadClip("HumanM@CastingExit01.fbx", warnings);
        var breakClip = LoadClip("HumanM@CastingDamage01.fbx", warnings);
        if (!enterClip || !idleClip || !exitClip || !breakClip) return;

        EnsureLoop(ClipFolder + "HumanM@CastingIdle01.fbx", log);

        var enter = EnsureState(magic, "ComboEnter", enterClip, new Vector3(600, 0), log);
        var idle = EnsureState(magic, "ComboIdle", idleClip, new Vector3(600, 70), log);
        var exit = EnsureState(magic, "ComboExit", exitClip, new Vector3(600, 140), log);
        var brk = EnsureState(magic, "ComboBreak", breakClip, new Vector3(600, 210), log);

        if (enter.transitions.Length == 0)
        {
            var t = enter.AddTransition(idle);
            t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.1f;
        }
        if (exit.transitions.Length == 0)
        {
            var t = exit.AddExitTransition();
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
        }
        if (brk.transitions.Length == 0)
        {
            var t = brk.AddExitTransition();
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    private static AnimationClip LoadClip(string file, List<string> warnings)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(ClipFolder + file).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) warnings.Add($"No encuentro la animación en {file}.");
        return clip;
    }

    private static void EnsureLoop(string fbxPath, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = false;
        foreach (var c in clips)
            if (!c.loopTime) { c.loopTime = true; changed = true; }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        log.AppendLine("CastingIdle01: activado el bucle.");
    }

    private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, Motion motion, Vector3 pos, StringBuilder log)
    {
        var existing = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        if (existing != null) return existing;
        var state = sm.AddState(name, pos);
        state.motion = motion;
        state.writeDefaultValues = true;
        log.AppendLine($"Animator: estado UpperBody.Magic.{name}.");
        return state;
    }

    // ── Secuencias ──────────────────────────────────────────────────────────────────

    private static void SetDefaultSequences(StringBuilder log, List<string> warnings)
    {
        SetSequence("Assets/_SPELLS/CorazonEstelar.asset", new[] { ComboButton.X, ComboButton.Y, ComboButton.B }, log, warnings);
        SetSequence("Assets/_SPELLS/SelloDelPacto.asset", new[] { ComboButton.B, ComboButton.A, ComboButton.X }, log, warnings);
        AssetDatabase.SaveAssets();
    }

    private static void SetSequence(string path, ComboButton[] seq, StringBuilder log, List<string> warnings)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (spell == null) { warnings.Add($"No encuentro {path}."); return; }
        if (spell.HasCombo) return;
        spell.comboSequence = seq;
        EditorUtility.SetDirty(spell);
        log.AppendLine($"{spell.name}: secuencia {string.Join(" ", seq)}.");
    }

    private static void Validate(StringBuilder log, List<string> warnings)
    {
        var spells = AssetDatabase.FindAssets("t:MagicSpellSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null && s.HasCombo).ToList();
        int problems = 0;
        for (int i = 0; i < spells.Count; i++)
            for (int j = 0; j < spells.Count; j++)
            {
                if (i == j) continue;
                var a = spells[i].comboSequence; var b = spells[j].comboSequence;
                if (a.Length > b.Length) continue;
                bool prefix = true;
                for (int k = 0; k < a.Length; k++) if (a[k] != b[k]) { prefix = false; break; }
                if (!prefix) continue;
                if (a.Length == b.Length && i > j) continue; // iguales: se avisa una vez
                warnings.Add(a.Length == b.Length
                    ? $"{spells[i].name} y {spells[j].name} tienen la misma secuencia ({string.Join(" ", a)})."
                    : $"La secuencia de {spells[i].name} ({string.Join(" ", a)}) es el principio de la de {spells[j].name} ({string.Join(" ", b)}): {spells[j].name} no saldría nunca.");
                problems++;
            }
        log.AppendLine($"Secuencias comprobadas: {spells.Count} combos, {problems} problema(s).");
    }

    // ── Will ────────────────────────────────────────────────────────────────────────

    private static void WireWill(StringBuilder log, List<string> warnings)
    {
        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var caster = root.GetComponentInChildren<MagicCaster>(true);
            if (caster == null) { warnings.Add("_WILL.prefab no tiene MagicCaster."); return; }
            if (caster.GetComponent<ComboCastController>() != null) return;
            caster.gameObject.AddComponent<ComboCastController>();
            PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
            log.AppendLine("Will: añadido ComboCastController.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── HUD ─────────────────────────────────────────────────────────────────────────

    private static void WireHud(StringBuilder log, List<string> warnings)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        try
        {
            Transform box = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                box = FindDeep(r.transform, "CombatButtons");
                if (box != null) break;
            }
            if (box == null) { warnings.Add("No encuentro CombatButtons en Start.unity (monta antes el HUD de combate)."); return; }
            if (box.Find("ComboPanel") != null) return;

            var go = new GameObject("ComboPanel", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(box, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(900f, 300f);
            rt.anchoredPosition = new Vector2(560f, 960f);

            var panel = go.AddComponent<ComboPanelUI>();
            var so = new SerializedObject(panel);
            so.FindProperty("rowBackground").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentBgPath);
            so.FindProperty("timerFill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentFillPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("HUD: creado ComboPanel encima de la cruz de combate. Start.unity guardada.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var t = FindDeep(child, name);
            if (t != null) return t;
        }
        return null;
    }

    private static void Report(string title, StringBuilder log, List<string> warnings)
    {
        var final = new StringBuilder($"=== {title} ===\n");
        final.Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }
}
