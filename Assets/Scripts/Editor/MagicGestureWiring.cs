using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Gestos de hechizo y presentación del combo (INC-495, INC-643). Idempotente.
/// <list type="bullet">
/// <item>Poses del combo en UpperBody/Magic, todas congeladas (velocidad 0 y un instante fijo del clip):
/// ComboIdle = brazos delante mientras se teclea, y ComboPose1…5 = una pose distinta por cada botón
/// acertado. Son clips de RPG Tiny Hero Duo, hechos para las proporciones de Will (los de Kevin
/// Iglesias le cruzan los brazos por la cara). Ver INC-643.</item>
/// <item>_WILL.prefab: ComboCastController con la pose ComboIdle; ElevacionVisual y PresentacionDelCombo
/// (círculo propio, chispas, polvo) con los materiales de Hovl; MagicCaster con el gesto de área
/// Cheer01 (brazo arriba). Ver INC-521 e INC-643.</item>
/// <item>Gesto por hechizo: Sello del Pacto y Grieta = área (saltito y brazo arriba), Corazón Estelar = omni.</item>
/// </list>
/// </summary>
public static class MagicGestureWiring
{
    private const string ControllerPath = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string TinyHero = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/";

    /// <summary>Estado, clip e instante (normalizado) de cada pose congelada del combo.</summary>
    private static readonly (string estado, string clip, float instante)[] Poses =
    {
        ("ComboIdle", "NoWeapon/Defend_NoWeapon.fbx", 0.5f),                 // brazos delante
        ("ComboPose1", "NoWeapon/FoundSomething_NoWeapon.fbx", 0.7f),        // brazos arriba
        ("ComboPose2", "NoWeapon/Swimming_Floating_NoWeapon.fbx", 0.375f),   // brazos en cruz
        ("ComboPose3", "MagicWand/Attack03_Start_MagicWand.fbx", 0.6f),      // puño al cielo
        ("ComboPose4", "NoWeapon/LevelUp_NoWeapon.fbx", 0.6f),               // brazos abiertos
        ("ComboPose5", "NoWeapon/Victory_NoWeapon.fbx", 0.6f),               // puño arriba
    };
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
        if (magic == null) { log.AppendLine("AVISO: el Animator no tiene UpperBody/Magic (¿se montó INC-494?)."); warn = true; }
        else
        {
            bool cambios = false;
            for (int i = 0; i < Poses.Length; i++)
                cambios |= MontarPose(magic, Poses[i].estado, Poses[i].clip, Poses[i].instante, new Vector3(850, i * 70), log, ref warn);
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

                var presentacion = combo.GetComponent<PresentacionDelCombo>();
                if (presentacion == null) presentacion = combo.gameObject.AddComponent<PresentacionDelCombo>();
                var sp = new SerializedObject(presentacion);
                warn |= !AsignarMaterial(sp, "materialBase", "MagicCircle.mat", log);
                warn |= !AsignarMaterial(sp, "materialExterior", "TechCircle.mat", log);
                warn |= !AsignarMaterial(sp, "materialAnillo", "Circle2.mat", log);
                warn |= !AsignarMaterial(sp, "materialDestello", "GlowFree1.mat", log);
                warn |= !AsignarMaterial(sp, "materialChispas", "Point.mat", log);
                warn |= !AsignarMaterial(sp, "materialPolvo", "SmokeFree1.mat", log);
                var poses = sp.FindProperty("posesPorBoton");
                if (poses.arraySize == 0)
                {
                    poses.arraySize = Poses.Length - 1;
                    for (int i = 1; i < Poses.Length; i++)
                        poses.GetArrayElementAtIndex(i - 1).stringValue = "UpperBody.Magic." + Poses[i].estado;
                }
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

    /// <summary>Crea o ajusta un estado con un fotograma fijo del clip (velocidad 0 y desfase de ciclo).</summary>
    private static bool MontarPose(AnimatorStateMachine sm, string nombre, string clipRelativo, float instante, Vector3 pos, StringBuilder log, ref bool warn)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(TinyHero + clipRelativo).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) { log.AppendLine($"AVISO: no encuentro {TinyHero + clipRelativo}."); warn = true; return false; }

        var estado = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == nombre);
        if (estado == null)
        {
            estado = sm.AddState(nombre, pos);
            estado.writeDefaultValues = true;
        }
        else if (estado.motion == clip && estado.speed == 0f && Mathf.Approximately(estado.cycleOffset, instante)) return false;

        estado.motion = clip;
        estado.speed = 0f;
        estado.cycleOffset = instante;
        log.AppendLine($"Animator: UpperBody.Magic.{nombre} = {clip.name} congelado en {instante:0.##}.");
        return true;
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
