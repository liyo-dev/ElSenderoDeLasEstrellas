using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Cap1: todas las quests dicen quién las encarga y qué dice mientras siguen en curso
/// (StartQuestNode · «Mientras está en curso»), o se marcan como propias del jugador.
///  - 10 WILL_MISSION0 (sal a ver qué pasa): propia de Will.
///  - 14b ELDRAN_MISSION1 (busca a Eldran): propia de Will; Oliver va en el grupo.
///  - 19 WILL_MISSION1 (gasta la moneda): Eldran, DLG_ELDRAN_WILL_MISSION1_INPROGRESS (nuevo).
///  - 21 ELDRAN_MISSION2 (la caja): Eldran, DLG_ELDRAN_MISSION2_INPROGRESS (ya existía).
///  - 31 ELDRAN_MISSION3 (el demonio): Eldran, DLG_ELDRAN_MISSION3_INPROGRESS (nuevo).
/// Por AssetDatabase, no editando el YAML (INC-441). Idempotente. Ver INC-514.
public static class Cap1FrasesMientrasTanto
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string CarpetaEldran = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
    private const string Eldran = "NPC_Eldran";
    private const string NombreEldran = "CHAR_ELDRAN";

    [MenuItem("El Sendero/Narrativa/Cap1: frases de mientras tanto en todas las misiones (INC-514)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null) { Debug.LogError($"[Cap1FrasesMientrasTanto] Falta {RutaGrafo}."); return; }

        var informe = new List<string>();
        Undo.RecordObject(grafo, "Cap1: frases de mientras tanto");

        Propia(grafo, "c748956c-7df0-4031-8789-65aab639f278", "WILL_MISSION0", informe);
        Propia(grafo, "aaccfcd4-a7bc-4732-b4b6-86e6c183db03", "ELDRAN_MISSION1", informe);

        var moneda = Dialogo($"{CarpetaEldran}/DLG_ELDRAN_WILL_MISSION1_INPROGRESS.asset",
            ("DLG_ELDRAN_WILL_MISSION1_INPROGRESS_01", NPCEmotion.Happy),
            ("DLG_ELDRAN_WILL_MISSION1_INPROGRESS_02", NPCEmotion.Neutral));
        Encargada(grafo, "a856e804-7ab5-4aaf-a241-774bce56c27f", "WILL_MISSION1", moneda, informe);

        var caja = AssetDatabase.LoadAssetAtPath<DialogueAsset>($"{CarpetaEldran}/MISION2/DLG_ELDRAN_MISSION2_INPROGRESS.asset");
        Encargada(grafo, "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba", "ELDRAN_MISSION2", caja, informe);

        var demonio = Dialogo($"{CarpetaEldran}/MISION3/DLG_ELDRAN_MISSION3_INPROGRESS.asset",
            ("DLG_ELDRAN_MISSION3_INPROGRESS_01", NPCEmotion.Scared));
        Encargada(grafo, "d1785c09-9503-4d7b-a24e-682424ca83a7", "ELDRAN_MISSION3", demonio, informe);

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        // Lo que quede sin frase en el grafo, por si hay más quests de las que este menú conoce.
        var avisos = new List<string>();
        foreach (var n in grafo.nodes)
        {
            if (n is not StartQuestNode) continue;
            var propios = new List<string>();
            n.CollectWarnings(propios);
            foreach (var a in propios) avisos.Add($"«{n.displayTitle}»: {a}");
        }

        Debug.Log("[Cap1FrasesMientrasTanto] ✓ Hecho:\n- " + string.Join("\n- ", informe) +
                  (avisos.Count == 0 ? "\nTodas las quests de Cap1 tienen quién las encarga y su frase (o son propias de Will)."
                                     : "\n⚠ Siguen sin frase:\n- " + string.Join("\n- ", avisos)));
    }

    private static void Propia(NarrativeGraph grafo, string guid, string questId, List<string> informe)
    {
        if (!Nodo(grafo, guid, questId, informe, out var nodo)) return;
        nodo.noGiver = true;
        nodo.giverId = "";
        nodo.inProgressDialogue = null;
        informe.Add($"{questId}: propia de Will (nadie la encarga).");
    }

    private static void Encargada(NarrativeGraph grafo, string guid, string questId, DialogueAsset dialogo, List<string> informe)
    {
        if (dialogo == null) { informe.Add($"⚠ {questId}: no encuentro su diálogo, no se ha tocado."); return; }
        if (!Nodo(grafo, guid, questId, informe, out var nodo)) return;
        nodo.noGiver = false;
        nodo.giverId = Eldran;
        nodo.inProgressDialogue = dialogo;
        informe.Add($"{questId}: Eldran dice {dialogo.name}.");
    }

    private static bool Nodo(NarrativeGraph grafo, string guid, string questId, List<string> informe, out StartQuestNode nodo)
    {
        nodo = grafo.FindNode(guid) as StartQuestNode;
        if (nodo != null && nodo.questId == questId) return true;
        informe.Add($"⚠ No encuentro el «Iniciar quest» de {questId}: no se ha tocado.");
        nodo = null;
        return false;
    }

    /// Crea el diálogo si no existe y le pone sus líneas (textos en dialogues_es/en.json).
    private static DialogueAsset Dialogo(string ruta, params (string textId, NPCEmotion emocion)[] lineas)
    {
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            string carpeta = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(carpeta))
            {
                string padre = System.IO.Path.GetDirectoryName(carpeta).Replace('\\', '/');
                AssetDatabase.CreateFolder(padre, System.IO.Path.GetFileName(carpeta));
            }
            dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogo, ruta);
        }

        var nuevas = new DialogueLine[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            nuevas[i] = new DialogueLine { speakerNameId = NombreEldran, textId = lineas[i].textId, emotion = lineas[i].emocion };
        dialogo.lines = nuevas;
        EditorUtility.SetDirty(dialogo);
        return dialogo;
    }
}
