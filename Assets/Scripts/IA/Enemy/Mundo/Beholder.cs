using UnityEngine;

/// <summary>
/// Beholder: torreta que gira hacia el objetivo y le dispara rayos con carga visible
/// (<see cref="CargaDeDisparo"/>, que ya los anuncia en <see cref="AmenazasAlJugador"/>). Su ojo
/// se cierra a los golpes normales; la respuesta es devolverle su rayo con la defensa (B): la
/// bola devuelta lo aturde unos segundos y en ese tiempo cualquier golpe entra. Los rayos son
/// <see cref="EnemyProjectile"/>, así que también sirven de pieza de puzle (una bola devuelta que
/// da en un cristal lo enciende). Ver INC-722.
/// </summary>
[RequireComponent(typeof(CargaDeDisparo))]
public sealed class Beholder : EnemigoDelMundo, IFiltroDeDano, IExpuestoAlDano
{
    [Header("Disparo")]
    [Tooltip("Prefab del rayo (con EnemyProjectile en la raíz o en un hijo).")]
    [SerializeField] private GameObject proyectil;
    [Tooltip("Punto de salida. Vacío = un metro por encima y delante del pivote.")]
    [SerializeField] private Transform boca;
    [SerializeField, Min(0.5f)] private float cadencia = 3f;
    [SerializeField, Min(0f)] private float dano = 12f;
    [Tooltip("Velocidad del rayo (m/s): da tiempo a devolverlo.")]
    [SerializeField, Min(1f)] private float velocidad = 10f;
    [Tooltip("Grados por segundo a los que gira hacia el objetivo.")]
    [SerializeField, Min(1f)] private float giro = 120f;
    [Tooltip("Dispara cuando el objetivo está dentro de este ángulo delante de él.")]
    [SerializeField, Range(1f, 90f)] private float anguloParaDisparar = 15f;

    [Header("Aturdido por su propio rayo")]
    [SerializeField, Min(0.5f)] private float segundosAturdido = 3.5f;
    [Tooltip("Parte del daño que entra sin estar aturdido. 0 = nada.")]
    [SerializeField, Range(0f, 1f)] private float factorSinAturdir = 0f;
    [Tooltip("Efecto sobre la cabeza mientras está aturdido (opcional).")]
    [SerializeField] private GameObject vfxAturdido;
    [Tooltip("Efecto cuando un golpe rebota en el ojo cerrado (opcional).")]
    [SerializeField] private GameObject vfxRebote;

    [Header("Animaciones (estados del Animator)")]
    [SerializeField] private string animReposo = "IdleNormal";
    [SerializeField] private string animAlerta = "SenseSomethingST";
    [SerializeField] private string animCombate = "IdleBattle";
    [SerializeField] private string animDisparo = "Attack01";
    [SerializeField] private string animAturdido = "Dizzy";

    public bool Expuesto => Time.time < _aturdidoHasta;

    private CargaDeDisparo _carga;
    private System.Action<GameObject, Vector3> _inicializar;
    private float _siguienteDisparo, _volverAlReposo, _aturdidoHasta = -1f, _ultimoRebote;
    private bool _aturdido;
    private Transform _aura;
    private ulong _usoAura;

    protected override void Awake()
    {
        base.Awake();
        _carga = GetComponent<CargaDeDisparo>();
        _inicializar = Inicializar;
    }

    protected override void OnDisable()
    {
        if (_carga != null) _carga.Cancelar();
        RecogerAura();
        base.OnDisable();
    }

    protected override void Pensar(float dt)
    {
        if (_aturdido)
        {
            if (Expuesto) return;
            _aturdido = false;
            RecogerAura();
            Animar(animCombate);
            _siguienteDisparo = Time.time + 1f;
        }

        float distancia = DistanciaAlObjetivo();
        if (!HayObjetivo || distancia > alcanceDePerdida)
        {
            if (EnCombate) { SalirDeCombate(); _carga.Cancelar(); }
            Animar(animReposo);
            return;
        }
        if (!EnCombate)
        {
            if (distancia > alcanceDeDeteccion) { Animar(animReposo); return; }
            EntrarEnCombate();
            AnimarDeNuevo(animAlerta);
            _siguienteDisparo = Time.time + 1f;
            _volverAlReposo = Time.time + 0.9f;
        }

        Vector3 hacia = HaciaElObjetivoPlano();
        MirarHacia(hacia, giro);
        if (Time.time >= _volverAlReposo) Animar(animCombate);

        if (_carga.EnCarga || Time.time < _siguienteDisparo || proyectil == null) return;
        if (distancia > alcanceDeDeteccion * 1.2f) return;
        if (Vector3.Angle(transform.forward, hacia) > anguloParaDisparar) return;

        Transform salida = boca != null ? boca : transform;
        Vector3 desplazamiento = boca != null ? Vector3.zero : Vector3.up + Vector3.forward;
        if (_carga.Cargar(proyectil, salida, desplazamiento, Objetivo, velocidad, _inicializar))
        {
            AnimarDeNuevo(animDisparo);
            _volverAlReposo = Time.time + 1f;
            _siguienteDisparo = Time.time + Mathf.Max(cadencia, _carga.SegundosDeCarga + 0.1f);
        }
    }

    private void Inicializar(GameObject disparo, Vector3 direccion)
    {
        var p = disparo.GetComponentInChildren<EnemyProjectile>();
        if (p == null) { Destroy(disparo); return; }
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        p.AsignarLanzador(gameObject);
        p.SetSpeed(velocidad);
        p.Initialize(direccion, dano);
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (Muerto || Expuesto) return cantidad;
        Vector3 centro = transform.position + Vector3.up;
        if (ProyectilDevuelto.GolpeandoAhora)
        {
            Aturdir();
            return cantidad;
        }
        if (vfxRebote != null && Time.time - _ultimoRebote > 0.2f)
        {
            _ultimoRebote = Time.time;
            Efecto(vfxRebote, centro, 1f);
        }
        return cantidad * factorSinAturdir;
    }

    private void Aturdir()
    {
        _aturdidoHasta = Time.time + segundosAturdido;
        _aturdido = true;
        _carga.Cancelar();
        AmenazasAlJugador.RetirarAnuncio(transform);
        AnimarDeNuevo(animAturdido);
        RecogerAura();
        _aura = Efecto(vfxAturdido, transform.position + Vector3.up * 2f, segundosAturdido, transform);
        if (_aura != null) _usoAura = VfxPoolService.Instance.ObtenerUso(_aura);
    }

    private void RecogerAura()
    {
        if (_aura != null && VfxPoolService.Instance != null) VfxPoolService.Instance.Recoger(_aura, _usoAura);
        _aura = null;
    }

    protected override void AlMorir()
    {
        if (_carga != null) _carga.Cancelar();
        RecogerAura();
    }
}
