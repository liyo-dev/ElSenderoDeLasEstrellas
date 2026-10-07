#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// Parte de rodaje automático: al terminar cada reproducción de una secuencia escribe en
/// Logs/Rodaje/ un informe plano a plano (cuándo empieza, cuánto dura, qué líneas suenan, cuánto
/// tiempo se sale el sujeto del cuadro, qué figurantes se retiraron) y señala los problemas que
/// más se repiten al revisar grabaciones: planos muertos, sujetos que se salen y actores atascados.
/// Sirve para revisar una cinemática sin tener que grabarla y mirarla fotograma a fotograma.
/// Solo existe con instrumentación. Ver INC-587.
public static class InformeDeRodaje
{
    private sealed class Plano
    {
        public float inicio, fin = -1f;
        public string fase, descripcion, sujeto;
        public readonly List<string> lineas = new();
        public readonly List<string> avisos = new();
        public string retirados;
        public float fueraDeCuadro, pequeno, muestras;
        // Dónde estaban la cámara y el sujeto en la primera muestra del plano.
        public string donde;
    }

    private static SequencePlayer s_player;
    private static float s_t0;
    private static string s_fase = "";
    private static readonly List<Plano> s_planos = new();
    private static readonly List<string> s_avisosSueltos = new();
    private static Coroutine s_muestreo;
    private static string s_carpetaFotos;
    private static int s_nFoto;

    /// Segundos de plano sin ninguna línea a partir de los que se considera «plano muerto».
    private const float PlanoMuerto = 6f;
    private const float Intervalo = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_player = null; s_fase = ""; s_planos.Clear(); s_avisosSueltos.Clear(); s_muestreo = null;
        s_carpetaFotos = null; s_nFoto = 0;
    }

    private static float Ahora => Time.unscaledTime - s_t0;
    private static Plano Actual => s_planos.Count > 0 ? s_planos[s_planos.Count - 1] : null;

    public static void Iniciar(SequencePlayer player)
    {
        if (player == null) return;
        ResetStatics();
        s_player = player;
        s_t0 = Time.unscaledTime;
        s_muestreo = player.StartCoroutine(Co_Muestreo(player));
        player.RegisterCleanup(() => Terminar(player));
    }

    public static void Fase(string nombre) { if (s_player != null) s_fase = nombre; }

    public static void PlanoNuevo(string descripcion, ShotFraming encuadre, IReadOnlyList<string> retirados, string sujeto = null)
    {
        if (s_player == null) return;
        var anterior = Actual;
        if (anterior != null && anterior.fin < 0f) anterior.fin = Ahora;
        s_planos.Add(new Plano
        {
            inicio = Ahora, fase = s_fase, descripcion = descripcion,
            sujeto = encuadre != null ? encuadre.subjectId : sujeto,
            retirados = retirados != null && retirados.Count > 0 ? string.Join(", ", retirados) : ""
        });
    }

    /// Foto del Game view para revisar el rodaje sin grabarlo. Solo si existe el fichero
    /// «Guiones/_ordenes/fotos.on» (para no llenar el disco en cada prueba normal).
    public static void Foto(string etiqueta)
    {
        if (s_player == null) return;
        if (s_carpetaFotos == null)
        {
            string bandera = Path.Combine(Application.dataPath, "..", "Guiones", "_ordenes", "fotos.on");
            if (!File.Exists(bandera)) { s_carpetaFotos = ""; return; }
            s_carpetaFotos = Path.Combine(Application.dataPath, "..", "Logs", "Rodaje", $"fotos_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(s_carpetaFotos);
        }
        if (s_carpetaFotos.Length == 0) return;
        try { ScreenCapture.CaptureScreenshot(Path.Combine(s_carpetaFotos, $"{++s_nFoto:000}_{etiqueta}.png")); }
        catch (Exception ex) { Debug.LogWarning($"[InformeDeRodaje] Foto: {ex.Message}"); }
    }

    public static void Linea(string actor, string clave)
    {
        if (s_player == null) return;
        if (Actual != null) Actual.lineas.Add($"{actor}: {clave}");
        else s_avisosSueltos.Add($"{Ahora:F1}s línea sin plano: {actor}: {clave}");
    }

    public static void Aviso(string texto)
    {
        if (s_player == null || string.IsNullOrEmpty(texto)) return;
        if (Actual != null) Actual.avisos.Add(texto);
        else s_avisosSueltos.Add($"{Ahora:F1}s {texto}");
    }

    private static IEnumerator Co_Muestreo(SequencePlayer player)
    {
        var espera = new WaitForSecondsRealtime(Intervalo);
        while (player != null && s_player == player)
        {
            yield return espera;
            var plano = Actual;
            var cam = player.CachedCamera;
            if (plano == null || cam == null || string.IsNullOrEmpty(plano.sujeto)) continue;
            if (!SequenceActor.TryResolve(plano.sujeto, out var sujeto) || sujeto?.Transform == null) continue;
            plano.muestras += Intervalo;
            Vector3 cara = sujeto.Transform.position + Vector3.up * sujeto.AlturaDeOjos;
            if (plano.donde == null)
            {
                plano.donde = $"cámara {cam.transform.position:F1} mira {cam.transform.forward:F2} · sujeto {sujeto.Transform.position:F1} · {Vector3.Distance(cam.transform.position, cara):F1} m";
                Debug.Log($"[InformeDeRodaje] Plano {s_planos.Count} «{plano.descripcion}»: {plano.donde} · cámara '{cam.name}' lejos {cam.farClipPlane:F0}");
            }
            Vector3 v = cam.WorldToViewportPoint(cara);
            if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) plano.fueraDeCuadro += Intervalo;
            else
            {
                Vector3 pies = cam.WorldToViewportPoint(sujeto.Transform.position);
                if (Mathf.Abs(v.y - pies.y) < 0.06f) plano.pequeno += Intervalo;
            }
        }
    }

    public static void Terminar(SequencePlayer player)
    {
        if (player == null || s_player != player) return;
        if (s_muestreo != null) player.StopCoroutine(s_muestreo);
        if (Actual != null && Actual.fin < 0f) Actual.fin = Ahora;
        try { Escribir(player); }
        catch (Exception ex) { Debug.LogWarning($"[InformeDeRodaje] No se pudo escribir el informe: {ex.Message}"); }
        s_player = null;
    }

    private static string Tiempo(float t) => $"{(int)(t / 60f)}:{t % 60f:00.0}";

    private static void Escribir(SequencePlayer player)
    {
        string carpeta = Path.Combine(Application.dataPath, "..", "Logs", "Rodaje");
        Directory.CreateDirectory(carpeta);
        string nombre = player.Definition != null ? player.Definition.name : player.name;
        string ruta = Path.Combine(carpeta, $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        var sb = new StringBuilder();
        var problemas = new List<string>();
        sb.AppendLine($"# Parte de rodaje: {nombre}");
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm} · {s_planos.Count} planos · {Tiempo(Ahora)} de secuencia");
        sb.AppendLine();
        sb.AppendLine("| # | Inicio | Dura | Fase | Plano | Líneas | Fuera de cuadro | Retirados | Avisos | Cámara y sujeto |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < s_planos.Count; i++)
        {
            var p = s_planos[i];
            float dura = Mathf.Max(0f, p.fin - p.inicio);
            string fuera = p.fueraDeCuadro > 0f ? $"{p.fueraDeCuadro:F1}s" : "";
            sb.AppendLine($"| {i + 1} | {Tiempo(p.inicio)} | {dura:F1}s | {p.fase} | {p.descripcion} | {string.Join("; ", p.lineas)} | {fuera} | {p.retirados} | {string.Join("; ", p.avisos)} | {p.donde} |");
            if (dura >= PlanoMuerto && p.lineas.Count == 0)
                problemas.Add($"Plano {i + 1} ({Tiempo(p.inicio)}, {p.fase}): {dura:F1}s sin ninguna línea — posible plano muerto: «{p.descripcion}».");
            if (p.fueraDeCuadro >= 0.5f)
                problemas.Add($"Plano {i + 1} ({Tiempo(p.inicio)}): el sujeto {p.sujeto} está {p.fueraDeCuadro:F1}s fuera del cuadro.");
            if (p.muestras > 1f && p.pequeno >= p.muestras * 0.8f && !p.descripcion.Contains("general"))
                problemas.Add($"Plano {i + 1} ({Tiempo(p.inicio)}): el sujeto {p.sujeto} se ve diminuto casi todo el plano.");
            foreach (var aviso in p.avisos) problemas.Add($"Plano {i + 1} ({Tiempo(p.inicio)}): {aviso}");
        }
        foreach (var aviso in s_avisosSueltos) problemas.Add(aviso);
        sb.AppendLine();
        sb.AppendLine($"## Problemas detectados ({problemas.Count})");
        foreach (var p in problemas) sb.AppendLine("- " + p);
        File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[InformeDeRodaje] {s_planos.Count} planos, {problemas.Count} problemas. Informe: {Path.GetFullPath(ruta)}");
    }
}
#endif
