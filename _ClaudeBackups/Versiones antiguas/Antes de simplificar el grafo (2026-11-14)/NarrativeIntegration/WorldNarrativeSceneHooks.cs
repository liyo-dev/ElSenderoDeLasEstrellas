/// <summary>
/// Al cargar el grafo una escena en aditivo, instancia los NPCs del roster cuyos puntos de
/// aparición viven en ella. WorldBootstrap solo lo hace al arrancar el mundo, antes de que esa
/// escena registre sus NpcSpawnPoint; NpcSpawner no duplica lo ya instanciado.
/// </summary>
public sealed class WorldNarrativeSceneHooks : INarrativeSceneHooks
{
    public void OnAdditiveSceneLoaded(string sceneName) => NpcSpawner.SpawnAllFromResources();
}
