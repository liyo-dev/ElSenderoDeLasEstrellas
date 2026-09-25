using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Limpieza del proyecto, tanda 2 (INC-444): saca de Assets las carpetas viejas que ya no usa nada.
///
/// Candidatas: el MainWorld antiguo (MainWorld_old + su escena, ~714 MB) y las cuatro maquetas de
/// maquetas del mapa del 17 sep (~228 MB). Hacen más lento cada import y cada búsqueda.
///
/// No se puede saber leyendo archivos si el MainWorld nuevo usa algo de ellas: MainWorld.unity está
/// guardada en binario. Por eso esto se hace DENTRO de Unity, preguntando al AssetDatabase:
///   1. Recorre todos los assets del proyecto que NO están en las carpetas candidatas y mira sus
///      dependencias directas. Si alguno usa algo de dentro, lo dice y NO mueve esa carpeta.
///   2. Las carpetas que nadie usa se mueven (con sus .meta, así que conservan sus GUID) a
///      «Versiones antiguas/» en la raíz del proyecto, fuera de Assets. No se borra nada: para
///      recuperarlas basta con devolverlas a su sitio.
/// </summary>
public static class SacarCarpetasViejasDeAssets
{
    private static readonly string[] Candidatas =
    {
        "Assets/Scenes/Worlds/MainWorld_old",
        "Assets/Scenes/Worlds/MainWorld_old.unity",
        "Assets/Scenes/Worlds/EldoriaCodex_20260917_074405",
        "Assets/Scenes/Worlds/EldoriaCodex_20260917_131201",
        "Assets/Scenes/Worlds/EldoriaCodex_20260917_131554",
        "Assets/Scenes/Worlds/EldoriaCodex_20260917_140610",
    };

    private const string Destino = "Versiones antiguas/Assets_Scenes_Worlds (2026-09-25)";

    [MenuItem("El Sendero/Archivo/Auditoría/Sacar de Assets MainWorld_old y las maquetas viejas del mapa (si nada las usa)")]
    public static void Ejecutar()
    {
        var existentes = Candidatas.Where(c => AssetDatabase.IsValidFolder(c) || File.Exists(c)).ToList();
        if (existentes.Count == 0)
        {
            EditorUtility.DisplayDialog("Limpieza", "Ya no queda ninguna de las carpetas candidatas en Assets.", "Vale");
            return;
        }

        // Escenas abiertas de dentro: hay que cerrarlas antes de mover nada.
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var sc = EditorSceneManager.GetSceneAt(i);
            if (existentes.Any(c => Dentro(sc.path, c)))
            {
                EditorUtility.DisplayDialog("Limpieza", $"Tienes abierta '{sc.path}'. Ciérrala y vuelve a lanzar esto.", "Vale");
                return;
            }
        }

        // Escenas de Build Settings que estén dentro.
        var enBuild = EditorBuildSettings.scenes.Where(s => existentes.Any(c => Dentro(s.path, c))).Select(s => s.path).ToList();

        // 1. ¿Quién usa algo de dentro?
        var usos = existentes.ToDictionary(c => c, _ => new List<string>());
        var todos = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !existentes.Any(c => Dentro(p, c)) && !AssetDatabase.IsValidFolder(p))
            .ToArray();
        try
        {
            for (int i = 0; i < todos.Length; i++)
            {
                if (i % 200 == 0 && EditorUtility.DisplayCancelableProgressBar("Limpieza",
                        $"Mirando quién usa las carpetas viejas… {i}/{todos.Length}", (float)i / todos.Length))
                {
                    Debug.Log("[Limpieza] Cancelado: no se ha movido nada.");
                    return;
                }
                foreach (var dep in AssetDatabase.GetDependencies(todos[i], false))
                    foreach (var c in existentes)
                        if (Dentro(dep, c) && usos[c].Count < 30)
                            usos[c].Add($"{todos[i]}  →  {dep}");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        var libres = existentes.Where(c => usos[c].Count == 0 && !enBuild.Any(b => Dentro(b, c))).ToList();
        var informe = new StringBuilder("[Limpieza] Carpetas viejas en Assets\n");
        foreach (var c in existentes)
        {
            if (enBuild.Any(b => Dentro(b, c))) informe.AppendLine($"✗ {c}: tiene una escena en Build Settings. Se queda.");
            else if (usos[c].Count > 0)
            {
                informe.AppendLine($"✗ {c}: la usan {usos[c].Count}{(usos[c].Count >= 30 ? "+" : "")} asset(s). Se queda. Por ejemplo:");
                foreach (var u in usos[c].Take(10)) informe.AppendLine("     " + u);
            }
            else informe.AppendLine($"✓ {c}: nadie la usa.");
        }
        Debug.Log(informe.ToString());

        if (libres.Count == 0)
        {
            EditorUtility.DisplayDialog("Limpieza", "Todas las candidatas se usan desde algún sitio: no se mueve nada. El detalle está en la consola.", "Vale");
            return;
        }

        if (!EditorUtility.DisplayDialog("Limpieza",
                $"Nadie usa estas {libres.Count} carpeta(s):\n\n{string.Join("\n", libres)}\n\n" +
                $"¿Las saco de Assets a «{Destino}»? No se borra nada; conservan sus .meta.",
                "Sacarlas", "Cancelar"))
            return;

        // 2. Mover fuera de Assets (con .meta).
        string raiz = Path.GetDirectoryName(Application.dataPath);
        string destino = Path.Combine(raiz, Destino);
        Directory.CreateDirectory(destino);
        var movidas = new StringBuilder("[Limpieza] Movido fuera de Assets:\n");
        foreach (var c in libres)
        {
            string origen = Path.Combine(raiz, c);
            string nombre = Path.GetFileName(c);
            string a = Path.Combine(destino, nombre);
            if (Directory.Exists(a) || File.Exists(a)) { movidas.AppendLine($"  (ya existía {a}, no se toca {c})"); continue; }

            if (Directory.Exists(origen)) Directory.Move(origen, a); else File.Move(origen, a);
            if (File.Exists(origen + ".meta")) File.Move(origen + ".meta", a + ".meta");
            movidas.AppendLine($"  {c}  →  {Destino}/{nombre}");
        }
        AssetDatabase.Refresh();
        Debug.Log(movidas.ToString());
    }

    private static bool Dentro(string ruta, string candidata)
        => !string.IsNullOrEmpty(ruta) && (ruta == candidata || ruta.StartsWith(candidata + "/"));
}
