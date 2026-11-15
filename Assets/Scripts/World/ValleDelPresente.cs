using UnityEngine;
using UnityEngine.SceneManagement;

/// Valle del prólogo (INC-1200): el pueblo del valle tal como está hoy (y lo que solo es de hoy, como la estatua del
/// Archimago). Mientras está cargada la escena del prólogo (el valle siglos atrás, con sus propias casas en el mismo
/// sitio), lo de hoy se esconde para que no se vean las dos épocas a la vez. En el Editor también (el horneado del
/// guion del prólogo no debe ver las dos épocas): si se guarda MainWorld con el prólogo abierto, queda escondido, y al
/// jugar sin el prólogo se vuelve a enseñar solo.
[ExecuteAlways]
public class ValleDelPresente : MonoBehaviour
{
    [SerializeField] private string escenaDelPasado = "Prologo_Valle";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargar;
        SceneManager.sceneUnloaded += AlDescargar;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += AlAbrirEnEditor;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += AlCerrarEnEditor;
#endif
        Aplicar(PasadoCargado());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneUnloaded -= AlDescargar;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= AlAbrirEnEditor;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosed -= AlCerrarEnEditor;
#endif
    }

    private void AlCargar(Scene escena, LoadSceneMode _) { if (escena.name == escenaDelPasado) Aplicar(true); }
    private void AlDescargar(Scene escena) { if (escena.name == escenaDelPasado) Aplicar(false); }
#if UNITY_EDITOR
    private void AlAbrirEnEditor(Scene escena, UnityEditor.SceneManagement.OpenSceneMode _) { if (escena.name == escenaDelPasado) Aplicar(true); }
    private void AlCerrarEnEditor(Scene escena) { if (escena.name == escenaDelPasado) Aplicar(false); }
#endif

    private bool PasadoCargado()
    {
        Scene s = SceneManager.GetSceneByName(escenaDelPasado);
        return s.IsValid() && s.isLoaded;
    }

    private void Aplicar(bool ocultar)
    {
        if (this == null) return;
        foreach (Transform hijo in transform)
            if (hijo.gameObject.activeSelf == ocultar) hijo.gameObject.SetActive(!ocultar);
    }
}
