/// Una escena que decide ella misma qué pasa cuando cae el jugador (los laboratorios de prueba
/// vuelven a su zona en vez de cargar la partida). Se registra en GameOverManager; si responde,
/// no se ofrece continuar desde el último guardado.
public interface IRespuestaALaDerrota
{
    /// Se llama al acabar la caída. Devuelve true si se ha hecho cargo.
    bool Responder();
}
