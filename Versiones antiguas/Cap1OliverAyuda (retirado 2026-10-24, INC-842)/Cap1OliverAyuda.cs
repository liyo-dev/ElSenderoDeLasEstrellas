using UnityEditor;
using UnityEngine;

/// <summary>
/// Cap1: Oliver se queda ayudando en el mercado hasta que Will compra la estrella. Hoy el grafo
/// lo mete en el party antes de la discusión de las peras (14c), así que se va con Will y nunca
/// llega a usar su ruta de oficio. Este menú mueve el evento OLIVER_JOIN_PARTY después de
/// completar WILL_MISSION1 (20b), justo antes de que Eldran le llame para ir al bosque (20c).
///
/// Es idempotente y valida el cableado actual antes de tocar nada. Se hace por AssetDatabase
/// porque el grafo está en memoria en el Editor (INC-441). Ver INC-837.
/// </summary>
public static class Cap1OliverAyuda
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";

    private const string Nodo14b = "aaccfcd4-a7bc-4732-b4b6-86e6c183db03"; // Busca a Eldran
    private const string Nodo14c = "486dfd45-dc63-438c-ae05-f2f8a045b36a"; // Oliver se une al grupo
    private const string Nodo14d = "36c16794-69b8-4e90-bb85-dc80d58b76ae"; // Tutorial minimapa
    private const string Nodo20b = "f393b1c0-87f6-4793-a705-a151a01af989"; // Completa WILL_MISSION1
    private const string Nodo20c = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13"; // Eldran le llama

    [MenuItem("El Sendero/Narrativa/Cap1: Oliver se une después de comprar la estrella (INC-837)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        if (grafo == null)
        {
            Debug.LogError($"[Cap1OliverAyuda] Falta {Ruta}.");
            return;
        }

        var n14b = grafo.FindNode(Nodo14b);
        var n14c = grafo.FindNode(Nodo14c);
        var n14d = grafo.FindNode(Nodo14d);
        var n20b = grafo.FindNode(Nodo20b);
        var n20c = grafo.FindNode(Nodo20c);
        if (n14b == null || n14c == null || n14d == null || n20b == null || n20c == null)
        {
            Debug.LogError("[Cap1OliverAyuda] No encuentro los nodos 14b, 14c, 14d, 20b y 20c. Revisa el grafo.");
            return;
        }

        if (n20b.outputs.Contains(Nodo14c) && n14c.outputs.Contains(Nodo20c))
        {
            Debug.Log("[Cap1OliverAyuda] = Oliver ya se une después de la estrella. Nada que hacer.");
            return;
        }

        if (!n14b.outputs.Contains(Nodo14c) || !n14c.outputs.Contains(Nodo14d) ||
            !n20b.outputs.Contains(Nodo20c) || n14c.outputs.Count != 1 || n20b.outputs.Count != 1)
        {
            Debug.LogError("[Cap1OliverAyuda] El cableado del grafo ya no es el esperado. " +
                           "Revisa 14b→14c→14d y 20b→20c a mano antes de mover nada.");
            return;
        }

        Undo.RecordObject(grafo, "Cap1: Oliver se une después de la estrella");
        n14b.outputs.Remove(Nodo14c);
        if (!n14b.outputs.Contains(Nodo14d)) n14b.outputs.Add(Nodo14d);

        n14c.outputs.Clear();
        n14c.outputs.Add(Nodo20c);
        n14c.position = n20b.position + new Vector2(190f, 0f);

        n20b.outputs.Clear();
        n20b.outputs.Add(Nodo14c);

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        Debug.Log("[Cap1OliverAyuda] ✓ Oliver se queda en el mercado ayudando con las cajas hasta " +
                  "que Will compra la estrella; después se une al grupo y Eldran le llama. " +
                  "Si tienes abierta la ventana del grafo, ciérrala y vuelve a abrirla.");
    }
}
