using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto de escena que persigue al jugador a saltitos durante un rato y acaba rindiéndose:
/// cuando se le esquiva el tiempo suficiente o cuando el jugador se sube a algo alto (una silla,
/// una mesa). No hace daño: si alcanza al jugador, rebota hacia atrás y lo vuelve a intentar.
/// Pensado para gags de interior (el plato que confunde a Will con una mancha, INC-725).
///
/// Avisa al grafo con señales opcionales (empieza, toca, se rinde) y por UnityEvent a la escena.
/// Se mueve sin física ni NavMesh: sigue el suelo con un rayo corto que no alcanza a subir a
/// muebles, así que el jugador subido queda fuera de su alcance.
/// </summary>
[DisallowMultipleComponent]
public sealed class PropQuePersigue : MonoBehaviour
{
    public enum Estado { Quieto, Persiguiendo, Rendido }

    [Header("Arranque")]
    [Tooltip("Empieza solo cuando el jugador está a esta distancia (0 = solo con Empezar() o con la señal).")]
    [SerializeField, Min(0f)] private float distanciaDeArranque = 3f;
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
    [Tooltip("Señal del grafo que lo hace empezar (vacía = ninguna).")]
    [SerializeField] private string senalDeArranque;

    [Header("Persecución")]
    [Tooltip("Velocidad en m/s. Por debajo de la carrera del jugador para que se pueda esquivar.")]
    [SerializeField, Min(0.1f)] private float velocidad = 2.4f;
    [SerializeField, Min(0f)] private float alturaDelSaltito = 0.22f;
    [SerializeField, Min(0.1f)] private float saltitosPorSegundo = 3f;
    [Tooltip("Grados por segundo al girar hacia el jugador.")]
    [SerializeField, Min(10f)] private float giro = 540f;
    [Tooltip("Distancia en planta a la que cuenta como que ha alcanzado al jugador.")]
    [SerializeField, Min(0.1f)] private float distanciaDeToque = 0.6f;
    [Tooltip("Metros que retrocede tras alcanzarle.")]
    [SerializeField, Min(0f)] private float retrocesoAlTocar = 1.2f;
    [SerializeField, Min(0f)] private float pausaTrasTocar = 0.8f;

    [Header("Rendirse")]
    [Tooltip("Segundos persiguiendo antes de rendirse aunque no le alcance.")]
    [SerializeField, Min(1f)] private float duracionMaxima = 25f;
    [Tooltip("Altura del jugador sobre el suelo del objeto a partir de la cual queda fuera de su alcance.")]
    [SerializeField, Min(0.1f)] private float alturaFueraDeAlcance = 0.4f;
    [Tooltip("Segundos esperando debajo del jugador subido antes de rendirse.")]
    [SerializeField, Min(0f)] private float esperaDebajo = 1.5f;

    [Header("Sonido (claves de AudioGraphProfile)")]
    [SerializeField] private string sfxSaltito;
    [SerializeField] private string sfxToque;
    [SerializeField] private string sfxRendirse;

    [Header("Señales al grafo (vacías = ninguna)")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Emite)]
    [SerializeField] private string senalAlEmpezar;
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Emite)]
    [SerializeField] private string senalAlTocar;
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Emite)]
    [SerializeField] private string senalAlRendirse;

    public UnityEvent alEmpezar;
    public UnityEvent alTocar;
    public UnityEvent alRendirse;

    private const float RayoArriba = 0.3f;
    private const float RayoLargo = 1.2f;
    private const float IntervaloDeCercania = 0.2f;

    private readonly RaycastHit[] _impactos = new RaycastHit[6];
    private Estado _estado;
    private Transform _jugador;
    private Vector3 _posicionInicial;
    private Quaternion _giroInicial;
    private float _suelo;
    private float _tiempo;
    private float _tiempoDebajo;
    private float _pausa;
    private float _proximaComprobacion;
    private Vector3 _retroceso;
    private bool _escuchandoSenal;
    private int _saltoAnterior;

    public Estado EstadoActual => _estado;

    private void Awake()
    {
        _posicionInicial = transform.position;
        _giroInicial = transform.rotation;
        _suelo = _posicionInicial.y;
    }

    private void OnEnable()
    {
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        _jugador = PlayerService.Player != null ? PlayerService.Player.transform : null;
        if (!string.IsNullOrEmpty(senalDeArranque) && DefaultNarrativeSignals.Instance != null)
        {
            DefaultNarrativeSignals.Instance.OnCustom(senalDeArranque, Empezar);
            _escuchandoSenal = true;
        }
    }

    private void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        if (_escuchandoSenal && DefaultNarrativeSignals.Instance != null)
            DefaultNarrativeSignals.Instance.OffCustom(senalDeArranque, Empezar);
        _escuchandoSenal = false;
    }

    private void AlRegistrarJugador(GameObject jugador) => _jugador = jugador != null ? jugador.transform : null;

    /// <summary>Empieza a perseguir (si no lo está haciendo ya ni se ha rendido).</summary>
    public void Empezar()
    {
        if (_estado != Estado.Quieto) return;
        _estado = Estado.Persiguiendo;
        _tiempo = 0f; _tiempoDebajo = 0f; _pausa = 0f;
        _suelo = BuscarSuelo(transform.position, _suelo);
        alEmpezar?.Invoke();
        Senal(senalAlEmpezar);
    }

    /// <summary>Deja de perseguir y se posa en el suelo.</summary>
    public void Rendirse()
    {
        if (_estado == Estado.Rendido) return;
        _estado = Estado.Rendido;
        var p = transform.position;
        transform.position = new Vector3(p.x, _suelo, p.z);
        Sonar(sfxRendirse);
        alRendirse?.Invoke();
        Senal(senalAlRendirse);
    }

    /// <summary>Vuelve a su sitio, quieto, para repetir el gag (pruebas).</summary>
    public void Reiniciar()
    {
        _estado = Estado.Quieto;
        transform.SetPositionAndRotation(_posicionInicial, _giroInicial);
        _suelo = _posicionInicial.y;
    }

    private void Update()
    {
        if (_estado == Estado.Quieto)
        {
            if (distanciaDeArranque > 0f && _jugador != null && Time.time >= _proximaComprobacion)
            {
                _proximaComprobacion = Time.time + IntervaloDeCercania;
                if (DistanciaEnPlanta(_jugador.position) <= distanciaDeArranque) Empezar();
            }
            return;
        }
        if (_estado != Estado.Persiguiendo || _jugador == null) return;

        float dt = Time.deltaTime;
        _tiempo += dt;
        if (_tiempo >= duracionMaxima) { Rendirse(); return; }

        Vector3 pos = transform.position;
        Vector3 objetivo = _jugador.position;
        Vector3 hacia = objetivo - pos; hacia.y = 0f;
        float distancia = hacia.magnitude;
        Vector3 direccion = distancia > 0.001f ? hacia / distancia : transform.forward;

        bool fueraDeAlcance = objetivo.y - _suelo > alturaFueraDeAlcance;
        Vector3 siguiente = pos;
        if (_pausa > 0f)
        {
            _pausa -= dt;
            siguiente = Vector3.MoveTowards(pos, _retroceso, retrocesoAlTocar / Mathf.Max(0.05f, pausaTrasTocar) * dt);
        }
        else if (fueraDeAlcance)
        {
            // Espera debajo del jugador subido, dando vueltas hasta que se cansa.
            if (distancia > distanciaDeToque) siguiente = pos + direccion * Mathf.Min(velocidad * dt, distancia - distanciaDeToque);
            _tiempoDebajo += dt;
            if (_tiempoDebajo >= esperaDebajo) { Rendirse(); return; }
        }
        else
        {
            _tiempoDebajo = 0f;
            if (distancia <= distanciaDeToque) { Tocar(pos, direccion); return; }
            siguiente = pos + direccion * Mathf.Min(velocidad * dt, distancia);
        }

        _suelo = BuscarSuelo(siguiente, _suelo);
        float fase = _tiempo * saltitosPorSegundo * Mathf.PI;
        float saltito = Mathf.Abs(Mathf.Sin(fase)) * alturaDelSaltito;
        int saltoActual = Mathf.FloorToInt(fase / Mathf.PI);
        if (saltoActual != _saltoAnterior) { _saltoAnterior = saltoActual; Sonar(sfxSaltito); }
        transform.position = new Vector3(siguiente.x, _suelo + saltito, siguiente.z);

        var mirar = Quaternion.LookRotation(direccion, Vector3.up);
        // Bamboleo de lado a lado al saltar: se lee como algo torpe y vivo.
        mirar *= Quaternion.Euler(0f, 0f, Mathf.Sin(fase) * 12f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, mirar, giro * dt);
    }

    private void Tocar(Vector3 pos, Vector3 direccion)
    {
        _pausa = pausaTrasTocar;
        _retroceso = pos - direccion * retrocesoAlTocar;
        Sonar(sfxToque);
        alTocar?.Invoke();
        Senal(senalAlTocar);
    }

    private float DistanciaEnPlanta(Vector3 punto)
    {
        Vector3 d = punto - transform.position; d.y = 0f;
        return d.magnitude;
    }

    /// Suelo bajo el punto, con un rayo que empieza justo por encima: un escalón pequeño se sube,
    /// un mueble no. Ignora triggers, al jugador y a sí mismo.
    private float BuscarSuelo(Vector3 punto, float anterior)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(punto.x, anterior + RayoArriba, punto.z), Vector3.down, _impactos,
            RayoLargo, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float mejor = float.NegativeInfinity;
        Transform raizJugador = _jugador != null ? _jugador.root : null;
        for (int i = 0; i < n; i++)
        {
            var t = _impactos[i].transform;
            if (t == transform || t.IsChildOf(transform) || (raizJugador != null && t.root == raizJugador)) continue;
            if (_impactos[i].point.y > mejor) mejor = _impactos[i].point.y;
        }
        return float.IsNegativeInfinity(mejor) ? anterior : mejor;
    }

    private void Sonar(string clave)
    {
        if (!string.IsNullOrEmpty(clave)) AudioService.Instance?.PlaySFX(clave, 1f, transform.position);
    }

    private static void Senal(string clave)
    {
        if (!string.IsNullOrEmpty(clave)) DefaultNarrativeSignals.Instance?.RaiseCustom(clave);
    }
}
