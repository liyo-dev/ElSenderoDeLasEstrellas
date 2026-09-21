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
            new PropMoveBeat
            {
                note = "La carreta empieza VOLCADA. Al instante y antes del primer plano, asi que nadie ve el truco - lo unico que se ve es que esta tumbada.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, -0.35f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 62.0f),
                segundos = 0.0f,
                suavizar = true,
                esperar = true,
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
                note = "La plaza con gente y la montana al fondo. Tiene que decir que esto es un sitio con gente dentro, y ensenar la montana de la que vendra el.",
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
                    heightBias = 2.0f,
                    distanceScale = 2.8f,
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
            new WaitBeat
            {
                note = "Dejar respirar la plaza antes de que nadie hable.",
                seconds = 2.5f,
                unscaled = true,
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
                note = "El, mirando su valle.",
                shotName = "",
                smooth = false,
                duration = 2.4f,
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
                note = "Le seguimos mientras cruza la plaza. Esto es lo que faltaba - verle IR a los sitios.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
            },
            new ShotBeat
            {
                note = "EL PLANO QUE FALTABA - la carreta, y solo la carreta. Aqui es donde se ve el favor.",
                shotName = "",
                smooth = false,
                duration = 3.2f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "PROP_Carreta",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.4f,
                    distanceScale = 1.2f,
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
            },
            new PropMoveBeat
            {
                note = "SE LEVANTA. Sube metro y medio y se endereza a la vez, en 1,4 s -- despacio, que pese.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 1.5f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, -62.0f),
                segundos = 1.4f,
                suavizar = true,
                esperar = true,
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
                note = "Y la posa. Baja mas rapido de lo que subio, como se suelta algo que ya esta donde toca.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, -1.5f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 0.9f,
                suavizar = true,
                esperar = true,
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
                note = "Los dos, con la carreta ya de pie entre ellos.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "Le seguimos otra vez.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
                note = "El corro, a la vez - dos bailando y uno llevando el compas con las palmas.",
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
                },
            },
            new ShotBeat
            {
                note = "El corro.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_03",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.4f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
                note = "",
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
                    distanceScale = 1.0f,
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
                note = "Y otra vez.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
                note = "El globo enganchado en el campanario.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "PROP_Globo",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.8f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
            new VfxBeat
            {
                note = "El aura prende tambien en el globo, igual que en la carreta - la magia del Archimago siempre se ve igual.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "PROP_Globo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
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
                note = "El globo se suelta y SUBE. Sin esperar (esperar=0) - sigue subiendo de fondo mientras la camara ya esta en otro sitio, que es como se comporta un globo de verdad.",
                propId = "PROP_Globo",
                deltaPosicion = new Vector3(1.5f, 16.0f, 0.8f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 5.0f,
                suavizar = true,
                esperar = false,
            },
            new ShotBeat
            {
                note = "Los dos mirandolo subir. El plano va detras del beat que lo mueve, no delante, para que cuando cortemos aqui el globo YA este en el aire.",
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
                    heightBias = 1.2f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
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
            new SetPropActiveBeat
            {
                note = "El globo se va.",
                propId = "PROP_Globo",
                active = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)13,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_GLOBO_OK",
                pageDuration = 2.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
        }),
        Fase("1c - El globo revienta", "", "manana_globo_ok", new List<SequenceBeat>
        {
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
        }),
        Fase("2 - La plaza", "", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "Ella se le acerca mientras el sigue a lo suyo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ParallelBeat
            {
                note = "La plaza sigue viva mientras ellos hablan - la conversacion pasa dentro de un pueblo, no en un vacio.",
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
                        actorId = "NPC_Aldeano_06",
                        gesture = "HandClap01",
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
                note = "Liora va hacia el.",
                actorId = "NPC_Liora",
                towardsActorId = "",
                markName = "M_Plaza_Liora",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.5f,
                timeout = 12.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_Liora",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new FaceBeat
            {
                note = "",
                actorId = "NPC_Liora",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new ShotBeat
            {
                note = "Los dos de pie, juntos, sin nada en medio.",
                shotName = "",
                smooth = true,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.25f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)8,
                revertAfter = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_1",
                pageDuration = 2.8f,
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
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
                pageDuration = 2.6f,
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
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_2",
                pageDuration = 3.0f,
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
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "Los dos riendose. Es el ultimo momento tranquilo del prologo y nadie lo sabe.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
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
        }),
        Fase("3 - Algo cambia en el cielo", "", "", new List<SequenceBeat>
        {
            new MusicBeat
            {
                note = "La musica se corta. Lo primero que nota el jugador de que algo pasa es el silencio, no una imagen.",
                musicId = "",
                fadeOut = 1.5f,
            },
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
                note = "La plaza entera, con la gente y la montana al fondo. Todavia no pasa nada.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.4f,
                    distanceScale = 2.4f,
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
                note = "EL RAYO golpea la cresta. Todavia no hay nadie ahi arriba - primero el susto, despues la figura.",
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                duration = 0.25f,
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
                note = "La cara del Archimago mirando arriba. Detras, la gente girada hacia el mismo sitio.",
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
        }),
        Fase("4 - La llegada", "", "", new List<SequenceBeat>
        {
            new PlaceAtMarkBeat
            {
                note = "Y AHORA aparece, en el sitio al que ya esta mirando todo el pueblo. El orden importa - primero el rayo y el susto, despues la figura.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Cresta",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("e824247f4f364400b0475f062217a915"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.0f, 0.0f),
                lifetime = 3.0f,
                earlyDespawn = 0.0f,
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
            new ShotBeat
            {
                note = "Vineta 5 - la silueta recortada contra las nubes, desde abajo.",
                shotName = "",
                smooth = false,
                duration = 2.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -1.6f,
                    distanceScale = 1.8f,
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
            new WaitBeat
            {
                note = "",
                seconds = 1.2f,
                unscaled = true,
            },
            new PlaceAtMarkBeat
            {
                note = "Corte - ya esta mas abajo. Nadie le ha visto recorrer esos metros, y eso lo hace peor.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Ladera_01",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new ShotBeat
            {
                note = "Vineta 6 - en la ladera, en contrapicado. El que esta mas alto domina el cuadro.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -1.2f,
                    distanceScale = 1.6f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
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
                    secondaryId = "",
                    heightBias = -1.0f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WalkPathBeat
            {
                note = "Los ultimos 20 metros, andando de verdad. De oeste a este, es decir de frente a la camara. 1,6 m/s son unos trece segundos - es mucho plano para una sola cosa, que es justo lo que se quiere aqui.",
                actorId = "NPC_MagoOscuro",
                speed = 1.6f,
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
            new MusicBeat
            {
                note = "Y entonces entra su musica.",
                musicId = "MAGOOSCURO_REVEAL",
                fadeOut = 0.5f,
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
            new SfxBeat
            {
                note = "",
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
            new ShotBeat
            {
                note = "El Archimago mira a los vecinos.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
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
                textKey = "PROLOGO_HORA_ARCHIMAGO",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "Levanta el escudo.",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 1.2f,
                returnToNormalAfter = false,
            },
            new SetPropActiveBeat
            {
                note = "Vineta 8 - el escudo. Se queda LEVANTADO hasta el final del duelo - es lo que hace que el duelo tenga algo en juego.",
                propId = "PROP_Escudo",
                active = true,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_SpellRelease",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "Los diez corren al puente a la vez. Al ESTE, el lado contrario por el que ha bajado el Mago Oscuro - que la gente huya alejandose de el es lo que hace legible donde esta el peligro.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        towardsActorId = "",
                        markName = "M_Puente_01",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        towardsActorId = "",
                        markName = "M_Puente_02",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        towardsActorId = "",
                        markName = "M_Puente_03",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        towardsActorId = "",
                        markName = "M_Puente_04",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        towardsActorId = "",
                        markName = "M_Puente_05",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        towardsActorId = "",
                        markName = "M_Puente_06",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        towardsActorId = "",
                        markName = "M_Puente_07",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        towardsActorId = "",
                        markName = "M_Puente_08",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        towardsActorId = "",
                        markName = "M_Puente_09",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new MoveToBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        towardsActorId = "",
                        markName = "M_Puente_10",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 12.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                },
            },
            new ShotBeat
            {
                note = "La plaza vaciandose, con el escudo aguantando.",
                shotName = "",
                smooth = false,
                duration = 3.0f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "PROP_Escudo",
                    heightBias = 1.2f,
                    distanceScale = 1.8f,
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
        }),
        Fase("6 - Solo quedan ellos dos", "", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "La villa casi vacia y ardiendo detras de ellos.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
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
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA_1",
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
                note = "",
                shotName = "",
                smooth = false,
                duration = 1.8f,
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
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO_1",
                pageDuration = 3.6f,
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
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
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
                note = "",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.OverTheShoulder,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
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
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO",
                pageDuration = 3.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)2,
                revertAfter = 0.0f,
            },
            new ShotBeat
            {
                note = "El abrazo.",
                shotName = "",
                smooth = true,
                duration = 2.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 0.85f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA",
                pageDuration = 4.4f,
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
                seconds = 0.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Ella, antes de irse.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SayBeat
            {
                note = "La frase que ata el prologo con el arco de Will entero. Rima con su propia linea de la carta - no es lo mismo que quererlo -> deja que elija.",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ELEGIR",
                pageDuration = 4.6f,
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
                seconds = 1.0f,
                unscaled = true,
            },
            new MoveToBeat
            {
                note = "Se marcha hacia la colina.",
                actorId = "NPC_Liora",
                towardsActorId = "",
                markName = "M_Puente_01",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 2.6f,
                timeout = 10.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new ShotBeat
            {
                note = "El, solo, con el escudo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
        }),
        Fase("7 - El duelo", "", "", new List<SequenceBeat>
        {
            new SetActionAxisBeat
            {
                note = "La camara al norte, perpendicular a la linea que une a los dos - se les ve de perfil, encarados. Es la vineta 10.",
                sideDegrees = 0.0f,
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "M_Duelo2_Mago",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_MagoOscuro",
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "M_Duelo2_Oscuro",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_Archimago",
            },
            new MusicBeat
            {
                note = "La musica del climax arranca aqui y no para hasta el silencio del asalto 4.",
                musicId = "MAGOOSCURO_CLIMAX",
                fadeOut = 0.5f,
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
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
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
            new GestureBeat
            {
                note = "El levanta la guardia.",
                actorId = "NPC_Archimago",
                gesture = "Defend_NoWeapon",
                repeats = 1,
                holdSeconds = 0.25f,
                returnToNormalAfter = false,
            },
            new VfxBeat
            {
                note = "El escudo se enciende justo a tiempo.",
                vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.0f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
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
                note = "Aguanta, pero el golpe le echa para atras.",
                actorId = "NPC_Archimago",
                gesture = "DefendHit_NoWeapon",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = true,
            },
            new WaitBeat
            {
                note = "Fin del asalto 1.",
                seconds = 0.5f,
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
            new SfxBeat
            {
                note = "",
                eventKey = "Star_SpellCast",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("33fd9c47032845d6ba47d3b222d1ae88"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.3f, 0.0f),
                lifetime = 1.0f,
                earlyDespawn = 0.0f,
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
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("8b20002c2b69a9d44ab5bae8e1d923cd"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.2f, 0.0f),
                lifetime = 0.9f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Impact1",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
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
            new ShakeBeat
            {
                note = "",
                intensity = 0.3f,
                duration = 0.35f,
                waitForEnd = false,
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
                    type = ShotType.TwoShot,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
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
                seconds = 0.5f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Plano general otra vez - lo que viene hay que verlo entero.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.3f,
                    distanceScale = 1.6f,
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
                markName = "M_Duelo_Choque",
                offset = new Vector3(0.0f, 1.6f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "La columna de luz que sube del choque es lo mas cerca que estamos de la verticalidad que pide el guion - los dos personajes no pueden volar, pero lo que se lanzan si.",
                vfxPrefab = Prefab("6555300494081614fa6b6a823cca9f64"),
                atActorId = "",
                markName = "M_Duelo_Choque",
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
                seconds = 0.7f,
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
                note = "CORTE. Y el suelo se abre BAJO EL, no delante - por eso no hay guardia que valga.",
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
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "TakeDamage_2",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = true,
            },
            new MoveToBeat
            {
                note = "Sale despedido dos metros hacia atras.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Duelo_Caida",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 5.5f,
                timeout = 3.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
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
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Pain01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = true,
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
                note = "El avanza sobre el caido.",
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
                markName = "M_Duelo2_Mago",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.3f,
                timeout = 6.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
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
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "Y levanta la mano para el ultimo.",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicSpecial",
                repeats = 1,
                holdSeconds = 0.4f,
                returnToNormalAfter = false,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("3dd50886582244645be87adb42aa8528"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.6f, 0.0f),
                lifetime = 2.6f,
                earlyDespawn = 0.0f,
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
            new MusicBeat
            {
                note = "SILENCIO. Se corta la musica entera - lo que viene no compite con nada.",
                musicId = "",
                fadeOut = 0.6f,
            },
            new TimeScaleBeat
            {
                note = "Camara lenta desde aqui hasta el final de la fase.",
                timeScale = 0.55f,
                rampDuration = 0.4f,
            },
            new ShotBeat
            {
                note = "El, en el suelo.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new FaceBeat
            {
                note = "Y no mira al Mago Oscuro - mira al puente, que es donde esta Liora.",
                actorId = "NPC_Archimago",
                targetActorId = "",
                markName = "M_Puente_01",
                lookAway = false,
                mutual = false,
                turnDuration = 0.7f,
            },
            new SayBeat
            {
                note = "Dicho para el, no para el otro.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DUELO_ARCHIMAGO_2",
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
                seconds = 0.6f,
                unscaled = true,
            },
            new TimeScaleBeat
            {
                note = "",
                timeScale = 1.0f,
                rampDuration = 0.5f,
            },
            new PlaceAtMarkBeat
            {
                note = "Se pone en pie y da el paso al frente - y vuelve a su sitio del duelo, que es donde tiene que estar para el hechizo y para la puerta.",
                actorId = "NPC_Archimago",
                markName = "M_Duelo2_Mago",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_MagoOscuro",
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
            new ShotBeat
            {
                note = "De pie otra vez, y ahora la camara esta mas baja que el.",
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
                playGestures = true,
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
        Fase("8 - Proteccion Absoluta", "", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "Mira hacia la colina, donde Liora y los vecinos ya estan a salvo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = -0.5f,
                    distanceScale = 1.1f,
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
                textKey = "PROLOGO_HECHIZO",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new TimeScaleBeat
            {
                note = "Camara lenta SOLO para el ultimo gesto.",
                timeScale = 0.5f,
                rampDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "El ultimo gesto.",
                actorId = "NPC_Archimago",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 1.0f,
                returnToNormalAfter = false,
            },
            ConModo(new InputPromptBeat
            {
                note = "El unico input de todo el prologo, en el pico emocional - soltar hacia arriba.",
                actionRef = Accion("GamePlay", "Interact"),
                directionActionRef = Accion("GamePlay", "Move"),
                pressesRequired = 8,
                windowSeconds = 2.5f,
                holdSeconds = 1.5f,
                maxHoldSeconds = 2.5f,
                expectedDirection = new Vector2(0.0f, 1.0f),
                directionAngleTolerance = 60.0f,
                directionHoldSeconds = 0.15f,
                iconGlyphName = "",
                successFlag = "duelo_redirige",
                tolerant = true,
            }, (PanicInputMode)4),
            new VfxBeat
            {
                note = "La luz nace en sus manos y cubre el valle.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 3.0f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("6555300494081614fa6b6a823cca9f64"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.6f,
                earlyDespawn = 0.0f,
            },
            new ScreenFlashBeat
            {
                note = "La esfera negra y el escudo chocan.",
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                duration = 0.35f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_Explosion",
                clip = null,
                atActorId = "NPC_Archimago",
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
                note = "",
                intensity = 0.6f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new TimeScaleBeat
            {
                note = "",
                timeScale = 1.0f,
                rampDuration = 0.5f,
            },
        }),
        Fase("9 - El Sendero y el corte a blanco", "", "", new List<SequenceBeat>
        {
            new SetPropActiveBeat
            {
                note = "Vineta 9 - entre el resplandor se abre una puerta de estrellas.",
                propId = "PROP_PuertaSendero",
                active = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Teleport_Start",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ShotBeat
            {
                note = "La puerta.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "PROP_PuertaSendero",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new VfxBeat
            {
                note = "La sombra es arrastrada hacia ella.",
                vfxPrefab = Prefab("57345e42a416405eaaeaa31e48cd8ee0"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.0f, 0.0f),
                lifetime = 2.5f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Teleport_End",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.35f,
                duration = 0.6f,
                waitForEnd = false,
            },
            new SetPropActiveBeat
            {
                note = "La puerta se cierra.",
                propId = "PROP_PuertaSendero",
                active = false,
            },
            new ShotBeat
            {
                note = "El escudo sigue en pie. El cae.",
                shotName = "",
                smooth = false,
                duration = 2.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.6f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)2,
                revertAfter = 0.0f,
            },
            new GestureBeat
            {
                note = "Se arrodilla - es la vineta 9 del storyboard, y es lo ultimo que hace.",
                actorId = "NPC_Archimago",
                gesture = "Reverence01",
                repeats = 1,
                holdSeconds = 1.6f,
                returnToNormalAfter = true,
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.2f,
                unscaled = true,
            },
            new ScreenFadeBeat
            {
                note = "A BLANCO, no a negro - ese blanco se convierte en la luz de la manana entrando por la ventana de Will.",
                fadeIn = true,
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                duration = 1.6f,
                waitForEnd = true,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
        }),
    };
}
#endif
