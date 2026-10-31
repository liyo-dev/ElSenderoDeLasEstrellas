#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Monta las fases 2 y 3 del plan «NPCs vivos según la historia»: la revancha con Erika (pregunta
/// de Sí / No y combate de práctica que se repite, con Esencia solo la primera vez), el álbum de
/// Oliver en su prefab y las tres piezas de su colección escondidas en el pueblo. Ver INC-878.
/// Se puede ejecutar varias veces: lo que ya está se deja como está.
public static class NpcsVivosMontaje
{
    const string GrafoSecundario = "Assets/NarrativeGraph/Secundary.asset";
    const string PrefabErika = "Assets/_NPCs/Combat/Erika.prefab";
    const string CombateErika = "Assets/_NPCs/Combat/NPC_Combat_Config_Erika.asset";
    const string DialogoRevanchaErika = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_REVANCHA_FIN.asset";
    const string PrefabOliver = "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab";
    const string ColeccionOliver = "Assets/_ITEMS/Colecciones/Coleccion_Oliver.asset";
    const string EstiloMenu = "Assets/_UI/Estilo_MenuSencillo.asset";
    const string EscenaPueblo = "Assets/Scenes/Worlds/MainWorld.unity";
    const string SenalErikaDerrotada = "ERIKA_ENTRENAMIENTO_DERROTADA";
    const string RaizDeTesoros = "Tesoros del pueblo (generado)";

    // Dónde se esconde cada pieza: junto a una marca o a un punto de aparición conocidos del pueblo.
    static readonly (string id, string nombre, string referencia, Vector3 desvio, string objeto)[] Piezas =
    {
        ("PUEBLO_COLECCION_CANICA", "Tesoro · canica de cristal", "SPAWN_Erika", new Vector3(-7f, 0f, 5f), "Assets/_ITEMS/IT_CanicaCristal.asset"),
        ("PUEBLO_COLECCION_PIEDRA", "Tesoro · piedra con agujero", "Eldran_Plaza", new Vector3(8f, 0f, -6f), "Assets/_ITEMS/IT_PiedraConAgujero.asset"),
        ("PUEBLO_COLECCION_MONEDA", "Tesoro · moneda antigua", "Eldran_PuntoGuardado", new Vector3(-6f, 0f, -7f), "Assets/_ITEMS/IT_MonedaAntigua.asset"),
    };

    [MenuItem("El Sendero/Archivo/NPCs vivos/Montar Erika, Oliver y su colección (INC-878)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("NPCs vivos", "Sal del modo Play antes de ejecutarlo.", "Vale");
            return;
        }

        var informe = new List<string>();
        MontarRevanchaDeErika(informe);
        MontarErika(informe);
        MontarOliver(informe);
        EsconderPiezas(informe);
        AssetDatabase.SaveAssets();

        string texto = string.Join("\n", informe);
        Debug.Log("[NPCs vivos] Montado:\n" + texto);
        EditorUtility.DisplayDialog("NPCs vivos", texto, "Vale");
    }

    // ── Erika ────────────────────────────────────────────────────────────────────────────────

    static void MontarRevanchaDeErika(List<string> informe)
    {
        var grafo = Cargar<NarrativeGraph>(GrafoSecundario);
        var inicio = grafo.nodes.OfType<StartNode>().FirstOrDefault()
                     ?? throw new InvalidOperationException("El grafo secundario no tiene nodo de inicio.");

        var espera = Asegurar(grafo, "sec-erika-revancha-0", () => new WaitCustomEventNode { eventKey = SenalErikaDerrotada, acceptEarlierEvent = true },
            "Erika ya te ha entrenado", inicio.position + new Vector2(300f, 900f));
        var hablar = Asegurar(grafo, "sec-erika-revancha-1", () => new WaitNpcInteractionNode { npcId = "NPC_Erika", showMarker = false },
            "Hablar con Erika (entrenar)", espera.position + new Vector2(280f, 0f));
        var pregunta = Asegurar(grafo, "sec-erika-revancha-2", () => new DialogueChoiceNode
            {
                promptTextId = "ERIKA_REVANCHA_PREGUNTA",
                promptText = "Erika: ¿Otra ronda de entrenamiento?",
            },
            "¿Otra ronda?", hablar.position + new Vector2(280f, 0f));
        var combate = Asegurar(grafo, "sec-erika-revancha-3", () => new StartNpcCombatNode
            {
                npcId = "NPC_Erika",
                defeatEventKey = SenalErikaDerrotada,
                waitForDefeat = true,
                restorePeacefulOnDefeat = true,
            },
            "Combate de práctica con Erika", pregunta.position + new Vector2(280f, 0f));

        espera.outputs = new List<string> { hablar.guid };
        hablar.outputs = new List<string> { pregunta.guid };
        pregunta.outputs = new List<string> { combate.guid, hablar.guid }; // Sí · No
        combate.outputs = new List<string> { hablar.guid };
        if (!inicio.outputs.Contains(espera.guid)) inicio.outputs.Add(espera.guid);

        EditorUtility.SetDirty(grafo);
        informe.Add("Secundarias: revancha con Erika (habla → ¿otra ronda? → combate → vuelta a empezar).");
    }

    static void MontarErika(List<string> informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(PrefabErika);
        try
        {
            var orbes = raiz.GetComponentInChildren<OrbDropper>(true) ?? throw new InvalidOperationException("Erika no tiene OrbDropper.");
            var so = new SerializedObject(orbes);
            so.FindProperty("_monedaSoloLaPrimeraVez").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabErika);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        var config = new SerializedObject(Cargar<ScriptableObject>(CombateErika));
        config.FindProperty("dialogueOnDizzyRepeat").objectReferenceValue = Cargar<ScriptableObject>(DialogoRevanchaErika);
        config.ApplyModifiedPropertiesWithoutUndo();
        informe.Add("Erika: Esencia solo la primera vez; en las revanchas dice «Has mejorado, Will…».");
    }

    // ── Oliver ───────────────────────────────────────────────────────────────────────────────

    static void MontarOliver(List<string> informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(PrefabOliver);
        try
        {
            var coleccionista = raiz.GetComponent<Coleccionista>() ?? raiz.AddComponent<Coleccionista>();
            var so = new SerializedObject(coleccionista);
            so.FindProperty("coleccion").objectReferenceValue = Cargar<ColeccionDeObjetos>(ColeccionOliver);
            so.FindProperty("estilo").objectReferenceValue = Cargar<EstiloDeMenuSencillo>(EstiloMenu);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabOliver);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        informe.Add("Oliver: coleccionista con su álbum.");
    }

    static void EsconderPiezas(List<string> informe)
    {
        var escena = EditorSceneManager.GetSceneByPath(EscenaPueblo);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(EscenaPueblo, OpenSceneMode.Additive);

        var raices = escena.GetRootGameObjects();
        var contenedor = raices.FirstOrDefault(g => g.name == RaizDeTesoros);
        if (contenedor == null)
        {
            contenedor = new GameObject(RaizDeTesoros);
            EditorSceneManager.MoveGameObjectToScene(contenedor, escena);
        }

        Physics.SyncTransforms();
        int capaSuelo = LayerMask.GetMask("Floor");
        foreach (var p in Piezas)
        {
            if (contenedor.transform.Find(p.nombre) != null) { informe.Add($"Ya estaba: {p.nombre}."); continue; }
            var referencia = BuscarReferencia(raices, p.referencia);
            if (referencia == null) { informe.Add($"No encuentro «{p.referencia}» en MainWorld: {p.nombre} sin colocar."); continue; }

            var pie = referencia.position + p.desvio;
            if (Physics.Raycast(pie + Vector3.up * 30f, Vector3.down, out var golpe, 80f, capaSuelo == 0 ? ~0 : capaSuelo))
                pie = golpe.point;
            var tesoro = CombatLabBuilder.CrearTesoro(p.nombre, contenedor.transform, pie, 0f, p.id, Tesoro.Forma.Cofre, "PUEBLO");
            CombatLabBuilder.Recompensas(tesoro, new CombatLabBuilder.Premio(PickupEffectType.AddToInventory, p.objeto, 1));
            informe.Add($"{p.nombre}: junto a {p.referencia}, en {pie:F1}.");
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
    }

    // Una marca (SpawnAnchor con ese id) o un objeto con ese nombre (puntos de aparición del roster).
    static Transform BuscarReferencia(GameObject[] raices, string id)
    {
        foreach (var r in raices)
        {
            var marca = r.GetComponentsInChildren<SpawnAnchor>(true).FirstOrDefault(a => a.anchorId == id);
            if (marca != null) return marca.transform;
        }
        foreach (var r in raices)
        {
            var t = r.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == id);
            if (t != null) return t;
        }
        return null;
    }

    static NarrativeNode Asegurar(NarrativeGraph grafo, string guid, Func<NarrativeNode> crear, string titulo, Vector2 posicion)
    {
        var nodo = grafo.FindNode(guid);
        if (nodo == null)
        {
            nodo = crear();
            nodo.guid = guid;
            nodo.position = posicion;
            grafo.nodes.Add(nodo);
        }
        nodo.displayTitle = titulo;
        return nodo;
    }

    static T Cargar<T>(string ruta) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("No se encuentra " + ruta);
}
#endif
