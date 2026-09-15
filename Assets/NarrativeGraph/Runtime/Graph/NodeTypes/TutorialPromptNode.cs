using System;
using Core;
using Core.InputGlyphs;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra u oculta el TutorialPromptUI.
/// Show: el grafo ESPERA hasta que el jugador pulse la acción indicada (A/Submit/Interact) y
/// entonces oculta el prompt antes de avanzar.
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
             "botón, o el texto dejaría de coincidir con lo que hace falta pulsar.")]
    public bool dismissWithCancel = false;

    [System.NonSerialized]
    private Action<GamepadInputReader.InputEvent> _waitHandler;
    [System.NonSerialized]
    private Action _closeButtonHandler;
    [System.NonSerialized]
    private Action _teleportHandler;

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

        void Finish()
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
            TutorialPromptUI.Instance?.Hide();
            onReadyToAdvance?.Invoke();
        }

        // Esperar la confirmación del jugador. Dos modos, según dismissWithCancel:
        // - false (por defecto): Interact del GamePlay map, o Submit como fallback cuando el mapa
        //   GamePlay está deshabilitado (p.ej. ActionMode.Cinematic) — el botón de siempre, el mismo
        //   que avanza diálogo.
        // - true: Cancelar (mando: Este/B — distinto del botón de diálogo, sin colisión posible) más
        //   el clic en la X (ver más abajo). En TECLADO, Cancel también se dispara con Escape, que a
        //   la vez abre el menú de pausa (comparten tecla física) — para no cerrar el aviso Y pausar
        //   el juego de golpe, en teclado/ratón el aviso NO se cierra por Cancel, solo con la X.
        _waitHandler = (GamepadInputReader.InputEvent evt) =>
        {
            if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;

            bool triggered;
            if (dismissWithCancel)
            {
                triggered = evt.Type == GamepadInputReader.InputEventType.Cancel
                         && InputGlyphService.CurrentFamily != InputGlyphDeviceFamily.KeyboardMouse;
            }
            else
            {
                triggered = evt.Type == GamepadInputReader.InputEventType.Interact
                         || evt.Type == GamepadInputReader.InputEventType.Submit;
            }
            if (!triggered) return;

            Finish();
        };
        GamepadInputReader.OnInput += _waitHandler;

        if (dismissWithCancel)
        {
            _closeButtonHandler = Finish;
            ui.CloseButtonClicked += _closeButtonHandler;

            // INC-201c (15 sept 2026, Raúl): un aviso informativo (dismissWithCancel=true) podía
            // quedarse esperando para siempre si el jugador salía de la casa/habitación sin
            // cerrarlo antes (p.ej. sin fijarse en el icono de cerrar) — el grafo no avanzaba Y el
            // aviso seguía dibujándose encima de la escena siguiente, porque TutorialPromptUI es un
            // singleton persistente sin ningún vínculo con la escena en la que se mostró.
            // TeleportService.OnTeleportEnded se dispara con CUALQUIER teletransporte del juego
            // (entrar/salir de interiores, puntos de guardado...), así que sirve como señal
            // genérica de "hemos cambiado de sitio" sin tener que enseñarle a este nodo la
            // escena/anchor concretos de cada aviso. Solo se engancha aquí, nunca para los avisos
            // que exigen pulsar el botón de una mecánica real (dismissWithCancel=false, p.ej.
            // "Pulsa {BOTON} para despertar"): esos SÍ deben seguir bloqueando hasta que se pulse
            // ese botón exacto.
            _teleportHandler = Finish;
            TeleportService.OnTeleportEnded += _teleportHandler;
        }
    }

    public override void Exit(NarrativeContext ctx)
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
        TutorialPromptUI.Instance?.Hide();
    }
}
