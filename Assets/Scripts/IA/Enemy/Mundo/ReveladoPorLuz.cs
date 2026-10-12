using System;
using UnityEngine;

/// <summary>
/// Regla de daño «intangible salvo bajo la luz»: los golpes no le hacen nada hasta que una magia
/// de luz del grupo del jugador (un proyectil o una zona de ese elemento) pasa cerca o le da;
/// entonces queda revelado unos segundos y cualquier golpe entra. La luz que le alcanza de lleno
/// ya cuenta en ese mismo golpe. Va junto al Damageable. Ver INC-722.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class ReveladoPorLuz : MonoBehaviour, IFiltroDeDano, IExpuestoAlDano, IReaccionaAMagia
{
    [Tooltip("Elemento de la magia que lo revela.")]
    [SerializeField] private MagicElement elementoQueRevela = MagicElement.Light;
    [Tooltip("Segundos que queda revelado tras la última luz.")]
    [SerializeField, Min(0.5f)] private float segundosRevelado = 6f;
    [Tooltip("Metros alrededor del centro a los que una luz mágica lo alcanza.")]
    [SerializeField, Min(0.5f)] private float radioDeLuz = 3.5f;
    [Tooltip("Altura del centro sobre el pivote.")]
    [SerializeField] private float alturaDelCentro = 1f;
    [Tooltip("Cada cuántos segundos mira si hay una luz cerca.")]
    [SerializeField, Min(0.05f)] private float revisarCada = 0.15f;
    [Tooltip("Efecto al quedar revelado (opcional).")]
    [SerializeField] private GameObject vfxAlRevelar;
    [Tooltip("Efecto cuando un golpe lo atraviesa sin hacerle nada (opcional).")]
    [SerializeField] private GameObject vfxGolpeSinEfecto;

    /// <summary>Revelado o no; se avisa al cambiar.</summary>
    public event Action<bool> AlCambiar;

    public bool Expuesto => Time.time < _reveladoHasta;
    public float SegundosRestantes => Mathf.Max(0f, _reveladoHasta - Time.time);

    private float _reveladoHasta = -1f;
    private float _siguienteRevision;
    private float _ultimoEfectoSinEfecto;
    private bool _avisadoRevelado;
    private Predicate<MagicProjectile> _esLuzDelGrupo;

    private Vector3 Centro => transform.position + Vector3.up * alturaDelCentro;

    private void Awake()
    {
        _esLuzDelGrupo = p => p.isActiveAndEnabled && p.Elemento == elementoQueRevela &&
                              AmenazasAlJugador.EsDelGrupoDelJugador(p.Lanzador);
    }

    private void OnDisable()
    {
        _reveladoHasta = -1f;
        if (_avisadoRevelado) { _avisadoRevelado = false; AlCambiar?.Invoke(false); }
    }

    private void Update()
    {
        if (Time.time >= _siguienteRevision)
        {
            _siguienteRevision = Time.time + revisarCada;
            if (HayLuzCerca()) Iluminar();
        }
        bool revelado = Expuesto;
        if (revelado == _avisadoRevelado) return;
        _avisadoRevelado = revelado;
        AlCambiar?.Invoke(revelado);
    }

    /// <summary>Lo revela (o alarga el tiempo revelado). Negativo = el tiempo configurado.</summary>
    public void Iluminar(float segundos = -1f)
    {
        bool antes = Expuesto;
        _reveladoHasta = Mathf.Max(_reveladoHasta, Time.time + (segundos > 0f ? segundos : segundosRevelado));
        if (antes) return;
        if (vfxAlRevelar != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxAlRevelar, Centro, Quaternion.identity, 1.5f);
        _avisadoRevelado = true;
        AlCambiar?.Invoke(true);
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (Expuesto) return cantidad;
        // La luz que le da de lleno todavía está aquí cuando llega su golpe.
        if (HayLuzCerca()) { Iluminar(); return cantidad; }
        if (vfxGolpeSinEfecto != null && VfxPoolService.Instance != null && Time.time - _ultimoEfectoSinEfecto > 0.2f)
        {
            _ultimoEfectoSinEfecto = Time.time;
            VfxPoolService.Instance.Play(vfxGolpeSinEfecto, Centro, Quaternion.identity, 1f);
        }
        return 0f;
    }

    /// <summary>Las zonas de magia avisan antes de hacer daño (ReaccionesAMagia).</summary>
    public void RecibirMagia(in ImpactoDeMagia impacto)
    {
        if (impacto.Elemento != elementoQueRevela) return;
        if (impacto.Lanzador != null && !AmenazasAlJugador.EsDelGrupoDelJugador(impacto.Lanzador)) return;
        Iluminar();
    }

    // Los proyectiles de luz se buscan cerca: al darle de lleno van por la rama de enemigos y no
    // avisan a ReaccionesAMagia.
    private bool HayLuzCerca() => EnemigoDelMundo.HayCerca(Centro, radioDeLuz, _esLuzDelGrupo);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.95f, 0.5f);
        Gizmos.DrawWireSphere(Centro, radioDeLuz);
    }
}
