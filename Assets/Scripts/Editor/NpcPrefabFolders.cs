#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ordena los prefabs de NPC según el sistema narrativo al que pertenecen, y saca a la luz los
/// duplicados.
///
/// CONTEXTO (16 sep 2026). La auditoría del proyecto dejó ver dos cosas:
///   1. `Assets/Prefabs/Cap1/` funciona de facto como "la carpeta de los migrados al grafo
///      narrativo" — es donde están los ÚNICOS dos prefabs de todo el proyecto con el componente
///      NarrativeActor (Eldran y Oliver). Pero se llama "Cap1", que ya no describe lo que hay
///      dentro, y además comparte carpeta con props de capítulo (Carta, Goods_Interactable).
///   2. Eldran y Oliver están DUPLICADOS: existen también en `Assets/_NPCs/`, y no son variantes
///      uno del otro sino prefabs independientes. Eso obliga a mantener a mano los dos por
///      separado — de hecho las herramientas de caras tocaron las dos copias de cada uno.
///
/// Mover y renombrar prefabs en Unity es seguro: el guid viaja en el .meta, así que las escenas y
/// los assets que los referencian no se enteran. Lo que NO es seguro es adivinar cuál de cada
/// pareja está viva, y por eso esta herramienta no mueve nada a "Versiones antiguas" sin
/// comprobarlo antes: recorre TODAS las escenas del proyecto y mira sus dependencias reales.
/// Si una copia está referenciada por alguna escena, se queda donde está y lo dice.
/// </summary>
public static class NpcPrefabFolders
{
    private const string MigratedFolder = "Assets/_NPCs/_GrafoNarrativo";
    private const string OldFolder = "Assets/_NPCs/Versiones antiguas";

    /// Los prefabs que hoy están en Cap1 y son personajes migrados (no props).
    private static readonly string[] MigratedPrefabs =
    {
        // Ya movidos aqui por Reorganize() el 16 sep 2026. Antes estaban en Prefabs/Cap1.
        // (Las rutas viejas se quedaron escritas un rato despues de mover los archivos, y el
        // informe salia diciendo "no lo referencia nadie" cuando lo que pasaba es que la ruta ya
        // no existia: File.Exists devolvia false y se saltaba la comprobacion entera.)
        MigratedFolder + "/Eldran.prefab",
        MigratedFolder + "/Oliver.prefab",
    };

    /// Las copias antiguas que quedarían huérfanas, si de verdad no las usa nadie.
    private static readonly string[] SuspectedOldCopies =
    {
        "Assets/_NPCs/Eldran.prefab",
        "Assets/_NPCs/Oliver.prefab",
    };

    // ── Auditoría (no toca nada) ─────────────────────────────────────────────

    [MenuItem("El Sendero/NPCs/Setup/Auditar prefabs de NPC duplicados")]
    public static void Audit()
    {
        Debug.Log(BuildAudit());
    }

    private static string BuildAudit()
    {
        var sb = new StringBuilder("=== Auditoría de prefabs de NPC ===\n\n");
        var scenes = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .ToArray();

        sb.AppendLine($"Escenas revisadas: {scenes.Length}\n");

        foreach (string path in MigratedPrefabs.Concat(SuspectedOldCopies))
        {
            var users = ScenesUsing(path, scenes);
            sb.AppendLine($"{path}");
            sb.AppendLine(users.Count == 0
                ? "   → NINGUNA escena lo referencia."
                : "   → usado por: " + string.Join(", ", users.Select(System.IO.Path.GetFileNameWithoutExtension)));
        }

        sb.AppendLine();
        sb.AppendLine("Recordatorio: que una escena no lo referencie no significa que sea basura —");
        sb.AppendLine("puede instanciarse desde código o desde un asset de datos. Por eso la");
        sb.AppendLine("reorganización mueve las copias antiguas a 'Versiones antiguas' en vez de");
        sb.AppendLine("borrarlas: si algo se rompe, están ahí al lado.");
        return sb.ToString();
    }

    /// Escenas que dependen del prefab indicado.
    ///
    /// OJO, RECURSIVO: esto sigue la cadena entera, así que una escena sale listada aunque no
    /// contenga el prefab — basta con que algo suyo lo acabe referenciando. Para saber si la
    /// referencia es de verdad o heredada, ver DirectReferrers() y el informe de cadena.
    private static List<string> ScenesUsing(string prefabPath, string[] scenes)
    {
        var users = new List<string>();
        if (!System.IO.File.Exists(prefabPath)) return users;

        foreach (string scene in scenes)
            if (AssetDatabase.GetDependencies(scene, true).Contains(prefabPath))
                users.Add(scene);

        return users;
    }

    /// Escenas que contienen el prefab DIRECTAMENTE (sin cadena de por medio).
    private static List<string> ScenesUsingDirectly(string prefabPath, string[] scenes)
    {
        var users = new List<string>();
        if (!System.IO.File.Exists(prefabPath)) return users;

        foreach (string scene in scenes)
            if (AssetDatabase.GetDependencies(scene, false).Contains(prefabPath))
                users.Add(scene);

        return users;
    }

    /// Assets que referencian DIRECTAMENTE al prefab dado: el eslabón real de la cadena.
    ///
    /// Es la pregunta que el informe recursivo no contesta. Un "usado por Start" puede significar
    /// que Start lo tiene puesto, o que Start referencia un manager que referencia un preset que
    /// referencia el prefab — y la diferencia lo cambia todo a la hora de decidir si una copia
    /// está viva o es un resto.
    private static List<string> DirectReferrers(string prefabPath)
    {
        var referrers = new List<string>();
        if (!System.IO.File.Exists(prefabPath)) return referrers;

        // Solo los tipos que pueden guardar una referencia a un prefab. Se excluyen carpetas de
        // terceros (Plugins) para que esto tarde segundos y no minutos.
        string[] guids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject t:Scene");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path == prefabPath) continue;
            if (path.StartsWith("Assets/Plugins/") || path.StartsWith("Packages/")) continue;

            if (AssetDatabase.GetDependencies(path, false).Contains(prefabPath))
                referrers.Add(path);
        }

        return referrers;
    }

    [MenuItem("El Sendero/NPCs/Setup/¿Quién referencia los prefabs de NPC? (cadena real)")]
    public static void TraceReferences()
    {
        var sb = new StringBuilder("=== Quién referencia de verdad a cada prefab ===\n\n");
        var scenes = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .ToArray();

        foreach (string path in MigratedPrefabs.Concat(SuspectedOldCopies))
        {
            sb.AppendLine(path);

            var directScenes = ScenesUsingDirectly(path, scenes);
            sb.AppendLine(directScenes.Count == 0
                ? "   Escenas que lo contienen DIRECTAMENTE: ninguna"
                : "   Escenas que lo contienen DIRECTAMENTE: " +
                  string.Join(", ", directScenes.Select(System.IO.Path.GetFileNameWithoutExtension)));

            var referrers = DirectReferrers(path);
            var nonScenes = referrers.Where(r => !r.EndsWith(".unity")).ToList();
            sb.AppendLine(nonScenes.Count == 0
                ? "   Otros assets que lo referencian: ninguno"
                : "   Otros assets que lo referencian:");
            foreach (var r in nonScenes) sb.AppendLine("      • " + r);

            sb.AppendLine();
        }

        sb.AppendLine("Cómo leerlo: si un prefab no lo contiene DIRECTAMENTE ninguna escena y solo");
        sb.AppendLine("aparece a través de otros assets, entonces el 'usado por Start' del informe");
        sb.AppendLine("anterior era heredado — Start referencia algo que, a su vez, lo referencia.");
        Debug.Log(sb.ToString());
    }

    // ── Reorganización ───────────────────────────────────────────────────────

    [MenuItem("El Sendero/NPCs/Setup/Reorganizar prefabs del sistema nuevo")]
    public static void Reorganize()
    {
        var sb = new StringBuilder("=== Reorganización de prefabs de NPC ===\n\n");
        var scenes = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .ToArray();

        EnsureFolder(MigratedFolder);

        // 1) Los migrados, a su carpeta con nombre honesto.
        foreach (string path in MigratedPrefabs)
        {
            if (!System.IO.File.Exists(path))
            {
                sb.AppendLine($"  • {path}: ya no está ahí (¿movido antes?). Nada que hacer.");
                continue;
            }

            string dest = $"{MigratedFolder}/{System.IO.Path.GetFileName(path)}";
            string error = AssetDatabase.MoveAsset(path, dest);
            sb.AppendLine(string.IsNullOrEmpty(error)
                ? $"  • MOVIDO: {path}\n        → {dest}"
                : $"  • ERROR moviendo {path}: {error}");
        }

        sb.AppendLine();

        // 2) Las copias antiguas, solo si NADIE las usa.
        bool anyOldMoved = false;
        foreach (string path in SuspectedOldCopies)
        {
            if (!System.IO.File.Exists(path))
            {
                sb.AppendLine($"  • {path}: no existe. Nada que hacer.");
                continue;
            }

            var users = ScenesUsing(path, scenes);
            if (users.Count > 0)
            {
                sb.AppendLine($"  • NO SE TOCA: {path}");
                sb.AppendLine($"        lo usa: {string.Join(", ", users.Select(System.IO.Path.GetFileNameWithoutExtension))}");
                sb.AppendLine("        (no es una copia muerta — hay que mirar qué escena lo usa y por qué)");
                continue;
            }

            if (!anyOldMoved) { EnsureFolder(OldFolder); anyOldMoved = true; }

            string dest = $"{OldFolder}/{System.IO.Path.GetFileName(path)}";
            string error = AssetDatabase.MoveAsset(path, dest);
            sb.AppendLine(string.IsNullOrEmpty(error)
                ? $"  • A VERSIONES ANTIGUAS (ninguna escena lo usa): {path}\n        → {dest}"
                : $"  • ERROR moviendo {path}: {error}");
        }

        AssetDatabase.Refresh();

        sb.AppendLine();
        sb.AppendLine("Los props que estaban en Cap1 (Carta, Goods_Interactable) se quedan donde");
        sb.AppendLine("están: son objetos de capítulo, no personajes, y esa carpeta sigue teniendo");
        sb.AppendLine("sentido para ellos.");
        Debug.LogWarning(sb.ToString());
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
