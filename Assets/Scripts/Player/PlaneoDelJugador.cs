using Core;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Planeo del jugador: mientras cae y mantiene el botón de saltar (A / Espacio), baja despacio y
/// avanza hacia donde apunta el stick. Solo si se lo concede lo que lleva puesto el personaje al
/// mando (<see cref="RasgosDeMovimiento.planear"/>, p. ej. el lazo de un atuendo de escena) o si
/// está marcado como siempre disponible. Encaja con el resto del aire: el salto y el doble salto
/// siguen siendo pulsaciones de A; el planeo empieza al mantenerla cuando ya se cae, y se corta
/// al soltarla, al tocar suelo, al volar, en una voltereta desde el suelo o si un golpe lanza al
/// personaje. La física la pone el motor (<c>vThirdPersonMotor.SostenerPlaneo</c>); este
/// componente solo decide cuándo se planea y pone su efecto y su sonido. Va en el objeto del
/// <c>vThirdPersonController</c> de <c>_WILL.prefab</c>, junto a <see cref="VolteretaDelJugador"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlaneoDelJugador : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Controlador del personaje. Se busca solo si está vacío.")]
    [SerializeField] private vThirdPersonController controller;

    [Header("Planeo")]
    [Tooltip("Velocidad máxima de caída (m/s) mientras planea.")]
    [SerializeField, Min(0.1f)] private float velocidadDeCaida = 2.5f;
    [Tooltip("Velocidad mínima (m/s) a la que avanza hacia donde mira mientras planea con el stick pulsado.")]
    [SerializeField, Min(0f)] private float velocidadDeAvance = 7f;
    [Tooltip("Se puede planear sin que lo conceda la ropa (pruebas en el LAB).")]
    [SerializeField] private bool siempreDisponible;

    [Header("Efectos")]
    [Tooltip("Efecto en bucle mientras planea, pegado al personaje (p. ej. destellos del lazo). Vacío = nada.")]
    [SerializeField] private GameObject efectoMientrasPlanea;
    [Tooltip("Posición del efecto respecto al personaje.")]
    [SerializeField] private Vector3 posicionDelEfecto = new Vector3(0f, 1.4f, -0.2f);
    [Tooltip("Clave de SFX al abrir el planeo. Vacío = sin sonido.")]
    [SerializeField] private string sfxAlPlanear = "";

    private Rigidbody _cuerpo;
    private AerialKnockbackReceiver _lanzado;
    private VolteretaDelJugador _voltereta;
    private GameObject _efecto;

    /// <summary>Está planeando ahora.</summary>
    public bool Planeando { get; private set; }

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (controller)
        {
            controller.TryGetComponent(out _cuerpo);
            controller.TryGetComponent(out _lanzado);
            controller.TryGetComponent(out _voltereta);
        }
    }

    void OnDisable()
    {
        if (Planeando) Cambiar(false);
    }

    void Update()
    {
        bool planear = DebePlanear();
        if (planear != Planeando) Cambiar(planear);
    }

    void FixedUpdate()
    {
        if (Planeando && controller) controller.SostenerPlaneo(velocidadDeCaida, velocidadDeAvance);
    }

    private bool DebePlanear()
    {
        if (!GamepadInputReader.JumpHeld) return false;
        if (!controller || !controller.enabled || !_cuerpo || _cuerpo.isKinematic) return false;
        if (controller.suppressAirMovement || !controller.IsAirborne) return false;
        if (_lanzado && _lanzado.IsLaunching) return false;
        if (_voltereta && _voltereta.EnCursoDesdeElSuelo) return false;
        // Empieza cayendo: mientras sube, A sigue siendo el salto.
        if (!Planeando && _cuerpo.linearVelocity.y > 0f) return false;
        return siempreDisponible || RasgosDeMovimiento.AlMando.planear;
    }

    private void Cambiar(bool planear)
    {
        Planeando = planear;
        if (planear)
        {
            if (controller) controller.SostenerPlaneo(velocidadDeCaida, velocidadDeAvance);
            if (!string.IsNullOrEmpty(sfxAlPlanear) && AudioService.Instance != null)
                AudioService.Instance.PlaySFX(sfxAlPlanear, 1f, transform.position);
        }
        else if (controller)
        {
            controller.SoltarPlaneo();
        }
        MostrarEfecto(planear);
    }

    private void MostrarEfecto(bool mostrar)
    {
        if (efectoMientrasPlanea == null) return;
        if (_efecto == null)
        {
            if (!mostrar) return;
            var padre = controller ? controller.transform : transform;
            _efecto = Instantiate(efectoMientrasPlanea, padre, false);
            _efecto.transform.localPosition = posicionDelEfecto;
        }
        if (_efecto.activeSelf != mostrar) _efecto.SetActive(mostrar);
    }
}
