using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Core.InputGlyphs;

/// Pide un gesto de input y guarda su resultado en el contexto de cualquier secuencia.
[System.Serializable]
public sealed class InputPromptBeat : SequenceBeat
{
    public PanicInputMode mode = PanicInputMode.Mash;
    public InputActionReference actionRef;
    public InputActionReference directionActionRef;
    public int pressesRequired = 8;
    public float windowSeconds = 2.5f;
    public float holdSeconds = 1.5f;
    public float maxHoldSeconds = 2.5f;
    public Vector2 expectedDirection = Vector2.up;
    public float directionAngleTolerance = 45f;
    public float directionHoldSeconds = 0.15f;
    public string iconGlyphName = InputGlyphNames.Confirm;
    public string promptTextId;
    public string successFlag;
    public bool tolerant = true;
    [UnityEngine.Tooltip("La escena se detiene mientras se espera al jugador (hasta acertar, fallar o agotar la ventana).")]
    public bool detenerEscena = false;

    public override string Describe() => $"Entrada {mode} → {successFlag}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        ctx.SetFlag(successFlag, false);
        var objeto = new GameObject($"InputPrompt · {mode}");
        var detector = objeto.AddComponent<PanicInputDetector>();
        detector.Configure(mode, actionRef, directionActionRef, pressesRequired, windowSeconds,
            holdSeconds, maxHoldSeconds, expectedDirection, directionAngleTolerance, directionHoldSeconds);
        var icono = InputGlyphService.GetSprite(iconGlyphName);
        var ui = PanicInputUI.GetOrCreate(icono);
        var texto = TutorialPromptUI.Instance;
        var salto = GlobalCinematicSkipController.Instance;
        bool accionActiva = actionRef != null && actionRef.action.enabled;
        bool direccionActiva = directionActionRef != null && directionActionRef.action.enabled;
        bool terminado = false, exito = false, limpio = false;
        if (detenerEscena) ctx.RetenerReloj();
        void Exito() { exito = true; terminado = true; }
        void Fallo() { terminado = true; }
        void Limpiar()
        {
            if (limpio) return;
            limpio = true;
            if (detenerEscena) ctx.SoltarReloj();
            if (detector != null)
            {
                detector.OnSuccess -= Exito;
                detector.OnFailure -= Fallo;
                detector.StopListening();
            }
            if (ui != null) ui.Deactivate();
            if (!string.IsNullOrEmpty(promptTextId) && texto != null) texto.Hide();
            if (salto != null) salto.Unsuppress();
            if (actionRef != null && !accionActiva) actionRef.action.Disable();
            if (directionActionRef != null && !direccionActiva) directionActionRef.action.Disable();
            if (objeto != null) Object.Destroy(objeto);
        }
        // La limpieza también cubre salto, desactivación y destrucción del reproductor.
        ctx.Player.RegisterCleanup(Limpiar);
        detector.OnSuccess += Exito;
        detector.OnFailure += Fallo;
        salto?.Suppress();
        ui?.SetIcon(icono);
        ui?.Activate(detector);
        if (!string.IsNullOrEmpty(promptTextId) && texto != null)
            texto.Show(LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(promptTextId, promptTextId) : promptTextId, iconGlyphName);
        detector.StartListening();
        float limite = Time.unscaledTime + Mathf.Max(0.1f, windowSeconds);
        while (!terminado && Time.unscaledTime < limite) yield return null;
        ctx.SetFlag(successFlag, terminado && exito);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (!terminado && !tolerant) Debug.LogWarning($"[InputPromptBeat] Expira {note}.");
#endif
        Limpiar();
    }
}
