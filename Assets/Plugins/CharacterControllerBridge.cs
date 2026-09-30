using UnityEngine;

/// <summary>
/// Entrada de movimiento que consume el controlador de personaje. El juego la implementa y la
/// registra en <see cref="CharacterControllerBridge"/>; los valores llegan ya con la supresión de
/// menús y la inversión de cámara aplicadas.
/// </summary>
public interface ICharacterInputSource
{
    Vector2 Move { get; }
    Vector2 CameraLook { get; }
    bool SprintHeld { get; }
    bool JumpPressed { get; }
}

/// <summary>
/// Puente entre el controlador de personaje (ensamblado de Plugins, que compila antes que el juego)
/// y el juego. El juego registra aquí sus servicios al arrancar, así el controlador no depende de
/// tipos del juego ni usa reflexión.
/// </summary>
public static class CharacterControllerBridge
{
    /// <summary>Fuente de entrada del jugador. Sin registrar, el controlador no recibe entrada.</summary>
    public static ICharacterInputSource Input { get; set; }

    /// <summary>Reproduce un efecto de sonido en una posición: clip, volumen y posición.</summary>
    public static System.Action<AudioClip, float, Vector3> PlaySfx { get; set; }
}
