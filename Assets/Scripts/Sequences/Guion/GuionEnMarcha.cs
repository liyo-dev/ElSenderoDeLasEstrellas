using System;
using UnityEngine;

/// Qué guion se está reproduciendo ahora mismo.
///
/// Es el punto único donde se pregunta «¿qué se está viendo?». Lo usan las herramientas del
/// Editor (el reproductor con su barra de tiempo) para no tener que buscar el guion por su
/// cuenta ni adivinar a qué secuencia pertenece.
public static class GuionEnMarcha
{
    private static ReproductorDeGuion s_actual;

    /// El guion que se está reproduciendo, o null si no hay ninguno.
    public static ReproductorDeGuion Actual => s_actual;

    /// Se levanta cuando empieza un guion o cuando termina.
    public static event Action Cambió;

    public static void Registrar(ReproductorDeGuion reproductor)
    {
        if (reproductor == null || s_actual == reproductor) return;
        s_actual = reproductor;
        Cambió?.Invoke();
    }

    public static void Quitar(ReproductorDeGuion reproductor)
    {
        if (reproductor == null || s_actual != reproductor) return;
        s_actual = null;
        Cambió?.Invoke();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_actual = null; Cambió = null; }
#endif
}
