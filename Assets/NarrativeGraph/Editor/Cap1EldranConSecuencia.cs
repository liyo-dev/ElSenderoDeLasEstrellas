using UnityEditor;
using UnityEngine;

/// <summary>
/// Cap1, nodo 20c: Eldran viene a pedir la caja con una secuencia de verdad (SEQ_EldranCaja), igual
/// que Oliver en SEQ_OliverSaludo, en vez del nodo «NPC viene a hablarte» (INC-445).
///
/// Se hace por AssetDatabase y no escribiendo el YAML: Unity tiene Cap1 en memoria (INC-441).
/// Idempotente.
/// </summary>
public static class Cap1EldranConSecuencia
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";
    private const string Nodo20c = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13";
    private const string Nodo21 = "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba";
    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_EldranCaja.asset";

    [MenuItem("El Sendero/Archivo/Narrativa/Cap1: Eldran viene a pedir la caja con secuencia (SEQ_EldranCaja)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        if (grafo == null || seq == null)
        {
            Debug.LogError($"[Cap1EldranConSecuencia] Falta {(grafo == null ? Ruta : RutaSecuencia)}.");
            return;
        }

        var viejo = grafo.FindNode(Nodo20c);
        if (viejo == null)
        {
            Debug.LogError("[Cap1EldranConSecuencia] No existe el nodo 20c: ejecuta antes «Cap1: aplicar «Algo inútil»…» (está en El Sendero ▸ Archivo ▸ Narrativa).");
            return;
        }
        if (viejo is PlayCinematicNode r && r.secuencia == seq)
        {
            Debug.Log("[Cap1EldranConSecuencia] = El nodo 20c ya reproduce SEQ_EldranCaja. Nada que hacer.");
            return;
        }

        Undo.RecordObject(grafo, "Cap1: 20c con SEQ_EldranCaja");
        var nuevo = new PlayCinematicNode
        {
            guid = viejo.guid, position = viejo.position, chapter = viejo.chapter, blockSaving = true,
            displayTitle = "20c.- Eldran le llama y viene a pedir la caja (SEQ_EldranCaja)",
            cinematicName = "Eldran viene a pedir la caja",
            signalIn = seq.signalIn, signalDone = seq.signalOut,
            secuencia = seq, plantillaDeAjustes = "SEQ_PerasEldran",
            acercarActor = "NPC_Eldran", distanciaMaxima = 24f, distanciaDeAparicion = 12f,
        };
        nuevo.outputs = new System.Collections.Generic.List<string> { Nodo21 };
        grafo.nodes[grafo.nodes.IndexOf(viejo)] = nuevo;

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cap1EldranConSecuencia] ✓ Nodo 20c → reproduce SEQ_EldranCaja (Eldran llama a Will, se acerca y le pide la caja). " +
                  "Si tienes abierta la ventana del grafo, ciérrala y vuelve a abrirla.");
    }
}
