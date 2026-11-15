using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Deja al día los estados del controlador de los retratos vivos (HUD, menús): cada expresión y
/// cada reacción con su animación. «Cansado» es la de reposo (el retrato ya respira más deprisa
/// por código); el mareo queda solo para «Caido». Faltaban «Golpe», «Victoria» y «Saludo», así
/// que el retrato no reaccionaba al daño. Ver INC-1120.
public static class EstadosDelRetratoVivo
{
    private const string Controlador = "Assets/Art/UI/Retratos/Grupo/RetratoVivo.controller";
    private const string Carpeta = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/";

    private static readonly (string estado, string fbx)[] Estados =
    {
        ("Normal", "Idle_Normal_NoWeapon"),
        ("Cansado", "Idle_Normal_NoWeapon"),
        ("Caido", "Dizzy_NoWeapon"),
        ("Contento", "Idle_Battle_NoWeapon"),
        ("Golpe", "GetHit01_NoWeapon"),
        ("Victoria", "Victory_NoWeapon"),
        ("Saludo", "Greeting01_NoWeapon"),
    };

    [MenuItem("El Sendero/Personajes/Retrato vivo: estados al día")]
    private static void Poner()
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        if (ac == null) { OrdenesDelEditor.Avisar("Retrato vivo", "No encuentro " + Controlador); return; }
        var maquina = ac.layers[0].stateMachine;
        var informe = new System.Text.StringBuilder();
        foreach (var (nombre, fbx) in Estados)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Carpeta + fbx + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) { informe.Append($"{nombre}: falta {fbx}. "); continue; }
            var estado = maquina.states.Select(s => s.state).FirstOrDefault(s => s.name == nombre)
                         ?? maquina.AddState(nombre);
            estado.motion = clip;
            informe.Append($"{nombre}={clip.name}. ");
        }
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Retrato vivo", informe.ToString());
    }
}
