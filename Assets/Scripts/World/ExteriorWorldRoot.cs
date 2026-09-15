using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Se coloca en el GameObject raíz del mundo exterior (p. ej. "ExteriorWorld" en MainWorld.unity).
/// Se registra a sí mismo por nombre en un diccionario estático para que EnvironmentController
/// pueda ocultarlo/mostrarlo al entrar/salir de un interior SIN usar GameObject.Find ni
/// GameObject.FindWithTag (prohibido en el proyecto — ver AGENTS.md § 2 / TDD.md § 12: "usar
/// registros"). Ese Find recorría toda la escena en cada cruce de anchor, y además
/// GameObject.FindWithTag lanza una excepción si el string no es un Tag registrado en el Tag
/// Manager — con "ExteriorWorld" como nombre normal (no tag), esa llamada fallaba en silencio o
/// con excepción sin capturar, dejando _hiddenExterior en null: el mundo se ocultaba pero nunca se
/// podía restaurar con seguridad al volver a salir (bug "al salir hay que activar el mundo antes").
///
/// Mismo patrón que AnchorRegistry (SpawnAnchor) y PlayerService: coste de resolución pagado una
/// sola vez (aquí en Awake), lookup O(1) después.
///
/// FIX (15 sep 2026, pedido de Raúl): hasta ahora EnvironmentController ocultaba este root entero
/// con GameObject.SetActive(false). Eso apagaba TODO lo que cuelga de aquí, no solo el
/// renderizado: ningún Update()/FixedUpdate() corre, ningún NavMeshAgent avanza, y ninguna
/// coroutine en marcha puede seguir ni pararse limpiamente. Un NPC del root "AI" (p. ej. Oliver)
/// congelado a mitad de una cinemática al entrar el jugador en un interior dejaba esa coroutine
/// tirada, y cualquier intento posterior de lanzar otra sobre él
/// (CinematicSequencerBase.RequestSkip/RequestSkipAll → StartCoroutine) fallaba en consola con
/// "Coroutine couldn't be started because the game object 'Oliver' is inactive" (conversación
/// 15 sep 2026). Ahora este componente solo apaga el RENDERIZADO (Renderer.enabled +
/// Light.enabled + Terrain.enabled) vía SetVisible(): el mundo exterior deja de verse desde dentro
/// del interior exactamente igual que antes — nada de skybox/copas de árboles colándose por las
/// paredes — pero la simulación de todo lo que cuelga de este root (IA, física, coroutines) sigue
/// corriendo con normalidad, así que no puede quedar nada "a medias". Se registra en Awake (no
/// OnEnable) y solo se quita del registro en OnDestroy, igual que antes.
/// </summary>
[DisallowMultipleComponent]
public class ExteriorWorldRoot : MonoBehaviour
{
    private static readonly Dictionary<string, ExteriorWorldRoot> _byName = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _byName.Clear();
#endif

    // Cacheados una sola vez en Awake para no pagar GetComponentsInChildren en cada cruce de
    // anchor (mismo criterio de coste-una-vez/lookup-O(1) que el registro por nombre de arriba).
    private Renderer[] _renderers;
    private Light[] _lights;
    // Además de Renderer: el Terrain (hierba/árboles pintados con el pincel de detalle, no son
    // GameObjects propios) se dibuja desde el propio componente Terrain, no desde un Renderer —
    // Renderer.enabled no le afecta nada. FIX (15 sep 2026, pedido de Raúl): la vegetación del
    // terreno seguía viéndose por detrás de las paredes del interior tras el cambio a
    // Renderer.enabled/Light.enabled porque no se estaba tocando esto.
    private Terrain[] _terrains;
    private bool _hidden;

    /// <summary>True mientras este root está oculto (última llamada fue SetVisible(false)).</summary>
    public bool IsHidden => _hidden;

    void Awake()
    {
        _byName[gameObject.name] = this;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _lights = GetComponentsInChildren<Light>(true);
        _terrains = GetComponentsInChildren<Terrain>(true);
    }

    void OnDestroy()
    {
        if (_byName.TryGetValue(gameObject.name, out var existing) && existing == this)
            _byName.Remove(gameObject.name);
    }

    /// <summary>
    /// Oculta (false) o muestra (true) el renderizado de este root sin desactivar ningún
    /// GameObject — ver el FIX de 15 sep 2026 en el comentario de la clase para el motivo.
    /// Guard de estado ya incluido (no repite trabajo si ya está en el estado pedido; cumple
    /// TDD.md § 12 también para este toggle, no solo para SetActive).
    /// </summary>
    public void SetVisible(bool visible)
    {
        bool hide = !visible;
        if (_hidden == hide) return;
        _hidden = hide;

        if (_renderers != null)
            foreach (var r in _renderers)
                if (r) r.enabled = visible;

        if (_lights != null)
            foreach (var l in _lights)
                if (l) l.enabled = visible;

        // Terrain.enabled apaga el dibujado del heightmap + árboles/hierba de detalle a la vez;
        // el TerrainCollider es un componente aparte y no se toca (no hace falta: el jugador no
        // puede llegar a chocar con este terreno mientras está en un interior).
        if (_terrains != null)
            foreach (var t in _terrains)
                if (t) t.enabled = visible;
    }

    /// <summary>Busca un ExteriorWorldRoot ya registrado por el nombre de su GameObject (O(1), sin Find).</summary>
    public static bool TryGet(string name, out GameObject root)
    {
        root = null;
        if (string.IsNullOrEmpty(name)) return false;
        if (_byName.TryGetValue(name, out var comp) && comp)
        {
            root = comp.gameObject;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Devuelve TODOS los ExteriorWorldRoot registrados en este momento. Usado por
    /// EnvironmentController.ApplyZoneVisibility() en vez de resolver por nombre uno a uno (14 sep
    /// 2026, pedido de Raúl): el propio componente ExteriorWorldRoot ya es la marca de "esto es
    /// mundo exterior, ocúltame al entrar en un interior/cinemática" — no hace falta, además,
    /// mantener una lista de nombres a mano en cada AnchorEnvironment.
    /// </summary>
    public static List<ExteriorWorldRoot> AllRegistered()
    {
        var list = new List<ExteriorWorldRoot>(_byName.Count);
        foreach (var comp in _byName.Values)
            if (comp) list.Add(comp);
        return list;
    }
}
