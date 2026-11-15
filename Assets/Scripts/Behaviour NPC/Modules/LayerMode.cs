namespace Game.NPC.Modules
{
    /// <summary>Capa de física que lleva un NPC (la cambia el combate al empezar).</summary>
    public enum LayerMode
    {
        Interactable = 0, // Capa "Interactable": se puede hablar con el NPC
        Enemy        = 1, // Capa "Enemy": necesaria para combatir
        Default      = 2, // Capa "Default": sin función propia
        Custom       = 3  // Se queda la capa que ya tenga el NPC
    }
}
