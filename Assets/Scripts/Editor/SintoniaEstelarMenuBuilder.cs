#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menús de Editor para probar e inspeccionar el sistema de Sintonía Estelar (INC-847).
/// </summary>
public static class SintoniaEstelarMenuBuilder
{
    [MenuItem("El Sendero/Progresión/Sintonía Estelar: sumar +50 puntos", priority = 10)]
    public static void Sumar50Puntos()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Sintonía Estelar] Esta acción requiere estar en Play Mode para interactuar con el sistema activo de juego.");
            return;
        }

        if (SintoniaEstelarService.Instance == null)
        {
            Debug.LogWarning("[SintoniaEstelarMenuBuilder] SintoniaEstelarService.Instance no está disponible.");
            return;
        }

        SintoniaEstelarService.Instance.SumarSintonia(50);
        Debug.Log($"[Sintonía Estelar] ✨ +50 puntos sumados. Progreso: {SintoniaEstelarService.Instance.PuntosActuales}/{SintoniaEstelarService.Instance.PuntosParaSiguienteHito} ({SintoniaEstelarService.Instance.FraccionProgreso:P0}) — Hitos: {SintoniaEstelarService.Instance.HitosAlcanzados} (Pendientes: {SintoniaEstelarService.Instance.HitosPendientes})");
    }

    [MenuItem("El Sendero/Progresión/Sintonía Estelar: forzar hito (abrir elección)", priority = 11)]
    public static void ForzarHito()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Sintonía Estelar] Esta acción requiere estar en Play Mode para abrir la ventana de elección.");
            return;
        }

        if (SintoniaEstelarService.Instance == null)
        {
            Debug.LogWarning("[SintoniaEstelarMenuBuilder] SintoniaEstelarService.Instance no está disponible.");
            return;
        }

        SintoniaEstelarService.Instance.ForzarHito();
        Debug.Log($"[Sintonía Estelar] 🌟 Hito forzado. Abriendo ventana de elección (Hitos alcanzados: {SintoniaEstelarService.Instance.HitosAlcanzados}).");
    }

    [MenuItem("El Sendero/Progresión/Sintonía Estelar: consultar estado", priority = 12)]
    public static void ConsultarEstado()
    {
        if (Application.isPlaying)
        {
            var svc = SintoniaEstelarService.Instance;
            if (svc == null)
            {
                Debug.LogWarning("[Sintonía Estelar] SintoniaEstelarService no está activo.");
                return;
            }

            var stats = EstadisticasDelPersonaje.Total;
            string msg = $"[Sintonía Estelar] ESTADO ACTUAL (Play Mode):\n" +
                         $"• Progreso actual: {svc.PuntosActuales} / {svc.PuntosParaSiguienteHito} puntos ({svc.FraccionProgreso:P1})\n" +
                         $"• Hitos alcanzados: {svc.HitosAlcanzados}\n" +
                         $"• Hitos pendientes de elección: {svc.HitosPendientes}\n" +
                         $"• Vida Máxima del grupo: {stats.vida:F0}\n" +
                         $"• Maná Máximo del grupo: {stats.magia:F0}\n" +
                         $"• En combate activo: {(svc.EstaEnCombate() ? "SÍ" : "NO")}";

            Debug.Log(msg);
        }
        else
        {
            var preset = UnlockService.GetActivePreset();
            if (preset == null)
            {
                var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
                if (profile != null)
                {
                    preset = profile.bootPreset != null ? profile.bootPreset : profile.defaultPlayerPreset;
                }
            }

            if (preset == null)
            {
                Debug.LogWarning("[Sintonía Estelar] No hay preset resuelto en este momento.");
                return;
            }

            string msg = $"[Sintonía Estelar] ESTADO EN PRESET ({preset.name}):\n" +
                         $"• Puntos acumulados: {preset.sintoniaPuntos}\n" +
                         $"• Hitos alcanzados: {preset.sintoniaHitos}\n" +
                         $"• Hitos pendientes: {preset.sintoniaHitosPendientes}\n" +
                         $"• Vida base en preset: {preset.maxHP}\n" +
                         $"• Maná base en preset: {preset.maxMP}";

            Debug.Log(msg);
        }
    }
}
#endif
