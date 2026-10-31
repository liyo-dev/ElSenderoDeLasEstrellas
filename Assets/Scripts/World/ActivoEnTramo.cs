using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enciende unos objetos del mundo solo mientras dura un tramo de la historia (un portal que se
/// queda abierto, un punto que se puede usar, una barrera) y los apaga fuera de él. El tramo se
/// consulta: se revisa al empezar y cada vez que cambia el historial de señales, sin quitarle la
/// señal a quien la espera. Al cargar una partida vale lo ya emitido.
/// </summary>
public class ActivoEnTramo : MonoBehaviour
{
    [Tooltip("Tramo de la historia durante el que los objetos están encendidos.")]
    [SerializeField] private TramoDeHistoria tramo = new();

    [Tooltip("Objetos que se encienden durante el tramo. No incluir este mismo objeto.")]
    [SerializeField] private List<GameObject> objetos = new();

    private bool _hayEstado;
    private bool _encendidos;

    public TramoDeHistoria Tramo => tramo;
    public List<GameObject> Objetos => objetos;

    private void OnEnable()
    {
        DefaultNarrativeSignals.HistoryChanged += Revisar;
        DefaultNarrativeSignals.OnAfterReset += Revisar;
        GameBootService.OnProfileReady += Revisar;
        _hayEstado = false;
        Revisar();
    }

    private void OnDisable()
    {
        DefaultNarrativeSignals.HistoryChanged -= Revisar;
        DefaultNarrativeSignals.OnAfterReset -= Revisar;
        GameBootService.OnProfileReady -= Revisar;
    }

    private void Revisar()
    {
        bool encender = tramo.EnCurso();
        if (_hayEstado && encender == _encendidos) return;
        _hayEstado = true;
        _encendidos = encender;
        foreach (var o in objetos)
            if (o != null && o.activeSelf != encender) o.SetActive(encender);
    }
}
