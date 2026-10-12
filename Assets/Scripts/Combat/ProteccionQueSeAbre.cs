using System;
using UnityEngine;
using UnityEngine.Events;

/// Algo con una protección que un hechizo abre durante un rato (Amanecer): un jefe al que nada le
/// hace daño, un cristal protector, un sello de templo, un guardián del post-juego. El hechizo
/// solo la abre; qué significa «abierta» lo decide quien la tiene. Ver INC-724.
public interface IProteccionQueSeAbre
{
    /// Abre la protección durante 'segundos'; si ya estaba abierta, dura lo que acabe más tarde.
    /// 'vfx' es el efecto que trae el hechizo para mientras dure (puede ser null).
    void AbrirProteccion(float segundos, GameObject quien, GameObject vfx);
}

/// Protección genérica que abre un hechizo con «abre la protección» (MagicSpellSO.abreProteccionSegundos).
/// Mientras está abierta cuenta como expuesto (IExpuestoAlDano): con SoloDanoCuandoExpuesto en el
/// mismo objeto, un guardián solo recibe daño con la protección abierta. Lo que se ve cerrado (un
/// sello, un velo) se oculta mientras está abierta y, con 'quedaAbierta', ya no se vuelve a
/// cerrar. Otros componentes (el Mago Oscuro) leen Abierta y sus eventos. Ver INC-724.
[DisallowMultipleComponent]
public sealed class ProteccionQueSeAbre : MonoBehaviour, IProteccionQueSeAbre, IExpuestoAlDano
{
    [Tooltip("Lo que se ve mientras está cerrada (sello, velo, aura). Se oculta mientras está abierta. Vacío = nada.")]
    [SerializeField] private GameObject visualCerrada;
    [Tooltip("Efecto en bucle mientras está abierta. Vacío = el que trae el hechizo.")]
    [SerializeField] private GameObject vfxAbierta;
    [Tooltip("Altura del efecto sobre el pivote.")]
    [SerializeField] private float alturaVfx = 1.2f;
    [Tooltip("Multiplica los segundos que la deja abierta el hechizo (0,5 = la mitad).")]
    [SerializeField, Min(0f)] private float multiplicadorDeDuracion = 1f;
    [Tooltip("Una vez abierta ya no se cierra (sellos).")]
    [SerializeField] private bool quedaAbierta;
    [Tooltip("Segundos que se ve el efecto al abrirse una protección que queda abierta.")]
    [SerializeField, Min(0.1f)] private float duracionVfxPermanente = 3f;

    public UnityEvent alAbrirse = new UnityEvent();
    public UnityEvent alCerrarse = new UnityEvent();

    /// Se abre (con quien la abrió).
    public event Action<ProteccionQueSeAbre, GameObject> AlAbrirse;
    public event Action<ProteccionQueSeAbre> AlCerrarse;

    private bool _abierta;
    private float _hasta = -1f;
    private VfxPoolService _pool;
    private Transform _fx;
    private ulong _uso;

    public bool Abierta => _abierta;
    public bool Expuesto => _abierta;
    public bool QuedaAbierta => quedaAbierta;
    /// Segundos que le quedan abierta (infinito si queda abierta; 0 si está cerrada).
    public float SegundosRestantes => !_abierta ? 0f : quedaAbierta ? float.PositiveInfinity : Mathf.Max(0f, _hasta - Time.time);

    public void AbrirProteccion(float segundos, GameObject quien, GameObject vfx)
    {
        float s = segundos * multiplicadorDeDuracion;
        if (s <= 0f && !quedaAbierta) return;
        _hasta = Mathf.Max(_hasta, Time.time + s);
        if (_abierta) return;

        _abierta = true;
        if (visualCerrada != null && visualCerrada.activeSelf) visualCerrada.SetActive(false);
        PonerVfx(vfxAbierta != null ? vfxAbierta : vfx);
        AlAbrirse?.Invoke(this, quien);
        alAbrirse.Invoke();
    }

    /// Cierra ya (al reiniciar un combate o un puzle), también si quedaba abierta.
    public void Cerrar()
    {
        if (!_abierta) return;
        _abierta = false;
        _hasta = -1f;
        RecogerVfx();
        if (visualCerrada != null && !visualCerrada.activeSelf) visualCerrada.SetActive(true);
        AlCerrarse?.Invoke(this);
        alCerrarse.Invoke();
    }

    void Update()
    {
        if (_abierta && !quedaAbierta && Time.time >= _hasta) Cerrar();
    }

    void OnDisable()
    {
        if (quedaAbierta) RecogerVfx();
        else Cerrar();
    }

    private void PonerVfx(GameObject prefab)
    {
        if (prefab == null) return;
        var pool = VfxPoolService.Instance;
        if (pool == null) return;
        Vector3 pos = transform.position + Vector3.up * alturaVfx;
        if (quedaAbierta)
        {
            pool.Play(prefab, pos, Quaternion.identity, duracionVfxPermanente);
            return;
        }
        _pool = pool;
        _fx = pool.Play(prefab, pos, Quaternion.identity, float.MaxValue, transform);
        _uso = _fx != null ? pool.ObtenerUso(_fx) : 0;
    }

    private void RecogerVfx()
    {
        if (_pool != null && _fx != null && _uso != 0) _pool.Recoger(_fx, _uso);
        _pool = null;
        _fx = null;
        _uso = 0;
    }

    /// La protección de lo que hay detrás de un collider (en él o en sus padres); null si no tiene.
    public static IProteccionQueSeAbre De(Collider col) =>
        col != null ? col.GetComponentInParent<IProteccionQueSeAbre>() : null;
}
