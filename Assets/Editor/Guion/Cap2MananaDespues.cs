#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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

    [MenuItem("El Sendero/Capítulo 2/Montar la mañana después")]
    [MenuItem("El Sendero/Capítulo 2/Montar la expedición de hace 40 años")]
    [MenuItem("El Sendero/Cinematicas/Montar Expedicion")]
    [MenuItem("El Sendero/Cinematicas/Montar Manana")]
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
        dormitorio.transform.localPosition = new Vector3(dormitorio.transform.localPosition.x, 0f, dormitorio.transform.localPosition.z);
        EditorUtility.SetDirty(dormitorio.gameObject);

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
        MontarExpedicion(casa, raiz, stage);
        Cap2PuntosDeLaManana.Aplicar();
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
        try
        {
            File.WriteAllText("Guiones/WillHouse/Cap2_02_MananaDespues/reparto.md", string.Join("\n", new[] {
                "Assets/Prefabs/_WILL.prefab",
                "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab",
                "Assets/_NPCs/_GrafoNarrativo/Selene.prefab",
                "Assets/_NPCs/MagoPrimero.prefab"
            }.SelectMany(r => new[] { "# " + r }.Concat(CapturaDeEscenario.Catalogo(AssetDatabase.LoadAssetAtPath<GameObject>(r)).Select(e => e.estado)))));
        }
        catch { }
        Debug.Log("[Cap2MananaDespues] Montaje guardado. Escenario de expedición recreado a X=300.");
    }

    private static void AsegurarPrefabSelene()
    {
        string rutaSelene = "Assets/_NPCs/_GrafoNarrativo/Selene.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaSelene) != null) return;

        var pfErika = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/Combat/Erika.prefab");
        if (pfErika == null) return;

        var go = UnityEngine.Object.Instantiate(pfErika);
        go.name = "Selene";

        var combatHandler = go.GetComponent<Game.NPC.Modules.NPCCombatLifecycleHandler>();
        if (combatHandler != null) UnityEngine.Object.DestroyImmediate(combatHandler);

        var beh = go.GetComponent<NPCBehaviourManagerV2>();
        if (beh != null)
        {
            var so = new SerializedObject(beh);
            var pId = so.FindProperty("persistenceId");
            if (pId != null) pId.stringValue = "Selene";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var actor = go.GetComponent<NarrativeActor>();
        if (actor != null)
        {
            var so = new SerializedObject(actor);
            var pId = so.FindProperty("actorId");
            if (pId != null) pId.stringValue = "Selene";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Head") && t.name != "head")
                t.gameObject.SetActive(t.name == "Head02_Female");
            else if (t.name.StartsWith("Hat"))
                t.gameObject.SetActive(t.name == "Hat06");
            else if (t.name.StartsWith("Hair"))
                t.gameObject.SetActive(t.name == "Hair07");
            else if (t.name.StartsWith("Body"))
                t.gameObject.SetActive(t.name == "Body08");
            else if (t.name.StartsWith("Wand") || t.name.StartsWith("Shield") || t.name.StartsWith("Spear") || t.name.StartsWith("OHS") || t.name.StartsWith("THS"))
                t.gameObject.SetActive(false);
        }

        PrefabUtility.SaveAsPrefabAsset(go, rutaSelene);
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
    }

    private static void MontarExpedicion(Scene casa, GameObject raizSeq, SequenceStage stage)
    {
        var todos = casa.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var raizExp = todos.FirstOrDefault(t => t != null && t.name == "EXPEDICION_FLASHBACK")?.gameObject;
        if (raizExp == null)
        {
            raizExp = new GameObject("EXPEDICION_FLASHBACK");
            SceneManager.MoveGameObjectToScene(raizExp, casa);
        }
        raizExp.transform.position = Vector3.zero;

        Transform Subnodo(string nombre)
        {
            var t = raizExp.transform.Find(nombre);
            if (t == null)
            {
                var go = new GameObject(nombre);
                go.transform.SetParent(raizExp.transform, false);
                t = go.transform;
            }
            return t;
        }

        AsegurarPrefabSelene();

        var arq = Subnodo("Arquitectura");
        for (int i = arq.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(arq.GetChild(i).gameObject);

        var luz = Subnodo("Iluminacion");
        var actores = Subnodo("Actores");
        for (int i = actores.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(actores.GetChild(i).gameObject);

        // 1. Suelo de losas de piedra planas (Modular Castle floor2).
        // La malla floor2 tiene min.y=0 y max.y=2 (bloque de 2 m de grosor); colocarlo a Y=-2
        // hace que la superficie transitable quede exactamente a Y=0 nivelada con la escena.
        string rutaFloor = "Assets/Art/World/Modular Castle/Assets/prefabs/floor2.prefab";
        var pfFloor = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFloor);
        if (pfFloor != null)
        {
            float[] xs = { 296f, 300f, 304f };
            float[] zs = { 0f, 4f, 8f, 12f };
            for (int i = 0; i < xs.Length; i++)
            {
                for (int j = 0; j < zs.Length; j++)
                {
                    string id = $"Floor_{i}_{j}";
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pfFloor, casa);
                    go.name = id;
                    var t = go.transform;
                    t.SetParent(arq, false);
                    t.position = new Vector3(xs[i], -2.0f, zs[j]);
                    t.rotation = Quaternion.identity;
                }
            }
        }

        // 2. Columnas en ruinas flanqueando el camino (pillar1 y pillar2)
        string rutaPillar1 = "Assets/Art/World/Modular Castle/Assets/prefabs/pillar1.prefab";
        string rutaPillar2 = "Assets/Art/World/Modular Castle/Assets/prefabs/pillar2.prefab";
        var pfPillar1 = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPillar1);
        var pfPillar2 = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPillar2);
        if (pfPillar1 != null && pfPillar2 != null)
        {
            Vector3[] posPillars = {
                new Vector3(295.0f, 0f, 2f), new Vector3(305.0f, 0f, 2f),
                new Vector3(295.0f, 0f, 6f), new Vector3(305.0f, 0f, 6f),
                new Vector3(295.0f, 0f, 10f), new Vector3(305.0f, 0f, 10f)
            };
            for (int k = 0; k < posPillars.Length; k++)
            {
                string id = $"Pillar_{k}";
                var pf = (k % 2 == 0) ? pfPillar1 : pfPillar2;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, casa);
                go.name = id;
                var t = go.transform;
                t.SetParent(arq, false);
                t.position = posPillars[k];
                t.rotation = Quaternion.Euler(0f, (k % 2 == 0) ? 90f : -90f, 0f);
            }
        }

        // 3. Antorchas con luz cálida en las primeras columnas
        string rutaTorch = "Assets/Art/World/Modular Castle/Assets/prefabs/torch.prefab";
        var pfTorch = AssetDatabase.LoadAssetAtPath<GameObject>(rutaTorch);
        if (pfTorch != null)
        {
            Vector3[] posTorches = { new Vector3(295.95f, 1.8f, 2f), new Vector3(304.05f, 1.8f, 2f) };
            float[] yRots = { 90f, -90f };
            for (int k = 0; k < posTorches.Length; k++)
            {
                string id = $"Torch_{k}";
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pfTorch, casa);
                go.name = id;
                var t = go.transform;
                t.SetParent(arq, false);
                var l = go.GetComponentInChildren<Light>() ?? go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.65f, 0.3f);
                l.range = 6f;
                l.intensity = 1.8f;
                t.position = posTorches[k];
                t.rotation = Quaternion.Euler(0f, yRots[k], 0f);
            }
        }

        // 4. Umbral Arruinado (doorway1) hacia lo desconocido
        string rutaDoor = "Assets/Art/World/Modular Castle/Assets/prefabs/doorway1.prefab";
        var pfDoor = AssetDatabase.LoadAssetAtPath<GameObject>(rutaDoor);
        if (pfDoor != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pfDoor, casa);
            go.name = "Umbral_Ruinas";
            var t = go.transform;
            t.SetParent(arq, false);
            t.position = new Vector3(300f, 0f, 11f);
            t.rotation = Quaternion.identity;
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var puerta = t.Find("door");
            if (puerta != null)
            {
                UnityEngine.Object.DestroyImmediate(puerta.gameObject);
            }
        }

        // 5. Pared de fondo que bloquea el vacío tras el umbral
        string rutaWall = "Assets/Art/World/Modular Castle/Assets/prefabs/wall1.prefab";
        var pfWall = AssetDatabase.LoadAssetAtPath<GameObject>(rutaWall);
        if (pfWall != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pfWall, casa);
            go.name = "Fondo_Oscuro";
            var t = go.transform;
            t.SetParent(arq, false);
            t.position = new Vector3(300f, 0f, 14.5f);
            t.rotation = Quaternion.identity;
        }

        // 6. Iluminación atmosférica (luz fría a contraluz por el umbral y ambiente nocturno)
        var tLuzPortal = luz.Find("Luz_UmbralMistico");
        if (tLuzPortal == null)
        {
            var goLuz = new GameObject("Luz_UmbralMistico");
            goLuz.transform.SetParent(luz, false);
            var l = goLuz.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.45f, 0.70f, 1.0f);
            l.range = 22f;
            l.spotAngle = 65f;
            l.intensity = 3.5f;
            tLuzPortal = goLuz.transform;
        }
        tLuzPortal.position = new Vector3(300f, 4.5f, 13f);
        tLuzPortal.rotation = Quaternion.Euler(22f, 180f, 0f);

        var tLuzAmb = luz.Find("Luz_AmbienteFrio");
        if (tLuzAmb == null)
        {
            var goAmb = new GameObject("Luz_AmbienteFrio");
            goAmb.transform.SetParent(luz, false);
            var l = goAmb.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.25f, 0.40f, 0.70f);
            l.range = 16f;
            l.intensity = 1.2f;
            tLuzAmb = goAmb.transform;
        }
        tLuzAmb.position = new Vector3(300f, 3f, 5f);

        // 7. Niebla nocturna misteriosa en el suelo
        var tNiebla = luz.Find("Niebla_Ruinas");
        if (tNiebla == null)
        {
            var goNiebla = new GameObject("Niebla_Ruinas");
            goNiebla.transform.SetParent(luz, false);
            var n = goNiebla.AddComponent<NieblaNocturna>();
            n.tamano = new Vector3(14f, 1.5f, 16f);
            n.cantidad = 30;
            n.desde = 0f;
            n.color = new Color(0.5f, 0.65f, 0.85f);
            n.opacidad = 0.38f;
            n.tamanoDeNube = new Vector2(3.5f, 6.5f);
            tNiebla = goNiebla.transform;
        }
        tNiebla.position = new Vector3(300f, 0f, 6f);

        // 8. Actores: Selene, Eldran Joven, Silas Joven
        GameObject ColocarActor(string id, string prefabRuta, Vector3 pos, float rumbo)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRuta);
            if (pf == null) throw new InvalidOperationException("Falta " + prefabRuta);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, casa);
            go.name = id;
            var t = go.transform;
            t.SetParent(actores, false);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            var mgr = go.GetComponent<NPCBehaviourManagerV2>();
            if (mgr != null)
            {
                var soMgr = new SerializedObject(mgr);
                var pId = soMgr.FindProperty("persistenceId");
                if (pId != null) { pId.stringValue = id; soMgr.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var anchor = go.GetComponent<SpawnAnchor>() ?? go.AddComponent<SpawnAnchor>();
            anchor.anchorId = id;
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, rumbo, 0f);
            return t.gameObject;
        }

        foreach (var col in arq.GetComponentsInChildren<Collider>(true))
        {
            bool esSuelo = col.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0
                        || (col.transform.parent != null && col.transform.parent.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
                        || col.gameObject.layer == 10;
            col.enabled = esSuelo;
            EditorUtility.SetDirty(col);
        }

        var seleneGo = ColocarActor("Actor_Selene", "Assets/_NPCs/_GrafoNarrativo/Selene.prefab", new Vector3(300.0f, 0f, 3.5f), 0f);
        var eldranGo = ColocarActor("Actor_EldranJoven", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab", new Vector3(298.5f, 0f, 1.5f), 15f);
        var silasGo = ColocarActor("Actor_SilasJoven", "Assets/_NPCs/MagoPrimero.prefab", new Vector3(301.5f, 0f, 1.2f), -15f);

        var bigote = eldranGo.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.Contains("Mustache"));
        if (bigote != null) bigote.gameObject.SetActive(false);

        // 9. Registrar actores en SequenceStage
        var soStage = new SerializedObject(stage);
        var propActors = soStage.FindProperty("_actors");
        var bindingsExistentes = new List<(string id, Transform target, float eyeHeight, Animator anim, NPCEmotionController emotion)>();
        for (int i = 0; i < propActors.arraySize; i++)
        {
            var elem = propActors.GetArrayElementAtIndex(i);
            string id = elem.FindPropertyRelative("id").stringValue;
            if (id == "Actor_Selene" || id == "selene" ||
                id == "Actor_EldranJoven" || id == "eldran_joven" ||
                id == "Actor_SilasJoven" || id == "silas_joven") continue;
            bindingsExistentes.Add((
                id,
                elem.FindPropertyRelative("target").objectReferenceValue as Transform,
                elem.FindPropertyRelative("eyeHeight").floatValue,
                elem.FindPropertyRelative("animator").objectReferenceValue as Animator,
                elem.FindPropertyRelative("emotionController").objectReferenceValue as NPCEmotionController
            ));
        }

        propActors.ClearArray();
        void BindActor(string id, Transform target)
        {
            if (target == null) return;
            int idx = propActors.arraySize;
            propActors.InsertArrayElementAtIndex(idx);
            var elem = propActors.GetArrayElementAtIndex(idx);
            elem.FindPropertyRelative("id").stringValue = id;
            elem.FindPropertyRelative("target").objectReferenceValue = target;
            elem.FindPropertyRelative("eyeHeight").floatValue = 0f;
            Animator animActivo = null;
            var anims = target.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] != null && anims[i].runtimeAnimatorController != null)
                {
                    animActivo = anims[i];
                    break;
                }
            }
            elem.FindPropertyRelative("animator").objectReferenceValue = animActivo;
            elem.FindPropertyRelative("emotionController").objectReferenceValue = target.GetComponentInChildren<NPCEmotionController>(true);
        }

        foreach (var b in bindingsExistentes)
        {
            int idx = propActors.arraySize;
            propActors.InsertArrayElementAtIndex(idx);
            var elem = propActors.GetArrayElementAtIndex(idx);
            elem.FindPropertyRelative("id").stringValue = b.id;
            elem.FindPropertyRelative("target").objectReferenceValue = b.target;
            elem.FindPropertyRelative("eyeHeight").floatValue = b.eyeHeight;
            elem.FindPropertyRelative("animator").objectReferenceValue = b.anim;
            elem.FindPropertyRelative("emotionController").objectReferenceValue = b.emotion;
        }

        BindActor("Actor_Selene", seleneGo.transform);
        BindActor("selene", seleneGo.transform);
        BindActor("Actor_EldranJoven", eldranGo.transform);
        BindActor("eldran_joven", eldranGo.transform);
        BindActor("Actor_SilasJoven", silasGo.transform);
        BindActor("silas_joven", silasGo.transform);

        soStage.ApplyModifiedPropertiesWithoutUndo();
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
