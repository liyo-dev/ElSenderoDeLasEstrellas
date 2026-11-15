#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

/// Cambia opciones concretas de una línea de un `.guion.txt` sin tocar nada más: conserva la
/// indentación, el resto de opciones, los comentarios y el tipo de salto de línea. Lo usa el taller
/// para aplicar un encuadre y para revertirlo si el horneado no se acepta. Ver INC-1230.
internal static class GuionTextoEditable
{
    /// El `.guion.txt` del que salió un horneado: la ruta guardada o, en assets viejos, el archivo
    /// con el mismo nombre dentro de `Assets/_SEQUENCES/Guiones`. Null si no se encuentra.
    public static string RutaDelTexto(GuionHorneado guion)
    {
        if (guion == null) return null;
        if (!string.IsNullOrEmpty(guion.rutaDelTexto) && File.Exists(guion.rutaDelTexto))
            return guion.rutaDelTexto;
        if (string.IsNullOrWhiteSpace(guion.nombre)) return null;

        string buscado = guion.nombre + ".guion.txt";
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_SEQUENCES/Guiones" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileName(ruta), buscado, StringComparison.OrdinalIgnoreCase))
                return ruta;
        }
        return null;
    }

    /// Aplica los cambios sobre un texto en memoria y devuelve el texto entero. El taller hornea
    /// desde aquí y no escribe el .txt hasta que el diff se acepta.
    public static bool ReescribirEnTexto(string texto, int linea,
        IReadOnlyDictionary<string, string> cambios, out string nuevo)
    {
        nuevo = texto;
        if (string.IsNullOrEmpty(texto) || linea <= 0) return false;

        string salto = texto.Contains("\r\n") ? "\r\n" : "\n";
        string[] lineas = texto.Replace("\r\n", "\n").Split('\n');
        if (linea > lineas.Length) return false;

        lineas[linea - 1] = ReescribirLinea(lineas[linea - 1], cambios);
        nuevo = string.Join(salto, lineas);
        return true;
    }

    /// Reescribe la línea (1-based). Devuelve false si no se puede; en ese caso no escribe nada.
    /// «anterior» sale con el texto original de la línea, para poder restaurarla.
    public static bool ReescribirOpciones(string ruta, int linea,
        IReadOnlyDictionary<string, string> cambios, out string anterior)
    {
        anterior = null;
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta) || linea <= 0 || cambios == null || cambios.Count == 0)
            return false;

        string texto = File.ReadAllText(ruta);
        string salto = texto.Contains("\r\n") ? "\r\n" : "\n";
        string[] lineas = texto.Replace("\r\n", "\n").Split('\n');
        if (linea > lineas.Length) return false;

        anterior = lineas[linea - 1];
        lineas[linea - 1] = ReescribirLinea(anterior, cambios);
        File.WriteAllText(ruta, string.Join(salto, lineas), new UTF8Encoding(false));
        return true;
    }

    /// Vuelve a dejar la línea como estaba. Se usa cuando el horneado no se acepta.
    public static bool RestaurarLinea(string ruta, int linea, string anterior)
    {
        if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta) || linea <= 0 || anterior == null) return false;

        string texto = File.ReadAllText(ruta);
        string salto = texto.Contains("\r\n") ? "\r\n" : "\n";
        string[] lineas = texto.Replace("\r\n", "\n").Split('\n');
        if (linea > lineas.Length) return false;

        lineas[linea - 1] = anterior;
        File.WriteAllText(ruta, string.Join(salto, lineas), new UTF8Encoding(false));
        return true;
    }

    /// Sustituye o añade «clave=valor» en una línea, sin tocar los comentarios. Es público para los
    /// tests: es la operación que más se puede romper.
    public static string ReescribirLinea(string linea, IReadOnlyDictionary<string, string> cambios)
    {
        linea ??= string.Empty;

        int inicio = 0;
        while (inicio < linea.Length && (linea[inicio] == ' ' || linea[inicio] == '\t')) inicio++;

        int comentario = IndiceDeComentario(linea, inicio);
        string codigo = comentario >= 0 ? linea.Substring(inicio, comentario - inicio) : linea.Substring(inicio);
        string cola = comentario >= 0 ? linea.Substring(comentario) : null;

        List<string> tokens = Trocear(codigo);
        var aplicadas = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < tokens.Count; i++)
        {
            int igual = tokens[i].IndexOf('=');
            if (igual <= 0) continue;

            string clave = tokens[i].Substring(0, igual);
            if (!cambios.TryGetValue(clave, out string valor)) continue;

            tokens[i] = clave + "=" + valor;
            aplicadas.Add(clave);
        }

        foreach (var kv in cambios)
            if (!aplicadas.Contains(kv.Key))
                tokens.Add(kv.Key + "=" + kv.Value);

        string nuevo = string.Join(" ", tokens);
        string prefijo = linea.Substring(0, inicio);
        return prefijo + nuevo + (cola != null ? " " + cola : string.Empty);
    }

    /// El «#» que empieza un comentario: al principio o precedido de espacio, y fuera de comillas.
    /// Así no se confunde con un color «#FFAA00» pegado a un «=».
    private static int IndiceDeComentario(string linea, int desde)
    {
        bool comillas = false;
        for (int i = desde; i < linea.Length; i++)
        {
            char c = linea[i];
            if (c == '"') { comillas = !comillas; continue; }
            if (comillas || c != '#') continue;
            if (i == desde || char.IsWhiteSpace(linea[i - 1])) return i;
        }
        return -1;
    }

    private static List<string> Trocear(string texto)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool comillas = false;

        foreach (char c in texto)
        {
            if (c == '"') { comillas = !comillas; sb.Append(c); continue; }
            if (!comillas && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }
}
#endif
