using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Cap1, la caja de Eldran (26 sep 2026, Raúl):
///  - «volvemos a mostrar el prompt de tutorial para ir a por la caja; el jugador ya sabe lo del
///    mapa porque lo hicimos antes, esto se debe quitar» → fuera el nodo 22 (21 → 23).
///  - «cuando entregamos la caja, la admiración debe pasar a interrogación» → el nodo 26 (esperar
///    a que hables con Eldran para la entrega) lleva el icono «?» (Canvas What!, el mismo que usan
///    todas las misiones para «ven a entregar»), en vez de la «!» por defecto del NPC. El aviso 25
///    («cuando veas este icono…») enseña ese icono, no el botón del mapa que tenía.
///  - «hay que quitar del diálogo del punto de guardado la parte de Oliver, que al final no
///    viene» → fuera DLG_OLIVER_SAVEPOINT_01 de DG_ELDRAN_PUNTO_GUARDADO (Oliver sale del grupo en
///    PERAS_START y no vuelve en el Cap. 1).
/// Por AssetDatabase, no editando el YAML (INC-441). Idempotente. Ver INC-467/468 en TRACKER.
public static class Cap1CajaDeEldranAjustes
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaDialogoParada = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DG_ELDRAN_PUNTO_GUARDADO.asset";
    private const string RutaIconoHablar = "Assets/Art/UI/NpcStatus/what.png";
    private const string RutaIconoHablarPrefab = "Assets/_NPCs/OverHead/Canvas What!.prefab";

    private const string Nodo21 = "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba";
    private const string Nodo22 = "0d50f50f-4be3-434d-a6b4-fa6ce0694d8f";
    private const string Nodo23 = "0cc20edd-5f8a-48c6-a9c5-38685774b09a";
    private const string Nodo25 = "da4a9b32-670c-4724-ab44-6743e2c728d1";
    private const string Nodo26 = "a48b6d65-8a01-4e7f-91b5-03ec8a38ae6d";

    [MenuItem("El Sendero/Narrativa/Cap1: caja de Eldran (sin tutorial del mapa, «?» al entregarla, parada sin Oliver)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null) { Debug.LogError($"[Cap1CajaDeEldranAjustes] Falta {RutaGrafo}."); return; }

        var informe = new List<string>();
        Undo.RecordObject(grafo, "Cap1: caja de Eldran");

        QuitarTutorialDelMapa(grafo, informe);
        IconoAlEntregar(grafo, informe);

        EditorUtility.SetDirty(grafo);
        QuitarOliverDeLaParada(informe);
        AssetDatabase.SaveAssets();

        Debug.Log("[Cap1CajaDeEldranAjustes] ✓ Hecho:\n- " + string.Join("\n- ", informe));
    }

    private static void QuitarTutorialDelMapa(NarrativeGraph grafo, List<string> informe)
    {
        var n22 = grafo.FindNode(Nodo22);
        if (n22 == null) { informe.Add("El tutorial del mapa de la caja (22) ya no estaba."); return; }

        string despues = n22.GetOutputGuid(0) ?? Nodo23;
        foreach (var n in grafo.nodes)
        {
            if (n?.outputs == null) continue;
            for (int i = 0; i < n.outputs.Count; i++)
                if (n.outputs[i] == Nodo22) n.outputs[i] = despues;
        }
        grafo.nodes.Remove(n22);
        informe.Add("Quitado «22.- Tutorial minimapa (caja de Eldran)»: 21 → 23.");
    }

    private static void IconoAlEntregar(NarrativeGraph grafo, List<string> informe)
    {
        // El icono de la cabeza lo pone el grafo (WaitNpcInteractionNode → NarrativeActor), no el
        // NPCQuestConfig congelado. El 26 («habla con Eldran para entregar») no tenía icono propio y
        // usaba el de por defecto del NPC, la «!» de «tengo algo para ti».
        if (grafo.FindNode(Nodo26) is WaitNpcInteractionNode esperar)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaIconoHablarPrefab);
            if (prefab != null)
            {
                esperar.questIcon = prefab;
                informe.Add("26 (habla con Eldran para entregar la caja): icono «?» en vez de la «!».");
            }
            else informe.Add($"⚠ No encuentro {RutaIconoHablarPrefab}: el 26 no se ha tocado.");
        }
        else informe.Add("⚠ No está el nodo 26: no se ha tocado el icono de la entrega.");

        if (grafo.FindNode(Nodo25) is TutorialPromptNode aviso)
        {
            var icono = AssetDatabase.LoadAssetAtPath<Sprite>(RutaIconoHablar);
            if (icono != null)
            {
                aviso.icon = icono;
                aviso.buttonName = "";   // antes enseñaba el botón del mapa, que no pinta nada aquí
                informe.Add("El aviso 25 («cuando veas este icono…») enseña el icono «?», no el botón del mapa.");
            }
            else informe.Add($"⚠ No encuentro {RutaIconoHablar}: el aviso 25 no se ha tocado.");
        }
    }

    private static void QuitarOliverDeLaParada(List<string> informe)
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(RutaDialogoParada);
        if (d == null || d.lines == null) { informe.Add("⚠ No está DG_ELDRAN_PUNTO_GUARDADO."); return; }
        if (!d.lines.Any(l => l.textId == "DLG_OLIVER_SAVEPOINT_01")) { informe.Add("La parada ya no tenía a Oliver."); return; }

        Undo.RecordObject(d, "Parada sin Oliver");
        d.lines = d.lines.Where(l => l.textId != "DLG_OLIVER_SAVEPOINT_01").ToArray();
        EditorUtility.SetDirty(d);
        informe.Add("Quitada la réplica de Oliver de DG_ELDRAN_PUNTO_GUARDADO (sale del grupo en PERAS_START).");
    }
}
