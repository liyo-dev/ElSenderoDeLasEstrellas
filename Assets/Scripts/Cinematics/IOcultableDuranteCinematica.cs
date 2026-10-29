using System.Collections.Generic;
using UnityEngine;

/// Lo que se esconde mientras se reproduce una cinemática (el HUD, el minimapa, el indicador
/// de la hora, los avisos de ArbitroDeAvisos) declara esta interfaz y se apunta en el registro
/// de abajo. Lo usan igual las cinemáticas, la intro de un jefe y los textos dramáticos.
///
/// Cada sistema lleva su propia cuenta de ocultados, así que pueden coexistir varias razones
/// para taparlo a la vez: una cinemática, un aviso, el icono de saltar.
public interface IOcultableDuranteCinematica
{
    /// Pide que este sistema se esconda. La clave identifica quién lo pide, para poder
    /// liberarlo después sin afectar a los demás.
    void Ocultar(object quien);

    /// Libera la petición de la clave que pidió esconderlo.
    void Mostrar(object quien);
}

/// El registro de los sistemas que se esconden durante una cinemática.
///
/// Quien tapa la pantalla no los conoce: se limita a pedir que se escondan todos los que estén
/// apuntados. Un sistema nuevo se esconde solo simplemente implementando la interfaz y
/// apuntándose aquí, sin tocar el reproductor.
public static class OcultablesDeCinematica
{
    private static readonly List<IOcultableDuranteCinematica> s_registrados = new();

    public static void Registrar(IOcultableDuranteCinematica ocultable)
    {
        if (ocultable == null) return;
        if (s_registrados.Contains(ocultable)) return;
        s_registrados.Add(ocultable);
    }

    public static void Quitar(IOcultableDuranteCinematica ocultable)
    {
        if (ocultable == null) return;
        s_registrados.Remove(ocultable);
    }

    /// Esconde todo lo registrado. Devuelve cuántos se escondieron: si no es ninguno, es que
    /// la escena se ha arrancado de una forma en la que no hay HUD (el síntoma clásico de
    /// empezar por la escena de una cinemática en vez de por Start).
    public static int OcultarTodos(object quien)
    {
        LimpiarMuertos();
        for (int i = 0; i < s_registrados.Count; i++)
        {
            try { s_registrados[i].Ocultar(quien); }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[OcultablesDeCinematica] Un sistema no se ha podido esconder: {ex.Message}");
#endif
            }
        }
        return s_registrados.Count;
    }

    public static void MostrarTodos(object quien)
    {
        LimpiarMuertos();
        for (int i = 0; i < s_registrados.Count; i++)
        {
            try { s_registrados[i].Mostrar(quien); }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[OcultablesDeCinematica] Un sistema no se ha podido volver a mostrar: {ex.Message}");
#endif
            }
        }
    }

    /// Los sistemas destruidos (cambio de escena) se quedan apuntados si no se limpian.
    private static void LimpiarMuertos()
    {
        for (int i = s_registrados.Count - 1; i >= 0; i--)
        {
            if (s_registrados[i] is Object o && o == null) s_registrados.RemoveAt(i);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_registrados.Clear();
}
