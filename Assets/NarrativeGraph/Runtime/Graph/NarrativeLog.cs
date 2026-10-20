using System.Diagnostics;

/// <summary>
/// Trazas de diagnóstico del grafo narrativo. Las llamadas —y la evaluación de sus
/// argumentos— desaparecen de los builds finales salvo que se compilen con
/// UNITY_EDITOR o UNITY_INCLUDE_INSTRUMENTATION, igual que un bloque #if.
/// Los avisos y errores de fallos reales siguen usando Debug.LogWarning/LogError.
/// </summary>
internal static class NarrativeLog
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("UNITY_INCLUDE_INSTRUMENTATION")]
    public static void Info(string message) => UnityEngine.Debug.Log(message);
}
