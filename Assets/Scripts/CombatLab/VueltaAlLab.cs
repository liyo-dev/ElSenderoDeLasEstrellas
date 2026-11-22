using UnityEngine;
using UnityEngine.SceneManagement;

/// Vuelta al LAB. Caer no lleva al último guardado: se recarga el LAB, en la entrada de la zona de
/// jefes si la caída fue en una pelea de jefe o en otra escena de pruebas (Batalla Final).
/// También da la salida al menú principal sin tocar la partida.
public sealed class VueltaAlLab : MonoBehaviour, IRespuestaALaDerrota
{
    public const string EscenaDelLab = "Lab";
    private const string EscenaDelMenu = "MainMenu";
    private static string s_destinoDelPortal;
    private static Scene s_escenaDesdeElLab;

    public static bool EnElLab => SceneManager.GetActiveScene().name == EscenaDelLab;

    public static bool LlegoDesdeElLab(Scene escena) => escena.IsValid() && escena == s_escenaDesdeElLab;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= RegistrarLlegada;
        s_destinoDelPortal = null;
        s_escenaDesdeElLab = default;
    }
#endif

    public static void SalirHacia(string escena)
    {
        s_destinoDelPortal = escena;
        SceneManager.sceneLoaded -= RegistrarLlegada;
        SceneManager.sceneLoaded += RegistrarLlegada;
        SceneManager.LoadScene(escena);
    }

    private static void RegistrarLlegada(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single) return;

        // Las aditivas del arranque no cambian el origen. La siguiente carga completa lo borra.
        s_escenaDesdeElLab = escena.name == s_destinoDelPortal || escena.path == s_destinoDelPortal ? escena : default;
        s_destinoDelPortal = null;
        if (!s_escenaDesdeElLab.IsValid()) SceneManager.sceneLoaded -= RegistrarLlegada;
    }

    private void OnEnable() => GameOverManager.RegistrarRespuestaALaDerrota(this);
    private void OnDisable() => GameOverManager.QuitarRespuestaALaDerrota(this);

    public bool Responder()
    {
        if (!EnElLab || ZonaDeJefesDelLab.HayCombateEnCurso) ZonaDeJefesDelLab.EmpezarEnLaZonaAlCargar();
        SceneManager.LoadScene(EscenaDelLab);
        return true;
    }

    /// Carga el LAB y aparece en la entrada de la zona de jefes.
    public static void VolverALaZonaDeJefes()
    {
        ZonaDeJefesDelLab.EmpezarEnLaZonaAlCargar();
        PanelDelLab.Cerrar();
        SceneManager.LoadScene(EscenaDelLab);
    }

    public static void SalirAlMenu()
    {
        PanelDelLab.Cerrar();
        SceneManager.LoadScene(EscenaDelMenu);
    }
}
