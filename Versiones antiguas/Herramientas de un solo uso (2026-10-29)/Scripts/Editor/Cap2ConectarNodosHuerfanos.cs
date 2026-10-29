#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.NPC;
using Game.NPC.Modules;

/// <summary>
/// Conecta los nodos huérfanos del Cap2.asset y añade las ramas faltantes de Erika y Eldran
/// al fork de "Prepárate para el viaje". INC-725.
/// </summary>
public static class Cap2ConectarNodosHuerfanos
{
    const string GrafoPath = "Assets/NarrativeGraph/Cap2.asset";
    const string ForkGuid = "cap2-prepara-viaje-fork";
    const string VictoriaEndGuid = "cap2-prepara-viaje-victoria-4";
    const string BoticariaEndGuid = "cap2-prepara-viaje-boticaria-4";

    // Nodos huérfanos existentes (ya están en el grafo pero sin conectar)
    const string BolaPrismaGuid = "cap2-desbloqueo-bola-prisma";
    const string CupulaEstelarGuid = "cap2-desbloqueo-cupula-estelar";
    const string LevitacionGuid = "cap2-desbloqueo-levitacion";
    const string TutorialCupulaGuid = "cap2-tutorial-cupula-estelar";

    // Prefabs de NPCs (ruta _GrafoNarrativo = los que usa el grafo nuevo)
    const string ErikaPrefabPath = "Assets/_NPCs/Combat/Erika.prefab";
    const string EldranPrefabPath = "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab";
    const string VictoriaPrefabPath = "Assets/_NPCs/_GrafoNarrativo/Victoria.prefab";
    const string BoticariaPrefabPath = "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab";

    // Diálogos existentes
    const string DlgErikaEntrenamiento = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_ENTRENAMIENTO.asset";
    const string DlgErikaDespues = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_DESPUES.asset";
    const string DlgEldranPunteria = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_PUNTERIA.asset";
    const string DlgEldranDespues = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_PUNTERIA_DESPUES.asset";

    // Quest conditions
    const string QuestId = "PREPARA_VIAJE";
    const string CondErika = "PREPARA_VIAJE_ERIKA";
    const string CondEldran = "PREPARA_VIAJE_ELDRAN";

    // Wardrobe / Spells
    const string WardrobeCloak01 = "Assets/_WARDROBE ITEMS/WardrobeItem_Cloak01.asset";
    const string SpellBolaPrisma = "Assets/_SPELLS/BolaPrisma.asset";

    [MenuItem("El Sendero/Capítulo 2/Conectar nodos huérfanos y añadir Erika/Eldran (INC-725)")]
    public static void Conectar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar el grafo.");

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GrafoPath)
            ?? throw new InvalidOperationException("No se encuentra Cap2.asset");

        // 1. Asegurar persistenceId en Erika
        AsegurarPersistenceIdErika();

        // 2. Crear/obtener diálogos necesarios
        var dlgErikaEntrenamiento = CrearDialogoErikaEntrenamiento();
        var dlgErikaDespues = CrearDialogoErikaDespues();
        var dlgEldranPunteria = CrearDialogoEldranPunteria();
        var dlgEldranDespues = CrearDialogoEldranDespues();

        var wardrobeCloak01 = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(WardrobeCloak01);
        var spellBolaPrisma = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(SpellBolaPrisma);

        Undo.RecordObject(grafo, "Cap2: conectar nodos huérfanos + Erika/Eldran");

        // 3. Obtener nodos existentes
        var fork = grafo.FindNode(ForkGuid) as ForkNode
            ?? throw new InvalidOperationException("No se encuentra el fork " + ForkGuid);

        var victoriaEnd = grafo.FindNode(VictoriaEndGuid) as CompleteQuestStepsNode
            ?? throw new InvalidOperationException("No se encuentra " + VictoriaEndGuid);

        var boticariaEnd = grafo.FindNode(BoticariaEndGuid) as CompleteQuestStepsNode
            ?? throw new InvalidOperationException("No se encuentra " + BoticariaEndGuid);

        var nodoBolaPrisma = grafo.FindNode(BolaPrismaGuid) as UnlockAbilitiesNode
            ?? throw new InvalidOperationException("No se encuentra nodo Bola Prisma huérfano");

        var nodoCupulaEstelar = grafo.FindNode(CupulaEstelarGuid) as UnlockAbilitiesNode
            ?? throw new InvalidOperationException("No se encuentra nodo Cúpula Estelar huérfano");

        var nodoLevitacion = grafo.FindNode(LevitacionGuid) as UnlockAbilitiesNode
            ?? throw new InvalidOperationException("No se encuentra nodo Levitación huérfano");

        var nodoTutorialCupula = grafo.FindNode(TutorialCupulaGuid) as TutorialPromptNode
            ?? throw new InvalidOperationException("No se encuentra tutorial Cúpula Estelar huérfano");

        // 4. Añadir salidas del fork hacia Erika y Eldran
        var nuevasSalidasFork = new List<string>(fork.outputs)
        {
            "cap2-prepara-viaje-erika-0",
            "cap2-prepara-viaje-eldran-0"
        };
        fork.outputs = nuevasSalidasFork;

        // 5. Crear rama ERIKA (paralela a Victoria y Boticaria)
        var erikaTalk = CrearNodo<WaitNpcInteractionNode>(grafo, "cap2-prepara-viaje-erika-0",
            "22b.- Hablar con Erika", new WaitNpcInteractionNode
            {
                npcId = "NPC_Erika",
                acceptEarlierInteraction = false,
                questIcon = null
            }, new Vector2(7220, 372)); // debajo de boticaria

        var erikaDlg = CrearNodo<PlayDialogueNode>(grafo, "cap2-prepara-viaje-erika-1",
            "23b.- Erika: entrenamiento y pañuelo", new PlayDialogueNode
            {
                dialogue = dlgErikaEntrenamiento,
                npcId = "NPC_Erika",
                oneShotFlag = ""
            }, new Vector2(7600, 372));

        var erikaComplete = CrearNodo<CompleteQuestStepsNode>(grafo, "cap2-prepara-viaje-erika-2",
            "24b.- Completar encargo · entrenamiento Erika", new CompleteQuestStepsNode
            {
                standingActorId = "NPC_Erika",
                standingDialogue = dlgErikaDespues,
                questId = QuestId,
                stepConditionIds = new List<string> { CondErika },
                completeQuest = false,
                logWarnings = true
            }, new Vector2(8360, 372));

        // 6. Crear rama ELDRAN (paralela)
        var eldranTalk = CrearNodo<WaitNpcInteractionNode>(grafo, "cap2-prepara-viaje-eldran-0",
            "22c.- Hablar con Eldran (puntería)", new WaitNpcInteractionNode
            {
                npcId = "NPC_Eldran",
                acceptEarlierInteraction = false,
                questIcon = null
            }, new Vector2(7220, 558));

        var eldranDlg = CrearNodo<PlayDialogueNode>(grafo, "cap2-prepara-viaje-eldran-1",
            "23c.- Eldran: cubos y Bola Prisma", new PlayDialogueNode
            {
                dialogue = dlgEldranPunteria,
                npcId = "NPC_Eldran",
                oneShotFlag = ""
            }, new Vector2(7600, 558));

        var eldranUnlockCloak = CrearNodo<UnlockWardrobeItemNode>(grafo, "cap2-prepara-viaje-eldran-2",
            "24c.- Desbloquear túnica de viaje (opcional)", new UnlockWardrobeItemNode
            {
                wardrobeItem = wardrobeCloak01,
                logWarnings = false
            }, new Vector2(7980, 558));

        var eldranComplete = CrearNodo<CompleteQuestStepsNode>(grafo, "cap2-prepara-viaje-eldran-3",
            "25c.- Completar encargo · puntería Eldran", new CompleteQuestStepsNode
            {
                standingActorId = "NPC_Eldran",
                standingDialogue = dlgEldranDespues,
                questId = QuestId,
                stepConditionIds = new List<string> { CondEldran },
                completeQuest = false,
                logWarnings = true
            }, new Vector2(8740, 558));

        // 7. CONECTAR BOLA PRISMA al final de la rama de Eldran
        // El desbloqueo de Bola Prisma (2.3d) ocurre tras los cubos de Eldran
        eldranComplete.outputs = new List<string> { nodoBolaPrisma.guid };
        nodoBolaPrisma.outputs = new List<string>(); // terminal por ahora

        // 8. Mantener salidas de Victoria y Boticaria vacías (terminales)
        // El quest se completa cuando las 4 condiciones están cumplidas (CheckQuestCompleteNode o similar)
        victoriaEnd.outputs = new List<string>();
        boticariaEnd.outputs = new List<string>();
        erikaComplete.outputs = new List<string>();

        // 9. Cúpula Estelar y Levitación quedan huérfanos por ahora (se conectarán en bloques 2.9 y 2.10)
        // Pero los movemos a posiciones lógicas para cuando se construyan esos bloques
        nodoCupulaEstelar.position = new Vector2(12000, 0);
        nodoTutorialCupula.position = new Vector2(12400, 0);
        nodoLevitacion.position = new Vector2(12000, 800);

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        Debug.Log("[Cap2ConectarNodosHuerfanos] ✓ Fork actualizado con Erika y Eldran\n" +
                  "✓ Rama Erika: WaitNpc → PlayDialogue → CompleteQuestSteps\n" +
                  "✓ Rama Eldran: WaitNpc → PlayDialogue → UnlockWardrobe → CompleteQuestSteps → UnlockBolaPrisma\n" +
                  "✓ Nodos huérfanos Cúpula/Levitación reposicionados para bloques futuros");
    }

    static void AsegurarPersistenceIdErika()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErikaPrefabPath)
            ?? throw new InvalidOperationException("No se encuentra " + ErikaPrefabPath);

        var manager = prefab.GetComponentInChildren<NPCBehaviourManagerV2>(true);
        if (manager == null)
            throw new InvalidOperationException("Erika.prefab no tiene NPCBehaviourManagerV2");

        var so = new SerializedObject(manager);
        var prop = so.FindProperty("persistenceId");
        if (prop == null)
            throw new InvalidOperationException("NPCBehaviourManagerV2 no tiene campo persistenceId");

        if (prop.stringValue != "NPC_Erika")
        {
            prop.stringValue = "NPC_Erika";
            so.ApplyModifiedProperties();
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[Cap2Conectar] Erika.prefab persistenceId → NPC_Erika");
        }
    }

    static DialogueAsset CrearDialogoErikaEntrenamiento()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika";
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder("Assets/_DIALOGUES/DIALOGUE NPCS", "Erika");

        var ruta = $"{carpeta}/DLG_ERIKA_ENTRENAMIENTO.asset";
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }

        dlg.lines = new[]
        {
            NewLine("CHAR_ERIKA", "DLG_ERIKA_ENTRENAMIENTO_01", NPCEmotion.Neutral),
            NewLine("CHAR_ERIKA", "DLG_ERIKA_ENTRENAMIENTO_02", NPCEmotion.Determined),
            NewLine("CHAR_WILL", "DLG_ERIKA_ENTRENAMIENTO_03", NPCEmotion.Neutral, true),
            NewLine("CHAR_ERIKA", "DLG_ERIKA_ENTRENAMIENTO_04", NPCEmotion.Happy),
            NewLine("CHAR_ERIKA", "DLG_ERIKA_ENTRENAMIENTO_05", NPCEmotion.Determined),
        };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueAsset CrearDialogoErikaDespues()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika";
        var ruta = $"{carpeta}/DLG_ERIKA_DESPUES.asset";
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }

        dlg.lines = new[] { NewLine("CHAR_ERIKA", "DLG_ERIKA_DESPUES_01", NPCEmotion.Happy) };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueAsset CrearDialogoEldranPunteria()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
        var ruta = $"{carpeta}/DLG_ELDRAN_PUNTERIA.asset";
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }

        dlg.lines = new[]
        {
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_PUNTERIA_01", NPCEmotion.Neutral),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_PUNTERIA_02", NPCEmotion.Determined),
            NewLine("CHAR_WILL", "DLG_ELDRAN_PUNTERIA_03", NPCEmotion.Neutral, true),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_PUNTERIA_04", NPCEmotion.Happy),
        };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueAsset CrearDialogoEldranDespues()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
        var ruta = $"{carpeta}/DLG_ELDRAN_PUNTERIA_DESPUES.asset";
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }

        dlg.lines = new[] { NewLine("CHAR_ELDRAN", "DLG_ELDRAN_PUNTERIA_DESPUES_01", NPCEmotion.Happy) };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueLine NewLine(string speaker, string textId, NPCEmotion emotion, bool isPlayer = false)
        => new DialogueLine
        {
            speakerNameId = speaker,
            textId = textId,
            text = "",
            portrait = null,
            isPlayerSpeaking = isPlayer,
            lookAtOverrideId = "",
            emotion = emotion
        };

    static T CrearNodo<T>(NarrativeGraph grafo, string guid, string titulo, T nodo, Vector2 pos) where T : NarrativeNode
    {
        var existente = grafo.FindNode(guid);
        if (existente != null && existente.GetType() != typeof(T))
            throw new InvalidOperationException($"GUID {guid} ya existe como otro tipo de nodo.");

        int idx = existente != null ? grafo.nodes.IndexOf(existente) : -1;
        nodo.guid = guid;
        nodo.displayTitle = titulo;
        nodo.chapter = "Cap. 2";
        nodo.position = pos;
        nodo.inputAnchor = NarrativeNode.PortAnchor.Left;
        nodo.outputAnchor = NarrativeNode.PortAnchor.Right;
        nodo.blockSaving = false;
        nodo.overrideNodeColor = false;
        nodo.nodeColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        if (idx >= 0) grafo.nodes[idx] = nodo;
        else grafo.nodes.Add(nodo);

        return nodo;
    }
}
#endif