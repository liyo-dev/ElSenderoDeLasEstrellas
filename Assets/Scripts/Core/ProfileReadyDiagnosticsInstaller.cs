using UnityEngine;

/// <summary>
/// 🔧 INSTALADOR AUTOMÁTICO DE DIAGNÓSTICO
/// 
/// Este script se ejecuta automáticamente al iniciar el juego (en modo desarrollo)
/// y crea los GameObjects necesarios para el diagnóstico de sincronización.
/// 
/// NO es necesario agregarlo manualmente a ninguna escena.
///
/// Apagado por defecto (INC-448): llenaba el log de cada Play con ~224 líneas. Se enciende y se
/// apaga con El Sendero ▸ Debug ▸ Diagnóstico de arranque (ProfileReady).
/// </summary>
public static class ProfileReadyDiagnosticsInstaller
{
    private const string Clave = "Diagnostico.ProfileReady";

#if UNITY_EDITOR
    [UnityEditor.MenuItem("El Sendero/Debug/Diagnóstico de arranque (ProfileReady)")]
    private static void Alternar()
    {
        bool activo = PlayerPrefs.GetInt(Clave, 0) == 1;
        PlayerPrefs.SetInt(Clave, activo ? 0 : 1);
        Debug.Log($"[ProfileReadyDiagnostics] Diagnóstico de arranque {(activo ? "APAGADO" : "ENCENDIDO")} para los próximos Play.");
    }

    [UnityEditor.MenuItem("El Sendero/Debug/Diagnóstico de arranque (ProfileReady)", true)]
    private static bool AlternarValidar()
    {
        UnityEditor.Menu.SetChecked("El Sendero/Debug/Diagnóstico de arranque (ProfileReady)", PlayerPrefs.GetInt(Clave, 0) == 1);
        return true;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Solo en modo desarrollo (no en builds finales)
        if (!Debug.isDebugBuild && !Application.isEditor)
            return;
        if (PlayerPrefs.GetInt(Clave, 0) != 1)
            return;

        // Verificar si ya existe ProfileReadyDiagnostics en la escena
        if (Object.FindAnyObjectByType<ProfileReadyDiagnostics>() == null)
        {
            var diagnosticsGO = new GameObject("[ProfileReadyDiagnostics]");
            diagnosticsGO.AddComponent<ProfileReadyDiagnostics>();
            Object.DontDestroyOnLoad(diagnosticsGO);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ProfileReadyDiagnosticsInstaller] ✅ ProfileReadyDiagnostics instalado automáticamente");
#endif
        }

        // Verificar si ya existe ProfileReadySubscriptionAnalyzer en la escena
        if (Object.FindAnyObjectByType<ProfileReadySubscriptionAnalyzer>() == null)
        {
            var analyzerGO = new GameObject("[ProfileReadySubscriptionAnalyzer]");
            analyzerGO.AddComponent<ProfileReadySubscriptionAnalyzer>();
            Object.DontDestroyOnLoad(analyzerGO);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ProfileReadyDiagnosticsInstaller] ✅ ProfileReadySubscriptionAnalyzer instalado automáticamente");
#endif
        }
    }
}
