using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pieza que se activa cuando la alcanza la magia de ciertos elementos: un cristal que se
/// enciende con luz, un molinete que gira con viento, un brasero que prende con fuego…
/// Se conecta por eventos con lo que tenga que mover (puerta, plataforma, ascensor).
/// Necesita un collider sólido para que lo encuentren los proyectiles y las bolas devueltas.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class ReceptorDeMagia : MonoBehaviour, IReaccionaAMagia
{
    public enum Modo
    {
        /// <summary>Dispara el evento de activación en cada impacto y no se queda encendido.</summary>
        Pulso,
        /// <summary>Se enciende durante unos segundos; otro impacto renueva el tiempo.</summary>
        Mantenido,
        /// <summary>Se enciende para siempre (hasta Reiniciar()).</summary>
        Permanente
    }

    [Header("Qué lo activa")]
    [SerializeField] private MagicElement[] elementosAceptados = { MagicElement.Light };
    [Tooltip("Solo reacciona a bolas hostiles devueltas con la B.")]
    [SerializeField] private bool soloMagiaDevuelta;

    [Header("Comportamiento")]
    [SerializeField] private Modo modo = Modo.Mantenido;
    [SerializeField, Min(0.1f)] private float segundosEncendido = 4f;

    [Header("Aspecto")]
    [Tooltip("Renderers que se iluminan mientras está encendido (opcional).")]
    [SerializeField] private Renderer[] brillo;
    [SerializeField] private Color colorEncendido = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField, Min(0f)] private float intensidadEncendido = 3f;
    [Tooltip("Luz que se enciende con la pieza (opcional).")]
    [SerializeField] private Light luz;
    [Tooltip("Objeto que gira mientras está encendido (aspa de un molinete, opcional).")]
    [SerializeField] private Transform parteQueGira;
    [SerializeField] private float gradosPorSegundo = 360f;

    [Header("Audio")]
    [SerializeField] private string sfxAlActivar = "RuneActivate";
    [SerializeField] private string sfxAlApagar;

    [Header("Eventos")]
    public UnityEvent alActivarse;
    public UnityEvent alApagarse;

    private bool _encendido;
    private Coroutine _apagado;
    private MaterialPropertyBlock _bloque;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool Encendido => _encendido;

    private void Awake() => Pintar(false);

    public void RecibirMagia(in ImpactoDeMagia impacto)
    {
        if (soloMagiaDevuelta && !impacto.Devuelto) return;
        if (!Acepta(impacto.Elemento)) return;
        Activar();
    }

    /// <summary>Activa la pieza como si la hubiera alcanzado la magia correcta.</summary>
    public void Activar()
    {
        if (modo == Modo.Permanente && _encendido) return;

        if (!string.IsNullOrEmpty(sfxAlActivar))
            AudioService.Instance?.PlaySFX(sfxAlActivar, 1f, transform.position);

        if (modo == Modo.Pulso)
        {
            alActivarse.Invoke();
            StartCoroutine(Co_Destello());
            return;
        }

        bool yaEncendido = _encendido;
        Encender();
        if (!yaEncendido) alActivarse.Invoke();

        if (modo == Modo.Mantenido)
        {
            if (_apagado != null) StopCoroutine(_apagado);
            _apagado = StartCoroutine(Co_ApagarTras(segundosEncendido));
        }
    }

    /// <summary>Deja la pieza encendida sin lanzar eventos (al restaurar una partida).</summary>
    public void RestaurarEncendido() => Encender();

    /// <summary>Apaga la pieza y lanza el evento de apagado si estaba encendida.</summary>
    public void Apagar()
    {
        if (_apagado != null) { StopCoroutine(_apagado); _apagado = null; }
        if (!_encendido) return;
        _encendido = false;
        Pintar(false);
        if (!string.IsNullOrEmpty(sfxAlApagar))
            AudioService.Instance?.PlaySFX(sfxAlApagar, 1f, transform.position);
        alApagarse.Invoke();
    }

    /// <summary>Vuelve al estado inicial (apagada) sin eventos.</summary>
    public void Reiniciar()
    {
        if (_apagado != null) { StopCoroutine(_apagado); _apagado = null; }
        _encendido = false;
        Pintar(false);
    }

    private bool Acepta(MagicElement elemento)
    {
        if (elementosAceptados == null || elementosAceptados.Length == 0) return true;
        for (int i = 0; i < elementosAceptados.Length; i++)
            if (elementosAceptados[i] == elemento) return true;
        return false;
    }

    private void Encender()
    {
        _encendido = true;
        Pintar(true);
    }

    private IEnumerator Co_ApagarTras(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        _apagado = null;
        Apagar();
    }

    private IEnumerator Co_Destello()
    {
        Pintar(true);
        yield return new WaitForSeconds(0.3f);
        if (!_encendido) Pintar(false);
    }

    private void Update()
    {
        if (_encendido && parteQueGira != null)
            parteQueGira.Rotate(Vector3.forward, gradosPorSegundo * Time.deltaTime, Space.Self);
    }

    private void Pintar(bool encendido)
    {
        if (luz != null) luz.enabled = encendido;
        if (brillo == null || brillo.Length == 0) return;
        _bloque ??= new MaterialPropertyBlock();
        Color emision = encendido ? colorEncendido * intensidadEncendido : Color.black;
        for (int i = 0; i < brillo.Length; i++)
        {
            var r = brillo[i];
            if (r == null) continue;
            r.GetPropertyBlock(_bloque);
            _bloque.SetColor(EmissionColorId, emision);
            r.SetPropertyBlock(_bloque);
        }
    }
}
