/// <summary>
/// Quitar y devolver el control al jugador desde el grafo. Mientras está bloqueado tampoco se ve
/// el HUD. El bloqueo y el desbloqueo pueden venir de nodos distintos: los empareja
/// <c>owner</c>.
/// </summary>
public interface INarrativePlayerControl
{
    void LockPlayer(object owner);

    void UnlockPlayer(object owner);
}
