using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cap1: vuelve «Will, ¡despierta!» con el iris sobre Will dormido, en lugar del despertar
/// subjetivo con párpados (INC-450).
///
/// Por AssetDatabase (Unity tiene Cap1 y Start en memoria). Idempotente:
///   1. El nodo 4 de Cap1 («Se destapa la habitación», fundido desde blanco) pasa a ser un
///      DramaticTextNode con DramaticText_DespiertaWill. La frase tapa el blanco en cuanto aparece
///      y sale con el iris, que destapa el plano cenital de la cama. Después, el nodo 5
///      («Pulsa A para despertar a Will»), igual que antes.
///   2. Asigna Mat_CircleIrisCutout al DramaticTextOverlayUI de Start.unity.
/// </summary>
public static class Cap1RecuperarDespiertaWill
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaFrase = "Assets/_DIALOGUES/Prologo/DramaticText_DespiertaWill.asset";
    private const string RutaIris = "Assets/Shaders/UI/Mat_CircleIrisCutout.mat";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string Nodo4 = "85279f14-0f9a-4376-8ca3-126ecb12b966";
    private const string Nodo5 = "d206615e-7e83-4d62-bd5a-d6e04d6b237b";

    [MenuItem("El Sendero/Archivo/Narrativa/Cap1: recuperar «Will, ¡despierta!» (iris sobre Will dormido)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Cap1", "Sal del Play antes de lanzar esto.", "Vale");
            return;
        }

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        var frase = AssetDatabase.LoadAssetAtPath<DramaticPhraseConfig>(RutaFrase);
        var iris = AssetDatabase.LoadAssetAtPath<Material>(RutaIris);
        if (grafo == null || frase == null || iris == null)
        {
            Debug.LogError($"[Cap1RecuperarDespiertaWill] Falta {(grafo == null ? RutaGrafo : frase == null ? RutaFrase : RutaIris)}.");
            return;
        }

        var log = new System.Text.StringBuilder("[Cap1RecuperarDespiertaWill]\n");

        // 1. Grafo
        var viejo = grafo.FindNode(Nodo4);
        if (viejo is DramaticTextNode dt && dt.config == frase) log.AppendLine("= El nodo 4 ya es «Will, ¡despierta!».");
        else if (viejo == null) log.AppendLine("✗ No encuentro el nodo 4 en Cap1.");
        else
        {
            Undo.RecordObject(grafo, "Cap1: «Will, ¡despierta!»");
            var nuevo = new DramaticTextNode
            {
                guid = viejo.guid, position = viejo.position, chapter = viejo.chapter, blockSaving = true,
                displayTitle = "4.- Will, ¡despierta! (el iris destapa a Will dormido)",
                config = frase, waitForCompletion = true,
                restoreSceneMusicWhenDone = true, restoreMusicFade = 3f,
            };
            nuevo.outputs = new System.Collections.Generic.List<string> { Nodo5 };
            grafo.nodes[grafo.nodes.IndexOf(viejo)] = nuevo;
            EditorUtility.SetDirty(grafo);
            AssetDatabase.SaveAssets();
            log.AppendLine("✓ Nodo 4 → «Will, ¡despierta!» con iris (antes: fundido desde blanco).");
        }

        // 2. Material del iris en el overlay de Start
        var start = SceneManager.GetSceneByPath(RutaStart);
        bool abiertaAqui = false;
        if (!start.isLoaded)
        {
            start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
            abiertaAqui = true;
        }
        bool yaTeniaCambios = !abiertaAqui && start.isDirty;

        var overlay = start.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<DramaticTextOverlayUI>(true))
            .FirstOrDefault();
        if (overlay == null) log.AppendLine("✗ No hay DramaticTextOverlayUI en Start.unity.");
        else
        {
            var so = new SerializedObject(overlay);
            var prop = so.FindProperty("_irisMaterial");
            if (prop.objectReferenceValue == iris) log.AppendLine("= El overlay ya tiene el material del iris.");
            else
            {
                prop.objectReferenceValue = iris;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(start);
                if (yaTeniaCambios)
                    log.AppendLine("✓ Material del iris asignado. Start tenía cambios sin guardar: guárdala tú (Ctrl+S).");
                else
                {
                    EditorSceneManager.SaveScene(start);
                    log.AppendLine("✓ Material del iris asignado al overlay de Start (guardada).");
                }
            }
        }
        if (abiertaAqui) EditorSceneManager.CloseScene(start, true);

        log.AppendLine("Si la ventana del grafo está abierta, ciérrala y vuelve a abrirla.");
        Debug.Log(log.ToString());
    }
}
