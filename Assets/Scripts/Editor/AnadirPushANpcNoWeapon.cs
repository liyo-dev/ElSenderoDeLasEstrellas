#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Añade el estado Push_InPlace_NoWeapon al layer UpperBody del Animator compartido de los NPCs.
///
/// El clip existe en el pack RPG Tiny Hero Duo (brazos delante, en bucle y sin root motion), pero
/// no estaba metido en el controller. Sin estado no hay forma de pedirlo desde el guion: HoldPose
/// resuelve por nombre de estado, no por nombre de clip.
public static class AnadirPushANpcNoWeapon
{
    const string RutaControlador = "Assets/Art/Characters/Animator/NPC_NoWeapon.controller";
    const string RutaFbx = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/InPlace/Push_InPlace_NoWeapon.fbx";
    const string NombreEstado = "Push_InPlace_NoWeapon";

    [MenuItem("El Sendero/Archivo/Anadir Push a NPC_NoWeapon")]
    public static void Ejecutar()
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (controlador == null)
        {
            Debug.LogError($"[Push] No encuentro el controller en '{RutaControlador}'.");
            return;
        }

        var clip = AssetDatabase.LoadAllAssetsAtPath(RutaFbx)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == NombreEstado);
        if (clip == null)
        {
            Debug.LogError($"[Push] No encuentro el clip '{NombreEstado}' en '{RutaFbx}'.");
            return;
        }

        AnimatorStateMachine maquina = null;
        foreach (var capa in controlador.layers)
            if (capa.name == "UpperBody") { maquina = capa.stateMachine; break; }

        if (maquina == null)
        {
            Debug.LogError("[Push] El controller no tiene un layer llamado 'UpperBody'.");
            return;
        }

        foreach (var hijo in maquina.states)
            if (hijo.state.name == NombreEstado)
            {
                Debug.Log($"[Push] El estado '{NombreEstado}' ya existe en UpperBody.");
                return;
            }

        var estado = maquina.AddState(NombreEstado);
        estado.motion = clip;
        estado.writeDefaultValues = true;

        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Push] Estado '{NombreEstado}' añadido a UpperBody de {RutaControlador}.");
    }
}
#endif
