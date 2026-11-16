using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// Valle del prólogo (INC-1200): vuelca los triángulos del NavMesh que caen en el valle (para dibujarlos) y si hay
/// NavMesh cerca de unos puntos. Solo lee.
public static class DiagnosticoNavMeshDelValle
{
    [MenuItem("El Sendero/Escenario/Valle del prólogo: diagnóstico del NavMesh (solo lee)", priority = 63)]
    public static void Volcar()
    {
        var t = NavMesh.CalculateTriangulation();
        var sb = new StringBuilder();
        for (int i = 0; i + 2 < t.indices.Length; i += 3)
        {
            Vector3 a = t.vertices[t.indices[i]], b = t.vertices[t.indices[i + 1]], c = t.vertices[t.indices[i + 2]];
            Vector3 m = (a + b + c) / 3f;
            if (m.x < -300f || m.x > -30f || m.z < -470f || m.z > -230f) continue;
            sb.Append(t.areas[i / 3]).Append(';')
              .Append(a.x.ToString("F1")).Append(',').Append(a.y.ToString("F1")).Append(',').Append(a.z.ToString("F1")).Append(';')
              .Append(b.x.ToString("F1")).Append(',').Append(b.y.ToString("F1")).Append(',').Append(b.z.ToString("F1")).Append(';')
              .Append(c.x.ToString("F1")).Append(',').Append(c.y.ToString("F1")).Append(',').Append(c.z.ToString("F1")).Append('\n');
        }
        string carpeta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Guiones", "_analisis");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "valle_navmesh.txt"), sb.ToString());
        var sup = new StringBuilder();
        foreach (var su in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
        {
            var dat = su.navMeshData;
            sup.Append(su.gameObject.scene.name).Append(" / ").Append(su.name).Append(" activo=").Append(su.isActiveAndEnabled)
               .Append(" pos=").Append(su.transform.position).Append(" recoge=").Append(su.collectObjects).Append(" centro=").Append(su.center).Append(" medida=").Append(su.size)
               .Append(" datos=").Append(dat != null ? dat.name + " " + dat.sourceBounds + " pos " + dat.position : "ninguno").Append('\n');
        }
        File.WriteAllText(Path.Combine(carpeta, "valle_superficies.txt"), sup.ToString());
        Debug.Log("[Valle] NavMesh del valle volcado.");
    }

    /// El prólogo ya no lleva NavMesh propio: en el valle de MainWorld usa el de MainWorld (que se hornea con el
    /// pueblo de hoy, que está en el mismo sitio). Dos NavMesh encima no se comunican y los caminos se cortaban.
    [MenuItem("El Sendero/Escenario/Valle del prólogo: el prólogo usa el NavMesh de MainWorld", priority = 64)]
    public static void ApagarElDelPrologo()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Prologo_Valle");
        if (!escena.IsValid() || !escena.isLoaded) { Debug.LogWarning("[Valle] Prologo_Valle no está abierta."); return; }
        int n = 0;
        foreach (var r in escena.GetRootGameObjects())
            foreach (var su in r.GetComponentsInChildren<NavMeshSurface>(true))
            {
                su.RemoveData();
                su.navMeshData = null;
                su.enabled = false;
                EditorUtility.SetDirty(su);
                n++;
            }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(escena);
        Debug.Log($"[Valle] {n} superficie(s) de NavMesh del prólogo apagadas.");
    }
}
