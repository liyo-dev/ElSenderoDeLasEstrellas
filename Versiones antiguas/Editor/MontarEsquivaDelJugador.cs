using System.Collections.Generic;
using System.Linq;
using Core.InputGlyphs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Deja lista la esquiva del jugador (RB): los cuatro estados de rodar «en el sitio» en el
/// Animator del grupo, el componente <see cref="EsquivaDelJugador"/> en el cuerpo común de los
/// tres protagonistas y las filas de la pantalla de Controles (esquivar con RB / C y cambiar de
/// objetivo con el stick derecho / la rueda). Se puede repetir: lo que ya está no se toca. Ver
/// INC-1349.
/// </summary>
public static class MontarEsquivaDelJugador
{
    private const string Controlador = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string CarpetaDeClips = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/InPlace/";
    private const string EstadoDeReferencia = "JumpFullSpin_InPlace_NoWeapon";
    private const string Cuerpo = "Assets/Prefabs/Personajes/_CUERPO_DEL_GRUPO.prefab";
    private const string Controles = "Assets/_UI/ControlsSchemeConfig.asset";

    private static readonly string[] Estados =
    {
        "RollFWD_Battle_InPlace_NoWeapon",
        "RollBWD_Battle_InPlace_NoWeapon",
        "RollLFT_Battle_InPlace_NoWeapon",
        "RollRGT_Battle_InPlace_NoWeapon",
    };

    [MenuItem("El Sendero/Combate/Montar esquiva del jugador (INC-1349)")]
    public static void Montar()
    {
        var informe = new List<string>();
        AnadirEstados(informe);
        AnadirComponente(informe);
        ActualizarControles(informe);
        AssetDatabase.SaveAssets();

        string texto = string.Join("\n", informe);
        Debug.Log("[Esquiva] " + texto.Replace("\n", " · "));
        EditorUtility.DisplayDialog("Esquiva del jugador", texto, "Vale");
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    private static void AnadirEstados(List<string> informe)
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        if (controlador == null) { informe.Add("✗ No encuentro el Animator del grupo: " + Controlador); return; }

        var referencia = MaquinaConElEstado(controlador.layers[0].stateMachine, EstadoDeReferencia, out var estadoReferencia);
        var maquina = referencia ?? controlador.layers[0].stateMachine;

        string parametro = EsquivaDelJugador.ParametroDeVelocidadPorDefecto;
        bool cambiado = false;
        if (!controlador.parameters.Any(p => p.name == parametro))
        {
            controlador.AddParameter(new AnimatorControllerParameter
            {
                name = parametro, type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });
            cambiado = true;
        }

        int anadidos = 0, conVelocidad = 0;
        for (int i = 0; i < Estados.Length; i++)
        {
            string nombre = Estados[i];
            if (MaquinaConElEstado(controlador.layers[0].stateMachine, nombre, out var existente) != null)
            {
                if (!existente.speedParameterActive || existente.speedParameter != parametro)
                {
                    existente.speedParameter = parametro;
                    existente.speedParameterActive = true;
                    conVelocidad++;
                }
                continue;
            }

            var clip = ClipDe(CarpetaDeClips + nombre + ".fbx");
            if (clip == null) { informe.Add("✗ Falta el clip " + nombre); continue; }

            var estado = maquina.AddState(nombre, new Vector3(650f, 50f + 60f * i, 0f));
            estado.motion = clip;
            if (estadoReferencia != null) estado.writeDefaultValues = estadoReferencia.writeDefaultValues;
            estado.speedParameter = parametro;
            estado.speedParameterActive = true;
            anadidos++;
        }

        if (anadidos > 0 || conVelocidad > 0 || cambiado) EditorUtility.SetDirty(controlador);
        informe.Add(anadidos > 0 ? $"✓ Animator: {anadidos} estados de rodar añadidos." : "· Animator: los estados de rodar ya estaban.");
        if (conVelocidad > 0) informe.Add($"✓ Animator: velocidad de esquiva enganchada a {conVelocidad} estados ({parametro}).");
    }

    private static AnimatorStateMachine MaquinaConElEstado(AnimatorStateMachine maquina, string nombre, out AnimatorState estado)
    {
        foreach (var hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) { estado = hijo.state; return maquina; }
        foreach (var sub in maquina.stateMachines)
        {
            if (sub.stateMachine == null) continue;
            var encontrada = MaquinaConElEstado(sub.stateMachine, nombre, out estado);
            if (encontrada != null) return encontrada;
        }
        estado = null;
        return null;
    }

    private static AnimationClip ClipDe(string rutaFbx)
    {
        return AssetDatabase.LoadAllAssetsAtPath(rutaFbx)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    // ── Cuerpo del grupo ──────────────────────────────────────────────────────

    private static void AnadirComponente(List<string> informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(Cuerpo);
        if (raiz == null) { informe.Add("✗ No encuentro el cuerpo del grupo: " + Cuerpo); return; }
        try
        {
            var voltereta = raiz.GetComponentInChildren<VolteretaDelJugador>(true);
            if (voltereta == null) { informe.Add("✗ El cuerpo del grupo no tiene VolteretaDelJugador."); return; }
            var esquiva = voltereta.GetComponent<EsquivaDelJugador>();
            bool nueva = esquiva == null;
            if (nueva) esquiva = voltereta.gameObject.AddComponent<EsquivaDelJugador>();
            var so = new SerializedObject(esquiva);
            so.FindProperty("velocidad").floatValue = EsquivaDelJugador.VelocidadPorDefecto;
            so.FindProperty("saltito").floatValue = EsquivaDelJugador.SaltitoPorDefecto;
            so.FindProperty("distancia").floatValue = EsquivaDelJugador.DistanciaPorDefecto;
            so.FindProperty("duracionAtras").floatValue = EsquivaDelJugador.DuracionPorDefecto;
            so.FindProperty("espera").floatValue = EsquivaDelJugador.EsperaPorDefecto;
            so.FindProperty("alturaDelSalto").floatValue = EsquivaDelJugador.AlturaDelSaltoPorDefecto;
            so.FindProperty("distanciaDelSalto").floatValue = EsquivaDelJugador.DistanciaDelSaltoPorDefecto;
            so.FindProperty("duracionDelSalto").floatValue = EsquivaDelJugador.DuracionDelSaltoPorDefecto;
            so.FindProperty("distanciaRodando").floatValue = EsquivaDelJugador.DistanciaRodandoPorDefecto;
            so.FindProperty("devuelveElControlEn").floatValue = EsquivaDelJugador.DevuelveElControlPorDefecto;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, Cuerpo);
            informe.Add(nueva ? "✓ Cuerpo del grupo: esquiva añadida (Will, Estela y Liam)."
                              : $"✓ Cuerpo del grupo: esquiva rápida (salto {EsquivaDelJugador.DistanciaDelSaltoPorDefecto} m en {EsquivaDelJugador.DuracionDelSaltoPorDefecto} s, rueda {EsquivaDelJugador.DistanciaRodandoPorDefecto} m).");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ── Pantalla de Controles ─────────────────────────────────────────────────

    private static void ActualizarControles(List<string> informe)
    {
        var config = AssetDatabase.LoadAssetAtPath<ControlsSchemeConfig>(Controles);
        if (config == null) { informe.Add("✗ No encuentro " + Controles); return; }

        var porDefecto = ControlsSchemeConfig.BuildDefaultEntries();
        var esquivar = porDefecto.First(e => e.descriptionKey == "CONTROLS_DODGE");
        var objetivo = porDefecto.First(e => e.descriptionKey == "CONTROLS_TARGET_NEXT");

        bool cambiado = false;
        int filaObjetivo = config.entries.FindIndex(e => e.descriptionKey == "CONTROLS_TARGET_NEXT");
        if (filaObjetivo >= 0 && !string.IsNullOrEmpty(config.entries[filaObjetivo].glyphName))
        {
            config.entries[filaObjetivo] = objetivo;
            cambiado = true;
        }
        if (!config.entries.Any(e => e.descriptionKey == "CONTROLS_DODGE"))
        {
            config.entries.Insert(filaObjetivo >= 0 ? filaObjetivo : config.entries.Count, esquivar);
            cambiado = true;
        }

        if (cambiado) EditorUtility.SetDirty(config);
        informe.Add(cambiado ? "✓ Controles: esquivar (RB / C) y cambiar de objetivo (stick derecho / rueda)." : "· Controles: ya estaban.");
    }
}
