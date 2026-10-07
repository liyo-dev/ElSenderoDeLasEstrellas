using UnityEngine;

/// Portal de los laboratorios: al tocarlo el jugador, hace lo suyo (Cruzar). Cada tipo de portal
/// es una subclase: saltar a otro punto del laboratorio, empezar un jefe o cargar otra escena.
[RequireComponent(typeof(Collider))]
public abstract class PortalDelLab : MonoBehaviour
{
    private const float Espera = 2f;
    private float _listoDesde;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < _listoDesde) return;
        var jugador = PlayerService.Player;
        if (jugador == null || !other.transform.IsChildOf(jugador.transform)) return;
        _listoDesde = Time.time + Espera;
        Cruzar(jugador);
    }

    protected abstract void Cruzar(GameObject jugador);
}
