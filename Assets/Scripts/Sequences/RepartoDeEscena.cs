using System;
using System.Collections.Generic;
using UnityEngine;

/// Los personajes que ahora están en manos de una escena (cinemática). La escena manda en la
/// pantalla y sabe dónde va cada uno, así que:
///   - al tomar a un personaje, quien lo tuviera retirado del mundo lo devuelve ya, sin esperar a
///     que el jugador no mire (<see cref="Tomado"/>);
///   - mientras la escena lo tiene, nadie más lo retira ni lo cambia de sitio
///     (<see cref="EstaTomado"/>);
///   - al soltarlo, cada sistema vuelve a hacer lo suyo con él.
/// Lo llena <see cref="SequenceContext"/> con cada actor que resuelve y lo vacía el
/// <see cref="SequencePlayer"/> al cerrar, así que vale igual para todas las secuencias.
public static class RepartoDeEscena
{
    static readonly Dictionary<string, int> s_tomados = new();

    /// Una escena acaba de tomar a este personaje: hay que tenerlo en el mundo ya.
    public static event Action<string> Tomado;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstaticos()
    {
        s_tomados.Clear();
        Tomado = null;
    }
#endif

    public static bool EstaTomado(string actorId)
        => !string.IsNullOrEmpty(actorId) && s_tomados.ContainsKey(actorId);

    public static void Tomar(string actorId)
    {
        if (string.IsNullOrEmpty(actorId) || actorId == SequenceActor.PlayerId) return;
        s_tomados.TryGetValue(actorId, out int veces);
        s_tomados[actorId] = veces + 1;
        if (veces == 0) Tomado?.Invoke(actorId);
    }

    public static void Soltar(string actorId)
    {
        if (string.IsNullOrEmpty(actorId) || !s_tomados.TryGetValue(actorId, out int veces)) return;
        if (veces <= 1) s_tomados.Remove(actorId);
        else s_tomados[actorId] = veces - 1;
    }
}
