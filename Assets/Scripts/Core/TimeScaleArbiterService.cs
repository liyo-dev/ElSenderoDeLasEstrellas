using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dueño único de <see cref="Time.timeScale"/>. Nadie más lo escribe: cada sistema que necesita
/// otra velocidad del tiempo (pausa de un menú, hitstop, cámara lenta de una cinemática o de la
/// muerte) la pide con su clave y la suelta al terminar, y el árbitro decide cuál manda.
///
/// Uso:
/// <list type="bullet">
/// <item><see cref="Request"/>(clave, escala) mientras se necesite. Pedir otra vez con la misma
/// clave actualiza su valor (no se acumula): así se anima una rampa (cámara lenta que entra
/// poco a poco) pidiendo cada fotograma el valor del momento.</item>
/// <item><see cref="Release"/>(clave) al terminar, y también en OnDisable/OnDestroy: es seguro
/// llamarlo aunque no hubiera petición.</item>
/// <item><see cref="TryGetRequest"/> para leer lo que pidió una clave (p. ej. el punto de
/// partida de una rampa) sin depender del valor efectivo, que puede estar pisado por otro.</item>
/// </list>
///
/// Regla de reparto: manda la petición más lenta, contando el <see cref="SetBaseline">baseline</see>
/// (1 por defecto). Una pausa (0) nunca la levanta un hitstop que termina ni al revés, y dos
/// hitstops solapados no dejan cámara lenta permanente. Las peticiones por encima de 1
/// (acelerar) solo se aplican cuando nadie pide ir más lento: entonces manda la más rápida.
///
/// Claves que mueren: si la clave es un <see cref="UnityEngine.Object"/> destruido (un menú de una
/// escena descargada, una cinemática que se destruye a mitad), su petición se descarta sola en el
/// siguiente reparto, y el reparto se repite al descargarse cada escena. Así una escena que se va
/// no puede dejar el juego en pausa o en cámara lenta, sin que nadie tenga que forzar el tiempo a 1
/// «por si acaso» (lo que anularía la pausa legítima de otro sistema).
///
/// <see cref="ReleaseAll"/> es solo para la recuperación de emergencia (deja el tiempo en reposo
/// sin ninguna petición). Ver C6 en TDD § 19.
/// </summary>
public static class TimeScaleArbiterService
{
    /// Velocidad normal del juego: por debajo es ir más lento; por encima, acelerar.
    private const float VelocidadNormal = 1f;

    /// Tope de Unity para Time.timeScale.
    private const float EscalaMaxima = 100f;

    private class RequestEntry
    {
        public object Owner;
        public float Scale;
    }

    private static readonly List<RequestEntry> _requests = new();
    private static float _baseline = VelocidadNormal;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _requests.Clear();
        _baseline = VelocidadNormal;
        Time.timeScale = VelocidadNormal;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EscucharDescargasDeEscena()
    {
        SceneManager.sceneUnloaded -= AlDescargarEscena;
        SceneManager.sceneUnloaded += AlDescargarEscena;
    }

    // Las claves destruidas con la escena se descartan al repartir.
    private static void AlDescargarEscena(Scene _) => Apply();

    /// <summary>
    /// Pide un timeScale mientras <paramref name="owner"/> lo mantenga. Si esa clave ya tenía una
    /// petición, se actualiza su valor. Aplica el resultado de inmediato.
    /// </summary>
    public static void Request(object owner, float scale)
    {
        if (owner == null) return;
        scale = Mathf.Clamp(scale, 0f, EscalaMaxima);

        for (int i = 0; i < _requests.Count; i++)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                _requests[i].Scale = scale;
                Apply();
                return;
            }
        }

        _requests.Add(new RequestEntry { Owner = owner, Scale = scale });
        Apply();
    }

    /// <summary>
    /// Libera la petición de <paramref name="owner"/>, si la tenía. No hace nada si no la tenía,
    /// así que se puede llamar en cualquier ruta de limpieza sin comprobar antes.
    /// </summary>
    public static void Release(object owner)
    {
        if (owner == null) return;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                _requests.RemoveAt(i);
                break;
            }
        }
        Apply();
    }

    /// <summary>
    /// Lo que pide ahora <paramref name="owner"/>. False si no tiene petición activa.
    /// </summary>
    public static bool TryGetRequest(object owner, out float scale)
    {
        scale = VelocidadNormal;
        if (owner == null) return false;
        for (int i = 0; i < _requests.Count; i++)
        {
            if (!ReferenceEquals(_requests[i].Owner, owner)) continue;
            scale = _requests[i].Scale;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cambia el timeScale de reposo, el que manda cuando nadie pide ir más lento. Entra en el
    /// reparto como una petición más: un baseline de 0 no lo levanta ninguna petición.
    /// </summary>
    public static void SetBaseline(float scale)
    {
        _baseline = Mathf.Clamp(scale, 0f, EscalaMaxima);
        Apply();
    }

    /// <summary>
    /// Recuperación de emergencia: descarta todas las peticiones y vuelve el baseline a la
    /// velocidad normal. Solo para cuando el jugador se ha quedado bloqueado sin salida; en el
    /// flujo normal cada sistema suelta lo suyo con <see cref="Release"/>.
    /// </summary>
    public static void ReleaseAll()
    {
        _requests.Clear();
        _baseline = VelocidadNormal;
        Apply();
    }

    private static void Apply()
    {
        float masLenta = _baseline;
        float masRapida = _baseline;
        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (_requests[i].Owner is UnityEngine.Object clave && clave == null)
            {
                _requests.RemoveAt(i);
                continue;
            }
            float escala = _requests[i].Scale;
            if (escala < masLenta) masLenta = escala;
            if (escala > masRapida) masRapida = escala;
        }
        Time.timeScale = masLenta < VelocidadNormal ? masLenta : masRapida;
    }
}
