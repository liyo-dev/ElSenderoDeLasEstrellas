using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Quién mueve este cuerpo: el jugador (mando) o la IA. Es la bandera única «lo lleva el jugador»
/// de un personaje que tiene su propio cuerpo y se puede llevar a mano o dejar a la IA.
///
/// El cuerpo lleva las dos pilas a la vez y aquí se enciende una y se apaga la otra: los
/// componentes de la lista «con el jugador» (control, cámara, entrada, magia a mano…) solo
/// funcionan mientras lo lleva el jugador, y los de «con la IA» (navegación, cerebro, charla)
/// mientras lo lleva la IA. Así nadie lee los botones desde un cuerpo que no es el del jugador.
/// Con la IA, además, el Rigidbody es cinemático (lo mueve el agente), el agente se coloca en el
/// NavMesh donde está el cuerpo y, si se indica, el Animator usa otro controlador.
///
/// No decide nada de la historia ni del grupo: quién toma el mando lo pide otro sistema con
/// <see cref="TomarElMando"/> y <see cref="DarALaIA"/>. Los cuerpos se apuntan solos en
/// <see cref="Cuerpos"/> mientras están activos.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1100)]
public sealed class PilotoDelCuerpo : MonoBehaviour
{
    [Tooltip("Componentes que solo funcionan mientras lleva el cuerpo el jugador.")]
    [SerializeField] private Behaviour[] conElJugador = Array.Empty<Behaviour>();

    [Tooltip("Componentes que solo funcionan mientras lleva el cuerpo la IA.")]
    [SerializeField] private Behaviour[] conLaIA = Array.Empty<Behaviour>();

    [Tooltip("Controlador del Animator mientras lo lleva la IA. Vacío: el mismo que con el jugador.")]
    [SerializeField] private RuntimeAnimatorController animadorConLaIA;

    [Tooltip("El Animator aplica el movimiento raíz de las animaciones mientras lo lleva el jugador.")]
    [SerializeField] private bool movimientoRaizConElJugador = true;

    [Tooltip("El Animator aplica el movimiento raíz de las animaciones mientras lo lleva la IA.")]
    [SerializeField] private bool movimientoRaizConLaIA;

    [Tooltip("Etiqueta del cuerpo mientras lo lleva el jugador.")]
    [SerializeField] private string etiquetaConElJugador = "Player";

    [Tooltip("Etiqueta del cuerpo mientras lo lleva la IA.")]
    [SerializeField] private string etiquetaConLaIA = "Untagged";

    [Tooltip("Al aparecer lo lleva el jugador. Quien reparte el mando puede cambiarlo nada más empezar.")]
    [SerializeField] private bool empiezaConElJugador;

    private static readonly List<PilotoDelCuerpo> s_cuerpos = new List<PilotoDelCuerpo>(4);

    /// Los cuerpos activos en escena.
    public static IReadOnlyList<PilotoDelCuerpo> Cuerpos => s_cuerpos;

    /// Un cuerpo se ha activado (y se ha apuntado en <see cref="Cuerpos"/>).
    public static event Action<PilotoDelCuerpo> Aparece;
    /// Un cuerpo se ha desactivado o destruido (y ha salido de <see cref="Cuerpos"/>).
    public static event Action<PilotoDelCuerpo> Desaparece;

    /// Ha cambiado quién lleva este cuerpo.
    public event Action<PilotoDelCuerpo> CambioDePiloto;

    /// Lo lleva el jugador.
    public bool LoLlevaElJugador { get; private set; }

    /// Lo lleva el jugador al aparecer (dato del prefab).
    public bool EmpiezaConElJugador => empiezaConElJugador;

    public IReadOnlyList<Behaviour> ConElJugador => conElJugador;
    public IReadOnlyList<Behaviour> ConLaIA => conLaIA;

    private Animator _animador;
    private PlayerDialogueAnimator _animadorDeDialogo;
    private RuntimeAnimatorController _animadorConElJugador;
    private Rigidbody _rb;
    private NavMeshAgent _agente;
    private bool _preparado;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_cuerpos.Clear();
        Aparece = null;
        Desaparece = null;
    }
#endif

    private void Awake()
    {
        Preparar();
        Poner(empiezaConElJugador, avisar: false);
    }

    // Los componentes de las dos pilas despiertan después que este y alguno toca la física o el
    // Animator en su Awake: se vuelve a dejar todo como pide el piloto antes del primer frame.
    private void Start() => Poner(LoLlevaElJugador, avisar: false);

    private void OnEnable()
    {
        if (!s_cuerpos.Contains(this)) s_cuerpos.Add(this);
        Aparece?.Invoke(this);
    }

    private void OnDisable()
    {
        s_cuerpos.Remove(this);
        Desaparece?.Invoke(this);
    }

    /// El jugador pasa a llevar este cuerpo.
    public void TomarElMando()
    {
        if (LoLlevaElJugador) return;
        Poner(true, avisar: true);
    }

    /// La IA pasa a llevar este cuerpo.
    public void DarALaIA()
    {
        if (!LoLlevaElJugador) return;
        Poner(false, avisar: true);
    }

    /// El piloto del cuerpo al que pertenece ese objeto (él mismo o un hijo). Null si no es de un
    /// cuerpo con piloto.
    public static PilotoDelCuerpo De(Component c) => c != null ? c.GetComponentInParent<PilotoDelCuerpo>(true) : null;

    private void Preparar()
    {
        if (_preparado) return;
        _preparado = true;
        _animador = GetComponent<Animator>();
        _animadorDeDialogo = GetComponentInChildren<PlayerDialogueAnimator>(true);
        _animadorConElJugador = _animador != null ? _animador.runtimeAnimatorController : null;
        _rb = GetComponent<Rigidbody>();
        _agente = GetComponent<NavMeshAgent>();
    }

    private void Poner(bool conJugador, bool avisar)
    {
        Preparar();
        LoLlevaElJugador = conJugador;

        // Primero se apaga lo que deja de mandar (suelta modos, suscripciones y bloqueos en su
        // OnDisable) y después se enciende lo que manda.
        Encender(conJugador ? conLaIA : conElJugador, false);

        if (_rb != null)
        {
            if (!conJugador && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = !conJugador;
        }

        if (_animador != null)
        {
            if (animadorConLaIA != null)
            {
                var controlador = conJugador ? _animadorConElJugador : animadorConLaIA;
                if (_animador.runtimeAnimatorController != controlador) _animador.runtimeAnimatorController = controlador;
            }
            _animador.applyRootMotion = conJugador ? movimientoRaizConElJugador : movimientoRaizConLaIA;
        }

        string etiqueta = conJugador ? etiquetaConElJugador : etiquetaConLaIA;
        if (!string.IsNullOrEmpty(etiqueta) && !CompareTag(etiqueta)) gameObject.tag = etiqueta;

        Encender(conJugador ? conElJugador : conLaIA, true);

        if (!conJugador && _agente != null && _agente.isActiveAndEnabled && !_agente.isOnNavMesh
            && NavMesh.SamplePosition(transform.position, out var punto, 3f, NavMesh.AllAreas))
            _agente.Warp(punto.position);

        if (conJugador && avisar && _animador != null)
        {
            // La IA puede dejar la capa base en un gesto o con el reloj de animación parado.
            _animador.speed = 1f;
            _animadorDeDialogo?.AlRecuperarElControl();
            _animadorDeDialogo?.ReturnToLocomotion(0.08f);
            AjustarPasoDelRelevo(Vector3.zero);
        }

        if (avisar) CambioDePiloto?.Invoke(this);
    }

    /// Ajusta la primera pose a la velocidad que hereda el cuerpo al recibir el mando.
    internal void AjustarPasoDelRelevo(Vector3 velocidad)
    {
        if (_animador == null) return;
        velocidad.y = 0f;
        float paso = Game.NPC.Common.NavMeshAgentUtility.FactorDeLocomocion(velocidad.magnitude);
        _animador.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, paso);
    }

    private static void Encender(Behaviour[] lista, bool encender)
    {
        if (lista == null) return;
        for (int i = 0; i < lista.Length; i++)
        {
            var b = lista[i];
            if (b != null && b.enabled != encender) b.enabled = encender;
        }
    }

}
