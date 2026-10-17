using UnityEngine;

/// <summary>
/// Trigger espacial para los hitos y portales a lo largo del Sendero de las Estrellas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SenderoTriggerArea : MonoBehaviour
{
    public enum TipoAccionTrigger
    {
        ManifestarPortalWill,
        EntrarPruebaWill,
        ManifestarPortalEstela,
        EntrarPruebaEstela,
        ManifestarPortalLiam,
        EntrarPruebaLiam,
        AvanzarHaciaAltar,
        DetonarAparicionMagoOscuro
    }

    [SerializeField] private TipoAccionTrigger _accion = TipoAccionTrigger.ManifestarPortalWill;
    [SerializeField] private bool _desactivarTrasDisparo = true;

    private bool _disparado;

    private void OnTriggerEnter(Collider other)
    {
        if (_disparado) return;

        // Comprobación de jugador oficial (tag Player o NPCSimpleAnimator en raíz según reglas del proyecto)
        if (!other.CompareTag("Player") && other.GetComponentInParent<NPCSimpleAnimator>() == null)
            return;

        _disparado = true;
        EjecutarAccion();

        if (_desactivarTrasDisparo)
        {
            gameObject.SetActive(false);
        }
    }

    private void EjecutarAccion()
    {
        var controller = SenderoProgressionController.Instance;
        if (controller == null) return;

        switch (_accion)
        {
            case TipoAccionTrigger.ManifestarPortalWill:
                controller.ManifestarPortalWill();
                break;
            case TipoAccionTrigger.EntrarPruebaWill:
                controller.EntrarPruebaWill();
                break;
            case TipoAccionTrigger.ManifestarPortalEstela:
                controller.ManifestarPortalEstela();
                break;
            case TipoAccionTrigger.EntrarPruebaEstela:
                controller.EntrarPruebaEstela();
                break;
            case TipoAccionTrigger.ManifestarPortalLiam:
                controller.ManifestarPortalLiam();
                break;
            case TipoAccionTrigger.EntrarPruebaLiam:
                controller.EntrarPruebaLiam();
                break;
            case TipoAccionTrigger.AvanzarHaciaAltar:
                controller.AvanzarHaciaElAltar();
                break;
            case TipoAccionTrigger.DetonarAparicionMagoOscuro:
                controller.DesatarAparicionMagoOscuro();
                break;
        }
    }
}
