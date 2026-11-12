using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Devuelve a la superficie del agua los cinco barriles a la deriva que la primera pasada de
/// «Apoyar en el suelo los props que flotan» bajó al fondo (flotan a propósito). Ver INC-1118.
public static class DevolverBarrilesALaDeriva
{
    private static readonly float[] Subidas = { 0f, -3.79f, 0.14f, 0f, -0.16f, 3.81f };

    [MenuItem("El Sendero/Archivo/Barriles a la deriva: alturas (INC-1118)")]
    private static void Alturas()
    {
        var escena = SceneManager.GetSceneByName("MainWorld");
        var vistos = new HashSet<GameObject>();
        var sb = new System.Text.StringBuilder();
        foreach (var raiz in escena.GetRootGameObjects())
        foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(false))
        {
            var prop = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (prop == null || !vistos.Add(prop)) continue;
            if (prop.name == "Barril a la deriva") sb.Append(prop.transform.position.ToString("F2")).Append(' ');
        }
        OrdenesDelEditor.Avisar("Barriles", sb.ToString());
    }

    [MenuItem("El Sendero/Archivo/Devolver los barriles a la deriva (INC-1118)")]
    private static void Devolver()
    {
        var escena = SceneManager.GetSceneByName("MainWorld");
        if (!escena.isLoaded) { OrdenesDelEditor.Avisar("Barriles", "MainWorld no está abierta."); return; }
        var vistos = new HashSet<GameObject>();
        var barriles = new List<GameObject>();
        foreach (var raiz in escena.GetRootGameObjects())
        foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(false))
        {
            var prop = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
            if (prop == null || !vistos.Add(prop)) continue;
            if (prop.name == "Barril a la deriva") barriles.Add(prop);
        }
        int n = Mathf.Min(barriles.Count, Subidas.Length);
        for (int i = 0; i < n; i++) barriles[i].transform.position += Vector3.up * Subidas[i];
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        OrdenesDelEditor.Avisar("Barriles", $"{n} de {barriles.Count} barriles devueltos a la superficie.");
    }
}
