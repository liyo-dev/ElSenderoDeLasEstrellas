using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Modo de entrada que espera un <see cref="PanicInputDetector"/>. Cada uno cubre un patrón de QTE
/// distinto. 'Mash' es el original (Despertar de la Estrella) y su comportamiento no cambia ni un
/// frame al añadir el resto — son ampliaciones aditivas pensadas para <c>InputPromptBeat</c>, que
/// las usa para QTEs de puesta en escena (el prólogo, ver
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §4.2).
public enum PanicInputMode
{
    /// Machaqueo: N pulsaciones discretas dentro de una ventana. El original, sin cambios.
    Mash = 0,

    /// Mantener pulsado sin soltar durante 'holdSeconds' seguidos. Soltar antes de tiempo reinicia
    /// el progreso a 0 — hay que aguantar de un tirón.
    Hold = 1,

    /// Mantener pulsado un mínimo de 'holdSeconds' y soltar antes de 'maxHoldSeconds'. Soltar
    /// demasiado pronto O demasiado tarde no cuenta como éxito — es la variante que castiga
    /// pasarse (la cometa del prólogo: soltarla tarde la rompe).
    HoldRelease = 2,

    /// Una sola pulsación en cualquier momento de la ventana. Azúcar sobre Mash con el contador a 1.
    TimedPress = 3,

    /// Empujar el stick/D-pad hacia 'expectedDirection' (con tolerancia de ángulo) y mantenerlo un
    /// instante. Usa 'directionAction' en vez de 'panicAction'.
    Direction = 4,
}

/// Detector de input para QTEs de secuencia (panic input, prólogo, combate). Usa unscaledTime para
/// funcionar durante Time.timeScale reducido.
///
/// El modo 'Mash' es el original y de sobra probado (INC-218/221/222/224): StarAwakeningModule lo
/// sigue usando exactamente igual, configurado a mano en un prefab de escena. Los otros cuatro
/// modos son para <c>InputPromptBeat</c>, que crea y configura el detector POR CÓDIGO en runtime
/// (ver <see cref="Configure"/>) — no hace falta prepararlo a mano en ninguna escena.
[DisallowMultipleComponent]
public class PanicInputDetector : MonoBehaviour
{
    [SerializeField] private PanicInputMode mode = PanicInputMode.Mash;

    [Header("Mash / TimedPress")]
    [SerializeField] private InputActionReference panicAction;
    [SerializeField] private int pressesRequired = 8;
    [SerializeField] private float windowSeconds = 2.5f;

    [Header("Hold / HoldRelease")]
    [Tooltip("Hold: segundos seguidos que hay que aguantar pulsado. HoldRelease: el mínimo antes " +
             "de poder soltar sin que cuente como fallo.")]
    [SerializeField] private float holdSeconds = 1.5f;
    [Tooltip("Solo HoldRelease: soltar después de este tiempo también cuenta como fallo.")]
    [SerializeField] private float maxHoldSeconds = 2.5f;

    [Header("Direction")]
    [SerializeField] private InputActionReference directionAction;
    [SerializeField] private Vector2 expectedDirection = Vector2.up;
    [SerializeField] private float directionAngleTolerance = 45f;
    [SerializeField] private float directionHoldSeconds = 0.15f;

    public event Action<float> OnProgressChanged;
    public event Action OnSuccess;
    public event Action OnFailure;

    private bool  _listening;
    private int   _pressCount;
    private float _windowEndUnscaled;

    // Hold / HoldRelease
    private bool  _isHeld;
    private float _heldSinceUnscaled;

    // Direction
    private bool  _directionHeld;
    private float _directionHeldSinceUnscaled;

    public bool IsListening => _listening;
    public float TimeRemaining => _listening
        ? Mathf.Max(0f, _windowEndUnscaled - Time.unscaledTime)
        : 0f;

    /// Configura el detector por código — lo usa <c>InputPromptBeat</c> al crearlo en runtime, en
    /// vez de dejarlo puesto a mano en un prefab de escena (como el del Despertar de la Estrella).
    /// No lo usa StarAwakeningModule: su detector sigue viniendo configurado desde el Inspector.
    public void Configure(PanicInputMode mode, InputActionReference action, InputActionReference directionAction,
        int pressesRequired, float windowSeconds, float holdSeconds, float maxHoldSeconds,
        Vector2 expectedDirection, float directionAngleTolerance, float directionHoldSeconds)
    {
        this.mode = mode;
        this.panicAction = action;
        this.directionAction = directionAction;
        this.pressesRequired = pressesRequired;
        this.windowSeconds = windowSeconds;
        this.holdSeconds = holdSeconds;
        this.maxHoldSeconds = maxHoldSeconds;
        this.expectedDirection = expectedDirection;
        this.directionAngleTolerance = directionAngleTolerance;
        this.directionHoldSeconds = directionHoldSeconds;
    }

    public void StartListening()
    {
        _listening  = true;
        _pressCount = 0;
        _isHeld = false;
        _directionHeld = false;
        _windowEndUnscaled = Time.unscaledTime + windowSeconds;

        if (mode == PanicInputMode.Direction)
        {
            if (directionAction == null) { LogMissingAction(); return; }
            directionAction.action.Enable();
            return;
        }

        if (panicAction == null) { LogMissingAction(); return; }

        // Force-enable ignorando el estado que PlayerActionManager pueda haber seteado
        panicAction.action.Enable();
        panicAction.action.performed += OnPress;
        if (mode == PanicInputMode.Hold || mode == PanicInputMode.HoldRelease)
            panicAction.action.canceled += OnRelease;
    }

    public void StopListening()
    {
        if (!_listening) return;
        _listening = false;

        if (panicAction != null)
        {
            panicAction.action.performed -= OnPress;
            panicAction.action.canceled  -= OnRelease;
        }
    }

    void Update()
    {
        if (!_listening) return;

        if (Time.unscaledTime >= _windowEndUnscaled)
        {
            StopListening();
            OnFailure?.Invoke();
            return;
        }

        if (mode == PanicInputMode.Hold) UpdateHold();
        else if (mode == PanicInputMode.Direction) UpdateDirection();
    }

    private void UpdateHold()
    {
        if (!_isHeld) return;
        float held = Time.unscaledTime - _heldSinceUnscaled;
        OnProgressChanged?.Invoke(Mathf.Clamp01(held / Mathf.Max(0.01f, holdSeconds)));
        if (held >= holdSeconds)
        {
            StopListening();
            OnSuccess?.Invoke();
        }
    }

    private void UpdateDirection()
    {
        if (directionAction == null) return;

        Vector2 stick = directionAction.action.ReadValue<Vector2>();
        bool pointingRight = stick.sqrMagnitude > 0.15f * 0.15f &&
            Vector2.Angle(stick.normalized, expectedDirection.normalized) <= directionAngleTolerance;

        if (!pointingRight)
        {
            _directionHeld = false;
            OnProgressChanged?.Invoke(0f);
            return;
        }

        if (!_directionHeld)
        {
            _directionHeld = true;
            _directionHeldSinceUnscaled = Time.unscaledTime;
        }

        float held = Time.unscaledTime - _directionHeldSinceUnscaled;
        OnProgressChanged?.Invoke(Mathf.Clamp01(held / Mathf.Max(0.01f, directionHoldSeconds)));
        if (held >= directionHoldSeconds)
        {
            StopListening();
            OnSuccess?.Invoke();
        }
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (!_listening) return;

        if (mode == PanicInputMode.Mash)
        {
            _pressCount++;
            float progress = Mathf.Clamp01((float)_pressCount / pressesRequired);
            OnProgressChanged?.Invoke(progress);

            if (_pressCount >= pressesRequired)
            {
                StopListening();
                OnSuccess?.Invoke();
            }
            return;
        }

        if (mode == PanicInputMode.TimedPress)
        {
            OnProgressChanged?.Invoke(1f);
            StopListening();
            OnSuccess?.Invoke();
            return;
        }

        if (mode == PanicInputMode.Hold || mode == PanicInputMode.HoldRelease)
        {
            _isHeld = true;
            _heldSinceUnscaled = Time.unscaledTime;
        }
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        if (!_listening || !_isHeld) return;

        float held = Time.unscaledTime - _heldSinceUnscaled;
        _isHeld = false;

        if (mode == PanicInputMode.HoldRelease && held >= holdSeconds && held <= maxHoldSeconds)
        {
            StopListening();
            OnSuccess?.Invoke();
            return;
        }

        // HoldRelease soltado demasiado pronto o demasiado tarde: no cuenta como éxito, se deja
        // que la ventana expire y falle por tiempo — mismo camino de salida que el resto de modos,
        // en vez de un fallo inmediato que no le daría tiempo a leer el aviso al jugador.
        // Hold soltado antes de tiempo: mismo trato, y se reinicia la barra visualmente.
        OnProgressChanged?.Invoke(0f);
    }

    private void LogMissingAction()
    {
        _listening = false;
        Debug.LogError($"[PanicInputDetector] Modo '{mode}' sin acción de input asignada.", this);
    }

    void OnDestroy() => StopListening();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular éxito")]
    void SimulateSuccess()
    {
        StopListening();
        OnSuccess?.Invoke();
    }
#endif
}
