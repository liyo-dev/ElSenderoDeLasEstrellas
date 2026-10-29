using UnityEditor;
using UnityEngine;

/// <summary>Retira la entrega de mermelada sin cambiar el resto de la progresión del capítulo.</summary>
public static class RetirarMermeladaInicial
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";
    private const string NodoEspera = "18a57f1d-20e3-4539-b920-ac49b3750703";
    private const string NodoMermelada = "721fd475-997e-4f7d-b54b-cedcbb5c1c2e";

    [MenuItem("El Sendero/Archivo/Retirar mermelada inicial")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        var espera = grafo != null ? grafo.FindNode(NodoEspera) : null;
        var entrega = grafo != null ? grafo.FindNode(NodoMermelada) as GiveInventoryItemNode : null;
        if (grafo == null || espera == null)
        {
            Debug.LogError("No se encuentra Cap1 o la espera tras PERAS_DONE.");
            return;
        }
        if (entrega == null)
        {
            Debug.Log("La entrega de mermelada ya está retirada.");
            return;
        }
        if (espera.outputs.Count != 1 || espera.outputs[0] != entrega.guid || entrega.outputs.Count != 1)
        {
            Debug.LogError("La conexión de la mermelada ha cambiado; se conserva el grafo para revisarlo.");
            return;
        }

        Undo.RecordObject(grafo, "Retirar mermelada inicial");
        espera.outputs[0] = entrega.outputs[0];
        grafo.nodes.Remove(entrega);
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log("Mermelada inicial retirada de Cap1.");
    }
}
