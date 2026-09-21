using System.Collections;
using UnityEngine;
using Game.NPC;
using Game.NPC.States;
using Game.NPC.Common;
using Sendero.Core.Feedback;

/// Secuencia "Oliver saluda a Will al salir de casa" — sustituye al infodump forzado de
/// DLG_OLIVER_MENUS_* por una escenita de personaje: Oliver ve a Will, corre hacia él sin aliento,
/// enseña un hechizo nuevo que le sale mal, Will le corta con prisa, y Oliver encadena con la
/// explicación de menús ya existente (mismas líneas DLG_OLIVER_MENUS_01 a 08, sin tocar el
/// doblaje ya grabado) — esta parte final se reproduce con el componente de diálogo normal
/// (DialogueManager), no con burbujas paginadas por tiempo, para que el jugador pueda leer con
/// calma y avanzar cuando quiera.
///
/// Ver propuesta-secuencia-oliver-menus-amigo-recurrente-2026-09-12.md e
/// incidencia-oliver-saludo-secuencia-en-prefab-trigger-prematuro-2026-09-15.md para el diseño e
/// historial completo.
///
/// PENDIENTE DE EDITOR (no se puede resolver desde código):
///   - Este componente debe vivir en su propio GameObject de escena (p. ej. "OliverSaludoSequencer"),
///     NO en el prefab de Oliver — ver incidencia-oliver-saludo-secuencia-en-prefab-trigger-prematuro-2026-09-15.md.
///   - Asignar en el Inspector: _willActor, _oliverActor, _oliverAnimator, _oliverAgent,
///     _oliverManager (el NPCBehaviourManagerV2 de Oliver — necesario para que no se ponga a andar
///     por su cuenta durante la secuencia), _menusDialogue (Assets/_DIALOGUES/DIALOGUE NPCS/Oliver
///     Menus/DG_Oliver_Menus.asset), _spellFailSmokeVfx/_spellFailSfx, y las animaciones
///     concretas (hay dos huecos sin confirmar: la de "sin aliento" y la de "lanzar hechizo").
///   - Cámara: asignar _cinematicCamera (heredado de CinematicSequencerBase) y colocar en la
///     escena los GameObjects vacíos que sirven de plano para cada _shotXxx (posición + rotación
///     de cámara), igual que en el resto de sequencers (ver p. ej. ReinoExitBanterSequencer). Los
///     que se dejen sin asignar simplemente no cortan y la cámara se queda en el plano anterior.
///   - _approachAngleAroundWill controla desde qué lado de Will se para Oliver (0° = justo
///     enfrente); ajustar en Play Mode hasta que la posición quede bien.
public class OliverSaludoSequencer : CinematicSequencerBase
{
    [Header("Actores")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _oliverActor;
    [SerializeField] private NPCSimpleAnimator _oliverAnimator;
    [SerializeField] private UnityEngine.AI.NavMeshAgent _oliverAgent;
    [Tooltip("NPCBehaviourManagerV2 de Oliver. Mientras dura la secuencia se le fuerza a CinematicState " +
             "(mismo mecanismo que usa MoveToPosition/LeadPlayerToAnchor) para que su comportamiento " +
             "ambiental (Wander/Idle) no compita con el movimiento/animación que esta secuencia controla " +
             "a mano — sin esto, Oliver puede ponerse a andar por su cuenta a mitad de la escena. " +
             "Se restaura automáticamente al terminar la secuencia (o al saltarla).")]
    [SerializeField] private NPCBehaviourManagerV2 _oliverManager;

    [Header("Movimiento")]
    [Tooltip("Distancia a la que Oliver se para respecto a Will, en el punto calculado según Approach Angle Around Will.")]
    [SerializeField] private float _stopDistanceFromWill = 1.8f;
    [Tooltip("Ángulo (grados) alrededor de Will, medido desde la dirección a la que mira Will, donde debe " +
             "pararse Oliver. 0 = justo enfrente de Will (encarados, mirándose). 90 = a la derecha de Will " +
             "(desde el punto de vista de Will). -90 = a su izquierda. 180 = a su espalda. Ajustar en Play " +
             "Mode hasta que la posición quede natural.")]
    [SerializeField] private float _approachAngleAroundWill = 0f;
    [Tooltip("Velocidad de sprint de Oliver durante la aproximación (más alta que su paseo normal).")]
    [SerializeField] private float _sprintSpeed = 5.5f;
    [Tooltip("Timeout de seguridad por si el NavMeshAgent no llega nunca (NPC bloqueado, etc.) — mismo patrón que MoveToPositionSequence.")]
    [SerializeField] private float _approachTimeout = 6f;
    [Tooltip("FIX 15 sep 2026 (Raúl: \"Oliver se queda andando y habla a la vez\"). Pequeña pausa " +
             "justo al llegar junto a Will, antes de la siguiente animación/bocadillo. Sin ella, el " +
             "crossfade de Co_RunTo() hacia Idle (0.2s) y el crossfade del siguiente gesto/diálogo " +
             "(interactState + el gesto de \"sin aliento\") se piden casi en el mismo frame — el " +
             "Animator nunca llega a asentarse en Idle antes de que arranque la siguiente transición, " +
             "y se ve como si Oliver siguiera caminando mientras ya está hablando. Ajustable por si " +
             "hace falta más o menos margen.")]
    [SerializeField] private float _arrivalSettleDelay = 0.3f;
    [Tooltip("FIX 16 sep 2026 (Raúl: \"ahora se pone a andar al final de la secuencia\"/" +
        "\"en esta prueba solo se ha puesto a andar al final de la secuencia\"). Causa raíz " +
        "real (confirmada leyendo Assets/_NPCs/Ambient/NPC_Ambient_Config.asset, el Ambient " +
        "Config que usa Oliver): en cuanto _oliverHold.Finish() devuelve a Oliver el control de " +
        "su comportamiento ambiental, IdleState sortea un _idleDuration de entre minIdleTime " +
        "(1.2s) y maxIdleTime (3s) antes de pasar a WanderState — es decir, Oliver se echa a " +
        "andar por su cuenta 1-3 segundos después de terminar esta escena tan personal, lo cual " +
        "queda raro aunque sea \"comportamiento normal\" de un NPC ambiente cualquiera. No es " +
        "un bug de animación/NavMeshAgent (ya descartado con HardStop()+TransitionToIdle() en " +
        "el cierre — Oliver ya estaba bien parado ahí, el problema es que la FSM lo suelta " +
        "demasiado pronto). Este margen mantiene a Oliver retenido (todavía en CinematicState, " +
        "quieto) unos segundos más después de Co_EndCinematicWithTransition, antes de soltarle " +
        "el control de verdad -- para que no salga caminando nada más terminar la conversación.")]
    [SerializeField] private float _idleGraceAfterSequence = 3.5f;

    [Header("Animaciones — confirmar en Animation Preview antes de dar por bueno")]
    [Tooltip("Estado de Animator para el saltito de alegría + saludo (\"¡Will!!!!\"). Candidato sin confirmar: Cheer01/Cheer02 o HandWave01/02 — ver catalogo-animaciones-invector.md.")]
    [SerializeField] private string _greetGesture = "Cheer01";
    [Tooltip("Estado de Animator para \"ay que me ahogo\" tras el sprint. SIN CONFIRMAR — no hay ningún estado catalogado literalmente como 'sin aliento/ahogarse'; candidato provisional: Dizzy_NoWeapon (aturdido). Verificar en Animation Preview antes de dar por bueno, y revisar si existe algo más específico en el Animator Controller compartido de NPCs.")]
    [SerializeField] private string _outOfBreathGesture = "Dizzy_NoWeapon";
    [Tooltip("Estado de Animator para lanzar el hechizo (da igual cuál, es un gag fallido). Cualquiera de los estados de casteo ya catalogados sirve.")]
    [SerializeField] private string _castSpellGesture = "Attack2";
    [Tooltip("Estado de Animator de Will para la risa/burla cariñosa.")]
    [SerializeField] private string _willLaughGesture = "Cheer01";

    [Header("Gag del hechizo fallido")]
    [Tooltip("FIX 15 sep 2026 (Raúl: \"el hechizo debe tener algo de delay, 0.5, para que se vea " +
             "como hace la animación primero\"). Espera entre que arranca el gesto de casteo " +
             "(_castSpellGesture) y que salta el humillo/SFX del fallo, para que se note el \"lanzo " +
             "el hechizo\" antes del \"y falla\".")]
    [SerializeField] private float _spellFailVfxDelay = 0.5f;
    [Tooltip("FIX 16 sep 2026 (Raúl: \"el vfx hay que destruirle 1 s antes porque se ve " +
        "como sigue en el suelo\"). VfxPoolService.Play() recibe como lifetime la duración " +
        "completa del ParticleSystem (main.duration) — visualmente el humo ya se ha disipado " +
        "bastante antes de que acabe esa duración total, así que se veía un rato \"pegado al " +
        "suelo\" (el resto/cola de la simulación) hasta que el pool lo recogía. Restado de " +
        "main.duration antes de pasarlo como lifetime, para devolverlo al pool antes.")]
    [SerializeField] private float _spellFailVfxEarlyDespawn = 1f;
    [SerializeField] private ParticleSystem _spellFailSmokeVfx;
    [SerializeField] private AudioClip _spellFailSfx;

    [Header("Diálogo")]
    [SerializeField] private float _pageDuration = 2.6f;
    [Tooltip("DialogueAsset con las líneas DLG_OLIVER_MENUS_01..08 (Assets/_DIALOGUES/DIALOGUE NPCS/" +
             "Oliver Menus/DG_Oliver_Menus.asset). Se reproduce con el componente de diálogo normal " +
             "(DialogueManager), no con burbujas paginadas, para que el jugador pueda leer con calma y " +
             "avanzar cuando quiera — si se deja vacío, esta parte se salta con un aviso en consola.")]
    [SerializeField] private DialogueAsset _menusDialogue;

    [Header("Cámara — planos (dejar vacíos = sin corte, se queda en el plano anterior)")]
    [Tooltip("Plano inicial: Oliver al detectar a Will y saludar a distancia (saltito + grito). " +
             "Se corta a este plano durante el blackout de la transición de entrada.")]
    [SerializeField] private Transform _shotOliverSpot;
    [Tooltip("Plano opcional durante el sprint de Oliver hacia Will (plano general/dos personajes).")]
    [SerializeField] private Transform _shotSprintWide;
    [Tooltip("Primer plano de Oliver — se usa para \"sin aliento\", el anuncio del hechizo nuevo y " +
             "el enganche final hacia la explicación de menús.")]
    [SerializeField] private Transform _shotOliverClose;
    [Tooltip("Plano opcional más abierto para el gag del hechizo fallido, de forma que se vea bien " +
             "el humillo. Dejar vacío para quedarse en _shotOliverClose.")]
    [SerializeField] private Transform _shotSpellFailWide;
    [Tooltip("Primer plano de Will para su línea de corte (\"tú siempre igual, Oliver...\").")]
    [SerializeField] private Transform _shotWillClose;
    [Tooltip("Plano general de los dos antes de entrar en la explicación de menús (diálogo normal) — " +
             "evita quedarse en un primer plano cerrado durante un diálogo de avance manual.")]
    [SerializeField] private Transform _shotTwoShot;

    [Header("Skip")]
    [Tooltip("Duración del fundido que retira el negro tras saltar la secuencia con el botón global de skip (ver OnSkipCleanup).")]
    [SerializeField] private float _skipRevealDuration = 0.25f;

    /// Secuencia "vacía" que solo sirve para mantener a Oliver en CinematicState (y por tanto con su
    /// comportamiento ambiental/Wander desactivado — ver NPCBehaviourManagerV2.StartCinematicSequence
    /// y CinematicState) durante TODA esta escena. Este propio sequencer mueve/anima a Oliver a mano
    /// (Co_RunTo, PlaySocialGesture...); esta clase no hace nada cada frame, solo existe para que la
    /// FSM de Oliver sepa que está "ocupado" y no vuelva a Idle/Wander por su cuenta hasta que
    /// Finish() la marque completada.
    private class HoldCinematicSequence : CinematicSequence
    {
        public override void Update(NPCStateContext context) { }
        public void Finish() => IsCompleted = true;
    }

    private HoldCinematicSequence _oliverHold;

    protected override IEnumerator Co_Sequence()
    {
        // Anular el comportamiento ambiental de Oliver ANTES de nada — si no, puede ponerse a
        // andar por su cuenta (Wander) mientras esta secuencia lo mueve/anima a mano. Se restaura
        // al llegar al final natural, si se salta la cinemática (OnSkipCleanup()), o — FIX 15 sep
        // 2026 (Raúl: "Oliver se queda caminando") — si esta corrutina revienta a mitad (excepción
        // no controlada / referencia nula puntual). Antes el candado (_oliverHold) solo se liberaba
        // al llegar al final feliz de este método o desde OnSkipCleanup(); un fallo en cualquier
        // punto intermedio (p.ej. a media Co_RunTo, con el NavMeshAgent desactivado y la animación
        // de sprint congelada) dejaba a Oliver atascado en CinematicState para siempre — su FSM
        // nunca volvía a Idle/Wander. Mismo patrón que ya usa CinematicSequencerBase.Co_SequenceGuarded
        // para el HUD/ActionMode (ver comentario FIX INC-052 ahí), aplicado aquí al candado propio
        // de este sequencer, que esa protección genérica no cubre.
        _oliverHold = new HoldCinematicSequence();
        _oliverManager?.StartCinematicSequence(_oliverHold);

        try
        {
            yield return Co_SequenceBody();
        }
        finally
        {
            // Si el fallo llegó a mitad del sprint (Co_RunTo), el agente puede haberse quedado
            // desactivado — mismo re-enable que ya hace OnSkipCleanup() para el camino de skip.
            if (_oliverAgent != null && !_oliverAgent.enabled && _oliverActor != null)
                NavMeshAgentUtility.SafeEnable(_oliverAgent, _oliverActor, _oliverActor.position);

            _oliverHold?.Finish();
            _oliverHold = null;
        }
    }

    private IEnumerator Co_SequenceBody()
    {
        // FIX 15 sep 2026 (Raúl, tras recompilar y volver a probar: los 4 síntomas seguían
        // igual pese a los fixes anteriores de VFX y del candado) — diagnóstico en vez de
        // adivinar más a ciegas: por lectura estática de OliverSaludoSequencer.cs,
        // CinematicSequencerBase.cs, CameraDirectorService.cs, SpeechBubbleUI.cs y
        // vThirdPersonCamera.cs no aparece ninguna causa de código para "las cámaras no
        // funcionan" ni para "el primer mensaje sale en Will" — el bocadillo usa siempre el
        // Transform que se le pasa (_oliverActor en el beat 1) y _cinematicCamera.Cut() solo
        // puede no hacer nada si _cinematicCamera está sin asignar o si Camera.main devuelve
        // null en ese instante (falla en silencio, sin excepción, sin log). Este aviso deja
        // constancia en consola de con qué Transforms/referencias arranca realmente la
        // secuencia en esta sesión de Play, para que el próximo log de consola confirme o
        // descarte de una vez si _oliverActor/_willActor están intercambiados o si
        // _cinematicCamera llega null.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[OliverSaludoSequencer:{name}] Arranca secuencia — " +
            $"_willActor={(_willActor != null ? _willActor.name : "NULL")}, " +
            $"_oliverActor={(_oliverActor != null ? _oliverActor.name : "NULL")}, " +
            $"_oliverManager={(_oliverManager != null ? "asignado" : "NULL")}, " +
            $"_cinematicCamera={(_cinematicCamera != null ? "asignado" : "NULL")}, " +
            $"_shotOliverSpot={(_shotOliverSpot != null ? _shotOliverSpot.name : "sin asignar")}.");
        if (_willActor != null && _oliverActor != null && _willActor == _oliverActor)
            Debug.LogWarning($"[OliverSaludoSequencer:{name}] _willActor y _oliverActor apuntan " +
                "al MISMO Transform — revisa el Inspector, están intercambiados o duplicados.");
        if (_cinematicCamera == null)
            Debug.LogWarning($"[OliverSaludoSequencer:{name}] _cinematicCamera sin asignar — " +
                "ningún corte de cámara de esta secuencia va a hacer nada.");
#endif
        yield return Co_BeginCinematicWithTransition(_shotOliverSpot);

        // FIX 15 sep 2026 (Raúl: "he añadido este id a la secuencia de oliver y no ha sonado
        // OLIVER_1") -- _sequenceMusicId/_audioProfile son campos heredados de
        // CinematicSequencerBase que BeginCinematic() usa para calcular MusicRule, pero NINGÚN
        // sequencer reproduce esa música solo: hace falta llamar a PlaySequenceMusic() a mano,
        // igual que EstelaAppearsSequencer/TabernaSequencer/etc. Este sequencer nunca la llamaba,
        // así que por mucho ID que se pusiera en el Inspector jamás iba a sonar nada.
        PlaySequenceMusic();

        FaceTarget(_oliverActor, _willActor);

        // 1) Oliver detecta a Will: saltito + saludo a distancia, antes de arrancar el sprint.
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_SPOT_WILL"), _pageDuration, _greetGesture);

        // 2) Sprint hacia Will — se para en el punto calculado alrededor de Will (distancia +
        // ángulo configurables), no donde le pille según desde qué dirección venga corriendo.
        if (_shotSprintWide != null) _cinematicCamera?.Cut(_shotSprintWide);
        Vector3 approachDir = Quaternion.AngleAxis(_approachAngleAroundWill, Vector3.up) * _willActor.forward;
        Vector3 stopPoint = _willActor.position + approachDir * _stopDistanceFromWill;
        yield return Co_RunTo(_oliverActor, _oliverAgent, _oliverAnimator, stopPoint, 0f, _sprintSpeed, _approachTimeout);

        // Los dos deben quedar mirándose — antes solo giraba Oliver hacia Will.
        FaceTarget(_oliverActor, _willActor);
        FaceTarget(_willActor, _oliverActor);

        // FIX 15 sep 2026 (Raúl: "Oliver se queda andando y ves como esta diciendo el texto y
        // habla a la vez") — ver comentario de _arrivalSettleDelay más arriba: deja que el
        // crossfade a Idle de Co_RunTo() se asiente antes de encadenar el siguiente gesto.
        if (_arrivalSettleDelay > 0f) yield return new WaitForSeconds(_arrivalSettleDelay);

        if (_shotOliverClose != null) _cinematicCamera?.Cut(_shotOliverClose);

        // 3) Sin aliento — "ay que me ahogo".
        // FIX 15 sep 2026 (Raúl: "cuando hace la animacion de cansado se queda asi, y debe
        // hacerlo solo para esa frase luego la de hablar"): antes se llamaba PlaySocialGesture()
        // a mano ANTES de ShowBubblePaged(), sin pasarle animTrigger — así ShowBubblePaged()
        // nunca sabía que ese gesto pertenecía a esta página, y su propio mecanismo de "gesto
        // específico una vez, luego variaciones de hablar genéricas" (PickTalkGesture, ver más
        // abajo en CinematicSequencerBase.cs) nunca entraba en juego. Igual que el beat 1
        // (_greetGesture pasado como animTrigger), se pasa aquí _outOfBreathGesture como
        // animTrigger: se reproduce una vez para esta frase y, si la burbuja dura más que el
        // gesto, cae a los genéricos de "hablando" en vez de quedarse congelado en la pose de
        // aturdido para la frase siguiente.
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_OUT_OF_BREATH"), _pageDuration, _outOfBreathGesture);

        // 4) Se recupera, contento: "he aprendido un hechizo nuevo, mira:"
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_NEW_SPELL_INTRO"), _pageDuration);

        if (_shotSpellFailWide != null) _cinematicCamera?.Cut(_shotSpellFailWide);

        // 5) Lanza el hechizo — falla a propósito (gag): humillo + SFX gracioso.
        _oliverAnimator?.PlaySocialGesture(_castSpellGesture);
        // FIX 15 sep 2026 (Raúl: "el hechizo debe tener algo de delay, 0.5, para que se vea como
        // hace la animacion primero"): antes el humillo/SFX saltaban en el mismo instante que el
        // gesto de casteo, sin darle tiempo a leerse — ahora se espera _spellFailVfxDelay antes
        // de disparar el VFX/SFX, para que se note primero el "lanza el hechizo" y luego el fallo.
        if (_spellFailVfxDelay > 0f) yield return new WaitForSeconds(_spellFailVfxDelay);
        // FIX 15 sep 2026 (Raúl: "no sale el vfx de hechizo fallido"): _spellFailSmokeVfx apunta al
        // ParticleSystem del PREFAB de FX_Smoke_Small (ver propuesta-secuencia-oliver-menus-amigo-
        // recurrente-2026-09-12.md), no a una instancia ya colocada en la escena. Llamar Play()
        // directamente sobre el componente de un asset de prefab no reproduce nada visible (no está
        // instanciado en ninguna escena) — y además viola la regla no negociable de CLAUDE.md § 2
        // ("VFX de un solo uso: siempre VfxPoolService.Instance.Play(prefab, pos, rot, lifetime),
        // nunca Instantiate/Play directo"). Se sustituye por el pool, igual que el resto del
        // proyecto — VfxPoolService clona el GameObject del prefab, así que sigue funcionando
        // aunque el campo siga apuntando al asset (no hace falta reasignarlo).
        if (_spellFailSmokeVfx != null && VfxPoolService.Instance != null)
        {
            // FIX 16 sep 2026 (ver _spellFailVfxEarlyDespawn más arriba): se resta el margen
            // configurable a la duración real del ParticleSystem antes de pasarla como lifetime,
            // con un mínimo de 0.1s para no devolverlo al pool antes de que llegue a reproducirse.
            float vfxLifetime = Mathf.Max(0.1f, _spellFailSmokeVfx.main.duration - _spellFailVfxEarlyDespawn);
            VfxPoolService.Instance.Play(_spellFailSmokeVfx.gameObject, _oliverActor.position,
                _oliverActor.rotation, vfxLifetime);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            // FIX 15 sep 2026 (Raúl, tras recompilar: "sigue sin salir el vfx" pese al fix del
            // pool aplicado antes) — el guard de arriba fallaba en silencio, sin ningún aviso,
            // así que no había forma de saber desde consola si la causa era _spellFailSmokeVfx
            // sin asignar en el Inspector o VfxPoolService.Instance sin inicializar en esta
            // escena. Ahora queda constancia explícita de cuál de los dos es.
            if (_spellFailSmokeVfx == null)
                Debug.LogWarning($"[OliverSaludoSequencer:{name}] _spellFailSmokeVfx sin asignar — no hay VFX de humo que reproducir.");
            else
                Debug.LogWarning($"[OliverSaludoSequencer:{name}] VfxPoolService.Instance es null — el pool de VFX no está listo en esta escena.");
        }
#endif
        if (_spellFailSfx != null && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFXAt(_spellFailSfx, _oliverActor.position);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (_spellFailSfx == null)
        {
            Debug.LogWarning($"[OliverSaludoSequencer:{name}] _spellFailSfx sin asignar — no hay SFX del gag que reproducir.");
        }
#endif
        yield return new WaitForSeconds(1.2f);

        if (_shotWillClose != null) _cinematicCamera?.Cut(_shotWillClose);

        // 6) Will se ríe y corta con prisa.
        _oliverAnimator?.SetTalking(false);
        yield return ShowBubblePaged(_willActor, Loc("WILL_GREETING_OLIVER_CUT_OFF"), _pageDuration, _willLaughGesture);

        if (_shotOliverClose != null) _cinematicCamera?.Cut(_shotOliverClose);

        // 7) Oliver engancha con "por si te has olvidado que te conozco..."
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_BEFORE_MENUS_HANDOFF"), _pageDuration, _greetGesture);

        // 8) Explicación de menús YA EXISTENTE (mismas claves DLG_OLIVER_MENUS_01..08, doblaje
        // reutilizado) — con el componente de diálogo normal (avance manual), no burbuja paginada
        // por tiempo: es contenido que enseña a jugar, el jugador debe poder leerlo con calma.
        if (_shotTwoShot != null) _cinematicCamera?.Cut(_shotTwoShot);
        if (_menusDialogue != null)
        {
            bool menusDialogueFinished = false;
            DialogueManager.Instance.StartDialogue(_menusDialogue, _oliverActor, () => menusDialogueFinished = true, isSequenceDialogue: true);
            yield return new WaitUntil(() => menusDialogueFinished);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning($"[OliverSaludoSequencer:{name}] _menusDialogue sin asignar — se salta la explicación de menús. Asigna Assets/_DIALOGUES/DIALOGUE NPCS/Oliver Menus/DG_Oliver_Menus.asset en el Inspector.");
        }
#endif

        // FIX 15 sep 2026 (Raúl: "ahora oliver se queda caminando al acabar la secuencia") --
        // Co_RunTo() ya deja a Oliver en Idle (SetMovementSpeed(0f) + TransitionToIdle()) justo
        // después del sprint, pero entre ahí y aquí el NavMeshAgent pasa por disable -> Warp() ->
        // enable dentro del propio Co_RunTo, y CinematicState.OnEnter() solo hace su HardStop()
        // (isStopped=true, ResetPath(), velocity=0) UNA VEZ, al arrancar toda la cinemática --
        // antes de que Co_RunTo tocase el agente. Si Unity resetea isStopped al reactivar el
        // componente (o si algo deja hasPath/velocity residual), CinematicState.OnExit() (que
        // solo desactiva AllowManualMovement, no vuelve a pararlo) puede dejar que
        // SyncWithNavMeshAgent() herede ese residuo en cuanto la FSM recupera el control al
        // terminar -- justo el momento en que Raúl ve a Oliver "seguir caminando". Se aplica
        // aqui el mismo HardStop() que ya usa NPCStateBase.StopMovement()/CinematicState.OnEnter()
        // para dejar al agente parado sin path ni velocidad residual, mas un TransitionToIdle()
        // explícito en el animator, justo antes de devolver el control -- por si el diagnóstico
        // exacto no es este y el síntoma persiste, esto no debería empeorar nada (Oliver ya
        // debería estar quieto en este punto de todos modos).
        if (_oliverAgent != null)
            NavMeshAgentUtility.HardStop(_oliverAgent);
        _oliverAnimator?.SetMovementSpeed(0f);
        _oliverAnimator?.TransitionToIdle();

        // FIX 15 sep 2026 (mismo motivo que PlaySequenceMusic() más arriba): sin pasar
        // RestoreMusic() aquí, la música de OLIVER_1 (si llegaba a sonar) se quedaría sonando
        // para siempre en vez de devolver el control a la música de ambiente/zona anterior.
        yield return Co_EndCinematicWithTransition(RestoreMusic);

        // FIX 16 sep 2026 (ver _idleGraceAfterSequence más arriba): mantener a Oliver retenido
        // (_oliverHold sigue vivo, CinematicState sigue activa, ya quieto por el HardStop() +
        // TransitionToIdle() de justo arriba) unos segundos más, DESPUES de que la pantalla ya
        // ha vuelto al gameplay normal, para que no se le vea echarse a andar (WanderState) a
        // los 1-3 segundos de terminar la conversacion.
        if (_idleGraceAfterSequence > 0f) yield return new WaitForSeconds(_idleGraceAfterSequence);

        RaiseSignalOut();
    }

    /// Ver comentario de OnSkipCleanup en CinematicSequencerBase: StopCoroutine() (usado por
    /// RequestSkip) no garantiza que se ejecute el resto de Co_Sequence(), así que todo lo que este
    /// sequencer necesita limpiar SIEMPRE (aunque se salte a mitad) debe repetirse aquí a mano.
    protected override void OnSkipCleanup()
    {
        // Si el skip llega a mitad del sprint (Co_RunTo), el NavMeshAgent puede haberse quedado
        // desactivado (se apaga para controlar el Transform a mano durante el Lerp y se reactiva
        // al llegar) — sin este re-enable, Oliver se queda con el agente apagado para siempre.
        if (_oliverAgent != null && !_oliverAgent.enabled && _oliverActor != null)
            NavMeshAgentUtility.SafeEnable(_oliverAgent, _oliverActor, _oliverActor.position);

        // FIX 15 sep 2026 (mismo motivo que el HardStop()+TransitionToIdle() añadido al cierre
        // normal de Co_SequenceBody, ver comentario ahi -- "Raul: ahora oliver se queda
        // caminando al acabar la secuencia"): mismo saneado aquí para el camino de skip, que
        // tiene el mismo hueco (el agente pudo quedar con isStopped/path residual tras pasar
        // por disable->Warp->enable en Co_RunTo si el skip llega después del sprint).
        if (_oliverAgent != null)
            NavMeshAgentUtility.HardStop(_oliverAgent);
        _oliverAnimator?.SetMovementSpeed(0f);
        _oliverAnimator?.TransitionToIdle();

        // Devolver a Oliver el control de su comportamiento ambiental.
        _oliverHold?.Finish();
        _oliverHold = null;

        // FIX (15 sep 2026 — misma auditoría que TabernaSequencer/EstelaAppearsSequencer/
        // ReinoExitBanterSequencer/LiamCrystalBallSequencer, 16 ago 2026): el cierre normal
        // (Co_EndCinematicWithTransition, al final de Co_SequenceBody) revela la pantalla él solo.
        // El cierre genérico de skip (RequestSkip -> Co_SkipToEnd -> Co_EndCinematicStayBlack)
        // deliberadamente NO revela — pensado para secuencias cuyo sistema siguiente gestiona su
        // propio reveal (p.ej. intro de un boss). Aquí no hay ningún sistema siguiente: el saludo
        // de Oliver es una rama en paralelo del grafo que termina en sí misma. Sin este fade
        // manual, saltar esta secuencia con el botón global de skip dejaba la pantalla en negro
        // para siempre (Raúl: "si salto la secuencia de oliver la pantalla se queda en negro").
        StartCoroutine(Co_RevealAfterSkip());
    }

    private IEnumerator Co_RevealAfterSkip()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, _skipRevealDuration, fadeIn: false);
    }

    /// Sprint simple hacia un punto, frenando a stopDistance del objetivo. Mismo espíridtu que
    /// Co_SimpleWalkTo (ver escena-wiring-localizacion-2026-08-30.md § 20.2): Lerp manual con
    /// orientación continua hacia la dirección real de avance, en vez de depender del NavMeshAgent
    /// para la parte visual — evita el "para, salta, se coloca mirando a un lado y gira" ya
    /// diagnosticado en el Mago Oscuro. PENDIENTE: verificar en el Editor que el blend de
    /// locomoción de Oliver realmente entra en carrera (no solo trote) a _sprintSpeed.
    private IEnumerator Co_RunTo(Transform actor, UnityEngine.AI.NavMeshAgent agent, NPCSimpleAnimator anim,
        Vector3 targetPos, float stopDistance, float sprintSpeed, float timeout)
    {
        Vector3 start = actor.position;
        Vector3 dirFull = targetPos - start;
        dirFull.y = 0f;
        float fullDistance = dirFull.magnitude;
        float travelDistance = Mathf.Max(0f, fullDistance - stopDistance);
        if (travelDistance <= 0.01f) yield break;

        Vector3 dirNorm = dirFull.normalized;
        Vector3 end = start + dirNorm * travelDistance;
        float duration = Mathf.Clamp(travelDistance / sprintSpeed, 0.1f, timeout);

        if (agent != null) agent.enabled = false; // control manual del transform durante el sprint, como Co_SimpleWalkTo
        // FIX 15 sep 2026 (Raúl: "Oliver está patinando", vuelve a pasar) — anim?.TransitionToLocomotion()
        // se llamaba cada frame DENTRO de este bucle. A diferencia de SetMovementSpeed() (que internamente
        // solo llama a TransitionToLocomotion() si el _currentState actual NO es ya Walking/Running),
        // TransitionToLocomotion() no comprueba nada: siempre llama CrossFadeToState(locomotionState,
        // locomotionBlendTime), y CrossFadeToState usa animator.CrossFadeInFixedTime(hash, transitionTime,
        // layer, fixedTime: 0f) — cada frame reinicia la animación de correr a su frame 0 y arranca un
        // blend nuevo. Resultado visual: el cuerpo se traslada suave por el Lerp mientras las piernas
        // nunca completan un ciclo de zancada, porque se resetean ~60 veces por segundo — exactamente
        // el aspecto de "patinar". Co_SimpleWalkTo (MagoOscuroFinalBattleSequencer.cs), el patrón que
        // este método dice seguir, solo llama TransitionToLocomotion() UNA VEZ, antes de entrar al
        // bucle — dentro del bucle solo llama SetMovementSpeed() en cada frame. Se iguala aquí ese
        // patrón: la llamada se mueve fuera del bucle.
        anim?.TransitionToLocomotion();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.position = Vector3.Lerp(start, end, t);
            anim?.SetMovementSpeed(sprintSpeed);
            yield return null;
        }
        actor.position = end;
        // FIX 15 sep 2026 (Raúl, tras recompilar: "Oliver se queda caminando" seguía pasando
        // pese al candado try/finally aplicado antes) — causa real encontrada comparando con
        // Co_SimpleWalkTo (MagoOscuroFinalBattleSequencer.cs), el helper hermano del que este
        // método dice explícitamente copiar el espíritu: allí, al terminar el tramo manual, se
        // llama SetMovementSpeed(0f) Y ADEMÁS TransitionToIdle() explícito. Aquí solo se hacía
        // lo primero. SetMovementSpeed(0f) únicamente amortigua (dampTime) el parámetro
        // InputMagnitude hacia 0 — NO cambia el _currentState interno de NPCSimpleAnimator, que
        // se queda en Walking/Running. Sin la transición explícita a Idle, Oliver se queda
        // "andando en el sitio" (el mismo bug que ResetMovement()/StopMovement() ya documentan
        // más arriba en NPCSimpleAnimator.cs como motivo de existir) hasta que algún gesto
        // posterior lo fuerce a otra cosa — de ahí el síntoma reportado.
        anim?.SetMovementSpeed(0f);
        anim?.TransitionToIdle();
        if (agent != null)
        {
            agent.Warp(actor.position);
            agent.enabled = true;
        }
    }
}
