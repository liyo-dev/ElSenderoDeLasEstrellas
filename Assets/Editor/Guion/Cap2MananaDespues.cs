#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Monta los datos y el escenario de la mañana mediante las API del Editor. Repetirlo conserva los IDs.
public static class Cap2MananaDespues
{
    const string Secuencia = "Assets/_SEQUENCES/SEQ_MananaDespues.asset";
    const string Mision = "Assets/_QUEST/PRINCIPALES/PRINCIPALES/Q_PREPARA_VIAJE.asset";
    const string Objeto = "Assets/_ITEMS/IT_TrozoDelAro.asset";
    const string Dialogo = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_PREPARA_VIAJE_EN_CURSO.asset";
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Inicio = "Assets/Scenes/Systems/Start.unity";
    const string Carpeta = "Assets/_VFX/MananaDespues";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Montar la mañana después")]
    public static void Asegurar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("El montaje se ejecuta fuera de Play.");
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap2.asset");
        if (grafo == null) throw new InvalidOperationException("Falta Cap2.");
        var antiguo = grafo.FindNode("c07008ca-ae29-4d50-848f-8c80e0dfa16a");
        if (antiguo == null) throw new InvalidOperationException("Falta el nodo 7 del tramo de Liam.");
        var casa = EditorSceneManager.GetSceneByPath(Casa);
        if (!casa.isLoaded) casa = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = casa.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var dormitorio = todos.Select(t => t.GetComponent<SpawnAnchor>()).FirstOrDefault(a => a != null && a.anchorId == "Bedroom");
        var molde = todos.Select(t => t.GetComponent<SequencePlayer>()).FirstOrDefault(p => p != null && p.name == "SEQ_DiscusionVentana");
        if (dormitorio == null || molde == null) throw new InvalidOperationException("Faltan Bedroom o SEQ_DiscusionVentana.");
        var entorno = dormitorio.GetComponent<AnchorEnvironment>();
        if (entorno == null) throw new InvalidOperationException("Bedroom necesita AnchorEnvironment.");

        var item = Asset<ItemData>(Objeto);
        item.itemId = "trozo_del_aro";
        item.displayNameId = "ITEM_TROZO_DEL_ARO_NAME";
        item.displayName = "Trozo del aro";
        item.useDescriptionId = "ITEM_TROZO_DEL_ARO_DESC";
        item.useDescription = "Un pedazo del aro de runas del demonio. Eldran guardó el resto en el cofre de sal.";
        item.icon = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Key.asset").icon;
        item.usableFromInventory = false;
        item.usageKind = ItemData.ItemUsageKind.Quest;
        item.buyPrice = item.sellValue = 0;
        EditorUtility.SetDirty(item);
        var quest = Asset<QuestData>(Mision);
        quest.questId = "PREPARA_VIAJE";
        quest.displayNameId = "QUEST_PREPARA_VIAJE_NAME";
        quest.displayName = "Prepárate para el viaje";
        quest.descriptionId = "QUEST_PREPARA_VIAJE_DESC";
        quest.description = "Antes de ir al Bosque Prohibido, prepárate en el pueblo.";
        quest.detailedDescriptionId = "QUEST_PREPARA_VIAJE_DETAIL";
        quest.detailedDescription = "Eldran quiere que no salgas con las manos vacías: pasa por la tienda de Victoria, por la boticaria y por el campo de entrenamiento de Erika, y practica la puntería con él.";
        string[] condiciones = { "CAPA", "POCION", "ERIKA", "ELDRAN" };
        string[] pasos = { "Pide una capa a Victoria", "Visita a la boticaria", "Entrena con Erika", "Practica la puntería con Eldran" };
        quest.steps = condiciones.Select((c, i) => new QuestData.Step { conditionId = "PREPARA_VIAJE_" + c,
            descriptionId = "QUEST_PREPARA_VIAJE_STEP" + (i + 1), description = pasos[i] }).ToArray();
        EditorUtility.SetDirty(quest);
        var dialogo = Asset<DialogueAsset>(Dialogo);
        dialogo.lines = new[] { new DialogueLine { speakerNameId = "CHAR_ELDRAN", textId = "DLG_ELDRAN_PREPARA_VIAJE_EN_CURSO", emotion = NPCEmotion.Neutral } };
        EditorUtility.SetDirty(dialogo);
        var def = Asset<SequenceDefinition>(Secuencia);
        def.displayName = "La mañana después";
        def.summary = "Will practica con una vela. Eldran le cuenta cómo lo encontró y le entrega un trozo del aro para Estela.";
        def.signalIn = "MANANA_DESPUES_START";
        def.signalOut = "MANANA_DESPUES_DONE";
        def.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        def.protagonistas = new List<string> { "Player", "NPC_Eldran" };
        def.endStayBlack = false;
        if (def.phases.Count == 0) def.phases.Add(new SequencePhase { name = "Guion", beats = new List<SequenceBeat> { new GuionBeat { note = "Guion: Cap2_02_MananaDespues" } } });
        EditorUtility.SetDirty(def);

        var raiz = todos.FirstOrDefault(t => t.name == "SEQ_MananaDespues")?.gameObject;
        if (raiz == null) { raiz = new GameObject("SEQ_MananaDespues"); SceneManager.MoveGameObjectToScene(raiz, casa); }
        var stage = raiz.GetComponent<SequenceStage>() ?? raiz.AddComponent<SequenceStage>();
        var player = raiz.GetComponent<SequencePlayer>() ?? raiz.AddComponent<SequencePlayer>();
        EditorUtility.CopySerialized(molde, player);
        var so = new SerializedObject(player);
        so.FindProperty("_definition").objectReferenceValue = def;
        so.FindProperty("_stage").objectReferenceValue = stage;
        so.FindProperty("_interiorAnchor").objectReferenceValue = entorno;
        so.FindProperty("_startAtPhase").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
        var escenarioMolde = molde.GetComponent<SequenceStage>();
        if (escenarioMolde != null) EditorUtility.CopySerialized(escenarioMolde, stage);
        var props = new List<SequenceStage.PropActor>();
        Transform Marca(string nombre, Vector3 p, float rumbo = 0)
        {
            var t = raiz.transform.Find(nombre);
            if (t == null) { t = new GameObject(nombre).transform; t.SetParent(raiz.transform, false); }
            t.SetPositionAndRotation(p, Quaternion.Euler(0, rumbo, 0));
            var a = t.GetComponent<SpawnAnchor>() ?? t.gameObject.AddComponent<SpawnAnchor>();
            a.anchorId = nombre;
            return t;
        }
        Marca("M_Manana_Will", new Vector3(115.75f, 0, 3.55f), -90);
        Marca("M_Manana_EldranPuerta", new Vector3(116.4f, 0, 5.0f), 180);
        Marca("M_Manana_EldranMesa", new Vector3(116.0f, 0, 2.25f), -15);
        Marca("M_Manana_EldranSilla", new Vector3(114.1f, 0.29f, 2.05f), 35);
        Marca("M_Manana_EldranCofre", new Vector3(115.4f, 0, 0.4f), 180);
        Marca("M_Manana_Vela", new Vector3(114.9f, 0.92f, 3.55f));
        Marca("M_Manana_Bollo", new Vector3(114.65f, 0.72f, 3.9f));
        Marca("M_Manana_Aro", new Vector3(114.65f, 0.75f, 3.1f));
        void Prop(string id, Transform t) { props.Add(new SequenceStage.PropActor { id = id, target = t, eyeHeight = 0 }); }
        Transform ColocarPrefab(string id, string ruta, Vector3 p, float tamano)
        {
            var t = raiz.transform.Find(id);
            if (t == null)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (pf == null) throw new InvalidOperationException("Falta " + ruta);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, casa);
                go.name = id; t = go.transform; t.SetParent(raiz.transform, false);
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                var rr = go.GetComponentsInChildren<Renderer>(true);
                if (rr.Length > 0)
                {
                    var b = rr[0].bounds; foreach (var r in rr) b.Encapsulate(r.bounds);
                    t.localScale *= tamano / Mathf.Max(b.size.x, b.size.y, b.size.z);
                }
            }
            t.position = p; Prop(id, t); return t;
        }
        ColocarPrefab("PROP_Vela", "Assets/Art/World/Modular Castle/Assets/prefabs/candle1.prefab", new Vector3(114.9f, 0.69f, 3.55f), 0.22f);
        var bollo = ColocarPrefab("PROP_Bollo", "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Food/Bread02_a01.prefab", new Vector3(115.9f, 0.89f, 2.25f), 0.28f);
        bollo.gameObject.SetActive(false);
        var cofre = todos.First(t => t.name == "Cabinet34_a01 (1)");
        Prop("PROP_CofreSal", cofre);
        var aro = raiz.transform.Find("PROP_Aro");
        if (aro != null && aro.GetComponentInChildren<MeshFilter>(true) == null)
        {
            UnityEngine.Object.DestroyImmediate(aro.gameObject);
            aro = null;
        }
        if (aro == null)
        {
            // El aro visible procede del catálogo de props; el collar del jefe es un efecto de partículas.
            aro = ColocarPrefab("PROP_Aro", "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Ring01_a01.prefab",
                new Vector3(116f, 0.72f, -0.5f), 0.35f);
            props.RemoveAt(props.Count - 1);
        }
        foreach (var r in aro.GetComponentsInChildren<Renderer>(true)) r.gameObject.SetActive(true);
        aro.position = new Vector3(116f, 0.72f, -0.5f);
        aro.gameObject.SetActive(false); Prop("PROP_Aro", aro);
        var st = new SerializedObject(stage);
        var lista = st.FindProperty("_props"); lista.ClearArray();
        for (int i = 0; i < props.Count; i++)
        {
            lista.InsertArrayElementAtIndex(i); var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = props[i].id;
            e.FindPropertyRelative("target").objectReferenceValue = props[i].target;
            e.FindPropertyRelative("eyeHeight").floatValue = 0;
            e.FindPropertyRelative("esObstaculo").boolValue = false;
        }
        st.ApplyModifiedPropertiesWithoutUndo();
        Fuego("VFX_VelaPequena", 0.10f);
        Fuego("VFX_VelaGrande", 0.25f);
        Fuego("VFX_Manga", 0.14f);
        var acciones = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/Core/PlayerControls.inputactions");
        string rutaInput = "Assets/_SEQUENCES/Guiones/EntradaMantenerSoltar.asset";
        if (AssetDatabase.LoadAssetAtPath<InputActionReference>(rutaInput) == null)
            AssetDatabase.CreateAsset(InputActionReference.Create(acciones.FindAction("GamePlay/Interact", true)), rutaInput);

        var tramo = new NarrativeNode[]
        {
            new SetTimeOfDayNode { targetTime = DayNightCycle.TimeOfDay.Morning, immediate = true },
            new AdditiveSceneNode { sceneName = "WillHouse", waitForCompletion = true },
            new PlaceActorNode { actorId = "Player", markId = "Bedroom", applyPlayerEnvironment = true },
            new PlaceActorNode { actorId = "NPC_Eldran", markId = "M_Manana_EldranPuerta", showActor = true },
            new PlayCinematicNode { cinematicName = "La mañana después", secuencia = def, signalIn = def.signalIn, signalDone = def.signalOut },
            new PlaceActorNode { actorId = "NPC_Eldran", markId = "M_Manana_EldranMesa", showActor = true },
            new GiveInventoryItemNode { item = item, amount = 1, onlyOnce = true },
            new StartQuestNode { questId = quest.questId, giverId = "NPC_Eldran", inProgressDialogue = dialogo },
            new CheckpointNode { checkpointId = "CAP2_MANANA_DESPUES", spawnAnchorId = "Bedroom", sceneName = "WillHouse" }
        };
        string[] titulos = { "7.- Amanece tras la noche", "8.- Carga la habitación de Will", "9.- Will vuelve a WillHouse · Bedroom",
            "10.- Eldran entra en la habitación", "11.- La mañana después (SEQ_MananaDespues)", "12.- Eldran queda junto a la mesa", "13.- Trozo del aro", "14.- Prepárate para el viaje", "15.- Cap. 2 · La mañana después" };
        for (int i = 0; i < tramo.Length; i++)
        {
            string id = i == 0 ? antiguo.guid : "cap2-manana-despues-" + (i + 7);
            var existente = grafo.FindNode(id);
            var n = tramo[i]; n.guid = id; n.chapter = "Cap. 2"; n.displayTitle = titulos[i];
            n.position = new Vector2(2540 + 380 * i, 0); n.blockSaving = i < 8;
            n.outputs = i == 8 ? (existente?.outputs ?? new List<string>()) : new List<string> { "cap2-manana-despues-" + (i + 8) };
            if (existente != null) grafo.nodes[grafo.nodes.IndexOf(existente)] = n;
            else grafo.nodes.Add(n);
        }
        EditorUtility.SetDirty(grafo);
        EditorSceneManager.MarkSceneDirty(casa);
        EditorSceneManager.SaveScene(casa);
        var start = EditorSceneManager.GetSceneByPath(Inicio);
        bool cerrar = !start.isLoaded;
        if (cerrar) start = EditorSceneManager.OpenScene(Inicio, OpenSceneMode.Additive);
        var manager = start.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<QuestManager>(true)).Single();
        var sm = new SerializedObject(manager); var catalogo = sm.FindProperty("questCatalog");
        bool contiene = false;
        for (int i = 0; i < catalogo.arraySize; i++) if (catalogo.GetArrayElementAtIndex(i).objectReferenceValue == quest) contiene = true;
        if (!contiene) { int i = catalogo.arraySize; catalogo.InsertArrayElementAtIndex(i); catalogo.GetArrayElementAtIndex(i).objectReferenceValue = quest; }
        sm.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(start); EditorSceneManager.SaveScene(start);
        if (cerrar) EditorSceneManager.CloseScene(start, true);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Guiones/WillHouse/Cap2_02_MananaDespues");
        File.WriteAllText("Guiones/WillHouse/Cap2_02_MananaDespues/reparto.md", string.Join("\n", new[] { "Assets/Prefabs/_WILL.prefab", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab" }
            .SelectMany(r => new[] { "# " + r }.Concat(CapturaDeEscenario.Catalogo(AssetDatabase.LoadAssetAtPath<GameObject>(r)).Select(e => e.estado)))));
        Debug.Log("[Cap2MananaDespues] Montaje guardado. Icono provisional: llave metálica IT_Key. Hornear Cap2_02_MananaDespues con storyboard.");
    }

    static T Asset<T>(string ruta) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, ruta); }
        return a;
    }

    static void Fuego(string nombre, float escala)
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/_VFX", "MananaDespues");
        string ruta = Carpeta + "/" + nombre + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) != null) return;
        var baseFuego = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Univeral FX Shader/Prefab/FireSmall.prefab");
        var go = UnityEngine.Object.Instantiate(baseFuego);
        go.name = nombre; go.transform.localScale = Vector3.one * escala;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        PrefabUtility.SaveAsPrefabAsset(go, ruta);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
#endif
