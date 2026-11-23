using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static partial class MontajeDeLosCapitulos
{
    private const string CobertizoPendiente = "Sendero.RetirarMarcadorDelCobertizo";

    [InitializeOnLoadMethod]
    private static void EscucharSalidaDePlayParaElCobertizo()
    {
        EditorApplication.playModeStateChanged += AlSalirDePlay;
        EditorApplication.delayCall += AplicarCobertizoPendiente;
    }

    [MenuItem("El Sendero/Historia/Retirar marcador del cobertizo")]
    private static void RetirarMarcadorDelCobertizo()
    {
        const string titulo = "Retirar marcador del cobertizo";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(CobertizoPendiente, true);
            OrdenesDelEditor.Avisar(titulo, "Se aplica al salir de Play.");
            return;
        }

        OrdenesDelEditor.Avisar(titulo, GuardarRetiradaDelCobertizo());
    }

    private static string GuardarRetiradaDelCobertizo()
    {
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);
        bool guardada = true;
        if (RetirarInspeccionesAntiguasDePez(escena))
        {
            EditorSceneManager.MarkSceneDirty(escena);
            guardada = EditorSceneManager.SaveScene(escena);
        }
        if (cerrar && guardada) EditorSceneManager.CloseScene(escena, true);
        return guardada ? "Inspecciones antiguas inactivas en MainWorld." : "MainWorld pendiente de guardar.";
    }

    private static void AlSalirDePlay(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += AplicarCobertizoPendiente;
    }

    private static void AplicarCobertizoPendiente()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(CobertizoPendiente, false)) return;
        SessionState.EraseBool(CobertizoPendiente);
        UnityEngine.Debug.Log("[Retirar marcador del cobertizo] " + GuardarRetiradaDelCobertizo());
    }
}
