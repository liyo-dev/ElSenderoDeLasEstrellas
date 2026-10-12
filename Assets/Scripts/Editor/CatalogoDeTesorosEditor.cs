using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantiene el catálogo de tesoros (Resources/CatalogoDeTesoros.asset) para que el registro sepa
/// el total de cada región: al guardar una escena rehace la parte de esa escena y, con el menú,
/// repasa todas las escenas del build. Las escenas de prueba (Scenes/Test) no entran: sus
/// tesoros cuentan solo mientras están cargadas. Ver INC-721.
/// </summary>
[InitializeOnLoad]
public static class CatalogoDeTesorosEditor
{
    private const string Ruta = "Assets/Resources/" + CatalogoDeTesoros.RutaEnResources + ".asset";
    private const string CarpetaDePruebas = "Assets/Scenes/Test/";

    static CatalogoDeTesorosEditor()
    {
        EditorSceneManager.sceneSaved -= AlGuardarEscena;
        EditorSceneManager.sceneSaved += AlGuardarEscena;
    }

    [MenuItem("El Sendero/Tesoros/Actualizar el catálogo de tesoros (todas las escenas)")]
    public static void ActualizarTodo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El catálogo de tesoros se actualiza fuera de Play."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var catalogo = Cargar();
        catalogo.tesoros.Clear();
        int escenas = 0;
        foreach (var e in EditorBuildSettings.scenes)
        {
            if (!e.enabled || e.path.StartsWith(CarpetaDePruebas)) continue;
            var escena = SceneManager.GetSceneByPath(e.path);
            bool abierta = !escena.isLoaded;
            if (abierta) escena = EditorSceneManager.OpenScene(e.path, OpenSceneMode.Additive);
            try { Anotar(catalogo, escena); escenas++; }
            finally { if (abierta) EditorSceneManager.CloseScene(escena, true); }
        }
        Guardar(catalogo);
        Debug.Log($"Catálogo de tesoros: {catalogo.tesoros.Count} tesoros en {escenas} escenas.");
    }

    private static void AlGuardarEscena(Scene escena)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrEmpty(escena.path)) return;
        if (escena.path.StartsWith(CarpetaDePruebas)) return;
        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoDeTesoros>(Ruta);
        bool tieneTesoros = TieneTesoros(escena);
        if (catalogo == null && !tieneTesoros) return;
        if (catalogo == null) catalogo = Cargar();

        int antes = catalogo.tesoros.Count;
        var previos = new List<CatalogoDeTesoros.Entrada>(catalogo.tesoros);
        catalogo.tesoros.RemoveAll(t => t.escena == escena.path);
        Anotar(catalogo, escena);
        if (antes == catalogo.tesoros.Count && Iguales(previos, catalogo.tesoros)) return;
        Guardar(catalogo);
    }

    private static void Anotar(CatalogoDeTesoros catalogo, Scene escena)
    {
        var vistos = new HashSet<string>();
        foreach (var t in catalogo.tesoros) vistos.Add(t.id);
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var tesoro in raiz.GetComponentsInChildren<Tesoro>(true))
            {
                var so = new SerializedObject(tesoro);
                string manual = so.FindProperty("idManual").stringValue;
                string id = string.IsNullOrWhiteSpace(manual) ? IdDePersistencia.DeObjeto(tesoro.gameObject) : manual.Trim();
                string region = so.FindProperty("region").stringValue;
                if (!vistos.Add(id))
                {
                    Debug.LogWarning($"[Tesoros] Id repetido «{id}» ({escena.path}, {tesoro.name}): ponle un id manual distinto.", tesoro);
                    continue;
                }
                catalogo.tesoros.Add(new CatalogoDeTesoros.Entrada
                {
                    id = id,
                    region = string.IsNullOrWhiteSpace(region) ? Tesoro.RegionPorDefecto : region.Trim(),
                    escena = escena.path
                });
            }
    }

    private static bool TieneTesoros(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
            if (raiz.GetComponentInChildren<Tesoro>(true) != null) return true;
        return false;
    }

    private static bool Iguales(List<CatalogoDeTesoros.Entrada> a, List<CatalogoDeTesoros.Entrada> b)
    {
        if (a.Count != b.Count) return false;
        var claves = new HashSet<string>();
        foreach (var e in a) claves.Add(e.id + "|" + e.region + "|" + e.escena);
        foreach (var e in b) if (!claves.Contains(e.id + "|" + e.region + "|" + e.escena)) return false;
        return true;
    }

    private static CatalogoDeTesoros Cargar()
    {
        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoDeTesoros>(Ruta);
        if (catalogo != null) return catalogo;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        catalogo = ScriptableObject.CreateInstance<CatalogoDeTesoros>();
        AssetDatabase.CreateAsset(catalogo, Ruta);
        return catalogo;
    }

    private static void Guardar(CatalogoDeTesoros catalogo)
    {
        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
    }
}
