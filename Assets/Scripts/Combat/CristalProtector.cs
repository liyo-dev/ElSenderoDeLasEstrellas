using System;
using UnityEngine;

/// Un cristal que protege a su dueño mientras está entero: espinas o anclas que conjura un jefe.
/// Se registra como objetivo de combate (los aliados lo atacan) y
/// avisa al romperse. Quién decide qué protege y cómo es el dueño: el cristal solo dice si sigue
/// activo. Va junto a su Damageable; con DebilidadDePersonaje, un personaje lo rompe antes.
/// Ver INC-663.
[RequireComponent(typeof(Damageable))]
public sealed class CristalProtector : MonoBehaviour
{
    [SerializeField] private float vida = 60f;
    [Tooltip("Lo que se ve mientras está activo (el cristal).")]
    [SerializeField] private GameObject visual;
    [SerializeField] private GameObject vfxRotura;

    private Damageable _vida;
    private Collider _col;

    public bool Activo { get; private set; }
    public event Action<CristalProtector> AlRomperse;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _vida.OnDied += AlMorir;
        _col = GetComponent<Collider>();
        Apagar();
    }

    void OnDestroy() { if (_vida != null) _vida.OnDied -= AlMorir; }

    public void Activar()
    {
        _vida.Revive(vida);
        _vida.SetMaxAndCurrent(vida, vida);
        Activo = true;
        if (visual) visual.SetActive(true);
        if (_col) _col.enabled = true;
        ActiveCombatRegistry.RegisterNPC(gameObject, allowsCameraLock: true);
    }

    public void Apagar()
    {
        Activo = false;
        if (visual) visual.SetActive(false);
        if (_col) _col.enabled = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
    }

    /// Lo rompe de golpe, como si le hubieran quitado toda la vida.
    public void Romper()
    {
        if (Activo) _vida.Kill();
    }

    private void AlMorir()
    {
        if (!Activo) return;
        if (vfxRotura && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRotura, transform.position, Quaternion.identity, 2f);
        Apagar();
        AlRomperse?.Invoke(this);
    }

}
