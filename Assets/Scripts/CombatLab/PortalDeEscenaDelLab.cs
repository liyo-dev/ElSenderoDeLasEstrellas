using UnityEngine;

/// Carga una escena de pruebas y registra que se llegó desde el laboratorio.
public sealed class PortalDeEscenaDelLab : PortalDelLab
{
    [SerializeField] private string escena = "BatallaFinal";

    protected override void Cruzar(GameObject jugador)
    {
        if (!string.IsNullOrEmpty(escena)) VueltaAlLab.SalirHacia(escena);
    }
}
