using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Incorpora el repertorio de actuación con importadores y estados reutilizables.
public static class AnadirAnimacionesActuacionINC574
{
    private const string Kevin = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male";
    private const string Mixamo = "Assets/Art/Animations/Mixamo";
    private static readonly string[] EstadosKevin =
    {
        "Beg01_Loop", "CastingEnter01", "CastingIdle01", "CastingExit01", "CastingDamage01",
        "Loot01_Begin", "Loot01_Loop", "Loot01_Stop", "MagicAttackOmni01_Load", "Reverence01_Loop",
        "Laugh01_Loop", "Cry01_Loop", "Stun01", "Knockdown01_Ground", "Knockdown01_StandUp",
        "Opening01_Begin", "Opening01_Loop", "Opening01_Stop"
    };
    private static readonly HashSet<string> BuclesMixamo = new(StringComparer.OrdinalIgnoreCase)
    {
        "RezarDePie", "RezarEncogido", "ArreglarAgachado", "InvocarBrazosArriba",
        "HechizoHaciaArriba", "LanzamientoSostenido", "Baile_A", "Baile_B"
    };

    [MenuItem("El Sendero/Archivo/Animación/Añadir animaciones de actuación INC-574")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controllers = RecogerControllers();
        int estados = 0, importadores = 0, clips = 0;
        var rutasKevin = AssetDatabase.FindAssets("t:Model", new[] { Kevin });
        foreach (string nombre in EstadosKevin)
        {
            string archivo = "HumanM@" + nombre.Replace("_", " - ");
            string ruta = null;
            foreach (string guid in rutasKevin)
            {
                string candidata = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(candidata) == archivo) { ruta = candidata; break; }
            }
            if (ruta == null) { Debug.LogWarning($"[INC-574] Falta el clip {archivo}."); continue; }
            bool bucle = nombre.EndsWith("_Loop", StringComparison.Ordinal) || nombre == "CastingIdle01" || nombre == "Knockdown01_Ground";
            if (PrepararImportador(ruta, bucle, false)) importadores++;
            var clip = CargarClip(ruta);
            if (clip == null) { Debug.LogWarning($"[INC-574] Sin AnimationClip en {ruta}."); continue; }
            clips++;
            foreach (var controller in controllers)
                if (AnadirEstado(controller, nombre, clip, CuerpoEntero(nombre))) estados++;
        }

        int encontradosMixamo = 0;
        if (AssetDatabase.IsValidFolder(Mixamo))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Mixamo }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (!ruta.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                encontradosMixamo++;
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                if (PrepararImportador(ruta, BuclesMixamo.Contains(nombre), true)) importadores++;
                var clip = CargarClip(ruta);
                if (clip == null) { Debug.LogWarning($"[INC-574] Sin AnimationClip en {ruta}."); continue; }
                clips++;
                foreach (var controller in controllers)
                    if (AnadirEstado(controller, nombre, clip, CuerpoEntero(nombre))) estados++;
            }
        }
        if (encontradosMixamo == 0) Debug.LogWarning("[INC-574] Todavía no hay FBX de Mixamo; se completa el repertorio disponible.");
        AssetDatabase.SaveAssets();
        Debug.Log($"[INC-574] Controllers: {controllers.Count}; clips disponibles: {clips}; estados creados: {estados}; importadores modificados: {importadores}. " +
            "Los estados existentes se actualizan sin duplicar estados ni salidas.");
    }

    private static HashSet<AnimatorController> RecogerControllers()
    {
        var resultado = new HashSet<AnimatorController>();
        foreach (string ruta in new[]
        {
            "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
            "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller"
        })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (controller != null) resultado.Add(controller);
        }
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset");
        if (roster != null)
            foreach (var entrada in roster.entries)
            {
                if (entrada?.prefab == null) continue;
                foreach (var animator in entrada.prefab.GetComponentsInChildren<Animator>(true))
                {
                    RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
                    while (runtime is AnimatorOverrideController variante) runtime = variante.runtimeAnimatorController;
                    if (runtime is AnimatorController controller) resultado.Add(controller);
                }
            }
        return resultado;
    }

    private static bool CuerpoEntero(string nombre)
        => nombre.StartsWith("Loot", StringComparison.Ordinal) || nombre.StartsWith("Knockdown", StringComparison.Ordinal) ||
           nombre.StartsWith("Opening", StringComparison.Ordinal) || nombre.StartsWith("Baile", StringComparison.Ordinal) ||
           nombre == "Stun01" || nombre == "Agacharse" || nombre == "ArreglarAgachado" ||
           nombre == "LevantarseDeRodillas" || nombre == "RezarEncogido";

    private static bool PrepararImportador(string ruta, bool bucle, bool mixamo)
    {
        var importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) return false;
        bool cambiado = false;
        if (mixamo)
        {
            if (importer.animationType != ModelImporterAnimationType.Human)
            { importer.animationType = ModelImporterAnimationType.Human; cambiado = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; cambiado = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; cambiado = true; }
        }
        var ajustes = importer.clipAnimations;
        if (ajustes.Length == 0) ajustes = importer.defaultClipAnimations;
        foreach (var ajuste in ajustes)
        {
            if (ajuste.loopTime != bucle) { ajuste.loopTime = bucle; cambiado = true; }
            if (!mixamo) continue;
            if (!ajuste.lockRootRotation || !ajuste.lockRootHeightY || !ajuste.lockRootPositionXZ ||
                !ajuste.keepOriginalOrientation || !ajuste.keepOriginalPositionY || !ajuste.keepOriginalPositionXZ)
            {
                ajuste.lockRootRotation = ajuste.lockRootHeightY = ajuste.lockRootPositionXZ = true;
                ajuste.keepOriginalOrientation = ajuste.keepOriginalPositionY = ajuste.keepOriginalPositionXZ = true;
                cambiado = true;
            }
        }
        if (!cambiado) return false;
        importer.clipAnimations = ajustes;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return true;
    }

    private static AnimationClip CargarClip(string ruta)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) return clip;
        return null;
    }

    private static AnimatorState Buscar(AnimatorStateMachine maquina, string nombre)
    {
        foreach (var hijo in maquina.states) if (hijo.state.name == nombre) return hijo.state;
        foreach (var hijo in maquina.stateMachines)
        {
            var estado = Buscar(hijo.stateMachine, nombre);
            if (estado != null) return estado;
        }
        return null;
    }

    private static bool AnadirEstado(AnimatorController controller, string nombre, AnimationClip clip, bool completo)
    {
        var capas = controller.layers;
        if (capas.Length == 0) return false;
        int capa = 0;
        if (!completo)
        {
            capa = Array.FindIndex(capas, c => c.name == "UpperBody");
            if (capa < 0) { Debug.LogWarning($"[INC-574] {controller.name} no tiene UpperBody; se omite {nombre}."); return false; }
        }
        var maquina = capas[capa].stateMachine;
        var estado = Buscar(maquina, nombre);
        bool nuevo = estado == null;
        if (nuevo) estado = maquina.AddState(nombre);
        Undo.RecordObject(estado, "Añadir animación de actuación");
        estado.motion = clip;
        if (completo) estado.tag = "ActuacionCuerpoEntero";
        var destino = completo ? Buscar(maquina, "Idle_Normal_NoWeapon") ?? Buscar(maquina, "Free Locomotion") : Buscar(maquina, "UpperIdle");
        if (destino == null) destino = maquina.defaultState;
        if (destino != null && destino != estado)
        {
            AnimatorStateTransition salida = null;
            foreach (var transicion in estado.transitions)
                if (transicion.destinationState == destino && transicion.conditions.Length == 0) { salida = transicion; break; }
            if (salida == null) salida = estado.AddTransition(destino);
            salida.hasExitTime = true;
            salida.exitTime = 1f;
            salida.hasFixedDuration = true;
            salida.duration = 0.2f;
            EditorUtility.SetDirty(salida);
        }
        EditorUtility.SetDirty(estado);
        EditorUtility.SetDirty(maquina);
        EditorUtility.SetDirty(controller);
        Debug.Log($"[INC-574] {controller.name}: {nombre} → {capas[capa].name} ({(nuevo ? "creado" : "actualizado")}).");
        return nuevo;
    }
}
