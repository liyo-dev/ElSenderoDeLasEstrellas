#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// La mañana después usaba la luz de «Amanecer», que dentro de casa queda casi a oscuras: pasa a la de día.
public static class Cap2MananaMasClara
{
    [MenuItem("El Sendero/Archivo/Capítulo 2/Mañana con luz de día")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap2.asset") ?? throw new InvalidOperationException("Falta Cap2");
        var nodo = grafo.nodes.OfType<SetTimeOfDayNode>().Single(n => n.guid == "c07008ca-ae29-4d50-848f-8c80e0dfa16a");
        nodo.targetTime = DayNightCycle.TimeOfDay.AfterNoon;
        nodo.displayTitle = "7.- Ya es de día";
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cap2] La mañana después, con luz de día.");
    }
}
#endif
