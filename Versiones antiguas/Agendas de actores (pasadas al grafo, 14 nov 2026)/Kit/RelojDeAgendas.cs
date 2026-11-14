using UnityEngine;

/// <summary>
/// Pide a <see cref="AgendaDeActores"/> que revise las agendas una vez por segundo. Lo crea
/// <see cref="AgendaDeActores"/> al arrancar el juego; no se pone a mano en ninguna escena.
/// </summary>
public sealed class RelojDeAgendas : MonoBehaviour
{
    const float Intervalo = 1f;
    float _falta;

    void Update()
    {
        _falta -= Time.unscaledDeltaTime;
        if (_falta > 0f) return;
        _falta = Intervalo;
        AgendaDeActores.Revisar();
    }
}
