/// Parte del estado proyectado propia de este juego: habilidades y hechizos del jugador.
/// Un INarrativeStateWriter que la conozca la expone con TryGetExtension; los nodos que la usan
/// (UnlockAbilitiesNode) no dependen de cómo se guarde.
public interface IPlayerLoadoutState
{
    void UnlockAbility(AbilityKey ability);

    /// equipInEmptySlot: además, ponerlo en la primera ranura libre si no está ya equipado.
    void UnlockSpell(SpellId spell, bool equipInEmptySlot);
}
