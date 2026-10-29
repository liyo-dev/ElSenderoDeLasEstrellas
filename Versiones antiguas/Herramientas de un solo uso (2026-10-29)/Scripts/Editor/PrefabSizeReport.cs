using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Vuelca a un fichero el tamaño real de los prefabs de decorado.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El Fantasy Kingdom Pack está modelado a una escala que no es la de los personajes de este juego
/// (Will mide 1,15 m), y por eso todo lo que se coloca de ese pack lleva una escala a ojo: 0,43
/// aquí, 0,58 allá, 1,25 en el árbol de al lado. Colocar así es adivinar, y el resultado se nota
/// enseguida — casas de juguete, vallas gigantes, colinas que parecen setas.
///
/// Con esto se mide una vez: cuánto ocupa cada prefab de verdad y a qué altura tiene el pivot.
/// A partir de ahí la escala de cada cosa se calcula ("esta casa debe medir 6 m de alto" → escala
/// 6 / altura_real) en vez de estimarse, y el decorado se puede generar desde fuera del Editor con
/// las proporciones correctas.
public static class PrefabSizeReport
{
    private static readonly string[] Carpetas =
    {
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Particle",
        "Assets/Low Poly Modular Terrain Pack/Terrain_Assets/Prefabs",
        "Assets/Plugins/Low Poly Modular Terrain Pack/Terrain_Assets/Prefabs",
    };

    [MenuItem("El Sendero/Mundo/Medir tamaño de los prefabs de decorado (a fichero)")]
    public static void Generar()
    {
        var existentes = new List<string>();
        foreach (var carpeta in Carpetas)
            if (AssetDatabase.IsValidFolder(carpeta)) existentes.Add(carpeta);

        if (existentes.Count == 0)
        {
            Debug.LogError("[PrefabSizeReport] Ninguna de las carpetas de decorado existe en este proyecto.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", existentes.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine("# nombre\tancho_x\talto_y\tfondo_z\tcentro_y\tpivot_a_base\truta");

        int hechos = 0;
        try
        {
            foreach (string guid in guids)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (++hechos % 50 == 0)
                    EditorUtility.DisplayProgressBar("Midiendo prefabs", ruta, hechos / (float)guids.Length);

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (go == null) continue;

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;

                bool primero = true;
                Bounds bounds = default;
                foreach (var r in renderers)
                {
                    // Los sistemas de partículas dan bounds del emisor, que no dicen nada del
                    // tamaño del objeto; se miden solo mallas.
                    if (r is ParticleSystemRenderer) continue;
                    if (primero) { bounds = r.bounds; primero = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (primero) continue;

                var c = CultureInfo.InvariantCulture;
                sb.AppendLine(string.Join("\t",
                    Path.GetFileName(ruta),
                    bounds.size.x.ToString("F3", c),
                    bounds.size.y.ToString("F3", c),
                    bounds.size.z.ToString("F3", c),
                    bounds.center.y.ToString("F3", c),
                    (bounds.center.y - bounds.extents.y).ToString("F3", c),
                    ruta));
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        string destino = Path.Combine(Directory.GetCurrentDirectory(), "Claude outputs", "prologo_valle");
        Directory.CreateDirectory(destino);
        string fichero = Path.Combine(destino, "medidas_prefabs.tsv");
        File.WriteAllText(fichero, sb.ToString(), Encoding.UTF8);

        Debug.Log($"[PrefabSizeReport] Medidos {hechos} prefabs → {fichero}");
        EditorUtility.RevealInFinder(fichero);
    }
}
