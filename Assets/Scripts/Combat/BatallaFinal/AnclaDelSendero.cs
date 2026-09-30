using System;
using UnityEngine;

/// Un ancla del Mago Oscuro (fase 2): un cristal en lo alto de un pilar que le sostiene en el aire
/// y le protege. Mientras quede alguna, el Mago no está expuesto; cuando caen todas, se desploma.
/// Se ven unidas a él por un rayo. Los aliados la atacan (se registra como objetivo de combate)
/// y la magia del pacto de Liam la rompe antes (DebilidadDePersonaje). Ver INC-509.
[RequireComponent(typeof(Damageable))]
public sealed class AnclaDelSendero : MonoBehaviour
{
    [SerializeField] private float vida = 60f;
    [Tooltip("Lo que se ve mientras está activa (el cristal).")]
    [SerializeField] private GameObject visual;
    [Tooltip("Rayo del ancla al Mago.")]
    [SerializeField] private LineRenderer rayo;
    [SerializeField] private GameObject vfxRotura;

    private Damageable _vida;
    private Collider _col;
    private Transform _destino;

    public bool Activa { get; private set; }
    public event Action<AnclaDelSendero> AlRomperse;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _vida.OnDied += AlMorir;
        _col = GetComponent<Collider>();
        Apagar();
    }

    void OnDestroy() { if (_vida != null) _vida.OnDied -= AlMorir; }

    public void Activar(Transform destino)
    {
        _destino = destino;
        _vida.Revive(vida);
        _vida.SetMaxAndCurrent(vida, vida);
        Activa = true;
        if (visual) visual.SetActive(true);
        if (_col) _col.enabled = true;
        if (rayo) rayo.enabled = true;
        ActiveCombatRegistry.RegisterNPC(gameObject, allowsCameraLock: true);
    }

    public void Apagar()
    {
        Activa = false;
        if (visual) visual.SetActive(false);
        if (_col) _col.enabled = false;
        if (rayo) rayo.enabled = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
    }

    private void AlMorir()
    {
        if (!Activa) return;
        if (vfxRotura && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRotura, transform.position, Quaternion.identity, 2f);
        Apagar();
        AlRomperse?.Invoke(this);
    }

    void LateUpdate()
    {
        if (!Activa || rayo == null || _destino == null) return;
        rayo.SetPosition(0, transform.position);
        rayo.SetPosition(1, _destino.position + Vector3.up * 1.2f);
    }
}
