using Sendero.UI;
using TMPro;
using UnityEngine;

/// <summary>Presenta cambios de una moneda con una ganancia agrupada y duración limitada.</summary>
public sealed class ContadorDeMonedaHUD : ContadorDeMonedas
{
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text ganancia;
    [SerializeField] private RectTransform textoFlotante;
    [SerializeField, Min(0.1f)] private float duracion = 2.5f;
    private int totalAnterior;
    private int acumulado;
    private float hasta;
    private float inicio;
    private float siguienteBusqueda;
    private Vector2 origen;

    protected override void OnEnable()
    {
        base.OnEnable();
        totalAnterior = Inventario != null && Moneda != null ? Inventario.Count(Moneda.itemId) : 0;
        if (textoFlotante != null) origen = textoFlotante.anchoredPosition;
        MenuManager.MenuOpened += AlCambiarMenu;
        MenuManager.MenuClosed += AlCambiarMenu;
        GameState.OnChanged += RevisarVisibilidad;
        Ocultar();
    }

    protected override void OnDisable()
    {
        MenuManager.MenuOpened -= AlCambiarMenu;
        MenuManager.MenuClosed -= AlCambiarMenu;
        GameState.OnChanged -= RevisarVisibilidad;
        base.OnDisable();
        Ocultar();
    }

    protected override void AlCambiarInventario(ItemData item, int total)
    {
        if (item == null || Moneda == null || item.itemId != Moneda.itemId) return;
        int diferencia = total - totalAnterior;
        totalAnterior = total;
        base.AlCambiarInventario(item, total);
        if (!PuedeMostrar()) { Ocultar(); return; }
        if (Time.unscaledTime >= hasta) acumulado = 0;
        acumulado += Mathf.Max(0, diferencia);
        if (ganancia != null)
        {
            ganancia.gameObject.SetActive(acumulado > 0);
            ganancia.SetText("+{0}", acumulado);
        }
        inicio = Time.unscaledTime;
        hasta = inicio + duracion;
        if (grupo != null) grupo.alpha = 1f;
    }

    private bool PuedeMostrar() => PlayerHUDV2.Instance != null && PlayerHUDV2.Instance.IsVisible &&
        !MenuManager.AnyOpen() && !GameState.Is(GamePhase.Cutscene) &&
        !GameState.Is(GamePhase.MainMenu) && !GameState.Is(GamePhase.Loading);

    private void AlCambiarMenu(MenuKind _) => RevisarVisibilidad();
    private void RevisarVisibilidad() { if (!PuedeMostrar()) Ocultar(); }
    private void Ocultar()
    {
        hasta = 0f;
        acumulado = 0;
        if (grupo != null) grupo.alpha = 0f;
    }

    private void Update()
    {
        if (Inventario == null && Time.unscaledTime >= siguienteBusqueda)
        {
            siguienteBusqueda = Time.unscaledTime + 0.5f;
            if (PlayerService.TryGetComponent(out Inventory inventory, allowSceneLookup: false))
            {
                ConectarInventario(inventory);
                totalAnterior = Moneda != null ? inventory.Count(Moneda.itemId) : 0;
            }
        }
        if (hasta <= 0f) return;
        if (!PuedeMostrar() || Time.unscaledTime >= hasta) { Ocultar(); return; }
        if (textoFlotante != null)
            textoFlotante.anchoredPosition = origen + Vector2.up * (24f * Mathf.Clamp01((Time.unscaledTime - inicio) / duracion));
    }
}
