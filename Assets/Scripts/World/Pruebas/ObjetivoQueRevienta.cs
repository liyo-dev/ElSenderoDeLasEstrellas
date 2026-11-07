using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Un objetivo que revienta al darle (un globo, una diana flotante): suelta un efecto, deja de verse
/// y de poderse apuntar, y vuelve a su sitio con <see cref="Reponer"/>. No sabe qué lo revienta: se
/// conecta por eventos (p. ej. <see cref="ReceptorDeMagia.alActivarse"/> → <see cref="Reventar"/>, y
/// el reinicio de un contador → <see cref="Reponer"/>).
/// </summary>
public sealed class ObjetivoQueRevienta : MonoBehaviour
{
    [SerializeField] private GameObject vfxAlReventar;
    [SerializeField, Min(0.1f)] private float duracionDelVfx = 2f;
    [SerializeField] private string sfxAlReventar = "";

    [Tooltip("Al reventar (una sola vez hasta que se reponga).")]
    public UnityEvent alReventar = new UnityEvent();

    // Solo lo que estaba encendido al empezar: lo apagado a propósito sigue apagado al reponer.
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<Collider> _colliders = new List<Collider>();
    private bool _leido;
    private Targetable _apuntable;
    private ReceptorDeMagia _receptor;

    public bool Reventado { get; private set; }

    private void Awake() => Leer();

    private void Leer()
    {
        if (_leido) return;
        _leido = true;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.enabled) _renderers.Add(r);
        foreach (var c in GetComponentsInChildren<Collider>(true)) if (c.enabled) _colliders.Add(c);
        _receptor = GetComponent<ReceptorDeMagia>();
    }

    /// <summary>Revienta: efecto, sonido y fuera de la vista. Repetirlo no hace nada.</summary>
    public void Reventar()
    {
        if (Reventado) return;
        Reventado = true;
        if (vfxAlReventar != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxAlReventar, transform.position, Quaternion.identity, duracionDelVfx);
        if (!string.IsNullOrEmpty(sfxAlReventar))
            AudioService.Instance?.PlaySFX(sfxAlReventar, 1f, transform.position);
        Mostrar(false);
        alReventar.Invoke();
    }

    /// <summary>Vuelve a estar entero y apuntable.</summary>
    public void Reponer()
    {
        Reventado = false;
        Mostrar(true);
        if (_receptor != null) _receptor.Reiniciar();
    }

    private void Mostrar(bool visible)
    {
        Leer();
        foreach (var r in _renderers) if (r != null) r.enabled = visible;
        foreach (var c in _colliders) if (c != null) c.enabled = visible;
        if (_apuntable == null) TryGetComponent(out _apuntable);
        if (_apuntable != null) _apuntable.enabled = visible;
    }
}
