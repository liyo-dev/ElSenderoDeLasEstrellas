using Kits.WorldWrap;
using UnityEngine;

/// El mundo cíclico de El Sendero: vigila al jugador que va al mando y, si va en barco, mueve el
/// barco con él. Los compañeros que le siguen volando o nadando van colocados respecto a él, así
/// que viajan con él solos. Ver INC-1197
[RequireComponent(typeof(WorldWrapper))]
public class MundoCiclicoDelJuego : MonoBehaviour
{
    private WorldWrapper _mundo;

    private void Awake() => _mundo = GetComponent<WorldWrapper>();

    private void OnEnable()
    {
        _mundo.TargetProvider = Jugador;
        _mundo.MoverResolver = LoQueLeLleva;
    }

    private void OnDisable()
    {
        _mundo.TargetProvider = null;
        _mundo.MoverResolver = null;
    }

    private static Transform Jugador()
        => PlayerService.TryGetPlayer(out var jugador, allowSceneLookup: false) && jugador != null ? jugador.transform : null;

    private static Transform LoQueLeLleva(Transform jugador)
    {
        var barco = jugador.GetComponentInParent<BarcoNavegable>();
        return barco != null ? barco.transform : jugador;
    }
}
