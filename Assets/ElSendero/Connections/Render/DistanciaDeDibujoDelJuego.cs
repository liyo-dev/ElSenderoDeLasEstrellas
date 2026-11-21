using Kits.DrawDistance;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enchufe del kit <c>Kits.DrawDistance</c>: cada escena de mundo (<c>Assets/Scenes/Worlds/</c>) lleva al
/// cargarse un <see cref="ScreenSizeCuller"/> que deja de dibujar las piezas que se verían más pequeñas que
/// unos pocos píxeles (lo pequeño y lejano del vestido del mundo). Ver INC-1339.
/// </summary>
public static class DistanciaDeDibujoDelJuego
{
    const string CarpetaDeMundos = "/Scenes/Worlds/";
    const string NombreDelObjeto = "[Distancia de dibujo]";

    // Un objeto con DontSave no se va solo al descargar su escena: se quita a mano (escena → objeto).
    static readonly System.Collections.Generic.Dictionary<Scene, GameObject> s_porEscena =
        new System.Collections.Generic.Dictionary<Scene, GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Enganchar()
    {
        s_porEscena.Clear();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        SceneManager.sceneUnloaded -= AlDescargarEscena;
        SceneManager.sceneUnloaded += AlDescargarEscena;
        // En el Editor, salir de Play también avisa con quitting: un DontSave sobreviviría a Play.
        Application.quitting -= QuitarTodos;
        Application.quitting += QuitarTodos;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            AlCargarEscena(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        // Sin recarga de dominio al salir de Play la suscripción sigue viva: fuera de Play no se toca la escena.
        if (!Application.isPlaying) return;
        if (!escena.IsValid() || !escena.isLoaded || escena.path.IndexOf(CarpetaDeMundos, System.StringComparison.Ordinal) < 0) return;
        if (s_porEscena.TryGetValue(escena, out var yaPuesto) && yaPuesto != null) return;

        var objeto = new GameObject(NombreDelObjeto) { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(objeto, escena);
        objeto.AddComponent<ScreenSizeCuller>().WholeScene = true;
        s_porEscena[escena] = objeto;
    }

    static void QuitarTodos()
    {
        foreach (var objeto in s_porEscena.Values)
            if (objeto != null) Object.DestroyImmediate(objeto);
        s_porEscena.Clear();
    }

    static void AlDescargarEscena(Scene escena)
    {
        if (!s_porEscena.TryGetValue(escena, out var objeto)) return;
        s_porEscena.Remove(escena);
        if (objeto != null) Object.Destroy(objeto);
    }
}
