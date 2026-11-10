using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inventario de MainWorld para montar los capítulos de la historia: lo que ya hay en la escena y
/// sirve a los capítulos 3 y 4 (compañeros, puntos del roster, arranques del grafo, reproductores
/// de cinemáticas, arenas, zonas de ambiente y objetos con nombres de los sitios de la historia).
/// Escribe «Guiones/Historia/inventario_MainWorld.md». Abre MainWorld en aditivo si no está abierta y
/// la cierra al acabar, sin guardar nada.
/// </summary>
public static class InventarioDeMainWorld
{
    private const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";

    private static readonly string[] Palabras =
    {
        "golem", "gólem", "woods", "bosque", "forest", "sea", "posada", "taberna", "tavern", "molino",
        "establo", "guard", "puente", "rio", "río", "arroyo", "estela", "liam", "campana", "claro", "carro",
        "contraband", "ladron", "vado", "pesquero", "puerto", "fisher", "village", "aldea", "barro", "mud",
        "ambient", "arena", "savepoint", "anchor", "spawn",
    };

    [MenuItem("El Sendero/Historia/Inventario de MainWorld")]
    public static void Hacer()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Inventario", "Sal de Play antes."); return; }
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);

        var sb = new StringBuilder();
        sb.AppendLine("# Inventario de MainWorld");
        sb.AppendLine();
        sb.AppendLine("Escenas abiertas: " + string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name)));
        var todos = new List<Transform>();
        foreach (var raiz in escena.GetRootGameObjects()) todos.AddRange(raiz.GetComponentsInChildren<Transform>(true));

        sb.AppendLine().AppendLine("## Arranques del grafo");
        foreach (var s in Componentes<NarrativeGraphStarter>(todos))
        {
            var so = new SerializedObject(s);
            var l = so.FindProperty("graphLabels");
            var etiquetas = new List<string>();
            for (int i = 0; i < l.arraySize; i++) etiquetas.Add(l.GetArrayElementAtIndex(i).stringValue);
            sb.AppendLine($"- {Ruta(s.transform)}: {string.Join(", ", etiquetas)}");
        }

        sb.AppendLine().AppendLine("## Puntos del roster (NpcSpawnPoint)");
        foreach (var p in Componentes<NpcSpawnPoint>(todos))
            sb.AppendLine($"- {p.spawnId} · {Pos(p.transform)} · {Ruta(p.transform)}");

        sb.AppendLine().AppendLine("## Reproductores de cinemáticas (SequencePlayer)");
        foreach (var p in Componentes<SequencePlayer>(todos))
            sb.AppendLine($"- {p.name} · {(p.Definition != null ? p.Definition.name : "(sin definición)")} · {Ruta(p.transform)}");

        sb.AppendLine().AppendLine("## Compañeros y NPCs de la escena (NPCBehaviourManagerV2)");
        foreach (var n in Componentes<Game.NPC.NPCBehaviourManagerV2>(todos))
            sb.AppendLine($"- {n.name} · activo {n.gameObject.activeInHierarchy} · {Pos(n.transform)} · {Ruta(n.transform)}");

        sb.AppendLine().AppendLine("## Arenas de jefe (BossArenaController)");
        foreach (var b in Componentes<BossArenaController>(todos))
            sb.AppendLine($"- {b.name} · {Pos(b.transform)} · {Ruta(b.transform)}");

        sb.AppendLine().AppendLine("## Objetos con nombre de sitio (primeros 3 niveles y los que coinciden)");
        var vistos = new HashSet<string>();
        foreach (var t in todos)
        {
            string n = t.name.ToLowerInvariant();
            int nivel = Nivel(t);
            bool coincide = Palabras.Any(p => n.Contains(p));
            if (!(coincide && nivel <= 6) && nivel > 2) continue;
            string clave = Ruta(t);
            if (!vistos.Add(clave)) continue;
            sb.AppendLine($"- {clave} · {Pos(t)} · hijos {t.childCount}{(t.gameObject.activeInHierarchy ? "" : " · inactivo")}");
        }

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "Historia");
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, "inventario_MainWorld.md");
        File.WriteAllText(ruta, sb.ToString());
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
        OrdenesDelEditor.Avisar("Inventario", "Escrito en " + ruta);
    }

    private static IEnumerable<T> Componentes<T>(List<Transform> todos) where T : Component
    {
        foreach (var t in todos)
            foreach (var c in t.GetComponents<T>())
                if (c != null) yield return c;
    }

    private static int Nivel(Transform t)
    {
        int n = 0;
        for (var p = t.parent; p != null; p = p.parent) n++;
        return n;
    }

    private static string Pos(Transform t) => $"({t.position.x:0.#}, {t.position.y:0.#}, {t.position.z:0.#})";

    private static string Ruta(Transform t)
    {
        var partes = new List<string>();
        for (var p = t; p != null; p = p.parent) partes.Insert(0, p.name);
        return string.Join("/", partes);
    }
}
