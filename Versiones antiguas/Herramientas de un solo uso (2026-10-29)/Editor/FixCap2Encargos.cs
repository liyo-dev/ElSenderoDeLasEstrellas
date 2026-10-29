#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.NPC;
using Game.NPC.Common;

public static class FixCap2Encargos
{
    [MenuItem("El Sendero/Archivo/Fix Encargos Victoria Erika Eldran")]
    public static void AplicarFix()
    {
        Debug.Log("[FixCap2] Iniciando aplicación de fixes para el Capítulo 2...");

        // 1. Configurar Erika.prefab (asignar Canvas OverHere.prefab a defaultQuestIconPrefab)
        string erikaPath = "Assets/_NPCs/Combat/Erika.prefab";
        var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/OverHead/Canvas OverHere.prefab");
        if (iconPrefab == null)
        {
            Debug.LogError("[FixCap2] No se encontró el prefab Canvas OverHere.prefab en Assets/_NPCs/OverHead/Canvas OverHere.prefab");
        }
        else
        {
            var erikaContents = PrefabUtility.LoadPrefabContents(erikaPath);
            if (erikaContents != null)
            {
                var actor = erikaContents.GetComponent<NarrativeActor>() ?? erikaContents.AddComponent<NarrativeActor>();
                var soActor = new SerializedObject(actor);
                var iconProp = soActor.FindProperty("defaultQuestIconPrefab");
                if (iconProp != null)
                {
                    iconProp.objectReferenceValue = iconPrefab;
                    soActor.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[FixCap2] Erika.prefab: defaultQuestIconPrefab asignado a Canvas OverHere.");
                }

                var mgr = erikaContents.GetComponent<NPCBehaviourManagerV2>();
                if (mgr != null)
                {
                    var soMgr = new SerializedObject(mgr);
                    var pIdProp = soMgr.FindProperty("persistenceId");
                    if (pIdProp != null && string.IsNullOrEmpty(pIdProp.stringValue))
                    {
                        pIdProp.stringValue = "NPC_Erika";
                        soMgr.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(erikaContents, erikaPath);
                PrefabUtility.UnloadPrefabContents(erikaContents);
            }
        }

        // 2. Configurar Victoria.prefab (desvincular interactiveNarrativeConfig y limpiar ejecutores viejos)
        string victoriaPath = "Assets/_NPCs/Victoria.prefab";
        var vicContents = PrefabUtility.LoadPrefabContents(victoriaPath);
        if (vicContents != null)
        {
            var mgr = vicContents.GetComponent<NPCBehaviourManagerV2>();
            if (mgr != null && mgr.Configuration != null)
            {
                mgr.Configuration.interactiveNarrativeConfig = null;
                mgr.Configuration.behaviourType &= ~NPCBehaviourType.InteractiveNarrative;
                Debug.Log("[FixCap2] Victoria.prefab: interactiveNarrativeConfig desvinculado y flag removido.");
            }

            // Quitar ejecutores interactivos legacy si estuvieran presentes en el prefab
            foreach (var c in vicContents.GetComponents<Component>())
            {
                if (c != null && (c.GetType().Name == "NPCInteractiveNarrativeExecutor" || c.GetType().Name == "NPCQuestActionExecutor" || c.GetType().Name == "NPCPersistentIconController"))
                {
                    UnityEngine.Object.DestroyImmediate(c);
                    Debug.Log($"[FixCap2] Victoria.prefab: removido componente legacy {c.GetType().Name}");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(vicContents, victoriaPath);
            PrefabUtility.UnloadPrefabContents(vicContents);
        }

        // 3. Revisar prefabs de Eldran (asegurar defaultQuestIconPrefab en ambos prefabs si existen)
        string[] eldranPaths = new[] { "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab", "Assets/_NPCs/Eldran.prefab" };
        foreach (var p in eldranPaths)
        {
            if (File.Exists(p))
            {
                var eldranContents = PrefabUtility.LoadPrefabContents(p);
                if (eldranContents != null)
                {
                    var actor = eldranContents.GetComponent<NarrativeActor>();
                    if (actor != null && iconPrefab != null)
                    {
                        var soActor = new SerializedObject(actor);
                        var iconProp = soActor.FindProperty("defaultQuestIconPrefab");
                        if (iconProp != null)
                        {
                            iconProp.objectReferenceValue = iconPrefab;
                            soActor.ApplyModifiedPropertiesWithoutUndo();
                            Debug.Log($"[FixCap2] {p}: defaultQuestIconPrefab asignado.");
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(eldranContents, p);
                    PrefabUtility.UnloadPrefabContents(eldranContents);
                }
            }
        }

        // 4. Configurar Oficio_Victoria en MainWorld.unity
        string mainWorldPath = "Assets/Scenes/Worlds/MainWorld.unity";
        var mwScene = EditorSceneManager.OpenScene(mainWorldPath, OpenSceneMode.Additive);
        if (mwScene.IsValid())
        {
            var oficioVicGo = GameObject.Find("Oficio_Victoria");
            var spVicGo = GameObject.Find("SP_Victoria");
            if (oficioVicGo != null)
            {
                var route = oficioVicGo.GetComponent<NPCWorkRoute>();
                if (route != null)
                {
                    route.offsetEnMano = new Vector3(-0.154f, -0.365f, -0.262f);
                    route.giroEnMano = new Vector3(18.2f, 122.4f, 87.5f);
                    route.terminarAlVaciarOrigen = true;
                    if (spVicGo != null)
                    {
                        route.puntoAlTerminar = spVicGo.transform;
                    }
                    EditorUtility.SetDirty(route);
                    Debug.Log($"[FixCap2] MainWorld Oficio_Victoria configurado: offset={route.offsetEnMano}, giro={route.giroEnMano}, terminarAlVaciar={route.terminarAlVaciarOrigen}, puntoAlTerminar={(route.puntoAlTerminar != null ? route.puntoAlTerminar.name : "null")}");
                }
                else
                {
                    Debug.LogWarning("[FixCap2] Oficio_Victoria no tiene NPCWorkRoute");
                }
            }
            else
            {
                Debug.LogWarning("[FixCap2] No se encontró el GameObject Oficio_Victoria en MainWorld");
            }

            var anchorEldran = GameObject.Find("Anchor_Eldran_PuntoGuardado");
            if (anchorEldran != null)
            {
                Debug.Log($"[FixCap2] Anchor_Eldran_PuntoGuardado en: {anchorEldran.transform.position}");
            }

            EditorSceneManager.MarkSceneDirty(mwScene);
            EditorSceneManager.SaveScene(mwScene);
            Debug.Log("[FixCap2] MainWorld.unity guardada.");
        }

        // 5. Configurar Cap2.asset
        string cap2Path = "Assets/NarrativeGraph/Cap2.asset";
        var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(cap2Path);
        if (graph != null)
        {
            var node15 = graph.FindNode("cap2-manana-despues-15");
            if (node15 != null)
            {
                var existingPlace = graph.FindNode("cap2-eldran-a-pueblo");
                if (existingPlace == null)
                {
                    var placeNode = new PlaceActorNode();
                    placeNode.guid = "cap2-eldran-a-pueblo";
                    placeNode.displayTitle = "15b.- Eldran sale al pueblo (puntería)";
                    placeNode.chapter = "Cap. 2";
                    placeNode.actorId = "NPC_Eldran";
                    placeNode.markId = "Eldran_PuntoGuardado";
                    placeNode.showActor = true;
                    placeNode.position = new Vector2(6650f, 0f);
                    placeNode.outputs = new List<string> { "cap2-prepara-viaje-fork" };
                    graph.nodes.Add(placeNode);

                    node15.outputs.Clear();
                    node15.outputs.Add("cap2-eldran-a-pueblo");

                    EditorUtility.SetDirty(graph);
                    Debug.Log("[FixCap2] Cap2.asset: PlaceActorNode 'cap2-eldran-a-pueblo' insertado entre node15 y fork.");
                }
                else
                {
                    Debug.Log("[FixCap2] Cap2.asset: PlaceActorNode 'cap2-eldran-a-pueblo' ya existe.");
                }
            }
            else
            {
                Debug.LogError("[FixCap2] No se encontró node15 (cap2-manana-despues-15) en Cap2.asset");
            }
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.LogError("[FixCap2] No se pudo cargar Cap2.asset");
        }

        Debug.Log("[FixCap2] ✅ PROCESO DE FIX COMPLETADO CON ÉXITO.");
    }
}
#endif
