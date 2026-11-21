using System.Collections.Generic;
using System.IO;
using Liyodev.NarrativeGraph.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Samples.ElFaro
{
    /// Monta el ejemplo «El faro» en «Assets/El faro (ejemplo)»: capítulo, diálogos, objeto, datos
    /// y ficha del jugador, y la escena (con el pipeline de render de tu proyecto). Se puede volver
    /// a ejecutar: rehace todo. «Quitar» borra esa carpeta.
    public static class ElFaroBuilder
    {
        public const string Folder = "Assets/El faro (ejemplo)";

        [MenuItem("Liyodev/Grafo narrativo/Ejemplos/Montar «El faro»")]
        public static void Build()
        {
            var folder = Folder;
            Directory.CreateDirectory(Path.Combine(folder, "Datos"));
            AssetDatabase.Refresh();

            var dlgLlamada = Dialogue(folder, "Ana te llama",
                ("Ana", "¡Por fin llegas! El farero ha perdido la llave del faro y esta noche hay tormenta."),
                ("Ana", "Creo que se le cayó en el muelle, entre las cajas."));
            var dlgNo = Dialogue(folder, "Ana espera", ("Ana", "Vale… Estaré aquí, mirando el mar."));
            var dlgMientras = Dialogue(folder, "Ana mientras tanto", ("Ana", "¿La has encontrado? Busca entre las cajas del muelle."));
            var dlgAun = Dialogue(folder, "Ana aún no", ("Ana", "Todavía no la tienes, ¿verdad?"));
            var dlgFin = Dialogue(folder, "Ana da las gracias",
                ("Ana", "¡La llave! ¡La has encontrado!"),
                ("Ana", "Corre, abre el faro antes de que llegue la tormenta."));

            var llave = Asset<NarrativeBasicItem>(folder, "Datos/Llave del faro");
            llave.id = "LLAVE_DEL_FARO";
            llave.displayName = "Llave del faro";
            EditorUtility.SetDirty(llave);

            var jugador = Asset<FaroJugadorDatos>(folder, "Datos/Jugador (partida nueva)");
            jugador.apareceEn = "Plaza";
            jugador.vida = 3;
            jugador.llevaFarol = false;
            EditorUtility.SetDirty(jugador);

            var ficha = Asset<NarrativePlayerSheet>(folder, "Datos/Ficha del jugador");
            ficha.template = jugador;
            ficha.fields = new List<string> { nameof(FaroJugadorDatos.apareceEn), nameof(FaroJugadorDatos.vida), nameof(FaroJugadorDatos.llevaFarol) };
            ficha.positionField = nameof(FaroJugadorDatos.apareceEn);
            ficha.isExample = true;
            EditorUtility.SetDirty(ficha);

            var graph = BuildGraph(folder, dlgLlamada, dlgNo, dlgMientras, dlgAun, dlgFin, llave);
            AssetDatabase.SaveAssets();
            BuildScene(folder, graph, jugador);
            Debug.Log("[El faro] Ejemplo montado en " + folder);
        }

        [MenuItem("Liyodev/Grafo narrativo/Ejemplos/Quitar «El faro»")]
        public static void Remove()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }

        /// Monta el ejemplo y lo abre (para scripts de línea de órdenes: -executeMethod).
        public static void BuildAndOpen()
        {
            Build();
            EditorSceneManager.OpenScene(Folder + "/El faro.unity", OpenSceneMode.Single);
        }

        static T Asset<T>(string folder, string name) where T : ScriptableObject
        {
            var path = $"{folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static NarrativeBasicDialogue Dialogue(string folder, string name, params (string speaker, string text)[] lines)
        {
            var d = Asset<NarrativeBasicDialogue>(folder, "Datos/" + name);
            d.lines = new List<NarrativeBasicDialogue.Line>();
            foreach (var (speaker, text) in lines) d.lines.Add(new NarrativeBasicDialogue.Line { speaker = speaker, text = text });
            EditorUtility.SetDirty(d);
            return d;
        }

        static NarrativeGraph BuildGraph(string folder, NarrativeBasicDialogue dLlamada, NarrativeBasicDialogue dNo, NarrativeBasicDialogue mientras,
                                         NarrativeBasicDialogue aun, NarrativeBasicDialogue fin, NarrativeBasicItem llave)
        {
            var path = folder + "/El faro.asset";
            var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(path);
            if (g == null) { g = ScriptableObject.CreateInstance<NarrativeGraph>(); AssetDatabase.CreateAsset(g, path); }
            g.nodes.Clear();

            const string Llegada = "La llegada", Muelle = "El muelle", Faro = "El faro";
            NarrativeNode Add(NarrativeNode n, string id, string title, string section, float x, float y)
            {
                n.Guid = "faro-" + id;
                n.DisplayTitle = title;
                n.Section = section;
                n.Position = new Vector2(x, y);
                n.Outputs = new List<string>();
                var ports = n.GetOutputPorts();
                if (ports != null) for (int i = 0; i < ports.Length; i++) n.Outputs.Add(string.Empty);
                g.nodes.Add(n);
                return n;
            }
            void Link(NarrativeNode from, NarrativeNode to, int port = -1)
            {
                if (port >= 0) from.Outputs[port] = to.Guid;
                else from.Outputs.Add(to.Guid);
            }

            var inicio = Add(new StartNode(), "inicio", "Inicio", Llegada, 0, 0);
            Add(new GraphNoteNode { Note = "Ejemplo «El faro». Elige cualquier paso, mira la pestaña Arranque y pulsa ▶: el juego empieza justo ahí. Los puntos ◆ dicen dónde aparece el jugador y si lleva el farol." },
                "nota", "Cómo usar este ejemplo", Llegada, 0, -170);
            var plaza = Add(new CheckpointNode { CheckpointId = "FARO_PLAZA" }, "plaza", "En la plaza", Llegada, 380, 0);
            var hablaAna = Add(new WaitNpcInteractionNode { NpcId = "ANA" }, "habla-ana", "Hablar con Ana", Llegada, 760, 0);
            var llamada = Add(new PlayDialogueNode { Dialogue = dLlamada, NpcId = "ANA" }, "llamada", "Ana te llama", Llegada, 1140, 0);
            var pregunta = Add(new DialogueChoiceNode { PromptText = "¿Ayudas a Ana a buscar la llave?", OptionAText = "Claro", OptionBText = "Ahora no" }, "pregunta", "¿La ayudas?", Llegada, 1520, 0);
            var dlgNo = Add(new PlayDialogueNode { Dialogue = dNo, NpcId = "ANA" }, "no", "Ana espera", Llegada, 1520, 220);

            var mision = Add(new StartQuestNode { QuestId = "LLAVE_PERDIDA", GiverId = "ANA", InProgressDialogue = mientras }, "mision", "La llave perdida", Muelle, 0, 460);
            var muelle = Add(new CheckpointNode { CheckpointId = "FARO_MUELLE" }, "muelle", "En el muelle", Muelle, 380, 460);
            var buscar = Add(new WaitNpcInteractionNode { NpcId = "MUELLE" }, "buscar", "Buscar entre las cajas", Muelle, 760, 460);
            var dar = Add(new GiveInventoryItemNode { Item = llave, Amount = 1 }, "dar", "Encuentras la llave", Muelle, 1140, 460);
            var dato = Add(new SetFlagNode { FlagKey = "TIENE_LA_LLAVE", Value = true }, "dato", "Ya tienes la llave", Muelle, 1520, 460);
            var bocadillo = Add(new ShowSpeechBubbleNode { Text = "¡Aquí está! Vuelvo con Ana.", SpeakerName = "Tú", Duration = 2.5f, WaitForCompletion = true }, "bocadillo", "¡Aquí está!", Muelle, 1900, 460);

            var volver = Add(new WaitNpcInteractionNode { NpcId = "ANA" }, "volver", "Volver con Ana", Faro, 0, 920);
            var tiene = Add(new BranchFlagNode { FlagKey = "TIENE_LA_LLAVE" }, "tiene", "¿Tienes la llave?", Faro, 380, 920);
            var aunNo = Add(new PlayDialogueNode { Dialogue = aun, NpcId = "ANA" }, "aun-no", "Aún no la tienes", Faro, 380, 1140);
            var gracias = Add(new PlayDialogueNode { Dialogue = fin, NpcId = "ANA" }, "gracias", "Ana da las gracias", Faro, 760, 920);
            var cerrar = Add(new CompleteQuestStepsNode { QuestId = "LLAVE_PERDIDA", CompleteQuest = true }, "cerrar", "Misión cumplida", Faro, 1140, 920);
            var senal = Add(new RaiseCustomEventNode { EventKey = "FARO_ABIERTO" }, "senal", "Se abre el faro", Faro, 1520, 920);
            var fundido = Add(new ScreenFadeNode { FadeColor = Color.black, Duration = 1.5f, FadeIn = true }, "fundido", "Fin: fundido a negro", Faro, 1900, 920);

            Link(inicio, plaza); Link(plaza, hablaAna); Link(hablaAna, llamada); Link(llamada, pregunta);
            Link(pregunta, mision, 0); Link(pregunta, dlgNo, 1); Link(dlgNo, hablaAna);
            Link(mision, muelle); Link(muelle, buscar); Link(buscar, dar); Link(dar, dato); Link(dato, bocadillo); Link(bocadillo, volver);
            Link(volver, tiene); Link(tiene, gracias, 0); Link(tiene, aunNo, 1); Link(aunNo, volver);
            Link(gracias, cerrar); Link(cerrar, senal); Link(senal, fundido);
            g.startNodeGuid = inicio.Guid;

            // Los dos puntos de arranque: en la plaza sin farol; en el muelle con el farol que da Ana.
            var tmp = ScriptableObject.CreateInstance<FaroJugadorDatos>();
            tmp.apareceEn = "Plaza"; tmp.llevaFarol = false;
            ((CheckpointNode)plaza).Player = new NarrativePlayerValues();
            NarrativePlayerValuesEditor.Set(((CheckpointNode)plaza).Player, tmp, new[] { nameof(FaroJugadorDatos.apareceEn), nameof(FaroJugadorDatos.llevaFarol) });
            tmp.apareceEn = "Muelle"; tmp.llevaFarol = true;
            ((CheckpointNode)muelle).Player = new NarrativePlayerValues();
            NarrativePlayerValuesEditor.Set(((CheckpointNode)muelle).Player, tmp, new[] { nameof(FaroJugadorDatos.apareceEn), nameof(FaroJugadorDatos.llevaFarol) });
            Object.DestroyImmediate(tmp);

            EditorUtility.SetDirty(g);
            return g;
        }

        static void BuildScene(string folder, NarrativeGraph graph, FaroJugadorDatos jugador)
        {
            // Se monta en una escena aparte, sin tocar las que tengas abiertas.
            // (Si la escena abierta está sin guardar, Unity no deja añadir otra: se sustituye.)
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool additive = !string.IsNullOrEmpty(previous.path);
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Camera>() != null)
                    root.transform.SetPositionAndRotation(new Vector3(0f, 14f, -16f), Quaternion.Euler(40f, 0f, 0f));

            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            suelo.transform.localScale = new Vector3(3f, 1f, 2f);

            Thing(PrimitiveType.Cylinder, "Faro", new Vector3(9f, 4f, 6f), new Vector3(2f, 4f, 2f));
            Thing(PrimitiveType.Cube, "Caja 1", new Vector3(-8f, 0.5f, 4f), Vector3.one);
            Thing(PrimitiveType.Cube, "Caja 2", new Vector3(-7f, 0.5f, 5.2f), Vector3.one);

            Point("Aparecer_Plaza", new Vector3(0f, 1f, -3f));
            Point("Aparecer_Muelle", new Vector3(-6f, 1f, 2f));

            var ana = Thing(PrimitiveType.Capsule, "Ana", new Vector3(2.5f, 1f, 0f), Vector3.one);
            Talk(ana, "ANA", "Hablar con Ana");
            var cajas = new GameObject("Muelle");
            cajas.transform.position = new Vector3(-7.5f, 0f, 4.6f);
            Talk(cajas, "MUELLE", "Buscar entre las cajas");

            var player = Thing(PrimitiveType.Capsule, "Jugador", new Vector3(0f, 1f, -3f), new Vector3(0.8f, 1f, 0.8f));
            player.tag = "Player";
            player.AddComponent<FaroJugador>().datos = jugador;

            var historia = new GameObject("Historia");
            historia.AddComponent<NarrativeBasicServices>();
            var runner = historia.AddComponent<NarrativeRunner>();
            runner.graph = graph;
            var starter = historia.AddComponent<NarrativeGraphStarter>();
            var so = new SerializedObject(starter);
            so.FindProperty("localRunner").objectReferenceValue = runner;
            var labels = so.FindProperty("graphLabels");
            labels.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, folder + "/El faro.unity");
            if (!additive) return;
            if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }

        static GameObject Thing(PrimitiveType type, string name, Vector3 at, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = at;
            go.transform.localScale = scale;
            return go;
        }

        static void Point(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
        }

        static void Talk(GameObject go, string id, string label)
        {
            var t = go.AddComponent<NarrativeBasicTalkTarget>();
            t.actorId = id;
            t.label = label;
        }
    }
}
