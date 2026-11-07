using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prueba de ir de la salida a la meta de una <see cref="RutaDeRecorrido"/>: una carrera de
/// obstáculos, una escalada (meta arriba) o cualquier recorrido con puntos de control. El jugador
/// tiene que pisar los puntos de control en orden y llegar a la meta; los rivales
/// (<see cref="CorredorDeRuta"/>) corren por guion, cada uno por su ruta, con una «goma» que los
/// frena si van muy por delante y los empuja si se quedan atrás, para que sea reñida y ganable.
///
/// Clasificación por progreso: lo hecho de su ruta por cada uno (de 0 a 1). Con
/// <see cref="hayQueGanar"/>, si un rival llega antes, se falla (y se reintenta: ver
/// <see cref="PruebaDelJugador"/>); sin él, basta con llegar. Si el jugador cae al vacío o se sale
/// del recorrido, reaparece en el último punto de control con un fundido corto.
/// </summary>
public sealed class PruebaDeRecorrido : PruebaDelJugador
{
    [Header("Recorrido")]
    [Tooltip("La ruta del jugador: sus puntos de control y su meta (el último punto).")]
    [SerializeField] private RutaDeRecorrido ruta;
    [Tooltip("Rivales con guion. Cada uno corre por su propia ruta (la misma u otra, p. ej. por el otro lado de una torre).")]
    [SerializeField] private List<CorredorDeRuta> rivales = new List<CorredorDeRuta>();
    [Tooltip("Hay que llegar el primero: si un rival llega antes, se falla. Sin marcar, basta con llegar.")]
    [SerializeField] private bool hayQueGanar = true;
    [Tooltip("Segundos para llegar a la meta (0 = sin límite).")]
    [SerializeField, Min(0f)] private float tiempoLimite;

    [Header("Caídas")]
    [Tooltip("Metros por debajo del tramo en curso a partir de los que se reaparece en el último punto de control.")]
    [SerializeField, Min(1f)] private float caidaMaxima = 6f;
    [Tooltip("Metros de distancia a la ruta a partir de los que se reaparece (salirse del recorrido).")]
    [SerializeField, Min(3f)] private float distanciaMaxima = 25f;
    [SerializeField, Min(0f)] private float fundidoAlReaparecer = 0.25f;

    [Header("Goma de los rivales")]
    [Tooltip("Metros de ventaja (o de retraso) con los que un rival llega al ritmo mínimo (o al máximo).")]
    [SerializeField, Min(1f)] private float metrosDeGoma = 12f;
    [SerializeField, Range(0.2f, 1f)] private float ritmoMinimo = 0.6f;
    [SerializeField, Range(1f, 2f)] private float ritmoMaximo = 1.3f;

    [Header("Avisos")]
    [Tooltip("Al pasar por cada punto de control, el puesto en el rótulo breve.")]
    [SerializeField] private bool avisarDelPuesto = true;
    [Tooltip("Clave del aviso del puesto ({0} = puesto, {1} = participantes).")]
    [SerializeField] private string claveDelPuesto = "PRUEBA_PUESTO";
    [SerializeField] private string textoDelPuesto = "{0}.º de {1}";

    /// <summary>Último punto de control que ha pisado el jugador (0 = la salida).</summary>
    public int UltimoControl { get; private set; }
    /// <summary>Lo que lleva hecho el jugador de su ruta, de 0 a 1.</summary>
    public float ProgresoDelJugador01 { get; private set; }
    public IReadOnlyList<CorredorDeRuta> Rivales => rivales;

    private int _siguienteControl;
    private int _rivalesEnLaMeta;
    private bool _jugadorEnLaMeta;
    private Coroutine _reaparicion;

    protected override void Awake()
    {
        base.Awake();
        if (salidaDelJugador == null && ruta != null && ruta.Cantidad > 0) salidaDelJugador = ruta.Punto(0).transform;
        foreach (var r in rivales)
            if (r != null) r.Llego += AlLlegarUnRival;
    }

    private void OnDestroy()
    {
        foreach (var r in rivales)
            if (r != null) r.Llego -= AlLlegarUnRival;
    }

    /// <summary>Puesto del jugador ahora (1 = primero), por lo hecho de su ruta.</summary>
    public int PuestoDelJugador()
    {
        int puesto = 1;
        foreach (var r in rivales)
            if (r != null && (r.HaLlegado || r.Progreso01 > ProgresoDelJugador01)) puesto++;
        return puesto;
    }

    protected override void PrepararIntento()
    {
        UltimoControl = 0;
        _siguienteControl = ruta != null ? ruta.SiguienteControl(0) : 0;
        ProgresoDelJugador01 = 0f;
        _rivalesEnLaMeta = 0;
        _jugadorEnLaMeta = false;
        foreach (var r in rivales)
            if (r != null) r.ColocarEnLaSalida();
    }

    protected override void Arrancar()
    {
        foreach (var r in rivales)
            if (r != null) r.Correr();
    }

    protected override void Detener()
    {
        if (_reaparicion != null) { StopCoroutine(_reaparicion); _reaparicion = null; }
        foreach (var r in rivales)
            if (r != null) r.Parar();
    }

    protected override void Actualizar(float dt)
    {
        var jugador = Jugador;
        if (jugador == null || ruta == null || ruta.Cantidad < 2) return;
        if (tiempoLimite > 0f && TiempoEnCurso >= tiempoLimite) { Fallar(); return; }
        if (_reaparicion != null) return;

        Vector3 p = jugador.position;
        if (ruta.Punto(_siguienteControl).Contiene(p))
        {
            UltimoControl = _siguienteControl;
            if (UltimoControl >= ruta.Cantidad - 1) { LlegaElJugador(); return; }
            _siguienteControl = ruta.SiguienteControl(UltimoControl);
            if (avisarDelPuesto)
                HudToastService.Instance?.ShowRaw(string.Format(PresentadorDeLinea.Texto(claveDelPuesto, textoDelPuesto),
                    PuestoDelJugador(), rivales.Count + 1), 1.5f);
        }

        float distancia = ruta.Proyectar(p, UltimoControl, _siguienteControl, out float avance);
        float total = ruta.Longitud;
        ProgresoDelJugador01 = total > 0f ? Mathf.Clamp01(avance / total) : 0f;

        if (p.y < AlturaMinimaDelTramo() - caidaMaxima || distancia > distanciaMaxima)
        {
            _reaparicion = StartCoroutine(Co_ReaparecerEnElControl());
            return;
        }

        AjustarLaGoma(total);
    }

    private float AlturaMinimaDelTramo()
    {
        float minima = float.MaxValue;
        for (int i = UltimoControl; i <= _siguienteControl; i++) minima = Mathf.Min(minima, ruta.Posicion(i).y);
        return minima;
    }

    private void AjustarLaGoma(float total)
    {
        for (int i = 0; i < rivales.Count; i++)
        {
            var r = rivales[i];
            if (r == null || !r.Corriendo) continue;
            float ventaja = (r.Progreso01 - ProgresoDelJugador01) * total;
            r.Ritmo = ventaja > 0f
                ? Mathf.Lerp(1f, ritmoMinimo, ventaja / metrosDeGoma)
                : Mathf.Lerp(1f, ritmoMaximo, -ventaja / metrosDeGoma);
        }
    }

    private IEnumerator Co_ReaparecerEnElControl()
    {
        yield return Co_Reaparecer(ruta.Punto(UltimoControl).transform, fundidoAlReaparecer);
        _reaparicion = null;
    }

    private void LlegaElJugador()
    {
        _jugadorEnLaMeta = true;
        ProgresoDelJugador01 = 1f;
        if (!hayQueGanar || _rivalesEnLaMeta == 0) Ganar();
        else Fallar();
    }

    private void AlLlegarUnRival(CorredorDeRuta rival)
    {
        if (!EnCurso || _jugadorEnLaMeta) return;
        _rivalesEnLaMeta++;
        if (hayQueGanar) Fallar();
    }
}
