using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// Al compilar, saca como error en consola cada aviso CS0618 (API obsoleta) del código propio del
/// proyecto, para que no se quede escondido entre los avisos amarillos. El código de terceros
/// (Assets/Plugins y paquetes que no son nuestros) no se mira. No bloquea la compilación.
[InitializeOnLoad]
internal static class ObsoletosEnRojo
{
    private const string Aviso = "CS0618";

    private static readonly string[] CarpetasPropias =
    {
        "Assets/Scripts/", "Assets/ElSendero/", "Assets/Kits/", "Assets/Editor/",
        "Packages/com.liyodev.narrativegraph/",
    };

    static ObsoletosEnRojo()
    {
        CompilationPipeline.assemblyCompilationFinished -= AlCompilar;
        CompilationPipeline.assemblyCompilationFinished += AlCompilar;
    }

    private static void AlCompilar(string ensamblado, CompilerMessage[] mensajes)
    {
        foreach (var m in mensajes.Where(EsObsoletoPropio))
            Debug.LogError($"[ObsoletosEnRojo] API obsoleta en {m.file}({m.line}): {m.message}\nVer CLAUDE.md § 2, «APIs obsoletas».");
    }

    private static bool EsObsoletoPropio(CompilerMessage m)
    {
        if (m.type != CompilerMessageType.Warning || m.message == null || !m.message.Contains(Aviso)) return false;
        string ruta = (m.file ?? string.Empty).Replace('\\', '/');
        return CarpetasPropias.Any(c => ruta.Contains(c));
    }
}
