#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Liyodev.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Aplica un lote de operaciones de la IA al .guion.txt de una cinemática. Las líneas son las
/// `L1`, `L2`… que ve la IA en el estado (1-based, sobre el texto original). Todo se valida y se
/// hornea en memoria antes de escribir nada: si el horneado no acepta el cambio, el guion queda
/// como estaba. Al final guarda una copia del texto anterior y cierra los encargos pedidos.
public static class GuionBatchEdit
{
    [Serializable]
    public sealed class Opcion
    {
        public string name;
        public string value;
    }

    [Serializable]
    public sealed class Operacion
    {
        public string op;
        public int line;
        public string match;
        public string text;
        public string anchor;
        public string task;
        public List<Opcion> options = new();
    }

    [Serializable]
    public sealed class Lote
    {
        public List<Operacion> ops = new();
    }

    public sealed class Resultado
    {
        public bool Applied;
        public string Resumen = string.Empty;
        public readonly List<string> Done = new();
        public readonly List<string> Diff = new();
        public readonly List<string> Warnings = new();
        public readonly List<string> Errors = new();
        public readonly List<string> Cerrar = new();
    }

    /// Aplica el lote sobre el horneado actual: texto, validación, horneado en memoria, diff y,
    /// solo si todo sale bien, escribe el guion, guarda el horneado y cierra encargos.
    public static Resultado Aplicar(GuionHorneado actual, string json)
    {
        var resultado = new Resultado();
        if (actual == null)
        {
            resultado.Errors.Add("No hay horneado para esa cinemática.");
            resultado.Resumen = "No se aplicó nada.";
            return resultado;
        }

        string ruta = GuionTextoEditable.RutaDelTexto(actual);
        if (string.IsNullOrEmpty(ruta) || !File.Exists(RutaFisica(ruta)))
        {
            resultado.Errors.Add("No encuentro el .guion.txt de esa cinemática.");
            resultado.Resumen = "No se aplicó nada.";
            return resultado;
        }

        string fuente = File.ReadAllText(RutaFisica(ruta));
        if (!AplicarTexto(fuente, json, out string nuevo, out var texto))
        {
            resultado.Errors.AddRange(texto.Errors);
            resultado.Done.AddRange(texto.Done);
            resultado.Cerrar.AddRange(texto.Cerrar);
            resultado.Resumen = "No se aplicó nada: el lote no es válido.";
            return resultado;
        }

        resultado.Done.AddRange(texto.Done);
        resultado.Cerrar.AddRange(texto.Cerrar);

        if (string.Equals(nuevo, fuente, StringComparison.Ordinal))
        {
            CerrarEncargos(actual, resultado);
            resultado.Applied = true;
            resultado.Resumen = "Sin cambios de texto; encargos cerrados.";
            return resultado;
        }

        var abiertasAntes = EscenasAbiertas();
        Horno horno = null;
        GuionHorneado nuevoHorneado = null;
        try
        {
            nuevoHorneado = HorneadorDeGuion.HornearParaRevisar(ruta, nuevo, out horno, storyboard: false);
        }
        catch (Exception e)
        {
            CerrarEscenasNuevas(abiertasAntes);
            resultado.Errors.Add("El horneado se ha parado: " + e.Message);
            resultado.Resumen = "El horneado no ha aceptado el cambio: no se ha escrito nada.";
            return resultado;
        }

        if (nuevoHorneado == null || horno == null)
        {
            CerrarEscenasNuevas(abiertasAntes);
            resultado.Errors.Add("El horneado no ha producido nada.");
            resultado.Resumen = "El horneado no ha aceptado el cambio: no se ha escrito nada.";
            return resultado;
        }

        if (horno.Errores.Count > 0)
        {
            resultado.Errors.AddRange(horno.Errores);
            CerrarEscenasNuevas(abiertasAntes);
            resultado.Resumen = "El horneado no ha aceptado el cambio: no se ha escrito nada.";
            return resultado;
        }

        resultado.Warnings.AddRange(horno.Avisos);
        resultado.Diff.AddRange(GuionDiff.Comparar(actual, nuevoHorneado));

        string respaldo = RutaRespaldo(actual, ruta);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(respaldo));
            File.WriteAllText(respaldo, fuente, new UTF8Encoding(false));

            Undo.RecordObject(actual, "Guion desde el asistente");
            File.WriteAllText(RutaFisica(ruta), nuevo, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            horno.GuardarResultado();
            horno.EscribirInforme();
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            CerrarEscenasNuevas(abiertasAntes);
            resultado.Errors.Add("No pude guardar el cambio: " + e.Message);
            resultado.Resumen = "El horneado se aceptó, pero no se pudo guardar: no des por hecho el cambio.";
            return resultado;
        }
        CerrarEscenasNuevas(abiertasAntes);

        CerrarEncargos(actual, resultado);
        resultado.Applied = true;
        resultado.Resumen = $"Aplicado y rehorneado · {resultado.Diff.Count} cambio(s) en el horneado.";
        CinematicsEditorServices.AvisarCambio();
        return resultado;
    }

    /// La parte pura: aplica las operaciones sobre un texto, sin tocar archivos ni hornear. Sirve
    /// para tests y para comprobar el lote antes de meterse en la escena.
    public static bool AplicarTexto(string fuente, string json, out string nuevo, out Resultado resultado)
    {
        nuevo = fuente;
        resultado = new Resultado();
        if (string.IsNullOrWhiteSpace(json))
        {
            resultado.Errors.Add("No hay operaciones.");
            return false;
        }

        Lote lote;
        try { lote = JsonUtility.FromJson<Lote>(json); }
        catch (Exception e)
        {
            resultado.Errors.Add("JSON inválido: " + e.Message);
            return false;
        }
        if (lote?.ops == null || lote.ops.Count == 0)
        {
            resultado.Errors.Add("No hay operaciones.");
            return false;
        }

        string salto = fuente.Contains("\r\n") ? "\r\n" : "\n";
        string[] lineas = fuente.Replace("\r\n", "\n").Split('\n');

        var mutaciones = new Dictionary<int, string>();
        var insertsAntes = new Dictionary<int, List<string>>();
        var insertsDespues = new Dictionary<int, List<string>>();
        var borradas = new HashSet<int>();
        var reemplazadas = new HashSet<int>();

        for (int i = 0; i < lote.ops.Count; i++)
        {
            var op = lote.ops[i];
            string etiqueta = $"#{i + 1} {op?.op}";
            if (op == null || string.IsNullOrWhiteSpace(op.op))
            {
                resultado.Errors.Add($"{etiqueta}: falta 'op'.");
                continue;
            }

            switch (op.op.Trim().ToLowerInvariant())
            {
                case "set_options":
                {
                    if (!LineaValida(op, lineas, out int linea, out string error))
                    {
                        resultado.Errors.Add($"{etiqueta}: {error}");
                        break;
                    }
                    if (borradas.Contains(linea))
                    {
                        resultado.Errors.Add($"{etiqueta}: la línea {linea} se borra en el mismo lote.");
                        break;
                    }

                    var opciones = op.options ?? new List<Opcion>();
                    if (opciones.Count == 0)
                    {
                        resultado.Errors.Add($"{etiqueta}: no hay opciones.");
                        break;
                    }

                    var cambios = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var opcion in opciones)
                    {
                        if (opcion == null || string.IsNullOrWhiteSpace(opcion.name))
                        {
                            resultado.Errors.Add($"{etiqueta}: opción sin nombre.");
                            continue;
                        }
                        cambios[opcion.name.Trim()] = opcion.value ?? string.Empty;
                    }
                    if (cambios.Count == 0) break;

                    string actual = mutaciones.TryGetValue(linea, out var puesta) && puesta != null
                        ? puesta
                        : lineas[linea - 1];
                    if (actual.Contains("\n"))
                    {
                        resultado.Errors.Add($"{etiqueta}: la línea {linea} ya es de varias líneas.");
                        break;
                    }

                    mutaciones[linea] = Reescribir(actual, cambios);
                    resultado.Done.Add($"L{linea}: " + string.Join(", ",
                        cambios.Select(kv => kv.Key + (string.IsNullOrEmpty(kv.Value) ? " (quitar)" : "=" + kv.Value))));
                    break;
                }

                case "replace_line":
                {
                    if (!LineaValida(op, lineas, out int linea, out string error))
                    {
                        resultado.Errors.Add($"{etiqueta}: {error}");
                        break;
                    }
                    if (borradas.Contains(linea))
                    {
                        resultado.Errors.Add($"{etiqueta}: la línea {linea} se borra en el mismo lote.");
                        break;
                    }
                    if (reemplazadas.Contains(linea))
                    {
                        resultado.Errors.Add($"{etiqueta}: la línea {linea} ya se reescribe en el mismo lote.");
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(op.text))
                    {
                        resultado.Errors.Add($"{etiqueta}: falta 'text'.");
                        break;
                    }

                    string comentario = ComentarioDe(lineas[linea - 1]);
                    string texto = op.text;
                    if (!string.IsNullOrEmpty(comentario) && texto.IndexOf('#') < 0)
                        texto += " " + comentario;

                    mutaciones[linea] = texto;
                    reemplazadas.Add(linea);
                    resultado.Done.Add($"L{linea}: reescrita");
                    break;
                }

                case "delete_line":
                {
                    if (!LineaValida(op, lineas, out int linea, out string error))
                    {
                        resultado.Errors.Add($"{etiqueta}: {error}");
                        break;
                    }
                    if (mutaciones.ContainsKey(linea) || reemplazadas.Contains(linea))
                    {
                        resultado.Errors.Add($"{etiqueta}: no se puede borrar y cambiar la línea {linea}.");
                        break;
                    }

                    mutaciones[linea] = null;
                    borradas.Add(linea);
                    resultado.Done.Add($"L{linea}: borrada");
                    break;
                }

                case "insert_after":
                case "insert_before":
                {
                    if (!DestinoInsercion(op, lineas, out int linea, out string error))
                    {
                        resultado.Errors.Add($"{etiqueta}: {error}");
                        break;
                    }
                    var nuevas = LineasDe(op.text);
                    if (nuevas == null)
                    {
                        resultado.Errors.Add($"{etiqueta}: falta 'text'.");
                        break;
                    }

                    bool despues = op.op.Trim().Equals("insert_after", StringComparison.OrdinalIgnoreCase);
                    var destino = despues ? insertsDespues : insertsAntes;
                    if (!destino.TryGetValue(linea, out var lista))
                    {
                        lista = new List<string>();
                        destino[linea] = lista;
                    }
                    lista.AddRange(nuevas);
                    resultado.Done.Add($"L{linea}: insertada(s) {nuevas.Count} línea(s) {(despues ? "después" : "antes")}");
                    break;
                }

                case "resolve_task":
                {
                    if (string.IsNullOrWhiteSpace(op.task))
                    {
                        resultado.Errors.Add($"{etiqueta}: falta 'task'.");
                        break;
                    }
                    resultado.Cerrar.Add(op.task.Trim());
                    resultado.Done.Add($"encargo {op.task.Trim()}: cerrado al aplicar");
                    break;
                }

                default:
                    resultado.Errors.Add($"{etiqueta}: operación desconocida (set_options, replace_line, insert_after, insert_before, delete_line, resolve_task).");
                    break;
            }
        }

        if (resultado.Errors.Count > 0) return false;

        var salida = new List<string>(lineas.Length + 8);
        for (int i = 0; i < lineas.Length; i++)
        {
            int n = i + 1;
            if (insertsAntes.TryGetValue(n, out var antes)) salida.AddRange(antes);
            if (!mutaciones.TryGetValue(n, out var cambio)) salida.Add(lineas[i]);
            else if (cambio != null) salida.AddRange(cambio.Replace("\r\n", "\n").Split('\n'));
            if (insertsDespues.TryGetValue(n, out var despues)) salida.AddRange(despues);
        }
        nuevo = string.Join(salto, salida);

        var guion = GuionTexto.Leer(nuevo);
        foreach (string error in guion.errores) resultado.Errors.Add(error);
        var erroresDeEfectos = resultado.Errors;
        foreach (var orden in guion.hilo.Concat(guion.pistas).Where(o => o.verbo == "efecto"))
            FabricaDeEfectos.Crear(orden.args.FirstOrDefault(), orden.opciones, orden,
                mensaje => erroresDeEfectos.Add(mensaje), null);

        if (resultado.Errors.Count > 0)
        {
            nuevo = fuente;
            return false;
        }
        return true;
    }

    private static bool LineaValida(Operacion op, string[] lineas, out int linea, out string error)
    {
        linea = op.line;
        error = null;
        if (linea < 1 || linea > lineas.Length)
        {
            error = $"la línea {linea} no existe (1..{lineas.Length}).";
            return false;
        }
        if (!string.IsNullOrEmpty(op.match) && !lineas[linea - 1].Contains(op.match, StringComparison.Ordinal))
        {
            error = $"la línea {linea} no contiene «{op.match}».";
            return false;
        }
        return true;
    }

    private static bool DestinoInsercion(Operacion op, string[] lineas, out int linea, out string error)
    {
        if (op.line > 0) return LineaValida(op, lineas, out linea, out error);

        error = null;
        linea = 0;
        if (string.IsNullOrWhiteSpace(op.anchor))
        {
            error = "falta 'line' o 'anchor'.";
            return false;
        }

        linea = BuscarAncla(lineas, op.anchor.Trim());
        if (linea <= 0)
        {
            error = $"no encuentro el ancla «{op.anchor}».";
            return false;
        }
        return true;
    }

    private static int BuscarAncla(string[] lineas, string ancla)
    {
        for (int i = 0; i < lineas.Length; i++)
        {
            string t = lineas[i].Trim();
            if (string.Equals(t, ancla, StringComparison.OrdinalIgnoreCase)) return i + 1;
            if (t.StartsWith(ancla + " ", StringComparison.OrdinalIgnoreCase)) return i + 1;
        }
        return -1;
    }

    private static List<string> LineasDe(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return texto.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
    }

    private static string Reescribir(string linea, Dictionary<string, string> cambios)
    {
        var quitar = new HashSet<string>(StringComparer.Ordinal);
        var poner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in cambios)
        {
            if (string.IsNullOrEmpty(kv.Value)) quitar.Add(kv.Key);
            else
            {
                string valor = kv.Value;
                if (valor.IndexOf(' ') >= 0 && !valor.StartsWith("\"")) valor = "\"" + valor + "\"";
                poner[kv.Key] = valor;
            }
        }

        string nuevo = poner.Count > 0 ? GuionTextoEditable.ReescribirLinea(linea, poner) : linea;
        if (quitar.Count > 0) nuevo = QuitarOpciones(nuevo, quitar);
        return nuevo;
    }

    private static string QuitarOpciones(string linea, HashSet<string> claves)
    {
        int comentario = IndiceComentario(linea);
        string codigo = comentario >= 0 ? linea.Substring(0, comentario) : linea;
        string cola = comentario >= 0 ? linea.Substring(comentario) : null;
        var tokens = codigo.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        tokens.RemoveAll(t =>
        {
            int igual = t.IndexOf('=');
            return igual > 0 && claves.Contains(t.Substring(0, igual));
        });
        string nuevo = string.Join(" ", tokens);
        return cola != null ? nuevo + " " + cola : nuevo;
    }

    private static string ComentarioDe(string linea)
    {
        int i = IndiceComentario(linea);
        return i >= 0 ? linea.Substring(i) : null;
    }

    private static int IndiceComentario(string linea)
    {
        bool comillas = false;
        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];
            if (c == '"') { comillas = !comillas; continue; }
            if (comillas || c != '#') continue;
            if (i == 0 || char.IsWhiteSpace(linea[i - 1])) return i;
        }
        return -1;
    }

    private static string RutaFisica(string rutaAsset)
        => Path.IsPathRooted(rutaAsset)
            ? rutaAsset
            : Path.Combine(Directory.GetParent(Application.dataPath).FullName, rutaAsset);

    private static HashSet<string> EscenasAbiertas()
    {
        var abiertas = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            abiertas.Add(SceneManager.GetSceneAt(i).path);
        return abiertas;
    }

    private static void CerrarEscenasNuevas(HashSet<string> abiertasAntes)
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (abiertasAntes.Contains(escena.path) || SceneManager.sceneCount < 2) continue;
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static string RutaRespaldo(GuionHorneado actual, string rutaTexto)
    {
        string nombre = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(rutaTexto));
        if (string.IsNullOrEmpty(nombre)) nombre = actual.nombre;
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Guiones", "_revisiones",
            "ultimo-antes-de-ia-" + nombre + ".guion.txt");
    }

    private static void CerrarEncargos(GuionHorneado actual, Resultado resultado)
    {
        if (resultado.Cerrar.Count == 0) return;

        bool todas = resultado.Cerrar.Any(t =>
            string.IsNullOrWhiteSpace(t) || t.Equals("all", StringComparison.OrdinalIgnoreCase));
        if (todas)
        {
            resultado.Done.Add(SenderoCinematicsBridge.MarcarRevisionesHechas(actual.nombre));
            return;
        }

        foreach (string tarea in resultado.Cerrar.Distinct())
            resultado.Done.Add(SenderoCinematicsBridge.MarcarRevisionesHechas(actual.nombre + " " + tarea));
    }
}
#endif
