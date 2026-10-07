using UnityEngine;
using UnityEngine.SceneManagement;

/// Vuelta al LAB. Caer no lleva al último guardado: se recarga el LAB, en la entrada de la zona de
/// jefes si la caída fue en una pelea de jefe o en otra escena de pruebas (Batalla Final).
/// También da la salida al menú principal sin tocar la partida.
public sealed class VueltaAlLab : MonoBehaviour, IRespuestaALaDerrota
{
    public const string EscenaDelLab = "Lab";
    private const string EscenaDelMenu = "MainMenu";

    public static bool EnElLab => SceneManager.GetActiveScene().name == EscenaDelLab;

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
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaDelLab);
    }

    public static void SalirAlMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaDelMenu);
    }
}
