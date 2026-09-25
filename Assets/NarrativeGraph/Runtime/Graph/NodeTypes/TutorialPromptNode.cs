using System;
using Core;
using Core.InputGlyphs;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra u oculta el TutorialPromptUI.
/// Show: el grafo ESPERA hasta que el jugador pulse la acción indicada (A/Submit/Interact) y
/// entonces oculta el prompt antes de avanzar. Excepción: si dismissWithCancel es true (aviso
/// puramente informativo, ver ese campo), el grafo YA NO espera — ver comentario en
/// _detachedInformational.
/// Hide: oculta inmediatamente y avanza.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "Aviso de tutorial", "Muestra un botón/instrucción en pantalla.")]
public sealed class TutorialPromptNode : NarrativeNode
{
    public enum PromptAction { Show, Hide }

    public PromptAction action = PromptAction.Show;

    [TextArea(1, 2)]
    [Tooltip("Puede incluir el token literal \"{BOTON}\" (ver Core.InputGlyphs.InputGlyphLabels), " +
             "que se sustituye en tiempo real por el nombre corto de la tecla/botón real según el " +
             "dispositivo activo (p.ej. \"E\" en teclado, \"A\" en Xbox). Solo funciona si " +
             "'buttonName' está relleno. NO escribir a mano el nombre de una tecla/botón concreta " +
             "aquí (p.ej. \"Pulsa A...\" o \"Usa el Joystick...\"): sería incorrecto en cuanto el " +
             "dispositivo activo no coincida con lo escrito — usar el token en su lugar.")]
    public string text;
    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    public string textId;
    [Tooltip("Icono de botón que aparece a la izquierda del texto (opcional). Si 'buttonName' " +
             "está relleno, este campo solo se usa como respaldo por si el nombre no resuelve nada " +
             "para la familia activa.")]
    public Sprite icon;
    [Tooltip("Nombre simbólico del botón/acción (constantes en Core.InputGlyphs.InputGlyphNames). " +
             "Resuelve el icono Y, si 'text' usa el token {BOTON}, también el literal — ambos en " +
             "tiempo real según el mando/teclado activo. Usar InputGlyphNames.Confirm (no South) " +
             "para cualquier prompt que dependa de UI/Submit en vez de GamePlay/Interact (p.ej. " +
             "cualquier \"pulsa para continuar\" que aparezca con el mapa GamePlay deshabilitado, " +
             "como en ActionMode.Cinematic — ver PlayerLockService.ApplyHardLock): en mando ambos " +
             "botones son físicamente el mismo, pero en teclado Interactuar (E) y Confirmar " +
             "(Espacio/Enter) son teclas distintas, y usar South ahí mostraría 'E' para algo que " +
             "solo funciona con Espacio.")]
    public string buttonName;

    [Tooltip("Si está activo, este aviso se cierra con el botón Cancelar (mando: Este/B, PS Círculo, " +
             "físicamente A en Switch — el mismo que ya usa el proyecto para 'atrás/cancelar', ver " +
             "PlayerControls.inputactions → UI/Cancel) y con el icono 'X' de la esquina superior " +
             "derecha (clic de ratón). En TECLADO no se usa Escape para cerrar: esa tecla también " +
             "abre el menú de pausa (GamePlay/Start comparte tecla con UI/Cancel), así que cerraría " +
             "el aviso Y pausaría el juego a la vez — en teclado/ratón (PC) este aviso se cierra solo " +
             "con la X. Pensado para avisos puramente informativos (tutorial de movimiento, de " +
             "minimapa...) que antes se cerraban con Confirmar/Interactuar, el mismo botón que avanza " +
             "diálogo — el jugador los cerraba sin querer, por inercia, nada más salir de una " +
             "conversación (petición de Raúl, 15 sept 2026). Dejar en 'false' (por defecto, " +
             "comportamiento de siempre) para avisos que piden pulsar el botón de una mecánica real " +
             "(p.ej. 'Pulsa {BOTON} para despertar'): ahí cerrar tiene que seguir exigiendo ESE " +
             "botón, o el texto dejaría de coincidir con lo que hace falta pulsar.\n\n" +
             "IMPORTANTE (INC-201d, 15 sept 2026): al activar esto, el grafo YA NO espera a que el " +
             "jugador cierre el aviso — avanza de inmediato al mostrarlo (ver _detachedInformational " +
             "en el código). Antes de este cambio, el propio cierre del aviso era lo único que hacía " +
             "avanzar el grafo, y como este aviso no bloquea el movimiento ni la interacción, un " +
             "jugador que siguiera jugando en vez de cerrarlo (algo perfectamente normal, es la " +
             "conducta esperada de un aviso no bloqueante) dejaba el grafo parado ahí para siempre. " +
             "Si el siguiente nodo del grafo es un WaitCustomEventNode que depende de algo que el " +
             "jugador puede disparar mientras el aviso sigue en pantalla (p.ej. leer la carta justo " +
             "después del tutorial de movimiento), el evento se perdía sin más — ver " +
             "incidencia-carta-no-activa-mision-tutorial-cierre-informativo-2026-09-15.md.")]
    public bool dismissWithCancel = false;

    [Header("Cierre automático (solo avisos informativos)")]
    [Tooltip("Señal que significa «ya has hecho lo que dice el aviso» (p. ej. WILL_REACHED_ELDRAN para " +
             "«Sigue el marcador para encontrar a Eldran»). Al emitirse, el aviso se quita solo. Si ya " +
             "se había emitido antes de mostrarlo, el aviso ni siquiera sale. No consume la señal: el " +
             "nodo que la espera la sigue recibiendo. Solo aplica con dismissWithCancel. INC-442.")]
    [NarrativeKey(NarrativeKeyKind.Signal)]
    public string cerrarConSenal;

    [Tooltip("Quitar el aviso en cuanto empiece una cinemática: el momento ya ha pasado y en mitad de " +
             "una escena queda raro. Solo aplica con dismissWithCancel. INC-442.")]
    public bool cerrarAlEmpezarCinematica = true;

    [Tooltip("Quitar el aviso cuando se abra cualquier diálogo (p. ej. «habla con quien lleva el icono»: " +
             "en cuanto hablas, ya está hecho). Solo aplica con dismissWithCancel. INC-442.")]
    public bool cerrarAlEmpezarDialogo = false;

    [System.NonSerialized]
    private Action<Transform> _dialogueHandler;

    [System.NonSerialized]
    private Action _signalHandler;
    [System.NonSerialized]
    private string _signalKey;
    [System.NonSerialized]
    private Action<bool> _cinematicHandler;

    [System.NonSerialized]
    private Action<GamepadInputReader.InputEvent> _waitHandler;
    [System.NonSerialized]
    private Action _closeButtonHandler;
    [System.NonSerialized]
    private Action _teleportHandler;

    // INC-201d (15 sept 2026): true mientras este nodo ha avanzado el grafo de inmediato
    // (dismissWithCancel=true) pero el aviso sigue en pantalla, aún sin cerrar a mano. GoTo()
    // llama a Exit() de este nodo de forma SÍNCRONA, anidada dentro de la propia llamada a
    // onReadyToAdvance() (antes de que Enter() siquiera termine de ejecutarse) — así que si Exit()
    // limpiase las suscripciones y ocultara la UI como hace siempre, el aviso desaparecería de
    // golpe en el mismo instante de mostrarse. Con esta bandera, Exit() no toca nada mientras el
    // cierre siga pendiente: las suscripciones (Cancelar/X/teletransporte) y la propia UI quedan
    // desligadas del ciclo de vida del nodo, y son ellas mismas quienes se limpian cuando el
    // jugador cierra el aviso (ver CloseInformationalPrompt más abajo).
    [System.NonSerialized]
    private bool _detachedInformational;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = TutorialPromptUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[TutorialPromptNode] ❌ TutorialPromptUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (action == PromptAction.Hide)
        {
            ui.Hide();
            onReadyToAdvance?.Invoke();
            return;
        }

        // Si lo que pide el aviso ya está hecho (su señal de cierre ya se emitió y sigue esperando
        // a quien la recoja), no tiene sentido enseñarlo. INC-442.
        if (dismissWithCancel && !string.IsNullOrEmpty(cerrarConSenal) && SenalYaEmitida(cerrarConSenal))
        {
            onReadyToAdvance?.Invoke();
            return;
        }

        string resolved = string.IsNullOrEmpty(textId)
            ? text
            : LocalizationManager.Instance?.Get(textId, text) ?? text;

        // La resolución de icono (y, si 'resolved' trae el token {BOTON}, también del literal) la
        // hace ahora TutorialPromptUI internamente, en caliente — así que si el jugador cambia de
        // mando/teclado con el prompt ya visible, texto e icono se actualizan solos en vez de
        // quedarse fijados al dispositivo que estaba activo cuando se llamó a Show(). El icono "X"
        // (allowManualClose) solo se muestra si dismissWithCancel está activo — ver comentario en
        // ese campo.
        ui.Show(resolved, buttonName, icon, allowManualClose: dismissWithCancel);

        void CloseInformationalPrompt()
        {
            if (_waitHandler != null)
            {
                GamepadInputReader.OnInput -= _waitHandler;
                _waitHandler = null;
            }
            if (_closeButtonHandler != null)
            {
                if (TutorialPromptUI.Instance != null) TutorialPromptUI.Instance.CloseButtonClicked -= _closeButtonHandler;
                _closeButtonHandler = null;
            }
            if (_teleportHandler != null)
            {
                TeleportService.OnTeleportEnded -= _teleportHandler;
                _teleportHandler = null;
            }
            QuitarCierresAutomaticos();
            TutorialPromptUI.Instance?.Hide();
        }

        if (dismissWithCancel)
        {
            // Informativo: el jugador puede cerrarlo a mano (Cancelar en mando, X en teclado/ratón,
            // o queda cerrado igualmente al teletransportarse — ver comentario original más abajo),
            // pero el GRAFO no espera a que lo haga (ver _detachedInformational arriba). Las
            // suscripciones se montan igual que siempre; lo único que cambia es que su limpieza ya
            // no está ligada a Exit() de este nodo, sino a que el propio jugador cierre el aviso.
            _detachedInformational = true;

            _waitHandler = (GamepadInputReader.InputEvent evt) =>
            {
                if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
                bool triggered = evt.Type == GamepadInputReader.InputEventType.Cancel
                               && InputGlyphService.CurrentFamily != InputGlyphDeviceFamily.KeyboardMouse;
                if (!triggered) return;
                CloseInformationalPrompt();
            };
            GamepadInputReader.OnInput += _waitHandler;

            _closeButtonHandler = CloseInformationalPrompt;
            ui.CloseButtonClicked += _closeButtonHandler;

            // Mismo salvavidas que antes: si el jugador se va de la zona sin cerrar el aviso, un
            // teletransporte lo cierra igualmente para que no se quede pegado en pantalla en la
            // escena siguiente (ver comentario histórico de INC-201c). Ya no hace falta como
            // salvavidas del GRAFO (que ya avanzó), solo de la UI.
            _teleportHandler = CloseInformationalPrompt;
            TeleportService.OnTeleportEnded += _teleportHandler;

            // INC-442: «o lo cierra el jugador, o se quita solo cuando se cumple lo que dice».
            var senales = DefaultNarrativeSignals.Instance;
            if (!string.IsNullOrEmpty(cerrarConSenal) && senales != null && !SenalYaEmitida(cerrarConSenal))
            {
                _signalKey = cerrarConSenal;
                _signalHandler = CloseInformationalPrompt;
                senales.OnCustom(_signalKey, _signalHandler);
            }
            if (cerrarAlEmpezarDialogo)
            {
                _dialogueHandler = _ => CloseInformationalPrompt();
                DialogueManager.OnDialogueStarted += _dialogueHandler;
            }
            if (cerrarAlEmpezarCinematica)
            {
                _cinematicHandler = activa => { if (activa) CloseInformationalPrompt(); };
                CinematicSequencerBase.OnAnySequenceActiveChanged += _cinematicHandler;
            }

            onReadyToAdvance?.Invoke();
            return;
        }

        // Comportamiento de siempre (dismissWithCancel = false): el grafo SÍ espera. Dos modos:
        // - Interactuar del GamePlay map, o Submit como fallback cuando el mapa GamePlay está
        //   deshabilitado (p.ej. ActionMode.Cinematic) — el botón de siempre, el mismo que avanza
        //   diálogo.
        void Finish()
        {
            CloseInformationalPrompt();
            onReadyToAdvance?.Invoke();
        }

        _waitHandler = (GamepadInputReader.InputEvent evt) =>
        {
            if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;

            bool triggered = evt.Type == GamepadInputReader.InputEventType.Interact
                           || evt.Type == GamepadInputReader.InputEventType.Submit;
            if (!triggered) return;

            Finish();
        };
        GamepadInputReader.OnInput += _waitHandler;
    }

    /// La señal ya se emitió y nadie la ha recogido todavía. Se mira sin suscribirse: OnCustom
    /// consumiría la señal pendiente y el nodo que de verdad la espera se quedaría sin ella.
    private static bool SenalYaEmitida(string key)
    {
        var s = DefaultNarrativeSignals.Instance;
        if (s == null) return false;
        foreach (var k in s.CurrentPending) if (k == key) return true;
        foreach (var k in s.CurrentRaised) if (k == key) return true;
        return false;
    }

    private void QuitarCierresAutomaticos()
    {
        if (_signalHandler != null)
        {
            DefaultNarrativeSignals.Instance?.OffCustom(_signalKey, _signalHandler);
            _signalHandler = null;
            _signalKey = null;
        }
        if (_cinematicHandler != null)
        {
            CinematicSequencerBase.OnAnySequenceActiveChanged -= _cinematicHandler;
            _cinematicHandler = null;
        }
        if (_dialogueHandler != null)
        {
            DialogueManager.OnDialogueStarted -= _dialogueHandler;
            _dialogueHandler = null;
        }
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_detachedInformational)
        {
            // El aviso y su cierre (Cancelar/X/teletransporte) siguen vivos, desligados de este
            // nodo — ver _detachedInformational. No tocar nada aquí: Exit() se llama de forma
            // síncrona nada más invocar onReadyToAdvance() en Enter(), así que limpiar aquí
            // cerraría el aviso en el mismo instante de mostrarse.
            _detachedInformational = false;
            return;
        }

        if (_waitHandler != null)
        {
            GamepadInputReader.OnInput -= _waitHandler;
            _waitHandler = null;
        }
        if (_closeButtonHandler != null)
        {
            if (TutorialPromptUI.Instance != null) TutorialPromptUI.Instance.CloseButtonClicked -= _closeButtonHandler;
            _closeButtonHandler = null;
        }
        if (_teleportHandler != null)
        {
            TeleportService.OnTeleportEnded -= _teleportHandler;
            _teleportHandler = null;
        }
        QuitarCierresAutomaticos();
        TutorialPromptUI.Instance?.Hide();
    }
}
