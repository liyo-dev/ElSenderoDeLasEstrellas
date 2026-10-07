using UnityEngine;
using UnityEngine.SceneManagement;

/// Carga otra escena de pruebas (la Batalla Final). Desde allí se vuelve con VueltaAlLab.
public sealed class PortalDeEscenaDelLab : PortalDelLab
{
    [SerializeField] private string escena = "BatallaFinal";

    protected override void Cruzar(GameObject jugador)
    {
        if (!string.IsNullOrEmpty(escena)) SceneManager.LoadScene(escena);
    }
}
