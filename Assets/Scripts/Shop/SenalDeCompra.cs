using System;
using System.Collections;
using UnityEngine;

/// Avisa al grafo narrativo cuando el jugador compra un objeto concreto (INC-436).
///
/// Nació para la estrella torcida de Tomasa: con la moneda de Eldran, en su puesto solo alcanza
/// para ella, y el capítulo 1 no sigue hasta que Will la compra. ShopMarketSignal avisaba al
/// CERRAR una tienda, comprara lo que comprara; aquí lo que cuenta es la compra de ese objeto.
///
/// Al comprarlo espera a que se cierre la tienda (con un menú abierto los bocadillos se esconden),
/// dice las frases de `reacciones` —el vendedor es este objeto; el jugador, Will— y lanza el
/// evento. Va en el propio NPC vendedor, así que no hay que tocar la escena para montarlo.
public class SenalDeCompra : MonoBehaviour
{
    [Serializable]
    public class Reaccion
    {
        [Tooltip("Marcado: la dice Will. Desmarcado: la dice el vendedor (este objeto).")]
        public bool diceElJugador;
        [Tooltip("Clave de localización de la frase.")]
        public string textKey;
        [Tooltip("Gesto al decirla (opcional, p. ej. 'Laugh01').")]
        public string gesto;
    }

    [Tooltip("El objeto cuya compra se espera.")]
    [SerializeField] private ItemData objeto;

    [Tooltip("Evento del grafo (RaiseCustom) que se lanza cuando el jugador lo ha comprado y han " +
             "terminado las frases.")]
    [SerializeField] private string eventoNarrativo = "ESTRELLA_COMPRADA";

    [Tooltip("Lo que se dice al salir de la tienda después de comprarlo, en orden.")]
    [SerializeField] private Reaccion[] reacciones = Array.Empty<Reaccion>();

    [Tooltip("Segundos por frase. 0 = lo que se tarda en leerla (el bocadillo nunca dura menos).")]
    [SerializeField, Min(0f)] private float segundosPorFrase = 0f;

    private bool _comprado;

    void OnEnable() => ShopController.CompraRealizada += AlComprar;
    void OnDisable() => ShopController.CompraRealizada -= AlComprar;

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
        yield return new WaitForSecondsRealtime(0.35f);

        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo != null && reacciones != null)
        {
            foreach (var r in reacciones)
            {
                if (r == null || string.IsNullOrEmpty(r.textKey)) continue;
                Transform quien = r.diceElJugador
                    ? (PlayerService.Player != null ? PlayerService.Player.transform : null)
                    : transform;
                if (quien == null) continue;

                string texto = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get(r.textKey, r.textKey)
                    : r.textKey;

                bool fin = false;
                bocadillo.Show(quien, texto, Mathf.Max(0.1f, segundosPorFrase), () => fin = true,
                               string.IsNullOrEmpty(r.gesto) ? null : r.gesto);

                // Tope por si otro bocadillo pisa este y su aviso no llega nunca (mismo motivo que
                // el tope de SayBeat).
                float tope = bocadillo.TiempoDeLectura(texto) * 4f + 10f;
                while (!fin && tope > 0f) { tope -= Time.unscaledDeltaTime; yield return null; }
            }
        }

        if (!string.IsNullOrEmpty(eventoNarrativo))
            DefaultNarrativeSignals.Instance?.RaiseCustom(eventoNarrativo, name);
    }
}
