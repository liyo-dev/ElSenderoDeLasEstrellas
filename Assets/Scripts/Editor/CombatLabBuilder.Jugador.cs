using Invector.vCharacterController;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El jugador del LAB: dónde se pone al regenerar la escena y cómo reponerlo si la escena se ha
/// quedado sin él. Sin jugador, <see cref="PlayerService"/> no registra a nadie, el HUD se queda
/// sin vida ni maná y la cámara no sigue a nadie: el laboratorio parece roto.
/// </summary>
public static partial class CombatLabBuilder
{
    private const string NombreDelJugadorDelLab = "LAB_JUGADOR_WILL";
    private const string RutaDelJugadorDelLab = "Assets/Prefabs/_WILL.prefab";
    private static readonly Vector3 PosicionDelJugadorDelLab = new(0f, 0.1f, -8f);

    /// <summary>
    /// Repara una escena del LAB a la que le falta el jugador o cuyo bootstrap lo ha perdido:
    /// abre la escena en aditivo (sin cerrar lo que haya abierto), engancha el jugador que ya
    /// estuviera o instancia el prefab de Will en su sitio, y lo guarda.
    /// </summary>
    [MenuItem("El Sendero/Combate/LAB: reponer el jugador si falta")]
    public static void ReponerJugadorDelLab()
    {
        if (Application.isPlaying)
        {
            OrdenesDelEditor.Avisar("LAB", "Sal de Play antes de reparar la escena.");
            return;
        }

        if (!System.IO.File.Exists(ScenePath))
        {
            OrdenesDelEditor.Avisar("LAB", $"No existe {ScenePath}.");
            return;
        }

        Scene escena = SceneManager.GetSceneByPath(ScenePath);
        bool abiertaAqui = false;
        if (!escena.IsValid() || !escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abiertaAqui = true;
        }

        try
        {
            CombatLabBootstrap bootstrap = BuscarBootstrap(escena);
            if (bootstrap == null)
            {
                OrdenesDelEditor.Avisar("LAB", "No encuentro LAB_INICIALIZACION con CombatLabBootstrap en Lab.unity.");
                return;
            }

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty campoJugador = serialized.FindProperty("player");
            GameObject jugador = campoJugador.objectReferenceValue as GameObject;

            if (jugador != null)
            {
                Avisar($"El LAB ya tiene jugador: {jugador.name}.");
                return;
            }

            jugador = BuscarCuerpoDelJugador(escena);
            string resultado;
            if (jugador != null)
            {
                resultado = $"Enganchado el jugador que ya estaba en la escena: {jugador.name}.";
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaDelJugadorDelLab);
                if (prefab == null)
                {
                    OrdenesDelEditor.Avisar("LAB", $"No se encontró {RutaDelJugadorDelLab}.");
                    return;
                }

                jugador = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                jugador.name = NombreDelJugadorDelLab;
                jugador.transform.SetPositionAndRotation(PosicionDelJugadorDelLab, Quaternion.identity);
                resultado = $"Repuesto {NombreDelJugadorDelLab} en {PosicionDelJugadorDelLab}.";
            }

            campoJugador.objectReferenceValue = jugador;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Avisar(resultado + " Escena guardada.");
        }
        finally
        {
            if (abiertaAqui && escena.IsValid() && escena.isLoaded)
                EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static void Avisar(string mensaje)
    {
        OrdenesDelEditor.Avisar("LAB", mensaje);
        Debug.Log("[LAB] " + mensaje);
    }

    private static CombatLabBootstrap BuscarBootstrap(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            var bootstrap = raiz.GetComponentInChildren<CombatLabBootstrap>(true);
            if (bootstrap != null) return bootstrap;
        }
        return null;
    }

    private static GameObject BuscarCuerpoDelJugador(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            var cuerpo = raiz.GetComponentInChildren<vThirdPersonController>(true);
            if (cuerpo != null) return cuerpo.gameObject;
        }
        return null;
    }
}
