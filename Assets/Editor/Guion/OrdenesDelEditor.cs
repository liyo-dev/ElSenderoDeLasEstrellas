#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Buzón de órdenes para el Editor: deja un fichero «*.orden» en «Guiones/_ordenes/» y el Editor
/// lo ejecuta en cuanto puede, aunque su ventana no esté delante. Así Claude puede capturar,
/// hornear o probar sin tocar el ratón de Raúl.
///
/// Una orden por fichero (primera línea):
///   refrescar                               reimporta y recompila lo que haya cambiado
///   capturar Prologo_Valle                  captura del escenario
///   hornear Assets/.../X.guion.txt [storyboard]
///   menu El Sendero/Secuencias/...          ejecuta un menú del Editor
///   play / stop                             entra o sale del modo Play
///   cerca X Z [radio]                       lista lo que hay alrededor de un punto (todas las escenas)
///   escena Nombre [aditiva]                 abre una escena (solo si no hay cambios sin guardar)
///   camara_de_prueba                        en Play, crea una cámara principal si no hay ninguna
///   gemini RUTA.mp4 [fps=2]                 manda un vídeo a Gemini (resultado en Logs/Gemini/)
///
/// El resultado queda en «Guiones/_ordenes/registro.log» y el fichero pasa a «*.hecha».
[InitializeOnLoad]
public static class OrdenesDelEditor
{
    private static double s_siguiente;
    private static string Carpeta => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes");

    static OrdenesDelEditor()
    {
        EditorApplication.update += Revisar;
    }

    private static void Revisar()
    {
        if (EditorApplication.timeSinceStartup < s_siguiente) return;
        s_siguiente = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!Directory.Exists(Carpeta)) return;

        string[] ordenes;
        try { ordenes = Directory.GetFiles(Carpeta, "*.orden"); }
        catch { return; }
        if (ordenes.Length == 0) return;
        Array.Sort(ordenes, StringComparer.Ordinal);
        string fichero = ordenes[0];

        string orden;
        try { orden = File.ReadAllText(fichero).Trim(); }
        catch { return; }
        string hecha = Path.ChangeExtension(fichero, ".hecha");
        try
        {
            if (File.Exists(hecha)) File.Delete(hecha);
            File.Move(fichero, hecha);
        }
        catch { return; }

        string resultado;
        try { resultado = Ejecutar(orden); }
        catch (Exception ex) { resultado = "ERROR: " + ex.Message + "\n" + ex.StackTrace; }
        try
        {
            File.AppendAllText(Path.Combine(Carpeta, "registro.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {Path.GetFileName(fichero)} «{orden}» → {resultado}\n");
        }
        catch { }
    }

    private static string Cerca(string resto)
    {
        var p = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float x = float.Parse(p[0], ci), z = float.Parse(p[1], ci), r = p.Length > 2 ? float.Parse(p[2], ci) : 2f;
        var sb = new System.Text.StringBuilder($"{x} {z} r={r}\n");
        for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount; k++)
        {
            var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
            sb.Append($"  escena {esc.name} cargada={esc.isLoaded}\n");
            if (!esc.isLoaded) continue;
            foreach (var raiz in esc.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    var rr = t.GetComponent<Renderer>();
                    Vector3 c = rr != null ? rr.bounds.center : t.position;
                    float dx = c.x - x, dz = c.z - z;
                    if (dx * dx + dz * dz > r * r) continue;
                    if (rr == null && t.GetComponent<Collider>() == null && t.GetComponent<MeshFilter>() == null) continue;
                    string ruta = t.name; for (var q = t.parent; q != null; q = q.parent) ruta = q.name + "/" + ruta;
                    sb.Append($"    {ruta} activo={t.gameObject.activeInHierarchy} capa={LayerMask.LayerToName(t.gameObject.layer)}");
                    if (rr != null) sb.Append($" renderer={rr.GetType().Name} on={rr.enabled} b={rr.bounds.center:F1}/{rr.bounds.size:F1}");
                    var col = t.GetComponent<Collider>();
                    if (col != null) sb.Append($" collider={col.GetType().Name} on={col.enabled}");
                    sb.Append("\n");
                }
        }
        return sb.ToString();
    }

    private static string Ejecutar(string orden)
    {
        string linea = orden.Split('\n')[0].Trim();
        int espacio = linea.IndexOf(' ');
        string verbo = espacio < 0 ? linea : linea.Substring(0, espacio);
        string resto = espacio < 0 ? "" : linea.Substring(espacio + 1).Trim();
        switch (verbo)
        {
            case "gemini":
            {
                // La pregunta son las líneas que siguen a la primera (si las hay).
                int salto = orden.IndexOf('\n');
                string pregunta = salto < 0 ? null : orden.Substring(salto + 1).Trim();
                return GeminiDelBuzon.Empezar(resto, pregunta);
            }
            case "refrescar":
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return "refresco pedido";
            case "capturar":
            {
                var pc = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (pc.Length >= 5)
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    float zx = float.Parse(pc[2], ci), zz = float.Parse(pc[3], ci), zm = float.Parse(pc[4], ci);
                    return "captura en " + (CapturaDeEscenario.Capturar(pc[0], Rect.MinMaxRect(zx - zm, zz - zm, zx + zm, zz + zm), pc[1]) ?? "(falló)");
                }
                return "captura en " + (CapturaDeEscenario.Capturar(string.IsNullOrEmpty(resto) ? "Prologo_Valle" : resto) ?? "(falló)");
            }
            case "hornear":
            {
                bool storyboard = resto.EndsWith(" storyboard");
                string ruta = storyboard ? resto.Substring(0, resto.Length - " storyboard".Length).Trim() : resto;
                if (string.IsNullOrEmpty(ruta)) ruta = HorneadorDeGuion.RutaPrologo;
                HorneadorDeGuion.Hornear(ruta, storyboard);
                return HorneadorDeGuion.UltimoResumen ?? "horneado";
            }
            case "menu":
                return EditorApplication.ExecuteMenuItem(resto) ? "menú ejecutado" : "no existe ese menú";
            case "play":
                EditorApplication.EnterPlaymode();
                return "entrando en Play";
            case "stop":
                EditorApplication.ExitPlaymode();
                return "saliendo de Play";
            case "cerca":
                return Cerca(resto);
            case "escena":
            {
                if (Application.isPlaying) return "no se puede en Play";
                for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount; k++)
                {
                    var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
                    if (esc.isDirty) return $"la escena {esc.name} tiene cambios sin guardar; no la cierro";
                }
                var partes = resto.Split(' ');
                bool aditiva = partes.Length > 1 && partes[1] == "aditiva";
                foreach (var guid in AssetDatabase.FindAssets($"{partes[0]} t:Scene"))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(ruta) != partes[0]) continue;
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ruta,
                        aditiva ? UnityEditor.SceneManagement.OpenSceneMode.Additive : UnityEditor.SceneManagement.OpenSceneMode.Single);
                    return "abierta " + ruta;
                }
                return "no encuentro la escena " + partes[0];
            }
            case "camara_de_prueba":
            {
                if (!Application.isPlaying) return "solo en Play";
                if (Camera.main != null) return "ya hay cámara principal: " + Camera.main.name;
                var go = new GameObject("Cámara de prueba (orden)") { tag = "MainCamera" };
                go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                return "cámara de prueba creada";
            }
            default:
                return "orden desconocida";
        }
    }
}
#endif
