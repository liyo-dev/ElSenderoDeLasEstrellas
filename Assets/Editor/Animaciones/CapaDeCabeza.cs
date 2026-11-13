#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Los gestos que solo mueven la cabeza (asentir, negar) van en su propia capa, «Cabeza», con una
/// máscara de solo la cabeza. En la capa de brazos sustituían la pose que se estuviera sosteniendo
/// (mano al pecho, manos a la espalda): los brazos bajaban durante el gesto y al acabar «subían».
/// Así la cabeza asiente y los brazos siguen con lo suyo. Se puede ejecutar varias veces. Ver INC-1122
public static class CapaDeCabeza
{
    private const string NombreDeLaCapa = "Cabeza";
    private const string RutaDeLaMascara = "Assets/Art/Characters/Animator/Mascara_Cabeza.mask";
    private static readonly string[] GestosDeCabeza = { "HeadNod01", "HeadShake01", "HeadShake02" };

    [MenuItem("El Sendero/Personajes/Animaciones/Gestos de cabeza en su capa")]
    public static void Aplicar()
    {
        var mascara = Mascara();
        var sb = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.StartsWith("Assets/Plugins/")) continue;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (controller == null) continue;
            int movidos = Pasar(controller, mascara);
            if (movidos > 0) sb.Append($"{controller.name}: {movidos} gestos a la capa {NombreDeLaCapa}. ");
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Gestos de cabeza", sb.Length > 0 ? sb.ToString() : "Nada que pasar: ya estaban en su capa.");
    }

    private static int Pasar(AnimatorController controller, AvatarMask mascara)
    {
        var capas = controller.layers;
        var aMover = new List<(AnimatorStateMachine maquina, AnimatorState estado)>();
        for (int i = 0; i < capas.Length; i++)
        {
            if (capas[i].name == NombreDeLaCapa) continue;
            foreach (var hijo in capas[i].stateMachine.states)
                if (GestosDeCabeza.Contains(hijo.state.name)) aMover.Add((capas[i].stateMachine, hijo.state));
        }
        if (aMover.Count == 0) return 0;

        int indice = System.Array.FindIndex(capas, c => c.name == NombreDeLaCapa);
        if (indice < 0)
        {
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = NombreDeLaCapa,
                defaultWeight = 1f,
                avatarMask = mascara,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = NombreDeLaCapa, hideFlags = HideFlags.HideInHierarchy },
            });
            capas = controller.layers;
            indice = capas.Length - 1;
            AssetDatabase.AddObjectToAsset(capas[indice].stateMachine, controller);
        }
        var maquina = capas[indice].stateMachine;
        var quieta = maquina.states.Select(s => s.state).FirstOrDefault(s => s.name == "Quieta")
                     ?? maquina.AddState("Quieta", new Vector3(250f, 0f, 0f));
        maquina.defaultState = quieta;

        foreach (var (origen, estado) in aMover)
        {
            if (maquina.states.All(s => s.state.name != estado.name))
            {
                var nuevo = maquina.AddState(estado.name, new Vector3(250f, 80f * maquina.states.Length, 0f));
                nuevo.motion = estado.motion;
                nuevo.speed = estado.speed;
                var vuelta = nuevo.AddTransition(quieta);
                vuelta.hasExitTime = true;
                vuelta.exitTime = 0.9f;
                vuelta.duration = 0.15f;
            }
            origen.RemoveState(estado);
        }
        EditorUtility.SetDirty(controller);
        return aMover.Count;
    }

    private static AvatarMask Mascara()
    {
        var mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(RutaDeLaMascara);
        if (mascara != null) return mascara;
        mascara = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mascara.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, (AvatarMaskBodyPart)i == AvatarMaskBodyPart.Head);
        AssetDatabase.CreateAsset(mascara, RutaDeLaMascara);
        return mascara;
    }
}
#endif
