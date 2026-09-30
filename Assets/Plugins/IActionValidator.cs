/// <summary>
/// Permisos de acción que el controlador de personaje (ensamblado de Plugins) consulta al juego
/// sin depender de sus tipos. Lo implementa PlayerActionManager.
/// </summary>
public interface IActionValidator
{
    bool CanJump();
    bool CanSprint();
}
