#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Erika da el grimorio a Will en el Cap. 2: entre «Hablar con Erika» y «Aprender Protección y
/// Bola Prisma» se pone el flag GRIMORIO_RECIBIDO, que es lo que deja ver el libro en la pestaña
/// Hechizos (GrimorioDelPersonaje.TieneGrimorio). Ver INC-961. Se puede ejecutar varias veces.
public static class Cap2ErikaDaElGrimorio
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string HablarConErika = "cap2-prepara-viaje-erika-0";
    const string Aprender = "cap2-prepara-viaje-erika-desbloqueo";
    const string Entrega = "cap2-prepara-viaje-erika-grimorio";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Erika da el grimorio (INC-961)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Cap. 2", "Sal del modo Play antes de ejecutarlo.");
            return;
        }

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Grafo)
            ?? throw new InvalidOperationException("No se encuentra " + Grafo);
        var hablar = grafo.FindNode(HablarConErika) ?? throw new InvalidOperationException("Falta el nodo " + HablarConErika);
        var aprender = grafo.FindNode(Aprender) ?? throw new InvalidOperationException("Falta el nodo " + Aprender);

        var entrega = grafo.FindNode(Entrega) as SetFlagNode;
        if (entrega == null)
        {
            entrega = new SetFlagNode
            {
                guid = Entrega,
                chapter = "Cap. 2",
                position = (hablar.position + aprender.position) * 0.5f + new Vector2(0f, 140f),
            };
            grafo.nodes.Add(entrega);
        }
        entrega.displayTitle = "22b-0.- Erika da el grimorio";
        entrega.flagKey = GrimorioDelPersonaje.FlagGrimorioRecibido;
        entrega.value = true;
        hablar.outputs = new List<string> { Entrega };
        entrega.outputs = new List<string> { Aprender };

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        const string texto = "Grafo Cap. 2: «Hablar con Erika» → «Erika da el grimorio» (GRIMORIO_RECIBIDO) → «Aprender Protección y Bola Prisma».";
        Debug.Log("[Cap. 2] " + texto);
        OrdenesDelEditor.Avisar("Cap. 2", texto);
    }
}
#endif
