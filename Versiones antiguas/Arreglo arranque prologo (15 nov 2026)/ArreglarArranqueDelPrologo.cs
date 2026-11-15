using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez (INC-1210): el prólogo arranca en Prologo_Valle + WillHouse sin cargar
// el mundo abierto, y el editor deja de recargar el dominio en cada Play. Deja MainWorld para
// cuando empieza el capítulo.
internal static class ArreglarArranqueDelPrologo
{
    private const string Clave = "ElSendero.ArranquePrologo.Hecho";
    private const string RutaPrologo = "Assets/Scenes/Worlds/Prologo_Valle.unity";
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";
    private const string EscenaNuevaPartida = "Prologo_Valle";

    [InitializeOnLoadMethod]
    private static void Arrancar()
    {
        if (SessionState.GetBool(Clave, false)) return;
        EditorApplication.update += Intentar;
    }

    private static void Intentar()
    {
        if (SessionState.GetBool(Clave, false))
        {
            EditorApplication.update -= Intentar;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= Intentar;
        SessionState.SetBool(Clave, true);
        Aplicar();
    }

    private static void Aplicar()
    {
        var informe = new StringBuilder("[Arranque prologo] ");

        // 1) Editor: no recargar el dominio en cada Play. El proyecto ya resetea sus estáticos en
        //    SubsystemRegistration; si algo se quedara sucio, se vuelve a marcar «Reload Domain».
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        informe.Append("dominio sin recargar; ");

        // 2) Prologo_Valle: que tenga su WorldBootstrap (el MainWorld no se carga en el prólogo).
        Scene prologo = SceneManager.GetSceneByName("Prologo_Valle");
        bool abrirPrologo = !prologo.IsValid() || !prologo.isLoaded;
        if (abrirPrologo)
            prologo = EditorSceneManager.OpenScene(RutaPrologo, OpenSceneMode.Additive);

        bool tieneBootstrap = false;
        foreach (var raiz in prologo.GetRootGameObjects())
        {
            if (raiz.GetComponentInChildren<WorldBootstrap>(true) != null)
            {
                tieneBootstrap = true;
                break;
            }
        }

        if (!tieneBootstrap)
        {
            var go = new GameObject("WorldBootstrap");
            SceneManager.MoveGameObjectToScene(go, prologo);
            go.AddComponent<WorldBootstrap>();
            EditorSceneManager.MarkSceneDirty(prologo);
            informe.Append("WorldBootstrap añadido a Prologo_Valle; ");
        }
        else
        {
            informe.Append("Prologo_Valle ya tenía WorldBootstrap; ");
        }

        if (prologo.isDirty) EditorSceneManager.SaveScene(prologo);
        if (abrirPrologo) EditorSceneManager.CloseScene(prologo, true);

        // 3) Menú: Nueva Partida carga Prologo_Valle; Continuar sigue cargando MainWorld.
        Scene menu = SceneManager.GetSceneByName("MainMenu");
        bool abrirMenu = !menu.IsValid() || !menu.isLoaded;
        if (abrirMenu)
            menu = EditorSceneManager.OpenScene(RutaMenu, OpenSceneMode.Additive);

        MainMenuController controlador = null;
        foreach (var raiz in menu.GetRootGameObjects())
        {
            controlador = raiz.GetComponentInChildren<MainMenuController>(true);
            if (controlador != null) break;
        }

        if (controlador == null)
        {
            informe.Append("MainMenu sin MainMenuController: no se tocó la escena de Nueva Partida.");
        }
        else
        {
            var so = new SerializedObject(controlador);
            var campo = so.FindProperty("nextSceneNewGame");
            if (campo != null)
            {
                campo.stringValue = EscenaNuevaPartida;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(menu);
                EditorSceneManager.SaveScene(menu);
                informe.Append($"Nueva Partida -> {EscenaNuevaPartida}; ");
            }
            else
            {
                informe.Append("no se encontró nextSceneNewGame; ");
            }
        }

        if (abrirMenu) EditorSceneManager.CloseScene(menu, true);

        Debug.Log(informe.ToString());
    }
}
