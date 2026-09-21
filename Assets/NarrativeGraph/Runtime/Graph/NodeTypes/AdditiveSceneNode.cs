using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga o descarga una escena en aditivo DESDE EL PROPIO GRAFO.
///
/// ── Por qué existe (17 sep 2026) ──────────────────────────────────────────────────────────────
/// Hasta ahora, una escena aditiva de narrativa se cargaba con `SignalAdditiveSceneLoader`: un
/// componente colocado en `Start.unity`, configurado con la señal que la dispara. Eso tenía tres
/// problemas, y los tres los resuelve tener un nodo:
///
///   1. **Era invisible en el grafo.** Que `PROLOGUE_START` además cargara `Prologo_Valle` no se
///      veía en ningún nodo — vivía dentro de un GameObject de `Start.unity`.
///   2. **Obligaba a un apaño en el bus de señales.** El loader CONSUMÍA la señal de arranque, y el
///      `SequencePlayer` que vive DENTRO de la escena cargada necesitaba esa misma señal para
///      arrancar sus fases; de ahí el `RequeueCustom()` de `SignalAdditiveSceneLoader`. Con este
///      nodo el orden es explícito y no hay carrera: primero se carga la escena (y sus Awake() ya
///      se han ejecutado, así que el `SequencePlayer` ya está suscrito), y DESPUÉS un
///      `RaiseCustomEventNode` levanta la señal. Un único consumidor.
///   3. **Cada escena aditiva nueva obligaba a tocar `Start.unity`**, que es la escena más delicada
///      del proyecto (todos los managers persistentes).
///
/// NO sustituye a `WorldBootstrap.AnchorHomeScene` ni a `InteriorPortalTrigger`: esos cargan
/// interiores por razones de spawn y de gameplay, no de narrativa. Este nodo es una pieza que se
/// suma, no una unificación.
///
/// La escena tiene que estar en Build Settings. Si no lo está, o el nombre está vacío, el nodo
/// avisa y AVANZA IGUALMENTE — un nodo nunca debe dejar el grafo colgado.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Mundo", "Escena aditiva (cargar/descargar)",
    "Carga o descarga una escena en aditivo desde el grafo, sin depender de un componente en Start.unity.")]
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

    [Tooltip("Nombre de la escena, sin ruta ni extensión (p. ej. 'Prologo_Valle'). Debe estar en Build Settings.")]
    public string sceneName;

    [Tooltip("Esperar a que la carga/descarga termine antes de avanzar al nodo siguiente. Déjalo " +
             "marcado si lo siguiente que hace el grafo depende de que la escena ya esté ahí — que " +
             "es el caso normal: cargar el valle y justo después levantar la señal que arranca su " +
             "secuencia.")]
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AdditiveSceneNode:{guid}] '{sceneName}' ya estaba cargada → se avanza sin tocar nada.");
#endif
            onReadyToAdvance?.Invoke();
            yield break;
        }

        // (21 sep) Si la pantalla está en negro (arranque de partida, una carga), se mantiene en
        // negro hasta que esta escena esté cargada: es lo que impedía ver la habitación de Will y
        // el valle a medio montar antes del prólogo. En pleno juego no tapa nada — Mantener solo
        // alarga un telón que ya estaba cerrado. Ver Telon.
        string claveTelon = "grafo:" + sceneName;
        Telon.Mantener(claveTelon);

        // El try/catch se queda FUERA del yield a propósito: C# no permite 'yield return' ni
        // 'yield break' dentro del cuerpo de un catch (CS1631), así que el fallo se anota en una
        // variable y se resuelve después, ya fuera del bloque.
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
            Telon.Soltar(claveTelon);
            onReadyToAdvance?.Invoke();
            yield break;
        }

        if (!waitForCompletion) onReadyToAdvance?.Invoke();

        yield return op;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AdditiveSceneNode:{guid}] ✅ '{sceneName}' cargada en aditivo.");
#endif

        // FIX (18 sep 2026): NpcSpawner.SpawnAllFromResources() solo se llamaba una vez, desde
        // WorldBootstrap.InitializeWorld() -- es decir, SOLO para lo que ya está cargado en el
        // arranque de MainWorld. Una escena que se añade más tarde por este nodo (como
        // Prologo_Valle) trae sus propios NpcSpawnPoint, pero esos marcadores se registran en su
        // OnEnable() DESPUÉS de que WorldBootstrap ya haya intentado emparejarlos con el roster --
        // así que cualquier NpcRosterSO.Entry cuyo spawnId viva en una escena aditiva nunca llegaba
        // a instanciarse (NpcSpawnRegistry.Get devolvía null en su único intento, en silencio salvo
        // por un warning). Volver a llamar aquí, con la escena ya cargada y sus OnEnable ya
        // ejecutados, es seguro y no duplica nada: NpcSpawner._spawned ya evita reinstanciar lo que
        // el arranque de MainWorld colocó.
        NpcSpawner.SpawnAllFromResources();

        // Primero se avanza y DESPUÉS se suelta: si el nodo siguiente arranca una cinemática, en
        // ese mismo fotograma ya ha recogido el telón y la pantalla no llega a destaparse.
        if (waitForCompletion) onReadyToAdvance?.Invoke();
        Telon.Soltar(claveTelon);
    }

    private IEnumerator Co_Descargar(Action onReadyToAdvance)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AdditiveSceneNode:{guid}] ✅ '{sceneName}' descargada.");
#endif

        if (waitForCompletion) onReadyToAdvance?.Invoke();
    }
}
