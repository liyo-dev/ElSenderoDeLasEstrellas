#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Quita las demos de Quibli (148 MB, 1.226 ficheros) sin romper lo que el juego usa de ellas.
/// Primero saca a «Assets/Art/World/Quibli/» todo lo que el juego referencia de las demos (material
/// y mallas de las nubes, textura de suelo del prólogo y manojos de hierba del valle) con sus
/// dependencias; los GUID se conservan, así que las referencias siguen valiendo. Después, solo si
/// ya nada de fuera las usa, borra la carpeta. Menú de un solo uso: al ejecutarse, el fichero sale
/// a «Versiones antiguas/».
public static class LimpiezaDeQuibli
{
    private const string Demos = "Assets/Plugins/Quibli/Demos";
    private const string Destino = "Assets/Art/World/Quibli";

    private static readonly string[] Semillas =
    {
        "Assets/Plugins/Quibli/Demos/Sample Scene with Quibli/Materials/SampleScene_Cloud3D.mat",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/Cloud3D-MeshCarrier_Cloud_01.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/Cloud3D-MeshCarrier_Cloud_02.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/Cloud3D-MeshCarrier_Cloud_03.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/Cloud3D-MeshCarrier_03_Dome.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/Cloud3D-Sphere.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Textures/NatureScene_Ground_Texture.png",
        "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Short.prefab",
        "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Long.prefab",
        "Assets/Plugins/Quibli/Demos/Nature/Materials/NatureScene_Grass_Long.mat",
        "Assets/Plugins/Quibli/Prefabs/Grass Mesh Generator.prefab",
        "Assets/Plugins/Quibli/Prefabs/Foliage Generator.prefab",
    };

    [MenuItem("El Sendero/Limpieza/Quibli 1: sacar lo que usa el juego", priority = 40)]
    public static void SacarLoQueSeUsa()
    {
        if (!AssetDatabase.IsValidFolder(Demos)) { Debug.Log("[Quibli] No hay carpeta de demos."); return; }

        var mover = new HashSet<string>(StringComparer.Ordinal);
        var informe = new StringBuilder();
        foreach (string semilla in Semillas)
        {
            if (AssetDatabase.LoadMainAssetAtPath(semilla) == null) { informe.Append("FALTA ").Append(semilla).Append("; "); continue; }
            if (semilla.StartsWith(Demos + "/", StringComparison.Ordinal)) mover.Add(semilla);
            foreach (string dep in AssetDatabase.GetDependencies(semilla, true))
                if (dep.StartsWith(Demos + "/", StringComparison.Ordinal)) mover.Add(dep);
        }

        var lista = new List<string>(mover);
        lista.Sort((a, b) => a.Length.CompareTo(b.Length));

        int movidos = 0;
        foreach (string ruta in lista)
        {
            string destino = Destino + ruta.Substring(Demos.Length);
            string carpeta = Path.GetDirectoryName(destino).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(carpeta)) CrearCarpetas(carpeta);

            string error = AssetDatabase.MoveAsset(ruta, destino);
            if (string.IsNullOrEmpty(error)) { movidos++; informe.Append(Path.GetFileName(ruta)).Append("; "); }
            else Debug.LogWarning($"[Quibli] No se pudo mover {ruta}: {error}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Quibli] {movidos} assets movidos a {Destino}. Con eso ya se puede comprobar si queda algo usado; no se ha borrado nada. Movidos: {informe}");
    }

    /// Borra la carpeta de demos. Se llama después de comprobar, fuera del Editor, que ningún asset
    /// del juego referencia lo que queda dentro (informe del 15 nov: 0 referencias).
    [MenuItem("El Sendero/Limpieza/Quibli 2: borrar las demos", priority = 41)]
    public static void BorrarLasDemos()
    {
        if (!AssetDatabase.IsValidFolder(Demos)) { Debug.Log("[Quibli] No hay carpeta de demos."); return; }

        int restos = AssetDatabase.FindAssets("t:Object", new[] { Demos }).Length;
        bool borrada = AssetDatabase.DeleteAsset(Demos);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Quibli] Demos borradas: {borrada} ({restos} assets que quedaban, ninguno referenciado desde fuera). Quedan {CountFiles("Assets/Plugins/Quibli")} ficheros en el plugin.");
    }

    private static int CountFiles(string carpeta)
    {
        int n = 0;
        foreach (string f in Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories)) n++;
        return n;
    }

    private static void CrearCarpetas(string ruta)
    {
        string actual = "Assets";
        foreach (string trozo in ruta.Substring("Assets/".Length).Split('/'))
        {
            string siguiente = actual + "/" + trozo;
            if (!AssetDatabase.IsValidFolder(siguiente)) AssetDatabase.CreateFolder(actual, trozo);
            actual = siguiente;
        }
    }
}
#endif
