#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Pose de brazos cruzados para humanoides (Liam, Will, Estela y los NPC): el Idle01 del pack con los
/// antebrazos cruzados delante del pecho, el izquierdo por debajo, y la respiración del Idle01 en el
/// resto del cuerpo. La genera <see cref="PoseSobreClip.CrearHumanoide"/> y queda como estado
/// «BrazosCruzados» en la capa base de los controladores de los NPC y del grupo, para usarla en los
/// guiones con «bucle BrazosCruzados» o con <c>HoldPose("BrazosCruzados")</c>. Ver INC-922.
public static class BrazosCruzados
{
    public const string Estado = "BrazosCruzados";
    public const string RutaDelClip = "Assets/Art/Animations/Propias/BrazosCruzados_Idle.anim";

    private const string ClipBase = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string Modelo = "Assets/Prefabs/_LIAM.prefab";

    private static readonly string[] Controladores =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller",
    };

    /// Direcciones en el espacio del personaje (X a su derecha, Y arriba, Z al frente): brazos abajo y algo
    /// al frente, antebrazos cruzados delante del pecho, el izquierdo por debajo.
    private static readonly PoseSobreClip.ApunteHumano[] Apuntes =
    {
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0.3f, -0.6f, 0.75f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-1f, 0.6f, 0.3f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-0.3f, -0.6f, 0.75f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(1f, 0.5f, 0.15f)),
    };

    [MenuItem("El Sendero/Personajes/Animaciones/Brazos cruzados: crear el clip y sus estados", priority = 130)]
    public static void Crear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        var base_ = AssetDatabase.LoadAllAssetsAtPath(ClipBase).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (prefab == null || base_ == null)
        {
            OrdenesDelEditor.Avisar("Brazos cruzados", $"falta {(prefab == null ? Modelo : ClipBase)}");
            return;
        }

        float desvio = PoseSobreClip.CrearHumanoide(prefab, base_, RutaDelClip, Apuntes);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDelClip);
        var hecho = new System.Text.StringBuilder($"clip {RutaDelClip} ({clip.length:0.00} s, desvío máx. {desvio:0.0}°).");
        foreach (var ruta in Controladores)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (ctrl == null) { hecho.Append($" Falta {ruta}."); continue; }
            bool nuevo = PoseSobreClip.EstadoEnBucle(ctrl, Estado, clip);
            hecho.Append($" {ctrl.name}: estado {Estado} {(nuevo ? "añadido" : "puesto al día")}.");
        }
        OrdenesDelEditor.Avisar("Brazos cruzados", hecho.ToString());
    }
}
#endif
