using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Informe de solo lectura: dónde está cada cosa de una escena (marcas SpawnAnchor, puntos del roster,
/// zonas ZONA_*, raíces y objetos cuyo nombre contenga un filtro). Sirve para diseñar recorridos y
/// colocar contenido sin abrir la escena a mano. No cambia nada. Escribe en Guiones/_informes/.
/// </summary>
public static class InformeDeMarcasDelMundo
{
    private const string RutaPorDefecto = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string FiltroPorDefecto = "salida|exit|puerta|gate|camino|road|path|puente|bridge|pesquer|fisher|savepoint|village|pueblo|zona";

    [MenuItem("El Sendero/Informes/Marcas del mundo (MainWorld)")]
    public static void Menu() => OrdenesDelEditor.Avisar("Marcas del mundo", Generar(RutaPorDefecto, FiltroPorDefecto));

    /// <summary>Escena por ruta y filtro opcional de nombres (separados por «|», sin distinguir mayúsculas).</summary>
    public static string Generar(string rutaEscena, string filtro)
    {
        var escena = SceneManager.GetSceneByPath(rutaEscena);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(rutaEscena, OpenSceneMode.Additive);
        var sb = new StringBuilder();
        sb.AppendLine($"# Marcas de {escena.name}\n");
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        sb.AppendLine("## Marcas (SpawnAnchor)");
        foreach (var a in todos.Select(t => t.GetComponent<SpawnAnchor>()).Where(a => a != null).OrderBy(a => a.anchorId))
            sb.AppendLine($"- {a.anchorId}: {P(a.transform.position)} · {Ruta(a.transform)}");

        sb.AppendLine("\n## Puntos del roster (NpcSpawnPoint)");
        foreach (var s in todos.Select(t => t.GetComponent<NpcSpawnPoint>()).Where(s => s != null).OrderBy(s => s.spawnId))
            sb.AppendLine($"- {s.spawnId}: {P(s.transform.position)}");

        sb.AppendLine("\n## Zonas y raíces");
        foreach (var t in todos.Where(t => t.name.StartsWith("ZONA_") || t.parent == null))
            sb.AppendLine($"- {t.name}: {P(t.position)}");

        if (!string.IsNullOrEmpty(filtro))
        {
            var partes = filtro.ToLowerInvariant().Split('|');
            sb.AppendLine($"\n## Filtro «{filtro}»");
            foreach (var t in todos.Where(t => partes.Any(p => t.name.ToLowerInvariant().Contains(p))).Take(400))
                sb.AppendLine($"- {t.name}: {P(t.position)} · {Ruta(t)}");
        }

        foreach (var ter in Terrain.activeTerrains.Where(x => x.gameObject.scene == escena))
            sb.AppendLine($"\nTerreno {ter.name}: origen {P(ter.transform.position)}, tamaño {ter.terrainData.size}");

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_informes");
        Directory.CreateDirectory(carpeta);
        string fichero = Path.Combine(carpeta, $"marcas_{escena.name}.md");
        File.WriteAllText(fichero, sb.ToString());
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
        return "informe en " + fichero;
    }

    private static string P(Vector3 v) => $"({v.x:0.#}, {v.y:0.#}, {v.z:0.#})";

    private static string Ruta(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
