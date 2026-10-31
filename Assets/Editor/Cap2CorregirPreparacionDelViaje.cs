#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Corrige la preparación del viaje del Cap. 2: Eldran no da nada (su encargo de puntería se
/// retira y la misión queda en tres encargos), Erika enseña Protección y Bola Prisma, Erika dice
/// su frase del pañuelo al perder, el rótulo «A la mañana siguiente…» vuelve a la cadena, el
/// Trozo del aro y el Pañuelo tienen icono propio y el pop-up de Protección tiene imagen.
/// Además cierra bien el capítulo: fuera los nodos sueltos, la lista de motivos espera a que se
/// hable con Eldran, el capítulo acaba con las dos despedidas y emite CH_Cap2_BACK_1.
/// Ver INC-872. Se puede ejecutar varias veces: lo ya corregido se deja como está.
public static class Cap2CorregirPreparacionDelViaje
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string Mision = "Assets/_QUEST/PRINCIPALES/PRINCIPALES/Q_PREPARA_VIAJE.asset";
    const string TrozoDelAro = "Assets/_ITEMS/IT_TrozoDelAro.asset";
    const string Panuelo = "Assets/_ITEMS/IT_Panuelo.asset";
    const string IconoTrozo = "Assets/Art/UI/Items/trozo_del_aro.png";
    const string IconoPanuelo = "Assets/Art/UI/Items/panuelo_erika.png";
    const string IconoProteccion = "Assets/Art/UI/Abilities/proteccion.png";
    const string CombateErika = "Assets/_NPCs/Combat/NPC_Combat_Config_Erika.asset";
    const string DialogoVictoriaErika = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika/DLG_ERIKA_VICTORIA.asset";
    const string EscenaStart = "Assets/Scenes/Systems/Start.unity";
    const string CarpetaDialogosEldran = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/";
    static readonly string[] DialogosDePunteria = { "DLG_ELDRAN_PUNTERIA.asset", "DLG_ELDRAN_PUNTERIA_DESPUES.asset" };

    // Nodos de la rama de Eldran que se retiran.
    static readonly string[] NodosDeEldran =
    {
        "cap2-prepara-viaje-eldran-0", "cap2-prepara-viaje-eldran-1", "cap2-desbloqueo-bola-prisma",
        "cap2-prepara-viaje-eldran-2", "cap2-prepara-viaje-eldran-3",
    };

    [MenuItem("El Sendero/Archivo/Capítulo 2/Corregir preparación del viaje (INC-872)")]
    public static void Corregir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Cap. 2", "Sal del modo Play antes de ejecutarlo.", "Vale");
            return;
        }

        var informe = new List<string>();
        CorregirGrafo(informe);
        CorregirMision(informe);
        PonerIcono(TrozoDelAro, IconoTrozo, informe);
        PonerIcono(Panuelo, IconoPanuelo, informe);
        CorregirCombateDeErika(informe);
        AnadirPopupDeProteccion(informe);
        AssetDatabase.SaveAssets();
        ArchivarDialogosDePunteria(informe);

        string texto = string.Join("\n", informe);
        Debug.Log("[Cap. 2] Preparación del viaje corregida:\n" + texto);
        EditorUtility.DisplayDialog("Cap. 2 corregido", texto, "Vale");
    }

    static void CorregirGrafo(List<string> informe)
    {
        var grafo = Cargar<NarrativeGraph>(Grafo);
        var retirar = new HashSet<string>(NodosDeEldran);

        int quitados = grafo.nodes.RemoveAll(n => n != null && retirar.Contains(n.guid));
        foreach (var n in grafo.nodes)
            n?.outputs?.RemoveAll(g => retirar.Contains(g));
        informe.Add($"Grafo: {quitados} nodos de la rama de Eldran retirados.");

        var erika = Nodo<UnlockAbilitiesNode>(grafo, "cap2-prepara-viaje-erika-desbloqueo");
        if (!erika.spellsToUnlock.Contains(SpellId.Plasmaball))
            erika.spellsToUnlock.Add(SpellId.Plasmaball);
        erika.assignSpellsToEmptySlot = true;
        erika.displayTitle = "22b-1.- Aprender Protección y Bola Prisma";
        informe.Add("Grafo: Erika enseña Protección y Bola Prisma.");

        var espera = Nodo<WaitSignalCountNode>(grafo, "cap2-prepara-viaje-espera");
        espera.count = 3;
        espera.displayTitle = "25b.- Esperar los 3 encargos";

        Nodo<NarrativeNode>(grafo, "cap2-eldran-a-pueblo").displayTitle = "15b.- Eldran sale a la plaza";

        // El rótulo estaba suelto (nadie llevaba a él): va entre la entrada de Eldran y la cinemática.
        Nodo<NarrativeNode>(grafo, "cap2-rotulo-manana-siguiente");
        var entrada = Nodo<NarrativeNode>(grafo, "cap2-manana-despues-10");
        entrada.outputs = new List<string> { "cap2-rotulo-manana-siguiente" };
        informe.Add("Grafo: misión en 3 encargos y rótulo «A la mañana siguiente…» enganchado.");

        // La fuente del aviso no tiene «▸» (salía un cuadro): se dice con el botón del menú.
        var tutorialCapa = Nodo<TutorialPromptNode>(grafo, "cap2-prepara-viaje-victoria-3");
        tutorialCapa.text = "Pulsa {BOTON} y equípate la capa en Equipo, apartado Capas.";
        tutorialCapa.displayTitle = "20.- Tutorial · equipar la capa";

        RetirarNodosSueltos(grafo, informe);
        CorregirCierreDelCapitulo(grafo, informe);

        EditorUtility.SetDirty(grafo);
    }

    // Nodos a los que no llega ninguna conexión (contenido de más adelante que nunca se juega) y
    // eventos que nadie escucha (Oliver ya no está en el grupo; nadie mueve a Eldran a la capital).
    static readonly string[] NodosSinUso =
    {
        "cap2-contrabandistas-0", "cap2-contrabandistas-ataque-0", "cap2-contrabandistas-ataque-1",
        "cap2-contrabandistas-ataque-2", "cap2-contrabandistas-ataque-3", "cap2-contrabandistas-pluma-0",
        "cap2-contrabandistas-pluma-1", "cap2-contrabandistas-pluma-2", "cap2-contrabandistas-jaula-0",
        "cap2-contrabandistas-jaula-1", "cap2-contrabandistas-fin",
        "cap2-desbloqueo-cupula-estelar", "cap2-tutorial-cupula-estelar", "cap2-desbloqueo-levitacion",
        "cap2-despedida-oliver-sale", "cap2-despedida-eldran-seva",
    };

    static void RetirarNodosSueltos(NarrativeGraph grafo, List<string> informe)
    {
        var retirar = new HashSet<string>(NodosSinUso);
        int quitados = grafo.nodes.RemoveAll(n => n != null && retirar.Contains(n.guid));
        foreach (var n in grafo.nodes)
            n?.outputs?.RemoveAll(g => retirar.Contains(g));
        informe.Add($"Grafo: {quitados} nodos sueltos o sin efecto retirados (contrabandistas, Cúpula, Levitación, eventos sin oyente).");
    }

    const string SenalDespedida = "CAP2_DESPEDIDA_HECHA";

    // Antes: la lista de motivos saltaba al acabar el tercer encargo, estuviera Will donde
    // estuviera; las dos despedidas desembocaban en el mismo nodo, así que el capítulo se daba por
    // acabado con la primera; el último checkpoint ponía a Will en una marca de WillHouse dentro de
    // MainWorld; y el capítulo no avisaba de que había terminado.
    static void CorregirCierreDelCapitulo(NarrativeGraph grafo, List<string> informe)
    {
        var espera = Nodo<WaitSignalCountNode>(grafo, "cap2-prepara-viaje-espera");
        var motivos = Nodo<NarrativeNode>(grafo, "cap2-despedida-motivos");
        var completar = Nodo<CompleteQuestStepsNode>(grafo, "cap2-prepara-viaje-completar");

        // Volver con Eldran → lista de motivos → misión completada.
        var volver = Asegurar(grafo, "cap2-prepara-viaje-volver", () => new WaitNpcInteractionNode { npcId = "NPC_Eldran" },
            "25d.- Volver con Eldran", espera.position + new Vector2(260f, 0f));
        espera.outputs = new List<string> { volver.guid };
        volver.outputs = new List<string> { motivos.guid };
        motivos.outputs = new List<string> { completar.guid };
        motivos.displayTitle = "26.- Eldran: la lista de motivos";
        completar.stepConditionIds = new List<string> { "PREPARA_VIAJE_VOLVER" };
        completar.completeQuest = true;
        completar.displayTitle = "26b.- Completar misión · Prepárate para el viaje";

        // Despedidas en paralelo; el capítulo acaba cuando están las dos.
        var fork = Asegurar(grafo, "cap2-despedida-fork", () => new ForkNode(),
            "26c.- Despedidas (en el orden que quiera)", completar.position + new Vector2(260f, 0f));
        var esperaDespedidas = Asegurar(grafo, "cap2-despedida-espera",
            () => new WaitSignalCountNode { eventKey = SenalDespedida, count = 2 },
            "32b.- Esperar las 2 despedidas", fork.position + new Vector2(520f, 260f));
        completar.outputs = new List<string> { fork.guid };
        fork.outputs = new List<string> { "cap2-despedida-oliver-talk", "cap2-despedida-eldran-talk", esperaDespedidas.guid };

        var oliverHecha = Asegurar(grafo, "cap2-despedida-oliver-hecha", () => new RaiseCustomEventNode { eventKey = SenalDespedida },
            "29.- Despedida de Oliver hecha", Nodo<NarrativeNode>(grafo, "cap2-despedida-oliver-dlg").position + new Vector2(260f, 0f));
        var eldranHecha = Asegurar(grafo, "cap2-despedida-eldran-hecha", () => new RaiseCustomEventNode { eventKey = SenalDespedida },
            "32.- Despedida de Eldran hecha", Nodo<NarrativeNode>(grafo, "cap2-despedida-eldran-dlg").position + new Vector2(260f, 0f));
        Nodo<NarrativeNode>(grafo, "cap2-despedida-oliver-dlg").outputs = new List<string> { oliverHecha.guid };
        Nodo<NarrativeNode>(grafo, "cap2-despedida-eldran-dlg").outputs = new List<string> { eldranHecha.guid };
        oliverHecha.outputs = new List<string>();
        eldranHecha.outputs = new List<string>();
        esperaDespedidas.outputs = new List<string> { "cap2-despedida-join" };

        // Checkpoint final en MainWorld, en la marca del punto de guardado de la entrada.
        var final = Nodo<CheckpointNode>(grafo, "cap2-final-checkpoint");
        final.sceneName = "MainWorld";
        final.spawnAnchorId = "Eldran_PuntoGuardado";
        var fin = Asegurar(grafo, "cap2-fin-senal", () => new RaiseCustomEventNode { eventKey = "CH_Cap2_BACK_1" },
            "35.- Fin del Cap. 2 (CH_Cap2_BACK_1)", final.position + new Vector2(260f, 0f));
        final.outputs = new List<string> { fin.guid };
        fin.outputs = new List<string>();

        informe.Add("Grafo: la lista de motivos espera a que hables con Eldran; el capítulo acaba con las dos despedidas; checkpoint final en la entrada del pueblo; señal CH_Cap2_BACK_1 al acabar.");
    }

    // Devuelve el nodo con ese guid, creándolo si no existe (para poder ejecutar el menú varias veces).
    static NarrativeNode Asegurar(NarrativeGraph grafo, string guid, Func<NarrativeNode> crear, string titulo, Vector2 posicion)
    {
        var nodo = grafo.FindNode(guid);
        if (nodo == null)
        {
            nodo = crear();
            nodo.guid = guid;
            nodo.position = posicion;
            nodo.chapter = "Cap. 2";
            grafo.nodes.Add(nodo);
        }
        nodo.displayTitle = titulo;
        return nodo;
    }

    static void CorregirMision(List<string> informe)
    {
        var mision = Cargar<ScriptableObject>(Mision);
        var so = new SerializedObject(mision);
        var pasos = so.FindProperty("steps");
        // El cuarto paso deja de ser la puntería con Eldran y pasa a ser volver a hablar con él.
        for (int i = 0; i < pasos.arraySize; i++)
        {
            var paso = pasos.GetArrayElementAtIndex(i);
            if (paso.FindPropertyRelative("conditionId").stringValue != "PREPARA_VIAJE_ELDRAN") continue;
            paso.FindPropertyRelative("conditionId").stringValue = "PREPARA_VIAJE_VOLVER";
            paso.FindPropertyRelative("description").stringValue = "Vuelve con Eldran";
            paso.FindPropertyRelative("descriptionId").stringValue = "QUEST_PREPARA_VIAJE_STEP4";
        }
        so.FindProperty("detailedDescription").stringValue =
            "Eldran quiere que no salgas con las manos vacías: pasa por la tienda de Victoria, por la boticaria y por el campo de entrenamiento de Erika. Luego vuelve con él.";
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.Add($"Misión: {pasos.arraySize} pasos (el de la puntería pasa a «Vuelve con Eldran»).");
    }

    static void PonerIcono(string rutaObjeto, string rutaIcono, List<string> informe)
    {
        var objeto = Cargar<ScriptableObject>(rutaObjeto);
        var icono = Cargar<Sprite>(rutaIcono);
        var so = new SerializedObject(objeto);
        so.FindProperty("icon").objectReferenceValue = icono;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.Add($"Icono: {Path.GetFileNameWithoutExtension(rutaObjeto)} → {Path.GetFileName(rutaIcono)}.");
    }

    static void CorregirCombateDeErika(List<string> informe)
    {
        var so = new SerializedObject(Cargar<ScriptableObject>(CombateErika));
        so.FindProperty("dialogueOnDefeat").objectReferenceValue = Cargar<ScriptableObject>(DialogoVictoriaErika);
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.Add("Erika: dice lo del pañuelo al perder (DLG_ERIKA_VICTORIA).");
    }

    static void AnadirPopupDeProteccion(List<string> informe)
    {
        var escena = EditorSceneManager.GetSceneByPath(EscenaStart);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(EscenaStart, OpenSceneMode.Additive);

        AbilityUnlockPopupUI popup = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            popup = raiz.GetComponentInChildren<AbilityUnlockPopupUI>(true);
            if (popup != null) break;
        }
        if (popup == null) throw new InvalidOperationException("No hay AbilityUnlockPopupUI en " + EscenaStart);

        var so = new SerializedObject(popup);
        var lista = so.FindProperty("abilityKeyPresentations");
        SerializedProperty entrada = null;
        for (int i = 0; i < lista.arraySize; i++)
        {
            var e = lista.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("abilityKey").intValue == (int)AbilityKey.Shield) { entrada = e; break; }
        }
        if (entrada == null)
        {
            lista.InsertArrayElementAtIndex(lista.arraySize);
            entrada = lista.GetArrayElementAtIndex(lista.arraySize - 1);
            entrada.FindPropertyRelative("abilityKey").intValue = (int)AbilityKey.Shield;
        }
        entrada.FindPropertyRelative("title").stringValue = "Protección";
        entrada.FindPropertyRelative("description").stringValue =
            "Mantén B para alzarla. Si bloqueas en el momento justo, devuelves el golpe.";
        entrada.FindPropertyRelative("icon").objectReferenceValue = Cargar<Sprite>(IconoProteccion);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        informe.Add("Pop-up de Protección: título, texto e icono propios (Start.unity).");
    }

    // Los diálogos de la puntería se quedan sin uso: van a «Versiones antiguas/» de la raíz.
    static void ArchivarDialogosDePunteria(List<string> informe)
    {
        string raiz = Path.GetDirectoryName(Application.dataPath);
        string destino = Path.Combine(raiz, "Versiones antiguas", "Cap2 punteria de Eldran (INC-872)");
        Directory.CreateDirectory(destino);
        foreach (var archivo in DialogosDePunteria)
        {
            string origen = Path.Combine(raiz, CarpetaDialogosEldran + archivo);
            if (!File.Exists(origen)) continue;
            File.Move(origen, Path.Combine(destino, archivo));
            if (File.Exists(origen + ".meta")) File.Move(origen + ".meta", Path.Combine(destino, archivo + ".meta"));
            informe.Add($"Archivado: {archivo}.");
        }
        AssetDatabase.Refresh();
    }

    static T Cargar<T>(string ruta) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("No se encuentra " + ruta);

    static T Nodo<T>(NarrativeGraph grafo, string guid) where T : NarrativeNode
        => grafo.FindNode(guid) as T ?? throw new InvalidOperationException($"Falta el nodo {guid} ({typeof(T).Name}) en {Grafo}");
}
#endif
