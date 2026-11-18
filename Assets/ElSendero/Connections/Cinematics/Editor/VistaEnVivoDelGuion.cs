using Liyodev.Cinematics;
using UnityEngine;

/// La vista en vivo que el Director pide para un guion de El Sendero: la sesión del previz
/// (`PrevizDeGuion.Sesion`) detrás del contrato del paquete. Coloca actores, poses y cámara en el
/// segundo pedido y rinde la escena en la textura de la mesa.
internal sealed class VistaEnVivoDelGuion : ICinematicLiveView
{
    private readonly PrevizDeGuion.Sesion _sesion;

    private VistaEnVivoDelGuion(PrevizDeGuion.Sesion sesion) => _sesion = sesion;

    public static VistaEnVivoDelGuion Abrir(GuionHorneado guion, out string error)
    {
        var sesion = PrevizDeGuion.Sesion.Abrir(guion, out error);
        return sesion != null ? new VistaEnVivoDelGuion(sesion) : null;
    }

    public string Error => _sesion.Error;

    public bool Rendir(float segundo, RenderTexture destino) => _sesion.Rendir(segundo, destino);

    public void Soltar() => _sesion.Soltar();

    public void Dispose() => _sesion.Dispose();
}
