using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Core.InputGlyphs;

/// Pide al jugador que pulse/mantenga/suelte/incline un botón para seguir, y deja el resultado en
/// una marca de contexto — sin que la secuencia tenga que traer nada preparado en la escena.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El Despertar de la Estrella ya tiene un panic input (mash), pero vive atado a un
/// PanicInputDetector/PanicInputUI puestos a mano en un prefab de esa escena concreta —
/// StarAwakeningModule.Co_PanicInput los busca por referencia de Inspector. Este beat es la
/// versión genérica: crea su propio detector en runtime (ver PanicInputDetector.Configure) y
/// reutiliza el mismo PanicInputUI singleton (PanicInputUI.GetOrCreate), así que CUALQUIER
/// secuencia puede pedir un QTE con un beat suelto, sin preparar nada en el Editor. Pensado en
/// primer lugar para las seis fases con input del prólogo (§4.2 del análisis).
///
/// StarAwakeningModule.PanicInput NO se ha tocado ni se ha migrado a este beat — sigue exactamente
/// igual que estaba. Es una pieza demasiado ajustada (INC-218/221/222/224 son la misma familia de
/// bug, encontrada y arreglada una vez cada vez) para tocarla sin poder probarla en Play. Migrarla
/// es un cambio aparte, deliberadamente pospuesto.
///
/// ── Ramificación ──────────────────────────────────────────────────────────────────────────────
/// 'successFlag' se deja puesto a true/false con el resultado — la siguiente fase se ramifica con
/// SequencePhase.onlyIfFlag/skipIfFlag, igual que ya hace el Despertar con 'panicSuperado'.
///
/// 'tolerant' (por defecto activado) es la regla para cualquier QTE de un prólogo: la secuencia
/// NUNCA se cuelga ni termina en un game over por no responder a tiempo. El propio
/// PanicInputDetector ya garantiza esto (su ventana expira sola y dispara OnFailure), y este beat
/// añade además el mismo margen de seguridad que ya usa StarAwakeningModule.Co_PanicInput por si
/// el detector se quedara parado por cualquier otra razón.
[System.Serializable]
public class InputPromptBeat : SequenceBeat
{
    [SerializeField] private PanicInputMode mode = PanicInputMode.Mash;

    [Tooltip("Acción de botón. Hace falta para todos los modos salvo 'Direction'. Normalmente la " +
             "misma acción de Confirmar/Interactuar que ya usan TutorialPromptNode y el resto de " +
             "prompts del juego.")]
    public InputActionReference actionRef;

    [Tooltip("Solo modo 'Direction': acción de stick/D-pad (Vector2).")]
    public InputActionReference directionActionRef;

    [Header("Mash / TimedPress")]
    public int pressesRequired = 8;
    public float windowSeconds = 2.5f;

    [Header("Hold / HoldRelease")]
    public float holdSeconds = 1.5f;
    public float maxHoldSeconds = 2.5f;

    [Header("Direction")]
    public Vector2 expectedDirection = Vector2.up;
    public float directionAngleTolerance = 45f;
    public float directionHoldSeconds = 0.15f;

    [Header("Icono")]
    [Tooltip("Nombre de sprite de InputGlyphNames (p. ej. InputGlyphNames.Confirm). Vacío = " +
             "'interactable_confirm', el mismo que usan los demás prompts de 'pulsa para continuar'.")]
    public string iconGlyphName = InputGlyphNames.Confirm;

    [Header("Resultado")]
    [Tooltip("Marca de contexto donde se deja true (éxito) o false (fallo/expiró). Vacío = no deja " +
             "nada — para un QTE puramente decorativo sin rama distinta después.")]
    public string successFlag;

    [Tooltip("Este beat, con 'tolerant' o sin él, NUNCA cuelga la secuencia — es una regla del " +
             "sistema entero (SequenceBeat.Run debe terminar solo), no algo que decida este campo. " +
             "'tolerant' (recomendado para cualquier prólogo o tutorial) solo cambia cómo se avisa " +
             "en consola si la ventana expira sin resolverse: como fallo esperado (tolerant) o " +
             "como aviso a revisar (desactivado) — para un QTE que en teoría el jugador siempre " +
             "debería completar y donde expirar sí merece mirarse.")]
    public bool tolerant = true;

    public override string Describe()
        => $"Prompt de input ({mode})" + (string.IsNullOrEmpty(successFlag) ? "" : $" → {successFlag}");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var player = ctx?.Player;
        if (player == null) yield break;

        // El detector se crea aquí, en runtime, específico de este beat — a diferencia del
        // Despertar de la Estrella, que lo trae ya puesto en un prefab de escena.
        var detectorGO = new GameObject($"InputPromptBeat_{mode}");
        var detector = detectorGO.AddComponent<PanicInputDetector>();
        detector.Configure(mode, actionRef, directionActionRef, pressesRequired, windowSeconds,
            holdSeconds, maxHoldSeconds, expectedDirection, directionAngleTolerance, directionHoldSeconds);

        string glyphName = string.IsNullOrWhiteSpace(iconGlyphName) ? InputGlyphNames.Confirm : iconGlyphName;
        var icon = InputGlyphService.GetSprite(glyphName);
        var ui = PanicInputUI.GetOrCreate(icon);
        ui?.SetIcon(icon);

        bool done = false;
        bool succeeded = false;
        bool cleaned = false;

        void HandleSuccess() { succeeded = true; done = true; }
        void HandleFailure() { succeeded = false; done = true; }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            detector.OnSuccess -= HandleSuccess;
            detector.OnFailure -= HandleFailure;
            detector.StopListening();
            ui?.Deactivate();
            if (detectorGO != null) Object.Destroy(detectorGO);

            // FIX (17 sep 2026, Raúl: "sale que pulse la misma tecla que usamos para saltar la
            // secuencia" -- confirmado en el prólogo: el jugador aguantando 'Espacio' para un
            // Hold/HoldRelease completaba SIN QUERER el hold-to-skip global (HoldToSkipUI usa
            // SIEMPRE UI/Submit mientras dura la cinemática, la MISMA acción física que Interactuar
            // en teclado/mando -- ver el comentario "CRÍTICO" en HoldToSkipUI.TryConfigureInput).
            // El síntoma real no era "botón equivocado" sino las FASES 4/5/6 saltándose enteras:
            // el log mostraba HoldToSkipUI completando su barra de 1,25s a mitad del QTE de 'La
            // hora concedida' y disparando PROLOGUE_DONE de golpe. Mientras este prompt está
            // escuchando, se oculta y desactiva el botón global de saltar (mismo mecanismo ya
            // usado para PromoEstudio.unity) -- así mantener pulsado para ESTE prompt nunca puede
            // además completar por debajo el gesto de saltar TODA la secuencia.
            //
            // OJO: Suppress()/Unsuppress() son un flag simple, no un contador de referencias. Si
            // algún día dos InputPromptBeat llegaran a solaparse dentro de un ParallelBeat, el que
            // termine antes reactivaría el botón mientras el otro sigue escuchando. Hoy ningún beat
            // del catálogo mete dos InputPromptBeat en paralelo (no tendría sentido: solo hay un
            // botón de acción), así que se deja así -- si se necesita en el futuro, cambiar esto por
            // un contador en GlobalCinematicSkipController en vez de duplicar la lógica aquí.
            GlobalCinematicSkipController.Instance?.Unsuppress();
        }

        // Registrado SIEMPRE, no solo si algo falla: si la secuencia se salta a mitad del QTE,
        // StopCoroutine no pasa por el final de este método, y sin esto el detector/objeto de
        // apoyo se quedarían vivos y el overlay del icono, en pantalla, para siempre — el mismo
        // patrón de fallo silencioso que ya documentan INC-208/INC-221/INC-224 en esta misma
        // familia de código.
        player.RegisterCleanup(Cleanup);

        detector.OnSuccess += HandleSuccess;
        detector.OnFailure += HandleFailure;

        GlobalCinematicSkipController.Instance?.Suppress();
        ui?.Activate(detector);
        detector.StartListening();

        // Red de seguridad, mismo patrón que StarAwakeningModule.Co_PanicInput: el detector ya se
        // corta solo a los 'windowSeconds' (o al completar el gesto), esto es solo por si algo
        // externo lo deja parado sin avisar.
        float limite = Time.unscaledTime + Mathf.Max(windowSeconds, 1f) + 5f;
        while (!done && Time.unscaledTime < limite)
            yield return null;

        if (!done)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string msg = $"[InputPromptBeat] '{note}' no resolvió a tiempo (detector parado o sin responder).";
            if (tolerant) Debug.LogWarning(msg + " Se continúa como fallo, por ser 'tolerant'.");
            else Debug.LogError(msg + " 'tolerant' está desactivado: conviene revisar por qué.");
#endif
            succeeded = false;
        }

        if (!string.IsNullOrWhiteSpace(successFlag))
            ctx.SetFlag(successFlag, succeeded);

        Cleanup();
    }
}
