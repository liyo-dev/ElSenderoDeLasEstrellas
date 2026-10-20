using System;
using UnityEngine;
using UnityEngine.Events;
using Sendero.Core.Feedback;

/// <summary>
/// Diana u objetivo celestial interactivo para el nivel de escape del Sendero.
/// Responde a ataques mágicos (a través de Damageable en capa Enemy),
/// interacción directa (Interactable) o impacto físico del jugador.
/// </summary>
[DisallowMultipleComponent]
public class DianaAstralSendero : MonoBehaviour
{
    [Header("Activación")]
    [SerializeField] private NivelEscapeSendero nivelCtrl;
    [SerializeField] private int numeroObstaculo = 1;
    [SerializeField] private bool desactivarAlActivar = true;
    [SerializeField] private GameObject objetoDesactivarAlRomper;

    public NivelEscapeSendero NivelCtrl { get => nivelCtrl; set => nivelCtrl = value; }
    public int NumeroObstaculo { get => numeroObstaculo; set => numeroObstaculo = value; }
    public GameObject ObjetoDesactivarAlRomper { get => objetoDesactivarAlRomper; set => objetoDesactivarAlRomper = value; }

    [Header("Feedback")]
    [SerializeField] private GameObject vfxAlActivar;
    [SerializeField] private Color colorDestello = new Color(1f, 0.85f, 0.4f, 0.6f);
    [SerializeField] private float intensidadTemblor = 0.35f;

    [Header("Eventos")]
    public UnityEvent alActivar = new UnityEvent();

    private bool _activado;
    private Damageable _damageable;
    private Interactable _interactable;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        if (_damageable != null)
        {
            _damageable.OnDamaged += OnRecibirDano;
            _damageable.OnDied += Activar;
        }

        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
        {
            _interactable.OnInteract.AddListener(OnInteractuar);
        }
    }

    private void OnDestroy()
    {
        if (_damageable != null)
        {
            _damageable.OnDamaged -= OnRecibirDano;
            _damageable.OnDied -= Activar;
        }
        if (_interactable != null)
        {
            _interactable.OnInteract.RemoveListener(OnInteractuar);
        }
    }

    private void OnInteractuar(GameObject go) => Activar();

    private void OnRecibirDano(float cantidad)
    {
        if (_activado) return;
        // Si no muere de un golpe pero recibe impacto mágico, activar también para agilidad
        Activar();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_activado) return;
        if (other.CompareTag("Player") || other.GetComponentInParent<Invector.vCharacterController.vThirdPersonController>() != null)
        {
            Activar();
        }
    }

    public void Activar()
    {
        if (_activado) return;
        _activado = true;

        if (intensidadTemblor > 0f)
        {
            FeedbackService.CameraShake(intensidadTemblor, 0.45f);
        }

        if (colorDestello.a > 0.01f)
        {
            FeedbackService.ScreenFlash(colorDestello, 0.35f);
        }

        if (vfxAlActivar != null)
        {
            VfxPoolService.Instance?.Play(vfxAlActivar, transform.position, transform.rotation, 2.5f);
        }

        alActivar?.Invoke();

        if (nivelCtrl != null)
        {
            if (numeroObstaculo == 1) nivelCtrl.ResolverObstaculo1();
            else if (numeroObstaculo == 2) nivelCtrl.ResolverObstaculo2();
            else if (numeroObstaculo == 3) nivelCtrl.ResolverObstaculo3();
        }

        if (objetoDesactivarAlRomper != null)
        {
            objetoDesactivarAlRomper.SetActive(false);
        }

        if (desactivarAlActivar)
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }
}
