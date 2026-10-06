using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mientras haya una secuencia en marcha, ninguna cámara usa el occlusion culling horneado
/// (INC-601, 5 oct 2026).
///
/// El de MainWorld se hornea pensando en la cámara del jugador, a ras de suelo y por las calles.
/// Las cámaras de las secuencias se ponen en sitios a los que el jugador nunca llega (en alto,
/// detrás de una casa, fuera del pueblo) y ahí el mapa de oclusión se equivoca: quita trozos de
/// casas que sí se ven. En una cinemática hay pocos planos y bien encuadrados, así que no hace falta.
///
/// Lo activa CinematicSequencerBase al bloquear la primera cinemática y lo devuelve todo como
/// estaba al soltar la última. Se aplica también a las cámaras que empiezan a pintar a mitad de
/// la secuencia (por si cambia la cámara o se carga otra escena).
/// </summary>
public static class SinOcclusionEnSecuencias
{
    private static readonly Dictionary<Camera, bool> s_original = new();
    private static bool s_activo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDePintar;
        s_original.Clear();
        s_activo = false;
    }

    public static void Activar()
    {
        if (s_activo) return;
        s_activo = true;
        RenderPipelineManager.beginCameraRendering += AntesDePintar;
        foreach (var c in Camera.allCameras) Apagar(c);
    }

    public static void Desactivar()
    {
        if (!s_activo) return;
        s_activo = false;
        RenderPipelineManager.beginCameraRendering -= AntesDePintar;
        foreach (var par in s_original)
            if (par.Key != null) par.Key.useOcclusionCulling = par.Value;
        s_original.Clear();
    }

    private static void AntesDePintar(ScriptableRenderContext contexto, Camera c) => Apagar(c);

    private static void Apagar(Camera c)
    {
        if (c == null || !c.useOcclusionCulling) return;
        if (!s_original.ContainsKey(c)) s_original[c] = true;
        c.useOcclusionCulling = false;
    }
}
