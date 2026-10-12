using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona (trigger) donde ciertas habilidades no se pueden usar mientras el jugador está dentro.
/// Uso principal: zona sin vuelo en las pruebas de plataformeo (si entra volando, el vuelo se
/// corta y cae). Usa los bloqueos externos de <see cref="PlayerActionManager"/>, así que no
/// toca los permisos del preset y se libera sola al salir o al desactivarse.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class ZonaQueBloqueaHabilidades : MonoBehaviour
{
    [SerializeField] private PlayerAbility[] habilidadesBloqueadas = { PlayerAbility.Fly };
    [Tooltip("Aviso al entrar si se estaba usando una habilidad bloqueada (p. ej. volando).")]
    [SerializeField] private string claveDeAviso = "ZONA_SIN_VUELO_AVISO";
    [SerializeField, Min(0.5f)] private float duracionDelAviso = 3f;

    // Jugadores dentro (al cambiar de personaje puede haber más de uno) y cuántos colliders suyos.
    private readonly Dictionary<PlayerActionManager, int> _dentro = new Dictionary<PlayerActionManager, int>();

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(GameTags.Player)) return;
        var acciones = other.GetComponentInParent<PlayerActionManager>();
        if (acciones == null) return;

        _dentro.TryGetValue(acciones, out int n);
        _dentro[acciones] = n + 1;
        if (n > 0) return;

        for (int i = 0; i < habilidadesBloqueadas.Length; i++)
            acciones.AnadirBloqueo(habilidadesBloqueadas[i], this);

        if (System.Array.IndexOf(habilidadesBloqueadas, PlayerAbility.Fly) >= 0)
        {
            var vuelo = acciones.GetComponentInChildren<PlayerFlyingController>();
            if (vuelo != null && vuelo.IsFlying)
            {
                vuelo.TerminarVuelo();
                if (!string.IsNullOrEmpty(claveDeAviso))
                    HudToastService.Instance?.Show(claveDeAviso, duracionDelAviso);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(GameTags.Player)) return;
        var acciones = other.GetComponentInParent<PlayerActionManager>();
        if (acciones == null || !_dentro.TryGetValue(acciones, out int n)) return;
        if (n > 1) { _dentro[acciones] = n - 1; return; }
        _dentro.Remove(acciones);
        Liberar(acciones);
    }

    private void OnDisable()
    {
        foreach (var acciones in _dentro.Keys)
            if (acciones != null) Liberar(acciones);
        _dentro.Clear();
    }

    private void Liberar(PlayerActionManager acciones)
    {
        for (int i = 0; i < habilidadesBloqueadas.Length; i++)
            acciones.QuitarBloqueo(habilidadesBloqueadas[i], this);
    }
}
