#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.NPC.Common;
using Game.NPC.Modules;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Core.InputGlyphs;

/// Monta los encargos del viaje con IDs estables y conserva las ramas añadidas por otras tareas.
public static class Cap2PrepararViaje
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string Mundo = "Assets/Scenes/Worlds/MainWorld.unity";
    const string Roster = "Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset";
    const string Carpeta = "Guiones/MainWorld_PreparaViaje";
    const string Dialogos = "Assets/_DIALOGUES/DIALOGUE NPCS";
    const string Prefab = "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab";
    const string Fork = "cap2-prepara-viaje-fork";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Inspeccionar pueblo para preparar viaje")]
    public static void Inspeccionar()
    {
        var escena = Abrir(Mundo);
        Directory.CreateDirectory(Carpeta);
        var ts = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        File.WriteAllLines(Carpeta + "/puntos.md", ts.Where(t => t.GetComponent<NpcSpawnPoint>() != null || t.name == "Ambient_WillTown")
            .Select(t => t.name + " " + t.position.ToString("F2") + " " + t.eulerAngles.ToString("F1")));
        var victoria = ts.First(t => t.GetComponent<NpcSpawnPoint>()?.spawnId == "SPAWN_Victoria");
        var p = victoria.position;
        CapturaDeEscenario.Capturar("MainWorld", Rect.MinMaxRect(p.x - 35, p.z - 35, p.x + 35, p.z + 35), "PreparaViaje");
    }

    [MenuItem("El Sendero/Archivo/Capítulo 2/Montar preparación del viaje")]
    public static void Asegurar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("El montaje necesita el Editor fuera de Play.");
        var escena = Abrir(Mundo);
        var grafo = Cargar<NarrativeGraph>(Grafo);
        var roster = Cargar<NpcRosterSO>(Roster);
        var capa = Cargar<WardrobeItemSO>("Assets/_WARDROBE ITEMS/WardrobeItem_Cloak02.asset");
        var sc = new SerializedObject(capa);
        sc.FindProperty("bonos").FindPropertyRelative("defensa").floatValue = 3;
        sc.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(capa);
        var icono = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("07a0b7801b6ad424eae696bee0e938ff"));
        if (icono == null) throw new InvalidOperationException("Falta el icono oficial de misión.");

        var victoria = Dialogo("Victoria", "DLG_VICTORIA_CAPA", "DLG_VICTORIA_CAPA_", 8, new[] { 1 },
            new[] { NPCEmotion.Smirk, NPCEmotion.Neutral, NPCEmotion.Happy, NPCEmotion.Thinking, NPCEmotion.Smirk, NPCEmotion.Happy, NPCEmotion.Happy, NPCEmotion.Determined });
        var boticaria = Dialogo("Boticaria", "DLG_BOTICARIA_POCION", "DLG_BOTICARIA_POCION_", 7, new[] { 4, 6 },
            new[] { NPCEmotion.Neutral, NPCEmotion.Worried, NPCEmotion.Determined, NPCEmotion.Thinking, NPCEmotion.Thinking, NPCEmotion.Determined, NPCEmotion.Determined });
        var victoriaDespues = Dialogo("Victoria", "DLG_VICTORIA_CAPA_DESPUES", "DLG_VICTORIA_CAPA_DESPUES_", 1, Array.Empty<int>(), new[] { NPCEmotion.Happy });
        var boticariaDespues = Dialogo("Boticaria", "DLG_BOTICARIA_POCION_DESPUES", "DLG_BOTICARIA_POCION_DESPUES_", 1, Array.Empty<int>(), new[] { NPCEmotion.Worried });
        PrepararBoticaria(escena, roster, icono);
        // El icono se configura una vez en cada actor y todos los nodos lo reutilizan.
        EditarActor(roster.entries.Single(e => e.persistenceId == "NPC_Victoria").prefab, icono);

        var inicio = grafo.nodes.OfType<WaitCustomEventNode>().Single(n => n.eventKey == "CH_Cap1_BACK_1");
        const string flagId = "cap2-iniciado-flag";
        var flag = grafo.FindNode(flagId);
        var salidas = flag?.outputs ?? new List<string>(inicio.outputs);
        Poner(grafo, new SetFlagNode { flagKey = "CAP2_INICIADO", outputs = salidas }, flagId, "1b.- Comienza el Cap. 2", new Vector2(480, -200));
        inicio.outputs = new List<string> { flagId };
        var checkpoint = grafo.FindNode("cap2-manana-despues-15") ?? throw new InvalidOperationException("Falta checkpoint 15.");
        checkpoint.outputs = new List<string> { Fork };
        var reparto = grafo.FindNode(Fork) as ForkNode;
        if (reparto == null)
        {
            reparto = new ForkNode();
            Poner(grafo, reparto, Fork, "16.- Prepárate para el viaje · encargos en paralelo", new Vector2(6000, 0));
        }
        // Las futuras ramas de Erika y Eldran se conectan aquí sin reconstruir las existentes.
        Rama(grafo, reparto, "victoria", 17, -300, new NarrativeNode[] {
            new WaitNpcInteractionNode { npcId = "NPC_Victoria" },
            new PlayDialogueNode { npcId = "NPC_Victoria", dialogue = victoria },
            new UnlockWardrobeItemNode { wardrobeItem = capa, logWarnings = false },
            new TutorialPromptNode { textId = "TUTORIAL_PREPARA_VIAJE_CAPA", text = "Equípate la capa desde el menú: Equipo ▸ Capas.", buttonName = InputGlyphNames.Start, dismissWithCancel = true, cerrarAlEmpezarDialogo = true },
            new CompleteQuestStepsNode { questId = "PREPARA_VIAJE", stepConditionIds = new List<string> { "PREPARA_VIAJE_CAPA" }, standingActorId = "NPC_Victoria", standingDialogue = victoriaDespues }
        }, new[] { "Hablar con Victoria", "Victoria entrega la capa", "Desbloquear capa de principiante", "Tutorial · Equipo ▸ Capas", "Completar encargo · capa" });
        Rama(grafo, reparto, "boticaria", 22, 300, new NarrativeNode[] {
            new WaitNpcInteractionNode { npcId = "NPC_Boticaria" },
            new PlayDialogueNode { npcId = "NPC_Boticaria", dialogue = boticaria },
            new GiveInventoryItemNode { item = Cargar<ItemData>("Assets/_ITEMS/IT_PocionVida.asset"), amount = 1, onlyOnce = true },
            new TutorialPromptNode { textId = "TUTORIAL_PREPARA_VIAJE_POCION", text = "En Inventario, selecciona la poción y confirma {BOTON}; confirma otra vez en Usar.", buttonName = InputGlyphNames.Confirm, dismissWithCancel = true, cerrarAlEmpezarDialogo = true },
            new CompleteQuestStepsNode { questId = "PREPARA_VIAJE", stepConditionIds = new List<string> { "PREPARA_VIAJE_POCION" }, standingActorId = "NPC_Boticaria", standingDialogue = boticariaDespues }
        }, new[] { "Hablar con la boticaria", "La única poción de vida", "Recibir poción de vida", "Tutorial · usar consumibles", "Completar encargo · poción" });
        EditorUtility.SetDirty(grafo); EditorUtility.SetDirty(roster);
        EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        Verificar();
    }

    static void Rama(NarrativeGraph grafo, ForkNode reparto, string clave, int numero, float y, NarrativeNode[] nodos, string[] titulos)
    {
        string Id(int i) => "cap2-prepara-viaje-" + clave + "-" + i;
        if (!reparto.outputs.Contains(Id(0))) reparto.outputs.Add(Id(0));
        for (int i = 0; i < nodos.Length; i++)
        {
            // La salida final permanece libre para la tarea que monta la despedida.
            nodos[i].outputs = i + 1 < nodos.Length ? new List<string> { Id(i + 1) } : grafo.FindNode(Id(i))?.outputs ?? new List<string>();
            nodos[i].blockSaving = i > 0 && i < nodos.Length - 1;
            Poner(grafo, nodos[i], Id(i), (numero + i) + ".- " + titulos[i], new Vector2(6380 + i * 380, y));
        }
    }

    static void Poner(NarrativeGraph g, NarrativeNode n, string id, string titulo, Vector2 posicion)
    {
        n.guid = id; n.displayTitle = titulo; n.chapter = "Cap. 2"; n.position = posicion;
        var anterior = g.FindNode(id);
        if (anterior == null) g.nodes.Add(n); else g.nodes[g.nodes.IndexOf(anterior)] = n;
    }

    static DialogueAsset Dialogo(string actor, string nombre, string prefijo, int cantidad, int[] jugador, NPCEmotion[] emociones)
    {
        string carpeta = Dialogos + "/" + actor;
        AsegurarCarpeta(carpeta);
        var d = Asset<DialogueAsset>(carpeta + "/" + nombre + ".asset");
        d.lines = Enumerable.Range(0, cantidad).Select(i => new DialogueLine {
            speakerNameId = jugador.Contains(i) ? "CHAR_WILL" : actor == "Victoria" ? "CHAR_VICTORIA" : "CHAR_BOTICARIA",
            textId = prefijo + (i + 1).ToString("00"), isPlayerSpeaking = jugador.Contains(i), emotion = emociones[i]
        }).ToArray();
        EditorUtility.SetDirty(d); return d;
    }

    static void PrepararBoticaria(Scene escena, NpcRosterSO roster, GameObject icono)
    {
        // El lugar elegido se guarda como datos de montaje, tras revisar la captura del pueblo.
        string sitio = Carpeta + "/posicion.json";
        if (!File.Exists(sitio)) throw new InvalidOperationException("Falta revisar la captura y guardar posicion.json.");
        var posicion = JsonUtility.FromJson<Sitio>(File.ReadAllText(sitio));
        var punto = new Vector3(posicion.x, posicion.y, posicion.z);
        if (!NavMesh.SamplePosition(punto, out var hit, 1f, NavMesh.AllAreas)) throw new InvalidOperationException("La boticaria debe quedar sobre NavMesh.");
        var ambiente = Asset<NPCAmbientConfig>("Assets/_NPCs/Ambient/NPC_Ambient_Boticaria.asset");
        ambiente.enableWander = false; ambiente.canSeekShelter = false; EditorUtility.SetDirty(ambiente);
        var fuente = Cargar<GameObject>("Assets/_NPCs/Patricia.prefab");
        // Se conserva el modelo y el rig del prefab existente con identidad y comportamiento propios.
        var go = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null ? Prefab : AssetDatabase.GetAssetPath(fuente));
        try
        {
            go.name = "Boticaria";
            var manager = go.GetComponent<Game.NPC.NPCBehaviourManagerV2>() ?? go.AddComponent<Game.NPC.NPCBehaviourManagerV2>();
            var so = new SerializedObject(manager);
            so.FindProperty("persistenceId").stringValue = "NPC_Boticaria";
            so.FindProperty("dialogueCharacterId").stringValue = "CHAR_BOTICARIA";
            so.ApplyModifiedPropertiesWithoutUndo();
            manager.Configuration.behaviourType = NPCBehaviourType.Ambient;
            manager.Configuration.ambientConfig = ambiente;
            manager.Configuration.questConfig = null;
            manager.Configuration.interactiveNarrativeConfig = null;
            manager.Configuration.socialConfig = null;
            foreach (var c in go.GetComponents<Component>())
                if (c != null && (c.GetType().Name == "NPCInteractiveNarrativeExecutor" || c.GetType().Name == "NPCQuestActionExecutor" || c.GetType().Name == "NPCQuestIconManager" || c.GetType().Name == "NPCPersistentIconController" || c is ShopVendor)) UnityEngine.Object.DestroyImmediate(c);
            var npcDialogue = go.GetComponent<Interactable>();
            if (npcDialogue != null)
            {
                var sd = new SerializedObject(npcDialogue);
                var prop = sd.FindProperty("dialogue");
                if (prop != null) prop.objectReferenceValue = null;
                sd.FindProperty("dialogueCharacterId").stringValue = "CHAR_BOTICARIA";
                sd.FindProperty("mode").enumValueIndex = (int)InteractableMode.HandOffToTarget;
                sd.ApplyModifiedPropertiesWithoutUndo();
            }
            ConfigurarIcono(go, icono);
            PrefabUtility.SaveAsPrefabAsset(go, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
        var ts = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var marca = ts.FirstOrDefault(t => t.GetComponent<NpcSpawnPoint>()?.spawnId == "SPAWN_Boticaria");
        if (marca == null)
        {
            var nuevo = new GameObject("SPAWN_Boticaria"); SceneManager.MoveGameObjectToScene(nuevo, escena);
            marca = nuevo.transform; nuevo.AddComponent<NpcSpawnPoint>().spawnId = "SPAWN_Boticaria";
        }
        marca.SetPositionAndRotation(hit.position, Quaternion.Euler(0, posicion.rumbo, 0));
        var entry = roster.entries.FirstOrDefault(e => e.spawnId == "SPAWN_Boticaria");
        if (entry == null) { entry = new NpcRosterSO.Entry(); roster.entries.Add(entry); }
        entry.spawnId = "SPAWN_Boticaria"; entry.prefab = Cargar<GameObject>(Prefab);
        entry.gameObjectName = "Boticaria"; entry.persistenceId = "NPC_Boticaria";
        entry.requiredFlag = NarrativeFlags.Key("CAP2_INICIADO"); entry.invertCondition = false;
        entry.enabled = entry.startActive = entry.requireNavMesh = true; entry.vozDeReacciones = "Vecina";
    }

    static void EditarActor(GameObject prefab, GameObject icono)
    {
        string ruta = AssetDatabase.GetAssetPath(prefab);
        var go = PrefabUtility.LoadPrefabContents(ruta);
        try { ConfigurarIcono(go, icono); PrefabUtility.SaveAsPrefabAsset(go, ruta); }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }

    static void ConfigurarIcono(GameObject go, GameObject icono)
    {
        var actor = go.GetComponent<NarrativeActor>() ?? go.AddComponent<NarrativeActor>();
        var so = new SerializedObject(actor);
        so.FindProperty("defaultQuestIconPrefab").objectReferenceValue = icono;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("El Sendero/Archivo/Capítulo 2/Verificar preparación del viaje")]
    public static void Verificar()
    {
        Directory.CreateDirectory(Carpeta);
        var g = Cargar<NarrativeGraph>(Grafo);
        var nodos = g.nodes.Where(n => n.guid.StartsWith("cap2-prepara-viaje-")).ToArray();
        if (nodos.Length != 11 || nodos.Select(n => n.guid).Distinct().Count() != 11) throw new InvalidOperationException("Número de nodos de preparación incorrecto.");
        foreach (var n in nodos)
            foreach (var salida in n.outputs)
                if (g.FindNode(salida) == null) throw new InvalidOperationException("Arista sin destino: " + salida);
        File.WriteAllLines(Carpeta + "/grafo-guardado.md", nodos.Select(n => n.displayTitle + " · " + n.GetType().Name + " · " + n.guid + " → " + string.Join(", ", n.outputs)));
        VerificarTextos(nodos.OfType<PlayDialogueNode>().Select(n => n.dialogue));
        Debug.Log("[Cap2PrepararViaje] Datos guardados y enlaces verificados.");
    }

    static void VerificarTextos(IEnumerable<DialogueAsset> dialogos)
    {
        var start = Abrir("Assets/Scenes/Systems/Start.unity");
        var manager = start.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueManager>(true)).Single();
        var body = (TextMeshProUGUI)new SerializedObject(manager).FindProperty("bodyText").objectReferenceValue;
        // La copia mide con la fuente y el ancho reales sin modificar el Canvas del juego.
        var copia = UnityEngine.Object.Instantiate(body.gameObject).GetComponent<TextMeshProUGUI>();
        try
        {
            copia.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, body.rectTransform.rect.width);
            copia.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, body.rectTransform.rect.height);
            var informe = new List<string>();
            foreach (string idioma in new[] { "es", "en" })
            {
                var tabla = JsonUtility.FromJson<Tabla>(File.ReadAllText("Assets/Resources/Localization/dialogues_" + idioma + ".json"));
                var textos = tabla.texts.ToDictionary(t => t.key, t => t.value);
                foreach (var dialogo in dialogos)
                    foreach (var linea in dialogo.lines)
                    {
                        if (!textos.TryGetValue(linea.textId, out var texto)) throw new InvalidOperationException("Falta " + linea.textId);
                        // Sin Canvas no hay malla: se mide la altura preferida frente a la de una línea.
                        float ancho = body.rectTransform.rect.width;
                        int lineas = Mathf.RoundToInt(copia.GetPreferredValues(texto, ancho, 0).y / copia.GetPreferredValues("Ág", ancho, 0).y);
                        if (lineas == 0) throw new InvalidOperationException("No se pudo medir " + linea.textId);
                        informe.Add(idioma + " · " + linea.textId + " · " + lineas + " líneas");
                        if (lineas > 3) throw new InvalidOperationException(linea.textId + " ocupa " + lineas + " líneas en " + idioma);
                    }
            }
            File.WriteAllLines(Carpeta + "/paginas-dialogo.md", informe);
        }
        finally { UnityEngine.Object.DestroyImmediate(copia.gameObject); }
    }

    [Serializable] class Tabla { public Texto[] texts; }
    [Serializable] class Texto { public string key, value; }

    [Serializable] class Sitio { public float x, y, z, rumbo; }
    static Scene Abrir(string ruta)
    {
        var escena = EditorSceneManager.GetSceneByPath(ruta);
        return escena.isLoaded ? escena : EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
    }
    static T Cargar<T>(string ruta) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("Falta " + ruta);
    static T Asset<T>(string ruta) where T : ScriptableObject
    {
        AsegurarCarpeta(Path.GetDirectoryName(ruta).Replace('\\', '/'));
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, ruta); }
        return a;
    }
    static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre); AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
#endif
