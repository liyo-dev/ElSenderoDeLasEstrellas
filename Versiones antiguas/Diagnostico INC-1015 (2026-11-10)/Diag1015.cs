#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// Diagnóstico temporal de INC-1015 (Eldran sigue al jugador en vez de esperar en la salida).
/// Se borra al cerrar la incidencia.
public static class Diag1015
{
    static readonly Dictionary<string, string> s_ultimo = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_ultimo.Clear();

    public static void Colocar(string actorId, string motivo)
    {
        if (s_ultimo.TryGetValue(actorId, out var m) && m == motivo) return;
        s_ultimo[actorId] = motivo;
        Debug.Log($"INC-1015 diag: agenda coloca a {actorId}: {motivo}");
    }

    public static string Llamadas(int cuantas = 5)
    {
        var st = new System.Diagnostics.StackTrace(2, false);
        var partes = new List<string>();
        for (int i = 0; i < st.FrameCount && partes.Count < cuantas; i++)
        {
            var m = st.GetFrame(i)?.GetMethod();
            if (m != null) partes.Add($"{m.DeclaringType?.Name}.{m.Name}");
        }
        return string.Join(" ← ", partes);
    }
}
#endif
