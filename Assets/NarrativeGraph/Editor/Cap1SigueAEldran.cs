using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Cap1: el «Ven, sígueme» de Eldran pasa a ser una misión, y por el camino charlan.
///  - 20c2: empieza ELDRAN_SIGUEME «Sigue a Eldran» (la encarga Eldran; mientras tanto dice
///    DLG_ELDRAN_SIGUEME_INPROGRESS). INC-535.
///  - 20d: de camino al punto de guardado hablan de la discusión con Victoria
///    (DG_ELDRAN_CHARLA_VICTORIA). 20d3: de camino al bosque, de la estrella que ha comprado Will
///    (DG_ELDRAN_CHARLA_ESTRELLA). INC-537.
///  - 20e2: cuando Eldran ya ha dicho lo que quería (la caja), se completa ELDRAN_SIGUEME.
/// Crea la quest y los diálogos si no existen (textos en quests_/dialogues_es/en.json) y añade la
/// quest al catálogo del QuestManager de las escenas abiertas. Por AssetDatabase, no editando el
/// YAML (INC-441). Idempotente.
public static class Cap1SigueAEldran
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaQuest = "Assets/_QUEST/PRINCIPALES/PRINCIPALES/Q_ELDRAN_SIGUEME.asset";
    private const string CarpetaEldran = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
    private const string QuestId = "ELDRAN_SIGUEME";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";

    private const string Nodo20c  = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13";
    private const string Nodo20d  = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e01";
    private const string Nodo20d3 = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e04";
    private const string Nodo20e  = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e02";
    private const string Nodo21   = "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba";

    private const string Eldran = "CHAR_ELDRAN", Will = "CHAR_WILL", Oliver = "CHAR_OLIVER";

    [MenuItem("El Sendero/Narrativa/Cap1: misión «Sigue a Eldran» y charla por el camino (INC-535, INC-537)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null) { Debug.LogError($"[Cap1SigueAEldran] Falta {RutaGrafo}."); return; }

        var n20c = grafo.FindNode(Nodo20c);
        var n20d = grafo.FindNode(Nodo20d) as GuiarJugadorNode;
        var n20d3 = grafo.FindNode(Nodo20d3) as GuiarJugadorNode;
        var n20e = grafo.FindNode(Nodo20e);
        if (n20c == null || n20d == null || n20d3 == null || n20e == null || grafo.FindNode(Nodo21) == null)
        {
            Debug.LogError("[Cap1SigueAEldran] No encuentro los nodos 20c, 20d, 20d3, 20e y 21 del «Ven, sígueme». Revisa el grafo.");
            return;
        }

        var informe = new List<string>();
        Quest(informe);

        var mientras = Dialogo($"{CarpetaEldran}/DLG_ELDRAN_SIGUEME_INPROGRESS.asset",
            (Eldran, "DLG_ELDRAN_SIGUEME_INPROGRESS_01", NPCEmotion.Happy));
        var charlaVictoria = Dialogo($"{CarpetaEldran}/DG_ELDRAN_CHARLA_VICTORIA.asset",
            (Will,   "DLG_ELDRAN_CHARLA_VICTORIA_01", NPCEmotion.Neutral),
            (Eldran, "DLG_ELDRAN_CHARLA_VICTORIA_02", NPCEmotion.Neutral),
            (Oliver, "DLG_ELDRAN_CHARLA_VICTORIA_03", NPCEmotion.Happy));
        var charlaEstrella = Dialogo($"{CarpetaEldran}/DG_ELDRAN_CHARLA_ESTRELLA.asset",
            (Eldran, "DLG_ELDRAN_CHARLA_ESTRELLA_01", NPCEmotion.Neutral),
            (Will,   "DLG_ELDRAN_CHARLA_ESTRELLA_02", NPCEmotion.Neutral),
            (Eldran, "DLG_ELDRAN_CHARLA_ESTRELLA_03", NPCEmotion.Happy));

        Undo.RecordObject(grafo, "Cap1: misión «Sigue a Eldran» y charla por el camino");

        // 20c → 20c2 (empieza la misión) → 20d
        string tras20c = n20c.outputs.Count == 1 ? n20c.outputs[0] : null;
        if (grafo.FindNode(tras20c) is StartQuestNode ya && ya.questId == QuestId)
        {
            ya.giverId = "NPC_Eldran";
            ya.noGiver = false;
            ya.inProgressDialogue = mientras;
            informe.Add("= 20c2 ya empieza ELDRAN_SIGUEME.");
        }
        else if (tras20c == Nodo20d)
        {
            var empieza = new StartQuestNode
            {
                displayTitle = "20c2.- SIGUE A ELDRAN A VER QUÉ QUIERE (ELDRAN_SIGUEME)",
                chapter = n20c.chapter,
                position = n20c.position + new Vector2(190f, 160f),
                questId = QuestId,
                giverId = "NPC_Eldran",
                inProgressDialogue = mientras,
            };
            empieza.outputs = new List<string> { Nodo20d };
            grafo.nodes.Insert(grafo.nodes.IndexOf(n20c) + 1, empieza);
            n20c.outputs[0] = empieza.guid;
            informe.Add("✓ 20c2: empieza ELDRAN_SIGUEME cuando Eldran dice «Ven, sígueme».");
        }
        else informe.Add("⚠ 20c ya no sale directo a 20d: no se ha insertado el inicio de la misión.");

        // 20e → 20e2 (completa la misión) → 21
        string tras20e = n20e.outputs.Count == 1 ? n20e.outputs[0] : null;
        if (grafo.FindNode(tras20e) is CompleteQuestStepsNode hecha && hecha.questId == QuestId)
        {
            informe.Add("= 20e2 ya completa ELDRAN_SIGUEME.");
        }
        else if (tras20e == Nodo21)
        {
            var completa = new CompleteQuestStepsNode
            {
                displayTitle = "20e2.- Completa ELDRAN_SIGUEME (ya sabe qué quiere)",
                chapter = n20e.chapter,
                position = n20e.position + new Vector2(190f, 160f),
                questId = QuestId,
                completeQuest = true,
                logWarnings = true,
            };
            completa.outputs = new List<string> { Nodo21 };
            grafo.nodes.Insert(grafo.nodes.IndexOf(n20e) + 1, completa);
            n20e.outputs[0] = completa.guid;
            informe.Add("✓ 20e2: se completa ELDRAN_SIGUEME tras el encargo de la caja.");
        }
        else informe.Add("⚠ 20e ya no sale directo a 21: no se ha insertado el final de la misión.");

        n20d.charla = charlaVictoria;
        n20d3.charla = charlaEstrella;
        informe.Add($"✓ Charla de 20d: {charlaVictoria.name}. Charla de 20d3: {charlaEstrella.name}.");

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        CatalogoDeMisiones(informe);

        Debug.Log("[Cap1SigueAEldran] Hecho:\n- " + string.Join("\n- ", informe) +
                  "\nSi la ventana del grafo está abierta, ciérrala y vuelve a abrirla.");
    }

    /// Añade la quest al catálogo del QuestManager de Start y guarda Start. Si Start no está
    /// abierta, la abre un momento junto a lo que haya abierto y la vuelve a cerrar.
    private static void CatalogoDeMisiones(List<string> informe)
    {
        var start = EditorSceneManager.GetSceneByPath(RutaStart);
        bool abiertaAqui = !start.isLoaded;
        if (abiertaAqui) start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);

        QuestCatalogAudit.AuditAndFix();
        if (start.isDirty) EditorSceneManager.SaveScene(start);
        informe.Add("✓ ELDRAN_SIGUEME está en el catálogo del QuestManager de Start (ver el informe de QuestCatalogAudit).");

        if (abiertaAqui) EditorSceneManager.CloseScene(start, true);
    }

    private static void Quest(List<string> informe)
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestData>(RutaQuest);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestData>();
            AssetDatabase.CreateAsset(quest, RutaQuest);
            informe.Add($"✓ Creada la quest {RutaQuest}.");
        }
        quest.questId = QuestId;
        quest.displayNameId = quest.displayName = $"QUEST_{QuestId}_NAME";
        quest.descriptionId = $"QUEST_{QuestId}_DESC";
        quest.detailedDescriptionId = $"QUEST_{QuestId}_DETAIL";
        quest.steps = new[] { new QuestData.Step { descriptionId = $"QUEST_{QuestId}_STEP01_DESC" } };
        EditorUtility.SetDirty(quest);
    }

    /// Crea el diálogo si no existe y le pone sus líneas. Las de Will van como «habla el jugador».
    private static DialogueAsset Dialogo(string ruta, params (string hablante, string textId, NPCEmotion emocion)[] lineas)
    {
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogo, ruta);
        }

        var nuevas = new DialogueLine[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            nuevas[i] = new DialogueLine
            {
                speakerNameId = lineas[i].hablante,
                textId = lineas[i].textId,
                isPlayerSpeaking = lineas[i].hablante == Will,
                emotion = lineas[i].emocion,
            };
        dialogo.lines = nuevas;
        EditorUtility.SetDirty(dialogo);
        return dialogo;
    }
}
