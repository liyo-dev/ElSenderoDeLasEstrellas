using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de las zonas (trigger) que cambian algo del jugador mientras está dentro: cuenta sus
/// colliders (al cambiar de personaje puede haber más de un cuerpo, y cada uno con varios), avisa
/// una sola vez al entrar y otra al salir, y lo suelta todo al desactivarse. Las zonas concretas
/// solo dicen qué ponen y qué quitan.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class ZonaDelJugador : MonoBehaviour
{
    // Jugadores dentro y cuántos colliders suyos.
    private readonly Dictionary<PlayerActionManager, int> _dentro = new Dictionary<PlayerActionManager, int>();
    private readonly List<PlayerActionManager> _aSoltar = new List<PlayerActionManager>();

    /// <summary>El jugador acaba de entrar (su primer collider).</summary>
    protected abstract void AlEntrar(PlayerActionManager jugador);

    /// <summary>El jugador ha salido del todo, o la zona se ha desactivado con él dentro.</summary>
    protected abstract void AlSalir(PlayerActionManager jugador);

    protected virtual void Reset() => GetComponent<Collider>().isTrigger = true;

    protected void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(GameTags.Player)) return;
        var jugador = other.GetComponentInParent<PlayerActionManager>();
        if (jugador == null) return;

        _dentro.TryGetValue(jugador, out int n);
        _dentro[jugador] = n + 1;
        if (n == 0) AlEntrar(jugador);
    }

    protected void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(GameTags.Player)) return;
        var jugador = other.GetComponentInParent<PlayerActionManager>();
        if (jugador == null || !_dentro.TryGetValue(jugador, out int n)) return;
        if (n > 1) { _dentro[jugador] = n - 1; return; }
        _dentro.Remove(jugador);
        AlSalir(jugador);
    }

    protected virtual void OnDisable()
    {
        _aSoltar.Clear();
        _aSoltar.AddRange(_dentro.Keys);
        _dentro.Clear();
        foreach (var jugador in _aSoltar)
            if (jugador != null) AlSalir(jugador);
        _aSoltar.Clear();
    }
}
