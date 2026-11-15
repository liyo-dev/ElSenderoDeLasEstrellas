#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// La torre de Liam no tenía techo: por encima de las vigas se veía el cielo, y de noche (el espejo
/// del Cap. 3) quedaba un hueco negro. El suelo de piedra gris, con poca luz, se veía azulado. Pone
/// techo de madera (con el material de las vigas) al cuarto de Liam y al de Tobi, y suelo de madera
/// (el del cuarto de Tobi) al de Liam. Ver INC-998. Se puede ejecutar varias veces: rehace lo suyo.
public static class Cap3TechoDeLaTorre
{
    const string RutaDeLaEscena = "Assets/Scenes/Interior/TorreDeLiam.unity";
    const string Raiz = "TorreDeLiam_Decorado";
    const string Grupo = "Techo y suelo (INC-998)";

    [MenuItem("El Sendero/Archivo/Capítulo 3/Torre de Liam: techo y suelo de madera (INC-998)")]
    public static void Aplicar()
    {
        const string titulo = "Techo de la torre";
        if (EditorApplication.isPlayingOrWillChangePlaymode) { OrdenesDelEditor.Avisar(titulo, "Sal de Play antes de ejecutarlo."); return; }

        Scene escena = SceneManager.GetSceneByPath(RutaDeLaEscena);
        bool laHeAbierto = !escena.IsValid() || !escena.isLoaded;
        if (laHeAbierto) escena = EditorSceneManager.OpenScene(RutaDeLaEscena, OpenSceneMode.Additive);
        else if (escena.isDirty) { OrdenesDelEditor.Avisar(titulo, "TorreDeLiam está abierta con cambios sin guardar; no la toco."); return; }

        Transform decorado = null;
        foreach (var r in escena.GetRootGameObjects()) if (r.name == Raiz) decorado = r.transform;
        if (decorado == null) { OrdenesDelEditor.Avisar(titulo, "No encuentro " + Raiz); return; }

        var viejo = decorado.Find(Grupo);
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        var grupo = new GameObject(Grupo).transform;
        grupo.SetParent(decorado, false);

        Material madera = MaterialDe(decorado, "Viga");
        Material suelo = MaterialDe(decorado, "Tobi · suelo");

        // Cuarto de Liam: Room01_a01 ocupa (-6003.7…-5996.3, 100…103.4, 5996.8…6003.2).
        Losa(grupo, "Techo de Liam", new Vector3(-6000f, 103.45f, 6000f), new Vector3(7.4f, 0.1f, 6.4f), madera);
        Losa(grupo, "Suelo de Liam", new Vector3(-6000f, 100.015f, 6000f), new Vector3(7.2f, 0.02f, 6.2f), suelo);
        // Cuarto de Tobi: suelo de 3,2 m y paredes de 2,6 m de alto.
        Losa(grupo, "Techo de Tobi", new Vector3(-5988f, 102.65f, 6000f), new Vector3(3.2f, 0.1f, 3.2f), madera);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (laHeAbierto) EditorSceneManager.CloseScene(escena, true);
        OrdenesDelEditor.Avisar(titulo, "TorreDeLiam: techo en los dos cuartos y suelo de madera en el de Liam. Escena guardada.");
    }

    static Material MaterialDe(Transform raiz, string nombre)
    {
        var t = raiz.Find(nombre);
        var r = t != null ? t.GetComponentInChildren<Renderer>() : null;
        return r != null ? r.sharedMaterial : null;
    }

    static void Losa(Transform padre, string nombre, Vector3 centro, Vector3 tamano, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        go.transform.localScale = tamano;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
#endif
