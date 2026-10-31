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

        // Solo se gasta si el controlador la acepta: tocar una puerta que aún no toca no la estropea.
        if (!EjecutarAccion()) return;
        _disparado = true;

        if (_desactivarTrasDisparo)
        {
            gameObject.SetActive(false);
        }
    }

    private bool EjecutarAccion()
    {
        var controller = SenderoProgressionController.Instance;
        if (controller == null) return false;

        return _accion switch
        {
            TipoAccionTrigger.ManifestarPortalWill => controller.ManifestarPortalWill(),
            TipoAccionTrigger.EntrarPruebaWill => controller.EntrarPruebaWill(),
            TipoAccionTrigger.ManifestarPortalEstela => controller.ManifestarPortalEstela(),
            TipoAccionTrigger.EntrarPruebaEstela => controller.EntrarPruebaEstela(),
            TipoAccionTrigger.ManifestarPortalLiam => controller.ManifestarPortalLiam(),
            TipoAccionTrigger.EntrarPruebaLiam => controller.EntrarPruebaLiam(),
            TipoAccionTrigger.AvanzarHaciaAltar => controller.AvanzarHaciaElAltar(),
            TipoAccionTrigger.DetonarAparicionMagoOscuro => controller.DesatarAparicionMagoOscuro(),
            _ => false,
        };
    }
}
