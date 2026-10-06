#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

/// Orden «gemini» del buzón (INC-603, 6 oct 2026): manda un vídeo a la API de Gemini y deja lo
/// que diga en Logs/Gemini/. Va por el Editor porque el PC de Raúl sí llega a Google; desde las
/// sesiones de Claude esa dirección está cortada.
///
///   gemini RUTA_DEL_VIDEO [fps=2] [modelo=gemini-3.8-flash]
///   (las líneas siguientes, si las hay, son la pregunta; si no, se usa la revisión de cinemáticas)
///
/// La ruta puede ser absoluta (C:\Users\...\Desktop\Despertar2.mp4) o relativa al proyecto.
/// La clave sale de la variable de entorno GEMINI_API_KEY o, si no está, de
/// UserSettings/GeminiKey.txt (carpeta que no entra en git).
///
/// Trabaja en segundo plano: el buzón responde enseguida «en marcha» y, al acabar, añade otra
/// línea al registro con el fichero del resultado. No hay que refrescar mientras tanto (la
/// recarga del Editor cortaría la petición).
public static class GeminiDelBuzon
{
    private const string Api = "https://generativelanguage.googleapis.com";
    private const string ModeloPorDefecto = "gemini-3.8-flash";

    private const string PreguntaPorDefecto =
@"Eres director de fotografía y montador revisando una cinemática de un videojuego 3D (estilo chibi, cel-shading, bandas negras de cine arriba y abajo y subtítulos como en el anime). Revisa el vídeo con mucho detalle y responde en castellano:

1. Lista de planos: para cada corte, el tiempo (MM:SS,d), qué tipo de plano es, a quién encuadra y qué pasa.
2. Problemas de encuadre: cabezas o cuerpos cortados, personajes pegados al borde, cosas del decorado (flores, árboles) tapando, cámara metida en algo, planos demasiado cortos o que no se entienden.
3. Animación: poses raras, personajes que se deslizan sin andar, gestos que no pegan, saltos o tirones.
4. Efectos y acción: si lo que debe pasar se ve y se entiende (por ejemplo, dos proyectiles que chocan), y en qué momento exacto.
5. Texto y sonido: si los subtítulos se leen, si tapan algo, si las voces y los efectos van a tiempo.
6. Las tres mejoras que más se notarían, por orden.

Da siempre el tiempo de cada cosa. Sé concreto y no inventes lo que no se vea.";

    public static string Empezar(string primeraLinea, string pregunta)
    {
        var partes = Partir(primeraLinea);
        if (partes.Count == 0) return "ERROR: falta la ruta del vídeo";
        var opciones = partes.Where(p => p.Contains('=')).Select(p => p.Split(new[] { '=' }, 2))
                             .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);
        string ruta = partes.First(p => !p.Contains('='));
        string raiz = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        if (!Path.IsPathRooted(ruta)) ruta = Path.Combine(raiz, ruta);
        if (!File.Exists(ruta)) return $"ERROR: no encuentro el vídeo «{ruta}»";

        string clave = Clave(raiz);
        if (string.IsNullOrEmpty(clave))
            return "ERROR: no hay clave. Pon la clave de Gemini en UserSettings/GeminiKey.txt (o en la variable GEMINI_API_KEY).";

        string modelo = opciones.TryGetValue("modelo", out var m) ? m : ModeloPorDefecto;
        float fps = opciones.TryGetValue("fps", out var f) && float.TryParse(f, NumberStyles.Float, CultureInfo.InvariantCulture, out var ff) ? ff : 2f;
        if (string.IsNullOrWhiteSpace(pregunta)) pregunta = PreguntaPorDefecto;

        string carpeta = Path.Combine(raiz, "Logs", "Gemini");
        Directory.CreateDirectory(carpeta);
        string nombre = $"{Path.GetFileNameWithoutExtension(ruta)}_{DateTime.Now:yyyyMMdd_HHmmss}";
        string salida = Path.Combine(carpeta, nombre + ".md");
        string registro = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes", "registro.log");

        Task.Run(async () =>
        {
            string fin;
            void Paso(string que)
            {
                try { File.AppendAllText(registro, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   gemini {Path.GetFileName(ruta)}: {que}\n"); } catch { }
            }
            try { fin = await Analizar(ruta, clave, modelo, fps, pregunta, salida, Paso); }
            catch (Exception ex) { fin = "ERROR: " + ex.Message; }
            try { File.AppendAllText(registro, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] gemini {Path.GetFileName(ruta)} → {fin}\n"); }
            catch { }
        });
        return $"en marcha con {modelo} a {fps.ToString(CultureInfo.InvariantCulture)} fps; el resultado irá a Logs/Gemini/{nombre}.md";
    }

    private static string Clave(string raiz)
    {
        string env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
        string fichero = Path.Combine(raiz, "UserSettings", "GeminiKey.txt");
        return File.Exists(fichero) ? File.ReadAllText(fichero).Trim() : null;
    }

    private static async Task<string> Analizar(string ruta, string clave, string modelo, float fps, string pregunta, string salida, Action<string> paso)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.Add("x-goog-api-key", clave);
        long bytes = new FileInfo(ruta).Length;

        // 1. Subida reanudable: primero se pide la dirección de subida…
        var inicio = new HttpRequestMessage(HttpMethod.Post, Api + "/upload/v1beta/files")
        {
            Content = new StringContent("{\"file\":{\"display_name\":" + Json(Path.GetFileName(ruta)) + "}}", Encoding.UTF8, "application/json"),
        };
        inicio.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        inicio.Headers.Add("X-Goog-Upload-Command", "start");
        inicio.Headers.Add("X-Goog-Upload-Header-Content-Length", bytes.ToString(CultureInfo.InvariantCulture));
        inicio.Headers.Add("X-Goog-Upload-Header-Content-Type", "video/mp4");
        var r1 = await http.SendAsync(inicio);
        if (!r1.IsSuccessStatusCode) return $"ERROR al pedir la subida ({(int)r1.StatusCode}): {Corto(await r1.Content.ReadAsStringAsync())}";
        if (!r1.Headers.TryGetValues("x-goog-upload-url", out var urls)) return "ERROR: Google no dio dirección de subida";
        string urlSubida = urls.First();

        // …y luego se manda el vídeo entero.
        string infoFichero;
        using (var fs = File.OpenRead(ruta))
        {
            var subida = new HttpRequestMessage(HttpMethod.Post, urlSubida) { Content = new StreamContent(fs) };
            subida.Content.Headers.ContentLength = bytes;
            subida.Headers.Add("X-Goog-Upload-Offset", "0");
            subida.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
            var r2 = await http.SendAsync(subida);
            infoFichero = await r2.Content.ReadAsStringAsync();
            if (!r2.IsSuccessStatusCode) return $"ERROR al subir ({(int)r2.StatusCode}): {Corto(infoFichero)}";
        }
        paso("vídeo subido");
        var file = (MiniJson.Leer(infoFichero) as Dictionary<string, object>)?.Get("file") as Dictionary<string, object>;
        string nombreFichero = file?.Get("name") as string;
        string uri = file?.Get("uri") as string;
        if (nombreFichero == null || uri == null) return "ERROR: respuesta de subida rara: " + Corto(infoFichero);

        // 2. Esperar a que Google lo tenga procesado (ACTIVE).
        string estado = file.Get("state") as string;
        for (int k = 0; estado != "ACTIVE" && k < 120; k++)
        {
            if (estado == "FAILED") return "ERROR: Google no pudo procesar el vídeo";
            await Task.Delay(5000);
            var info = MiniJson.Leer(await http.GetStringAsync(Api + "/v1beta/" + nombreFichero)) as Dictionary<string, object>;
            estado = info?.Get("state") as string;
        }
        if (estado != "ACTIVE") return "ERROR: el vídeo no llegó a estar listo en 10 minutos";
        paso("vídeo listo en Google; pregunto");

        // 3. La pregunta, con el vídeo delante (como recomienda Google).
        string cuerpo = "{\"model\":" + Json(modelo) + ",\"input\":[" +
                        "{\"type\":\"video\",\"uri\":" + Json(uri) + ",\"mime_type\":\"video/mp4\"," +
                        "\"processing\":{\"type\":\"static\",\"fps\":" + fps.ToString(CultureInfo.InvariantCulture) + "}}," +
                        "{\"type\":\"text\",\"text\":" + Json(pregunta) + "}]}";
        // Si Google está saturado (503) o nos frena (429), se reintenta con esperas crecientes.
        HttpResponseMessage r3 = null;
        string respuesta = null;
        int[] esperas = { 20, 40, 80, 120, 180 };
        for (int intento = 0; ; intento++)
        {
            r3 = await http.PostAsync(Api + "/v1beta/interactions", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
            respuesta = await r3.Content.ReadAsStringAsync();
            int codigo = (int)r3.StatusCode;
            paso($"respuesta {codigo}" + (codigo == 200 ? "" : $" (intento {intento + 1})"));
            if ((codigo != 503 && codigo != 429 && codigo != 500) || intento >= esperas.Length) break;
            await Task.Delay(esperas[intento] * 1000);
        }
        File.WriteAllText(Path.ChangeExtension(salida, ".json"), respuesta);
        try { await http.DeleteAsync(Api + "/v1beta/" + nombreFichero); } catch { }
        if (!r3.IsSuccessStatusCode) return $"ERROR en la consulta ({(int)r3.StatusCode}): {Corto(respuesta)}";

        string texto = TextoDeSalida(MiniJson.Leer(respuesta));
        var md = new StringBuilder();
        md.AppendLine($"# Gemini: {Path.GetFileName(ruta)}");
        md.AppendLine();
        md.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm} · {modelo} · {fps.ToString(CultureInfo.InvariantCulture)} fps · {bytes / 1048576f:0.0} MB");
        md.AppendLine();
        md.AppendLine("## Pregunta");
        md.AppendLine();
        md.AppendLine(pregunta);
        md.AppendLine();
        md.AppendLine("## Respuesta");
        md.AppendLine();
        md.AppendLine(string.IsNullOrWhiteSpace(texto) ? "(sin texto; ver el .json)" : texto);
        File.WriteAllText(salida, md.ToString());
        return "hecho: Logs/Gemini/" + Path.GetFileName(salida);
    }

    /// El texto de la respuesta: steps[].content[].text de los pasos del modelo
    /// (o output_text si viniera así).
    private static string TextoDeSalida(object raiz)
    {
        if (raiz is not Dictionary<string, object> d) return null;
        if (d.Get("output_text") is string ot) return ot;
        var sb = new StringBuilder();
        if (d.Get("steps") is List<object> pasos)
            foreach (var p in pasos.OfType<Dictionary<string, object>>())
            {
                string tipo = p.Get("type") as string;
                if (tipo != null && tipo != "model_output") continue;
                if (p.Get("content") is List<object> contenido)
                    foreach (var c in contenido.OfType<Dictionary<string, object>>())
                        if (c.Get("text") is string t) sb.AppendLine(t);
            }
        return sb.ToString().Trim();
    }

    private static List<string> Partir(string linea)
    {
        // Rutas con espacios: entre comillas.
        var res = new List<string>();
        var sb = new StringBuilder();
        bool comillas = false;
        foreach (char ch in linea)
        {
            if (ch == '"') { comillas = !comillas; continue; }
            if (ch == ' ' && !comillas) { if (sb.Length > 0) { res.Add(sb.ToString()); sb.Clear(); } continue; }
            sb.Append(ch);
        }
        if (sb.Length > 0) res.Add(sb.ToString());
        return res;
    }

    private static string Corto(string s) => s == null ? "" : s.Length > 400 ? s.Substring(0, 400) + "…" : s;

    private static object Get(this Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;

    private static string Json(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// Lector de JSON mínimo (objetos, listas, textos, números, true/false/null).
    private static class MiniJson
    {
        public static object Leer(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = 0;
            try { return Valor(s, ref i); }
            catch { return null; }
        }

        private static void Blancos(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Valor(string s, ref int i)
        {
            Blancos(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++; Blancos(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Blancos(s, ref i);
                    string k = Texto(s, ref i);
                    Blancos(s, ref i); i++; // ':'
                    d[k] = Valor(s, ref i);
                    Blancos(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d; // '}'
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++; Blancos(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Valor(s, ref i));
                    Blancos(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l; // ']'
                }
            }
            if (c == '"') return Texto(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int ini = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(ini, i - ini), CultureInfo.InvariantCulture);
        }

        private static string Texto(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // '"'
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }
    }
}
#endif
