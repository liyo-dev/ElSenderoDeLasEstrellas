using System;
using System.Collections.Generic;
using Core;
using Core.InputGlyphs;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// Muestra u oculta el aviso de tutorial (TutorialPromptUI).
/// Mostrar: la historia espera a que el jugador pulse la acción indicada (A/Submit/Interact) y
/// entonces se oculta el aviso. Con <see cref="dismissWithCancel"/> (aviso solo informativo) la
/// historia no espera: el aviso queda en pantalla y se cierra a mano o solo (ver los campos).
/// Ocultar: oculta en el acto y sigue.
[Serializable]
[NarrativeNodeInfo("Diálogo", "Aviso de tutorial", "Muestra un botón o una instrucción en pantalla.")]
[NarrativeNoWorldEffect]
public sealed class PedirAvisoDeTutorial : NarrativeRequest
{
    public enum PromptAction { Show, Hide }

    public PromptAction action = PromptAction.Show;

    [TextArea(1, 2)]
    [Tooltip("Puede incluir el token literal \"{BOTON}\" (ver Core.InputGlyphs.InputGlyphLabels), que se " +
             "sustituye en tiempo real por el nombre corto del botón según el dispositivo activo. Solo " +
             "funciona si 'buttonName' está relleno. No escribir a mano el nombre de un botón concreto: " +
             "sería incorrecto en cuanto cambie el dispositivo.")]
    public string text;

    [Tooltip("ID de localización. Si no está vacío, sustituye a 'text'.")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    public string textId;

    [Tooltip("Icono del botón a la izquierda del texto (opcional). Con 'buttonName' relleno solo se usa si el nombre no resuelve nada para el dispositivo activo.")]
    public Sprite icon;

    [Tooltip("Nombre simbólico del botón o acción (constantes en Core.InputGlyphs.InputGlyphNames). Resuelve el " +
             "icono y, si 'text' usa {BOTON}, también el texto, en tiempo real. Usar InputGlyphNames.Confirm " +
             "(no South) para avisos que dependen de UI/Submit con el mapa GamePlay apagado (p. ej. en " +
             "ActionMode.Cinematic): en teclado Interactuar (E) y Confirmar (Espacio/Enter) son teclas distintas.")]
    public string buttonName;

    [Tooltip("Aviso solo informativo (tutorial de movimiento, de minimapa…): se cierra con Cancelar en el mando " +
             "y con la X de la esquina (ratón); en teclado no con Escape, que también abre la pausa. La historia " +
             "no espera a que se cierre: el aviso no bloquea el movimiento. Dejar en false para avisos que " +
             "piden pulsar el botón de una mecánica real (p. ej. «Pulsa {BOTON} para despertar»).")]
    public bool dismissWithCancel = false;

    [Header("Cierre automático (solo avisos informativos)")]
    [Tooltip("Señal que significa «ya has hecho lo que dice el aviso». Al emitirse, el aviso se quita solo; si ya " +
             "se había emitido, ni sale. No consume la señal. Solo con dismissWithCancel.")]
    [NarrativeKey(NarrativeKeyKind.Signal)]
    public string cerrarConSenal;

    [Tooltip("Quitar el aviso en cuanto empiece una cinemática. Solo con dismissWithCancel.")]
    public bool cerrarAlEmpezarCinematica = true;

    [Tooltip("Quitar el aviso cuando se abra cualquier diálogo. Solo con dismissWithCancel.")]
    public bool cerrarAlEmpezarDialogo = false;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (action == PromptAction.Hide) { d.Line("Oculta el aviso", NarrativeTextTone.Strong); return; }
        d.Line(d.Text(textId, text) ?? string.Empty);
        if (!string.IsNullOrEmpty(buttonName)) d.Tag(buttonName);
        if (dismissWithCancel) d.Tag("informativo");
        if (!string.IsNullOrEmpty(cerrarConSenal)) d.Signal(cerrarConSenal, SignalRole.Escucha, "se cierra con");
    }
}

public sealed class AvisoDeTutorialEnCurso : NarrativeRequestRun<PedirAvisoDeTutorial>
{
    Action<GamepadInputReader.InputEvent> _waitHandler;
    Action _closeButtonHandler;
    Action _teleportHandler;
    Action<Transform> _dialogueHandler;
    Action _signalHandler;
    string _signalKey;
    Action<bool> _cinematicHandler;
    Action _popupHandler;

    // Un aviso informativo sigue en pantalla después de que la historia siga: su cierre (Cancelar,
    // X, teletransporte, señal…) ya no va ligado al paso, así que Stop no lo toca. Ver INC-201d.
    bool _sueltoDelPaso;

    protected override void Start()
    {
        var ui = TutorialPromptUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[Aviso de tutorial] Falta TutorialPromptUI en el Canvas persistente de Start.unity.");
            Finish(0);
            return;
        }

        if (R.action == PedirAvisoDeTutorial.PromptAction.Hide)
        {
            ui.Hide();
            Finish(0);
            return;
        }

        // Si lo que pide el aviso ya está hecho (su señal de cierre ya se emitió y sigue esperando
        // a quien la recoja), no tiene sentido enseñarlo. INC-442.
        if (R.dismissWithCancel && !string.IsNullOrEmpty(R.cerrarConSenal) && SenalYaEmitida(R.cerrarConSenal))
        {
            Finish(0);
            return;
        }

        string resolved = string.IsNullOrEmpty(R.textId) ? R.text : LocalizationManager.Instance?.Get(R.textId, R.text) ?? R.text;
        // El icono (y, con {BOTON}, el texto) se resuelve en caliente según el dispositivo activo.
        ui.Show(resolved, R.buttonName, R.icon, allowManualClose: R.dismissWithCancel);
        VoiceLines.TryPlay(R.textId);

        if (R.dismissWithCancel)
        {
            MostrarInformativo(ui);
            _sueltoDelPaso = true;
            Finish(0);
            return;
        }

        // Espera a Interactuar del mapa GamePlay, o a Submit con el mapa GamePlay apagado
        // (p. ej. ActionMode.Cinematic): es el mismo botón que avanza el diálogo.
        _waitHandler = evt =>
        {
            if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
            if (evt.Type != GamepadInputReader.InputEventType.Interact && evt.Type != GamepadInputReader.InputEventType.Submit) return;
            Finish(0);
        };
        GamepadInputReader.OnInput += _waitHandler;
    }

    protected override void Stop()
    {
        if (_sueltoDelPaso) return;
        Cerrar();
    }

    void MostrarInformativo(TutorialPromptUI ui)
    {
        _waitHandler = evt =>
        {
            if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
            if (evt.Type == GamepadInputReader.InputEventType.Cancel && InputGlyphService.CurrentFamily != InputGlyphDeviceFamily.KeyboardMouse)
                Cerrar();
        };
        GamepadInputReader.OnInput += _waitHandler;

        _closeButtonHandler = Cerrar;
        ui.CloseButtonClicked += _closeButtonHandler;

        // Si el jugador se va de la zona sin cerrarlo, un teletransporte lo cierra. Ver INC-201c.
        _teleportHandler = Cerrar;
        TeleportService.OnTeleportEnded += _teleportHandler;

        // O lo cierra el jugador, o se quita solo cuando se cumple lo que dice. Ver INC-442.
        var senales = DefaultNarrativeSignals.Instance;
        if (!string.IsNullOrEmpty(R.cerrarConSenal) && senales != null)
        {
            if (R.cerrarConSenal.StartsWith("NPC_INTERACT_")) senales.ClearPendingCustom(R.cerrarConSenal);
            if (!SenalYaEmitida(R.cerrarConSenal))
            {
                _signalKey = R.cerrarConSenal;
                _signalHandler = Cerrar;
                senales.OnCustom(_signalKey, _signalHandler);
            }
        }
        if (R.cerrarAlEmpezarDialogo)
        {
            _dialogueHandler = _ => Cerrar();
            DialogueManager.OnDialogueStarted += _dialogueHandler;
        }
        if (R.cerrarAlEmpezarCinematica)
        {
            _cinematicHandler = activa => { if (activa) Cerrar(); };
            CinematicSequencerBase.OnAnySequenceActiveChanged += _cinematicHandler;
        }
        // Un aviso informativo no se queda debajo de una pregunta que se abre. INC-1163.
        _popupHandler = Cerrar;
        ConfirmationPopupUI.AlAbrir += _popupHandler;
    }

    /// Quita el aviso y todas sus suscripciones.
    void Cerrar()
    {
        if (_waitHandler != null) { GamepadInputReader.OnInput -= _waitHandler; _waitHandler = null; }
        if (_closeButtonHandler != null)
        {
            if (TutorialPromptUI.Instance != null) TutorialPromptUI.Instance.CloseButtonClicked -= _closeButtonHandler;
            _closeButtonHandler = null;
        }
        if (_teleportHandler != null) { TeleportService.OnTeleportEnded -= _teleportHandler; _teleportHandler = null; }
        if (_signalHandler != null)
        {
            DefaultNarrativeSignals.Instance?.OffCustom(_signalKey, _signalHandler);
            _signalHandler = null;
            _signalKey = null;
        }
        if (_cinematicHandler != null) { CinematicSequencerBase.OnAnySequenceActiveChanged -= _cinematicHandler; _cinematicHandler = null; }
        if (_popupHandler != null) { ConfirmationPopupUI.AlAbrir -= _popupHandler; _popupHandler = null; }
        if (_dialogueHandler != null) { DialogueManager.OnDialogueStarted -= _dialogueHandler; _dialogueHandler = null; }
        TutorialPromptUI.Instance?.Hide();
    }

    /// La señal ya se emitió y nadie la ha recogido todavía. Se mira sin suscribirse: OnCustom
    /// consumiría la señal pendiente y el paso que de verdad la espera se quedaría sin ella. Las
    /// de interacción con NPCs (NPC_INTERACT_*) se repiten en la partida y no cuentan.
    static bool SenalYaEmitida(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("NPC_INTERACT_")) return false;
        var s = DefaultNarrativeSignals.Instance;
        return s != null && s.IsPendingDelivery(key);
    }
}
