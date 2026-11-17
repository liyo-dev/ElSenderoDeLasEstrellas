#if UNITY_EDITOR
using System.IO;
using Liyodev.Cinematics;
using UnityEditor;
using UnityEngine;

/// Diagnóstico de un solo uso (INC-1275): comprueba que el manual del guion y el estado que lee el
/// asistente de cinemáticas se generan sin errores. Deja el resultado en Logs/asistente_cine.txt.
public static class DiagnosticoAsistenteCine
{
    [MenuItem("El Sendero/Cinemáticas/Probar lectura del asistente")]
    public static void Probar()
    {
        var puente = CinematicsEditorServices.Bridge;
        if (puente == null) { Debug.LogError("[Diag] sin puente de cinemáticas"); return; }

        string manual = puente.ManualDeGuion();
        string estado = puente.EstadoParaAsistente("Cap3_03_ElCarro", 5, 27.4f);
        string ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "asistente_cine.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllText(ruta,
            "MANUAL (" + manual.Length + " chars)\n\n" + manual +
            "\n\n===== ESTADO DE Cap3_03_ElCarro (P06, 27.4 s) =====\n\n" + estado);
        Debug.Log($"[Diag] manual {manual.Length} chars; estado {estado.Length} chars; escrito en {ruta}");
    }
}
#endif
