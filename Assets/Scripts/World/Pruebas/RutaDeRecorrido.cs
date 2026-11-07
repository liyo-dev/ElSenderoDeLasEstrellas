using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un recorrido como lista de puntos (<see cref="PuntoDeRuta"/>, sus hijos en orden): la salida es
/// el primero y la meta el último. Lo usan las pruebas para saber cuánto ha avanzado cada
/// participante (<see cref="Proyectar"/>) y los corredores con guion (<see cref="CorredorDeRuta"/>)
/// para saber por dónde ir. Se retoca moviendo sus puntos; los tramos se ven en la escena.
/// </summary>
public sealed class RutaDeRecorrido : MonoBehaviour
{
    private readonly List<PuntoDeRuta> _puntos = new List<PuntoDeRuta>();
    private float[] _acumulado = new float[0];

    /// <summary>Número de puntos (salida y meta incluidas).</summary>
    public int Cantidad { get { Asegurar(); return _puntos.Count; } }
    /// <summary>Metros de la salida a la meta siguiendo los puntos.</summary>
    public float Longitud { get { Asegurar(); return _acumulado.Length > 0 ? _acumulado[_acumulado.Length - 1] : 0f; } }

    public PuntoDeRuta Punto(int i) { Asegurar(); return _puntos[i]; }
    public Vector3 Posicion(int i) { Asegurar(); return _puntos[i].transform.position; }
    /// <summary>Metros de la salida al punto <paramref name="i"/>.</summary>
    public float DistanciaHasta(int i) { Asegurar(); return _acumulado[Mathf.Clamp(i, 0, _acumulado.Length - 1)]; }

    private void Awake() => Recalcular();

    /// <summary>Vuelve a leer los puntos (si se han añadido o movido).</summary>
    public void Recalcular()
    {
        _puntos.Clear();
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).TryGetComponent(out PuntoDeRuta p)) _puntos.Add(p);
        _acumulado = new float[_puntos.Count];
        for (int i = 1; i < _puntos.Count; i++)
            _acumulado[i] = _acumulado[i - 1] + Vector3.Distance(_puntos[i - 1].transform.position, _puntos[i].transform.position);
    }

    private void Asegurar()
    {
        if (_acumulado.Length != _puntos.Count || _puntos.Count == 0) Recalcular();
    }

    /// <summary>
    /// El punto de la ruta más cercano a <paramref name="posicion"/>, buscado solo entre los tramos
    /// que van del punto <paramref name="desde"/> al <paramref name="hasta"/>. Devuelve la distancia
    /// a la ruta y, en <paramref name="avance"/>, los metros desde la salida hasta ese punto.
    /// </summary>
    public float Proyectar(Vector3 posicion, int desde, int hasta, out float avance)
    {
        Asegurar();
        avance = 0f;
        if (_puntos.Count == 0) return float.MaxValue;
        if (_puntos.Count == 1) { avance = 0f; return Vector3.Distance(posicion, Posicion(0)); }
        desde = Mathf.Clamp(desde, 0, _puntos.Count - 2);
        hasta = Mathf.Clamp(hasta, desde + 1, _puntos.Count - 1);
        float mejor = float.MaxValue;
        for (int i = desde; i < hasta; i++)
        {
            Vector3 a = _puntos[i].transform.position, b = _puntos[i + 1].transform.position;
            Vector3 ab = b - a;
            float largo2 = ab.sqrMagnitude;
            float t = largo2 > 0.0001f ? Mathf.Clamp01(Vector3.Dot(posicion - a, ab) / largo2) : 0f;
            float d = Vector3.Distance(posicion, a + ab * t);
            if (d >= mejor) continue;
            mejor = d;
            avance = _acumulado[i] + Mathf.Sqrt(largo2) * t;
        }
        return mejor;
    }

    /// <summary>Índice del primer punto de control después de <paramref name="i"/> (la meta si no hay más).</summary>
    public int SiguienteControl(int i)
    {
        Asegurar();
        for (int k = i + 1; k < _puntos.Count - 1; k++)
            if (_puntos[k].EsPuntoDeControl) return k;
        return _puntos.Count - 1;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Transform anterior = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            var hijo = transform.GetChild(i);
            if (!hijo.TryGetComponent(out PuntoDeRuta p)) continue;
            Gizmos.color = p.EsPuntoDeControl ? Color.yellow : new Color(1f, 0.5f, 0.8f);
            Gizmos.DrawSphere(hijo.position, 0.25f);
            if (anterior != null)
            {
                Gizmos.color = p.ComoSeLlega == PuntoDeRuta.Llegada.Saltando ? Color.cyan
                             : p.ComoSeLlega == PuntoDeRuta.Llegada.Trepando ? Color.green : new Color(1f, 0.5f, 0.8f);
                Gizmos.DrawLine(anterior.position, hijo.position);
            }
            anterior = hijo;
        }
    }
#endif
}
