#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Mide lo que tarda la partida en verse desde que se pulsa Play: cuándo vuelve a cargarse el
/// código, cuándo llega cada escena, qué fotogramas se quedan parados y cada mensaje de consola
/// con su momento, hasta que el telón se abre. Lo deja en Logs/claude/arranque_play.txt y, al
/// lado, el trozo de Editor.log de esa entrada (arranque_play_editorlog.txt), que trae el
/// desglose de Unity de la recarga del código y de la carga de escenas. Ver INC-1210.
[InitializeOnLoad]
public static class MedirArranqueDePlay
{
    const string MenuPath = "El Sendero/Pruebas/Medir el arranque al dar a Play";
    const string ClaveActivo = "Sendero.MedirArranqueDePlay";
    const string ClaveInicio = "MedirArranqueDePlay.Inicio";
    const string ClaveLog = "MedirArranqueDePlay.Log";
    const string ClaveLineas = "MedirArranqueDePlay.Lineas";
    const string Ruta = "Logs/claude/arranque_play.txt";
    const string RutaLog = "Logs/claude/arranque_play_editorlog.txt";

    const double FotogramaLentoMs = 250;
    const double TrasAbrirseS = 3;
    const double TopeS = 180;
    const int MaxLineas = 2500;
    const int MaxLogBytes = 3 * 1024 * 1024;

    static double s_ultimoTick;
    static double s_abiertoEn = -1;
    static bool s_vioCerrado;
    static int s_lineas;
    static readonly StringBuilder s_apuntes = new();

    static MedirArranqueDePlay()
    {
        EditorApplication.playModeStateChanged += AlCambiarDeModo;
        AssemblyReloadEvents.beforeAssemblyReload += Guardar;
        if (!Midiendo) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Una medida que se quedó a medias (el Play no llegó a entrar): se descarta.
            SessionState.EraseString(ClaveInicio);
            return;
        }
        s_apuntes.Append(SessionState.GetString(ClaveLineas, string.Empty));
        Apuntar("Código recargado (dominio nuevo)");
        Engancharse();
    }

    static bool Activo => EditorPrefs.GetBool(ClaveActivo, true);
    static bool Midiendo => !string.IsNullOrEmpty(SessionState.GetString(ClaveInicio, string.Empty));

    [MenuItem(MenuPath)]
    static void Alternar() => EditorPrefs.SetBool(ClaveActivo, !Activo);

    [MenuItem(MenuPath, true)]
    static bool AlternarCheck()
    {
        Menu.SetChecked(MenuPath, Activo);
        return true;
    }

    static void AlCambiarDeModo(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingEditMode && Activo)
        {
            SessionState.SetString(ClaveInicio, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetString(ClaveLineas, string.Empty);
            SessionState.SetString(ClaveLog, TamanoDelEditorLog().ToString());
            s_apuntes.Clear();
            s_lineas = 0;
            Apuntar("Play pulsado");
            Engancharse();
            return;
        }
        if (!Midiendo) return;
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            Apuntar("Entra en Play (escenas despiertas)");
            s_ultimoTick = Ahora();
            s_abiertoEn = -1;
            s_vioCerrado = false;
            EditorApplication.update -= CadaTick;
            EditorApplication.update += CadaTick;
        }
        else if (estado == PlayModeStateChange.ExitingPlayMode)
        {
            Apuntar("Se para el Play antes de terminar de medir");
            Terminar();
        }
    }

    static void Engancharse()
    {
        Application.logMessageReceived -= AlMensaje;
        Application.logMessageReceived += AlMensaje;
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void Soltarse()
    {
        Application.logMessageReceived -= AlMensaje;
        SceneManager.sceneLoaded -= AlCargarEscena;
        EditorApplication.update -= CadaTick;
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (Midiendo) Apuntar($"Escena cargada: {escena.name} ({modo})");
    }

    static void AlMensaje(string texto, string pila, LogType tipo)
    {
        if (!Midiendo || string.IsNullOrEmpty(texto)) return;
        int salto = texto.IndexOf('\n');
        var linea = salto >= 0 ? texto.Substring(0, salto) : texto;
        if (linea.Length > 220) linea = linea.Substring(0, 220) + "…";
        string marca = tipo == LogType.Log ? "·" : tipo == LogType.Warning ? "!" : "✖";
        Apuntar($"{marca} {linea}");
    }

    static void CadaTick()
    {
        if (!Midiendo) { EditorApplication.update -= CadaTick; return; }
        double ahora = Ahora();
        double hueco = (ahora - s_ultimoTick) * 1000.0;
        s_ultimoTick = ahora;
        if (hueco >= FotogramaLentoMs) Apuntar($"⏸ Fotograma {Time.frameCount} parado {hueco:0} ms");

        if (Telon.Cerrado) s_vioCerrado = true;
        else if (s_abiertoEn < 0 && (s_vioCerrado || Time.frameCount > 5)
                 && !EsEscenaDeCarga(SceneManager.GetActiveScene().name))
        {
            s_abiertoEn = ahora;
            Apuntar("Telón abierto: se ve el juego");
        }

        if ((s_abiertoEn >= 0 && ahora - s_abiertoEn >= TrasAbrirseS) || Transcurrido() >= TopeS)
        {
            if (s_abiertoEn < 0) Apuntar($"Pasados {TopeS:0} s sin abrirse el telón: se deja de medir");
            Terminar();
        }
    }

    static void Terminar()
    {
        Soltarse();
        try { Escribir(); }
        catch (Exception e) { Debug.LogWarning("[Arranque] No se pudo escribir la medida: " + e.Message); }
        SessionState.EraseString(ClaveInicio);
        SessionState.EraseString(ClaveLineas);
        SessionState.EraseString(ClaveLog);
        s_apuntes.Clear();
    }

    // ── Apuntes (en memoria; pasan la recarga del código guardados en SessionState) ──────────

    static void Apuntar(string texto)
    {
        if (s_lineas >= MaxLineas) return;
        s_lineas++;
        s_apuntes.Append(Transcurrido().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(texto).Append('\n');
    }

    static void Guardar()
    {
        if (Midiendo) SessionState.SetString(ClaveLineas, s_apuntes.ToString());
    }

    static double Transcurrido()
    {
        if (!long.TryParse(SessionState.GetString(ClaveInicio, string.Empty), out var ticks)) return 0;
        return (DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond;
    }

    static double Ahora() => EditorApplication.timeSinceStartup;

    /// Las pantallas de arranque y el menú no cuentan como «se ve el juego»: el telón de
    /// `arranque` se abre sobre SplashScreen, el de la carga sobre LoadingScreen y el del menú
    /// sobre MainMenu. Antes se paraba la medida ahí y no se llegaba a medir la escena de verdad
    /// (INC-1210). El mundo de la partida (MainWorld, Prologo_Valle…) sí cuenta.
    static bool EsEscenaDeCarga(string nombre) =>
        nombre == "SplashScreen" || nombre == "LoadingScreen" || nombre == "Start" || nombre == "MainMenu";

    static void Escribir()
    {
        var lineas = s_apuntes.ToString().Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine($"Arranque al dar a Play · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Escena abierta: {SceneManager.GetActiveScene().name}");
        sb.AppendLine("(segundos desde que se pulsó Play · hueco desde la línea anterior · qué pasó)");
        sb.AppendLine();
        double anterior = 0;
        foreach (var l in lineas)
        {
            int tab = l.IndexOf('\t');
            if (tab <= 0) continue;
            if (!double.TryParse(l.Substring(0, tab), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)) continue;
            double hueco = t - anterior;
            anterior = t;
            sb.Append(t.ToString("0.00").PadLeft(7)).Append("  ")
              .Append((hueco >= 0.5 ? "+" + hueco.ToString("0.00") : string.Empty).PadLeft(7)).Append("  ")
              .AppendLine(l.Substring(tab + 1));
        }

        var log = TrozoDelEditorLog();
        if (log.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── Lo que dice Unity en Editor.log (recarga del código y escenas) ──");
            ResumenDelLog(log, sb);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
        File.WriteAllText(Ruta, sb.ToString());
        if (log.Length > 0) File.WriteAllText(RutaLog, log);
        Debug.Log("[Arranque] Medido: " + Path.GetFullPath(Ruta));
    }

    // ── Editor.log ───────────────────────────────────────────────────────────────────────────

    static long TamanoDelEditorLog()
    {
        try
        {
            var ruta = Application.consoleLogPath;
            return !string.IsNullOrEmpty(ruta) && File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        }
        catch (Exception) { return 0; }
    }

    static string TrozoDelEditorLog()
    {
        try
        {
            var ruta = Application.consoleLogPath;
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return string.Empty;
            long.TryParse(SessionState.GetString(ClaveLog, "0"), out var desde);
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (desde < 0 || desde > fs.Length) desde = 0;
            long cuanto = Math.Min(fs.Length - desde, MaxLogBytes);
            fs.Seek(desde, SeekOrigin.Begin);
            var bytes = new byte[cuanto];
            int leidos = 0;
            while (leidos < cuanto)
            {
                int n = fs.Read(bytes, leidos, (int)(cuanto - leidos));
                if (n <= 0) break;
                leidos += n;
            }
            return Encoding.UTF8.GetString(bytes, 0, leidos);
        }
        catch (Exception) { return string.Empty; }
    }

    /// Los bloques de Unity que dicen en qué se va el tiempo: la recarga del código (con su
    /// desglose) y cada escena cargada.
    static void ResumenDelLog(string log, StringBuilder sb)
    {
        var lineas = log.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            var l = lineas[i];
            bool bloque = l.StartsWith("Domain Reload Profiling", StringComparison.Ordinal)
                       || l.StartsWith("Loaded scene", StringComparison.Ordinal)
                       || l.StartsWith("Unloading", StringComparison.Ordinal)
                       || l.Contains("Asset Pipeline Refresh")
                       || l.StartsWith("Reloading assemblies", StringComparison.Ordinal)
                       || l.StartsWith("Begin MonoManager ReloadAssembly", StringComparison.Ordinal)
                       || l.StartsWith("Refreshing native plugins", StringComparison.Ordinal);
            if (!bloque) continue;
            sb.AppendLine(l);
            while (i + 1 < lineas.Length && lineas[i + 1].Length > 0 && (lineas[i + 1][0] == '\t' || lineas[i + 1][0] == ' '))
                sb.AppendLine(lineas[++i]);
        }
    }
}
#endif
