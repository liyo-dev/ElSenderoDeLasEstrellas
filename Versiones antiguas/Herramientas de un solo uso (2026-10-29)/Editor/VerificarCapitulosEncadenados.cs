#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Comprueba que un capítulo terminado no vuelve a empezar al cargar y que Quick Test
/// da por terminados los capítulos previos del grafo que se prueba.
public static class VerificarCapitulosEncadenados
{
    [MenuItem("El Sendero/Archivo/Narrativa/Verificar capítulos encadenados")]
    public static void Verificar()
    {
        var cap1 = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap1.asset");
        var cap2 = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap2.asset");

        // Quick Test desde el inicio del Cap. 2: Cap. 1 terminado y se salta la espera del puente.
        string objetivo = cap2.startNodeGuid;
        var previos = NarrativeQuickTestWindow.CapitulosPrevios(cap2, ref objetivo);
        Exigir(previos.Count == 1 && previos[0].label == "Cap1", "Cap2 debe tener Cap1 como capítulo previo.");
        Exigir(!(cap2.FindNode(objetivo) is StartNode) && !(cap2.FindNode(objetivo) is WaitCustomEventNode), "El inicio del Cap2 no salta el puente.");
        Debug.Log($"[Capítulos] Quick Test Cap2 · inicio → empieza en '{cap2.FindNode(objetivo).displayTitle}' con {previos[0].label} terminado.");

        // Quick Test dentro del Cap. 1: nada previo y el nodo elegido se respeta.
        objetivo = cap1.startNodeGuid;
        Exigir(NarrativeQuickTestWindow.CapitulosPrevios(cap1, ref objetivo).Count == 0 && objetivo == cap1.startNodeGuid, "Cap1 no debe tener capítulos previos.");

        // Runner: un grafo terminado no vuelve a su inicio al cargar.
        var grafo = ScriptableObject.CreateInstance<NarrativeGraph>();
        var go = new GameObject("Verificación capítulos encadenados");
        go.SetActive(false);
        try
        {
            var inicio = new StartNode { guid = "inicio", outputs = new List<string> { "fin" } };
            var fin = new RaiseCustomEventNode { guid = "fin", eventKey = "VERIFICACION_FIN_CAPITULO", outputs = new List<string>() };
            grafo.nodes = new List<NarrativeNode> { inicio, fin };
            grafo.startNodeGuid = "inicio";
            var signals = go.AddComponent<DefaultNarrativeSignals>();
            var runner = go.AddComponent<NarrativeRunner>();
            runner.graph = grafo;
            runner.SetSignalsProvider(signals);
            runner.StartFromStartNode();
            Exigir(runner.Blackboard.Get<bool>(NarrativeRunner.FlowEndedKey, false), "El final del flujo no se marca.");
            var guardado = runner.Blackboard.ExportToSerializable();
            runner.Blackboard.Clear();
            runner.Blackboard.ImportFromSerializable(guardado);
            runner.StartFromStartNode();
            Exigir(runner.CurrentNode == null && string.IsNullOrEmpty(runner.Blackboard.Get<string>(NarrativeRunner.CurrentNodeKey, null)), "Un grafo terminado vuelve a empezar al cargar.");
            runner.Blackboard.Clear();
            runner.StartFromStartNode();
            Exigir(runner.Blackboard.Get<bool>(NarrativeRunner.FlowEndedKey, false), "Partida nueva: el grafo no arranca desde su inicio.");
            signals.ResetState();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(grafo);
        }
        Debug.Log("[Capítulos] OK: capítulo terminado no se repite al cargar; Quick Test termina los capítulos previos.");
    }

    static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException("[Capítulos] " + mensaje);
    }
}
#endif
