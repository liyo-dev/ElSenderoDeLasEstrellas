using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cap1: al entregar la caja, Eldran dice «Toma, unas monedas por tu ayuda» y da las monedas de
/// verdad. Añade un «Dar objeto» (IT_Coin) entre el diálogo de la entrega (27) y el cierre de la
/// misión (28). Ver INC-526.
///
/// Se hace por AssetDatabase y no escribiendo el YAML: Unity tiene Cap1 en memoria (INC-441).
/// Idempotente.
/// </summary>
public static class Cap1MonedasDeEldran
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaMoneda = "Assets/_ITEMS/IT_Coin.asset";
    private const string Nodo27 = "e10bdcbf-059f-40f6-bfcf-7799abe09913";
    private const string Nodo28 = "bfb7330e-0489-4388-bcab-d6305b68a97a";
    private const int Monedas = 20;

    [MenuItem("El Sendero/Narrativa/Cap1: monedas de Eldran al entregar la caja (INC-526)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        var moneda = AssetDatabase.LoadAssetAtPath<ItemData>(RutaMoneda);
        if (grafo == null || moneda == null)
        {
            Debug.LogError($"[Cap1MonedasDeEldran] Falta {(grafo == null ? Ruta : RutaMoneda)}.");
            return;
        }

        var dialogo = grafo.FindNode(Nodo27);
        if (dialogo == null || grafo.FindNode(Nodo28) == null)
        {
            Debug.LogError("[Cap1MonedasDeEldran] No encuentro los nodos 27 (diálogo de la entrega) y 28 (completa ELDRAN_MISSION2).");
            return;
        }

        foreach (var salida in dialogo.outputs)
        {
            if (grafo.FindNode(salida) is GiveInventoryItemNode ya && ya.item == moneda)
            {
                Debug.Log("[Cap1MonedasDeEldran] = El diálogo de la entrega ya va seguido de las monedas. Nada que hacer.");
                return;
            }
        }

        if (dialogo.outputs.Count != 1 || dialogo.outputs[0] != Nodo28)
        {
            Debug.LogError("[Cap1MonedasDeEldran] El nodo 27 ya no sale directo al 28; revisa el grafo a mano antes de insertar nada.");
            return;
        }

        Undo.RecordObject(grafo, "Cap1: monedas de Eldran en la entrega");
        var dar = new GiveInventoryItemNode
        {
            displayTitle = $"27b.- Eldran paga la ayuda (IT_Coin x{Monedas})",
            chapter = dialogo.chapter,
            position = dialogo.position + new Vector2(190f, 160f),
            item = moneda,
            amount = Monedas,
            logWarnings = true,
        };
        dar.outputs = new List<string> { Nodo28 };
        grafo.nodes.Insert(grafo.nodes.IndexOf(dialogo) + 1, dar);
        dialogo.outputs[0] = dar.guid;

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cap1MonedasDeEldran] ✓ Tras «Toma, unas monedas por tu ayuda» Eldran da {Monedas} monedas (nodo 27b). " +
                  "Si tienes abierta la ventana del grafo, ciérrala y vuelve a abrirla.");
    }
}
