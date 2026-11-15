#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// Diagnóstico temporal: reproduce el paso de Oliver y apunta el estado de las bandas de cine.
/// Ver INC-1233. Se retira en cuanto termine.
public static class zzDiagnosticoOliver
{
    static double _fin, _siguiente;
    static int _n;

    [MenuItem("zz/Diagnóstico Oliver")]
    public static void Go()
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap1.asset");
        var l = new Sendero.Narrative.Editor.SenderoPlayLauncher();
        string msg;
        bool ok = l.Launch(g, "32febd1e-bce7-478d-aab9-792eddddd73d", null, out msg);
        Debug.Log($"[zz] launch={ok} {msg}");
        _fin = EditorApplication.timeSinceStartup + 45;
        _siguiente = EditorApplication.timeSinceStartup + 6;
        _n = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        double t = EditorApplication.timeSinceStartup;
        if (t >= _siguiente)
        {
            _siguiente = t + 2;
            _n++;
            var b = BandasDeCineUI.Instance;
            string estado = b == null
                ? "null"
                : $"altVis={b.AlturaVisible:0.00} altObj={b.AlturaObjetivo:0.00} " +
                  $"activa={b.gameObject.activeInHierarchy} padre={b.transform.parent?.name}";
            Debug.Log($"[zz] {_n * 2}s bandas: {estado} · enPantalla={PresentadorDeLinea.HayAlgoEnPantalla} · guion={(GuionEnMarcha.Actual != null)}");
            Directory.CreateDirectory("Guiones/_fotos");
            ScreenCapture.CaptureScreenshot(Path.Combine("Guiones", "_fotos", $"oliver_{_n:00}.png"));
        }
        if (t >= _fin)
        {
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("[zz] fin");
        }
    }
}
#endif
