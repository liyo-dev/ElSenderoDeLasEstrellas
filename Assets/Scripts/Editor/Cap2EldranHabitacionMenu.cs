#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Corrige la permanencia de Eldran en WillHouse tras la cinemática de la vela y su traslado a la plaza del pueblo.
/// Ver INC-854.
/// </summary>
public static class Cap2EldranHabitacionMenu
{
    private const string MainWorldPath = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string Cap2GraphPath = "Assets/NarrativeGraph/Cap2.asset";
    private const string AnchorPlazaId = "Eldran_Plaza";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Ajustar salida de Eldran de la habitación (INC-854)")]
    public static void Corregir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de ejecutar el ajuste.");

        // 1. Asegurar SpawnAnchor en SP_Eldran en MainWorld
        AsegurarAnchorPlazaMainWorld();

        // 2. Ajustar grafo Cap2.asset
        AjustarGrafoCap2();

        Debug.Log("[Cap2EldranHabitacionMenu] ✓ INC-854 aplicado con éxito:\n" +
                  "  - SpawnAnchor 'Eldran_Plaza' asegurado en SP_Eldran (MainWorld).\n" +
                  "  - Nodo 15a 'cap2-manana-espera-salida' insertado esperando WILL_EXITS_HOUSE.\n" +
                  "  - Nodo 15b actualizado para colocar a Eldran en 'Eldran_Plaza'.\n" +
                  "  - Eldran permanece en la habitación tras la cinemática y solo va a la plaza al salir Will.");
    }

    private static void AsegurarAnchorPlazaMainWorld()
    {
        var escena = SceneManager.GetSceneByPath(MainWorldPath);
        bool mustClose = false;
        if (!escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene(MainWorldPath, OpenSceneMode.Additive);
            mustClose = true;
        }

        if (!escena.IsValid())
            throw new InvalidOperationException($"No se pudo abrir la escena '{MainWorldPath}'.");

        GameObject spEldran = null;
        foreach (var root in escena.GetRootGameObjects())
        {
            spEldran = BuscarObjeto(root.transform, "SP_Eldran");
            if (spEldran != null) break;
        }

        if (spEldran == null)
            throw new InvalidOperationException("No se encontró el objeto 'SP_Eldran' en MainWorld.");

        var anchor = spEldran.GetComponent<SpawnAnchor>();
        if (anchor == null)
        {
            anchor = spEldran.AddComponent<SpawnAnchor>();
            Undo.RegisterCreatedObjectUndo(anchor, "Añadir SpawnAnchor a SP_Eldran");
        }

        if (anchor.anchorId != AnchorPlazaId)
        {
            Undo.RecordObject(anchor, "Asignar anchorId Eldran_Plaza");
            anchor.anchorId = AnchorPlazaId;
            EditorUtility.SetDirty(anchor);
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        if (mustClose)
        {
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static void AjustarGrafoCap2()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Cap2GraphPath);
        if (grafo == null)
            throw new InvalidOperationException($"No se pudo cargar '{Cap2GraphPath}'.");

        Undo.RecordObject(grafo, "Ajustar salida de Eldran de la habitación (INC-854)");

        var nodo15 = grafo.FindNode("cap2-manana-despues-15") as CheckpointNode
            ?? throw new InvalidOperationException("Falta el nodo 'cap2-manana-despues-15' en Cap2.asset.");

        var nodo15b = grafo.FindNode("cap2-eldran-a-pueblo") as PlaceActorNode
            ?? throw new InvalidOperationException("Falta el nodo 'cap2-eldran-a-pueblo' en Cap2.asset.");

        // Nodo 15a: Esperar salida de casa
        var esperaGuid = "cap2-manana-espera-salida";
        var espera = grafo.FindNode(esperaGuid) as WaitCustomEventNode;
        if (espera == null)
        {
            espera = new WaitCustomEventNode();
            espera.guid = esperaGuid;
            grafo.nodes.Add(espera);
        }

        espera.chapter = "Cap. 2";
        espera.displayTitle = "15a.- Will sale de casa (WILL_EXITS_HOUSE)";
        espera.eventKey = "WILL_EXITS_HOUSE";
        espera.acceptEarlierEvent = false;
        espera.blockSaving = false;
        espera.position = new Vector2(6000, 0);
        espera.outputs = new List<string> { nodo15b.guid };

        // Conectar nodo 15 -> nodo 15a
        nodo15.outputs = new List<string> { esperaGuid };

        // Conectar nodo 15b -> fork (en la plaza del pueblo)
        nodo15b.displayTitle = "15b.- Eldran sale a la plaza (puntería)";
        nodo15b.actorId = "NPC_Eldran";
        nodo15b.markId = AnchorPlazaId;
        nodo15b.showActor = true;
        nodo15b.position = new Vector2(6420, 0);
        nodo15b.outputs = new List<string> { "cap2-prepara-viaje-fork" };

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
    }

    private static GameObject BuscarObjeto(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre.gameObject;
        for (int i = 0; i < padre.childCount; i++)
        {
            var res = BuscarObjeto(padre.GetChild(i), nombre);
            if (res != null) return res;
        }
        return null;
    }
}
#endif
