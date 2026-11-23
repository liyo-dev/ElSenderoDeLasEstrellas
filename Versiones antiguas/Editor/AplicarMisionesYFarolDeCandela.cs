using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Liyodev.NarrativeGraph;

public static partial class MontajeDeLosCapitulos
{
    [MenuItem("El Sendero/Historia/Aplicar misiones y farol de Candela")]
    public static void AplicarMisionesYFarolDeCandela()
    {
        const string titulo = "Misiones y farol de Candela";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar(titulo, "Sal de Play antes de aplicar el cambio.");
            return;
        }

        const string ruta = "Assets/NarrativeGraph/Cap3.asset";
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(ruta);
        var inicio = grafo.FindNode("cap3-mision") as StartQuestNode;
        var pasos = new[] { "paso-llegar", "paso-posada", "paso-pez", "paso-lubina", "paso-farol" }
            .Select(id => grafo.FindNode("cap3-" + id) as CompleteQuestStepsNode).ToArray();
        var cierre = pasos[pasos.Length - 1];
        var candela = grafo.FindNode("cap3-candela-dlg");
        if (inicio == null || pasos.Any(p => p == null) || candela?.Outputs?.Contains(cierre.Guid) != true)
            throw new System.InvalidOperationException("La ruta de Candela en Cap3 no coincide con la esperada.");

        var mundo = SceneManager.GetSceneByPath(RutaMainWorld);
        if (!mundo.isLoaded) mundo = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);
        var objetos = mundo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var casa = objetos.Single(t => t.name == CasaDelFarol);
        var farol = objetos.Single(t => t.name == "Farol de Candela");
        if (!farol.GetComponentInChildren<LuzNocturna>(true))
            throw new System.InvalidOperationException("El farol de Candela no tiene su luz nocturna.");

        s_misiones.Clear();
        MontarMisionesDeLaAldea();
        var misionAldea = s_misiones.Single(m => m.questId == MisionDeLaAldea);
        s_misiones.Clear();
        var start = SceneManager.GetSceneByPath(RutaStart);
        bool cerrarStart = !start.isLoaded;
        if (cerrarStart) start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        var questManager = new SerializedObject(Buscar<QuestManager>(start));
        var catalogo = questManager.FindProperty("questCatalog");
        bool registrada = false;
        for (int i = 0; i < catalogo.arraySize; i++)
            registrada |= catalogo.GetArrayElementAtIndex(i).objectReferenceValue == misionAldea;
        if (!registrada)
        {
            int indice = catalogo.arraySize;
            catalogo.arraySize++;
            catalogo.GetArrayElementAtIndex(indice).objectReferenceValue = misionAldea;
            questManager.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(start);
        }
        bool startGuardado = !start.isDirty || EditorSceneManager.SaveScene(start);
        if (cerrarStart && startGuardado) EditorSceneManager.CloseScene(start, true);

        inicio.QuestId = MisionDeLaAldea;
        inicio.DisplayTitle = "Misión: pistas en la Aldea del Mar";
        foreach (var paso in pasos) paso.QuestId = MisionDeLaAldea;
        cierre.CompleteQuest = true;
        cierre.DisplayTitle = "Paso: hablar con Candela (cierra Pistas en la Aldea del Mar)";
        var marcas = grafo.FindNode("cap3-mision-marcas") as StartQuestNode;
        if (marcas == null)
        {
            var cap = new Capitulo { Prefijo = "cap3-", Nombre = cierre.Chapter };
            marcas = new StartQuestNode { QuestId = MisionCap3, NoGiver = true };
            Poner(grafo, cap, "cap3-mision-marcas", "Misión: las marcas de hierro",
                cierre.Position + new Vector2(0f, 145f), marcas, new List<string>(cierre.Outputs));
            cierre.Outputs = new List<string> { marcas.Guid };
        }
        EditorUtility.SetDirty(grafo);

        var senalesAldea = new HashSet<string> { SD.Acampada, S3.LlegadaAldea, S3.Boya1, S3.Boya2 };
        var llegadas = mundo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SignalEmitter>(true))
            .Where(s => senalesAldea.Contains(s.eventKey)).ToArray();
        var objetivosAldea = new HashSet<GameObject>(llegadas.Select(s => s.gameObject));
        foreach (var senal in llegadas)
        {
            senal.soloConLaMision = MisionDeLaAldea;
            PrefabUtility.RecordPrefabInstancePropertyModifications(senal);
        }
        foreach (var marcador in mundo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<QuestObjectiveMarker>(true)))
        {
            var datos = new SerializedObject(marcador);
            var mision = datos.FindProperty("questId");
            if (mision.stringValue != MisionCap3) continue;
            var paso = datos.FindProperty("stepIndex");
            if (objetivosAldea.Contains(marcador.gameObject) || paso.intValue == 3) mision.stringValue = MisionDeLaAldea;
            else if (paso.intValue == 5) paso.intValue = 0;
            datos.ApplyModifiedPropertiesWithoutUndo();
        }

        ColocarFarolJuntoALaPuerta(casa, farol);
        PrefabUtility.RecordPrefabInstancePropertyModifications(farol);
        EditorSceneManager.MarkSceneDirty(mundo);
        bool mundoGuardado = EditorSceneManager.SaveScene(mundo);
        TextosDeLaHistoria.Escribir();
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar(titulo, $"Cap3 y catálogo actualizados. Farol en {farol.position:F2}. " +
            (mundoGuardado ? "MainWorld guardada." : "MainWorld queda pendiente de guardar.") +
            (startGuardado ? " Start guardada." : " Start queda pendiente de guardar."));
    }
}
