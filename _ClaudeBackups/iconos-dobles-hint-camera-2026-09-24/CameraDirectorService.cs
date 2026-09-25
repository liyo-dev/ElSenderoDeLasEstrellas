using System.Collections;
using UnityEngine;

/// <summary>
/// Árbitro central de "quién tiene el control de la cámara cinemática" (el flag estático
/// <see cref="vThirdPersonCamera.lockCameraForCinematic"/>).
///
/// Motivación: hoy cada sistema de cámara (CinematicCameraDriver, FocusCameraNode,
/// KingdomExitTransitionNode, BossIntroPresentation, etc.) pone y quita ese flag por su cuenta.
/// Cuando dos sistemas se entregan el control casi en el mismo instante (ej: una secuencia
/// termina y, uno o dos frames después, un nodo del grafo narrativo corta a otro plano),
/// el hueco entre "suelto el flag" y "el siguiente sistema lo vuelve a tomar" lo ocupa
/// vThirdPersonCamera, que retoma el control de gameplay durante ese instante — el salto
/// brusco a modo gameplay que se ve entre dos cortes que deberían sentirse como uno solo.
///
/// Este servicio no sustituye a vThirdPersonCamera ni reimplementa el corte de cámara de nadie:
/// solo decide CUÁNDO se suelta realmente el flag. Los sistemas de cámara deben:
///   - Al tomar el control:  CameraDirectorService.Claim(this)   en vez de poner el flag a true.
///   - Al soltarlo:          CameraDirectorService.Release(this) en vez de ponerlo a false.
///
/// Claim() cancela cualquier liberación pendiente de forma inmediata (aunque sea de otro owner),
/// así que si un segundo sistema reclama la cámara mientras el primero todavía está "soltando",
/// el flag nunca llega a bajar y vThirdPersonCamera nunca ve un frame para meter baza.
/// Release() no baja el flag al instante: espera <see cref="ReleaseGraceSeconds"/> por si alguien
/// reclama la cámara en ese margen (coalescing). Si nadie la reclama, entonces sí se libera y
/// vThirdPersonCamera recupera el control con su propio suavizado existente (_doSmoothSnap).
///
/// Migración incremental (ver CLAUDE.md §7 sobre por qué no se toca todo de golpe): mientras no
/// todos los sistemas de cámara pasen por aquí, el flag de vThirdPersonCamera sigue siendo la
/// única fuente de verdad real. Los sistemas que aún no se han migrado a Claim()/Release() siguen
/// funcionando exactamente igual que antes; para ellos este servicio es invisible.
/// </summary>
public static class CameraDirectorService
{
    /// Ventana de gracia por defecto tras un Release(): si nadie reclama la cámara en este
    /// margen, se suelta de verdad. Deliberadamente corta (solo cubre la latencia normal de
    /// procesado de eventos/grafo entre un "suelto" y un "reclamo" inmediato); no está pensada
    /// para tapar transiciones largas — para eso sigue siendo correcto usar el patrón
    /// Co_EndCinematicStayBlack + FeedbackService.IsScreenFaded (pantalla cubierta, así que da
    /// igual cuánto tarde el siguiente sistema en reclamar).
    private const float ReleaseGraceSeconds = 0.15f;

    private static object s_currentOwner;
    private static bool s_saliendo;
    private static Coroutine s_pendingRelease;
    private static Runner s_runner;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_currentOwner = null;
        s_pendingRelease = null;
        s_runner = null;
        s_saliendo = false;
    }
#endif

    /// Crea el GameObject "CameraDirectorService" de forma EAGER, antes de que cargue
    /// cualquier escena. Antes se creaba de forma perezosa dentro de EnsureRunner(), la
    /// primera vez que algún sistema llamaba a Release() — pero Release() se llama a menudo
    /// desde OnDestroy (ver CinematicCameraDriver.OnDestroy, BossIntroPresentation, etc.). Si
    /// esa primera llamada coincidía con el cierre/descarga de una escena, el "new
    /// GameObject(...)" ocurría literalmente durante el teardown de la escena, y Unity lo
    /// reportaba como "Some objects were not cleaned up when closing the scene (Did you spawn
    /// new GameObjects from OnDestroy?)" señalando a "CameraDirectorService". Al crear el
    /// runner aquí, ya existe en la escena persistente (DontDestroyOnLoad) mucho antes de que
    /// ninguna escena empiece a cerrarse, así que ese hueco desaparece.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        s_saliendo = false;
        Application.quitting -= AlSalir;
        Application.quitting += AlSalir;
        EnsureRunner();
    }

    /// En el Editor, `Application.quitting` también salta al salir de Play.
    private static void AlSalir() => s_saliendo = true;

    /// True si hay algún owner con el control reclamado en este momento (incluida la ventana de gracia).
    public static bool HasOwner => s_currentOwner != null;

    /// Fuerza el reseteo completo del estado interno (owner + liberación pendiente) y suelta el
    /// flag real, ignorando quién sea el owner actual. Pensado para los mismos puntos de entrada
    /// seguros de sesión que GameBootService.ResetTransientSessionState() (MainMenu, arranque de
    /// partida): ahí es seguro asumir que no hay ninguna cámara bloqueada legítimamente en curso,
    /// así que no hace falta (ni conviene) respetar el ownership actual como sí hace Release().
    /// Sin esto, un owner de una sesión anterior que nunca llegó a soltar (ej:
    /// KingdomExitTransitionNode.CloseDemo(), que deja el candado true a propósito porque la
    /// escena va a cambiar) dejaría s_currentOwner apuntando para siempre a un objeto ya
    /// destruido de la sesión anterior.
    public static void ForceResetState()
    {
        CancelPendingRelease();
        s_currentOwner = null;
        vThirdPersonCamera.lockCameraForCinematic = false;
    }

    /// Reclama el control de la cámara cinemática para <paramref name="owner"/>. Cancela
    /// cualquier liberación pendiente (propia o de otro owner) antes de que llegue a aplicarse,
    /// de forma que el flag nunca baja entre un handoff y el siguiente.
    public static void Claim(object owner)
    {
        if (owner == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_currentOwner != null && !Equals(s_currentOwner, owner))
            Debug.Log($"[CameraDirectorService] '{Describe(owner)}' reclama la cámara mientras seguía " +
                $"marcada como de '{Describe(s_currentOwner)}' (con o sin liberación pendiente) — relevo " +
                "normal entre sistemas, no un error por sí solo.");
#endif
        CancelPendingRelease();
        s_currentOwner = owner;
        vThirdPersonCamera.lockCameraForCinematic = true;
    }

    /// Pide soltar el control en nombre de <paramref name="owner"/>. Solo tiene efecto si
    /// <paramref name="owner"/> es quien tiene el control ahora mismo. No libera al instante:
    /// programa la liberación real tras ReleaseGraceSeconds para dar tiempo a un posible
    /// siguiente Claim() a coalescer con este handoff.
    public static void Release(object owner)
    {
        if (owner == null) return;

        // DIAGNÓSTICO (20 sept 2026, Raúl: "al terminar todas las secuencias siempre se queda
        // mal la cámara y debe cambiar a la de gameplay"): por lectura de código no se ha
        // encontrado ningún sistema que reclame la cámara sin pasar por Claim()/Release() (salvo
        // KingdomExitTransitionNode, a propósito, documentado arriba) — pero SI un segundo owner
        // se hubiera colado como dueño actual sin que este owner se enterase, Release() haría
        // aquí un `return` COMPLETAMENTE SILENCIOSO y el flag se quedaría bloqueado para
        // siempre, con "cámara que nunca vuelve al gameplay" como único síntoma visible. Este
        // aviso hace ruidoso justo ese caso, en vez de dejarlo pasar sin rastro: si aparece en
        // consola, apunta directamente a quién es el dueño real que está bloqueando la cámara.
        if (!Equals(s_currentOwner, owner))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[CameraDirectorService] Release() IGNORADO: '{Describe(owner)}' pide soltar la " +
                $"cámara, pero el dueño actual es '{Describe(s_currentOwner)}'. Si la cámara se queda bloqueada " +
                "en modo cinemático a partir de aquí, el dueño real es este, no quien intentó soltarla.");
#endif
            return;
        }

        CancelPendingRelease();
        EnsureRunner();

        // Cerrando la escena o saliendo de Play no hay runner (ni debe crearse, ver EnsureRunner):
        // se suelta al instante, que es lo único que tiene sentido sin frames por delante.
        if (s_runner == null)
        {
            s_currentOwner = null;
            vThirdPersonCamera.lockCameraForCinematic = false;
            return;
        }

        s_pendingRelease = s_runner.StartCoroutine(Co_DeferredRelease(owner));
    }

    private static IEnumerator Co_DeferredRelease(object owner)
    {
        yield return new WaitForSecondsRealtime(ReleaseGraceSeconds);
        // Si nadie ha reclamado la cámara durante la ventana de gracia, soltar de verdad.
        if (Equals(s_currentOwner, owner))
        {
            s_currentOwner = null;
            vThirdPersonCamera.lockCameraForCinematic = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CameraDirectorService] Cámara devuelta al gameplay (dueño soltado: '{Describe(owner)}').");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.Log($"[CameraDirectorService] Liberación de '{Describe(owner)}' superada por un Claim() " +
                $"nuevo de '{Describe(s_currentOwner)}' durante la ventana de gracia — coalescido sin soltar " +
                "el flag, comportamiento esperado.");
        }
#endif
        s_pendingRelease = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static string Describe(object owner)
    {
        if (owner == null) return "(nadie)";
        if (owner is UnityEngine.Object uo)
            return uo == null ? $"{owner.GetType().Name} (destruido)" : $"{owner.GetType().Name} '{uo.name}'";
        return owner.GetType().Name;
    }
#endif

    private static void CancelPendingRelease()
    {
        if (s_pendingRelease != null && s_runner != null)
            s_runner.StopCoroutine(s_pendingRelease);
        s_pendingRelease = null;
    }

    private static void EnsureRunner()
    {
        if (s_runner != null) return;

        // INC-401: «al parar la escena siempre sale "Some objects were not cleaned up…
        // CameraDirectorService"». El runner se crea al arrancar (Bootstrap), pero al salir de
        // Play Unity destruye primero los DontDestroyOnLoad; luego el CinematicCameraDriver llama
        // a Release() desde su OnDestroy, el runner ya está destruido (== null) y aquí se creaba
        // OTRO en pleno cierre. Un runner que existió y ya no existe significa exactamente eso:
        // se está cerrando todo. No se recrea.
        if (!ReferenceEquals(s_runner, null) || s_saliendo || !Application.isPlaying) return;

        var go = new GameObject("CameraDirectorService");
        Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
    }

    /// MonoBehaviour mínimo: una clase estática no puede alojar coroutines, así que este
    /// componente solo existe para darle un StartCoroutine/StopCoroutine al servicio.
    private class Runner : MonoBehaviour
    {
    }
}
