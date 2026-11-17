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

    /// <summary>Nombre de pantalla de los efectos que interesa poder anotar; los que no están, no salen.</summary>
    private static readonly Dictionary<string, string> NombresDeEfecto = new()
    {
        ["BOLA_PROTECTORA"] = "Orbe",
        ["CIRCULO_PLEGARIA"] = "Círculo mágico",
        ["CARGA_ESFERA"] = "Agujero negro",
        ["CARGA_CRECE"] = "Círculo oscuro",
        ["FORCEJEO"] = "Forcejeo",
    };

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
        Generar(guion);
    }

    /// <summary>
    /// La página de un guion concreto, sin depender de lo que haya seleccionado en el Project: así la
    /// puede pedir el buzón de órdenes. Devuelve lo que dejó escrito, o el motivo de no haberlo hecho.
    /// </summary>
    public static string Generar(GuionHorneado guion)
    {
        if (guion == null) return "no hay guion horneado";

        string storyboard = CarpetaDelStoryboard(guion);
        if (storyboard == null)
        {
            string aviso = $"No encuentro las fotos horneadas de «{guion.nombre}». Hornea antes con storyboard.";
            OrdenesDelEditor.Avisar("Storyboard anotable", aviso);
            return aviso;
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
        js.Append("  sello: \"").Append(Js(SelloDelHorneado(guion))).Append("\",\n");
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
            foreach (var efecto in guion.efectos)
                if (efecto.beat is SayBeat dice && !string.IsNullOrEmpty(dice.textKey) &&
                    efecto.t >= plano.t0 && efecto.t < plano.t1)
                    AnadirElemento(elementos,
                        string.IsNullOrEmpty(dice.markName) ? dice.speakerNameKey : dice.markName,
                        string.IsNullOrEmpty(dice.speakerNameKey) ? "Voz" : dice.speakerNameKey);
            foreach (var efecto in EfectosConNombre(guion))
                if (efecto.hasta > plano.t0 && efecto.desde < plano.t1 &&
                    NombresDeEfecto.TryGetValue(efecto.id, out string etiqueta))
                    AnadirElemento(elementos, efecto.id, etiqueta);
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
            string aviso = $"Falta {CarpetaDeLaPagina}/{Plantilla}.";
            OrdenesDelEditor.Avisar("Storyboard anotable", aviso);
            return aviso;
        }

        string html = File.ReadAllText(plantilla)
            .Replace($"<script src=\"{DatosDeLaPlantilla}\"></script>", $"<script src=\"{archivoDatos}\"></script>");
        File.WriteAllText(Path.Combine(carpetaPagina, archivoPagina), html, new UTF8Encoding(false));

        string hecho = $"{guion.planos.Count} planos → {CarpetaDeLaPagina}/{archivoPagina} (datos en {archivoDatos}).";
        OrdenesDelEditor.Avisar("Storyboard anotable", hecho);
        return hecho;    }

    /// <summary>
    /// Los efectos que se registran con un nombre, y lo que duran: así un plano puede anotar «el orbe» o
    /// «el círculo» aunque no sean personajes. Los que no declaran duración valen hasta el final.
    /// </summary>
    private static IEnumerable<(float desde, float hasta, string id)> EfectosConNombre(GuionHorneado guion)
    {
        foreach (var efecto in guion.efectos)
        {
            string id = null;
            float duracion = 0f;
            switch (efecto.beat)
            {
                case ConjuroEnLasManosBeat conjuro when !string.IsNullOrEmpty(conjuro.registrarComo):
                    id = conjuro.registrarComo;
                    duracion = conjuro.duracion > 0f ? conjuro.duracion : conjuro.vidaEnPool;
                    break;
                case VfxBeat vfx when !string.IsNullOrEmpty(vfx.registrarComo):
                    id = vfx.registrarComo;
                    duracion = vfx.lifetime;
                    break;
            }
            if (id == null) continue;
            yield return (efecto.t, duracion > 0f ? efecto.t + duracion : float.MaxValue, id);
        }
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

    /// <summary>
    /// El título del guion: la primera línea de cabecera en mayúsculas; si el guion no tiene ninguna
    /// (los hay que empiezan con la sinopsis), la primera frase de su primer comentario. Si no, su nombre.
    /// </summary>
    private static string Titulo(GuionHorneado guion, string[] lineas)
    {
        string primera = null;
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
            if (primera == null) primera = texto;
        }

        if (primera != null)
        {
            int punto = primera.IndexOf('.', 8);
            string frase = punto > 10 ? primera.Substring(0, punto) : primera;
            return frase.Length > 90 ? frase.Substring(0, 90).TrimEnd() + "…" : frase;
        }
        return (guion.nombre ?? "Guion").Replace('_', ' ');
    }

    /// La fecha del horneado, para que una pestaña vieja se note a simple vista.
    private static string SelloDelHorneado(GuionHorneado guion)
    {
        try
        {
            string ruta = AssetDatabase.GetAssetPath(guion);
            if (!string.IsNullOrEmpty(ruta) && File.Exists(ruta))
                return "Horneado el " + File.GetLastWriteTime(ruta).ToString("dd MMM yyyy · HH:mm");
        }
        catch
        {
            // Sin fecha: la página sale igual, solo se queda sin sello.
        }
        return string.Empty;
    }

    private static string Reloj(float t)    {
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

        // Las voces fuera de escena (SayBeat con marca) no son líneas: si no se leen aquí, el plano
        // con la discusión de la ventana sale mudo aunque tenga tres frases.
        foreach (var efecto in guion.efectos)
        {
            if (!(efecto.beat is SayBeat dice) || string.IsNullOrEmpty(dice.textKey)) continue;
            if (efecto.t < plano.t0 || efecto.t >= plano.t1) continue;
            string quien = !string.IsNullOrEmpty(dice.speakerNameKey) ? dice.speakerNameKey
                         : string.IsNullOrEmpty(dice.markName) ? "Voz" : dice.markName;
            string que = textos.TryGetValue(dice.textKey, out string dicho) ? dicho : dice.textKey;
            partes.Add($"{quien} (fuera de escena): «{que}»");
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
