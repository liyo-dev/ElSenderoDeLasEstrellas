#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Diagnóstico temporal (INC-1337): en Play, mientras exista «Guiones/_ordenes/diag_camaras.on»,
/// apunta en «Logs/diag_camaras.txt» cada cámara de juego que dibuja a pantalla en cada fotograma
/// cuando cambia de cámara, de pose o de lente respecto al fotograma anterior.
[InitializeOnLoad]
public static class DiagnosticoDeCamaras
{
    static readonly string Bandera = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes", "diag_camaras.on");
    static readonly string Salida = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "diag_camaras.txt");
    static double s_proximaMirada;
    static bool s_activo;
    static Camera s_ultima;
    static Vector3 s_pos; static Quaternion s_rot; static float s_fov;
    static int s_frame = -1, s_enEsteFrame;
    static readonly StringBuilder s_sb = new();

    static DiagnosticoDeCamaras()
    {
        RenderPipelineManager.beginCameraRendering += AlDibujar;
        EditorApplication.update += Mirar;
    }

    static void Mirar()
    {
        if (EditorApplication.timeSinceStartup < s_proximaMirada) return;
        s_proximaMirada = EditorApplication.timeSinceStartup + 1.0;
        s_activo = Application.isPlaying && File.Exists(Bandera);
        if (s_sb.Length > 0) { File.AppendAllText(Salida, s_sb.ToString()); s_sb.Clear(); }
    }

    static void AlDibujar(ScriptableRenderContext ctx, Camera cam)
    {
        if (!s_activo || cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
        if (Time.frameCount != s_frame) { s_frame = Time.frameCount; s_enEsteFrame = 0; }
        s_enEsteFrame++;
        var t = cam.transform;
        bool cambia = cam != s_ultima || (t.position - s_pos).sqrMagnitude > 0.0025f
                      || Quaternion.Angle(t.rotation, s_rot) > 2f || Mathf.Abs(cam.fieldOfView - s_fov) > 0.5f || s_enEsteFrame > 1;
        if (cambia)
            s_sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.000} f{1} #{2} {3} [{4}] pos={5} rot={6} fov={7:0.0} depth={8}\n",
                Time.unscaledTime, Time.frameCount, s_enEsteFrame, cam.name, Ruta(t), t.position.ToString("F2"),
                t.eulerAngles.ToString("F1"), cam.fieldOfView, cam.depth);
        s_ultima = cam; s_pos = t.position; s_rot = t.rotation; s_fov = cam.fieldOfView;
    }

    static string Ruta(Transform t)
    {
        string r = t.name;
        for (var p = t.parent; p != null; p = p.parent) r = p.name + "/" + r;
        return t.gameObject.scene.name + ":" + r;
    }
}
#endif
