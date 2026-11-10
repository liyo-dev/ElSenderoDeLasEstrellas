#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Medida de un solo uso: altura de manos y cabeza en el último fotograma de un gesto (INC-939).
public static class MedirPoseDeConjuro
{
    [MenuItem("El Sendero/Archivo/Medir pose de conjuro (INC-939)")]
    public static void Medir()
    {
        var sb = new StringBuilder();
        string[] clips = { "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/LevelUp_NoWeapon.fbx" };
        foreach (var (nombre, guid) in new[] { ("NPC_Archimago", "0697f276b63523d4cad83528e85e3df2"),  })
        {
            string rutaPrefab = guid == "NINA" ? "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab" : AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefab == null) { sb.AppendLine(nombre + ": sin prefab"); continue; }
            var go = (GameObject)Object.Instantiate(prefab);
            go.transform.position = Vector3.zero;
            sb.AppendLine(nombre + " = " + rutaPrefab);
            var anim = go.GetComponentInChildren<Animator>(true);
            foreach (var ruta in clips)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
                if (clip == null) { sb.AppendLine(ruta + ": sin clip"); continue; }
                AnimationMode.StartAnimationMode();
                foreach (float f in new[] { 0.70f, 0.72f, 0.74f })
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(anim.gameObject, clip, clip.length * f);
                    AnimationMode.EndSampling();
                    Vector3 raiz = go.transform.position;
                    string Hueso(HumanBodyBones h)
                    {
                        var t = anim.isHuman ? anim.GetBoneTransform(h) : null;
                        if (t == null) return h + "=?";
                        Vector3 l = go.transform.InverseTransformPoint(t.position);
                        return $"{h}=({l.x:0.00},{l.y:0.00},{l.z:0.00})";
                    }
                    sb.AppendLine($"{nombre} {clip.name} {f:0.0} (dur {clip.length:0.00}s) escala {go.transform.lossyScale.y:0.00}: " +
                        Hueso(HumanBodyBones.LeftUpperArm) + " " + Hueso(HumanBodyBones.LeftLowerArm) + " " + Hueso(HumanBodyBones.LeftHand) + " " + Hueso(HumanBodyBones.RightUpperArm) + " " + Hueso(HumanBodyBones.RightLowerArm) + " " + Hueso(HumanBodyBones.RightHand) + " " + Hueso(HumanBodyBones.Head) + " " + Hueso(HumanBodyBones.Chest));
                }
                AnimationMode.StopAnimationMode();
            }
            var rend = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled);
            if (rend.Any())
            {
                var b = rend.Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
                sb.AppendLine($"{nombre}: suelo {go.transform.position.y:0.00}, techo {b.max.y - go.transform.position.y:0.00}");
            }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText("Logs/medida_pose_conjuro.txt", sb.ToString());
    }
}
#endif
