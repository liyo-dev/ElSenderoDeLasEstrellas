#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Diagnóstico temporal de la cinemática final: en Play, si existe Logs/DiagFinal/activo, cada 3 s
/// guarda una captura de la Game view y una línea con el estado de render. Se retira al cerrar la incidencia.
[InitializeOnLoad]
public static class DiagnosticoCinematicaFinal
{
    private static double s_siguiente;
    private static int s_n;
    private static string Carpeta => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "DiagFinal");

    static DiagnosticoCinematicaFinal()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { s_n = 0; return; }
        if (!File.Exists(Path.Combine(Carpeta, "activo"))) return;
        if (EditorApplication.timeSinceStartup < s_siguiente) return;
        s_siguiente = EditorApplication.timeSinceStartup + 3.0;
        s_n++;
        string png = Path.Combine(Carpeta, $"f{s_n:000}.png");
        ScreenCapture.CaptureScreenshot(png);

        var sb = new StringBuilder();
        sb.Append($"#{s_n:000} t={Time.time:F1} ");
        sb.Append($"fog={RenderSettings.fog} mode={RenderSettings.fogMode} dens={RenderSettings.fogDensity:F4} lin={RenderSettings.fogStartDistance:F0}-{RenderSettings.fogEndDistance:F0} col={RenderSettings.fogColor} ");
        sb.Append($"sky={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")} amb={RenderSettings.ambientMode}/{RenderSettings.ambientLight} ");
        var cam = Camera.main;
        if (cam != null)
        {
            sb.Append($"\n   cam={cam.name} pos={cam.transform.position:F1} fwd={cam.transform.forward:F2} clear={cam.clearFlags} bg={cam.backgroundColor} near={cam.nearClipPlane} far={cam.farClipPlane} fov={cam.fieldOfView:F0} mask={cam.cullingMask}");
            float[] ys = { 0.9f, 0.7f, 0.5f, 0.3f, 0.1f };
            foreach (var y in ys)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, y, 0f));
                sb.Append($"\n   ray y={y}: ");
                var hits = Physics.RaycastAll(ray, 400f, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < Mathf.Min(4, hits.Length); i++)
                    sb.Append($"[{hits[i].collider.name} {hits[i].distance:F1}m] ");
            }
        }
        else sb.Append("\n   cam=null");
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
        {
            if (!v.enabled || v.weight <= 0f) continue;
            var p = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
            sb.Append($"\n   vol {v.name} global={v.isGlobal} w={v.weight:F2} prio={v.priority} perfil={(p != null ? p.name : "-")}: ");
            if (p != null) foreach (var c in p.components) if (c.active) sb.Append(c.GetType().Name + " ");
        }
        // Renderers grandes visibles (posibles cúpulas, planos o nieblas que tapen el cuadro)
        if (cam != null)
        {
            var planos = GeometryUtility.CalculateFrustumPlanes(cam);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || !r.isVisible) continue;
                var b = r.bounds;
                bool grande = b.size.magnitude > 60f;
                bool particula = r is ParticleSystemRenderer;
                if (!grande && !particula) continue;
                if (!GeometryUtility.TestPlanesAABB(planos, b)) continue;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name + "/" + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?") : "-";
                sb.Append($"\n   vis {(particula ? "PS" : "BIG")} {Ruta(r.transform)} b={b.center:F0}/{b.size:F0} mat={mat}");
            }
        }
        var jug = GameObject.Find("Player");
        if (jug != null) sb.Append($"\n   player {jug.transform.position:F1}");
        for (int k = 0; k < SceneManager.sceneCount; k++) sb.Append($" | escena {SceneManager.GetSceneAt(k).name}");
        File.AppendAllText(Path.Combine(Carpeta, "diag.txt"), sb.ToString() + "\n");
    }

    private static string Ruta(Transform t)
    {
        string s = t.name;
        for (var q = t.parent; q != null && s.Length < 120; q = q.parent) s = q.name + "/" + s;
        return s;
    }
}
#endif
