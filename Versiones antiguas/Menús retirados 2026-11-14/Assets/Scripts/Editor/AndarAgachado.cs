using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Añade al animator del grupo (Invector@BasicLocomotion) el estado «Andar agachado»: una mezcla
/// por «InputMagnitude» entre quieto agachado y andar agachado, con las animaciones de Kevin
/// Iglesias. Se entra desde «Free Locomotion» con «IsCrouching» y se vuelve al soltarlo o al dejar
/// de pisar el suelo (saltos y caídas siguen por «Free Locomotion»). Lo usa SigiloDelGrupo.
/// El animator traía un «Free Crouch» en una máquina de estados que ninguna capa usa, con una
/// animación que no está en el proyecto: no se toca. Idempotente.
public static class AndarAgachado
{
    private const string RutaDelAnimator = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string Carpeta = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Movement/Crouch/";
    private const string Quieto = Carpeta + "HumanM@Crouch01_Idle.fbx";
    private const string Andando = Carpeta + "CrouchWalk/HumanM@Crouch01_Walk_Forward.fbx";
    private const string EstadoDeOrigen = "Free Locomotion";
    private const string NombreDelEstado = "Andar agachado";
    private const string Agachado = "IsCrouching";
    private const string EnElSuelo = "IsGrounded";
    private const float Transicion = 0.2f;

    [MenuItem("El Sendero/Personajes/Poner la animación de andar agachado")]
    public static void Poner()
    {
        var informe = new StringBuilder();
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaDelAnimator);
        if (ctrl == null) { Avisar($"No encuentro {RutaDelAnimator}."); return; }
        if (!TieneBool(ctrl, EnElSuelo)) { Avisar($"El animator no tiene el parámetro «{EnElSuelo}»."); return; }
        if (!TieneBool(ctrl, Agachado))
        {
            ctrl.AddParameter(Agachado, AnimatorControllerParameterType.Bool);
            informe.AppendLine($"Parámetro «{Agachado}» añadido.");
        }

        var quieto = Clip(Quieto);
        var andando = Clip(Andando);
        if (quieto == null || andando == null) { Avisar("No encuentro las animaciones de agacharse de Kevin Iglesias."); return; }
        EnBucle(Quieto, informe);
        EnBucle(Andando, informe);

        var encontrados = new List<(AnimatorState estado, AnimatorStateMachine dueno)>();
        BuscarEstados(ctrl.layers[0].stateMachine, EstadoDeOrigen, encontrados);
        if (encontrados.Count != 1)
        {
            Avisar($"La capa base tiene {encontrados.Count} estados «{EstadoDeOrigen}»; esperaba uno.");
            return;
        }
        var (origen, maquina) = encontrados[0];

        var mezcla = AssetDatabase.LoadAllAssetsAtPath(RutaDelAnimator).OfType<BlendTree>()
            .FirstOrDefault(b => b.name == NombreDelEstado);
        if (mezcla == null)
        {
            mezcla = new BlendTree { name = NombreDelEstado, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(mezcla, ctrl);
        }
        mezcla.blendType = BlendTreeType.Simple1D;
        mezcla.blendParameter = "InputMagnitude";
        mezcla.useAutomaticThresholds = false;
        mezcla.children = new ChildMotion[0];
        mezcla.AddChild(quieto, 0f);
        mezcla.AddChild(andando, 0.5f);

        var estado = maquina.states.Select(s => s.state).FirstOrDefault(s => s.name == NombreDelEstado);
        if (estado == null)
        {
            var pos = maquina.states.First(s => s.state == origen).position + new Vector3(0f, 80f, 0f);
            estado = maquina.AddState(NombreDelEstado, pos);
            informe.AppendLine($"Estado «{NombreDelEstado}» creado junto a «{EstadoDeOrigen}».");
        }
        estado.motion = mezcla;
        estado.speed = 1f;
        estado.speedParameterActive = false;
        estado.writeDefaultValues = origen.writeDefaultValues;
        estado.iKOnFeet = true;

        foreach (var t in origen.transitions.Where(t => t.destinationState == estado).ToArray()) origen.RemoveTransition(t);
        foreach (var t in estado.transitions.ToArray()) estado.RemoveTransition(t);

        var entrar = origen.AddTransition(estado);
        Configurar(entrar);
        entrar.AddCondition(AnimatorConditionMode.If, 0f, Agachado);
        entrar.AddCondition(AnimatorConditionMode.If, 0f, EnElSuelo);

        var levantarse = estado.AddTransition(origen);
        Configurar(levantarse);
        levantarse.AddCondition(AnimatorConditionMode.IfNot, 0f, Agachado);

        var alAire = estado.AddTransition(origen);
        Configurar(alAire);
        alAire.duration = 0.1f;
        alAire.AddCondition(AnimatorConditionMode.IfNot, 0f, EnElSuelo);

        EditorUtility.SetDirty(mezcla);
        EditorUtility.SetDirty(estado);
        EditorUtility.SetDirty(origen);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        informe.AppendLine($"«{NombreDelEstado}» en «{maquina.name}»: quieto agachado (0) y andar agachado (0,5) por InputMagnitude; entra con {Agachado} pisando el suelo.");
        Avisar(informe.ToString());
    }

    private static void Configurar(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = Transicion;
        t.canTransitionToSelf = false;
    }

    private static bool TieneBool(AnimatorController ctrl, string nombre)
        => ctrl.parameters.Any(p => p.name == nombre && p.type == AnimatorControllerParameterType.Bool);

    private static AnimationClip Clip(string ruta)
        => AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    private static void EnBucle(string ruta, StringBuilder informe)
    {
        if (AssetImporter.GetAtPath(ruta) is not ModelImporter importer) return;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool cambiado = false;
        foreach (var c in clips)
            if (!c.loopTime) { c.loopTime = true; cambiado = true; }
        if (!cambiado) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        informe.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: activado el bucle.");
    }

    private static void BuscarEstados(AnimatorStateMachine sm, string nombre, List<(AnimatorState, AnimatorStateMachine)> encontrados)
    {
        foreach (var s in sm.states)
            if (s.state.name == nombre) encontrados.Add((s.state, sm));
        foreach (var hijo in sm.stateMachines)
            BuscarEstados(hijo.stateMachine, nombre, encontrados);
    }

    private static void Avisar(string texto) => OrdenesDelEditor.Avisar("Andar agachado", texto);
}
