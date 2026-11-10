#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// Los capítulos 2 (despedida), 3 y 4 son historia, no «la demo»: mueve sus misiones, diálogos y
/// materiales a carpetas por capítulo (con AssetDatabase, se conservan las referencias) y renombra
/// la raíz generada en MainWorld. El corte de la demo (ConfiguracionDeLaDemo, FinDeLaDemoNode) no
/// se toca. Idempotente. Ver INC-970.
public static class QuitarDemoDeLosCapitulos
{
    private const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string RaizVieja = "Capítulos de la demo (generado)";
    private const string RaizNueva = "Capítulos (generado)";

    [MenuItem("El Sendero/Archivo/Capítulos: quitar «demo» de carpetas y escena (INC-970)")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) { OrdenesDelEditor.Avisar("Quitar demo", "Sal de Play."); return; }
        var informe = new StringBuilder();
        Mover("Assets/_QUEST/PRINCIPALES/DEMO", n => "Assets/_QUEST/PRINCIPALES/" + Capitulo(n).ToUpperInvariant(), n => n, informe);
        Mover("Assets/_DIALOGUES/Demo", n => "Assets/_DIALOGUES/" + Capitulo(n), n => n, informe);
        Mover("Assets/_DIALOGUES/Capitulos", n => "Assets/_DIALOGUES/" + Capitulo(n), n => n, informe);
        Mover("Assets/Art/Demo", n => "Assets/Art/Capitulos", n => n.Replace("Demo_", "Cap4_"), informe);
        RenombrarRaiz(informe);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Quitar demo", informe.ToString());
    }

    private static string Capitulo(string archivo)
    {
        var m = Regex.Match(archivo, @"CAP(\d+)");
        if (m.Success) return "Cap" + m.Groups[1].Value;
        return archivo.Contains("_ALDEA_") ? "AldeaDelMar" : "Capitulos";
    }

    private static void Mover(string origen, System.Func<string, string> destino, System.Func<string, string> nombre, StringBuilder informe)
    {
        if (!AssetDatabase.IsValidFolder(origen)) { informe.AppendLine($"{origen}: ya no existe."); return; }
        foreach (var guid in AssetDatabase.FindAssets("", new[] { origen }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(ruta) || Path.GetDirectoryName(ruta).Replace('\\', '/') != origen) continue;
            string archivo = Path.GetFileNameWithoutExtension(ruta);
            string carpeta = destino(archivo);
            Asegurar(carpeta);
            string nueva = $"{carpeta}/{nombre(archivo)}{Path.GetExtension(ruta)}";
            string error = AssetDatabase.MoveAsset(ruta, nueva);
            informe.AppendLine(string.IsNullOrEmpty(error) ? $"{ruta} → {nueva}" : $"ERROR {ruta}: {error}");
        }
        if (!AssetDatabase.FindAssets("", new[] { origen }).Any()) { AssetDatabase.DeleteAsset(origen); informe.AppendLine($"{origen}: carpeta vacía, quitada."); }
        else informe.AppendLine($"{origen}: quedan cosas dentro, no se quita.");
    }

    private static void Asegurar(string carpeta)
    {
        if (AssetDatabase.IsValidFolder(carpeta)) return;
        string padre = Path.GetDirectoryName(carpeta).Replace('\\', '/');
        Asegurar(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(carpeta));
    }

    private static void RenombrarRaiz(StringBuilder informe)
    {
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool abierta = escena.IsValid() && escena.isLoaded;
        if (abierta && escena.isDirty) { informe.AppendLine("MainWorld tiene cambios sin guardar: la raíz se renombra al volver a montar los capítulos."); return; }
        if (!abierta) escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);
        int n = 0;
        foreach (var r in escena.GetRootGameObjects().Where(r => r.name == RaizVieja)) { r.name = RaizNueva; n++; }
        if (n > 0) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        informe.AppendLine(n > 0 ? $"MainWorld: «{RaizVieja}» → «{RaizNueva}»." : "MainWorld: no había raíz vieja.");
        if (!abierta) EditorSceneManager.CloseScene(escena, true);
    }
}
#endif
