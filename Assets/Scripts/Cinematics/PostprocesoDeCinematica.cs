using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// El «look de peli» de las cinemáticas: mientras hay una secuencia en marcha, un volumen global
/// con prioridad alta pone encima de lo que tenga la escena el perfil
/// Resources/Postproceso/PerfilDeCinematica (menos rojo, grano de película, blancos contenidos,
/// detalle suavizado). Entra y sale con un fundido corto. En juego no se nota: el peso es 0.
///
/// No hay nada que montar en las escenas ni en las secuencias: se engancha solo al aviso
/// CinematicSequencerBase.OnAnySequenceActiveChanged, que ya cuenta las secuencias anidadas.
/// Para cambiar el look se edita el perfil; para quitarlo, se borra o se vacía.
public static class PostprocesoDeCinematica
{
    private const string RutaDelPerfil = "Postproceso/PerfilDeCinematica";
    private const float Prioridad = 500f;
    private const float FundidoDeEntrada = 0.6f;
    private const float FundidoDeSalida = 0.5f;

    private static Volume s_volumen;
    private static bool s_sinPerfil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Enganchar()
    {
        s_volumen = null;
        s_sinPerfil = false;
        CinematicSequencerBase.OnAnySequenceActiveChanged -= AlCambiar;
        CinematicSequencerBase.OnAnySequenceActiveChanged += AlCambiar;
    }

    /// ¿Es el volumen del look de cine? Los controladores de escena exclusivos (PostprocesoDeEscena)
    /// no lo silencian: el look de cine va siempre encima de lo que tenga la escena.
    public static bool EsSuVolumen(Volume v) => v != null && v == s_volumen;

    private static void AlCambiar(bool hayCinematica)
    {
        var volumen = Volumen();
        if (volumen == null) return;
        DOTween.Kill(volumen);
        if (hayCinematica) volumen.enabled = true;
        DOTween.To(() => volumen.weight, w => volumen.weight = w, hayCinematica ? 1f : 0f,
                   hayCinematica ? FundidoDeEntrada : FundidoDeSalida)
               .SetTarget(volumen)
               .SetUpdate(true)
               .OnComplete(() => { if (!hayCinematica && volumen != null) volumen.enabled = false; });
    }

    private static Volume Volumen()
    {
        if (s_volumen != null || s_sinPerfil) return s_volumen;
        var perfil = Resources.Load<VolumeProfile>(RutaDelPerfil);
        if (perfil == null)
        {
            s_sinPerfil = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PostprocesoDeCinematica] No está Resources/{RutaDelPerfil}: las cinemáticas se ven como el juego.");
#endif
            return null;
        }
        var go = new GameObject("Postproceso de cinemática");
        Object.DontDestroyOnLoad(go);
        s_volumen = go.AddComponent<Volume>();
        s_volumen.isGlobal = true;
        s_volumen.priority = Prioridad;
        s_volumen.sharedProfile = perfil;
        s_volumen.weight = 0f;
        s_volumen.enabled = false;
        return s_volumen;
    }
}
