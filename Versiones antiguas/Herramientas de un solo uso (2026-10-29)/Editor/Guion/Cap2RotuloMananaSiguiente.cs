#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// Pone el rótulo «A la mañana siguiente…» entre la escena de Liam y la mañana en casa de Will.
public static class Cap2RotuloMananaSiguiente
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string Antes = "cap2-manana-despues-10";
    const string Despues = "cap2-manana-despues-11";
    const string Id = "cap2-rotulo-manana-siguiente";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Rótulo A la mañana siguiente")]
    public static void Montar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Grafo) ?? throw new InvalidOperationException("Falta " + Grafo);
        var antes = grafo.FindNode(Antes) ?? throw new InvalidOperationException("Falta el nodo 10.");
        var despues = grafo.FindNode(Despues) ?? throw new InvalidOperationException("Falta el nodo 11.");

        var rotulo = new TitleCardNode
        {
            guid = Id,
            displayTitle = "10b.- Rótulo · A la mañana siguiente…",
            chapter = "Cap. 2",
            textId = "ROTULO_CAP2_MANANA_SIGUIENTE",
            text = "A la mañana siguiente…",
            fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Plugins/Fonts/Nunito-Bold SDF.asset"),
            position = (antes.position + despues.position) * 0.5f + new Vector2(0f, -160f),
            outputs = new List<string> { despues.guid },
            blockSaving = true
        };
        var anterior = grafo.FindNode(Id);
        if (anterior == null) grafo.nodes.Add(rotulo); else grafo.nodes[grafo.nodes.IndexOf(anterior)] = rotulo;
        antes.outputs = new List<string> { Id };

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        if (grafo.FindNode(Antes).outputs.Single() != Id || grafo.FindNode(Id).outputs.Single() != Despues)
            throw new InvalidOperationException("El rótulo no quedó enlazado entre 10 y 11.");
        Debug.Log("[Cap2Rotulo] Rótulo «A la mañana siguiente…» entre los nodos 10 y 11.");
    }
}
#endif
