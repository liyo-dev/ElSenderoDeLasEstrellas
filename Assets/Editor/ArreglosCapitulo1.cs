using System.Collections.Generic;
using Game.NPC;
using UnityEditor;
using UnityEngine;

/// Arreglos de datos del principio del capítulo 1 (INC-358 a INC-361). Idempotente: se puede
/// ejecutar las veces que haga falta, y lo llama también PREPARAR TODO.
///
/// Todo se hace con PrefabUtility, SerializedObject y AssetDatabase — nada de YAML a mano (INC-249).
///
///   1. Fuera «Will, ¡DESPIERTA!» y su iris. El nodo de texto dramático del grafo pasa a ser un
///      fundido normal que destapa la habitación y pone su música. Se borran el shader y el
///      material del iris y la frase, para no dejar basura.
///   2. Oliver se une al grupo. El Oliver que aparece en el mundo es el del ROSTER
///      (_NPCs/_GrafoNarrativo/Oliver.prefab), no _NPCs/Oliver.prefab, que es el que se había
///      preparado: el de verdad no era compañero ni tenía PartyConfig, y los PartyMembershipSignal
///      se habían enganchado en la escena, donde Oliver no existe hasta que el roster lo crea.
///   3. Eldran: el aviso de llegada (WILL_REACHED_ELDRAN, que lanza la secuencia de las peras)
///      y su marcador en el minimapa, en el prefab del roster. Ninguno de los dos existía: el
///      montaje que los ponía colgaba de la escena y nunca llegó a ejecutarse.
///   4. En la ventana, la cara de Will cambia: sorpresa al primer grito, y media sonrisa cuando
///      comenta que ya les conoce.
public static class ArreglosCapitulo1
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string GuidNodoDespierta = "85279f14-0f9a-4376-8ca3-126ecb12b966";

    private static readonly string[] BasuraDelIris =
    {
        "Assets/Shaders/UI/Mat_CircleIrisCutout.mat",
        "Assets/Shaders/UI/CircleIrisCutout.shader",
        "Assets/_DIALOGUES/Prologo/DramaticText_Prolog.asset",
    };

    private const string RutaOliver = "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab";
    private const string RutaPartyOliver = "Assets/_NPCs/Party/Oliver_PartyConfig.asset";
    private const string RutaEldran = "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab";
    private const string RutaVentana = "Assets/_SEQUENCES/SEQ_DiscusionVentana.asset";

    private const int FlagCompanion = 1 << 6;

    [MenuItem("El Sendero/Capítulo 1: arreglos (despertar, Oliver, Eldran, ventana)")]
    public static void Menu()
    {
        string r = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Capítulo 1", r, "Vale");
    }

    public static string Ejecutar(bool avisar)
    {
        var informe = new List<string>
        {
            QuitarDespierta(),
            OliverCompanero(),
            EldranLlegadaYMarcador(),
            CaraEnLaVentana(),
            DiscusionDeEldranYVictoria(),
        };
        AssetDatabase.SaveAssets();
        string r = string.Join("\n", informe);
        Debug.Log("[Capítulo 1]\n" + r);
        return r;
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────────────────────
    private static string QuitarDespierta()
    {
        string r;
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null) return $"(1) No encuentro {RutaGrafo}.";

        int i = grafo.nodes.FindIndex(n => n != null && n.guid == GuidNodoDespierta);
        if (i < 0) r = "(1) El nodo de «Will, ¡DESPIERTA!» ya no está.";
        else if (grafo.nodes[i] is ScreenFadeNode) r = "(1) El despertar ya era un fundido normal.";
        else
        {
            var viejo = grafo.nodes[i];
            var nuevo = new ScreenFadeNode
            {
                displayTitle = "4.- Se destapa la habitacion de Will",
                chapter = viejo.chapter,
                blockSaving = viejo.blockSaving,
                inputAnchor = viejo.inputAnchor,
                outputAnchor = viejo.outputAnchor,
                overrideNodeColor = viejo.overrideNodeColor,
                nodeColor = viejo.nodeColor,
                guid = viejo.guid,
                position = viejo.position,
                outputs = new List<string>(viejo.outputs),
                color = Color.black,
                duration = 0.8f,
                fadeIn = false,
                restaurarMusicaDeEscena = true,
                fundidoDeMusica = 2f,
            };
            grafo.nodes[i] = nuevo;
            EditorUtility.SetDirty(grafo);
            r = "(1) «Will, ¡DESPIERTA!» fuera: ahora la habitación se destapa con un fundido normal y suena su música.";
        }

        int borrados = 0;
        foreach (var ruta in BasuraDelIris)
            if (AssetDatabase.LoadMainAssetAtPath(ruta) != null && AssetDatabase.DeleteAsset(ruta)) borrados++;
        if (borrados > 0) r += $" Borrados {borrados} archivo(s) del iris.";
        return r;
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────────────────────
    private static string OliverCompanero()
    {
        var party = AssetDatabase.LoadAssetAtPath<Object>(RutaPartyOliver);
        if (party == null) return $"(2) No encuentro {RutaPartyOliver}.";

        var raiz = PrefabUtility.LoadPrefabContents(RutaOliver);
        if (raiz == null) return $"(2) No encuentro {RutaOliver}.";
        try
        {
            var manager = raiz.GetComponentInChildren<NPCBehaviourManagerV2>(true);
            if (manager == null) return "(2) El prefab de Oliver no tiene NPCBehaviourManagerV2.";

            var so = new SerializedObject(manager);
            var tipo = so.FindProperty("configuration.behaviourType");
            var cfg = so.FindProperty("configuration.partyConfig");
            if (tipo == null || cfg == null) return "(2) NPCBehaviourManagerV2 ha cambiado: no encuentro behaviourType/partyConfig.";

            bool cambiado = false;
            if ((tipo.intValue & FlagCompanion) == 0) { tipo.intValue |= FlagCompanion; cambiado = true; }
            if (cfg.objectReferenceValue != party) { cfg.objectReferenceValue = party; cambiado = true; }
            so.ApplyModifiedPropertiesWithoutUndo();

            var go = manager.gameObject;
            cambiado |= Senal(go, "OLIVER_JOIN_PARTY", PartyMembershipSignal.SignalAction.Join);
            cambiado |= Senal(go, "PERAS_START", PartyMembershipSignal.SignalAction.Leave);

            if (!cambiado) return "(2) Oliver ya era compañero y ya escuchaba OLIVER_JOIN_PARTY / PERAS_START.";
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaOliver);
            return "(2) Oliver (el del roster) ya es compañero: se une al grupo con OLIVER_JOIN_PARTY y se sale con PERAS_START.";
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    private static bool Senal(GameObject go, string clave, PartyMembershipSignal.SignalAction accion)
    {
        foreach (var s in go.GetComponents<PartyMembershipSignal>())
            if (new SerializedObject(s).FindProperty("narrativeEventKey").stringValue == clave) return false;

        var nueva = go.AddComponent<PartyMembershipSignal>();
        var so = new SerializedObject(nueva);
        so.FindProperty("narrativeEventKey").stringValue = clave;
        so.FindProperty("action").enumValueIndex = (int)accion;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────────────────────
    private const string NombreLlegada = "_LlegadaDeWill";

    private static string EldranLlegadaYMarcador()
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaEldran);
        if (raiz == null) return $"(3) No encuentro {RutaEldran}.";
        try
        {
            var manager = raiz.GetComponentInChildren<NPCBehaviourManagerV2>(true);
            var go = manager != null ? manager.gameObject : raiz;
            bool cambiado = false;

            // Llegada: una esfera de 4 m que avisa SOLO si el grafo está esperando (onlyIfListening),
            // para que pasar por delante de Eldran antes de tiempo no deje el aviso pendiente.
            var llegada = go.transform.Find(NombreLlegada);
            if (llegada == null)
            {
                var hijo = new GameObject(NombreLlegada);
                hijo.transform.SetParent(go.transform, false);
                var esfera = hijo.AddComponent<SphereCollider>();
                esfera.isTrigger = true;
                esfera.radius = 2.5f;   // Eldran queda a cuatro metros del puesto de Oliver: el aviso tiene que ser de LLEGAR (INC-382).
                hijo.AddComponent<SignalEmitter>();
                llegada = hijo.transform;
                cambiado = true;
            }

            // La guarda es LA MISIÓN, no «que alguien esté escuchando» (INC-365): el grafo no se
            // pone a esperar WILL_REACHED_ELDRAN hasta que el jugador cierra el tutorial del
            // minimapa, así que con onlyIfListening se podía llegar hasta Eldran y no pasar nada.
            var esferaYa = llegada.GetComponent<SphereCollider>();
            if (esferaYa != null && esferaYa.radius > 2.51f) { esferaYa.radius = 2.5f; cambiado = true; }

            var emisor = llegada.GetComponent<SignalEmitter>();
            if (emisor != null && (emisor.eventKey != "WILL_REACHED_ELDRAN"
                                   || emisor.soloConLaMision != "ELDRAN_MISSION1"
                                   || emisor.radioDeProximidad > 2.51f || emisor.radioDeProximidad < 0.01f
                                   || emisor.onlyIfListening))
            {
                emisor.eventKey = "WILL_REACHED_ELDRAN";
                emisor.trigger = SignalEmitter.TriggerType.PhysicsTrigger;
                emisor.requiredTag = "Player";
                emisor.once = true;
                emisor.onlyIfListening = false;
                emisor.soloConLaMision = "ELDRAN_MISSION1";
                // Y por distancia además del trigger (INC-376): que llegar hasta Eldran arranque
                // la escena no puede depender de que un collider dispare bien.
                emisor.radioDeProximidad = 2.5f;
                cambiado = true;
            }

            // Marcador del minimapa mientras la misión «Busca a Eldran» esté activa.
            bool tieneMarcador = false;
            foreach (var m in go.GetComponents<QuestObjectiveMarker>())
                if (new SerializedObject(m).FindProperty("questId").stringValue == "ELDRAN_MISSION1") tieneMarcador = true;
            if (!tieneMarcador)
            {
                var marcador = go.AddComponent<QuestObjectiveMarker>();
                var so = new SerializedObject(marcador);
                so.FindProperty("questId").stringValue = "ELDRAN_MISSION1";
                so.FindProperty("stepIndex").intValue = -1;
                so.FindProperty("showAfterStep").intValue = -1;
                so.ApplyModifiedPropertiesWithoutUndo();
                cambiado = true;
            }

            if (!cambiado) return "(3) Eldran ya tenía su aviso de llegada y su marcador.";
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaEldran);
            return "(3) Eldran (el del roster): aviso WILL_REACHED_ELDRAN a 4 m y marcador en el minimapa con ELDRAN_MISSION1.";
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────────────────────
    private const string RutaIconoPelea = "Assets/_NPCs/OverHead/Canvas Fight!.prefab";

    /// Eldran y Victoria discuten en bucle desde el saludo de Oliver hasta PERAS_START (INC-363).
    private static string DiscusionDeEldranYVictoria()
    {
        var icono = AssetDatabase.LoadAssetAtPath<GameObject>(RutaIconoPelea);
        var raiz = PrefabUtility.LoadPrefabContents(RutaEldran);
        if (raiz == null) return $"(5) No encuentro {RutaEldran}.";
        try
        {
            var manager = raiz.GetComponentInChildren<NPCBehaviourManagerV2>(true);
            var go = manager != null ? manager.gameObject : raiz;
            var d = go.GetComponent<DiscusionEnBucle>();
            bool nuevo = d == null;
            if (nuevo) d = go.AddComponent<DiscusionEnBucle>();

            var so = new SerializedObject(d);
            var campo = so.FindProperty("iconoDePelea");
            bool cambiado = nuevo;
            if (campo != null && campo.objectReferenceValue != icono)
            {
                campo.objectReferenceValue = icono;
                cambiado = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!cambiado) return "(5) Eldran y Victoria ya discutían en bucle.";
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaEldran);
            return "(5) Eldran y Victoria discuten en bucle (enfado/hablar alternados, icono de pelea) hasta PERAS_START." +
                   (icono == null ? $" OJO: no encuentro {RutaIconoPelea}, van sin icono." : "");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────────────────────
    private const string NotaCara = "CARA (INC-361)";

    private static string CaraEnLaVentana()
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaVentana);
        if (def == null || def.phases.Count == 0) return $"(4) No encuentro {RutaVentana}.";

        var beats = def.phases[0].beats;
        foreach (var b in beats)
            if (b is EmotionBeat e && (e.note ?? "").StartsWith(NotaCara)) return "(4) La cara de Will en la ventana ya estaba.";

        // Sorpresa en cuanto se gira a la ventana (el FaceBeat va el primero).
        int iGiro = beats.FindIndex(b => b is FaceBeat f && f.actorId == SequenceActor.PlayerId);
        beats.Insert(iGiro >= 0 ? iGiro + 1 : 0, new EmotionBeat
        {
            note = NotaCara + ": sorpresa al primer grito.",
            actorId = SequenceActor.PlayerId,
            emotion = NPCEmotion.Surprised,
            revertAfter = 0f,
        });

        // Media sonrisa cuando él comenta la escena: ya les conoce.
        int iComenta = beats.FindLastIndex(b => b is SayBeat s && s.actorId == SequenceActor.PlayerId);
        if (iComenta >= 0)
            beats.Insert(iComenta, new EmotionBeat
            {
                note = NotaCara + ": media sonrisa, ya les conoce.",
                actorId = SequenceActor.PlayerId,
                emotion = NPCEmotion.Smirk,
                revertAfter = 0f,
            });

        // Y la pensativa del medio pasa a confusa, que se distingue más.
        foreach (var b in beats)
            if (b is ParallelBeat p && p.beats != null)
                foreach (var h in p.beats)
                    if (h is EmotionBeat e && e.actorId == SequenceActor.PlayerId && e.emotion == NPCEmotion.Thinking)
                        e.emotion = NPCEmotion.Confused;

        EditorUtility.SetDirty(def);
        return "(4) Will en la ventana: sorpresa al primer grito, confuso mientras discuten, media sonrisa al final.";
    }
}
