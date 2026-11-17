#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Arnés de un solo uso para comprobar GuionBatchEdit sin depender del Test Runner (el proyecto
/// arrastra un fallo de `Assembly-CSharp-check` que impide lanzar los tests). Se retira a
/// «Versiones antiguas» en cuanto se usa (regla 8 de AGENTS.md).
public static class ProbarGuionBatchEditTemporal
{
    private const string Base = @"GUION Prueba
ESCENA MainWorld
SECUENCIA Assets/_SEQUENCES/SEQ_Prueba.asset
TEXTO subtitulo
REPARTO
will Player nombre=Will
PUNTOS
inicio 0 0
HILO
@inicio
will en inicio
plano medio will lente=30 # encuadre
will dice SALUDO
espera 1
";

    private static int _ok;
    private static int _fallos;
    private static readonly StringBuilder s_log = new();

    [MenuItem("El Sendero/Cinemáticas/Probar el lote de guion (temporal)")]
    public static void Probar()
    {
        _ok = 0;
        _fallos = 0;
        s_log.Clear();

        string json;

        json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\",\"options\":[{\"name\":\"lente\",\"value\":\"24\"}]}]}";
        Comprobar("set_options conserva comentario y opciones",
            GuionBatchEdit.AplicarTexto(Base, json, out string nuevo, out var r) &&
            nuevo.Contains("plano medio will lente=24 # encuadre"), Errores(r));

        json = "{\"ops\":[{\"op\":\"insert_after\",\"anchor\":\"PUNTOS\",\"text\":\"fuego 10 10\"}," +
               "{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\",\"options\":[{\"name\":\"lente\",\"value\":\"20\"}]}]}";
        Comprobar("insertar en PUNTOS no desplaza la línea del plano",
            GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            nuevo.Contains("fuego 10 10") && nuevo.Contains("plano medio will lente=20") &&
            nuevo.IndexOf("fuego 10 10") < nuevo.IndexOf("plano medio"), Errores(r));

        json = "{\"ops\":[{\"op\":\"replace_line\",\"line\":12,\"match\":\"plano medio\",\"text\":\"plano medio will lente=24\"}]}";
        Comprobar("replace_line conserva el comentario final",
            GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            nuevo.Contains("plano medio will lente=24 # encuadre"), Errores(r));

        json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"plano medio\",\"options\":[{\"name\":\"lente\",\"value\":\"\"}]}]}";
        Comprobar("set_options con valor vacío quita la opción",
            GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            !nuevo.Contains("lente=30") && nuevo.Contains("plano medio will # encuadre"), Errores(r));

        json = "{\"ops\":[{\"op\":\"delete_line\",\"line\":14,\"match\":\"espera 1\"}]}";
        Comprobar("delete_line quita solo esa línea",
            GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            !nuevo.Contains("espera 1") && nuevo.Contains("will dice SALUDO"), Errores(r));

        json = "{\"ops\":[{\"op\":\"replace_line\",\"line\":99,\"text\":\"plano medio will\"}]}";
        Comprobar("una línea que no existe no aplica nada",
            !GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            nuevo == Base && Errores(r).Contains("no existe"), Errores(r));

        json = "{\"ops\":[{\"op\":\"set_options\",\"line\":12,\"match\":\"primer plano\",\"options\":[{\"name\":\"lente\",\"value\":\"24\"}]}]}";
        Comprobar("un match que no coincide no aplica nada",
            !GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            nuevo == Base && Errores(r).Contains("no contiene"), Errores(r));

        json = "{\"ops\":[{\"op\":\"insert_after\",\"anchor\":\"HILO\",\"text\":\"efecto NoExiste\"}]}";
        Comprobar("un efecto inexistente no aplica nada",
            !GuionBatchEdit.AplicarTexto(Base, json, out nuevo, out r) &&
            nuevo == Base && Errores(r).Contains("NoExiste"), Errores(r));

        s_log.AppendLine($"fin: {_ok} ok, {_fallos} fallos");
        string ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "guion_batch_edit_tests.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllText(ruta, s_log.ToString());
        Debug.LogWarning($"[GuionBatchEditTests] fin: {_ok} ok, {_fallos} fallos");
    }

    private static void Comprobar(string nombre, bool bien, string detalle)
    {
        if (bien) { _ok++; s_log.AppendLine("OK " + nombre); Debug.LogWarning($"[GuionBatchEditTests] OK {nombre}"); }
        else { _fallos++; s_log.AppendLine("FALLO " + nombre + ": " + detalle); Debug.LogError($"[GuionBatchEditTests] FALLO {nombre}: {detalle}"); }
    }

    private static string Errores(GuionBatchEdit.Resultado r)
        => r == null ? "sin resultado" : string.Join(" · ", r.Errors);
}
#endif
