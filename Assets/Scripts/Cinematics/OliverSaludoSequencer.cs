using System.Collections;
using UnityEngine;

/// Secuencia "Oliver saluda a Will al salir de casa" — sustituye al infodump forzado de
/// DLG_OLIVER_MENUS_* por una escenita de personaje: Oliver ve a Will, corre hacia él sin aliento,
/// enseña un hechizo nuevo que le sale mal, Will le corta con prisa, y Oliver encadena con la
/// explicación de menús ya existente (mismas líneas DLG_OLIVER_MENUS_01 a 08, sin tocar el
/// doblaje ya grabado).
///
/// Ver propuesta-secuencia-oliver-menus-amigo-recurrente-2026-09-12.md para el diseño completo
/// (por qué esta forma, qué queda pendiente de verificar en el Editor, qué animaciones/VFX/SFX
/// hacen falta todavía).
///
/// PENDIENTE DE EDITOR (no se puede resolver desde código):
///   - Colocar este componente en el prefab/instancia de Oliver de la escena del pueblo de Will.
///   - Crear el SignalEmitter (o reutilizar el trigger que hoy dispara DLG_OLIVER_MENUS) que
///     levanta la señal de entrada de este sequencer al salir de la casa.
///   - En el grafo narrativo: sustituir el nodo que hoy dispara directamente DLG_OLIVER_MENUS por
///     un WaitCustomEventNode(_signalIn) → PlayCinematicNode(este cinematicId) → (el grafo sigue
///     igual que antes tras _signalOut, ya que el propio sequencer reproduce las líneas de menú al
///     final).
///   - Asignar en el Inspector: _willActor, _oliverActor, _oliverAnimator, _oliverAgent,
///     _spellFailVfx (humillo), _spellFailSfx (gracioso), _cinematicCamera si se quiere encuadre
///     propio, y las animaciones concretas (ver comentarios junto a cada campo — hay dos huecos
///     sin confirmar: la de "sin aliento" y la de "lanzar hechizo").
public class OliverSaludoSequencer : CinematicSequencerBase
{
    [Header("Actores")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _oliverActor;
    [SerializeField] private NPCSimpleAnimator _oliverAnimator;
    [SerializeField] private UnityEngine.AI.NavMeshAgent _oliverAgent;

    [Header("Movimiento")]
    [Tooltip("Distancia a la que Oliver frena su sprint hacia Will, antes de la animación de sin aliento.")]
    [SerializeField] private float _stopDistanceFromWill = 1.8f;
    [Tooltip("Velocidad de sprint de Oliver durante la aproximación (más alta que su paseo normal).")]
    [SerializeField] private float _sprintSpeed = 5.5f;
    [Tooltip("Timeout de seguridad por si el NavMeshAgent no llega nunca (NPC bloqueado, etc.) — mismo patrón que MoveToPositionSequence.")]
    [SerializeField] private float _approachTimeout = 6f;

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
    [SerializeField] private ParticleSystem _spellFailSmokeVfx;
    [SerializeField] private AudioClip _spellFailSfx;

    [Header("Diálogo")]
    [SerializeField] private float _pageDuration = 2.6f;

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition();

        FaceTarget(_oliverActor, _willActor);

        // 1) Oliver detecta a Will: saltito + saludo a distancia, antes de arrancar el sprint.
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_SPOT_WILL"), _pageDuration, _greetGesture);

        // 2) Sprint hacia Will.
        yield return Co_RunTo(_oliverActor, _oliverAgent, _oliverAnimator, _willActor.position, _stopDistanceFromWill, _sprintSpeed, _approachTimeout);
        FaceTarget(_oliverActor, _willActor);

        // 3) Sin aliento — "ay que me ahogo".
        _oliverAnimator?.PlaySocialGesture(_outOfBreathGesture);
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_OUT_OF_BREATH"), _pageDuration);

        // 4) Se recupera, contento: "he aprendido un hechizo nuevo, mira:"
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_NEW_SPELL_INTRO"), _pageDuration);

        // 5) Lanza el hechizo — falla a propósito (gag): humillo + SFX gracioso.
        _oliverAnimator?.PlaySocialGesture(_castSpellGesture);
        if (_spellFailSmokeVfx != null) _spellFailSmokeVfx.Play();
        if (_spellFailSfx != null && AudioService.Instance != null)
            AudioService.Instance.PlaySFXAt(_spellFailSfx, _oliverActor.position);
        yield return new WaitForSeconds(1.2f);

        // 6) Will se ríe y corta con prisa.
        _oliverAnimator?.SetTalking(false);
        yield return ShowBubblePaged(_willActor, Loc("WILL_GREETING_OLIVER_CUT_OFF"), _pageDuration, _willLaughGesture);

        // 7) Oliver engancha con "por si te has olvidado que te conozco..." y sigue con el
        // diálogo de menús YA EXISTENTE (mismas claves DLG_OLIVER_MENUS_01..08, doblaje reutilizado).
        yield return ShowBubblePaged(_oliverActor, Loc("OLIVER_GREETING_BEFORE_MENUS_HANDOFF"), _pageDuration, _greetGesture);

        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_01"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_02"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_03"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_04"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_05"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_06"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_07"), _pageDuration);
        yield return ShowBubblePaged(_oliverActor, Loc("DLG_OLIVER_MENUS_08"), _pageDuration);

        yield return Co_EndCinematicWithTransition();
        RaiseSignalOut();
    }

    /// Sprint simple hacia un punto, frenando a stopDistance del objetivo. Mismo espíritu que
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
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.position = Vector3.Lerp(start, end, t);
            anim?.TransitionToLocomotion();
            anim?.SetMovementSpeed(sprintSpeed);
            yield return null;
        }
        actor.position = end;
        anim?.SetMovementSpeed(0f);
        if (agent != null)
        {
            agent.Warp(actor.position);
            agent.enabled = true;
        }
    }
}
