using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Valle del prólogo (INC-1200): vuelca el relieve de MainWorld y la lista de lo que hay colocado, para
/// buscar sitio al valle sin pisar otros niveles. Solo lee; no cambia la escena.
public static class RelieveParaElValle
{
    [MenuItem("El Sendero/Escenario/Valle del prólogo: volcar relieve y objetos (solo lee)", priority = 60)]
    public static void Volcar()
    {
        Terrain terreno = null;
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var escena = SceneManager.GetSceneAt(s);
            if (!escena.isLoaded || escena.name != "MainWorld") continue;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                var t = raiz.GetComponentInChildren<Terrain>(true);
                if (t != null) { terreno = t; break; }
            }
        }
        if (terreno == null) { Debug.LogWarning("[Valle] MainWorld no está abierta."); return; }
        string carpeta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Guiones", "_analisis");
        Directory.CreateDirectory(carpeta);
        var datos = terreno.terrainData;
        int res = datos.heightmapResolution;
        float[,] h = datos.GetHeights(0, 0, res, res);
        Vector3 o = terreno.transform.position, m = datos.size;
        using (var w = new BinaryWriter(File.Create(Path.Combine(carpeta, "valle_relieve.bin"))))
        {
            w.Write(res); w.Write(o.x); w.Write(o.y); w.Write(o.z); w.Write(m.x); w.Write(m.y); w.Write(m.z);
            for (int k = 0; k < res; k++) for (int i = 0; i < res; i++) w.Write(o.y + h[k, i] * m.y);
        }
        var sb = new StringBuilder();
        int n = 0;
        var mw = terreno.gameObject.scene;
        foreach (var raiz in mw.GetRootGameObjects()) Listar(raiz.transform, 0, sb, ref n);
        File.WriteAllText(Path.Combine(carpeta, "valle_objetos.tsv"), sb.ToString());
        Debug.Log("[Valle] Relieve " + res + "x" + res + " y " + n + " objetos volcados en Guiones/_analisis.");
    }

    [MenuItem("El Sendero/Escenario/Valle del prólogo: volcar la escena Prologo_Valle (solo lee)", priority = 61)]
    public static void VolcarPrologo()
    {
        var escena = SceneManager.GetSceneByName("Prologo_Valle");
        if (!escena.IsValid() || !escena.isLoaded) { Debug.LogWarning("[Valle] Prologo_Valle no está abierta."); return; }
        string carpeta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Guiones", "_analisis");
        Directory.CreateDirectory(carpeta);
        var sb = new StringBuilder();
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects()) ListarTodo(raiz.transform, 0, sb, ref n);
        File.WriteAllText(Path.Combine(carpeta, "valle_prologo_objetos.tsv"), sb.ToString());
        Debug.Log("[Valle] " + n + " objetos de Prologo_Valle volcados.");
    }

    [MenuItem("El Sendero/Escenario/Valle del prólogo: volcar la escena Sendero (solo lee)", priority = 61)]
    public static void VolcarSendero()
    {
        var escena = SceneManager.GetSceneByName("Sendero");
        if (!escena.IsValid() || !escena.isLoaded) { Debug.LogWarning("[Valle] Sendero no está abierta."); return; }
        string carpeta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Guiones", "_analisis");
        var sb = new StringBuilder();
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects()) ListarTodo(raiz.transform, 0, sb, ref n);
        File.WriteAllText(Path.Combine(carpeta, "sendero_objetos.tsv"), sb.ToString());
    }

    static void ListarTodo(Transform t, int nivel, StringBuilder sb, ref int n)
    {
        if (n > 40000) return;
        var p = t.position;
        var r = t.GetComponent<Renderer>();
        string tipo = t.GetComponent<Terrain>() != null ? "TERRAIN" : r != null ? "R" : "";
        string comps = "";
        foreach (var c in t.GetComponents<Component>()) if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) comps += c.GetType().Name + ",";
        string prefab = "";
        var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
        if (src != null) prefab = AssetDatabase.GetAssetPath(src);
        sb.Append(nivel).Append('\t').Append(t.gameObject.activeInHierarchy ? 1 : 0).Append('\t').Append(GetPath(t)).Append('\t')
          .Append(p.x.ToString("F2")).Append('\t').Append(p.y.ToString("F2")).Append('\t').Append(p.z.ToString("F2")).Append('\t')
          .Append(t.eulerAngles.y.ToString("F0")).Append('\t').Append(tipo).Append('\t').Append(comps).Append('\t').Append(prefab).Append('\n');
        n++;
        if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && nivel > 0) return;
        foreach (Transform c in t) ListarTodo(c, nivel + 1, sb, ref n);
    }

    static void Listar(Transform t, int nivel, StringBuilder sb, ref int n)
    {
        if (n > 20000) return;
        var p = t.position;
        sb.Append(nivel).Append('\t').Append(t.gameObject.activeInHierarchy ? 1 : 0).Append('\t').Append(GetPath(t)).Append('\t')
          .Append(p.x.ToString("F1")).Append('\t').Append(p.y.ToString("F1")).Append('\t').Append(p.z.ToString("F1")).Append('\n');
        n++;
        if (nivel >= 3) return;
        foreach (Transform c in t) Listar(c, nivel + 1, sb, ref n);
    }

    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
