#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Pose de abrazarse de frío para humanoides (Will, Liam, Estela): el Idle01 del pack con los brazos
/// pegados al cuerpo y cada mano agarrando el brazo contrario, el izquierdo por debajo. La genera
/// <see cref="PoseSobreClip.CrearHumanoide"/> y queda como estado «ClimaFrio» de la capa UpperBody
/// (tronco y brazos; las piernas siguen con su reposo) en el controlador del jugador y en el de los
/// NPC del grupo. La usa <see cref="ClimaSobreElJugador"/> con lluvia o nieve, quieto; los hombros
/// encogidos y la tiritona se ponen encima, en código. Ver INC-959.
public static class AbrazarseDeFrio
{
    public const string RutaDelClip = "Assets/Art/Animations/Propias/Frio_Abrazarse.anim";
    private const string Capa = "UpperBody";

    private const string ClipBase = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string Modelo = "Assets/Prefabs/_LIAM.prefab";

    private static readonly string[] Controladores =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller",
    };

    /// Direcciones en el espacio del personaje (X a su derecha, Y arriba, Z al frente): brazos casi
    /// pegados al costado y algo al frente, antebrazos cruzados por delante del pecho hacia el brazo
    /// contrario (el avatar no tiene dedos: las manos siguen al antebrazo).
    private static readonly PoseSobreClip.ApunteHumano[] Apuntes =
    {
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(-0.1f, -0.6f, 0.78f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-0.9f, 0.42f, -0.12f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(0.15f, -0.66f, 0.74f)),
        PoseSobreClip.ApunteHumano.Hacia(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(0.9f, 0.36f, -0.12f)),
    };

    [MenuItem("El Sendero/Personajes/Animaciones/Abrazarse de frío: crear el clip y sus estados", priority = 131)]
    public static void Crear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modelo);
        var base_ = AssetDatabase.LoadAllAssetsAtPath(ClipBase).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (prefab == null || base_ == null)
        {
            OrdenesDelEditor.Avisar("Abrazarse de frío", $"falta {(prefab == null ? Modelo : ClipBase)}");
            return;
        }

        float desvio = PoseSobreClip.CrearHumanoide(prefab, base_, RutaDelClip, Apuntes);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDelClip);
        var hecho = new System.Text.StringBuilder($"clip {RutaDelClip} ({clip.length:0.00} s, desvío máx. {desvio:0.0}°).");
        foreach (var ruta in Controladores)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (ctrl == null) { hecho.Append($" Falta {ruta}."); continue; }
            bool nuevo = PoseSobreClip.EstadoEnBucle(ctrl, ClimaSobreElJugador.EstadoDeFrio, clip, Capa);
            hecho.Append($" {ctrl.name}: {Capa}.{ClimaSobreElJugador.EstadoDeFrio} {(nuevo ? "añadido" : "puesto al día")}.");
        }
        OrdenesDelEditor.Avisar("Abrazarse de frío", hecho.ToString());
    }
}
#endif
