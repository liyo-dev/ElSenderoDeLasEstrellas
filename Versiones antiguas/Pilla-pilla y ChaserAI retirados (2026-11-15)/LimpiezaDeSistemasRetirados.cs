#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Limpieza del 15 nov 2026: quita los restos del pilla-pilla de los prefabs y del menú, saca la
/// escena de pruebas de la build y hace un recuento de los materiales de las escenas de la build
/// que no son del aspecto de dibujo. Menú de un solo uso: al ejecutarse, el fichero sale a
/// «Versiones antiguas/».
public static class LimpiezaDeSistemasRetirados
{
    private static readonly string[] Prefabs =
    {
        "Assets/Prefabs/_WILL.prefab",
        "Assets/Prefabs/_ESTELA.prefab",
    };

    private const string EscenaDelMenu = "Assets/Scenes/Systems/MainMenu.unity";
    private const string EscenaDePruebas = "Assets/Scenes/Test/Lab.unity";

    [MenuItem("El Sendero/Limpieza/Retirar sistemas sueltos y auditar el aspecto")]
    public static void Ejecutar()
    {
        int componentes = QuitarDeLosPrefabs() + QuitarDeLaEscena(EscenaDelMenu);

        int escenas = QuitarDeLaBuild();

        AssetDatabase.SaveAssets();
        string informe = RecuentoDeMateriales();
        Debug.Log($"[Limpieza] {componentes} componentes retirados, {escenas} escena(s) fuera de la build.\n{informe}");
    }

    private static int QuitarDeLosPrefabs()
    {
        int n = 0;
        foreach (string ruta in Prefabs)
        {
            GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool tocado = false;
                foreach (var c in raiz.GetComponentsInChildren<ChaserAI>(true))
                {
                    UnityEngine.Object.DestroyImmediate(c, true);
                    n++; tocado = true;
                }
                if (tocado) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        return n;
    }

    private static int QuitarDeLaEscena(string ruta)
    {
        if (!System.IO.File.Exists(ruta)) return 0;
        int n = 0;
        EnEscena(ruta, escena =>
        {
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var c in raiz.GetComponentsInChildren<ChaserAI>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(c)) { Debug.LogWarning($"[Limpieza] ChaserAI de un prefab en {raiz.name}: no se toca"); continue; }
                    UnityEngine.Object.DestroyImmediate(c, true);
                    n++;
                }
        });
        return n;
    }

    private static int QuitarDeLaBuild()
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int antes = escenas.Count;
        escenas.RemoveAll(e => string.Equals(e.path, EscenaDePruebas, StringComparison.OrdinalIgnoreCase));
        if (escenas.Count == antes) return 0;
        EditorBuildSettings.scenes = escenas.ToArray();
        return antes - escenas.Count;
    }

    /// Materiales de las escenas de la build que no son del aspecto de dibujo ni de luz sin sombra.
    private static string RecuentoDeMateriales()
    {
        var malos = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        int revisados = 0;
        foreach (var escena in EditorBuildSettings.scenes)
        {
            if (!escena.enabled) continue;
            foreach (string dependencia in AssetDatabase.GetDependencies(escena.path, true))
            {
                if (!dependencia.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(dependencia);
                if (mat == null || mat.shader == null) continue;
                revisados++;
                if (EsDelAspecto(mat.shader)) continue;
                string shader = mat.shader.name;
                if (!malos.TryGetValue(shader, out var lista)) malos[shader] = lista = new List<string>();
                lista.Add(dependencia);
            }
        }

        var texto = new StringBuilder();
        texto.Append("Materiales revisados en las escenas de la build: ").Append(revisados).AppendLine();
        if (malos.Count == 0) texto.AppendLine("TODO el arte de la build está en el aspecto de dibujo.");
        foreach (var par in malos)
        {
            texto.Append("  ").Append(par.Key).Append("  (").Append(par.Value.Count).Append("): ");
            texto.AppendLine(string.Join(", ", par.Value));
        }
        return texto.ToString();
    }

    private static bool EsDelAspecto(Shader shader)
    {
        if (shader.name.StartsWith("Hidden/", StringComparison.Ordinal)) return true;
        string ruta = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(ruta)) return false;
        return ruta.StartsWith("Assets/Shaders/", StringComparison.Ordinal)
            || ruta.StartsWith("Assets/Scripts/Shaders/", StringComparison.Ordinal)
            || ruta.StartsWith("Assets/Kits/", StringComparison.Ordinal)
            || ruta.StartsWith("Assets/_VFX/Prologo/", StringComparison.Ordinal)
            || ruta.StartsWith("Assets/Resources/", StringComparison.Ordinal)
            || ruta.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)
            || shader.name.Contains("Unlit")
            || shader.name.Contains("Sprite");
    }

    private static void EnEscena(string ruta, Action<UnityEngine.SceneManagement.Scene> cambio)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var antes = EditorSceneManager.GetSceneManagerSetup();
        var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        cambio(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (antes != null && antes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(antes);
    }
}
#endif
