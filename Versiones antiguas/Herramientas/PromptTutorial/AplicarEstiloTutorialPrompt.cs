using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AplicarEstiloTutorialPrompt
{
    const string RutaPrefab = "Assets/Prefabs/UI/TutorialPromptUI.prefab";
    const string RutaEscena = "Assets/Scenes/Systems/Start.unity";
    const string RutaFondo = "Assets/Art/UI/Misiones/panel_glass_bg.png";
    const string RutaMenu = "El Sendero/Archivo/Aplicar estilo glass al prompt de tutorial";
    const string ClaveEjecucion = "ElSendero.AplicarEstiloTutorialPrompt.20261014";

    [InitializeOnLoadMethod]
    static void ProgramarMenu()
    {
        if (SessionState.GetBool(ClaveEjecucion, false)) return;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("Se programa el menú de estilo del prompt de tutorial.");
#endif
        EditorApplication.delayCall += EjecutarMenu;
    }

    static void EjecutarMenu()
    {
        if (SessionState.GetBool(ClaveEjecucion, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("El Editor espera a salir de PlayMode antes de ajustar el prompt.");
#endif
            EditorApplication.playModeStateChanged -= EsperarModoEdicion;
            EditorApplication.playModeStateChanged += EsperarModoEdicion;
            return;
        }

        SessionState.SetBool(ClaveEjecucion, true);
        if (!EditorApplication.ExecuteMenuItem(RutaMenu))
        {
            SessionState.SetBool(ClaveEjecucion, false);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"No se pudo ejecutar el menú {RutaMenu}.");
#endif
        }
    }

    static void EsperarModoEdicion(PlayModeStateChange cambio)
    {
        if (cambio != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= EsperarModoEdicion;
        EditorApplication.delayCall += EjecutarMenu;
    }

    [MenuItem("El Sendero/Archivo/Aplicar estilo glass al prompt de tutorial")]
    public static void Aplicar()
    {
        var fondo = AssetDatabase.LoadAssetAtPath<Sprite>(RutaFondo);
        if (fondo == null)
            throw new InvalidOperationException($"No se encontró el sprite del panel: {RutaFondo}");

        ActualizarPrefab(fondo);

        var escenaAnterior = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.GetSceneByPath(RutaEscena);
        bool abrirEscena = !escena.IsValid() || !escena.isLoaded;
        if (abrirEscena)
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        if (escena.isDirty)
        {
            if (abrirEscena) EditorSceneManager.CloseScene(escena, true);
            throw new InvalidOperationException($"{RutaEscena} ya tiene cambios sin guardar.");
        }

        try
        {
            TutorialPromptUI prompt = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                prompt = raiz.GetComponentInChildren<TutorialPromptUI>(true);
                if (prompt != null) break;
            }

            if (prompt == null)
                throw new InvalidOperationException($"No se encontró TutorialPromptUI en {RutaEscena}");

            AplicarEstilo(prompt, fondo, 900f, 100f);
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException($"No se pudo guardar la escena {RutaEscena}");
        }
        finally
        {
            if (abrirEscena) EditorSceneManager.CloseScene(escena, true);
            if (escenaAnterior.IsValid() && escenaAnterior.isLoaded)
                SceneManager.SetActiveScene(escenaAnterior);
        }

        AssetDatabase.SaveAssets();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("El prompt de tutorial usa el panel glass y conserva su posición, texto e iconos.");
#endif
    }

    static void ActualizarPrefab(Sprite fondo)
    {
        var contenido = PrefabUtility.LoadPrefabContents(RutaPrefab);
        try
        {
            var prompt = contenido.GetComponentInChildren<TutorialPromptUI>(true);
            if (prompt == null)
                throw new InvalidOperationException($"No se encontró TutorialPromptUI en {RutaPrefab}");

            AplicarEstilo(prompt, fondo, 900f, 100f);
            PrefabUtility.SaveAsPrefabAsset(contenido, RutaPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    static void AplicarEstilo(TutorialPromptUI prompt, Sprite fondo, float ancho, float alto)
    {
        var rect = prompt.GetComponent<RectTransform>();
        var fondoTransform = prompt.transform.Find("Background");
        var imagen = fondoTransform != null ? fondoTransform.GetComponent<Image>() : null;
        if (rect == null || imagen == null)
            throw new InvalidOperationException("TutorialPromptUI necesita RectTransform e Image en Background.");

        rect.sizeDelta = new Vector2(ancho, alto);
        imagen.sprite = fondo;
        imagen.type = Image.Type.Sliced;
        imagen.color = Color.white;

        var fila = prompt.GetComponentInChildren<HorizontalLayoutGroup>(true);
        if (fila != null)
        {
            fila.padding.top = 12;
            fila.padding.bottom = 12;
            EditorUtility.SetDirty(fila);
        }

        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(imagen);
        EditorUtility.SetDirty(prompt.gameObject);
    }
}
