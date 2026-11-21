using Liyodev.NarrativeGraph;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Arreglo de una sola vez (INC-1251): la pieza «Eldran: vuelve a lo de siempre» pasa del inicio de
/// 3.1 al fin de la cinemática de la despedida. Al acabar la película el grafo recibe su señal y es
/// ahí donde vuelve a colocar el reparto; la cinemática solo lo deja fuera de cuadro. Deja el grafo
/// como lo dejará el montaje (ver MontajeDeLosCapitulos.Despedida). Se ejecuta una vez y este
/// archivo se retira a «Versiones antiguas/».
public static class PiezaDeEldranTrasLaDespedida
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap3.asset";
    private const string Cine = "cap3-adios";
    private const string Vieja = "pieza-npc_eldran-backtousual-cap-23-espera-en-la-salida-del-pueblo-cap3-camino";
    private const string Nueva = "pieza-npc_eldran-backtousual-cap-23-espera-en-la-salida-del-pueblo-cap3-adios";

    [MenuItem("El Sendero/Pruebas/Arreglo de una vez: la pieza de Eldran tras la despedida")]
    public static void Ejecutar()
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (g == null) { Debug.LogError($"[INC-1251] No está {RutaGrafo}"); return; }
        var cine = g.FindNode(Cine);
        if (cine == null) { Debug.LogError($"[INC-1251] No está «{Cine}» en {RutaGrafo}"); return; }
        if (g.FindNode(Nueva) != null) { Debug.Log("[INC-1251] Ya está hecha."); return; }

        // 1. Fuera la vieja, empalmando lo que venía detrás.
        if (g.FindNode(Vieja) is ActorStateNode vieja)
        {
            string siguiente = vieja.Outputs != null && vieja.Outputs.Count > 0 ? vieja.Outputs[0] : string.Empty;
            foreach (var n in g.nodes)
            {
                if (n?.Outputs == null) continue;
                for (int i = 0; i < n.Outputs.Count; i++)
                    if (n.Outputs[i] == Vieja) n.Outputs[i] = siguiente;
            }
            g.nodes.Remove(vieja);
        }

        // 2. La nueva, en la salida buena de la cinemática (la primera: «Hecho»).
        var pieza = new ActorStateNode
        {
            Guid = Nueva,
            DisplayTitle = "Eldran: vuelve a lo de siempre (acaba «Cap. 2–3 · espera en la salida del pueblo»)",
            Chapter = cine.Chapter,
            Section = cine.Section,
            Position = cine.Position + new Vector2(40f, 150f),
            Outputs = new List<string>(),
            ActorId = "NPC_Eldran",
            PieceLayer = ActorStateNode.Layer.BackToUsual,
            Presence = ActorPresence.Unchanged,
            MarkId = string.Empty,
            Lines = new List<NarrativeDialogueAsset>(),
        };
        string bueno = cine.Outputs != null && cine.Outputs.Count > 0 ? cine.Outputs[0] : string.Empty;
        if (!string.IsNullOrEmpty(bueno)) pieza.Outputs.Add(bueno);
        cine.Outputs ??= new List<string>();
        if (cine.Outputs.Count > 0) cine.Outputs[0] = Nueva;
        else cine.Outputs.Add(Nueva);
        g.nodes.Add(pieza);

        EditorUtility.SetDirty(g);
        AssetDatabase.SaveAssets();
        Debug.Log($"[INC-1251] Pieza de Eldran: de «{Vieja}» a «{Nueva}», en la salida buena de «{Cine}».");
    }
}
#endif
