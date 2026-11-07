using Invector.vCharacterController;
using UnityEngine;

/// <summary>
/// El cuerpo del jugador llevado por algo que se mueve (un barco, una atracción): su locomoción
/// queda parada (motor de Invector, CharacterController y Rigidbody) para que lo mueva quien lo
/// lleva colgado, y al devolverlo se le restaura todo tal como estaba. No decide dónde va ni lo
/// cuelga: eso lo hace quien lo lleva. Ver INC-891.
/// </summary>
public sealed class PasajeroJugador
{
    private readonly ActionMode _modo;

    private CharacterController _controlador;
    private Rigidbody _cuerpo;
    private vThirdPersonMotor _motor;
    private vThirdPersonInput _entrada;
    private vThirdPersonController _controladorInvector;
    private Animator _animador;
    private bool _controladorEstabaActivo;
    private bool _cuerpoEraCinematico;
    private bool _motorEstabaBloqueado;
    private bool _suppressEstabaActivo;
    private bool _controladorInvectorEstabaActivo;

    /// <param name="modo">Modo de acción que se pone mientras va llevado (qué habilidades bloquea).</param>
    public PasajeroJugador(ActionMode modo) { _modo = modo; }

    /// <summary>El jugador llevado (null si no lleva a nadie).</summary>
    public GameObject Jugador { get; private set; }

    /// <summary>El padre que tenía el jugador antes de subir, para devolvérselo.</summary>
    public Transform PadreAnterior { get; private set; }

    /// <summary>Su gestor de modos de acción.</summary>
    public PlayerActionManager Acciones { get; private set; }

    /// <summary>True mientras lleva a alguien.</summary>
    public bool Ocupado => Jugador != null;

    /// <summary>Para la locomoción del jugador y pone el modo. Devuelve false si ya llevaba a alguien.</summary>
    public bool Tomar(GameObject jugador)
    {
        if (jugador == null || Jugador != null) return false;
        Jugador = jugador;
        PadreAnterior = jugador.transform.parent;

        _controlador = jugador.GetComponent<CharacterController>();
        _cuerpo = jugador.GetComponent<Rigidbody>();
        _motor = jugador.GetComponent<vThirdPersonMotor>();
        _entrada = jugador.GetComponent<vThirdPersonInput>();
        _controladorInvector = jugador.GetComponentInParent<vThirdPersonController>();
        _animador = jugador.GetComponentInChildren<Animator>();
        Acciones = jugador.GetComponent<PlayerActionManager>();

        if (_controlador != null)
        {
            _controladorEstabaActivo = _controlador.enabled;
            _controlador.enabled = false;
        }

        if (_cuerpo != null)
        {
            _cuerpoEraCinematico = _cuerpo.isKinematic;
            // La velocidad se pone a cero ANTES de marcarlo cinemático: al revés, Unity avisa
            // de que no se puede poner velocidad a un cuerpo cinemático.
            _cuerpo.linearVelocity = Vector3.zero;
            _cuerpo.isKinematic = true;
        }

        if (_motor != null)
        {
            _motorEstabaBloqueado = _motor.lockMovement;
            _motor.lockMovement = true;
        }

        if (_entrada != null)
        {
            _suppressEstabaActivo = _entrada.SuppressMoveInput;
            _entrada.SuppressMoveInput = true;
        }

        // El Animator del jugador lo lleva otro componente (el de animación de Invector), que sigue
        // vivo aunque el motor esté parado: sin bajar el InputMagnitude a cero, se queda
        // "caminando" en el sitio mientras lo llevan.
        if (_animador != null)
        {
            try { _animador.SetFloat(vAnimatorParameters.InputMagnitude, 0f); } catch { }
            try { _animador.SetFloat(vAnimatorParameters.InputHorizontal, 0f); } catch { }
            try { _animador.SetFloat(vAnimatorParameters.InputVertical, 0f); } catch { }
        }

        // El controlador de Invector se apaga: si siguiera vivo, el motor creería que el jugador
        // sigue en el suelo y pelearía con quien lo mueve (corta saltos y volteretas).
        if (_controladorInvector != null)
        {
            _controladorInvectorEstabaActivo = _controladorInvector.enabled;
            _controladorInvector.enabled = false;
        }

        Acciones?.PushMode(_modo);
        return true;
    }

    /// <summary>
    /// Lo baja: le devuelve su padre, lo pone de pie sobre suelo firme (el <c>groundLayer</c> del
    /// motor) junto a <paramref name="destino"/>, mirando hacia <paramref name="mirada"/>, y le devuelve
    /// todo con el estado de suelo puesto, para que no arranque cayendo ni metido en el suelo.
    /// </summary>
    public void DejarEn(Vector3 destino, Quaternion mirada)
    {
        if (Jugador == null) return;
        var t = Jugador.transform;
        var motor = _motor;
        t.SetParent(PadreAnterior, true);

        // Suelo firme junto al destino: no encima de una barandilla ni metido en una tarima.
        int suelo = motor != null ? motor.groundLayer.value : Physics.DefaultRaycastLayers;
        int noEstorba = SueloFirme.CapasQueNoEstorban | (1 << Jugador.layer);
        if (SueloFirme.Buscar(destino, 3f, 1f, suelo, noEstorba, out Vector3 pie)) destino = pie;
        Vector3 delante = mirada * Vector3.forward; delante.y = 0f;
        Quaternion giro = delante.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(delante.normalized) : t.rotation;

        t.SetPositionAndRotation(destino, giro);
        if (_cuerpo != null) { _cuerpo.position = destino; _cuerpo.rotation = giro; }
        Physics.SyncTransforms();

        Devolver();
        if (motor != null) motor.ForzarEnElSuelo();
    }

    /// <summary>Le devuelve al jugador la física, el input y el modo. No lo mueve ni le cambia el padre.</summary>
    public void Devolver()
    {
        if (Jugador == null) return;

        if (_controlador != null && _controladorEstabaActivo) _controlador.enabled = true;
        if (_cuerpo != null) _cuerpo.isKinematic = _cuerpoEraCinematico;
        if (_motor != null) _motor.lockMovement = _motorEstabaBloqueado;
        if (_entrada != null) _entrada.SuppressMoveInput = _suppressEstabaActivo;
        if (_controladorInvector != null && _controladorInvectorEstabaActivo) _controladorInvector.enabled = true;

        Acciones?.PopMode(_modo);

        // Margen para que el botón de salto no se dispare en el frame de la restauración.
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);

        _controladorEstabaActivo = _cuerpoEraCinematico = _motorEstabaBloqueado = false;
        _suppressEstabaActivo = _controladorInvectorEstabaActivo = false;
        _controlador = null; _cuerpo = null; _motor = null; _entrada = null;
        _controladorInvector = null; _animador = null;
        Jugador = null;
        PadreAnterior = null;
        Acciones = null;
    }
}
