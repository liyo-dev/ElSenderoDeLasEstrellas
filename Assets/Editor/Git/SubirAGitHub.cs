#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;

/// Subir el proyecto a GitHub desde el Editor, sin terminal: lo usa Claude a través del buzón
/// (la VM de Cowork no tiene git-lfs ni las credenciales de Windows; este git sí).
/// Todo lo que pasa queda en «Guiones/_ordenes/git.log». Los comandos de git corren en segundo
/// plano para no bloquear el Editor.
///   El Sendero ▸ Git ▸ Estado                    rama, LFS, cambios y archivos de más de 50 MB
///   El Sendero ▸ Git ▸ Guardar escenas abiertas  guarda escenas y assets (nunca en Play)
///   El Sendero ▸ Git ▸ Subir todo                add -A, commit con «git_mensaje.txt» y push
public static class SubirAGitHub
{
    private static string Raiz => Directory.GetParent(UnityEngine.Application.dataPath).FullName;
    private static string Log => Path.Combine(Raiz, "Guiones", "_ordenes", "git.log");
    private static string Mensaje => Path.Combine(Raiz, "Guiones", "_ordenes", "git_mensaje.txt");
    private static bool s_ocupado;

    private static void Escribir(string texto)
    {
        lock (typeof(SubirAGitHub))
            File.AppendAllText(Log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {texto}\n", new UTF8Encoding(false));
    }

    private static (int codigo, string salida) Git(string args, int minutos = 30)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = Raiz,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        using var p = Process.Start(psi);
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(minutos * 60 * 1000)) { try { p.Kill(); } catch { } return (-1, "tiempo agotado"); }
        return (p.ExitCode, (o.Result + e.Result).Trim());
    }

    private static void EnSegundoPlano(string nombre, Action trabajo)
    {
        if (s_ocupado) { Escribir($"{nombre}: ya hay otra operación de git en marcha"); return; }
        s_ocupado = true;
        Escribir($"{nombre}: empieza");
        Task.Run(() =>
        {
            try { trabajo(); }
            catch (Exception ex) { Escribir($"{nombre}: error {ex.Message}"); }
            finally { s_ocupado = false; Escribir($"{nombre}: FIN"); }
        });
    }

    [MenuItem("El Sendero/Git/Estado")]
    public static void Estado() => EnSegundoPlano("estado", () =>
    {
        Escribir("lfs: " + Git("lfs version").salida);
        Escribir("fetch: " + Git("fetch origin").salida);
        Escribir("rama: " + Git("status -sb --porcelain=v1 -uno").salida.Split('\n')[0]);
        var s = Git("status --porcelain=v1 -uall", 10).salida.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int mod = 0, nuevos = 0, borr = 0;
        var grandes = new StringBuilder();
        var raicesNuevas = new System.Collections.Generic.SortedSet<string>();
        foreach (var l in s)
        {
            string est = l.Substring(0, 2), ruta = l.Substring(3).Trim('"');
            if (est == "??") { nuevos++; raicesNuevas.Add(ruta.Split('/')[0] + (ruta.Contains("/") ? "/" + ruta.Split('/')[1] : "")); }
            else if (est.Contains("D")) borr++; else mod++;
            try
            {
                string full = Path.Combine(Raiz, ruta);
                if (File.Exists(full) && new FileInfo(full).Length > 50L * 1024 * 1024)
                    grandes.Append($"{ruta} ({new FileInfo(full).Length / 1048576} MB); ");
            }
            catch { }
        }
        Escribir($"cambios: {mod} modificados, {nuevos} nuevos, {borr} borrados");
        Escribir("grandes (>50 MB): " + (grandes.Length == 0 ? "ninguno" : grandes.ToString()));
        Escribir("carpetas con archivos nuevos: " + string.Join(" | ", raicesNuevas));
    });

    [MenuItem("El Sendero/Git/Guardar escenas abiertas")]
    public static void GuardarEscenas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Escribir("guardar: el Editor está en Play; no guardo"); return; }
        var sb = new StringBuilder();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            sb.Append(esc.name).Append(esc.isDirty ? "*" : "").Append(' ');
        }
        bool ok = EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Escribir($"guardar: escenas abiertas ({sb}) → {(ok ? "guardadas" : "FALLÓ")}");
    }

    [MenuItem("El Sendero/Git/Subir todo")]
    public static void SubirTodo() => EnSegundoPlano("subir", () =>
    {
        if (!File.Exists(Mensaje)) { Escribir("subir: falta git_mensaje.txt"); return; }
        var a = Git("add -A", 30); Escribir($"add ({a.codigo}): " + Recortar(a.salida));
        if (a.codigo != 0) return;
        var c = Git($"commit -F \"{Mensaje}\"", 10); Escribir($"commit ({c.codigo}): " + Recortar(c.salida));
        if (c.codigo != 0) return;
        var p = Git("push origin main", 60); Escribir($"push ({p.codigo}): " + Recortar(p.salida));
        Escribir("HEAD: " + Git("log -1 --oneline").salida + " · " + Git("status -sb -uno").salida.Split('\n')[0]);
    });

    private static string Recortar(string s) => s.Length > 3000 ? s.Substring(0, 1500) + " … " + s.Substring(s.Length - 1500) : s;
}
#endif
