using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Quita la sombra proyectada a lo pequeño de las escenas abiertas o de todas las jugables: flores, hierbas, bichos y objetos de
/// menos de un metro. Con la luz de dos tonos, esas sombras diminutas salían como un moteado que bailaba,
/// y eran la mayoría de los objetos que pintan sombra. Siguen recibiendo sombra (la del árbol o la casa).
/// Los personajes (SkinnedMeshRenderer) no se tocan. Informe en Guiones/_ordenes/sombras.md.
public static class SombrasDeLoPequeno
{
    const float TamanoMaximo = 1.0f;
    static readonly Regex Flora = new Regex("flower|flor|plant|grass|hierba|petal|bee|butterfly|mariposa|dandelion|mushroom|seta|clover|weed", RegexOptions.IgnoreCase);

    [MenuItem("El Sendero/Aspecto de dibujo/Render: sin sombra en lo pequeño (escenas abiertas)")]
    static void EnLasAbiertas() => Quitar(false);

    [MenuItem("El Sendero/Aspecto de dibujo/Render: sin sombra en lo pequeño (todas las escenas jugables)")]
    static void EnLasJugables() => Quitar(true);

    static void Quitar(bool todasLasJugables)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Sombras] Sal de Play antes."); return; }
        var sb = new StringBuilder("# Sombra quitada a lo pequeño\n\n");
        EscenasJugables.ParaCada(todasLasJugables, QuitarEn, sb);
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "sombras.md"), sb.ToString());
        Debug.Log("[Sombras] " + sb);
    }

    static bool QuitarEn(Scene escena, StringBuilder sb)
    {
        int quitadas = 0, total = 0;
        foreach (var r in escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            total++;
            bool pequeno = r.bounds.size.magnitude < TamanoMaximo;
            if (!pequeno && !Flora.IsMatch(r.name)) continue;
            Undo.RecordObject(r, "Sin sombra en lo pequeño");
            r.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            quitadas++;
        }
        sb.Append($"- {escena.name}: {quitadas} de {total} dejan de proyectar sombra\n");
        return quitadas > 0;
    }
}
