#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta el bloque 2.4 (Despedida) en Cap2.asset y cierra el capítulo. INC-725.
/// </summary>
public static class Cap2Bloque24Despedida
{
    const string GrafoPath = "Assets/NarrativeGraph/Cap2.asset";

    // Nodos existentes del fork (finales de las 4 ramas)
    const string VictoriaEndGuid = "cap2-prepara-viaje-victoria-4";
    const string BoticariaEndGuid = "cap2-prepara-viaje-boticaria-4";
    const string ErikaEndGuid = "cap2-prepara-viaje-erika-2";
    const string EldranEndGuid = "cap2-prepara-viaje-eldran-3";

    // Fork de los encargos y nodos que esperan a que terminen los cuatro
    const string ForkGuid = "cap2-prepara-viaje-fork";
    const string EncargoHechoGuid = "cap2-prepara-viaje-encargo-hecho";
    const string EsperaEncargosGuid = "cap2-prepara-viaje-espera";
    const string CompletarEncargosGuid = "cap2-prepara-viaje-completar";
    const string EncargoHechoSenal = "CAP2_PREPARA_VIAJE_ENCARGO_HECHO";

    // Checkpoint existente tras contrabandistas (lo reutilizamos como "tras preparación")
    const string CheckpointTrasPreparacionGuid = "cap2-contrabandistas-fin";

    // Diálogos a crear
    const string DlgDespedidaOliver = "Assets/_DIALOGUES/DIALOGUE NPCS/Oliver/DLG_OLIVER_DESPEDIDA.asset";
    const string DlgDespedidaEldran = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_DESPEDIDA.asset";
    const string DlgMotivosBrujula = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_MOTIVOS_BRUJULA.asset";

    // Quest
    const string QuestId = "PREPARA_VIAJE";

    [MenuItem("El Sendero/Capítulo 2/Montar bloque 2.4 Despedida y cerrar Cap2 (INC-725)")]
    public static void MontarDespedida()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar el grafo.");

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GrafoPath)
            ?? throw new InvalidOperationException("No se encuentra Cap2.asset");

        // 1. Crear/obtener diálogos
        var dlgMotivos = CrearDialogoMotivosBrujula();
        var dlgOliver = CrearDialogoOliverDespedida();
        var dlgEldran = CrearDialogoEldranDespedida();

        Undo.RecordObject(grafo, "Cap2: montar bloque 2.4 Despedida");

        // 2. Conectar las 4 ramas al nodo común "Lista de motivos + brújula"
        ConectarRamasInternas(grafo);
        ConectarRamasAlComun(grafo);

        // 3. Crear nodo "Lista de motivos y brújula" (PlayDialogue con Eldran)
        var nodoMotivos = CrearNodo<PlayDialogueNode>(grafo, "cap2-despedida-motivos",
            "26.- Lista de motivos y brújula", new PlayDialogueNode
            {
                dialogue = dlgMotivos,
                npcId = "NPC_Eldran",
                oneShotFlag = ""
            }, new Vector2(9500, 200));

        // 4. Despedida Oliver (WaitNpcInteraction -> PlayDialogue)
        var nodoOliverTalk = CrearNodo<WaitNpcInteractionNode>(grafo, "cap2-despedida-oliver-talk",
            "27.- Hablar con Oliver (despedida)", new WaitNpcInteractionNode
            {
                npcId = "NPC_Oliver",
                acceptEarlierInteraction = false,
                questIcon = null
            }, new Vector2(10300, -100));

        var nodoOliverDlg = CrearNodo<PlayDialogueNode>(grafo, "cap2-despedida-oliver-dlg",
            "28.- Oliver: nos vemos pronto", new PlayDialogueNode
            {
                dialogue = dlgOliver,
                npcId = "NPC_Oliver",
                oneShotFlag = ""
            }, new Vector2(10700, -100));

        // Oliver sale del grupo (RaiseCustomEvent para ActiveCharacterSwapper)
        var nodoOliverSale = CrearNodo<RaiseCustomEventNode>(grafo, "cap2-despedida-oliver-sale",
            "29.- Oliver sale del grupo", new RaiseCustomEventNode
            {
                eventKey = "OLIVER_SALE_DEL_GRUPO"
            }, new Vector2(11100, -100));

        // 5. Despedida Eldran (parte a la capital)
        var nodoEldranTalk = CrearNodo<WaitNpcInteractionNode>(grafo, "cap2-despedida-eldran-talk",
            "30.- Hablar con Eldran (se va a la capital)", new WaitNpcInteractionNode
            {
                npcId = "NPC_Eldran",
                acceptEarlierInteraction = false,
                questIcon = null
            }, new Vector2(10300, 500));

        var nodoEldranDlg = CrearNodo<PlayDialogueNode>(grafo, "cap2-despedida-eldran-dlg",
            "31.- Eldran: me voy a la capital", new PlayDialogueNode
            {
                dialogue = dlgEldran,
                npcId = "NPC_Eldran",
                oneShotFlag = ""
            }, new Vector2(10700, 500));

        var nodoEldranSeVa = CrearNodo<RaiseCustomEventNode>(grafo, "cap2-despedida-eldran-seva",
            "32.- Eldran parte (evento)", new RaiseCustomEventNode
            {
                eventKey = "ELDRAN_PARTE_A_CAPITAL"
            }, new Vector2(11100, 500));

        // 6. Nodo de join (ambas despedidas convergen)
        var nodoJoin = CrearNodo<SetFlagNode>(grafo, "cap2-despedida-join",
            "33.- Cap. 2 completado - Listos para el viaje", new SetFlagNode
            {
                flagKey = "CAP2_COMPLETADO",
                value = true
            }, new Vector2(11900, 200));

        // 7. Checkpoint final de Cap 2
        var nodoCheckpoint = CrearNodo<CheckpointNode>(grafo, "cap2-final-checkpoint",
            "34.- Cap. 2 - Fin (Checkpoint)", new CheckpointNode
            {
                checkpointId = "CAP2_FIN",
                spawnAnchorId = "Bedroom",
                sceneName = "MainWorld"
            }, new Vector2(12700, 200));

        // 8. Cableado
        nodoMotivos.outputs = new List<string>
        {
            nodoOliverTalk.guid,
            nodoEldranTalk.guid
        };

        nodoOliverTalk.outputs = new List<string> { nodoOliverDlg.guid };
        nodoOliverDlg.outputs = new List<string> { nodoOliverSale.guid };
        nodoOliverSale.outputs = new List<string> { nodoJoin.guid };

        nodoEldranTalk.outputs = new List<string> { nodoEldranDlg.guid };
        nodoEldranDlg.outputs = new List<string> { nodoEldranSeVa.guid };
        nodoEldranSeVa.outputs = new List<string> { nodoJoin.guid };

        nodoJoin.outputs = new List<string> { nodoCheckpoint.guid };
        nodoCheckpoint.outputs = new List<string>(); // FIN DEL CAPÍTULO 2

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        Debug.Log("[Cap2Bloque24Despedida] Bloque 2.4 montado:\n" +
                  "  26. Lista motivos + brujula (Eldran)\n" +
                  "  27-29. Despedida Oliver -> sale del grupo (OLIVER_SALE_DEL_GRUPO)\n" +
                  "  30-32. Despedida Eldran -> parte a capital (ELDRAN_PARTE_A_CAPITAL)\n" +
                  "  33. Flag CAP2_COMPLETADO = 1\n" +
                  "  34. Checkpoint CAP2_FIN -> FIN DEL CAPITULO 2");
    }

    /// <summary>
    /// Solo reconecta las cuatro ramas del fork con la despedida (sin recrear el resto del bloque),
    /// para arreglar grafos ya montados. Ver INC-725.
    /// </summary>
    [MenuItem("El Sendero/Archivo/Capítulo 2/Esperar los 4 encargos antes de la despedida")]
    public static void EsperarEncargosAntesDeLaDespedida()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar el grafo.");

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GrafoPath)
            ?? throw new InvalidOperationException("No se encuentra Cap2.asset");

        Undo.RecordObject(grafo, "Cap2: esperar los 4 encargos");
        ConectarRamasInternas(grafo);
        ConectarRamasAlComun(grafo);
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Cierra el tramo interno de las ramas de Erika y Eldran, que quedaron creadas sin cables:
    /// sin esto el contador de los cuatro encargos nunca recibe sus dos señales y la despedida
    /// no puede abrirse. El desbloqueo de Bola Prisma va entre la charla de Eldran y la túnica.
    /// </summary>
    static void ConectarRamasInternas(NarrativeGraph grafo)
    {
        Enlazar(grafo, "cap2-prepara-viaje-erika-0", "cap2-prepara-viaje-erika-1");
        Enlazar(grafo, "cap2-prepara-viaje-erika-1", "cap2-prepara-viaje-erika-2");

        Enlazar(grafo, "cap2-prepara-viaje-eldran-0", "cap2-prepara-viaje-eldran-1");
        Enlazar(grafo, "cap2-prepara-viaje-eldran-1", "cap2-desbloqueo-bola-prisma");
        Enlazar(grafo, "cap2-desbloqueo-bola-prisma", "cap2-prepara-viaje-eldran-2");
        Enlazar(grafo, "cap2-prepara-viaje-eldran-2", "cap2-prepara-viaje-eldran-3");
    }

    static void Enlazar(NarrativeGraph grafo, string desde, string hasta)
    {
        var nodo = grafo.FindNode(desde);
        if (nodo == null || grafo.FindNode(hasta) == null)
        {
            Debug.LogWarning($"[Cap2Bloque24Despedida] No se enlaza {desde} → {hasta}: falta uno de los nodos.");
            return;
        }
        if (nodo.outputs == null) nodo.outputs = new List<string>();
        if (!nodo.outputs.Contains(hasta)) nodo.outputs.Add(hasta);
    }

    /// <summary>
    /// Cada rama del fork (capa, poción, Erika, Eldran) termina emitiendo una señal común; una rama
    /// extra del fork cuenta cuatro señales, completa la misión y solo entonces abre la despedida.
    /// Sin esta espera, la primera rama terminada lanzaba ya el diálogo de la despedida.
    /// </summary>
    static void ConectarRamasAlComun(NarrativeGraph grafo)
    {
        var fork = grafo.FindNode(ForkGuid) as ForkNode
            ?? throw new InvalidOperationException("No se encuentra el fork " + ForkGuid);

        var encargoHecho = CrearNodo(grafo, EncargoHechoGuid, "25.- Encargo terminado (señal)",
            new RaiseCustomEventNode { eventKey = EncargoHechoSenal },
            PosicionOExistente(grafo, EncargoHechoGuid, new Vector2(9120, -300)));

        var espera = CrearNodo(grafo, EsperaEncargosGuid, "25b.- Esperar los 4 encargos",
            new WaitSignalCountNode { eventKey = EncargoHechoSenal, count = 4 },
            PosicionOExistente(grafo, EsperaEncargosGuid, new Vector2(7220, 900)));

        var completar = CrearNodo(grafo, CompletarEncargosGuid, "25c.- Completar misión · Prepárate para el viaje",
            new CompleteQuestStepsNode { questId = QuestId, completeQuest = true, logWarnings = false },
            PosicionOExistente(grafo, CompletarEncargosGuid, new Vector2(7600, 900)));

        encargoHecho.outputs = new List<string>();
        espera.outputs = new List<string> { completar.guid };
        completar.outputs = new List<string> { "cap2-despedida-motivos" };

        foreach (var guid in new[] { VictoriaEndGuid, BoticariaEndGuid, ErikaEndGuid, EldranEndGuid })
        {
            var nodo = grafo.FindNode(guid) as CompleteQuestStepsNode;
            if (nodo != null)
                nodo.outputs = new List<string> { encargoHecho.guid };
        }

        if (!fork.outputs.Contains(espera.guid))
            fork.outputs.Add(espera.guid);
    }

    static Vector2 PosicionOExistente(NarrativeGraph grafo, string guid, Vector2 porDefecto)
    {
        var existente = grafo.FindNode(guid);
        return existente != null ? existente.position : porDefecto;
    }

    static DialogueAsset CrearDialogoMotivosBrujula()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
        var ruta = DlgMotivosBrujula;
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }
        dlg.lines = new[]
        {
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_MOTIVOS_01", NPCEmotion.Neutral),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_MOTIVOS_02", NPCEmotion.Determined),
            NewLine("CHAR_WILL", "DLG_ELDRAN_MOTIVOS_03", NPCEmotion.Thinking, true),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_MOTIVOS_04", NPCEmotion.Happy),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_BRUJULA_01", NPCEmotion.Neutral),
        };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueAsset CrearDialogoOliverDespedida()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Oliver";
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder("Assets/_DIALOGUES/DIALOGUE NPCS", "Oliver");

        var ruta = DlgDespedidaOliver;
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }
        dlg.lines = new[]
        {
            NewLine("CHAR_OLIVER", "DLG_OLIVER_DESPEDIDA_01", NPCEmotion.Happy),
            NewLine("CHAR_OLIVER", "DLG_OLIVER_DESPEDIDA_02", NPCEmotion.Worried),
            NewLine("CHAR_WILL", "DLG_OLIVER_DESPEDIDA_03", NPCEmotion.Grateful, true),
            NewLine("CHAR_OLIVER", "DLG_OLIVER_DESPEDIDA_04", NPCEmotion.Happy),
        };
        dlg.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dlg);
        return dlg;
    }

    static DialogueAsset CrearDialogoEldranDespedida()
    {
        var carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran";
        var ruta = DlgDespedidaEldran;
        var dlg = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dlg == null)
        {
            dlg = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlg, ruta);
        }
        dlg.lines = new[]
        {
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_DESPEDIDA_01", NPCEmotion.Neutral),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_DESPEDIDA_02", NPCEmotion.Determined),
            NewLine("CHAR_WILL", "DLG_ELDRAN_DESPEDIDA_03", NPCEmotion.Grateful, true),
            NewLine("CHAR_ELDRAN", "DLG_ELDRAN_DESPEDIDA_04", NPCEmotion.Relieved),
        };
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
            throw new InvalidOperationException("GUID " + guid + " ya existe como otro tipo de nodo.");

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