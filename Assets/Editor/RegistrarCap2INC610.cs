#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sendero.Narrative.Editor;

// Migración de datos del juego; no forma parte del núcleo reutilizable del grafo.
public static class RegistrarCap2INC610
{
    const string Ruta = "Assets/NarrativeGraph/Cap2.asset";
    const string Puente = "CH_Cap1_BACK_1";

    [MenuItem("El Sendero/Archivo/Narrativa/Registrar esqueleto Cap2 INC-610")]
    public static void Registrar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ejecutar fuera de Play.");
        var previo = CrossSystemNarrativeValidator.Validate();
        var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        if (graph == null)
        {
            graph = ScriptableObject.CreateInstance<NarrativeGraph>();
            var inicio = new StartNode { displayTitle = "0.- Inicio", chapter = "Cap. 2", position = new Vector2(0, 0) };
            var espera = new WaitCustomEventNode { displayTitle = "1.- Esperar fin del Cap. 1", chapter = "Cap. 2", eventKey = Puente, position = new Vector2(320, 0) };
            var checkpoint = new CheckpointNode { displayTitle = "2.- Cap. 2 · Inicio (tras el Demonio)", chapter = "Cap. 2", checkpointId = "CAP2_INICIO_TRAS_DEMONIO", sceneName = "MainWorld", spawnAnchorId = "Eldran_PuntoGuardado", position = new Vector2(640, 0) };
            inicio.outputs.Add(espera.guid);
            espera.outputs.Add(checkpoint.guid);
            graph.nodes = new List<NarrativeNode> { inicio, espera, checkpoint };
            graph.startNodeGuid = inicio.guid;
            AssetDatabase.CreateAsset(graph, Ruta);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }
        ComprobarEstructura(graph);
        EditarEscena("Assets/Scenes/Systems/Start.unity", scene =>
        {
            var hubs = Componentes<NarrativeGraphHub>(scene).ToArray();
            Exigir(hubs.Length == 1, "Start debe contener un único Hub.");
            var so = new SerializedObject(hubs[0]);
            var slots = so.FindProperty("graphs");
            int indice = -1;
            bool cap1 = false;
            for (int i = 0; i < slots.arraySize; i++)
            {
                string label = slots.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue;
                if (label == "Cap1") cap1 = true;
                if (label != "Cap2") continue;
                Exigir(indice < 0, "Hay etiquetas Cap2 duplicadas.");
                indice = i;
            }
            Exigir(cap1, "Falta el slot Cap1 de referencia.");
            if (indice >= 0)
            {
                Exigir(slots.GetArrayElementAtIndex(indice).FindPropertyRelative("graph").objectReferenceValue == graph, "Cap2 apunta a otro asset.");
                return false;
            }
            indice = slots.arraySize++;
            var slot = slots.GetArrayElementAtIndex(indice);
            slot.FindPropertyRelative("label").stringValue = "Cap2";
            slot.FindPropertyRelative("graph").objectReferenceValue = graph;
            slot.FindPropertyRelative("initialBlackboardValues").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hubs[0]);
            return true;
        });
        // Esta es la escena que inicia Cap1; se conserva el mismo mecanismo para Cap2.
        EditarEscena("Assets/Scenes/Worlds/MainWorld.unity", scene =>
        {
            bool cambio = false;
            foreach (var starter in Componentes<NarrativeGraphStarter>(scene))
            {
                var so = new SerializedObject(starter);
                var labels = so.FindProperty("graphLabels");
                var valores = Enumerable.Range(0, labels.arraySize).Select(i => labels.GetArrayElementAtIndex(i).stringValue).ToArray();
                if (!valores.Contains("Cap1") || valores.Contains("Cap2")) continue;
                int i = labels.arraySize++;
                labels.GetArrayElementAtIndex(i).stringValue = "Cap2";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(starter);
                cambio = true;
            }
            Exigir(Componentes<NarrativeGraphStarter>(scene).Any(s =>
            {
                var p = new SerializedObject(s).FindProperty("graphLabels");
                return Enumerable.Range(0, p.arraySize).Any(i => p.GetArrayElementAtIndex(i).stringValue == "Cap2");
            }), "Cap2 no tiene arranque en MainWorld.");
            return cambio;
        });
        var posterior = CrossSystemNarrativeValidator.Validate();
        Exigir(!posterior.Errors.Except(previo.Errors).Any(), "El validador cruzado detecta errores nuevos.");
        Debug.Log($"[INC-610] Interactive vs Grafo: {previo.Errors.Count} → {posterior.Errors.Count} errores; {previo.Warnings.Count} → {posterior.Warnings.Count} avisos.");
        Verificar();
    }

    [MenuItem("El Sendero/Archivo/Narrativa/Verificar esqueleto Cap2 INC-610")]
    public static void Verificar()
    {
        var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        ComprobarEstructura(graph);
        var validacion = NarrativeGraphValidator.ValidateGraph(graph);
        validacion.LogResults("Cap2");
        Exigir(validacion.IsValid, "Cap2 no supera el validador.");
        // Objetos aislados e inactivos: las pruebas no registran servicios ni arrancan escenas.
        var go = new GameObject("Verificación aislada INC-610");
        go.SetActive(false);
        try
        {
            var signals = go.AddComponent<DefaultNarrativeSignals>();
            var runner = go.AddComponent<NarrativeRunner>();
            runner.graph = graph;
            runner.SetSignalsProvider(signals);
            var wait = (WaitCustomEventNode)graph.nodes[1];
            var checkpoint = (CheckpointNode)graph.nodes[2];
            signals.RaiseCustom(Puente);
            signals.ResetState(preservePending: true);
            runner.StartFromStartNode();
            Exigir(runner.CurrentNode == checkpoint, "Se pierde la señal anterior al arranque.");
            signals.ResetState();
            runner.Blackboard.Clear();
            runner.StartFromStartNode();
            Exigir(runner.CurrentNode == wait, "Cap2 no espera el puente.");
            var hub = go.AddComponent<NarrativeGraphHub>();
            var diccionario = (Dictionary<string, NarrativeRunner>)typeof(NarrativeGraphHub).GetField("_runnersByLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hub);
            var principal = go.AddComponent<NarrativeRunner>();
            principal.graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap1.asset");
            diccionario.Add("Cap1", principal);
            diccionario.Add("Cap2", runner);
            var bb = new SimpleBlackboard();
            bb.Set("__currentNodeGuid", principal.graph.startNodeGuid);
            bb.Set("INC610_COMPROBACION", 37);
            hub.RestoreBlackboards(new List<PlayerSaveData.NarrativeBlackboardSnapshot> { new PlayerSaveData.NarrativeBlackboardSnapshot { graphLabel = "Cap1", blackboardData = bb.ExportToSerializable() } });
            Exigir(principal.Blackboard.Get<int>("INC610_COMPROBACION", 0) == 37, "El snapshot antiguo de Cap1 no se restaura.");
            Exigir(runner.Blackboard.Get<string>("__currentNodeGuid", null) == wait.guid, "El snapshot de Cap1 altera Cap2.");
            var capturados = hub.CaptureBlackboards();
            Exigir(capturados.Any(s => s.graphLabel == "Cap1") && capturados.Any(s => s.graphLabel == "Cap2"), "Falta un grafo en la captura.");
            signals.RaiseCustom(Puente);
            Exigir(runner.CurrentNode == checkpoint, "Se pierde la señal con listener activo.");
            runner.Blackboard.Clear();
            runner.Blackboard.Set("__currentNodeGuid", checkpoint.guid);
            runner.StartFromStartNode();
            Exigir(runner.CurrentNode == checkpoint, "No se puede iniciar desde el checkpoint.");
            Debug.Log("[INC-610] OK: señal anticipada y en vivo; snapshot antiguo Cap1 sin Cap2; captura por etiquetas; inicio directo desde checkpoint.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void ComprobarEstructura(NarrativeGraph g)
    {
        Exigir(g != null && g.nodes.Count == 3, "El esqueleto debe tener tres nodos.");
        Exigir(g.nodes[0] is StartNode && g.nodes[1] is WaitCustomEventNode && g.nodes[2] is CheckpointNode, "Tipos inesperados.");
        Exigir(g.startNodeGuid == g.nodes[0].guid && g.nodes[0].outputs.SequenceEqual(new[] { g.nodes[1].guid }) && g.nodes[1].outputs.SequenceEqual(new[] { g.nodes[2].guid }) && g.nodes[2].outputs.Count == 0, "Enlaces inesperados.");
        Exigir(g.nodes.All(n => n.chapter == "Cap. 2") && ((WaitCustomEventNode)g.nodes[1]).eventKey == Puente, "Capítulo o puente incorrecto.");
    }

    static IEnumerable<T> Componentes<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    static void EditarEscena(string ruta, Func<Scene, bool> editar)
    {
        var scene = SceneManager.GetSceneByPath(ruta);
        bool abierta = scene.IsValid() && scene.isLoaded;
        if (!abierta) scene = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        try
        {
            if (!editar(scene)) return;
            EditorSceneManager.MarkSceneDirty(scene);
            Exigir(EditorSceneManager.SaveScene(scene), "No se pudo guardar " + ruta);
        }
        finally { if (!abierta) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException(mensaje);
    }
}
#endif
