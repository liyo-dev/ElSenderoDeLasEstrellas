using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

/// <summary>
/// Los textos (ES/EN) que ponen los montadores de la demo en las tablas de localización de
/// Assets/Resources/Localization. Se apuntan durante el montaje y se escriben de una vez al final:
/// cada clave se añade o se sustituye en su sitio, sin tocar el resto del archivo ni su estilo.
/// </summary>
public static class TextosDeLaDemo
{
    public enum Tabla { Dialogos, Misiones, Cinematicas, Interfaz }

    private const string Carpeta = "Assets/Resources/Localization";
    private static readonly Dictionary<Tabla, List<(string clave, string es, string en)>> s_pendientes = new();

    public static void Poner(Tabla tabla, string clave, string es, string en)
    {
        if (string.IsNullOrEmpty(clave)) return;
        if (!s_pendientes.TryGetValue(tabla, out var lista)) s_pendientes[tabla] = lista = new();
        lista.RemoveAll(t => t.clave == clave);
        lista.Add((clave, es, en ?? es));
    }

    /// <summary>Escribe lo apuntado en las tablas y lo olvida. Devuelve cuántas claves se escribieron.</summary>
    public static int Escribir()
    {
        int n = 0;
        foreach (var par in s_pendientes)
        {
            string nombre = par.Key switch
            {
                Tabla.Dialogos => "dialogues", Tabla.Misiones => "quests", Tabla.Cinematicas => "cinematics", _ => "ui",
            };
            bool cine = par.Key == Tabla.Cinematicas;
            foreach (var idioma in new[] { "es", "en" })
            {
                string ruta = $"{Carpeta}/{nombre}_{idioma}.json";
                string texto = File.Exists(ruta) ? File.ReadAllText(ruta) : (cine ? "{\n    \"subtitles\": [\n    ]\n}\n" : "{\n    \"texts\": [\n    ]\n}\n");
                foreach (var (clave, es, en) in par.Value)
                {
                    texto = Upsert(texto, cine, clave, idioma == "es" ? es : en);
                    n++;
                }
                File.WriteAllText(ruta, texto, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ruta);
            }
        }
        s_pendientes.Clear();
        return n / 2;
    }

    private static string Upsert(string json, bool cine, string clave, string valor)
    {
        string campoClave = cine ? "id" : "key", campoValor = cine ? "text" : "value";
        string marca = $"\"{campoClave}\":";
        // Entrada existente: «"key": "CLAVE"» con o sin espacio tras los dos puntos.
        int i = BuscarClave(json, campoClave, clave);
        if (i >= 0)
        {
            int v = json.IndexOf($"\"{campoValor}\"", i);
            int dosPuntos = json.IndexOf(':', v);
            int ini = json.IndexOf('"', dosPuntos + 1);
            int fin = FinDeCadena(json, ini);
            return json.Substring(0, ini) + "\"" + Escapar(valor) + "\"" + json.Substring(fin + 1);
        }
        // Entrada nueva al final de la lista, con el estilo largo (como las del resto del archivo).
        int cierre = json.LastIndexOf(']');
        int ultimo = json.LastIndexOf('}', cierre);
        bool vacia = json.IndexOf('{', json.IndexOf('[')) > cierre || json.IndexOf('{', json.IndexOf('[')) < 0;
        string entrada = $"        {{\n            \"{campoClave}\": \"{Escapar(clave)}\",\n            \"{campoValor}\": \"{Escapar(valor)}\"\n        }}";
        if (vacia) return json.Substring(0, json.IndexOf('[') + 1) + "\n" + entrada + "\n    " + json.Substring(cierre);
        return json.Substring(0, ultimo + 1) + ",\n" + entrada + json.Substring(ultimo + 1);
    }

    private static int BuscarClave(string json, string campo, string clave)
    {
        foreach (var patron in new[] { $"\"{campo}\": \"{Escapar(clave)}\"", $"\"{campo}\":\"{Escapar(clave)}\"" })
        {
            int i = json.IndexOf(patron, System.StringComparison.Ordinal);
            if (i >= 0) return i;
        }
        return -1;
    }

    private static int FinDeCadena(string s, int ini)
    {
        for (int k = ini + 1; k < s.Length; k++)
        {
            if (s[k] == '\\') { k++; continue; }
            if (s[k] == '"') return k;
        }
        return s.Length - 1;
    }

    private static string Escapar(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
