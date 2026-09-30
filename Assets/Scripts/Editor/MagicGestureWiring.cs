using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Gestos de hechizo y círculo del combo (INC-495). Idempotente.
/// <list type="bullet">
/// <item>ComboIdle (UpperBody/Magic) pasa a HumanM@MagicAttackOmni01 - Load: brazos abiertos cargando
/// (la de Casting parecía que hablaba).</item>
/// <item>_WILL.prefab: ComboCastController con la pose ComboIdle y el círculo mágico a los pies;
/// MagicCaster con el gesto de área Cheer01 (brazo arriba). Ver INC-521.</item>
/// <item>Gesto por hechizo: Sello del Pacto y Grieta = área (saltito y brazo arriba), Corazón Estelar = omni.</item>
/// </list>
/// </summary>
public static class MagicGestureWiring
{
    private const string ControllerPath = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string LoadClipPath = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Omnidirectional/HumanM@MagicAttackOmni01 - Load.fbx";
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string AreaGestureState = "UpperBody.Cheer01";
    private const string CircleVfxPath = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab";

    [MenuItem("El Sendero/Combate/Gestos de hechizo y círculo del combo (INC-495)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        bool warn = false;

        // Animator
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAllAssetsAtPath(LoadClipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        var layer = ctrl != null ? ctrl.layers.FirstOrDefault(l => l.name == "UpperBody") : null;
        var magic = layer?.stateMachine.stateMachines.Select(c => c.stateMachine).FirstOrDefault(m => m.name == "Magic");
        var idle = magic?.states.Select(s => s.state).FirstOrDefault(s => s.name == "ComboIdle");
        if (idle == null || clip == null) { log.AppendLine("AVISO: no encuentro ComboIdle o el clip Omni01 - Load (¿se montó INC-494?)."); warn = true; }
        else if (idle.motion != clip)
        {
            idle.motion = clip;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.AppendLine("Animator: ComboIdle = HumanM@MagicAttackOmni01 - Load.");
        }

        // Will
        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var combo = root.GetComponentInChildren<ComboCastController>(true);
            if (combo == null) { log.AppendLine("AVISO: _WILL.prefab no tiene ComboCastController."); warn = true; }
            else
            {
                var so = new SerializedObject(combo);
                so.FindProperty("enterState").stringValue = "UpperBody.Magic.ComboIdle";
                var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(CircleVfxPath);
                if (vfx != null) so.FindProperty("circleVfx").objectReferenceValue = vfx;
                else { log.AppendLine($"AVISO: no encuentro {CircleVfxPath}."); warn = true; }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
                log.AppendLine("Will: pose del combo = ComboIdle y círculo mágico a los pies.");
            }

            var caster = root.GetComponentInChildren<MagicCaster>(true);
            if (caster == null) { log.AppendLine("AVISO: _WILL.prefab no tiene MagicCaster."); warn = true; }
            else
            {
                var so = new SerializedObject(caster);
                var area = so.FindProperty("areaState");
                if (area.stringValue != AreaGestureState)
                {
                    area.stringValue = AreaGestureState;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
                    log.AppendLine($"Will: gesto de área = {AreaGestureState}.");
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Gestos por hechizo
        SetStyle("Assets/_SPELLS/SelloDelPacto.asset", MagicCastStyle.Area, log);
        SetStyle("Assets/_SPELLS/MagoOscuroGrieta.asset", MagicCastStyle.Area, log);
        SetStyle("Assets/_SPELLS/CorazonEstelar.asset", MagicCastStyle.Omni, log);
        AssetDatabase.SaveAssets();

        string text = "=== Gestos de hechizo y círculo del combo (INC-495) ===\n" + log + (warn ? "" : "Sin avisos.\n");
        if (warn) Debug.LogWarning(text); else Debug.Log(text);
    }

    private static void SetStyle(string path, MagicCastStyle style, StringBuilder log)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(path);
        if (spell == null || spell.castStyle == style) return;
        spell.castStyle = style;
        EditorUtility.SetDirty(spell);
        log.AppendLine($"{spell.name}: gesto {style}.");
    }
}
