using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Muestra una moneda y su cantidad; se refresca cuando cambia el inventario.</summary>
[DisallowMultipleComponent]
public class ContadorDeMonedas : MonoBehaviour
{
    [SerializeField] private ItemData moneda;
    [SerializeField] private Inventory inventario;
    [SerializeField] private TMP_Text cantidad;
    [SerializeField] private Image icono;
    [SerializeField, Tooltip("Oculta el contador mientras la cantidad de esta moneda es cero.")]
    private bool ocultarSinCantidad;
    private CanvasGroup visibilidad;
    private Inventory suscrito;
    protected ItemData Moneda => moneda;
    protected Inventory Inventario => inventario;

    public void ConectarInventario(Inventory inventory)
    {
        Desuscribir();
        inventario = inventory;
        if (isActiveAndEnabled) Suscribir();
        Refrescar();
    }

    public void Configurar(ItemData item, Inventory inventory, TMP_Text texto, Image imagen, bool ocultarCuandoVacio = false)
    {
        Desuscribir();
        moneda = item;
        inventario = inventory;
        cantidad = texto;
        icono = imagen;
        if (ocultarSinCantidad && !ocultarCuandoVacio && visibilidad != null) visibilidad.alpha = 1f;
        ocultarSinCantidad = ocultarCuandoVacio;
        PrepararVisibilidad();
        if (isActiveAndEnabled) Suscribir();
        Refrescar();
    }

    protected virtual void OnEnable()
    {
        PrepararVisibilidad();
        if (inventario == null)
            PlayerService.TryGetComponent(out inventario, includeInactive: true, allowSceneLookup: true);
        Suscribir();
        Refrescar();
    }

    protected virtual void OnDisable() => Desuscribir();

    private void Suscribir()
    {
        if (suscrito == inventario) return;
        Desuscribir();
        suscrito = inventario;
        if (suscrito != null) suscrito.OnInventoryChanged += AlCambiarInventario;
    }

    private void Desuscribir()
    {
        if (suscrito != null) suscrito.OnInventoryChanged -= AlCambiarInventario;
        suscrito = null;
    }

    protected virtual void AlCambiarInventario(ItemData item, int total) => Refrescar();

    private void PrepararVisibilidad()
    {
        if (!ocultarSinCantidad || visibilidad != null) return;
        visibilidad = GetComponent<CanvasGroup>();
        if (visibilidad == null) visibilidad = gameObject.AddComponent<CanvasGroup>();
        visibilidad.interactable = false;
        visibilidad.blocksRaycasts = false;
    }

    protected void Refrescar()
    {
        int total = inventario != null && moneda != null ? inventario.Count(moneda.itemId) : 0;
        if (cantidad != null)
            cantidad.text = total.ToString();
        if (icono != null) icono.sprite = moneda != null ? moneda.icon : null;
        // La suscripción permanece activa aunque se oculte, para mostrar la primera ganancia.
        if (ocultarSinCantidad && visibilidad != null) visibilidad.alpha = total > 0 ? 1f : 0f;
    }
}
