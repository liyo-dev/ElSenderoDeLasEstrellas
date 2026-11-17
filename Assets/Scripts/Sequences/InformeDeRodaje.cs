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
/// Las fotos de revisión, si se piden, van aparte (Library/Director/rodaje): comprimidas y con tope,
/// para que una tarde de pruebas no llene el disco. Solo existe con instrumentación. Ver INC-587.
public static class InformeDeRodaje
{
    private sealed class Plano
    {
        public float inicio, fin = -1f;
        public string fase, descripcion, sujeto;
        public readonly List<string> lineas = new();
        public readonly List<string> avisos = new();
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
    private static long s_bytesDeLaSesion;
    private static bool s_capAvisado;

    /// Segundos de plano sin ninguna línea a partir de los que se considera «plano muerto».
    private const float PlanoMuerto = 6f;
    private const float Intervalo = 0.25f;

    // Las capturas son diagnóstico, no contenido: viven fuera del proyecto, van comprimidas y tienen
    // tope. Sin esto, cada prueba dejaba una foto por plano y el disco se llenaba sin avisar.
    private const int AnchoDeLaFoto = 1280;
    private const int CalidadJpg = 80;
    private const long TopeDeLaSesion = 200L * 1024 * 1024;
    private const long TopeTotal = 500L * 1024 * 1024;
    private const int SesionesQueSeGuardan = 3;
    private const long DiscoMinimoLibre = 5L * 1024 * 1024 * 1024;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_player = null; s_fase = ""; s_planos.Clear(); s_avisosSueltos.Clear(); s_muestreo = null;
        s_carpetaFotos = null; s_nFoto = 0; s_bytesDeLaSesion = 0; s_capAvisado = false;
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

    public static void PlanoNuevo(string descripcion, string sujeto = null)
    {
        if (s_player == null) return;
        var anterior = Actual;
        if (anterior != null && anterior.fin < 0f) anterior.fin = Ahora;
        s_planos.Add(new Plano
        {
            inicio = Ahora, fase = s_fase, descripcion = descripcion,
            sujeto = sujeto
        });
    }

    /// Foto del Game view para revisar el rodaje sin grabarlo. Solo se saca si existe el fichero
    /// «Guiones/_ordenes/fotos.on» (el menú *Pruebas ▸ Rodaje ▸ Capturar fotos…* lo pone). Va a
    /// Library/Director/rodaje, en JPG y con tope: es diagnóstico, no contenido.
    public static void Foto(string etiqueta)
    {
        if (s_player == null) return;
        if (s_carpetaFotos == null)
        {
            string bandera = Path.Combine(Application.dataPath, "..", "Guiones", "_ordenes", "fotos.on");
            if (!File.Exists(bandera)) { s_carpetaFotos = ""; return; }

            string raiz = Path.Combine(Application.dataPath, "..", "Library", "Director", "rodaje");
            if (!HayDiscoParaCapturas(raiz)) { s_carpetaFotos = ""; return; }
            s_carpetaFotos = Path.Combine(raiz, $"fotos_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(s_carpetaFotos);
            PodarCapturas(raiz);
        }
        if (s_carpetaFotos.Length == 0) return;
        if (s_bytesDeLaSesion >= TopeDeLaSesion)
        {
            if (!s_capAvisado)
            {
                s_capAvisado = true;
                Debug.LogWarning($"[InformeDeRodaje] La sesión de capturas llegó a {s_bytesDeLaSesion / (1024f * 1024f):0} MB: dejo de sacar fotos.");
            }
            s_carpetaFotos = "";
            return;
        }

        try
        {
            byte[] jpg = JpgDeLaPantalla();
            if (jpg == null || jpg.Length == 0) return;
            File.WriteAllBytes(Path.Combine(s_carpetaFotos, $"{++s_nFoto:000}_{etiqueta}.jpg"), jpg);
            s_bytesDeLaSesion += jpg.Length;
        }
        catch (Exception ex) { Debug.LogWarning($"[InformeDeRodaje] Foto: {ex.Message}"); }
    }

    /// La pantalla reducida a <see cref="AnchoDeLaFoto"/> y comprimida en JPG: la foto de revisión
    /// no necesita el PNG del tamaño del monitor.
    private static byte[] JpgDeLaPantalla()
    {
        var pantalla = ScreenCapture.CaptureScreenshotAsTexture();
        if (pantalla == null) return null;

        RenderTexture rt = null;
        Texture2D reducida = null;
        try
        {
            int ancho = Mathf.Min(AnchoDeLaFoto, pantalla.width);
            int alto = Mathf.Max(1, Mathf.RoundToInt(pantalla.height * (ancho / (float)pantalla.width)));
            if (ancho == pantalla.width)
            {
                reducida = new Texture2D(pantalla.width, pantalla.height, TextureFormat.RGB24, false);
                reducida.SetPixels32(pantalla.GetPixels32());
                reducida.Apply(false);
            }
            else
            {
                rt = RenderTexture.GetTemporary(ancho, alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var previo = RenderTexture.active;
                try
                {
                    Graphics.Blit(pantalla, rt);
                    RenderTexture.active = rt;
                    reducida = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                    reducida.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
                    reducida.Apply(false);
                }
                finally { RenderTexture.active = previo; }
            }
            return reducida.EncodeToJPG(CalidadJpg);
        }
        finally
        {
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (reducida != null) UnityEngine.Object.DestroyImmediate(reducida);
            UnityEngine.Object.DestroyImmediate(pantalla);
        }
    }

    /// Guarda solo las últimas sesiones y por debajo del tope total. Los partes .md no se tocan:
    /// pesan nada y son el diagnóstico útil.
    private static void PodarCapturas(string raiz)
    {
        try
        {
            var sesiones = new DirectoryInfo(raiz).GetDirectories("fotos_*");
            Array.Sort(sesiones, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            long total = 0;
            foreach (var sesion in sesiones) total += TamanoDe(sesion);

            for (int i = 0; i < sesiones.Length; i++)
            {
                bool sobran = sesiones.Length - i > SesionesQueSeGuardan;
                if (!sobran && total <= TopeTotal) break;
                if (i >= sesiones.Length - 1) break;   // la sesión de ahora no se borra
                total -= TamanoDe(sesiones[i]);
                try { sesiones[i].Delete(true); } catch { }
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[InformeDeRodaje] No pude limpiar capturas viejas: {ex.Message}"); }
    }

    private static long TamanoDe(DirectoryInfo carpeta)
    {
        long total = 0;
        try { foreach (var f in carpeta.EnumerateFiles("*", SearchOption.AllDirectories)) total += f.Length; }
        catch { }
        return total;
    }

    private static bool HayDiscoParaCapturas(string raiz)
    {
        try
        {
            string unidad = Path.GetPathRoot(Path.GetFullPath(raiz));
            return new DriveInfo(unidad).AvailableFreeSpace >= DiscoMinimoLibre;
        }
        catch { return true; }
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
        if (s_nFoto > 0)
            sb.AppendLine($"Capturas: {s_nFoto} en {s_carpetaFotos} ({s_bytesDeLaSesion / (1024f * 1024f):0.0} MB de {TopeDeLaSesion / (1024f * 1024f):0} MB).");
        sb.AppendLine();
        sb.AppendLine("| # | Inicio | Dura | Fase | Plano | Líneas | Fuera de cuadro | Avisos | Cámara y sujeto |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < s_planos.Count; i++)
        {
            var p = s_planos[i];
            float dura = Mathf.Max(0f, p.fin - p.inicio);
            string fuera = p.fueraDeCuadro > 0f ? $"{p.fueraDeCuadro:F1}s" : "";
            sb.AppendLine($"| {i + 1} | {Tiempo(p.inicio)} | {dura:F1}s | {p.fase} | {p.descripcion} | {string.Join("; ", p.lineas)} | {fuera} | {string.Join("; ", p.avisos)} | {p.donde} |");
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
