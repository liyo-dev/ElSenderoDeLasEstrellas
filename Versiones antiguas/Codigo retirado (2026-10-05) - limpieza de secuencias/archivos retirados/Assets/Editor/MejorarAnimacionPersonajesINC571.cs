using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Ajusta transiciones sociales e idles sin alterar combate, máscaras ni tiempos de salida.
public static class MejorarAnimacionPersonajesINC571
{
    private const string ParametroIdle = "VariacionIdle";
    private static readonly string[] RutasControllers =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller"
    };
    private static readonly string[] VariantesIdle = { "Idle02", "Idle03", "Idle02_NoWeapon", "Idle03_NoWeapon" };

    [MenuItem("El Sendero/Archivo/Animación/Aplicar mejora de personajes INC-571")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        int transiciones = 0, idles = 0, prefabs = 0;
        foreach (string ruta in RutasControllers)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
            if (controller == null) continue;
            Undo.RegisterCompleteObjectUndo(controller, "Mejorar animación de personajes");
            bool parametro = false;
            bool parametroCompatible = true;
            foreach (var p in controller.parameters)
                if (p.name == ParametroIdle)
                {
                    parametro = true;
                    parametroCompatible = p.type == AnimatorControllerParameterType.Float;
                }
            if (!parametro)
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = ParametroIdle, type = AnimatorControllerParameterType.Float, defaultFloat = 1f
                });
            foreach (var layer in controller.layers)
                AjustarEstados(layer.stateMachine, parametroCompatible, ref transiciones, ref idles);
            EditorUtility.SetDirty(controller);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Model ||
                prefab.GetComponentInChildren<NPCSimpleAnimator>(true) == null) continue;
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool cambiado = false;
                foreach (var npc in raiz.GetComponentsInChildren<NPCSimpleAnimator>(true))
                {
                    var animator = npc.GetComponent<Animator>();
                    if (animator == null) continue;
                    RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
                    while (runtime is AnimatorOverrideController variante) runtime = variante.runtimeAnimatorController;
                    if (!(runtime is AnimatorController controller)) continue;
                    var estados = new HashSet<string>();
                    if (controller.layers.Length == 0) continue;
                    RecogerEstados(controller.layers[0].stateMachine, estados);
                    var so = new SerializedObject(npc);
                    var propiedad = so.FindProperty("idleVariationStates");
                    var validas = new List<string>();
                    for (int i = 0; i < propiedad.arraySize; i++)
                    {
                        string nombre = propiedad.GetArrayElementAtIndex(i).stringValue;
                        if (estados.Contains(nombre) && !validas.Contains(nombre)) validas.Add(nombre);
                    }
                    // Solo sustituye configuraciones que no pueden reproducir ninguna variante.
                    if (validas.Count != 0) continue;
                    foreach (string nombre in VariantesIdle)
                        if (estados.Contains(nombre)) validas.Add(nombre);
                    if (validas.Count == 0) continue;
                    propiedad.arraySize = validas.Count;
                    for (int i = 0; i < validas.Count; i++) propiedad.GetArrayElementAtIndex(i).stringValue = validas[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(npc);
                    cambiado = true;
                }
                if (cambiado)
                {
                    EditorUtility.SetDirty(raiz);
                    PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                    prefabs++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Animación de personajes",
            $"Fundidos corregidos: {transiciones}. Idles preparados: {idles}. Prefabs con variantes corregidas: {prefabs}.\n" +
            "Para completar Eye09 donde falte, ejecuta El Sendero/Diálogos/Completar caras de todos los personajes.\n" +
            "Revisa los cambios y prueba conversación, poses, pausa y combate en Play.", "Vale");
    }

    private static bool EsIdle(string nombre)
    {
        if (nombre == "Idle_Normal_NoWeapon") return true;
        foreach (string variante in VariantesIdle) if (nombre == variante) return true;
        return false;
    }
    private static bool EsSocial(string nombre)
        => nombre.StartsWith("Talk", System.StringComparison.Ordinal) ||
           nombre.StartsWith("Greeting", System.StringComparison.Ordinal) ||
           nombre.StartsWith("HandWave", System.StringComparison.Ordinal) ||
           nombre.StartsWith("HeadNod", System.StringComparison.Ordinal) ||
           nombre.StartsWith("HeadShake", System.StringComparison.Ordinal);

    private static void AjustarEstados(AnimatorStateMachine maquina, bool parametroCompatible,
        ref int transiciones, ref int idles)
    {
        foreach (var hijo in maquina.states)
        {
            var estado = hijo.state;
            if (parametroCompatible && EsIdle(estado.name) &&
                (!estado.speedParameterActive || estado.speedParameter == ParametroIdle))
            {
                if (!estado.speedParameterActive || estado.speedParameter != ParametroIdle)
                {
                    Undo.RecordObject(estado, "Variar velocidad de idle");
                    estado.speedParameter = ParametroIdle;
                    estado.speedParameterActive = true;
                    EditorUtility.SetDirty(estado);
                    idles++;
                }
            }
            if (!EsSocial(estado.name) && !EsIdle(estado.name)) continue;
            foreach (var transicion in estado.transitions)
            {
                // Las salidas sin destino, AnyState y combate conservan sus reglas originales.
                string destino = transicion.destinationState != null ? transicion.destinationState.name : null;
                if (transicion.isExit || transicion.duration > 0f || destino == null ||
                    !(EsSocial(destino) || EsIdle(destino) || destino == "UpperIdle" || destino == "Free Locomotion")) continue;
                Undo.RecordObject(transicion, "Suavizar transición social");
                transicion.hasFixedDuration = true;
                transicion.duration = 0.2f;
                EditorUtility.SetDirty(transicion);
                transiciones++;
            }
        }
        foreach (var sub in maquina.stateMachines)
            AjustarEstados(sub.stateMachine, parametroCompatible, ref transiciones, ref idles);
    }
    private static void RecogerEstados(AnimatorStateMachine maquina, HashSet<string> nombres)
    {
        foreach (var hijo in maquina.states) nombres.Add(hijo.state.name);
        foreach (var sub in maquina.stateMachines) RecogerEstados(sub.stateMachine, nombres);
    }
}
