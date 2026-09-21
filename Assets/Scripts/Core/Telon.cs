using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sendero.Core.Feedback;

/// El TELÓN: el único que decide cuándo la pantalla está lista para verse.
///
/// ── Por qué existe (21 sep 2026) ─────────────────────────────────────────────────────────────
/// «Cuando iniciamos partida nueva sale la habitación de Will y luego ya el prólogo. Tenemos
/// sistemas de guardas para las cámaras, para los sonidos, y queda raro, ensucia el juego.»
///
/// Mirando la grabación 11 medio segundo a medio segundo, antes del prólogo se colaban 2,5 s: el
/// cielo, la habitación de Will, el valle a oscuras y la plaza con el Mago Oscuro todavía en
/// medio. No era un sistema roto, eran CUATRO que decidían cada uno por su cuenta cuándo destapar:
/// la pantalla de carga destapaba al abrir la escena, WorldBootstrap destapaba en cuanto veía
/// arrancar una cinemática, la transición de entrada de la cinemática tapaba desde lo que hubiera
/// en pantalla y destapaba antes de que los primeros beats colocaran a nadie, y la música de la
/// habitación sonaba igual aunque no se viera nada. Tres fundidos a negro distintos, ninguno
/// sabía de los otros.
///
/// ── Cómo funciona ────────────────────────────────────────────────────────────────────────────
/// Cualquiera puede pedir «no destapes todavía» con una clave (`Cerrar("mundo")`) y la suelta al
/// acabar (`Soltar("mundo")`). La pantalla solo se destapa cuando YA NADIE lo pide, y ni siquiera
/// entonces al instante: se espera una gracia corta por si el siguiente sistema de la cadena lo
/// recoge en el mismo momento — que es exactamente lo que pasa entre «la escena ha cargado» y
/// «arranca la cinemática». Es el mismo relevo sin hueco que ya hace CameraDirectorService con la
/// cámara.
///
/// `Mantener(clave)` es la forma para quien NO debe tapar la pantalla por sí mismo pero, si ya
/// está tapada, necesita que siga así hasta terminar (un nodo que carga una escena en aditivo:
/// en pleno juego no debe tapar nada, pero durante el arranque sí debe alargar el negro).
///
/// Mientras el telón está cerrado, NADIE destapa: FeedbackService ignora cualquier fundido de
/// salida, y AudioService no arranca música de escena (se pone al abrirse).
///
/// Si alguien se olvida de soltar, la consola dice QUIÉN a los pocos segundos, y pasado un tope el
/// telón se abre igualmente: una pantalla negra para siempre es peor que cualquier parpadeo.
public static class Telon
{
    /// Tiempo que se espera, con el telón ya sin nadie, antes de empezar a destapar.
    private const float Gracia = 0.3f;

    /// Duración del fundido de salida.
    private const float Destape = 0.5f;

    /// A partir de cuánto se avisa en consola de quién lo tiene cerrado.
    private const float AvisarTras = 8f;

    /// A partir de cuánto se abre a la fuerza.
    private const float Tope = 25f;

    /// Clave del cierre de arranque: pantalla negra desde el primer fotograma del juego.
    private const string Arranque = "arranque";

    private static readonly Dictionary<string, float> s_quien = new();
    private static readonly HashSet<string> s_avisados = new();
    private static readonly List<string> s_buffer = new();
    private static bool s_enGracia;
    private static Coroutine s_abriendo;
    private static Coroutine s_vigilante;
    private static Runner s_runner;

    /// Se dispara justo cuando el telón empieza a abrirse (con la pantalla todavía negra).
    public static event System.Action AlAbrirse;

    /// ¿Está la pantalla retenida en negro? Incluye la gracia, en la que ya nadie la pide pero aún
    /// no se ha destapado: en ese margen cualquiera puede recogerla sin que se vea nada.
    public static bool Cerrado => s_quien.Count > 0 || s_enGracia;

    public static bool Retiene(string quien) => !string.IsNullOrEmpty(quien) && s_quien.ContainsKey(quien);

    // ── Ciclo de vida ────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        s_quien.Clear();
        s_avisados.Clear();
        s_enGracia = false;
        s_abriendo = null;
        s_vigilante = null;
        s_runner = null;
        AlAbrirse = null;
        SceneManager.sceneLoaded -= TrasLaPrimeraEscena;
    }

    /// Negro desde el primer fotograma. Se suelta en cuanto la primera escena ha despertado: si esa
    /// escena necesita más tiempo (WorldBootstrap colocando al jugador, el grafo cargando el valle)
    /// ya lo habrá pedido ella en su Awake/OnEnable, y si no (el menú) se destapa sola.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AlArrancarElJuego()
    {
        Cerrar(Arranque);
        SceneManager.sceneLoaded += TrasLaPrimeraEscena;
    }

    private static void TrasLaPrimeraEscena(Scene escena, LoadSceneMode modo)
    {
        SceneManager.sceneLoaded -= TrasLaPrimeraEscena;
        Correr().StartCoroutine(Co_SoltarTras(Arranque, 2));
    }

    private static IEnumerator Co_SoltarTras(string quien, int fotogramas)
    {
        for (int i = 0; i < fotogramas; i++) yield return null;
        Soltar(quien);
    }

    // ── API ──────────────────────────────────────────────────────────────────────────────────

    /// Tapa la pantalla YA (si no lo estaba) y la retiene hasta que se suelte esta clave.
    public static void Cerrar(string quien)
    {
        if (string.IsNullOrEmpty(quien)) return;

        bool nuevo = !s_quien.ContainsKey(quien);
        if (nuevo) s_quien[quien] = Time.unscaledTime;

        // Si estaba abriéndose (en gracia o a medio fundido), se corta y vuelve a negro.
        s_enGracia = false;
        if (s_abriendo != null && s_runner != null) s_runner.StopCoroutine(s_abriendo);
        s_abriendo = null;

        FeedbackService.SetScreenFadeImmediate(Color.black);

        if (s_vigilante == null) s_vigilante = Correr().StartCoroutine(Co_Vigilar());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (nuevo) Debug.Log($"[Telón] Cierra '{quien}'. Lo retienen: {Describir()}.");
#endif
    }

    /// Si la pantalla ya está retenida, la sigue reteniendo con esta clave. Si no, no hace nada:
    /// quien llama a esto no debe tapar la pantalla en pleno juego.
    public static bool Mantener(string quien)
    {
        if (!Cerrado) return false;
        Cerrar(quien);
        return true;
    }

    /// Suelta la clave. Si ya nadie retiene la pantalla, se destapa tras la gracia. Idempotente.
    public static void Soltar(string quien)
    {
        if (string.IsNullOrEmpty(quien) || !s_quien.Remove(quien)) return;
        s_avisados.Remove(quien);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Telón] Suelta '{quien}'." +
            (s_quien.Count > 0 ? $" Siguen reteniéndolo: {Describir()}." : " Nadie más: se abre."));
#endif

        if (s_quien.Count == 0) AbrirTrasLaGracia();
    }

    public static string Describir()
    {
        if (s_quien.Count == 0) return "(nadie)";
        var sb = new StringBuilder();
        float ahora = Time.unscaledTime;
        foreach (var kv in s_quien)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(kv.Key).Append(" (").Append((ahora - kv.Value).ToString("F1")).Append(" s)");
        }
        return sb.ToString();
    }

    // ── Interno ──────────────────────────────────────────────────────────────────────────────

    private static void AbrirTrasLaGracia()
    {
        if (s_abriendo != null && s_runner != null) s_runner.StopCoroutine(s_abriendo);
        s_abriendo = Correr().StartCoroutine(Co_Abrir());
    }

    private static IEnumerator Co_Abrir()
    {
        s_enGracia = true;
        yield return new WaitForSecondsRealtime(Gracia);
        s_enGracia = false;

        if (s_quien.Count > 0) { s_abriendo = null; yield break; }   // alguien lo recogió

        AlAbrirse?.Invoke();
        yield return FeedbackService.ScreenFadeAsync(Color.black, Destape, fadeIn: false);
        s_abriendo = null;
    }

    private static IEnumerator Co_Vigilar()
    {
        var espera = new WaitForSecondsRealtime(1f);
        while (s_quien.Count > 0)
        {
            yield return espera;

            float ahora = Time.unscaledTime;
            s_buffer.Clear();
            foreach (var kv in s_quien)
            {
                float lleva = ahora - kv.Value;
                if (lleva >= Tope) s_buffer.Add(kv.Key);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else if (lleva >= AvisarTras && s_avisados.Add(kv.Key))
                    Debug.LogWarning($"[Telón] '{kv.Key}' lleva {lleva:F0} s con la pantalla en negro. " +
                        $"Lo retienen: {Describir()}. Si no la suelta, se abrirá sola a los {Tope:F0} s.");
#endif
            }

            foreach (var quien in s_buffer)
            {
                Debug.LogError($"[Telón] '{quien}' no ha soltado la pantalla en {Tope:F0} s: se abre a " +
                    "la fuerza. Mejor un parpadeo que una pantalla negra para siempre. Hay que mirar " +
                    "por qué no llegó a su Soltar().");
                Soltar(quien);
            }
        }
        s_vigilante = null;
    }

    private static Runner Correr()
    {
        if (s_runner != null) return s_runner;
        var go = new GameObject("_Telon");
        Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
        return s_runner;
    }

    private sealed class Runner : MonoBehaviour { }
}
