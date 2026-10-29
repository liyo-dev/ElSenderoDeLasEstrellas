using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga o descarga una escena en aditivo desde el propio grafo, para que se vea en él qué escena
/// trae cada tramo y en qué orden: primero se carga la escena (sus Awake/OnEnable ya han corrido,
/// así que lo que haya dentro ya escucha) y después el grafo sigue, p. ej. con la señal que la
/// pone en marcha.
///
/// Si la pantalla está tapada al empezar la carga, el telón (INarrativeScreen) la mantiene tapada
/// hasta que la escena está lista. Con la escena cargada se avisa al juego
/// (INarrativeSceneHooks) por si tiene que poblarla.
///
/// La escena tiene que estar en Build Settings. Si no lo está, o el nombre está vacío, el nodo
/// avisa y avanza igualmente: un nodo nunca debe dejar el grafo colgado.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Mundo", "Escena aditiva (cargar/descargar)",
    "Carga o descarga una escena en aditivo desde el grafo.")]
public sealed class AdditiveSceneNode : NarrativeNode
{
    public enum Operacion
    {
        /// Carga la escena en aditivo. Si ya estaba cargada, no hace nada.
        Cargar = 0,

        /// Descarga la escena. Si no estaba cargada, no hace nada.
        Descargar = 1,
    }

    [Tooltip("Qué hacer con la escena.")]
    public Operacion operacion = Operacion.Cargar;

    [Tooltip("Nombre de la escena, sin ruta ni extensión. Debe estar en Build Settings.")]
    public string sceneName;

    [Tooltip("Esperar a que la carga/descarga termine antes de avanzar al nodo siguiente. Déjalo " +
             "marcado si lo siguiente que hace el grafo depende de que la escena ya esté ahí, que " +
             "es el caso normal: cargar la escena y justo después emitir la señal que la pone en marcha.")]
    public bool waitForCompletion = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[AdditiveSceneNode:{guid}] ⚠️ 'sceneName' está vacío — no se hace nada y se avanza.");
            onReadyToAdvance?.Invoke();
            return;
        }

        var runner = ctx?.Runner;
        if (runner == null)
        {
            Debug.LogError($"[AdditiveSceneNode:{guid}] No hay runner — no se puede {operacion} '{sceneName}'.");
            onReadyToAdvance?.Invoke();
            return;
        }

        runner.StartCoroutine(operacion == Operacion.Cargar
            ? Co_Cargar(onReadyToAdvance)
            : Co_Descargar(onReadyToAdvance));
    }

    private IEnumerator Co_Cargar(Action onReadyToAdvance)
    {
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[AdditiveSceneNode:{guid}] '{sceneName}' ya estaba cargada → se avanza sin tocar nada.");
#endif
            onReadyToAdvance?.Invoke();
            yield break;
        }

        // Si la pantalla está tapada (arranque de partida, una carga), sigue tapada hasta que esta
        // escena esté cargada, para no enseñar el mundo a medio montar. En pleno juego no tapa
        // nada: solo alarga un telón que ya estaba cerrado.
        string claveTelon = "grafo:" + sceneName;
        var pantalla = NarrativeServices.Get<INarrativeScreen>();
        pantalla?.KeepCurtainClosed(claveTelon);

        // El try/catch queda fuera del yield: C# no permite 'yield' dentro de un catch (CS1631),
        // así que el fallo se anota en una variable y se resuelve después.
        AsyncOperation op = null;
        try
        {
            op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdditiveSceneNode:{guid}] Error al cargar '{sceneName}': {e.Message}");
        }

        if (op == null)
        {
            Debug.LogError($"[AdditiveSceneNode:{guid}] No se pudo cargar '{sceneName}' — " +
                "¿está en Build Settings? Se avanza igualmente para no colgar el grafo.");
            pantalla?.ReleaseCurtain(claveTelon);
            onReadyToAdvance?.Invoke();
            yield break;
        }

        if (!waitForCompletion) onReadyToAdvance?.Invoke();

        yield return op;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AdditiveSceneNode:{guid}] ✅ '{sceneName}' cargada en aditivo.");
#endif

        // Lo que la escena necesite del juego al llegar (p. ej. instanciar sus personajes) se hace
        // ahora, con sus marcadores ya registrados en su OnEnable.
        NarrativeServices.Get<INarrativeSceneHooks>()?.OnAdditiveSceneLoaded(sceneName);

        // Primero se avanza y después se suelta: si el nodo siguiente arranca una cinemática, en
        // ese mismo fotograma ya ha recogido el telón y la pantalla no llega a destaparse.
        if (waitForCompletion) onReadyToAdvance?.Invoke();
        pantalla?.ReleaseCurtain(claveTelon);
    }

    private IEnumerator Co_Descargar(Action onReadyToAdvance)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[AdditiveSceneNode:{guid}] '{sceneName}' no estaba cargada → nada que descargar.");
#endif
            onReadyToAdvance?.Invoke();
            yield break;
        }

        // Guardia: nunca dejar el juego sin escenas, ni descargar la escena activa (donde vive el
        // jugador). Ninguna de las dos debería pasar con una escena de narrativa, pero descargar
        // la activa por un nombre mal escrito deja el juego en negro sin decir por qué.
        if (SceneManager.sceneCount <= 1 || scene == SceneManager.GetActiveScene())
        {
            Debug.LogWarning($"[AdditiveSceneNode:{guid}] ⚠️ No se descarga '{sceneName}': es la escena activa " +
                "o la única cargada. Se avanza sin tocar nada.");
            onReadyToAdvance?.Invoke();
            yield break;
        }

        var op = SceneManager.UnloadSceneAsync(scene);
        if (op == null)
        {
            Debug.LogWarning($"[AdditiveSceneNode:{guid}] No se pudo descargar '{sceneName}'. Se avanza igualmente.");
            onReadyToAdvance?.Invoke();
            yield break;
        }

        if (!waitForCompletion) onReadyToAdvance?.Invoke();

        yield return op;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AdditiveSceneNode:{guid}] ✅ '{sceneName}' descargada.");
#endif

        if (waitForCompletion) onReadyToAdvance?.Invoke();
    }
}
