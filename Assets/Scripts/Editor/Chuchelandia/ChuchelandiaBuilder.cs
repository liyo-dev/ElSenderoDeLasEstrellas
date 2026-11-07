using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta Chuchelandia, la prueba de Estela (GDD §5.15). Cada parte del nivel vive en su propio
/// fichero parcial de esta carpeta.
/// Este fichero retira la versión anterior de la prueba (el Duque de Regaliz y la criatura de
/// mazapán): quita su objeto raíz de la escena y archiva en «Versiones antiguas» el disparador, el
/// diálogo y los materiales que solo usaba ella. Trabaja por nombre de objeto y por ruta, sin
/// depender de sus tipos, para compilar igual antes y después de borrarlos.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/Chuchelandia.unity";

    private const string TituloRetirada = "Chuchelandia: retirar la versión del Duque";
    private const string RaizDeLaVersionVieja = "CANDYLAND_CLIMAX_CORONACION";
    private const string CarpetaDeArchivo = "Versiones antiguas/Chuchelandia - Duque de Regaliz (2026-10-31)";

    /// <summary>Assets que solo usaba la versión del Duque; se archivan y se borran si nada más los usa.</summary>
    private static readonly string[] AssetsDeLaVersionVieja =
    {
        "Assets/Scripts/World/CandylandCoronationTrigger.cs",
        "Assets/_DIALOGUES/DIALOGUE NPCS/Candyland",
        "Assets/_NPCs/Candyland",
    };

    /// <summary>Tipos de asset que pueden guardar referencias a otros (los que se revisan antes de borrar).</summary>
    private static readonly HashSet<string> ExtensionesQueReferencian = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".unity", ".prefab", ".asset", ".mat", ".playable", ".controller", ".overrideController",
    };

    [MenuItem("El Sendero/Escenario/Chuchelandia: retirar la versión del Duque de Regaliz")]
    public static void RetirarVersionDelDuque()
    {
        Scene escena = SceneManager.GetActiveScene();
        if (escena.path != RutaEscena)
        {
            EditorUtility.DisplayDialog(TituloRetirada,
                $"Abre {RutaEscena} y déjala como escena activa antes de usar este menú. No se ha cambiado nada.", "Aceptar");
            return;
        }
        if (!EditorUtility.DisplayDialog(TituloRetirada,
                $"Se quita «{RaizDeLaVersionVieja}» (Duque, criatura de mazapán, jaula y disparador) y se guarda la escena, " +
                "con cualquier otro cambio que tenga sin guardar.\n\nDespués se copian a «" + CarpetaDeArchivo + "» y se borran del proyecto " +
                "el disparador de la coronación, su diálogo y los materiales del Duque y del mazapán, salvo los que use otra cosa.",
                "Retirar", "Cancelar"))
            return;

        var resumen = new StringBuilder();
        if (!QuitarRaizVieja(escena, resumen))
        {
            EditorUtility.DisplayDialog(TituloRetirada, resumen.ToString(), "Aceptar");
            return;
        }
        ArchivarYBorrarAssetsViejos(resumen);
        EditorUtility.DisplayDialog(TituloRetirada, resumen.ToString(), "Aceptar");
    }

    /// <summary>Quita de la escena los objetos raíz de la versión vieja y la guarda. Devuelve false si no se pudo guardar.</summary>
    private static bool QuitarRaizVieja(Scene escena, StringBuilder resumen)
    {
        int quitados = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name != RaizDeLaVersionVieja) continue;
            Undo.DestroyObjectImmediate(raiz);
            quitados++;
        }

        if (quitados == 0)
        {
            resumen.AppendLine($"Escena: «{RaizDeLaVersionVieja}» ya no estaba.");
            return true;
        }

        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena))
        {
            resumen.AppendLine($"Escena: se ha quitado «{RaizDeLaVersionVieja}», pero no se ha podido guardar. Guárdala a mano y vuelve a usar el menú para retirar los assets.");
            return false;
        }
        // Reimporta la escena para que la búsqueda de dependencias la vea ya sin la versión vieja.
        AssetDatabase.ImportAsset(RutaEscena, ImportAssetOptions.ForceSynchronousImport);
        resumen.AppendLine($"Escena: quitado «{RaizDeLaVersionVieja}» y guardada.");
        return true;
    }

    /// <summary>Copia cada asset viejo (con sus .meta) a la carpeta de archivo y lo borra, si nada más del proyecto lo usa.</summary>
    private static void ArchivarYBorrarAssetsViejos(StringBuilder resumen)
    {
        var grupos = new Dictionary<string, HashSet<string>>();
        foreach (string ruta in AssetsDeLaVersionVieja)
        {
            bool esCarpeta = AssetDatabase.IsValidFolder(ruta);
            if (!esCarpeta && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ruta, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                resumen.AppendLine($"{ruta}: ya no estaba.");
                continue;
            }
            grupos[ruta] = RutasDelGrupo(ruta, esCarpeta);
        }
        if (grupos.Count == 0) return;

        Dictionary<string, List<string>> usos = BuscarQuienUsaLosGrupos(grupos);
        if (usos == null)
        {
            resumen.AppendLine("Búsqueda de usos cancelada: no se ha borrado ningún asset.");
            return;
        }

        string raizDelProyecto = Path.GetDirectoryName(Application.dataPath);
        foreach (var grupo in grupos)
        {
            string ruta = grupo.Key;
            List<string> quienes = usos[ruta];
            if (quienes.Count > 0)
            {
                resumen.AppendLine($"{ruta}: NO se borra, lo usa: {string.Join(", ", quienes)}.");
                continue;
            }

            string origen = Path.Combine(raizDelProyecto, ruta);
            string destino = Path.Combine(raizDelProyecto, CarpetaDeArchivo, ruta.Substring("Assets/".Length));
            try
            {
                if (Directory.Exists(origen)) CopiarCarpeta(origen, destino);
                else CopiarFichero(origen, destino);
                CopiarFichero(origen + ".meta", destino + ".meta");
            }
            catch (Exception e)
            {
                resumen.AppendLine($"{ruta}: NO se borra, no se ha podido copiar al archivo ({e.Message}).");
                continue;
            }

            resumen.AppendLine(AssetDatabase.DeleteAsset(ruta)
                ? $"{ruta}: archivado y borrado."
                : $"{ruta}: archivado, pero Unity no lo ha podido borrar.");
        }
    }

    /// <summary>El asset y, si es una carpeta, todo lo que contiene.</summary>
    private static HashSet<string> RutasDelGrupo(string ruta, bool esCarpeta)
    {
        var rutas = new HashSet<string>(StringComparer.Ordinal) { ruta };
        if (!esCarpeta) return rutas;
        foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { ruta }))
            rutas.Add(AssetDatabase.GUIDToAssetPath(guid));
        return rutas;
    }

    /// <summary>
    /// Para cada grupo, los assets de fuera de él que lo referencian directamente (escenas, prefabs,
    /// datos, materiales…). Devuelve null si se cancela la búsqueda.
    /// </summary>
    private static Dictionary<string, List<string>> BuscarQuienUsaLosGrupos(Dictionary<string, HashSet<string>> grupos)
    {
        var usos = new Dictionary<string, List<string>>();
        foreach (string ruta in grupos.Keys) usos[ruta] = new List<string>();

        string[] todas = AssetDatabase.GetAllAssetPaths();
        try
        {
            for (int i = 0; i < todas.Length; i++)
            {
                string candidata = todas[i];
                if (!candidata.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (!ExtensionesQueReferencian.Contains(Path.GetExtension(candidata))) continue;
                if (i % 250 == 0 && EditorUtility.DisplayCancelableProgressBar(TituloRetirada, "Buscando quién usa los assets viejos…", i / (float)todas.Length))
                    return null;

                string[] dependencias = AssetDatabase.GetDependencies(candidata, false);
                foreach (var grupo in grupos)
                {
                    if (grupo.Value.Contains(candidata)) continue;
                    foreach (string dependencia in dependencias)
                    {
                        if (!grupo.Value.Contains(dependencia)) continue;
                        usos[grupo.Key].Add(candidata);
                        break;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        return usos;
    }

    private static void CopiarCarpeta(string origen, string destino)
    {
        foreach (string fichero in Directory.GetFiles(origen, "*", SearchOption.AllDirectories))
            CopiarFichero(fichero, Path.Combine(destino, fichero.Substring(origen.Length + 1)));
    }

    private static void CopiarFichero(string origen, string destino)
    {
        if (!File.Exists(origen)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destino));
        File.Copy(origen, destino, true);
    }
}
