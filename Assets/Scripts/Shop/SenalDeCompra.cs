using System.Collections;
using UnityEngine;

/// Avisa al grafo narrativo cuando el jugador compra un objeto concreto (INC-436).
///
/// ShopMarketSignal avisa al cerrar una tienda, se compre lo que se compre; aquí cuenta la compra
/// de ese objeto. Al comprarlo espera a que se cierre la tienda y lanza el evento. Lo que se diga
/// después va en la cinemática que el grafo lanza con ese evento (modo cine), no aquí. Va en el
/// propio NPC vendedor, así que no hay que tocar la escena para montarlo. Ver INC-1145.
public class SenalDeCompra : MonoBehaviour
{
    [Tooltip("El objeto cuya compra se espera.")]
    [SerializeField] private ItemData objeto;

    [Tooltip("Evento del grafo (RaiseCustom) que se lanza cuando el jugador lo ha comprado y se ha " +
             "cerrado la tienda.")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Emite)]
    [SerializeField] private string eventoNarrativo = "ESTRELLA_COMPRADA";

    private bool _comprado;
    private bool _eventoEmitido;

    void OnEnable() => ShopController.CompraRealizada += AlComprar;

    // Si el vendedor desaparece antes de que se cierre la tienda (cambio de escena, carga, roster),
    // se avisa igualmente al grafo para que el capítulo no espere una compra que ya se hizo.
    void OnDisable()
    {
        ShopController.CompraRealizada -= AlComprar;
        if (_comprado && !_eventoEmitido) Emitir();
    }

    private void AlComprar(ShopController tienda, ItemData item)
    {
        if (_comprado || item == null || objeto == null) return;
        if (item != objeto && item.itemId != objeto.itemId) return;
        _comprado = true;
        StartCoroutine(Co_TrasLaCompra());
    }

    private IEnumerator Co_TrasLaCompra()
    {
        while (MenuManager.AnyOpen()) yield return null;
        if (!_eventoEmitido) Emitir();
    }

    private void Emitir()
    {
        _eventoEmitido = true;
        var senales = DefaultNarrativeSignals.Instance;
        if (!string.IsNullOrEmpty(eventoNarrativo) && senales != null)
            senales.RaiseCustom(eventoNarrativo, name);
    }
}
