#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Datos y página del storyboard anotable (<c>Tools/Storyboard</c>) a partir de un guion horneado: un
/// panel por plano con su foto, su tiempo, lo que se dice, la línea del guion que lo pide y un elemento
/// por personaje que sale o habla (más la cámara). Así la revisión se pega a un elemento concreto y no
/// a «el plano». Se vuelve a pasar después de hornear y la página queda al día. Ver
/// <c>Tools/Storyboard/LEEME.md</c> y INC-1230.
/// </summary>
public static class StoryboardAnotable
{
    private const string CarpetaDeLaPagina = "Tools/Storyboard";
    private const string Plantilla = "index.html";
    private const string DatosDeLaPlantilla = "datos.js";
    private const string Prologo = "Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche_Horneado.asset";

    [MenuItem("El Sendero/Cinemáticas/Guion/Storyboard anotable: generar la página del guion", false, 21)]
    public static void Generar()
    {
        var guion = Selection.activeObject as GuionHorneado
            ?? AssetDatabase.LoadAssetAtPath<GuionHorneado>(Prologo);
        if (guion == null)
        {
            OrdenesDelEditor.Avisar("Storyboard anotable", "Selecciona un guion horneado en el Project.");
            return;
        }

        string storyboard = CarpetaDelStoryboard(guion);
        if (storyboard == null)
        {
            OrdenesDelEditor.Avisar("Storyboard anotable",
                $"No encuentro las fotos horneadas de «{guion.nombre}». Hornea antes con storyboard.");
            return;
        }

        string rutaDelTexto = GuionTextoEditable.RutaDelTexto(guion);
        string[] lineasDelGuion = rutaDelTexto != null && File.Exists(rutaDelTexto)
            ? File.ReadAllLines(rutaDelTexto)
            : Array.Empty<string>();
        var textos = TextosDeCine();
        var actoresPorId = guion.actores
            .Where(a => a != null && !string.IsNullOrEmpty(a.id))
            .GroupBy(a => a.id)
            .ToDictionary(g => g.Key, g => g.First());

        string carpetaPagina = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CarpetaDeLaPagina);
        Directory.CreateDirectory(carpetaPagina);
        string relativa = Path.GetRelativePath(carpetaPagina, storyboard).Replace('\\', '/');
        string nombre = Limpio(guion.nombre);
        string archivoDatos = "datos_" + nombre + ".js";
        string archivoPagina = nombre + ".html";

        var js = new StringBuilder();
        js.Append("// Generado por «Storyboard anotable: generar la página del guion» desde ").Append(guion.name).Append(".\n");
        js.Append("// No se edita a mano: se vuelve a pasar el menú después de hornear.\n");
        js.Append("window.STORYBOARD = {\n");
        js.Append("  id: \"").Append(Js(Id(guion))).Append("\",\n");
        js.Append("  titulo: \"").Append(Js(Titulo(guion, lineasDelGuion))).Append("\",\n");
        js.Append("  subtitulo: \"").Append(Js("Planos del horneado real. Elige un plano, selecciona un elemento de su leyenda y escribe o dibuja: la anotación queda pegada a ese elemento.")).Append("\",\n");
        js.Append("  paneles: [\n");

        for (int i = 0; i < guion.planos.Count; i++)
        {
            var plano = guion.planos[i];
            js.Append("    {\n");
            js.Append("      id: \"").Append($"P{i + 1:00}").Append("\",\n");
            js.Append("      titulo: \"").Append(Js(plano.descripcion)).Append("\",\n");
            js.Append("      tiempo: \"").Append(Reloj(plano.t0)).Append(" – ").Append(Reloj(plano.t1)).Append("\",\n");
            js.Append("      descripcion: \"").Append(Js(LoQueSeDice(guion, plano, textos, actoresPorId))).Append("\",\n");
            js.Append("      guion: \"").Append(Js(OrdenDelGuion(plano, lineasDelGuion))).Append("\",\n");
            js.Append("      imagen: \"").Append(relativa).Append('/').Append($"{i + 1:000}.png").Append("\",\n");

            var elementos = new List<(string id, string etiqueta)>();
            foreach (string sujeto in plano.sujetos)
                AnadirElemento(elementos, sujeto, NombreDe(sujeto, actoresPorId));
            foreach (var linea in guion.lineas)
                if (linea.t1 > plano.t0 && linea.t0 < plano.t1)
                    AnadirElemento(elementos, linea.actor, NombreDe(linea.actor, actoresPorId));
            AnadirElemento(elementos, "camara", "Cámara");

            js.Append("      elementos: [");
            js.Append(string.Join(", ", elementos.Select(e => $"{{ id: \"{Js(e.id)}\", etiqueta: \"{Js(e.etiqueta)}\" }}")));
            js.Append("]\n    }");
            if (i < guion.planos.Count - 1) js.Append(',');
            js.Append('\n');
        }

        js.Append("  ]\n};\n");
        File.WriteAllText(Path.Combine(carpetaPagina, archivoDatos), js.ToString(), new UTF8Encoding(false));

        string plantilla = Path.Combine(carpetaPagina, Plantilla);
        if (!File.Exists(plantilla))
        {
            OrdenesDelEditor.Avisar("Storyboard anotable", $"Falta {CarpetaDeLaPagina}/{Plantilla}.");
            return;
        }

        string html = File.ReadAllText(plantilla)
            .Replace($"<script src=\"{DatosDeLaPlantilla}\"></script>", $"<script src=\"{archivoDatos}\"></script>");
        File.WriteAllText(Path.Combine(carpetaPagina, archivoPagina), html, new UTF8Encoding(false));

        OrdenesDelEditor.Avisar("Storyboard anotable",
            $"{guion.planos.Count} planos → {CarpetaDeLaPagina}/{archivoPagina} (datos en {archivoDatos}).");
    }

    /// <summary>La carpeta de las fotos: Guiones/&lt;escena&gt;/&lt;guion&gt;/storyboard, o se busca por el nombre.</summary>
    private static string CarpetaDelStoryboard(GuionHorneado guion)
    {
        string raiz = CapturaDeEscenario.CarpetaDeGuiones;
        string directa = Path.Combine(raiz, guion.escena ?? string.Empty, guion.nombre ?? string.Empty, "storyboard");
        if (Directory.Exists(directa)) return directa;

        try
        {
            foreach (string carpeta in Directory.GetDirectories(raiz, guion.nombre ?? string.Empty, SearchOption.AllDirectories))
            {
                string candidata = Path.Combine(carpeta, "storyboard");
                if (Directory.Exists(candidata)) return candidata;
            }
        }
        catch (Exception)
        {
            // Sin permiso en alguna carpeta: se queda con la ruta directa, que es el caso normal.
        }
        return null;
    }

    private static string Id(GuionHorneado guion)
        => Regex.Replace((guion.nombre ?? "guion").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string Limpio(string nombre)
        => Regex.Replace(nombre ?? "guion", "[^\\w\\-]+", "_").Trim('_');

    /// <summary>El título del guion: la primera línea de cabecera en mayúsculas; si no, su nombre.</summary>
    private static string Titulo(GuionHorneado guion, string[] lineas)
    {
        foreach (string cruda in lineas.Take(10))
        {
            string l = cruda.Trim();
            if (!l.StartsWith("#")) continue;
            string texto = l.TrimStart('#').Trim(' ', '═', '=', '─', '-', '—', '·');
            if (texto.Length < 8) continue;
            if (texto.StartsWith("GUION ", StringComparison.OrdinalIgnoreCase)) continue;

            int letras = 0, mayusculas = 0;
            foreach (char c in texto)
            {
                if (!char.IsLetter(c)) continue;
                letras++;
                if (char.IsUpper(c)) mayusculas++;
            }
            if (letras >= 6 && mayusculas >= letras * 0.8f) return texto;
        }
        return (guion.nombre ?? "Guion").Replace('_', ' ');
    }

    private static string Reloj(float t)
    {
        int minutos = (int)(t / 60f);
        float segundos = t - minutos * 60f;
        return $"{minutos}:{segundos.ToString("00.0", CultureInfo.GetCultureInfo("es-ES"))}";
    }

    /// <summary>Lo que se dice en el plano, con el nombre de quien lo dice.</summary>
    private static string LoQueSeDice(GuionHorneado guion, PlanoHorneado plano,
        Dictionary<string, string> textos, Dictionary<string, ActorHorneado> actoresPorId)
    {
        var partes = new List<string>();
        foreach (var linea in guion.lineas)
        {
            if (linea.t1 <= plano.t0 || linea.t0 >= plano.t1) continue;
            string quien = NombreDe(linea.actor, actoresPorId);
            string que = textos.TryGetValue(linea.clave ?? string.Empty, out string texto) ? texto : (linea.clave ?? string.Empty);
            partes.Add($"{quien}: «{que}»");
        }
        return string.Join(" · ", partes);
    }

    /// <summary>La orden del guion que pide el plano, con su número de línea para poder cambiarla.</summary>
    private static string OrdenDelGuion(PlanoHorneado plano, string[] lineas)
    {
        if (plano.linea <= 0 || plano.linea > lineas.Length) return string.Empty;
        return $"L{plano.linea}: {lineas[plano.linea - 1].Trim()}";
    }

    private static void AnadirElemento(List<(string id, string etiqueta)> lista, string id, string etiqueta)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (lista.Any(e => e.id == id)) return;
        lista.Add((id, etiqueta));
    }

    private static string NombreDe(string id, Dictionary<string, ActorHorneado> actoresPorId)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        if (!actoresPorId.TryGetValue(id, out var actor)) return id;
        string alias = actor.alias ?? actor.id ?? id;
        return alias.Length > 0 ? char.ToUpperInvariant(alias[0]) + alias.Substring(1) : id;
    }

    /// <summary>Los textos en español de las frases, como los lee el horneado.</summary>
    private static Dictionary<string, string> TextosDeCine()
    {
        var textos = new Dictionary<string, string>();
        foreach (string ruta in new[]
                 {
                     "Assets/Resources/Localization/cinematics_es.json",
                     "Assets/Resources/Localization/dialogues_es.json",
                 })
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ruta);
            if (asset == null) continue;
            foreach (Match m in Regex.Matches(asset.text,
                         "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"text\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
                textos[m.Groups[1].Value] = Regex.Unescape(m.Groups[2].Value);
        }
        return textos;
    }

    private static string Js(string texto) => (texto ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", string.Empty)
        .Replace("\n", "\\n")
        .Replace("\t", " ");
}
#endif
