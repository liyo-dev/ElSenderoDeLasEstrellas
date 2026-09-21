using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Carga una escena aditiva cuando se levanta una señal narrativa, y la descarga cuando se levanta
/// otra (o la misma, si 'unloadOnSignal' se deja vacío no se descarga nunca sola).
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El refactor de lore del 17 sep 2026 monta el prólogo (`SEQ_Prologo_UltimaNoche`) en su propia
/// escena aditiva (`Prologo_Valle.unity`, con coordenadas lejanas del resto del mapa) en vez de
/// dentro de `WillHouse.unity` — decisión de Raúl para no repetir el solape de escenas que ya tenía
/// `WillHouse`. Pero nada en el grafo narrativo sabe cargar o descargar una escena por señal: los
/// nodos `RaiseCustomEventNode`/`WaitCustomEventNode` solo levantan y esperan marcas, y los
/// portales (`InteriorPortalTrigger`) cargan al cruzar un trigger físico, no al levantar una señal
/// desde un grafo. Este componente es la pieza que falta, y se ha hecho genérica a propósito (regla
/// del catálogo de beats: lo que solo vale para una escena concreta no debería fijarse a mano en
/// código nuevo) para que sirva a cualquier futura secuencia que necesite su propia escena aditiva
/// sin escribir un script por caso.
///
/// Vive como componente persistente (p.ej. en `Start.unity`, junto al resto de sistemas globales
/// como `DefaultNarrativeSignals`), no dentro de la escena que carga: si viviera ahí, se
/// destruiría a sí mismo al descargar su propia escena a mitad de la corrutina.
public class SignalAdditiveSceneLoader : MonoBehaviour
{
    [Tooltip("Señal que dispara la carga (el mismo nombre que levanta un RaiseCustomEventNode del grafo, o un SignalBeat de una secuencia).")]
    [SerializeField] private string _loadOnSignal;

    [Tooltip("Nombre de la escena a cargar en aditivo. Debe estar en Build Settings.")]
    [SerializeField] private string _sceneToLoad;

    [Tooltip("Señal que dispara la descarga. Vacío = esta escena no se descarga sola (para escenas persistentes que solo se cargan una vez).")]
    [SerializeField] private string _unloadOnSignal;

    [Tooltip("Escena a descargar. Vacío = se descarga la misma que se cargó ('_sceneToLoad').")]
    [SerializeField] private string _sceneToUnload;

    private bool _loaded;

    private void OnEnable()
    {
        var signals = DefaultNarrativeSignals.EnsureInstance();
        if (!string.IsNullOrWhiteSpace(_loadOnSignal))
            signals.OnCustom(_loadOnSignal, HandleLoadSignal);
        if (!string.IsNullOrWhiteSpace(_unloadOnSignal))
            signals.OnCustom(_unloadOnSignal, HandleUnloadSignal);
    }

    private void OnDisable()
    {
        var signals = DefaultNarrativeSignals.Instance;
        if (signals == null) return;
        if (!string.IsNullOrWhiteSpace(_loadOnSignal))
            signals.OffCustom(_loadOnSignal, HandleLoadSignal);
        if (!string.IsNullOrWhiteSpace(_unloadOnSignal))
            signals.OffCustom(_unloadOnSignal, HandleUnloadSignal);
    }

    private void HandleLoadSignal()
    {
        // FIX (17 sep 2026, bug "el prólogo cargó pero nunca arrancó ninguna fase" -- confirmado
        // leyendo DefaultNarrativeSignals.OnCustom/RaiseCustom): la entrega "sticky" de una señal
        // sin oyentes es de UN SOLO CONSUMIDOR -- RaiseCustom() guarda la señal en _pending/_raised,
        // y el PRIMER OnCustom() que llega la retira de ahí y la entrega, sin volver a dejarla
        // disponible para nadie más. Eso vale para el caso de siempre (un único WaitCustomEventNode
        // esperando), pero esta clase introduce un caso nuevo: DOS oyentes distintos necesitan la
        // MISMA señal de carga -- este loader (para saber que hay que cargar la escena) y el
        // SequencePlayer que vive DENTRO de esa escena (para arrancar sus fases) -- y el segundo no
        // existe todavía en el instante del Raise: existe recién cuando la carga que ESTE handler
        // dispara termina. Sin este requeue, este loader se queda con la señal para siempre y el
        // SequencePlayer, al suscribirse más tarde, no encuentra ni _pending/_raised (ya vaciados)
        // ni un _custom con oyente (la señal nunca llegó a tener uno) -- se queda esperando un Raise
        // que no va a repetirse. RequeueCustom() (ya existía en DefaultNarrativeSignals, pensado
        // para un caso simétrico: "consumí la señal pero no era para mí") deja la señal otra vez en
        // _pending/_raised SIN invocar a nadie, lista para que el próximo OnCustom() de verdad
        // (el del SequencePlayer, en su Awake(), en cuanto la escena cargue) la reciba.
        DefaultNarrativeSignals.EnsureInstance().RequeueCustom(_loadOnSignal);
        StartCoroutine(Co_Load());
    }

    private void HandleUnloadSignal() => StartCoroutine(Co_Unload());

    private IEnumerator Co_Load()
    {
        if (_loaded || string.IsNullOrWhiteSpace(_sceneToLoad)) yield break;

        if (SceneManager.GetSceneByName(_sceneToLoad).isLoaded)
        {
            // Ya estaba cargada (p.ej. tras un reintento o una carga de partida a mitad de la
            // secuencia) — no hay nada que hacer, pero se marca igualmente para que la descarga
            // funcione cuando toque.
            _loaded = true;
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(_sceneToLoad, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[SignalAdditiveSceneLoader] No se pudo cargar '{_sceneToLoad}' — " +
                "¿está en Build Settings?");
            yield break;
        }

        yield return op;
        _loaded = true;
    }

    private IEnumerator Co_Unload()
    {
        string target = string.IsNullOrWhiteSpace(_sceneToUnload) ? _sceneToLoad : _sceneToUnload;
        if (string.IsNullOrWhiteSpace(target)) yield break;

        var scene = SceneManager.GetSceneByName(target);
        if (!scene.isLoaded) yield break;

        yield return SceneManager.UnloadSceneAsync(scene);
        _loaded = false;
    }
}
