using UnityEngine;

/// Puente entre una tienda (ShopUI) y el grafo narrativo: dispara un evento personalizado
/// (RaiseCustom, compatible con WaitCustomEventNode) la primera vez que el jugador cierra esa
/// tienda. Pensado para "TUTORIAL_MARKET / MARKET_DONE" (Paso 4 del análisis de refactor,
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §3): el tutorial del mercado no
/// exige comprar nada en concreto, solo que el jugador haya entrado y salido de la tienda una vez
/// — igual de "no bloqueante" que el resto de tutoriales del tramo.
///
/// No toca ShopUI ni ShopVendor más allá del evento OnClosed que ya exponen — es un componente
/// aparte, reutilizable para cualquier tienda futura que necesite avisar al grafo, sin acoplar el
/// sistema de tienda a NarrativeGraph.
public class ShopMarketSignal : MonoBehaviour
{
    [Tooltip("La ShopUI a escuchar. Si se deja vacío, se busca una en la escena al activarse.")]
    [SerializeField] private ShopUI shopUI;

    [Tooltip("Clave del evento a disparar en el grafo narrativo al cerrar la tienda (RaiseCustom).")]
    [SerializeField] private string narrativeEventKey;

    [Tooltip("Si true, solo dispara la primera vez que se cierra la tienda en esta sesión. Si " +
             "false, dispara cada vez — inofensivo para un WaitCustomEventNode ya completado, " +
             "pero innecesario salvo que algo más quiera escuchar re-aperturas.")]
    [SerializeField] private bool onlyOnce = true;

    private bool _fired;

    void OnEnable()
    {
        if (shopUI == null) shopUI = FindObjectOfType<ShopUI>();
        if (shopUI != null) shopUI.OnClosed += HandleShopClosed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else Debug.LogWarning($"[ShopMarketSignal:{name}] No se encontró ninguna ShopUI a la que engancharse.");
#endif
    }

    void OnDisable()
    {
        if (shopUI != null) shopUI.OnClosed -= HandleShopClosed;
    }

    private void HandleShopClosed()
    {
        if (onlyOnce && _fired) return;
        if (string.IsNullOrWhiteSpace(narrativeEventKey)) return;

        var signals = DefaultNarrativeSignals.Instance;
        if (signals == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[ShopMarketSignal:{name}] narrativeEventKey='{narrativeEventKey}' pero DefaultNarrativeSignals.Instance es NULL.");
#endif
            return;
        }

        _fired = true;
        signals.RaiseCustom(narrativeEventKey, name);
    }
}
