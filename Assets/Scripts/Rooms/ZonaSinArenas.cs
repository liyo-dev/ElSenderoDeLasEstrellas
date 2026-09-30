using System.Collections.Generic;
using UnityEngine;

/// Marca una zona en la que no puede entrar ninguna arena en modo radio (BossArenaController).
/// Si al empezar una batalla el círculo de la arena invade la zona, el centro se desplaza lo
/// mínimo para que el borde quede fuera, sin cambiar el radio.
///
/// Va en cualquier objeto con collider (caja, esfera, cápsula o malla convexa). Lo normal es
/// ponerlo en el mismo objeto que la AmbientZone de un pueblo, para que ninguna batalla se libre
/// dentro de él; no depende de AmbientZone ni la modifica.
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class ZonaSinArenas : MonoBehaviour
{
    // Pasadas como mucho al apartar la arena de varias zonas: al salir de una puede invadir otra.
    private const int MaxPasadas = 4;

    private static readonly List<ZonaSinArenas> s_activas = new();

    private Collider _collider;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_activas.Clear();
#endif

    private void Awake() => _collider = GetComponent<Collider>();

    private void OnEnable()
    {
        if (!s_activas.Contains(this)) s_activas.Add(this);
    }

    private void OnDisable() => s_activas.Remove(this);

    /// Aparta un círculo horizontal (centro, radio) de todas las zonas activas. Devuelve true si
    /// ha tenido que moverlo; <paramref name="centroFinal"/> conserva la altura del centro original.
    public static bool ApartarDeTodas(Vector3 centro, float radio, float margen, out Vector3 centroFinal)
    {
        centroFinal = centro;
        bool desplazado = false;

        for (int pasada = 0; pasada < MaxPasadas; pasada++)
        {
            bool movidoEnEstaPasada = false;
            for (int i = 0; i < s_activas.Count; i++)
            {
                if (s_activas[i].Apartar(centroFinal, radio, margen, out Vector3 apartado))
                {
                    centroFinal = apartado;
                    movidoEnEstaPasada = true;
                    desplazado = true;
                }
            }
            if (!movidoEnEstaPasada) return desplazado;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[ZonaSinArenas] No hay hueco para una arena de radio {radio} m cerca de {centro} sin invadir alguna zona. Se usa la última posición calculada.");
#endif
        return desplazado;
    }

    /// Si el círculo (centro, radio) invade esta zona, devuelve true y el centro desplazado lo
    /// justo para que el círculo quede fuera, a <paramref name="margen"/> metros del borde.
    public bool Apartar(Vector3 centro, float radio, float margen, out Vector3 centroApartado)
    {
        centroApartado = centro;
        if (_collider == null || !_collider.enabled) return false;

        // Todo se mide en horizontal, a la altura media de la zona.
        Bounds b = _collider.bounds;
        Vector3 q = new Vector3(centro.x, b.center.y, centro.z);
        float necesario = radio + margen;

        Vector3 borde = PuntoMasCercano(q);
        Vector3 fuera = q - borde;
        fuera.y = 0f;
        float distancia = fuera.magnitude;
        if (distancia >= necesario) return false;

        Vector3 direccion;
        if (distancia > 0.001f)
        {
            // Centro fuera de la zona pero demasiado cerca: se aleja desde el punto más cercano.
            direccion = fuera / distancia;
        }
        else
        {
            // Centro dentro de la zona: sale en línea recta desde el centro de la zona.
            direccion = q - b.center;
            direccion.y = 0f;
            if (direccion.sqrMagnitude < 0.0001f) direccion = Vector3.forward;
            direccion.Normalize();

            float lejos = b.extents.magnitude * 2f + 1f;
            borde = _collider.Raycast(new Ray(q + direccion * lejos, -direccion), out RaycastHit hit, lejos)
                ? hit.point
                : q + direccion * b.extents.magnitude;
        }

        Vector3 nuevo = borde + direccion * necesario;
        centroApartado = new Vector3(nuevo.x, centro.y, nuevo.z);
        return true;
    }

    private Vector3 PuntoMasCercano(Vector3 punto)
    {
        // Collider.ClosestPoint no admite mallas cóncavas: con ellas se usa la caja que las envuelve.
        if (_collider is MeshCollider malla && !malla.convex)
            return _collider.bounds.ClosestPoint(punto);
        return _collider.ClosestPoint(punto);
    }
}
