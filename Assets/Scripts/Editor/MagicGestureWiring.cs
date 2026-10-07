using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Gestos de hechizo y presentación del combo (INC-495, INC-643). Idempotente.
/// <list type="bullet">
/// <item>ComboIdle (UpperBody/Magic) = Idle_Battle_NoWeapon de RPG Tiny Hero Duo (respira y se mece);
/// los brazos los coloca ManosIK sosteniendo el orbe, así que encajan con cualquier proporción.
/// La capa UpperBody lleva «IK Pass». Se retiran las poses congeladas ComboPose1…5. Ver INC-643.</item>
/// <item>_WILL.prefab: ComboCastController con la pose ComboIdle; ElevacionVisual, ManosIK y
/// PresentacionDelCombo (círculo, orbe, chispas, polvo) con los materiales de Hovl; MagicCaster con el
/// gesto de área Cheer01 (brazo arriba). Ver INC-521 e INC-643.</item>
/// <item>Gesto por hechizo: Sello del Pacto y Grieta = área (saltito y brazo arriba), Corazón Estelar = omni.</item>
/// </list>
/// </summary>
public static class MagicGestureWiring
{
    private const string ControllerPath = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string TinyHero = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/";

    private const string ComboIdleClip = TinyHero + "NoWeapon/Idle_Battle_NoWeapon.fbx";
    private static readonly string[] PosesRetiradas = { "ComboPose1", "ComboPose2", "ComboPose3", "ComboPose4", "ComboPose5" };
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string AreaGestureState = "UpperBody.Cheer01";
    private const string HovlMaterials = "Assets/VFX/Hovl Studio/Magic effects pack/Materials/";

    [MenuItem("El Sendero/Combate/Gestos de hechizo y presentación del combo (INC-495, INC-643)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        bool warn = false;

        // Animator
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var layer = ctrl != null ? ctrl.layers.FirstOrDefault(l => l.name == "UpperBody") : null;
        var magic = layer?.stateMachine.stateMachines.Select(c => c.stateMachine).FirstOrDefault(m => m.name == "Magic");
        var clip = AssetDatabase.LoadAllAssetsAtPath(ComboIdleClip).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        var idle = magic?.states.Select(st => st.state).FirstOrDefault(st => st.name == "ComboIdle");
        if (idle == null || clip == null) { log.AppendLine("AVISO: no encuentro UpperBody/Magic/ComboIdle o Idle_Battle_NoWeapon (¿se montó INC-494?)."); warn = true; }
        else
        {
            bool cambios = false;
            if (idle.motion != clip || idle.speed != 1f || idle.cycleOffset != 0f)
            {
                idle.motion = clip;
                idle.speed = 1f;
                idle.cycleOffset = 0f;
                cambios = true;
                log.AppendLine("Animator: ComboIdle = Idle_Battle_NoWeapon (Tiny Hero).");
            }
            foreach (var nombre in PosesRetiradas)
            {
                var vieja = magic.states.Select(st => st.state).FirstOrDefault(st => st.name == nombre);
                if (vieja == null) continue;
                magic.RemoveState(vieja);
                cambios = true;
                log.AppendLine($"Animator: retirado UpperBody.Magic.{nombre}.");
            }
            var capas = ctrl.layers;
            for (int i = 0; i < capas.Length; i++)
            {
                if (capas[i].name != "UpperBody" || capas[i].iKPass) continue;
                capas[i].iKPass = true;
                ctrl.layers = capas;
                cambios = true;
                log.AppendLine("Animator: IK Pass activado en UpperBody (para ManosIK).");
            }
            if (cambios) { EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }
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
                so.ApplyModifiedPropertiesWithoutUndo();

                var animator = combo.GetComponentInParent<Animator>();
                var cuerpo = animator != null ? animator.gameObject : combo.gameObject;
                if (cuerpo.GetComponent<ElevacionVisual>() == null) cuerpo.AddComponent<ElevacionVisual>();
                if (cuerpo.GetComponent<ManosIK>() == null) cuerpo.AddComponent<ManosIK>();

                var presentacion = combo.GetComponent<PresentacionDelCombo>();
                if (presentacion == null) presentacion = combo.gameObject.AddComponent<PresentacionDelCombo>();
                var sp = new SerializedObject(presentacion);
                warn |= !AsignarMaterial(sp, "materialBase", "MagicCircle.mat", log);
                warn |= !AsignarMaterial(sp, "materialExterior", "TechCircle.mat", log);
                warn |= !AsignarMaterial(sp, "materialAnillo", "Circle2.mat", log);
                warn |= !AsignarMaterial(sp, "materialDestello", "GlowFree1.mat", log);
                warn |= !AsignarMaterial(sp, "materialChispas", "Point.mat", log);
                warn |= !AsignarMaterial(sp, "materialPolvo", "SmokeFree1.mat", log);
                warn |= !AsignarMaterial(sp, "materialOrbe", "GlowFree1.mat", log);
                sp.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
                log.AppendLine("Will: pose del combo = ComboIdle; ElevacionVisual y PresentacionDelCombo montados.");
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

        string text = "=== Gestos de hechizo y presentación del combo (INC-495, INC-643) ===\n" + log + (warn ? "" : "Sin avisos.\n");
        if (warn) Debug.LogWarning(text); else Debug.Log(text);
    }

    /// <summary>Pone el material si el campo está vacío (respeta lo ajustado a mano).</summary>
    private static bool AsignarMaterial(SerializedObject so, string campo, string archivo, StringBuilder log)
    {
        var prop = so.FindProperty(campo);
        if (prop.objectReferenceValue != null) return true;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HovlMaterials + archivo);
        if (mat == null) { log.AppendLine($"AVISO: no encuentro {HovlMaterials + archivo}."); return false; }
        prop.objectReferenceValue = mat;
        return true;
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
