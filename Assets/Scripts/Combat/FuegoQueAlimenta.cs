using UnityEngine;
using Sendero.Core.Feedback;

/// Regla propia de un jefe (el Gólem): el fuego que le llega de frente, además de rebotar en su
/// coraza (ArmaduraFrontal), le da fuerza. Cada impacto de fuego por delante sube un nivel (hasta 'nivelesMaximos'): crece,
/// le llegan más lejos los golpes y pegan más. Se ve claro: una llamarada al recibirlo, el cuerpo
/// se enrojece y brilla con una luz anaranjada que sube con cada nivel. Cada nivel se apaga solo
/// pasados unos segundos sin fuego. No es una debilidad ni una resistencia elemental general: solo
/// vale para quien lleve este componente. Por la espalda el fuego hace daño normal.
///
/// Solo cuenta el fuego que lanza el jugador (no el de los compañeros con IA).
/// Se entera del fuego de dos formas: los proyectiles del jugador que entran en su cuerpo (una
/// consulta por fotograma con buffer fijo, solo en la capa de los proyectiles) y las zonas de magia
/// (IReaccionaAMagia). La IA del jefe lee MultAlcance y MultDano. Ver INC-723.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class FuegoQueAlimenta : MonoBehaviour, IReaccionaAMagia
{
    [Header("Qué le da fuerza")]
    [Tooltip("Elemento que le alimenta.")]
    [SerializeField] private MagicElement elemento = MagicElement.Fire;
    [Tooltip("Metros de margen alrededor del cuerpo para notar un proyectil que llega.")]
    [SerializeField, Min(0f)] private float margen = 0.6f;
    [Tooltip("Segundos mínimos entre dos subidas de nivel (un mismo hechizo no cuenta dos veces).")]
    [SerializeField, Min(0f)] private float pausaEntreSubidas = 0.5f;

    [Header("Cuánto crece")]
    [SerializeField, Min(1)] private int nivelesMaximos = 3;
    [Tooltip("Tamaño que gana por nivel (0,12 = un 12 % más grande).")]
    [SerializeField, Min(0f)] private float tamanoPorNivel = 0.12f;
    [Tooltip("Alcance de sus golpes (puñetazo, onda, contacto, lluvia) que gana por nivel.")]
    [SerializeField, Min(0f)] private float alcancePorNivel = 0.15f;
    [Tooltip("Daño de sus golpes que gana por nivel.")]
    [SerializeField, Min(0f)] private float danoPorNivel = 0.2f;
    [Tooltip("Segundos sin fuego hasta que pierde un nivel.")]
    [SerializeField, Min(1f)] private float segundosPorNivel = 9f;
    [Tooltip("Velocidad a la que cambia de tamaño (fracción por segundo).")]
    [SerializeField, Min(0.1f)] private float velocidadDeCrecer = 0.6f;

    [Header("Cómo se ve")]
    [SerializeField] private Color colorDeFuego = new Color(1f, 0.45f, 0.18f);
    [Tooltip("Cuánto se enrojece el cuerpo con todos los niveles (0 = nada, 1 = del todo).")]
    [SerializeField, Range(0f, 1f)] private float tinteMaximo = 0.6f;
    [SerializeField, Min(0f)] private float luzPorNivel = 2.5f;
    [Tooltip("Llamarada al recibir el fuego. Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxAlCrecer;

    private Damageable _vida;
    private Collider _cuerpo;
    private ArmaduraFrontal _armadura;
    private TransicionDeFaseDeJefe _transicion;
    private IJefeConFases _fases;
    private Light _luz;

    private int _nivel;
    private float _bajarEn;
    private float _ultimaSubida = -999f;
    private float _factorActual = 1f;
    private int _capaProyectiles;

    private readonly Collider[] _buffer = new Collider[16];
    // Proyectiles ya contados (id y cuándo), para no contar dos veces el mismo al cruzar el cuerpo.
    private readonly MagicProjectile[] _vistos = new MagicProjectile[16];
    private readonly float[] _vistosEn = new float[16];
    private int _siguienteVisto;

    /// Nivel actual (0 = normal).
    public int Nivel => _nivel;
    /// Multiplica el alcance de sus golpes.
    public float MultAlcance => 1f + alcancePorNivel * _nivel;
    /// Multiplica el daño de sus golpes.
    public float MultDano => 1f + danoPorNivel * _nivel;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _cuerpo = GetComponent<Collider>();
        _armadura = GetComponent<ArmaduraFrontal>();
        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        _fases = GetComponent<IJefeConFases>();
        int capa = LayerMask.NameToLayer("Projectile");
        _capaProyectiles = capa >= 0 ? 1 << capa : ~0;
        for (int i = 0; i < _vistos.Length; i++) _vistosEn[i] = -999f;

        var luz = new GameObject("Luz del fuego que alimenta");
        luz.transform.SetParent(transform, false);
        luz.transform.position = Pecho();
        _luz = luz.AddComponent<Light>();
        _luz.type = LightType.Point;
        _luz.color = colorDeFuego;
        _luz.range = 6f;
        _luz.intensity = 0f;
        _luz.enabled = false;
    }

    void Update()
    {
        if (_vida == null || !_vida.IsAlive) return;

        BuscarProyectilesDeFuego();

        if (_nivel > 0 && Time.time >= _bajarEn) CambiarNivel(_nivel - 1);

        // Tamaño: se acerca poco a poco al de su nivel. Se aplica como cambio relativo para
        // convivir con otros cambios de escala del jefe (la fusión con el bosque de la fase 3).
        float objetivo = 1f + tamanoPorNivel * _nivel;
        if (!Mathf.Approximately(_factorActual, objetivo))
        {
            float nuevo = Mathf.MoveTowards(_factorActual, objetivo, velocidadDeCrecer * Time.deltaTime);
            transform.localScale *= nuevo / _factorActual;
            _factorActual = nuevo;
        }
    }

    /// Zonas de magia (y cualquier otra vía que avise a las piezas que reaccionan).
    public void RecibirMagia(in ImpactoDeMagia impacto)
    {
        if (impacto.Elemento != elemento || impacto.Devuelto || !EsDelJugador(impacto.Lanzador)) return;
        Vector3 desde = impacto.Lanzador != null ? impacto.Lanzador.transform.position : impacto.Punto;
        if (EsDeFrente(desde)) Alimentar(impacto.Punto);
    }

    private void BuscarProyectilesDeFuego()
    {
        Vector3 centro = Pecho();
        float radio = RadioDelCuerpo() + margen;
        int n = Physics.OverlapSphereNonAlloc(centro, radio, _buffer, _capaProyectiles, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var col = _buffer[i];
            if (col == null) continue;
            var proyectil = col.GetComponentInParent<MagicProjectile>();
            if (proyectil == null || proyectil.Elemento != elemento) continue;
            if (!EsDelJugador(proyectil.Lanzador)) continue;
            if (YaVisto(proyectil)) continue;

            Vector3 desde = proyectil.Lanzador != null ? proyectil.Lanzador.transform.position : proyectil.transform.position;
            if (EsDeFrente(desde)) Alimentar(proyectil.transform.position);
        }
    }

    /// Solo cuenta el fuego del jugador: es su lección. Los compañeros con IA (Estela provoca al
    /// Gólem de frente y lanza fuego) no deben arruinarle la pelea.
    private static bool EsDelJugador(GameObject lanzador)
    {
        var jugador = PlayerService.Player;
        return lanzador != null && jugador != null && lanzador.transform.root == jugador.transform.root;
    }

    private bool EsDeFrente(Vector3 posicion)
    {
        if (_armadura != null) return _armadura.EsDeFrente(posicion);
        Vector3 hacia = posicion - transform.position;
        hacia.y = 0f;
        return hacia.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, hacia) < 110f;
    }

    private bool YaVisto(MagicProjectile proyectil)
    {
        for (int i = 0; i < _vistos.Length; i++)
            if (_vistos[i] == proyectil && Time.time - _vistosEn[i] < 2f) return true;
        _vistos[_siguienteVisto] = proyectil;
        _vistosEn[_siguienteVisto] = Time.time;
        _siguienteVisto = (_siguienteVisto + 1) % _vistos.Length;
        return false;
    }

    private void Alimentar(Vector3 punto)
    {
        if (Time.time - _ultimaSubida < pausaEntreSubidas) return;
        _ultimaSubida = Time.time;

        if (vfxAlCrecer != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxAlCrecer, punto, Quaternion.identity, 2f);
        FeedbackService.CameraShake(0.15f, 0.2f);
        AvisosDeCombate.Fortalecerse(gameObject);

        // Ya al máximo: el fuego mantiene el nivel (reinicia la cuenta) pero no lo sube.
        CambiarNivel(Mathf.Min(_nivel + 1, nivelesMaximos));
    }

    private void CambiarNivel(int nivel)
    {
        _nivel = Mathf.Clamp(nivel, 0, nivelesMaximos);
        _bajarEn = Time.time + segundosPorNivel;
        RefrescarAspecto();
    }

    /// Vuelve a poner el color del cuerpo según la fase y el nivel. La IA la llama tras teñirlo
    /// por otra causa (cambio de fase, barro) para no borrar el brillo del fuego.
    public void RefrescarAspecto()
    {
        float k = nivelesMaximos > 0 ? (float)_nivel / nivelesMaximos : 0f;
        if (_luz != null)
        {
            bool encendida = _nivel > 0;
            if (_luz.enabled != encendida) _luz.enabled = encendida;
            _luz.intensity = luzPorNivel * _nivel;
            _luz.range = 6f + 1.5f * _nivel;
            _luz.transform.position = Pecho();
        }
        if (_transicion != null)
        {
            Color fase = _transicion.TinteDeFase(_fases != null ? _fases.Fase : 0);
            _transicion.Tintar(fase * Color.Lerp(Color.white, colorDeFuego, k * tinteMaximo), apagarEmision: false);
        }
    }

    private Vector3 Pecho() => _cuerpo != null ? _cuerpo.bounds.center : transform.position + Vector3.up * 2f;

    private float RadioDelCuerpo()
    {
        if (_cuerpo == null) return 1.5f;
        Vector3 e = _cuerpo.bounds.extents;
        return Mathf.Max(e.x, e.z);
    }
}
