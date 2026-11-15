/// <summary>
/// Lo que el juego necesita hacer cuando el grafo carga una escena en aditivo (p. ej. poblarla de
/// personajes). Es opcional: sin implementación, la escena se carga igual.
/// </summary>
public interface INarrativeSceneHooks
{
    /// <summary>Se llama con la escena ya cargada y sus Awake/OnEnable ejecutados, antes de que el grafo avance.</summary>
    void OnAdditiveSceneLoaded(string sceneName);
}
