using UnityEngine;

/// <summary>
/// 🔧 INSTALADOR AUTOMÁTICO DE DIAGNÓSTICO
/// 
/// Este script se ejecuta automáticamente al iniciar el juego (en modo desarrollo)
/// y crea los GameObjects necesarios para el diagnóstico de sincronización.
/// 
/// NO es necesario agregarlo manualmente a ninguna escena.
/// </summary>
public static class ProfileReadyDiagnosticsInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Solo en modo desarrollo (no en builds finales)
        if (!Debug.isDebugBuild && !Application.isEditor)
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
