using UnityEngine;

namespace Core
{
    /// <summary>
    /// Registra al arrancar los servicios del juego que usa el controlador de personaje de Plugins
    /// (entrada y sonido) en <see cref="CharacterControllerBridge"/>.
    /// </summary>
    public static class CharacterControllerBridgeSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            CharacterControllerBridge.Input = GamepadCharacterInput.Instance;
            CharacterControllerBridge.PlaySfx = PlaySfxAt;
        }

        static void PlaySfxAt(AudioClip clip, float volume, Vector3 position)
        {
            if (clip == null) return;
            if (AudioService.Instance != null) AudioService.Instance.PlaySFXAt(clip, position, volume);
            else AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }

    /// <summary>
    /// Entrada de movimiento del jugador leída de <see cref="GamepadInputReader"/>. La cámara no se
    /// mueve con una pantalla de UI abierta (el tiempo está parado y el giro se acumularía hasta
    /// cerrarla) y respeta la inversión elegida en ajustes.
    /// </summary>
    public sealed class GamepadCharacterInput : ICharacterInputSource
    {
        public static readonly GamepadCharacterInput Instance = new GamepadCharacterInput();

        GamepadCharacterInput() { }

        public Vector2 Move => GamepadInputReader.Move;
        public bool SprintHeld => GamepadInputReader.SprintHeld;
        // Tecleando un combo (Y), la A es un botón de la secuencia, no un salto (INC-494).
        public bool JumpPressed => !ComboCastController.IsComposing && GamepadInputReader.JumpPressed;

        public Vector2 CameraLook
        {
            get
            {
                var pim = PlayerInputManager.Instance;
                if (pim != null && pim.IsInUIMode) return Vector2.zero;
                return PlayerSettings.ApplyLookInversion(GamepadInputReader.CameraLook);
            }
        }
    }
}
