#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// Reconstruye SEQ_Prologo_UltimaNoche desde codigo, con el AssetDatabase.
///
/// -- Por que existe -----------------------------------------------------------------------------
/// Es la regla de INC-249, aprendida ahora por tercera vez en el mismo archivo: un .asset que Unity
/// puede tener cargado NO se escribe a mano. Generando su YAML por fuera se colaron, uno detras de
/// otro, un ': ' dentro de un mapa en linea, un espacio de mas delante de un bloque anidado, y un
/// tercer fallo que ni siquiera daba error de parseo -- el asset se leia, las 11 fases salian con
/// sus cuentas correctas, y los 152 beats estaban a null, asi que la secuencia entera se recorria
/// en dos segundos sin ejecutar nada.
///
/// Construyendo los objetos aqui, el YAML lo escribe Unity, que es quien sabe escribirlo. Y como
/// son tipos de verdad, cada nombre de campo se comprueba al compilar en vez de fallar en silencio.
///
/// Es idempotente: reemplaza las fases enteras cada vez que se ejecuta.
public static class ConstruirPrologoUltimaNoche
{
    private const string Ruta = "Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset";
    /// El .inputactions se localiza por su guid, no por una ruta escrita a mano: moverlo de
    /// carpeta no puede romper esto.
    private const string GuidInputActions = "fc9f9f1bd7ce1874fb152afa8758ce88";

    [MenuItem("El Sendero/Secuencias/Prologo: reconstruir la secuencia")]
    public static void Ejecutar()
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(Ruta);
        if (def == null)
        {
            Debug.LogError($"[ConstruirPrologo] No encuentro la secuencia en '{Ruta}'.");
            return;
        }

        Undo.RecordObject(def, "Reconstruir el prologo");
        def.musicId = "PROLOGUE_DREAM";
        def.endStayBlack = true;
        def.phases = Fases();

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int total = 0;
        foreach (var f in def.phases) total += f.beats.Count;
        Debug.Log($"[ConstruirPrologo] Hecho: {def.phases.Count} fases, {total} beats. " +
            "El YAML lo ha escrito Unity, asi que esta bien formado por construccion.");
    }

    // -- Utilidades de referencia ---------------------------------------------------------------

    private static GameObject Prefab(string guid)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(guid);
        var go = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (go == null) Debug.LogWarning($"[ConstruirPrologo] No encuentro el prefab de guid {guid}. Ese VfxBeat se queda vacio.");
        return go;
    }

    /// Un InputActionReference es un sub-asset que genera el importador del Input System, y su
    /// fileID no es derivable desde fuera (ver INC-246). Aqui se resuelve por nombre, que es lo que
    /// ya hacia SequenceInputActionWiring.
    private static InputActionReference Accion(string mapa, string accion)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(GuidInputActions);

        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            if (a is not InputActionReference r || r.action == null) continue;
            if (r.action.name == accion && r.action.actionMap != null && r.action.actionMap.name == mapa)
                return r;
        }

        Debug.LogWarning($"[ConstruirPrologo] No encuentro la accion '{mapa}/{accion}'. El prompt se queda sin ella.");
        return null;
    }

    /// InputPromptBeat.mode es [SerializeField] private, asi que no se puede poner en el
    /// inicializador. Se pone aqui por reflexion, que es la unica forma y es una sola vez.
    private static InputPromptBeat ConModo(InputPromptBeat beat, PanicInputMode modo)
    {
        var campo = typeof(InputPromptBeat).GetField("mode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (campo != null) campo.SetValue(beat, modo);
        else Debug.LogWarning("[ConstruirPrologo] InputPromptBeat ya no tiene el campo 'mode'; el prompt se queda en su modo por defecto.");

        return beat;
    }

    private static SequencePhase Fase(string nombre, string only, string skip, List<SequenceBeat> beats)
        => new() { name = nombre, onlyIfFlag = only, skipIfFlag = skip, beats = beats };

    // -- Las fases ------------------------------------------------------------------------------

    private static List<SequencePhase> Fases() => new()
    {
        Fase("1 - Un dia cualquiera", "", "", new List<SequenceBeat>
        {
            new PlaceAtMarkBeat
            {
                note = "FUERA DE ESCENA. Su SpawnPoint esta en mitad de la plaza, asi que sin esto se pasa los dos primeros minutos de pie entre los vecinos que bailan. Aparece en la fase 4, no antes.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Espera_Oscuro",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new PropMoveBeat
            {
                note = "La carreta empieza VOLCADA, al instante y antes del primer plano. Y APOYADA: al girarla sobre su base media carreta se metia en la tierra, asi que la altura no se pone a ojo -- se mide contra el suelo.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 0.0f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 62.0f),
                segundos = 0.0f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
                apoyarEnElSuelo = true,
            },
            new SetActionAxisBeat
            {
                note = "La camara entra por el este. Todo el decorado esta compuesto para ese lado, y ademas es el lado contrario por el que bajara el Mago Oscuro - baja de frente a la camara.",
                sideDegrees = 90.0f,
            },
            new TimeOfDayBeat
            {
                note = "Manana.",
                timeOfDay = DayNightCycle.TimeOfDay.Morning,
                immediate = true,
                waitForTransition = false,
                transitionSeconds = 2.0f,
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "M_Apertura",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new PlaceAtMarkBeat
            {
                note = "Liora entre la gente desde el primer plano.",
                actorId = "NPC_Liora",
                markName = "M_Plaza_Liora",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new ShotBeat
            {
                note = "VISTA DE PAJARO. La camara catorce metros por encima del pueblo, mirandolo desde arriba, y desde ahi baja sola hasta el. El valle primero, el hombre despues.",
                shotName = "",
                smooth = true,
                duration = 4.5f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 14.0f,
                    distanceScale = 1.6f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ParallelBeat
            {
                note = "La plaza se mueve desde el primer segundo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "FoundSomething_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Question01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "La plaza PASEA, no solo gesticula.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Se une al corro.",
                        actorId = "NPC_Aldeano_02",
                        speed = 1.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.4f,
                        markNames = new List<string> { "M_Duelo_Oscuro" },
                    },
                    new WalkPathBeat
                    {
                        note = "A la mesa del desayuno.",
                        actorId = "NPC_Aldeano_09",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 1.3f,
                        markNames = new List<string> { "M_Mesa" },
                    },
                    new WalkPathBeat
                    {
                        note = "Cruza la plaza hacia el oeste.",
                        actorId = "NPC_Aldeano_04",
                        speed = 1.25f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 2.2f,
                        markNames = new List<string> { "M_Duelo_Mago", "M_Duelo_Choque" },
                    },
                    new WalkPathBeat
                    {
                        note = "Al horno.",
                        actorId = "NPC_Aldeano_10",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 3.4f,
                        markNames = new List<string> { "M_Horno" },
                    },
                },
            },
            new WaitBeat
            {
                note = "Dejar respirar la plaza antes de que nadie hable.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ParallelBeat
            {
                note = "La plaza habla sola desde el primer segundo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Cheer02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new EmotionBeat
            {
                note = "La manana es lo unico alegre de todo el prologo, y hay que verlo en la cara.",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_02",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_03",
                emotion = (NPCEmotion)15,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_04",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)15,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_07",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_08",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_09",
                emotion = (NPCEmotion)15,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_10",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "Y baja hasta el, sin cortar: el descenso ES el enganche con su frase.",
                shotName = "",
                smooth = true,
                duration = 3.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.5f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "Lo justo para que la camara asiente. Habla YA.",
                seconds = 0.15f,
                unscaled = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_INTRO",
                pageDuration = 3.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "La plaza, quieta, con la carreta volcada y el vecino al lado. El Archimago entra andando en el cuadro: la camara no le persigue, le espera.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "NPC_Archimago",
                    heightBias = 1.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Question02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HeadShake02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Cheer02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new MoveToBeat
            {
                note = "Cruza la plaza hasta la carreta.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Carreta",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.6f,
                timeout = 14.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "El vecino le ve llegar.",
                actorId = "NPC_Aldeano_06",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Le llama.",
                actorId = "NPC_Aldeano_06",
                gesture = "HandWave01",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "Los dos, con la carreta volcada en cuadro.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_06",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                markName = "",
                textKey = "PROLOGO_FAVOR_CARRETA",
                pageDuration = 3.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Vecino",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new FaceBeat
            {
                note = "Mira a la carreta ANTES de levantar la mano. Sin esto lanzaba el hechizo de espaldas a la camara, mirando a donde hubiera acabado al llegar.",
                actorId = "NPC_Archimago",
                targetActorId = "PROP_Carreta",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Extiende la mano.",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Levitation_Cast",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "La magia nace en su mano. Es el mismo aura que ve el jugador cuando levita algo.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 2.4f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShotBeat
            {
                note = "EL PLANO QUE FALTABA - la carreta, y solo la carreta. Camara QUIETA (nada de 'live'): si la camara la acompana mientras sube, no se ve que suba.",
                shotName = "",
                smooth = false,
                duration = 3.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "PROP_Carreta",
                    secondaryId = "NPC_Archimago",
                    heightBias = 1.0f,
                    distanceScale = 1.8f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new VfxBeat
            {
                note = "El aura prende en la carreta.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "PROP_Carreta",
                markName = "",
                offset = new Vector3(0.0f, 0.5f, 0.0f),
                lifetime = 2.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new PropMoveBeat
            {
                note = "SE LEVANTA. Sube metro y medio y se endereza a la vez, en 1,4 s -- despacio, que pese. El giro es ABSOLUTO: acaba derecha, no 62 grados menos de como estuviera.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 1.5f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 1.4f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Levitation_Impact",
                clip = null,
                atActorId = "PROP_Carreta",
                markName = "",
                volume = 1.0f,
            },
            new WaitBeat
            {
                note = "Y se queda ahi flotando un segundo. Este segundo es la diferencia entre un truco de magia y un destello.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ParallelBeat
            {
                note = "Y los de mas alla siguen a lo suyo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "Y otro mas. La carreta flotando es el truco entero de la escena.",
                seconds = 0.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "La cara del vecino mirando hacia arriba.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "PROP_Carreta",
                    heightBias = 0.3f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new PropMoveBeat
            {
                note = "Y la posa: vuelve EXACTA a como estaba, derecha y apoyada en el suelo. No se resta lo que se sumo -- se recuerda donde estaba.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 0.0f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 0.9f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
            },
            new SayBeat
            {
                note = "Lo DICE, no lo asiente. Tres HeadNod01 seguidos no son un agradecimiento, son un tic.",
                actorId = "NPC_Aldeano_06",
                markName = "",
                textKey = "PROLOGO_CARRETA_GRACIAS",
                pageDuration = 2.2f,
                gesture = "HeadNod01",
                gestureRepeats = 1,
                speakerNameKey = "Vecino",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("dcd90c4976197424b9958a7c54b6bb8c"),
                atActorId = "PROP_Carreta",
                markName = "",
                offset = new Vector3(0.0f, 0.4f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Levitation_Impact",
                clip = null,
                atActorId = "PROP_Carreta",
                markName = "",
                volume = 1.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)15,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                gesture = "HeadNod01",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El corro que celebra, y el llegando por la izquierda.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_03",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.3f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "HandWave02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Question01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new MoveToBeat
            {
                note = "Hacia el corro que esta de celebracion.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Baile",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.6f,
                timeout = 14.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new ParallelBeat
            {
                note = "El corro ENTERO. Antes bailaban tres y el resto se quedaba de pie en medio del corro mirando al frente, que es lo que canta.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Dance_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Dance_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Dance_NoWeapon",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Cheer02",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HandClap01",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "El se para a mirar.",
                seconds = 0.9f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "Se une un momento - y baila el, que es lo que hace que la frase siguiente tenga gracia.",
                actorId = "NPC_Archimago",
                gesture = "Dance_NoWeapon",
                repeats = 1,
                holdSeconds = 1.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El, en el corro. Plano medio de verdad, no un general.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_03",
                    heightBias = 0.0f,
                    distanceScale = 0.95f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_BAILE",
                pageDuration = 2.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                gesture = "Cheer02",
                repeats = 1,
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El campanario con el globo enganchado, en contrapicado, y el vecino debajo. Encuadrado sobre EL GLOBO: antes iba sobre el vecino con el Archimago de secundario y la camara se iba hacia el, a encuadrar tejados.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "PROP_Globo",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = -1.8f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "Los de la plaza, mientras tanto.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Y los que se fueron, vuelven.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        speed = 1.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.5f,
                        markNames = new List<string> { "M_Aldeano_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 1.8f,
                        markNames = new List<string> { "M_Aldeano_10" },
                    },
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "FoundSomething_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new MoveToBeat
            {
                note = "Hasta el campanario.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Globo",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.6f,
                timeout = 14.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Senala hacia arriba.",
                actorId = "NPC_Aldeano_05",
                gesture = "Question01",
                repeats = 1,
                holdSeconds = 0.7f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El vecino senalando hacia arriba. Es QUIEN HABLA: sin este plano la frase sale de un tejado, que es lo que se ve en la decima grabacion.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "PROP_Globo",
                    heightBias = -0.5f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                markName = "",
                textKey = "PROLOGO_FAVOR_GLOBO",
                pageDuration = 2.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Nina",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El globo, arriba del campanario, en contrapicado cerrado. Antes era un plano GENERAL con el Archimago dentro: para meter a los dos la camara se iba atras y lo que llenaba el cuadro eran los tejados de en medio. Ahora es un primer plano DEL GLOBO: la camara sube con el, a la altura del campanario, y lo que queda detras es cielo. El secundario no sale -- solo da el angulo.",
                shotName = "",
                smooth = false,
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "PROP_Globo",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.4f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "Contraplano - el mago mirando hacia arriba.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "PROP_Globo",
                    heightBias = 0.35f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Levitation_Cast",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 1.8f,
                earlyDespawn = 0.0f,
            },
            new SayBeat
            {
                note = "'Con cuidado...' se dice AL HACER el hechizo, no despues de que el globo ya se haya ido. Es una advertencia, no un comentario.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_GLOBO_OK",
                pageDuration = 2.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new VfxBeat
            {
                note = "El aura prende tambien en el globo, igual que en la carreta - la magia del Archimago siempre se ve igual.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "PROP_Globo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("dcd90c4976197424b9958a7c54b6bb8c"),
                atActorId = "PROP_Globo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 1.8f,
                earlyDespawn = 0.0f,
            },
            new PropMoveBeat
            {
                note = "El globo se suelta y SUBE. Sin esperar - sigue subiendo de fondo mientras la escena continua, y ahora le da tiempo a perderse de vista antes de que nadie lo apague.",
                propId = "PROP_Globo",
                deltaPosicion = new Vector3(2.0f, 22.0f, 1.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 8.0f,
                suavizar = true,
                esperar = false,
            },
            new WaitBeat
            {
                note = "Que se le vea empezar a subir.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Contrapicado fuerte, camara quieta, con el vecino abajo del cuadro. El globo se va por arriba y el plano se queda -- que es lo que hace que se vea SUBIR.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "PROP_Globo",
                    secondaryId = "NPC_Archimago",
                    heightBias = -2.4f,
                    distanceScale = 1.6f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "Y que se le vea IRSE. Este silencio mirando al cielo es el ultimo momento tonto del prologo.",
                seconds = 2.6f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                gesture = "Cheer01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new SetFlagBeat
            {
                note = "El globo se libera bien. Cambiar value a 0 para quedarse con la version en la que revienta.",
                flag = "manana_globo_ok",
                value = true,
            },
        }),
        Fase("1b - El globo se libera bien", "manana_globo_ok", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "La cara del vecino mirando subir el globo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "PROP_Globo",
                    heightBias = -0.6f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)13,
                revertAfter = 0.0f,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Y el corro entero, celebrandolo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_03",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 0.5f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "El corro a la vez. Que se pisen: es lo que hace que suene a gente y no a turnos de palabra.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new SayBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        markName = "",
                        textKey = "PROLOGO_GLOBO_VECINO_1",
                        pageDuration = 2.4f,
                        gesture = "",
                        gestureRepeats = 1,
                        speakerNameKey = "Vecina",
                        playGestures = true,
                        overrideBubbleOffset = false,
                        bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                    },
                    new SayBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        markName = "",
                        textKey = "PROLOGO_GLOBO_VECINO_2",
                        pageDuration = 2.4f,
                        gesture = "",
                        gestureRepeats = 1,
                        speakerNameKey = "Vecino",
                        playGestures = true,
                        overrideBubbleOffset = false,
                        bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                    },
                    new SayBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        markName = "",
                        textKey = "PROLOGO_GLOBO_VECINO_3",
                        pageDuration = 2.4f,
                        gesture = "",
                        gestureRepeats = 1,
                        speakerNameKey = "Vecino",
                        playGestures = true,
                        overrideBubbleOffset = false,
                        bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                    },
                },
            },
            new ParallelBeat
            {
                note = "Y lo celebran con el cuerpo, no solo con la boca.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Cheer02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "Que se oiga el corro.",
                seconds = 2.6f,
                unscaled = true,
            },
            new SetPropActiveBeat
            {
                note = "Y ahora si se apaga, con el ya fuera de plano.",
                propId = "PROP_Globo",
                active = false,
            },
        }),
        Fase("1c - El globo revienta", "", "manana_globo_ok", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "La cara del vecino cuando revienta.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.4f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SetPropActiveBeat
            {
                note = "El globo revienta.",
                propId = "PROP_Globo",
                active = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)2,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "Y el, disculpandose.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_GLOBO_ROTO",
                pageDuration = 2.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.2f,
                unscaled = true,
            },
        }),
        Fase("2 - El rio", "", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "Bajando hacia el rio, EN PICADO desde doce metros. A ras de suelo este tramo eran veinticinco segundos de fachadas: el pueblo se mete en medio haga lo que haga la camara. Desde arriba se les ve a ellos, el pueblo y a donde van.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 12.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "Bajan juntos y se plantan.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Paso normal. A 1,3 m/s bajaban al rio como en una procesion.",
                        actorId = "NPC_Archimago",
                        speed = 1.9f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Rio_Fin_Mago" },
                    },
                    new WalkPathBeat
                    {
                        note = "Paso normal. A 1,3 m/s bajaban al rio como en una procesion.",
                        actorId = "NPC_Liora",
                        speed = 1.9f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Rio_Fin_Liora" },
                    },
                },
            },
            new SetActionAxisBeat
            {
                note = "La camara al oeste: el agua y el puente detras de ellos.",
                sideDegrees = 270.0f,
            },
            new ParallelBeat
            {
                note = "El pueblo sigue vivo detras, aunque no salga.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Cheer02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new FaceBeat
            {
                note = "Se encaran.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_Liora",
                markName = "",
                lookAway = false,
                mutual = true,
                turnDuration = 0.6f,
            },
            new ShotBeat
            {
                note = "Y ya en la orilla, el general: los dos, el agua y el puente. Este plano iba ANTES de que anduvieran, asi que encuadraba la plaza y ellos se iban de cuadro -- cuarenta segundos de casas.",
                shotName = "",
                smooth = false,
                duration = 3.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.0f,
                    distanceScale = 1.35f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Y el agua, un momento, antes de que nadie hable.",
                seconds = 1.6f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos, quietos, encarados, con el rio detras.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_1",
                pageDuration = 3.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLAZA_ARCHIMAGO",
                pageDuration = 3.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_2",
                pageDuration = 3.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Liora",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "Los dos riendose con el rio detras. Es el ultimo momento tranquilo del prologo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.4f,
                    distanceScale = 1.45f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Justo lo que dura la risa. El trueno entra encima, no despues: la pausa mataba el corte.",
                seconds = 0.35f,
                unscaled = true,
            },
        }),
        Fase("3 - Algo cambia en el cielo", "", "", new List<SequenceBeat>
        {
            new WaitBeat
            {
                note = "El silencio, sostenido. Aqui todavia no se ve nada.",
                seconds = 1.8f,
                unscaled = true,
            },
            new WeatherBeat
            {
                note = "El viento agita la plaza.",
                fenomeno = WeatherBeat.Fenomeno.Viento,
                encender = true,
                immediate = false,
            },
            new WeatherBeat
            {
                note = "Las nubes cubren el sol. Sin transicion - la catastrofe no pide permiso.",
                fenomeno = WeatherBeat.Fenomeno.Tormenta,
                encender = true,
                immediate = true,
            },
            new TimeOfDayBeat
            {
                note = "La luz cae.",
                timeOfDay = DayNightCycle.TimeOfDay.Sunset,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 2.0f,
            },
            new ShotBeat
            {
                note = "La gente de la plaza, no la montana. Antes esto se iba tan atras que el cuadro era una pared de roca gris y los vecinos no se veian.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_01",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 1.2f,
                    distanceScale = 0.85f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new ScreenFlashBeat
            {
                note = "Solo el parpadeo de la luz. El rayo no se ve: se oye.",
                color = new Color(1f, 1f, 1f, 0.5f),
                duration = 0.1f,
            },
            new ScreenFlashBeat
            {
                note = "Y el segundo parpadeo, mas flojo.",
                color = new Color(1f, 1f, 1f, 0.28f),
                duration = 0.08f,
            },
            new MusicBeat
            {
                note = "La musica de la manana se apaga con el trueno. Desde aqui hasta que aparece el, silencio: es lo que da tension.",
                musicId = "",
                fadeOut = 2.0f,
            },
            new SfxBeat
            {
                note = "Trueno seco.",
                eventKey = "Prologue_Explosion",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.5f,
                duration = 0.8f,
                waitForEnd = false,
            },
            new ParallelBeat
            {
                note = "Todo el pueblo se gira hacia la montana a la vez. Escalonado un poco (cada uno tarda algo distinto) para que no parezcan doce munecos con el mismo resorte.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.35f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.45f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.35f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.45f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.5f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.55f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.6f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.65f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.7f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.75f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.8f,
                    },
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)9,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_02",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_03",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_04",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_07",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_08",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_09",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_10",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "La cara del Archimago, todavia en la orilla, mirando arriba. Detras de el, el agua: es la ultima vez que ese fondo esta tranquilo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "El silencio despues del trueno. Este es el beat mas importante de la fase.",
                seconds = 1.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos vuelven corriendo al pueblo, EN PICADO desde cinco metros, como la bajada al rio. A ras de suelo, entre casas, la camara saltaba de angulo cada vez que una fachada se metia en medio.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 5.0f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Vuelven corriendo. No esperan a saber que es.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Apertura" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        speed = 3.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Plaza_Liora" },
                    },
                },
            },
            new FaceBeat
            {
                note = "Y se vuelve otra vez hacia la montana.",
                actorId = "NPC_Archimago",
                targetActorId = "",
                markName = "M_Cresta",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
        }),
        Fase("4 - La llegada", "", "", new List<SequenceBeat>
        {
            new PlaceAtMarkBeat
            {
                note = "Y AHORA aparece, en el sitio al que ya esta mirando todo el pueblo. El orden importa: primero el golpe y el susto, despues la figura.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Cresta",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new SfxBeat
            {
                note = "EL GOLPE. Es esto lo que corta la musica -- no un fundido, un impacto.",
                eventKey = "Prologue_Explosion",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "Y se nota en el mando.",
                intensity = 0.55f,
                duration = 0.6f,
                waitForEnd = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_ActorAppear",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new MusicBeat
            {
                note = "Su tema entra con el, encima del golpe. Viene de silencio: la manana se apago con el trueno.",
                musicId = "MAGOOSCURO_REVEAL",
                fadeOut = 0.0f,
            },
            new FaceBeat
            {
                note = "Mira al valle desde el primer fotograma: si no, su entrada es la espalda de alguien parado en una loma.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.2f,
                unscaled = true,
            },
            new PlaceAtMarkBeat
            {
                note = "Otro corte - ya esta al pie de la montana.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Ladera_02",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new ShotBeat
            {
                note = "Y ahora si, un plano que le SIGUE mientras baja los ultimos metros hacia la camara.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.0f,
                    distanceScale = 1.05f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "Un latido antes de que eche a andar.",
                seconds = 0.8f,
                unscaled = true,
            },
            new WalkPathBeat
            {
                note = "Baja los ultimos metros andando, de frente a la camara. A 1,5 m/s eran trece segundos de figura pequena en una ladera marron.",
                actorId = "NPC_MagoOscuro",
                speed = 2.4f,
                stickToGround = true,
                groundOffset = 0.0f,
                faceTravelDirection = true,
                markNames = new List<string> { "M_Entrada_Villa" },
            },
            new FaceBeat
            {
                note = "El Archimago levanta la vista.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new FaceBeat
            {
                note = "Liora tambien.",
                actorId = "NPC_Liora",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.5f,
            },
            new FaceBeat
            {
                note = "Se queda mirando al pueblo, que es hacia donde venia. Sin esto acaba de perfil.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.5f,
            },
            new ShotBeat
            {
                note = "Su cara, de cerca y desde abajo. Primera vez en toda la partida que se le ve.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_IRRUPCION_MAGO",
                pageDuration = 4.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Vineta 7 - desde detras de su espalda, el Archimago y el pueblo al fondo.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.OverTheShoulder,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
        }),
        Fase("5 - La orden de evacuar", "", "", new List<SequenceBeat>
        {
            new TimeOfDayBeat
            {
                note = "Cae la tarde y el valle se pone rojo. El incendio hace el resto.",
                timeOfDay = DayNightCycle.TimeOfDay.Sunset,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 2.0f,
            },
            new SetPropActiveBeat
            {
                note = "Una descarga golpea una casa y el valle empieza a arder.",
                propId = "PROP_Incendio",
                active = true,
            },
            new ShotBeat
            {
                note = "La plaza entera en el momento en que estalla. Hace falta verla LLENA para que vaciarse signifique algo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 2.2f,
                    distanceScale = 2.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_Explosion",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(1f, 0.6f, 0.2f, 1f),
                duration = 0.18f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.6f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new ParallelBeat
            {
                note = "El susto, los diez a la vez y sin esperar a nadie.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        emotion = (NPCEmotion)5,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        emotion = (NPCEmotion)9,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        emotion = (NPCEmotion)5,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        emotion = (NPCEmotion)9,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        emotion = (NPCEmotion)5,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        emotion = (NPCEmotion)9,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        emotion = (NPCEmotion)5,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        emotion = (NPCEmotion)9,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        emotion = (NPCEmotion)5,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        emotion = (NPCEmotion)9,
                        revertAfter = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Se dispersan corriendo. No es todavia la evacuacion: es el panico, que es desordenado a proposito -- cada uno hacia un sitio distinto.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_01" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_02" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_03" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_05" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_06" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_07" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_08" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_10" },
                    },
                },
            },
            new WaitBeat
            {
                note = "Un segundo de gente corriendo antes de que nadie hable.",
                seconds = 1.2f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Ellos dos, en medio de la plaza que se vacia.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)5,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA_1",
                pageDuration = 2.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.25f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO_1",
                pageDuration = 4.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El, gritando a los que se van, DE FRENTE. Antes el plano ponia el puente al fondo, que es lo mismo que poner la camara a su espalda.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_01",
                    heightBias = -0.4f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Senala al puente.",
                actorId = "NPC_Archimago",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.4f,
                returnToNormalAfter = false,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_HORA_ARCHIMAGO",
                pageDuration = 2.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Primera oleada: los seis que pueden andar solos. Se van AHORA y siguen andando por su cuenta mientras la escena continua.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "Con esto el Parallel termina enseguida y los demas siguen andando de fondo el resto de la secuencia.",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_01",
                        speed = 3.75f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_01", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_01" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_02",
                        speed = 3.98f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_02", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_02" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_03",
                        speed = 3.54f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_03", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_03" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_04",
                        speed = 3.87f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_04", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_05",
                        speed = 3.65f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_05", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_05" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_06",
                        speed = 4.09f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_06", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_06" },
                    },
                },
            },
            new ShotBeat
            {
                note = "La plaza vaciandose hacia el puente.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.6f,
                    distanceScale = 2.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 2.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.05f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA_2",
                pageDuration = 2.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO",
                pageDuration = 3.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)2,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.9f,
                unscaled = true,
            },
            new SayBeat
            {
                note = "La broma del rio, devuelta. Es lo que convierte la despedida en una promesa en vez de en un discurso.",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ELEGIR",
                pageDuration = 3.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "Que se quede en el aire.",
                seconds = 1.4f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Ella se vuelve hacia los que quedan y les llama.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Aldeano_07",
                    heightBias = -0.4f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Les hace senas.",
                actorId = "NPC_Liora",
                gesture = "HandWave02",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_EVACUACION_LIORA",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Y salen con ella: los cuatro que quedaban y Liora delante. Despacio -- son los que no pueden correr, y son los que van a seguir cruzando el puente cuando el Archimago diga que todavia estan cruzando, un minuto despues.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Liora",
                        speed = 4.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_07",
                        speed = 2.6f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_07", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_07" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_08",
                        speed = 2.77f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_08", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_08" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_09",
                        speed = 2.66f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_10",
                        speed = 2.88f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_10", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_10" },
                    },
                },
            },
            new ShotBeat
            {
                note = "La plaza vaciandose hacia el puente, y el quieto en medio.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.8f,
                    distanceScale = 2.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Que se les vea irse.",
                seconds = 2.4f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "LA FILA EN EL PUENTE. El puente se nombra tres veces en la fase y no se veia ni una. Encuadrado sobre Liora, que va la primera, y desde cuatro metros y medio de alto: asi entran el tablero, el agua y los que todavia estan cruzando. VIVO, porque van andando.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Aldeano_07",
                    heightBias = 4.5f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new WaitBeat
            {
                note = "Que se les vea cruzar de verdad, no salir de cuadro.",
                seconds = 2.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El, solo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = -0.3f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.6f,
                unscaled = true,
            },
        }),
        Fase("7 - El duelo", "", "", new List<SequenceBeat>
        {
            new MusicBeat
            {
                note = "Silencio antes del duelo: su tema se apaga mientras entra en la plaza vacia.",
                musicId = "",
                fadeOut = 2.0f,
            },
            new SetActionAxisBeat
            {
                note = "El eje al norte. La linea que une a los dos va de oeste a este, asi que la perpendicular es esta -- y los planos sobre el hombro miran al este, que es donde estan el puente y la gente cruzandolo.",
                sideDegrees = 0.0f,
            },
            new ShotBeat
            {
                note = "Le vemos entrar en la plaza vacia. De frente: el Archimago de secundario fija la direccion de la camara, asi que no gira con el.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WalkPathBeat
            {
                note = "Los ultimos metros, andando. Nadie le ha visto cubrir la distancia hasta ahora y eso le hacia parecer que aparecia por corte; asi llega.",
                actorId = "NPC_MagoOscuro",
                speed = 2.4f,
                stickToGround = true,
                groundOffset = 0.0f,
                faceTravelDirection = true,
                animarAndando = true,
                encadenarCon = "",
                retraso = 0.0f,
                markNames = new List<string> { "M_Duelo3_Oscuro" },
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "M_Duelo3_Mago",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_MagoOscuro",
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "M_Duelo3_Oscuro",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_Archimago",
            },
            new ParallelBeat
            {
                note = "Los dos se ponen en guardia a la vez.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "Idle_Battle_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Idle_Battle_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "Plano general de los dos, con la villa vacia y ardiendo detras. Aqui se establece donde esta cada uno; a partir de ahora ya no hace falta volver a explicarlo.",
                shotName = "",
                smooth = false,
                duration = 2.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 1.0f,
                returnToNormalAfter = false,
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_MagoOscuro",
                pose = "Challenging_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_DUELO_MAGO",
                pageDuration = 2.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new MusicBeat
            {
                note = "El climax entra DESPUES de «¿Vas a salvarlos a todos, mago?», que se dice en silencio.",
                musicId = "MAGOOSCURO_CLIMAX",
                fadeOut = 0.5f,
            },
            new ShotBeat
            {
                note = "El lanza.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.45f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_SpellInstantiate",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "La bola se forma en su mano.",
                vfxPrefab = Prefab("5ee28f65ca127db42a9a46da980189d4"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.4f, 0.0f),
                lifetime = 1.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShotBeat
            {
                note = "CORTE. Desde detras del hombro del Archimago - lo que viene, viene hacia nosotros.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.OverTheShoulder,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new PoseBeat
            {
                note = "La guardia se SOSTIENE, no se dispara: el clip es ciclico y disparado se repetia solo dos o tres veces.",
                actorId = "NPC_Archimago",
                pose = "Defend_NoWeapon",
                soltar = false,
                volverAIdle = false,
            },
            new VfxBeat
            {
                note = "El escudo se enciende justo a tiempo.",
                vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, -0.1f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGolpe",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "El impacto.",
                vfxPrefab = Prefab("df9374346b76e444dbbb2b019de4da25"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.2f, 0.0f),
                lifetime = 0.9f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "EstelaAppears_ShieldBlock",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(0.75f, 0.7f, 1.0f, 1.0f),
                duration = 0.12f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.35f,
                duration = 0.4f,
                waitForEnd = false,
            },
            new GestureBeat
            {
                note = "Y al acabar vuelve a la normalidad: sin esto se quedaba encajando el golpe en bucle el resto del duelo.",
                actorId = "NPC_Archimago",
                gesture = "DefendHit_NoWeapon",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = true,
            },
            new WaitBeat
            {
                note = "Fin del asalto 1.",
                seconds = 1.3f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara. Primera vez en todo el prologo que el Archimago ataca a alguien.",
                shotName = "",
                smooth = false,
                duration = 1.3f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.45f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "CORTE al que recibe, antes de que llegue nada - el hechizo entra en el plano ya empezado.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SpellBeat
            {
                note = "Su bola de fuego: sale de la mano, cruza el plano y revienta en el. El golpe va DETRAS, cuando ha llegado.",
                lanzaId = "NPC_Archimago",
                objetivoId = "NPC_MagoOscuro",
                objetivoMarca = "",
                vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                vfxProyectil = Prefab("eccbc655050af0b4f81d8db39f84a58e"),
                vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                sfxLanzamiento = "Star_SpellCast",
                sfxImpacto = "Impact1",
                alturaDeLaMano = 1.15f,
                separacionDelCuerpo = 0.45f,
                velocidad = 14.0f,
                tiempoDeCarga = 0.25f,
                alturaDelImpacto = 1.1f,
                sacudidaAlImpacto = 0.3f,
                esperarAlImpacto = true,
                registrarComo = "",
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("98c4704d0fd7211449bcf5c451095a60"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.2f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
            },
            new GestureBeat
            {
                note = "Le da de lleno.",
                actorId = "NPC_MagoOscuro",
                gesture = "TakeDamage",
                repeats = 1,
                holdSeconds = 0.55f,
                returnToNormalAfter = true,
            },
            new ShotBeat
            {
                note = "Y se rie -- con el Archimago en cuadro, sin inmutarse. Two-shot y no otro primer plano - el corte anterior ya era un plano corto de el, y dos seguidos del mismo tamano y el mismo sujeto se ven como un salto. Ademas asi la risa tiene a quien ignorar.",
                shotName = "",
                smooth = false,
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new WaitBeat
            {
                note = "Fin del asalto 2.",
                seconds = 1.3f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Plano general del choque, desde ARRIBA. A ras de suelo la calle es estrecha y siempre entra media fachada; cuatro metros y medio mas alto se sale por encima de los tejados y se ve el choque entero.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 4.5f,
                    distanceScale = 1.35f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ParallelBeat
            {
                note = "Cargan los dos a la vez.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "MagicSpecial",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "MagicSpecial",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_SpellChargeLoop",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "Un circulo de invocacion a los pies de cada uno.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 0.05f, 0.0f),
                        lifetime = 2.2f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 0.05f, 0.0f),
                        lifetime = 2.2f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.7f,
                unscaled = true,
            },
            new ParallelBeat
            {
                note = "Los dos sueltan.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("8b20002c2b69a9d44ab5bae8e1d923cd"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 1.3f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("df9374346b76e444dbbb2b019de4da25"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 1.3f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "ProjectileClash",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "Y chocan EN EL AIRE, en el punto medio exacto. Colgado de la marca y no de nadie - si se cuelga de uno de los dos, el choque parece que lo esta ganando el otro.",
                vfxPrefab = Prefab("867c572a5be680d42a042d2349f10143"),
                atActorId = "",
                markName = "M_Duelo3_Choque",
                offset = new Vector3(0.0f, 1.6f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "La columna de luz que sube del choque es lo mas cerca que estamos de la verticalidad que pide el guion - los dos personajes no pueden volar, pero lo que se lanzan si.",
                vfxPrefab = Prefab("6555300494081614fa6b6a823cca9f64"),
                atActorId = "",
                markName = "M_Duelo3_Choque",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.2f,
                earlyDespawn = 0.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                duration = 0.28f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.75f,
                duration = 1.0f,
                waitForEnd = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_WarClashStinger_A",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "La onda les tira a los dos hacia atras.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "Dizzy_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.1f,
                        returnToNormalAfter = true,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Dizzy_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.1f,
                        returnToNormalAfter = true,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Polvo levantandose a los pies de los dos.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 0.0f, 0.0f),
                        lifetime = 2.0f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 0.0f, 0.0f),
                        lifetime = 2.0f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new WaitBeat
            {
                note = "Un segundo de nada despues del choque. Sin esto los cuatro asaltos se atropellan y no se lee que son cuatro.",
                seconds = 1.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos doblados, respirando, y la plaza destrozada alrededor. PLANO GENERAL, no two-shot - el anterior ya era un two-shot y hay que cambiar de tamano, no solo de angulo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.8f,
                    distanceScale = 1.9f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "Contrapicado - el que esta mas alto en el cuadro es el que va a ganar este asalto.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicSpecial",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGrieta",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ShotBeat
            {
                note = "Y el suelo se abre BAJO EL. VIVO: justo despues sale despedido dos metros, y con el plano quieto la camara se quedaba encuadrando la carreta.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("d8087ea6f3f1a934e8f05e0bcded1494"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.4f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("3dd50886582244645be87adb42aa8528"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.7f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new SaltoBeat
            {
                note = "El suelo se abre y le LANZA dos metros hacia atras, con el dolor puesto desde el primer fotograma (Pain01): nada de quedarse de pie.",
                actorId = "NPC_Archimago",
                altura = 0.6f,
                desplazamiento = -2.0f,
                haciaElLado = false,
                subida = 0.18f,
                sostener = 0.0f,
                caida = 0.3f,
                poseSubida = "Pain01",
                poseAire = "Pain01",
                poseCaida = "",
                parabola = true,
                poseEnElSuelo = "Pain01",
            },
            new WaitBeat
            {
                note = "Doblado, un momento. Sin esto el golpe no se siente.",
                seconds = 0.8f,
                unscaled = true,
            },
            new PoseBeat
            {
                note = "Se recompone.",
                actorId = "NPC_Archimago",
                pose = "",
                soltar = true,
                volverAIdle = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "El escudo, arriba, parpadeando. Es el cronometro de la escena - lo que se esta jugando no es el, es eso.",
                shotName = "",
                smooth = false,
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "PROP_Escudo",
                    secondaryId = "",
                    heightBias = 1.4f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("a164e676dbbd6e54587237e619372c98"),
                atActorId = "PROP_Escudo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 1.4f,
                earlyDespawn = 0.0f,
            },
            new ShotBeat
            {
                note = "El avanza sobre el caido. VIVO: la camara le acompana mientras anda, en vez de cortar, moverle y volver a cortar -- que es el parpadeo que se veia.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.6f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new MoveToBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                towardsActorId = "",
                markName = "M_Duelo3_Mago",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.3f,
                timeout = 6.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new WaitBeat
            {
                note = "Aire entre los dos cortes.",
                seconds = 0.5f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos - el de pie, el otro en el suelo. La linea de sus cabezas dice el resultado sin una sola palabra.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.8f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_MagoOscuro",
                pose = "Challenging_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_DUELO_MAGO_2",
                pageDuration = 2.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Cada uno a su marca en el mismo corte que lleva a la respuesta: primero se colocan, despues se resuelve el plano, y no se ve ningun salto.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PlaceAtMarkBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        markName = "M_Duelo3_Oscuro",
                        faceTowardsMark = "",
                        faceTowardsActor = "NPC_Archimago",
                    },
                    new PlaceAtMarkBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        markName = "M_Duelo3_Mago",
                        faceTowardsMark = "",
                        faceTowardsActor = "NPC_MagoOscuro",
                    },
                    new ShotBeat
                    {
                        note = "CORTE directo desde «Ni siquiera puedes salvarte tu» a su respuesta. De pie, y la camara mas baja que el.",
                        shotName = "",
                        smooth = false,
                        duration = 2.0f,
                        waitForArrival = true,
                        live = false,
                        framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.7f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
                    },
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Idle_Battle_NoWeapon",
                repeats = 1,
                holdSeconds = 0.0f,
                returnToNormalAfter = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_Archimago",
                pose = "Idle_Battle_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DUELO_ARCHIMAGO",
                pageDuration = 3.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Y el lo entiende un segundo antes de que pase.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.3f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
        }),
        Fase("8 - El ultimo hechizo", "", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "El, desde abajo, antes de despegar.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.4f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)3,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "Se planta.",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new GestureBeat
            {
                note = "Flexiona y salta.",
                actorId = "NPC_MagoOscuro",
                gesture = "JumpStart_InPlace_NoWeapon",
                repeats = 1,
                holdSeconds = 0.3f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_SpellRelease",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "Polvo a sus pies al despegar. Antes salia un rayo, que no venia de ningun sitio.",
                vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 0.2f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new ShotBeat
            {
                note = "Y la camara sube con el, desde muy abajo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -2.8f,
                    distanceScale = 1.6f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Sube echandose atras: siete metros al oeste por seis y medio de alto.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PoseBeat
                    {
                        note = "La pose se sostiene todo el tramo.",
                        actorId = "NPC_MagoOscuro",
                        pose = "fly_idle",
                        soltar = false,
                        volverAIdle = true,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        speed = 3.6f,
                        stickToGround = false,
                        groundOffset = 0.0f,
                        faceTravelDirection = false,
                        animarAndando = false,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Aire_Oscuro" },
                    },
                },
            },
            new FaceBeat
            {
                note = "Arriba, se vuelve a mirarle.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new ShotBeat
            {
                note = "EL PLANAZO. Desde el suelo, hacia arriba, con el recortado contra el cielo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -5.0f,
                    distanceScale = 1.6f,
                    fovOverride = 58.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new PoseBeat
            {
                note = "Flotando. Una pose sostenida, no tres disparos del mismo clip: eso es lo que se leia como que se habia quedado pillado.",
                actorId = "NPC_MagoOscuro",
                pose = "fly_idle",
                soltar = false,
                volverAIdle = true,
            },
            new WaitBeat
            {
                note = "Que se le vea ahi arriba antes de que haga nada.",
                seconds = 1.4f,
                unscaled = true,
            },
            new GestureBeat
            {
                note = "Dispara desde arriba.",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.4f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "El disparo sale de su mano y baja.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new SpellBeat
                    {
                        note = "Nace en su mano, viaja, y revienta en el suelo.",
                        lanzaId = "NPC_MagoOscuro",
                        objetivoId = "",
                        objetivoMarca = "M_Duelo3_Mago",
                        vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                        vfxProyectil = Prefab("232bdd92f4fb5f642bb0d7a40d53380f"),
                        vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                        sfxLanzamiento = "Prologue_SpellInstantiate",
                        sfxImpacto = "Impact1",
                        alturaDeLaMano = 1.15f,
                        separacionDelCuerpo = 0.45f,
                        velocidad = 13.0f,
                        tiempoDeCarga = 0.3f,
                        alturaDelImpacto = 1.1f,
                        sacudidaAlImpacto = 0.55f,
                        esperarAlImpacto = true,
                        registrarComo = "",
                    },
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.35f,
                        unscaled = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "El Archimago lo ve venir.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.8f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)4,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "La esquiva, siguiendole.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.6f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new SaltoBeat
            {
                note = "SE QUITA DE EN MEDIO. Salta hacia ARRIBA y de lado, desde donde este, y baja al suelo que tenga debajo. Antes esto eran dos viajes a marcas aereas puestas a mano: iba hacia la coordenada y no hacia arriba -- \"parece que salta para otro lado\" -- y aterrizaba donde dijera el numero, que en la sexta grabacion fue encima de un tejado.",
                actorId = "NPC_Archimago",
                altura = 2.8f,
                desplazamiento = 2.6f,
                haciaElLado = true,
                subida = 0.4f,
                sostener = 0.35f,
                caida = 0.45f,
                poseSubida = "JumpStart_InPlace_NoWeapon",
                poseAire = "JumpAirSpin_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new WaitBeat
            {
                note = "Un respiro. Aqui es donde se entiende que ha esquivado.",
                seconds = 1.2f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El, desde abajo, contestando.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -1.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "Responde.",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "El contraataque, siguiendole por el aire.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new SpellBeat
                    {
                        note = "Nace en su mano y persigue al Mago Oscuro.",
                        lanzaId = "NPC_Archimago",
                        objetivoId = "NPC_MagoOscuro",
                        objetivoMarca = "",
                        vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                        vfxProyectil = Prefab("eccbc655050af0b4f81d8db39f84a58e"),
                        vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                        sfxLanzamiento = "Star_SpellCast",
                        sfxImpacto = "ProjectileClash",
                        alturaDeLaMano = 1.15f,
                        separacionDelCuerpo = 0.45f,
                        velocidad = 15.0f,
                        tiempoDeCarga = 0.3f,
                        alturaDelImpacto = 1.1f,
                        sacudidaAlImpacto = 0.4f,
                        esperarAlImpacto = true,
                        registrarComo = "HECHIZO_ARCHIMAGO",
                    },
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.3f,
                        unscaled = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "Le da en el aire, y se le ve encajarlo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -2.2f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new EsperarHechizoBeat
            {
                note = "Hasta que le llega. Sin esto se dolia antes de que la bola le alcanzara.",
                hechizo = "HECHIZO_ARCHIMAGO",
                topeDeSalida = 1.5f,
                topeDeVuelo = 6.0f,
            },
            new GestureBeat
            {
                note = "Le alcanza de lleno.",
                actorId = "NPC_MagoOscuro",
                gesture = "TakeDamage",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "Un instante suspendido antes de venirse abajo.",
                seconds = 0.6f,
                unscaled = true,
            },
            new SaltoBeat
            {
                note = "No baja volando: se cae. Es lo que hace el jugador cuando le alcanzan en el aire, y es lo que hace que el golpe cuente.",
                actorId = "NPC_MagoOscuro",
                altura = 0.0f,
                desplazamiento = 0.0f,
                haciaElLado = false,
                subida = 0.0f,
                sostener = 0.0f,
                caida = 0.75f,
                poseSubida = "",
                poseAire = "JumpAir_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new GestureBeat
            {
                note = "Toma tierra de mala manera.",
                actorId = "NPC_MagoOscuro",
                gesture = "Landing",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "Y AQUI se espera. Es el unico momento del prologo en que el Mago Oscuro no manda.",
                seconds = 1.1f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara, desde abajo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.7f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                emotion = (NPCEmotion)13,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "Y se rie. Desde el suelo, que es peor.",
                actorId = "NPC_MagoOscuro",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 1.3f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "VIVO desde que toma impulso: la camara le sigue en el despegue, la subida y el picado, sin cortes. Sustituye al general a ras de suelo de la foto del 21 sep, en el que se salia por arriba.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.8f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new GestureBeat
            {
                note = "Toma impulso.",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new SaltoBeat
            {
                note = "Y vuelve a subir, ahora que esta en el suelo.",
                actorId = "NPC_MagoOscuro",
                altura = 7.0f,
                desplazamiento = 0.0f,
                haciaElLado = false,
                subida = 0.6f,
                sostener = 0.2f,
                caida = 0.0f,
                poseSubida = "JumpStart_InPlace_NoWeapon",
                poseAire = "JumpAir_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.7f,
                unscaled = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGrieta",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new FaceBeat
            {
                note = "De cara a lo que baja: al romperse el escudo sale despedido HACIA ATRAS, y atras depende de adonde mire.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.25f,
            },
            new ParallelBeat
            {
                note = "Baja a por el, y el se cubre mientras baja.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new ParallelBeat
                    {
                        note = "Se tira en picado a por el: diez metros en poco mas de un segundo.",
                        waitForAll = true,
                        beats = new List<SequenceBeat>
                        {
                            new PoseBeat
                            {
                                note = "La pose se sostiene todo el tramo.",
                                actorId = "NPC_MagoOscuro",
                                pose = "fly_dive",
                                soltar = false,
                                volverAIdle = true,
                            },
                            new WalkPathBeat
                            {
                                note = "",
                                actorId = "NPC_MagoOscuro",
                                speed = 9.0f,
                                stickToGround = false,
                                groundOffset = 0.0f,
                                faceTravelDirection = false,
                                animarAndando = false,
                                encadenarCon = "",
                                retraso = 0.0f,
                                markNames = new List<string> { "M_Picado_Oscuro" },
                            },
                        },
                    },
                    new PoseBeat
                    {
                        note = "La guardia se SOSTIENE, no se dispara: el clip es ciclico y disparado se repetia solo dos o tres veces.",
                        actorId = "NPC_Archimago",
                        pose = "Defend_NoWeapon",
                        soltar = false,
                        volverAIdle = false,
                    },
                    new VfxBeat
                    {
                        note = "El escudo, encendido antes del choque.",
                        vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, -0.1f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                        seguirAlActor = true,
                    },
                },
            },
            new VfxBeat
            {
                note = "El choque, sobre el escudo.",
                vfxPrefab = Prefab("67a684e320da6e7439421a07e3fa265c"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.7f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new VfxBeat
            {
                note = "El escudo, rompiendose.",
                vfxPrefab = Prefab("98c4704d0fd7211449bcf5c451095a60"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.0f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGolpe",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(1f, 0.6f, 0.2f, 1f),
                duration = 0.16f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.8f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new ShotBeat
            {
                note = "Le sigue mientras vuela. VIVO, general y un poco alto: se tiene que ver entera la parabola y donde cae.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 1.2f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Sale despedido; el otro toma tierra.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new SaltoBeat
                    {
                        note = "Le rompe el escudo y le LANZA en parabola, cayendo con Die01 (el derribo del pack: de pie a tirado). Se queda en su ultimo fotograma hasta GetUp.",
                        actorId = "NPC_Archimago",
                        altura = 2.2f,
                        desplazamiento = -2.8f,
                        haciaElLado = false,
                        subida = 0.42f,
                        sostener = 0.0f,
                        caida = 0.55f,
                        poseSubida = "Die01_NoWeapon",
                        poseAire = "Die01_NoWeapon",
                        poseCaida = "",
                        parabola = true,
                        poseEnElSuelo = "Die01_NoWeapon",
                    },
                    new GestureBeat
                    {
                        note = "Toma tierra al final del picado.",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Landing",
                        repeats = 1,
                        holdSeconds = 0.5f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "En el suelo, un momento.",
                seconds = 0.6f,
                unscaled = true,
            },
            new PoseBeat
            {
                note = "Y se levanta. GetUp arranca justo de la pose de tirado en el suelo, y sin bucle se queda de pie en su ultimo fotograma.",
                actorId = "NPC_Archimago",
                pose = "GetUp_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new WaitBeat
            {
                note = "Lo que dura levantarse (25 fotogramas).",
                seconds = 1.0f,
                unscaled = true,
            },
            new PoseBeat
            {
                note = "De pie otra vez.",
                actorId = "NPC_Archimago",
                pose = "",
                soltar = true,
                volverAIdle = true,
            },
            new GestureBeat
            {
                note = "Y vuelve a despegar.",
                actorId = "NPC_MagoOscuro",
                gesture = "JumpStart_InPlace_NoWeapon",
                repeats = 1,
                holdSeconds = 0.25f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "Y vuelve a subir, por el otro lado.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PoseBeat
                    {
                        note = "La pose se sostiene todo el tramo.",
                        actorId = "NPC_MagoOscuro",
                        pose = "fly_idle",
                        soltar = false,
                        volverAIdle = true,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        speed = 5.5f,
                        stickToGround = false,
                        groundOffset = 0.0f,
                        faceTravelDirection = false,
                        animarAndando = false,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Aire_Oscuro_3" },
                    },
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.9f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "EL PLANAZO otra vez, mas cerrado. Las manos levantadas y el cielo detras.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -5.5f,
                    distanceScale = 0.95f,
                    fovOverride = 36.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new PoseBeat
            {
                note = "SOSTIENE el hechizo. Es una pose mantenida, que es lo que significa sostener: repetirla tres veces era lo que se veia como un tic.",
                actorId = "NPC_MagoOscuro",
                pose = "FoundSomething_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_SpellChargeLoop",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "El hechizo nace entre sus manos. Y se queda EN sus manos: esta flotando, asi que un VFX clavado en el mundo se le escapa del cuerpo.",
                vfxPrefab = Prefab("5ee28f65ca127db42a9a46da980189d4"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.9f, 0.0f),
                lifetime = 4.5f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.3f,
                unscaled = true,
            },
            new VfxBeat
            {
                note = "Y crece. Un segundo VFX encima del primero, mas grande, es lo que hace que se lea como que se esta HACIENDO GRANDE y no como que ya estaba ahi.",
                vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 2.1f, 0.0f),
                lifetime = 3.5f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.35f,
                duration = 1.6f,
                waitForEnd = false,
            },
            new WaitBeat
            {
                note = "Que dure. Es la unica amenaza de todo el prologo que se ve venir.",
                seconds = 1.6f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara, un segundo antes de soltarlo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.2f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Y lo suelta.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -3.4f,
                    distanceScale = 1.45f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Lo lanza.",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.3f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_WarClashStinger_A",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "Y cae. El plano corta al Archimago mientras el hechizo sigue en el aire.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new SpellBeat
                    {
                        note = "El hechizo grande, cayendo.",
                        lanzaId = "NPC_MagoOscuro",
                        objetivoId = "NPC_Archimago",
                        objetivoMarca = "",
                        vfxEnLaMano = Prefab("41494896fc96c9748b81d3356632794e"),
                        vfxProyectil = Prefab("232bdd92f4fb5f642bb0d7a40d53380f"),
                        vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                        sfxLanzamiento = "",
                        sfxImpacto = "Prologue_Explosion",
                        alturaDeLaMano = 1.9f,
                        separacionDelCuerpo = 0.45f,
                        velocidad = 8.0f,
                        tiempoDeCarga = 0.5f,
                        alturaDelImpacto = 1.1f,
                        sacudidaAlImpacto = 0.9f,
                        esperarAlImpacto = true,
                        registrarComo = "HECHIZO_FINAL",
                    },
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.4f,
                        unscaled = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.6f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "CORTE al Archimago. Le sigue mientras corre, con el Mago Oscuro de secundario para que la camara no gire con el.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.5f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                revertAfter = 0.0f,
            },
            new ParallelBeat
            {
                note = "Lo grita corriendo, no parado.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Corre HACIA lo que le viene encima, no en contra. Cuatro metros y medio por segundo es carrera: WalkPathBeat pasa esa velocidad al animator y sale Run.",
                        actorId = "NPC_Archimago",
                        speed = 4.5f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "JumpStart_InPlace_NoWeapon",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Carrera_Mago" },
                    },
                    new SayBeat
                    {
                        note = "La primera mitad, en carrera.",
                        actorId = "NPC_Archimago",
                        markName = "",
                        textKey = "PROLOGO_HECHIZO_1",
                        pageDuration = 1.2f,
                        gesture = "",
                        gestureRepeats = 1,
                        speakerNameKey = "Archimago",
                        playGestures = false,
                        overrideBubbleOffset = false,
                        bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                    },
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Star_SpellCast",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new TimeScaleBeat
            {
                note = "Camara lenta SOLO para esto.",
                timeScale = 0.45f,
                rampDuration = 0.2f,
            },
            new ShotBeat
            {
                note = "El, en el aire, desde abajo, con el hechizo cayendole encima. VIVO: sube tres metros, asi que un plano quieto le pierde justo cuando dice la frase.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -2.4f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new SaltoBeat
            {
                note = "EL SALTO. Sube tres metros desde donde este y SE QUEDA ARRIBA (caida=0): la frase se dice en el aire y lo que viene despues es el fundido a negro, asi que no hay que bajarle. Antes iba a la marca M_Salto_Mago_Aire y terminaba de pie encima de un tejado.",
                actorId = "NPC_Archimago",
                altura = 6.0f,
                desplazamiento = 1.2f,
                haciaElLado = false,
                subida = 1.0f,
                sostener = 0.0f,
                caida = 0.0f,
                poseSubida = "JumpStart_InPlace_NoWeapon",
                poseAire = "JumpAir_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new PoseBeat
            {
                note = "Los brazos levantados, y SOSTENIDOS ahi: un gesto suelto acabaria en idle en mitad del aire, y el clip sin bucle se queda en su ultimo fotograma -- con los brazos arriba.",
                actorId = "NPC_Archimago",
                pose = "FoundSomething_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "Y la segunda mitad arriba, con los brazos levantados. Es la ultima frase del prologo.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_HECHIZO",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new SetPropActiveBeat
            {
                note = "La cupula se levanta AHORA, con la frase, y no dos minutos antes. Era lo que llenaba media pantalla de azul encima del bocadillo.",
                propId = "PROP_Escudo",
                active = true,
            },
            new VfxBeat
            {
                note = "La luz nace en sus manos y cubre el valle. SIGUE al Archimago: lo lanza en el aire y despues baja, y sin esto el escudo se queda arriba.",
                vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, -0.1f, 0.0f),
                lifetime = 3.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("6555300494081614fa6b6a823cca9f64"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 3.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ScreenFlashBeat
            {
                note = "El escudo se enciende.",
                color = new Color(1f, 1f, 1f, 1f),
                duration = 0.25f,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.5f,
                unscaled = true,
            },
            new ScreenFadeBeat
            {
                note = "A NEGRO, y la explosion se oye DESPUES. Ver lo que pasa seria menos que imaginarlo: lo que hay al otro lado del negro es el valle entero.",
                fadeIn = true,
                color = Color.black,
                duration = 0.7f,
                waitForEnd = true,
            },
            new TimeScaleBeat
            {
                note = "",
                timeScale = 1.0f,
                rampDuration = 0.0f,
            },
            new WaitBeat
            {
                note = "Medio segundo de negro y de silencio.",
                seconds = 0.6f,
                unscaled = true,
            },
        }),
        Fase("9 - La explosion", "", "", new List<SequenceBeat>
        {
            new SfxBeat
            {
                note = "LA EXPLOSION, sobre negro. Y el negro se queda: de aqui se sale ya en la habitacion de Will, sin volver a ensenar el valle.",
                eventKey = "Prologue_Explosion",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_WarClashStinger_B",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "El mando tiembla aunque no se vea nada. Es lo que hace que el negro sea la explosion y no un corte.",
                intensity = 0.9f,
                duration = 1.6f,
                waitForEnd = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 2.2f,
                unscaled = true,
            },
        }),
    };
}
#endif
