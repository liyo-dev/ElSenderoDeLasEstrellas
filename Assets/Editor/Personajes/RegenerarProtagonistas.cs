using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Un solo menú para dejar a los tres protagonistas al día después de cambiar sus modelos en Blender:
/// aspecto de Estela, Liam y Will (piezas, colores, aspecto de partida y presets del jugador) y sus caras
/// (piezas, perfiles de emociones y registro). Es lo mismo que pasar los cuatro menús en orden, con un
/// único informe al final. Después: partida nueva (una partida empezada conserva la ropa guardada).
/// </summary>
public static class RegenerarProtagonistas
{
    [MenuItem("El Sendero/Personajes/REGENERAR TODOS LOS PROTAGONISTAS", priority = 0)]
    public static void Todo()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Protagonistas", "Hazlo fuera de Play.", "Vale"); return; }
        AssetDatabase.Refresh();
        var informe = new StringBuilder();
        var pasos = new (string titulo, System.Action<StringBuilder> paso)[]
        {
            ("Estela", sb => AspectoDeProtagonista.Aplicar(AspectoDeProtagonista.FichaEstela, sb)),
            ("Liam", sb => AspectoDeProtagonista.Aplicar(AspectoDeProtagonista.FichaLiam, sb)),
            ("Will", sb => AspectoDeProtagonista.Aplicar(AspectoDeProtagonista.FichaWill, sb)),
            ("Caras", sb => CarasDeLosProtagonistas.Aplicar(sb)),
        };
        try
        {
            for (int i = 0; i < pasos.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Regenerando protagonistas", pasos[i].titulo, (float)i / pasos.Length);
                informe.AppendLine($"── {pasos[i].titulo} ──");
                try { pasos[i].paso(informe); }
                catch (System.Exception e) { informe.AppendLine($"ERROR: {e.Message}"); Debug.LogException(e); }
                AssetDatabase.SaveAssets();
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log("[RegenerarProtagonistas] " + informe.ToString().Replace('\n', ' '));
        EditorUtility.DisplayDialog("Protagonistas regenerados", informe + "\nAhora: partida nueva.", "Vale");
    }
}
