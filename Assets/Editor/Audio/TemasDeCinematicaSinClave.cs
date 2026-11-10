using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Da clave de secuencia (<see cref="AudioGraphProfile.sequences"/>) a cada tema de
/// «Audio/Music/Cinematicas» que aún no la tiene, para poder usarlo en un guion con «musica CLAVE».
/// La clave es el nombre del archivo en mayúsculas, sin acentos y con «_» (Tobias.mp3 → TOBIAS).
/// </summary>
public static class TemasDeCinematicaSinClave
{
    private const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string Carpeta = "Assets/Audio/Music/Cinematicas";

    [MenuItem("El Sendero/Audio/Dar clave a los temas de cinemática que no la tienen")]
    public static void Registrar()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null) { OrdenesDelEditor.Avisar("Temas de cinemática", "Falta " + RutaPerfil); return; }
        var informe = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Carpeta }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null || perfil.sequences.Any(r => r != null && r.music == clip)) continue;
            string clave = Clave(Path.GetFileNameWithoutExtension(ruta));
            if (perfil.GetSequenceRule(clave) != null) { informe.AppendLine($"{clave}: ya existe con otro tema, sin tocar."); continue; }
            Undo.RecordObject(perfil, "Temas de cinemática");
            perfil.sequences.Add(new AudioGraphProfile.SequenceRule { sequenceId = clave, music = clip, fadeIn = 1f, fadeOut = 2f });
            informe.AppendLine($"{clave} → {Path.GetFileName(ruta)}");
        }
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Temas de cinemática", informe.Length > 0 ? informe.ToString() : "Todos los temas ya tenían clave.");
    }

    private static string Clave(string nombre)
    {
        var sb = new StringBuilder();
        foreach (char c in nombre.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
    }
}
