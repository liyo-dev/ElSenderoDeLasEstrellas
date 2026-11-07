using System;
using UnityEngine;

/// <summary>
/// Un rival con guion que recorre una <see cref="RutaDeRecorrido"/>: corre por el suelo, salta en
/// parábola y trepa según cómo se llega a cada punto, sin NavMesh ni física (sirve en plataformas
/// sueltas y en paredes). La prueba que lo usa le cambia el <see cref="Ritmo"/> sobre la marcha
/// (la «goma» que mantiene la carrera reñida). Lo que se ve lo cuenta a un
/// <see cref="IAnimacionDeCorredor"/> de su mismo objeto. Sus colliders conviene que sean
/// triggers: no empuja a nadie.
/// </summary>
public sealed class CorredorDeRuta : MonoBehaviour
{
    private const float Gravedad = 18f;

    [SerializeField] private RutaDeRecorrido ruta;
    [Tooltip("Metros por segundo corriendo, con ritmo 1.")]
    [SerializeField, Min(0.5f)] private float velocidad = 4.5f;
    [Tooltip("Metros por segundo trepando, con ritmo 1.")]
    [SerializeField, Min(0.2f)] private float velocidadAlTrepar = 1.8f;
    [Tooltip("Multiplica las dos velocidades: rivales más o menos buenos.")]
    [SerializeField, Range(0.5f, 1.5f)] private float habilidad = 1f;
    [Tooltip("Metros a la derecha (+) o a la izquierda (−) de la ruta por los que corre.")]
    [SerializeField] private float carril;
    [Tooltip("Metros por detrás de la salida en los que espera la salida.")]
    [SerializeField, Min(0f)] private float filaEnLaSalida;
    [Tooltip("Al correr, se ajusta a la altura del suelo que tenga debajo.")]
    [SerializeField] private bool pegarseAlSuelo = true;
    [SerializeField, Min(30f)] private float giroPorSegundo = 720f;

    /// <summary>Multiplica la velocidad (lo pone la prueba: más de 1 si va detrás, menos si va delante).</summary>
    public float Ritmo { get; set; } = 1f;
    public bool Corriendo { get; private set; }
    public bool HaLlegado { get; private set; }
    /// <summary>Lo que lleva hecho de su ruta, de 0 a 1.</summary>
    public float Progreso01 { get; private set; }
    public RutaDeRecorrido Ruta => ruta;

    /// <summary>Ha llegado a la meta.</summary>
    public event Action<CorredorDeRuta> Llego;

    private IAnimacionDeCorredor _animacion;
    private int _tramo;
    private float _enTramo;
    private float _duracionDelSalto;
    private float _alturaExtra;
    private Vector3 _desvio;
    private Vector3 _mirando = Vector3.forward;
    private int _mascaraDeSuelo;

    private void Awake()
    {
        _animacion = GetComponent<IAnimacionDeCorredor>();
        int capaDelJugador = LayerMask.NameToLayer("Player");
        _mascaraDeSuelo = capaDelJugador >= 0 ? ~(1 << capaDelJugador) : ~0;
    }

    /// <summary>Lo deja quieto en su sitio de la salida.</summary>
    public void ColocarEnLaSalida()
    {
        Corriendo = false;
        HaLlegado = false;
        Ritmo = 1f;
        _tramo = 0;
        _enTramo = 0f;
        Progreso01 = 0f;
        if (ruta == null || ruta.Cantidad < 2) return;
        Vector3 a = ruta.Posicion(0), b = ruta.Posicion(1);
        _mirando = Horizontal(b - a, transform.forward);
        _desvio = Derecha(_mirando) * carril;
        transform.SetPositionAndRotation(a + _desvio - _mirando * filaEnLaSalida, Quaternion.LookRotation(_mirando));
        _animacion?.Trepar(false);
        _animacion?.Avanzar(0f);
    }

    /// <summary>Sale.</summary>
    public void Correr()
    {
        if (ruta == null || ruta.Cantidad < 2 || HaLlegado) return;
        Corriendo = true;
        EmpezarTramo();
    }

    /// <summary>Se para donde esté.</summary>
    public void Parar()
    {
        Corriendo = false;
        _animacion?.Trepar(false);
        _animacion?.Avanzar(0f);
    }

    private void Update()
    {
        if (!Corriendo) return;
        float dt = Time.deltaTime;
        Vector3 a = ruta.Posicion(_tramo), b = ruta.Posicion(_tramo + 1);
        var llegada = ruta.Punto(_tramo + 1).ComoSeLlega;
        float largo = Vector3.Distance(a, b);
        float t;
        Vector3 pos;

        switch (llegada)
        {
            case PuntoDeRuta.Llegada.Saltando:
                _enTramo += dt * Mathf.Max(0.5f, Ritmo);
                t = _duracionDelSalto > 0f ? Mathf.Clamp01(_enTramo / _duracionDelSalto) : 1f;
                pos = Vector3.Lerp(a, b, t) + Vector3.up * (_alturaExtra * 4f * t * (1f - t));
                AcercarDesvio(dt, Derecha(_mirando) * carril);
                pos += _desvio;
                break;

            case PuntoDeRuta.Llegada.Trepando:
                float subida = velocidadAlTrepar * habilidad * Ritmo;
                _enTramo += subida * dt;
                t = largo > 0.01f ? Mathf.Clamp01(_enTramo / largo) : 1f;
                AcercarDesvio(dt, Vector3.zero);
                pos = Vector3.Lerp(a, b, t) + _desvio;
                break;

            default:
                float rapidez = velocidad * habilidad * Ritmo;
                _enTramo += rapidez * dt;
                t = largo > 0.01f ? Mathf.Clamp01(_enTramo / largo) : 1f;
                _mirando = Horizontal(b - a, _mirando);
                AcercarDesvio(dt, Derecha(_mirando) * carril);
                pos = Vector3.Lerp(a, b, t) + _desvio;
                if (pegarseAlSuelo && Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out RaycastHit golpe, 2.4f,
                        _mascaraDeSuelo, QueryTriggerInteraction.Ignore))
                    pos.y = golpe.point.y;
                _animacion?.Avanzar(rapidez);
                break;
        }

        transform.position = pos;
        if (llegada != PuntoDeRuta.Llegada.Trepando)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_mirando), giroPorSegundo * dt);

        float total = ruta.Longitud;
        Progreso01 = total > 0f ? Mathf.Clamp01((ruta.DistanciaHasta(_tramo) + largo * t) / total) : 1f;
        if (t < 1f) return;

        _tramo++;
        if (_tramo >= ruta.Cantidad - 1) { Llegar(); return; }
        EmpezarTramo();
    }

    private void EmpezarTramo()
    {
        _enTramo = 0f;
        var desde = ruta.Punto(_tramo);
        var hasta = ruta.Punto(_tramo + 1);
        Vector3 a = desde.transform.position, b = hasta.transform.position;

        _animacion?.Trepar(hasta.ComoSeLlega == PuntoDeRuta.Llegada.Trepando);
        if (hasta.ComoSeLlega != PuntoDeRuta.Llegada.Saltando) return;

        // Parábola que pasa por encima del más alto de los dos puntos, con la gravedad del corredor.
        _mirando = Horizontal(b - a, _mirando);
        float cima = Mathf.Max(a.y, b.y) + hasta.AlturaDelSalto;
        _alturaExtra = cima - (a.y + b.y) * 0.5f;
        _duracionDelSalto = Mathf.Sqrt(2f * (cima - a.y) / Gravedad) + Mathf.Sqrt(2f * (cima - b.y) / Gravedad);
        _animacion?.Avanzar(0f);
        _animacion?.Saltar();
        if (desde.Trampolin != null) desde.Trampolin.Rebotar();
    }

    private void Llegar()
    {
        Corriendo = false;
        HaLlegado = true;
        Progreso01 = 1f;
        _animacion?.Trepar(false);
        _animacion?.Avanzar(0f);
        Llego?.Invoke(this);
    }

    private void AcercarDesvio(float dt, Vector3 objetivo)
        => _desvio = Vector3.MoveTowards(_desvio, objetivo, 3f * dt);

    private static Vector3 Horizontal(Vector3 v, Vector3 siNoHay)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : siNoHay;
    }

    private static Vector3 Derecha(Vector3 adelante) => Vector3.Cross(Vector3.up, adelante).normalized;
}
