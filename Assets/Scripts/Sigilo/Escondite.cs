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
    private static readonly List<Escondite> s_activos = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_activos.Clear();
#endif

    // Margen para dar por dentro a quien roza el borde del hueco.
    private const float Margen = 0.15f;

    private Collider _collider;

    /// El jugador está dentro de algún escondite activo. Se mira dónde está ahora, no se cuentan
    /// entradas y salidas del trigger: un collider que se apaga o un jugador movido por una
    /// cinemática no avisan al salir, y el jugador se quedaba «escondido» para siempre (INC-1406).
    public static bool JugadorEscondido
    {
        get
        {
            var jugador = PlayerService.PlayerTransform;
            if (jugador == null) return false;
            Vector3 pecho = jugador.position + Vector3.up * 0.6f;
            for (int i = 0; i < s_activos.Count; i++)
                if (s_activos[i] != null && s_activos[i].Contiene(pecho)) return true;
            return false;
        }
    }

    private bool Contiene(Vector3 punto)
    {
        if (_collider == null || !_collider.enabled) return false;
        if ((_collider.bounds.ClosestPoint(punto) - punto).sqrMagnitude > Margen * Margen) return false;
        // ClosestPoint no vale con mallas cóncavas: ahí basta la caja.
        if (_collider is MeshCollider malla && !malla.convex) return true;
        return (_collider.ClosestPoint(punto) - punto).sqrMagnitude <= Margen * Margen;
    }

    private void Awake() => _collider = GetComponent<Collider>();

    private void OnEnable()
    {
        if (!s_activos.Contains(this)) s_activos.Add(this);
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnDisable() => s_activos.Remove(this);

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
