#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Las fotos de las pruebas de cinemática. Viven en <c>Library/Director/rodaje</c> (fuera del
/// proyecto, comprimidas y con tope); aquí se encienden, se apagan y se limpian sin tocar ficheros
/// a mano. El parte de rodaje (<c>Logs/Rodaje/*.md</c>) se queda como está: pesa nada y es el
/// diagnóstico útil. Ver INC-587 e INC-1250.
/// </summary>
public static class CapturasDeRodaje
{
    private static string Proyecto => Directory.GetParent(Application.dataPath).FullName;
    private static string Raiz => Path.Combine(Proyecto, "Library", "Director", "rodaje");
    private static string Bandera => Path.Combine(Proyecto, "Guiones", "_ordenes", "fotos.on");

    [MenuItem("El Sendero/Pruebas/Rodaje/Capturar fotos en las pruebas: activar", priority = 30)]
    public static void Activar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Bandera));
        File.WriteAllText(Bandera, string.Empty);
        OrdenesDelEditor.Avisar("Capturas de rodaje",
            "Activadas. Se guardan en Library/Director/rodaje, en JPG y con tope; se conservan las 3 últimas sesiones. " +
            "Vuelve a pasar este menú para desactivarlas.");
    }

    [MenuItem("El Sendero/Pruebas/Rodaje/Capturar fotos en las pruebas: desactivar", priority = 31)]
    public static void Desactivar()
    {
        if (File.Exists(Bandera)) File.Delete(Bandera);
        OrdenesDelEditor.Avisar("Capturas de rodaje", "Desactivadas: las pruebas ya no guardan fotos.");
    }

    [MenuItem("El Sendero/Pruebas/Rodaje/Limpiar las capturas", priority = 32)]
    public static void Limpiar()
    {
        if (!Directory.Exists(Raiz))
        {
            OrdenesDelEditor.Avisar("Capturas de rodaje", "No hay capturas guardadas.");
            return;
        }

        double mb = TamanoDe(Raiz) / (1024.0 * 1024.0);
        if (!OrdenesDelEditor.Confirmar("Capturas de rodaje",
                $"¿Borrar {mb:0.0} MB de capturas de Library/Director/rodaje?", "Borrar"))
            return;

        try
        {
            Directory.Delete(Raiz, true);
            OrdenesDelEditor.Avisar("Capturas de rodaje", $"Borradas {mb:0.0} MB.");
        }
        catch (Exception ex)
        {
            OrdenesDelEditor.Avisar("Capturas de rodaje", "No pude borrarlas: " + ex.Message);
        }
    }

    private static long TamanoDe(string carpeta)
    {
        if (!Directory.Exists(carpeta)) return 0;
        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(carpeta).EnumerateFiles("*", SearchOption.AllDirectories))
                total += f.Length;
        }
        catch { }
        return total;
    }
}
#endif
