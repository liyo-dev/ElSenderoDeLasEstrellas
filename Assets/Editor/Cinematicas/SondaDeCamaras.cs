using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Diagnóstico (solo Editor): apunta qué cámaras pintan la pantalla en cada fotograma de Play y
/// escribe los fotogramas en que cambian en «Guiones/_informes/sonda_de_camaras.md». Sirve para
/// cazar una cámara que se cuela un fotograma suelto en un plano (parpadeo). Ver INC-1337.
public static class SondaDeCamaras
{
    private const int MaximoDeCambios = 400;
    private static bool s_registrando;
    private static int s_fotograma = -1;
    private static readonly List<string> s_actual = new();
    private static readonly List<string> s_detalle = new();
    private static string s_anterior = "";
    private static readonly StringBuilder s_informe = new();
    private static int s_cambios;
    private static readonly Dictionary<string, int> s_fotogramasPorCamara = new();
    private static readonly Dictionary<string, (Vector3 pos, Quaternion rot, float fov)> s_ultimaPose = new();
    private static int s_saltos;

    [MenuItem("El Sendero/Cinemáticas/Diagnóstico: registrar cámaras (en Play)", priority = 950)]
    public static void Empezar()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Sonda de cámaras] Solo en Play."); return; }
        if (s_registrando) return;
        s_registrando = true;
        s_informe.Clear(); s_actual.Clear(); s_detalle.Clear(); s_anterior = ""; s_cambios = 0; s_fotograma = -1;
        s_fotogramasPorCamara.Clear(); s_ultimaPose.Clear(); s_saltos = 0;
        RenderPipelineManager.beginCameraRendering += AlPintar;
        EditorApplication.playModeStateChanged += AlCambiarDePlay;
        Debug.Log("[Sonda de cámaras] Registrando.");
    }

    [MenuItem("El Sendero/Cinemáticas/Diagnóstico: parar y escribir cámaras", priority = 951)]
    public static void Parar()
    {
        if (!s_registrando) return;
        Cerrar();
        RenderPipelineManager.beginCameraRendering -= AlPintar;
        EditorApplication.playModeStateChanged -= AlCambiarDePlay;
        s_registrando = false;

        var sb = new StringBuilder();
        sb.AppendLine("# Sonda de cámaras");
        sb.AppendLine();
        sb.AppendLine("## Fotogramas pintados por cada cámara (a pantalla)");
        foreach (var kv in s_fotogramasPorCamara) sb.AppendLine($"- {kv.Key}: {kv.Value}");
        sb.AppendLine();
        sb.AppendLine("## Cambios (fotograma · tiempo · cámaras que pintan la pantalla)");
        sb.Append(s_informe);
        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Guiones", "_informes");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "sonda_de_camaras.md"), sb.ToString());
        Debug.Log("[Sonda de cámaras] Escrito en Guiones/_informes/sonda_de_camaras.md");
    }

    private static void AlCambiarDePlay(PlayModeStateChange cambio)
    {
        if (cambio == PlayModeStateChange.ExitingPlayMode) Parar();
    }

    private static void AlPintar(ScriptableRenderContext contexto, Camera camara)
    {
        if (camara == null || camara.cameraType != CameraType.Game || camara.targetTexture != null) return;
        if (Time.frameCount != s_fotograma) { Cerrar(); s_fotograma = Time.frameCount; }
        string clave = Ruta(camara.transform);
        s_actual.Add(clave);
        s_detalle.Add(clave + $" (fov {camara.fieldOfView:F0}, en {camara.transform.position:F1})");
        s_fotogramasPorCamara.TryGetValue(clave, out int n);
        s_fotogramasPorCamara[clave] = n + 1;
        ApuntarSalto(clave, camara);
    }

    /// Un salto de la misma cámara de un fotograma al siguiente (posición, giro o lente): así se ve
    /// un plano que se cuela un fotograma aunque lo pinte la misma cámara.
    private static void ApuntarSalto(string clave, Camera camara)
    {
        var t = camara.transform;
        var ahora = (t.position, t.rotation, camara.fieldOfView);
        if (s_ultimaPose.TryGetValue(clave, out var antes) && s_saltos < MaximoDeCambios)
        {
            float metros = Vector3.Distance(antes.pos, ahora.position);
            float grados = Quaternion.Angle(antes.rot, ahora.rotation);
            float lente = Mathf.Abs(antes.fov - ahora.fieldOfView);
            if (metros > 0.3f || grados > 4f || lente > 3f)
            {
                s_saltos++;
                s_informe.AppendLine($"- SALTO {Time.frameCount} · {Time.time:F2} s · {clave}: {antes.pos:F2} → {ahora.position:F2} ({metros:F2} m, {grados:F0}°, fov {antes.fov:F0}→{ahora.fieldOfView:F0})");
            }
        }
        s_ultimaPose[clave] = ahora;
    }

    private static void Cerrar()
    {
        if (s_fotograma < 0) return;
        string ahora = string.Join(" + ", s_actual);
        string detalle = string.Join(" + ", s_detalle);
        s_actual.Clear(); s_detalle.Clear();
        if (ahora == s_anterior || s_cambios >= MaximoDeCambios) return;
        s_anterior = ahora;
        s_cambios++;
        s_informe.AppendLine($"- {s_fotograma} · {Time.time:F2} s · {detalle}");
    }

    private static string Ruta(Transform t)
    {
        string r = t.name;
        for (var p = t.parent; p != null; p = p.parent) r = p.name + "/" + r;
        return t.gameObject.scene.name + ":" + r;
    }
}
