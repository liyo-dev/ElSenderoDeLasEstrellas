using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un NPC que colecciona objetos raros (Oliver). Al terminar de hablar con él se queda con las
/// piezas de su colección que lleve el jugador, da los premios de las páginas completas y abre el
/// álbum; si no le traes nada, pregunta si quieres verlo. Se engancha al final de la charla normal
/// del NPC (<see cref="Interactable.OnFinished"/>), igual que una tienda.
/// </summary>
[RequireComponent(typeof(Interactable))]
public sealed class Coleccionista : MonoBehaviour
{
    [SerializeField] private ColeccionDeObjetos coleccion;
    [SerializeField] private EstiloDeMenuSencillo estilo;
    [SerializeField] private string preguntaId = "COLECCION_PREGUNTA_VER";
    [SerializeField] private string pregunta = "¿Quieres ver la colección?";

    // Tras cerrar el diálogo, para que el botón que lo cerró no conteste también la pregunta.
    private const float EsperaTrasLaCharla = 0.25f;

    private Interactable _interactable;
    private Coroutine _atendiendo;

    private void Awake() => _interactable = GetComponent<Interactable>();

    private void OnEnable() => _interactable.OnFinished.AddListener(Atender);

    private void OnDisable()
    {
        _interactable.OnFinished.RemoveListener(Atender);
        if (_atendiendo != null) { StopCoroutine(_atendiendo); _atendiendo = null; }
    }

    public void Atender()
    {
        if (coleccion == null || _atendiendo != null) return;
        _atendiendo = StartCoroutine(Co_Atender());
    }

    private IEnumerator Co_Atender()
    {
        yield return new WaitForSecondsRealtime(EsperaTrasLaCharla);
        _atendiendo = null;

        var nuevas = RecogerPiezas();
        DarPremios();
        if (nuevas.Count > 0)
        {
            ColeccionUI.Abrir(coleccion, estilo, nuevas);
            yield break;
        }

        var popup = ConfirmationPopupUI.Instance;
        if (popup == null) yield break;
        popup.Show(ColeccionDeObjetos.Texto(preguntaId, pregunta),
                   () => ColeccionUI.Abrir(coleccion, estilo, nuevas), null, selectConfirm: true);
    }

    /// Se queda con las piezas que aún no tiene y que el jugador lleva encima.
    private List<PiezaDeColeccion> RecogerPiezas()
    {
        var nuevas = new List<PiezaDeColeccion>();
        if (!PlayerService.TryGetComponent(out Inventory inventario, includeInactive: true, allowSceneLookup: true)) return nuevas;

        foreach (var pagina in coleccion.paginas)
        {
            if (pagina?.piezas == null) continue;
            foreach (var pieza in pagina.piezas)
            {
                if (pieza?.objeto == null || coleccion.Tiene(pieza) || !inventario.HasItem(pieza.objeto)) continue;
                if (!inventario.TryConsume(pieza.objeto, 1)) continue;
                FlagDePartida.Marcar(coleccion.ClaveDePieza(pieza.objeto));
                nuevas.Add(pieza);
            }
        }
        return nuevas;
    }

    /// Da, una sola vez, el premio de cada página completa.
    private void DarPremios()
    {
        if (!PlayerService.TryGetComponent(out Inventory inventario, includeInactive: true, allowSceneLookup: true)) return;
        for (int i = 0; i < coleccion.paginas.Count; i++)
        {
            var pagina = coleccion.paginas[i];
            if (pagina?.premio == null || !coleccion.PaginaCompleta(i)) continue;
            if (!FlagDePartida.Marcar(coleccion.ClaveDePremio(i))) continue; // ya dado
            inventario.Add(pagina.premio, pagina.cantidadDelPremio);
        }
    }
}
