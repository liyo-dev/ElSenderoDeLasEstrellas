#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Cap. 2, final de «La mañana después»: Eldran sale de casa a la vista dentro de la cinemática,
/// así que al acabar ya espera junto a la casa de Will (INC-962). El grafo lo coloca allí en el nodo 12 y se
/// retiran la espera de «Will sale de casa» y la segunda colocación, que ya no hacen nada.
/// Ver INC-903. Se puede ejecutar varias veces.
public static class Cap2EldranSaleDeCasa
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string NodoColocar = "cap2-manana-despues-12";
    const string NodoCheckpoint = "cap2-manana-despues-15";
    const string NodoFork = "cap2-prepara-viaje-fork";
    static readonly string[] NodosSinUso = { "cap2-manana-espera-salida", "cap2-eldran-a-pueblo" };

    [MenuItem("El Sendero/Archivo/Capítulo 2/Eldran sale de casa en la cinemática (INC-903)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Cap. 2", "Sal del modo Play antes de ejecutarlo.");
            return;
        }

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Grafo)
            ?? throw new InvalidOperationException("No se encuentra " + Grafo);

        var colocar = grafo.FindNode(NodoColocar) as PlaceActorNode
            ?? throw new InvalidOperationException($"Falta el nodo {NodoColocar} en {Grafo}");
        colocar.markId = Cap2EldranYAroINC962.MarcaEldran;
        colocar.showActor = true;
        colocar.displayTitle = "12.- Eldran espera junto a la casa de Will (sale en la cinemática)";

        var retirar = new HashSet<string>(NodosSinUso);
        int quitados = grafo.nodes.RemoveAll(n => n != null && retirar.Contains(n.guid));
        foreach (var n in grafo.nodes)
            n?.outputs?.RemoveAll(g => retirar.Contains(g));

        var checkpoint = grafo.FindNode(NodoCheckpoint)
            ?? throw new InvalidOperationException($"Falta el nodo {NodoCheckpoint} en {Grafo}");
        if (grafo.FindNode(NodoFork) == null)
            throw new InvalidOperationException($"Falta el nodo {NodoFork} en {Grafo}");
        checkpoint.outputs = new List<string> { NodoFork };

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        string texto = $"Eldran se coloca en la plaza al acabar la cinemática; {quitados} nodos retirados (espera de salida y segunda colocación); el checkpoint lleva a los encargos.";
        Debug.Log("[Cap. 2] " + texto);
        OrdenesDelEditor.Avisar("Cap. 2", texto);
    }
}
#endif
