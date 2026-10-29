#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using Game.NPC;
using Game.NPC.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Ajustes de assets y escenas que acompañan al rediseño del 29 oct 2026. Se ejecuta una vez desde
/// el buzón de órdenes y después se archiva. Deja su informe en Logs/claude/herramienta.txt.
public static class HerramientaDeUnSoloUso
{
    private const string Cap2 = "Assets/NarrativeGraph/Cap2.asset";
    private const string MainWorld = "Assets/Scenes/Worlds/MainWorld.unity";

    [MenuItem("El Sendero/Archivo/Rediseño 29 oct/Fase 5")]
    public static void Fase5()
    {
        var informe = new StringBuilder();

        // Bola Prisma la enseña Eldran (GDD 2.3); Erika enseña la Protección, y antes del combate,
        // que es el entrenamiento de defensa.
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Cap2);
        var hablar = grafo.FindNode("cap2-prepara-viaje-erika-0");
        var combate = grafo.FindNode("cap2-prepara-viaje-erika-combate");
        var aprender = grafo.FindNode("cap2-prepara-viaje-erika-desbloqueo") as UnlockAbilitiesNode;
        var panuelo = grafo.FindNode("cap2-prepara-viaje-erika-item");
        if (hablar != null && combate != null && aprender != null && panuelo != null)
        {
            Undo.RecordObject(grafo, "Erika enseña la Protección antes del combate");
            hablar.outputs = new() { aprender.guid };
            aprender.outputs = new() { combate.guid };
            combate.outputs = new() { panuelo.guid };
            aprender.spellsToUnlock.Clear();
            aprender.displayTitle = "22b-1.- Aprender Protección";
            EditorUtility.SetDirty(grafo);
            AssetDatabase.SaveAssets();
            informe.AppendLine("Cap2: Erika enseña solo la Protección, antes del combate.");
        }
        else informe.AppendLine("Cap2: NO encuentro algún nodo de la rama de Erika.");

        // La discusión de Eldran y Victoria: lo que antes iba escrito en el código pasa al prefab.
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null || prefab.GetComponentInChildren<DiscusionEnBucle>(true) == null) continue;
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            foreach (var d in raiz.GetComponentsInChildren<DiscusionEnBucle>(true))
            {
                var so = new SerializedObject(d);
                Anadir(so, "tramo.empiezaConSenal", "PROLOGUE_DONE");
                Anadir(so, "tramo.empiezaConSenal", "HOOK_DISCUSION_START");
                Anadir(so, "tramo.empiezaConMisionActiva", "WILL_MISSION0");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            PrefabUtility.UnloadPrefabContents(raiz);
            informe.AppendLine("Discusión: tramo completado en " + ruta);
        }

        // Oliver trabaja: el paseo para «no estar quieto» está congelado (AGENTS.md §8).
        foreach (var guid in AssetDatabase.FindAssets("NPC_Ambient_Oliver t:NPCAmbientConfig"))
        {
            var cfg = AssetDatabase.LoadAssetAtPath<NPCAmbientConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (cfg == null || !cfg.permitirTrabajo || !cfg.enableWander) continue;
            cfg.enableWander = false;
            EditorUtility.SetDirty(cfg);
            informe.AppendLine("Oliver: sin paseo mientras trabaja.");
        }
        AssetDatabase.SaveAssets();

        // Las rutas de oficio pasan sus condiciones de texto al tramo al cargar; guardar MainWorld lo deja escrito.
        var escena = SceneManager.GetSceneByPath(MainWorld);
        bool abiertaAqui = !escena.isLoaded;
        if (!abiertaAqui && escena.isDirty)
        {
            informe.AppendLine("MainWorld tiene cambios sin guardar: no la toco (las rutas se migran solas al cargar).");
        }
        else
        {
            if (abiertaAqui) escena = EditorSceneManager.OpenScene(MainWorld, OpenSceneMode.Additive);
            int n = 0;
            foreach (var r in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NPCWorkRoute>(true)))
            {
                EditorUtility.SetDirty(r);
                n++;
            }
            if (n > 0) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
            informe.AppendLine($"MainWorld: {n} rutas de oficio guardadas con su tramo.");
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }

        File.WriteAllText(Path.Combine("Logs", "claude", "herramienta.txt"), informe.ToString());
    }

    private static void Anadir(SerializedObject so, string lista, string valor)
    {
        var p = so.FindProperty(lista);
        if (p == null) return;
        for (int i = 0; i < p.arraySize; i++)
            if (p.GetArrayElementAtIndex(i).stringValue == valor) return;
        p.arraySize++;
        p.GetArrayElementAtIndex(p.arraySize - 1).stringValue = valor;
    }
}
#endif
