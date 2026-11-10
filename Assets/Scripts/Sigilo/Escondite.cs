using System.Collections.Generic;
using UnityEngine;

/// Sitio donde no se ve al jugador mientras está dentro: un matorral, la hierba alta, la sombra
/// de un carro. Necesita un collider marcado como trigger que cubra el hueco donde cabe el
/// jugador. Quien vigila pregunta <see cref="JugadorEscondido"/> antes de mirar si le ve
/// (ver <see cref="VistaDelVigia"/>).
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Escondite : MonoBehaviour
{
    private static readonly HashSet<Escondite> s_conElJugador = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_conElJugador.Clear();
#endif

    /// El jugador está dentro de algún escondite activo.
    public static bool JugadorEscondido => s_conElJugador.Count > 0;

    // Colliders del jugador dentro: su cuerpo puede tener más de uno.
    private int _dentro;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider otro)
    {
        if (!EsDelJugador(otro)) return;
        _dentro++;
        s_conElJugador.Add(this);
    }

    private void OnTriggerExit(Collider otro)
    {
        if (!EsDelJugador(otro)) return;
        _dentro = Mathf.Max(0, _dentro - 1);
        if (_dentro == 0) s_conElJugador.Remove(this);
    }

    private void OnDisable()
    {
        _dentro = 0;
        s_conElJugador.Remove(this);
    }

    private static bool EsDelJugador(Collider otro)
    {
        var jugador = PlayerService.PlayerTransform;
        return jugador != null && otro != null && otro.transform.IsChildOf(jugador);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
#endif
}
