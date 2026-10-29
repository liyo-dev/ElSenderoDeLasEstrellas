#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using Game.NPC.Modules;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Restaura el entrenamiento de combate interactivo con Erika en el Capítulo 2 (Cap2.asset),
/// entrega Bola Prisma y Protección, entrega el Pañuelo de Erika al vencerla y completa el encargo. (INC-851)
/// </summary>
public static class Cap2RestaurarEntrenamientoErika
{
    const string GrafoPath = "Assets/NarrativeGraph/Cap2.asset";
    const string ErikaPrefabPath = "Assets/_NPCs/Combat/Erika.prefab";
    const string CombatConfigPath = "Assets/_NPCs/Combat/NPC_Combat_Config_Erika.asset";
    const string PanueloItemPath = "Assets/_ITEMS/IT_Panuelo.asset";
    const string DlgEntrenamientoPath = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_ENTRENAMIENTO.asset";
    const string DlgVictoriaPath = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_VICTORIA.asset";
    const string DlgDespuesPath = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_DESPUES.asset";
    const string DefeatEvent = "ERIKA_ENTRENAMIENTO_DERROTADA";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Restaurar entrenamiento Erika (INC-851)")]
    public static void Restaurar()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("El montaje debe ejecutarse fuera de Play.");

        Debug.Log("[Cap2RestaurarErika] Iniciando configuración de Erika y Cap2.asset...");

        // 1. Ítem Pañuelo de Erika
        var panuelo = AssetDatabase.LoadAssetAtPath<ItemData>(PanueloItemPath);
        if (panuelo == null)
        {
            panuelo = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(panuelo, PanueloItemPath);
        }
        panuelo.itemId = "panuelo_erika";
        panuelo.displayNameId = "ITEM_PANUELO_NAME";
        panuelo.displayName = "Pañuelo de Erika";
        panuelo.useDescriptionId = "ITEM_PANUELO_DESC";
        panuelo.useDescription = "Un pañuelo de tela resistente entregado por Erika tras el entrenamiento. Para el sudor y para acordarte de volver de una pieza.";
        var keyItem = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Key.asset");
        if (keyItem != null) panuelo.icon = keyItem.icon;
        panuelo.usableFromInventory = false;
        panuelo.usageKind = ItemData.ItemUsageKind.Quest;
        panuelo.buyPrice = 0;
        panuelo.sellValue = 0;
        EditorUtility.SetDirty(panuelo);

        // 2. Diálogo 1: Reto previo al combate (enseña protección, páginas del grimorio y bola prisma)
        var dlgReto = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DlgEntrenamientoPath);
        if (dlgReto == null)
        {
            dlgReto = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlgReto, DlgEntrenamientoPath);
        }
        dlgReto.lines = new DialogueLine[]
        {
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_ENTRENAMIENTO_01", emotion = NPCEmotion.Determined },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_ENTRENAMIENTO_02", emotion = NPCEmotion.Determined },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_ENTRENAMIENTO_03", emotion = NPCEmotion.Determined },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_ENTRENAMIENTO_04", emotion = NPCEmotion.Determined },
            new DialogueLine { speakerNameId = "CHAR_WILL",  textId = "DLG_ERIKA_ENTRENAMIENTO_05", isPlayerSpeaking = true, emotion = NPCEmotion.Happy },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_ENTRENAMIENTO_06", emotion = NPCEmotion.Determined }
        };
        EditorUtility.SetDirty(dlgReto);

        // 3. Diálogo 2: Victoria y entrega del pañuelo
        var dlgVictoria = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DlgVictoriaPath);
        if (dlgVictoria == null)
        {
            dlgVictoria = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlgVictoria, DlgVictoriaPath);
        }
        dlgVictoria.lines = new DialogueLine[]
        {
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_VICTORIA_01", emotion = NPCEmotion.Happy },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_VICTORIA_02", emotion = NPCEmotion.Determined },
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_VICTORIA_03", emotion = NPCEmotion.Determined }
        };
        EditorUtility.SetDirty(dlgVictoria);

        // 4. Diálogo 3: Permanente en reposo
        var dlgDespues = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DlgDespuesPath);
        if (dlgDespues == null)
        {
            dlgDespues = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dlgDespues, DlgDespuesPath);
        }
        dlgDespues.lines = new DialogueLine[]
        {
            new DialogueLine { speakerNameId = "CHAR_ERIKA", textId = "DLG_ERIKA_DESPUES_01", emotion = NPCEmotion.Determined }
        };
        EditorUtility.SetDirty(dlgDespues);

        // 5. Configuración de combate de Erika
        var combatConfig = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(CombatConfigPath);
        if (combatConfig != null)
        {
            combatConfig.health = 100f;
            combatConfig.detectionRange = 10f;
            combatConfig.isAggressive = true;
            combatConfig.dialogueOnAlert = dlgReto;
            combatConfig.dialogueOnDizzy = dlgVictoria;
            combatConfig.dialogueAfterDefeat = dlgDespues;
            combatConfig.sendEventOnDefeat = true;
            combatConfig.defeatEventKey = DefeatEvent;
            combatConfig.sendDefeatEventBeforeDeath = false;
            combatConfig.postDeathBehavior = PostDeathBehavior.GetUpDizzy;
            combatConfig.postDefeatAction = PostDefeatAction.None;
            EditorUtility.SetDirty(combatConfig);
        }

        // 6. Prefab de Erika
        var erikaGo = PrefabUtility.LoadPrefabContents(ErikaPrefabPath);
        if (erikaGo != null)
        {
            var mgr = erikaGo.GetComponent<NPCBehaviourManagerV2>();
            if (mgr != null)
            {
                var so = new SerializedObject(mgr);
                so.FindProperty("configuration.combatConfig").objectReferenceValue = combatConfig;
                so.FindProperty("persistenceId").stringValue = "NPC_Erika";
                so.FindProperty("dialogueCharacterId").stringValue = "CHAR_ERIKA";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var dmg = erikaGo.GetComponent<Damageable>() ?? erikaGo.AddComponent<Damageable>();
            dmg.SetMaxAndCurrent(100f, 100f);
            dmg.SetDestroyOnDeath(false);

            if (!erikaGo.GetComponent<NPCCombatLifecycleHandler>())
            {
                erikaGo.AddComponent<NPCCombatLifecycleHandler>();
            }

            PrefabUtility.SaveAsPrefabAsset(erikaGo, ErikaPrefabPath);
            PrefabUtility.UnloadPrefabContents(erikaGo);
        }

        // 7. Configurar el Grafo Narrativo Cap2.asset
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GrafoPath);
        if (grafo == null) throw new InvalidOperationException("No se encontró " + GrafoPath);

        Undo.RecordObject(grafo, "Restaurar combate de entrenamiento Erika");

        // Quitar nodos obsoletos de pruebas anteriores
        grafo.nodes.RemoveAll(n => n.guid == "cap2-prepara-viaje-erika-1" || n.guid == "cap2-prepara-viaje-erika-victoria");

        // Nodo 1: Esperar interacción (o alerta cuando Erika ve al jugador)
        var nTalk = grafo.FindNode("cap2-prepara-viaje-erika-0") as WaitNpcInteractionNode;
        if (nTalk == null)
        {
            nTalk = new WaitNpcInteractionNode { npcId = "NPC_Erika" };
            PonerNodo(grafo, nTalk, "cap2-prepara-viaje-erika-0", "22b.- Hablar con Erika", new Vector2(7220, 372));
        }
        nTalk.npcId = "NPC_Erika";
        nTalk.outputs = new List<string> { "cap2-prepara-viaje-erika-combate" };

        // Nodo 2: Esperar evento de derrota de Erika (ERIKA_ENTRENAMIENTO_DERROTADA)
        var nWaitDefeat = grafo.FindNode("cap2-prepara-viaje-erika-combate") as WaitCustomEventNode;
        if (nWaitDefeat == null)
        {
            nWaitDefeat = new WaitCustomEventNode();
            PonerNodo(grafo, nWaitDefeat, "cap2-prepara-viaje-erika-combate", "23b.- Esperar fin del combate con Erika", new Vector2(7600, 372));
        }
        nWaitDefeat.eventKey = DefeatEvent;
        nWaitDefeat.outputs = new List<string> { "cap2-prepara-viaje-erika-desbloqueo" };

        // Nodo 3: Desbloqueo formal de Bola Prisma y Protección en el grafo
        var nUnlock = grafo.FindNode("cap2-prepara-viaje-erika-desbloqueo") as UnlockAbilitiesNode;
        if (nUnlock == null)
        {
            nUnlock = new UnlockAbilitiesNode();
            PonerNodo(grafo, nUnlock, "cap2-prepara-viaje-erika-desbloqueo", "23b-2.- Aprender Bola Prisma y Protección", new Vector2(7980, 372));
        }
        nUnlock.spellsToUnlock = new List<SpellId> { SpellId.Plasmaball };
        nUnlock.abilityKeysToUnlock = new List<AbilityKey> { AbilityKey.Shield };
        nUnlock.assignSpellsToEmptySlot = true;
        nUnlock.applyPresetAfterUnlock = true;
        nUnlock.saveAfterUnlock = true;
        nUnlock.outputs = new List<string> { "cap2-prepara-viaje-erika-item" };

        // Nodo 4: Dar Pañuelo de Erika
        var nItem = grafo.FindNode("cap2-prepara-viaje-erika-item") as GiveInventoryItemNode;
        if (nItem == null)
        {
            nItem = new GiveInventoryItemNode();
            PonerNodo(grafo, nItem, "cap2-prepara-viaje-erika-item", "23b-3.- Recibir Pañuelo de Erika", new Vector2(8360, 372));
        }
        nItem.item = panuelo;
        nItem.amount = 1;
        nItem.onlyOnce = true;
        nItem.outputs = new List<string> { "cap2-prepara-viaje-erika-2" };

        // Nodo 5: Completar paso de quest
        var nComplete = grafo.FindNode("cap2-prepara-viaje-erika-2") as CompleteQuestStepsNode;
        if (nComplete == null)
        {
            nComplete = new CompleteQuestStepsNode();
            PonerNodo(grafo, nComplete, "cap2-prepara-viaje-erika-2", "24b.- Completar encargo · entrenamiento Erika", new Vector2(8740, 372));
        }
        nComplete.displayTitle = "24b.- Completar encargo · entrenamiento Erika";
        nComplete.standingActorId = "NPC_Erika";
        nComplete.standingDialogue = dlgDespues;
        nComplete.questId = "PREPARA_VIAJE";
        nComplete.stepConditionIds = new List<string> { "PREPARA_VIAJE_ERIKA" };
        nComplete.completeQuest = false;
        nComplete.logWarnings = true;
        nComplete.outputs = new List<string> { "cap2-prepara-viaje-encargo-hecho" };

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        Debug.Log("[Cap2RestaurarErika] ¡Entrenamiento de Erika completamente restaurado y sincronizado en Cap2.asset!");
    }

    static void PonerNodo(NarrativeGraph g, NarrativeNode n, string id, string titulo, Vector2 pos)
    {
        n.guid = id;
        n.displayTitle = titulo;
        n.chapter = "Cap. 2";
        n.position = pos;
        var ant = g.FindNode(id);
        if (ant == null) g.nodes.Add(n);
        else g.nodes[g.nodes.IndexOf(ant)] = n;
    }
}
#endif