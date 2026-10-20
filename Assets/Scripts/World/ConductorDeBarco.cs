using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// El jugador a bordo de una embarcación.
///
/// Mientras está a bordo el jugador queda colgado del puesto de conducción (hijo del barco),
/// así que el barco se lo lleva, y su locomoción queda bloqueada: motor de Invector parado,
/// CharacterController desactivado y Rigidbody cinemático. Es la misma técnica que usa
/// <see cref="PlayerAmbientActivityHandler"/> para sentar al jugador en un banco, pero aquí el
/// punto de apoyo se mueve con el barco en vez de estar quieto en el mundo.
///
/// Al desembarcar se devuelve al jugador todo tal como estaba (padre, posición, física, input y
/// modo de acción).
/// </summary>
[DisallowMultipleComponent]
public class ConductorDeBarco : MonoBehaviour
{
    [Header("Embarque")]
    [Tooltip("Segundos que tarda el jugador en subir a la cubierta con el salto.")]
    [SerializeField] private float duracionDeSubida = 0.45f;

    [Header("Cámara")]
    [Tooltip("Metros extra que se aleja la cámara mientras el jugador está a bordo: a ras de cubierta " +
             "la cámara de exploración se queda dentro de la barco.")]
    [SerializeField] private float distanciaExtraEnBarco = 1.6f;

    private GameObject _jugador;
    private BarcoNavegable _barco;
    private Transform _padreAnterior;
    private CharacterController _controlador;
    private Rigidbody _cuerpo;
    private vThirdPersonMotor _motor;
    private vThirdPersonInput _entrada;
    private vThirdPersonController _controladorInvector;
    private VolteretaDelJugador _voltereta;
    private PlayerActionManager _acciones;
    private vThirdPersonCamera _camara;
    private float _distanciaDeCamaraNormal;
    private bool _controladorEstabaActivo;
    private bool _cuerpoEraCinematico;
    private bool _motorEstabaBloqueado;
    private bool _suppressEstabaActivo;
    private bool _controladorInvectorEstabaActivo;
    private Coroutine _acercamiento;

    /// <summary>True mientras hay un jugador a bordo.</summary>
    public bool TieneConductor => _jugador != null;

    /// <summary>El jugador a bordo (null si el barco está vacío).</summary>
    public GameObject Conductor => _jugador;

    void Awake()
    {
        _barco = GetComponent<BarcoNavegable>();
    }

    void Update()
    {
        // Si el modo de acción se ha reiniciado (muerte o respawn del jugador), el jugador ya no
        // está a bordo: se suelta para que no se quede bloqueado sin poder moverse.
        if (_jugador == null) return;
        if (_acciones != null && !_acciones.IsInMode(ActionMode.Boating)) SoltarJugador();
    }

    /// <summary>Deja al jugador donde está y devuelve su control, sin moverlo de sitio.</summary>
    void SoltarJugador()
    {
        var t = _jugador != null ? _jugador.transform : null;
        if (t != null) t.SetParent(_padreAnterior, true);
        RestaurarJugador();
        AlejarCamara(false);
        DevolverMusicaDelLugar();
        _jugador = null;
        _padreAnterior = null;
    }

    /// <summary>
    /// Aleja la cámara mientras el jugador va a bordo. La cámara de tercera persona va detrás
    /// del personaje a 2,5 m: dentro de un barco eso es estar dentro del casco, así que se
    /// separa un poco más y al bajar vuelve a su sitio.
    /// </summary>
    void AlejarCamara(bool alejar)
    {
        if (_camara == null) _camara = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (_camara == null) return;

        if (alejar)
        {
            _distanciaDeCamaraNormal = _camara.defaultDistance;
            _camara.defaultDistance = _distanciaDeCamaraNormal + distanciaExtraEnBarco;
        }
        else if (_distanciaDeCamaraNormal > 0f)
        {
            _camara.defaultDistance = _distanciaDeCamaraNormal;
            _distanciaDeCamaraNormal = 0f;
        }
    }

    /// <summary>Música propia del barco mientras se navega (si el barco tiene clip).</summary>
    void PonerMusicaDelBarco()
    {
        var audio = AudioService.Instance;
        if (audio == null || _barco == null) return;
        var clip = _barco.MusicaDelBarco;
        if (clip == null) return;
        audio.PlayMusic(clip, _barco.FundidoDeMusica);
    }

    /// <summary>
    /// Al bajar del barco vuelve la música del sitio. Se pide (no se pone) para que el relevo
    /// sea con gracia, como el de la cámara.
    /// </summary>
    void DevolverMusicaDelLugar()
    {
        var audio = AudioService.Instance;
        if (audio == null) return;
        audio.PedirMusicaDelLugar(1.8f);
    }

    /// <summary>
    /// Sube al jugador al puesto de conducción. Devuelve false si no se pudo (ya hay alguien a
    /// bordo, o el puesto no existe).
    /// </summary>
    public bool Embarcar(GameObject jugador, Transform puesto)
    {
        if (jugador == null || puesto == null || _jugador != null) return false;

        // Si venía nadando, sale del agua antes de tomar el barco: el nado y la conducción
        // escriben ambos en el Rigidbody del jugador y se pisarían.
        var nadadora = jugador.GetComponent<PlayerSwimmingController>();
        if (nadadora != null && nadadora.EstaEnElAgua) nadadora.SalirDelAgua();

        _jugador = jugador;

        var t = jugador.transform;
        _padreAnterior = t.parent;

        _controlador = jugador.GetComponent<CharacterController>();
        _cuerpo = jugador.GetComponent<Rigidbody>();
        _motor = jugador.GetComponent<vThirdPersonMotor>();
        _entrada = jugador.GetComponent<vThirdPersonInput>();
        _controladorInvector = jugador.GetComponentInParent<vThirdPersonController>();
        _voltereta = jugador.GetComponentInParent<VolteretaDelJugador>();
        _acciones = jugador.GetComponent<PlayerActionManager>();

        BloquearJugador();

        // Modo de acción propio: bloquea salto, sprint, voltereta, ataque, magia, porteo,
        // trepar y volar. El movimiento NO se bloquea (el barco lo lee) ni la interacción
        // (para poder volver a pulsar el botón y bajar).
        _acciones?.PushMode(ActionMode.Boating);

        // Colgado del puesto: a partir de aquí el barco se lleva al jugador.
        t.SetParent(puesto, true);

        AlejarCamara(true);
        PonerMusicaDelBarco();

        if (_acercamiento != null) StopCoroutine(_acercamiento);
        _acercamiento = StartCoroutine(ColocarEnElPuesto(puesto));
        return true;
    }

    /// <summary>Baja al jugador del barco y lo deja en <paramref name="destino"/>.</summary>
    public void Desembarcar(Vector3 destino)
    {
        if (_jugador == null) return;

        if (_acercamiento != null)
        {
            StopCoroutine(_acercamiento);
            _acercamiento = null;
        }

        var t = _jugador.transform;
        t.SetParent(_padreAnterior, true);
        t.position = destino;

        // Al bajar, el jugador mira hacia fuera del barco (hacia donde se baja), no de costado.
        Vector3 fuera = destino - transform.position;
        fuera.y = 0f;
        t.rotation = fuera.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(fuera.normalized, Vector3.up)
            : Quaternion.LookRotation(ProaMundo(), Vector3.up);

        RestaurarJugador();
        AlejarCamara(false);
        DevolverMusicaDelLugar();

        _jugador = null;
        _padreAnterior = null;
    }

    void OnDisable()
    {
        // El barco se va (escena, destruction): el jugador no puede quedarse colgado.
        if (_jugador != null) SoltarJugador();
    }

    // ── Bloqueo del jugador ───────────────────────────────────────────────────

    void BloquearJugador()
    {
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

        // El controlador de Invector se apaga mientras dura el salto al barco: si siguiera
        // vivo, el motor creería que el jugador sigue en el suelo y cortaría la voltereta.
        if (_controladorInvector != null)
        {
            _controladorInvectorEstabaActivo = _controladorInvector.enabled;
            _controladorInvector.enabled = false;
        }
    }

    void RestaurarJugador()
    {
        if (_controlador != null)
        {
            if (_controladorEstabaActivo) _controlador.enabled = true;
            _controladorEstabaActivo = false;
        }

        if (_cuerpo != null)
        {
            _cuerpo.isKinematic = _cuerpoEraCinematico;
            _cuerpoEraCinematico = false;
        }

        if (_motor != null)
        {
            _motor.lockMovement = _motorEstabaBloqueado;
            _motorEstabaBloqueado = false;
        }

        if (_entrada != null)
        {
            _entrada.SuppressMoveInput = _suppressEstabaActivo;
            _suppressEstabaActivo = false;
        }

        if (_controladorInvector != null)
        {
            if (_controladorInvectorEstabaActivo) _controladorInvector.enabled = true;
            _controladorInvectorEstabaActivo = false;
        }

        _acciones?.PopMode(ActionMode.Boating);

        // Grace period para que el botón de salto no se dispare en el frame de la restauración.
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
    }

    // ── El salto al puesto ───────────────────────────────────────────────────

    /// <summary>
    /// El salto a bordo: el jugador sube desde el agua hasta la cubierta con un salto con
    /// voltereta. La voltereta (el giro) la pone el sistema oficial
    /// (<see cref="VolteretaDelJugador.EnElAire"/>); la parábola la lleva este componente, que
    /// es quien lo acerca al puesto.
    /// </summary>
    IEnumerator ColocarEnElPuesto(Transform puesto)
    {
        var t = _jugador != null ? _jugador.transform : null;
        if (t == null)
        {
            _acercamiento = null;
            yield break;
        }

        Vector3 inicio = t.localPosition;
        Quaternion giroInicio = t.localRotation;
        // Al final el jugador mira hacia la proa del barco (no hacia el +Z local del modelo:
        // estos barcos no vienen con la proa en +Z).
        Quaternion giroFinal = _barco != null ? _barco.RotacionLocalDeProa : Quaternion.identity;

        if (_voltereta != null) _voltereta.EnElAire();

        float duracion = Mathf.Max(0.2f, duracionDeSubida);
        float altura = Mathf.Max(0.9f, Mathf.Abs(inicio.y) * 0.6f + 0.9f);

        for (float s = 0f; s < duracion; s += Time.deltaTime)
        {
            if (_jugador == null || t == null)
            {
                _acercamiento = null;
                yield break;
            }
            float u = Mathf.Clamp01(s / duracion);
            Vector3 p = Vector3.Lerp(inicio, Vector3.zero, u);
            p.y += altura * 4f * u * (1f - u);
            t.localPosition = p;
            t.localRotation = Quaternion.Slerp(giroInicio, giroFinal, u);
            yield return null;
        }

        if (_jugador != null && t != null)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = giroFinal;
        }
        _acercamiento = null;
    }

    Vector3 ProaMundo()
    {
        if (_barco != null) return _barco.DireccionDeProa;
        return transform.forward;
    }
}
