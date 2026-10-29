using UnityEngine;

/// <summary>
/// Arranque de la escena de prueba del nivel de Escape del Sendero: registra a Will en
/// PlayerService y pone la vuelta al laboratorio al caer (VueltaAlLab). Lo que el nivel cambia
/// mientras se juega (sin vuelo, HUD y minimapa ocultos) lo hace y lo deshace NivelEscapeSendero.
/// </summary>
[DefaultExecutionOrder(-850)]
public sealed class EscapeSenderoBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject player;

    private void Awake()
    {
        if (player != null)
        {
            PlayerService.RegisterPlayer(player);
        }

        if (GetComponent<VueltaAlLab>() == null)
        {
            gameObject.AddComponent<VueltaAlLab>();
        }
    }
}
