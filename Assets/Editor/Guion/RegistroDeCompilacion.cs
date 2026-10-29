#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;

/// Deja el resultado de cada compilación del Editor en «Guiones/_ordenes/compilacion.log»:
/// los errores y avisos de cada ensamblado y una última línea «FIN errores=N avisos=M».
/// Sirve para comprobar desde fuera del Editor (buzón de órdenes) que un cambio compila.
/// Si la compilación falla, el Editor sigue con los ensamblados anteriores, así que este
/// registro sigue escuchando y apunta también los fallos.
[InitializeOnLoad]
public static class RegistroDeCompilacion
{
    private static readonly StringBuilder s_texto = new StringBuilder();
    private static int s_errores;
    private static int s_avisos;

    private static string Ruta => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes", "compilacion.log");

    static RegistroDeCompilacion()
    {
        CompilationPipeline.compilationStarted -= AlEmpezar;
        CompilationPipeline.compilationStarted += AlEmpezar;
        CompilationPipeline.assemblyCompilationFinished -= AlTerminarEnsamblado;
        CompilationPipeline.assemblyCompilationFinished += AlTerminarEnsamblado;
        CompilationPipeline.compilationFinished -= AlTerminar;
        CompilationPipeline.compilationFinished += AlTerminar;
    }

    private static void AlEmpezar(object _)
    {
        s_texto.Clear();
        s_errores = 0;
        s_avisos = 0;
        s_texto.AppendLine($"INICIO {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private static void AlTerminarEnsamblado(string ensamblado, CompilerMessage[] mensajes)
    {
        foreach (var m in mensajes)
        {
            if (m.type == CompilerMessageType.Error) s_errores++;
            else if (m.type == CompilerMessageType.Warning) s_avisos++;
            else continue;
            s_texto.AppendLine($"{(m.type == CompilerMessageType.Error ? "ERROR" : "AVISO")} {m.message}");
        }
    }

    private static void AlTerminar(object _)
    {
        s_texto.AppendLine($"FIN {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} errores={s_errores} avisos={s_avisos}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
            File.WriteAllText(Ruta, s_texto.ToString());
        }
        catch (IOException) { }
    }
}
#endif
