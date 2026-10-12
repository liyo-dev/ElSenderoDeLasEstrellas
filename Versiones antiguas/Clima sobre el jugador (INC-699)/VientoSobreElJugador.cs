using Invector.vCharacterController;
using UnityEngine;

/// Lo que el viento le hace al jugador mientras sopla (ver DayNightCycle, viento):
/// - Andando CONTRA el viento va más despacio (tope de TopeDeVelocidadDelJugador, que también
///   acompasa la animación de locomoción) y se tapa la cara (Fear01 en la capa UpperBody, con las
///   piernas libres).
/// - Quieto, el viento lo arrastra despacio en su dirección (vThirdPersonMotor.empujeExterno), y
///   también se tapa la cara.
/// Solo con el jugador libre: en el suelo, en modo Default, sin bloqueo de PlayerLockService
/// (diálogos, cinemáticas) y sin otra cosa usando la capa UpperBody. Lo mueve DayNightCycle cada
/// frame; sin viento no hace nada.
public static class VientoSobreElJugador
{
    private const string GestoDeTaparse = "Fear01";
    private const string ReposoDeTorso = "UpperIdle";
    private const float FundidoDelGesto = 0.35f;   // segundos para entrar/salir del gesto
    private const float UmbralDeEntrada = 0.1f;    // stick por debajo de esto = quieto
    private const float UmbralDeContra = -0.3f;    // coseno a partir del cual se anda contra el viento

    private static readonly int HashGesto = Animator.StringToHash(GestoDeTaparse);
    private static readonly int HashReposo = Animator.StringToHash(ReposoDeTorso);
    private static readonly object Dueño = new object();

    private static GameObject _jugador;
    private static vThirdPersonMotor _motor;
    private static Animator _animator;
    private static PlayerActionManager _modos;
    private static int _capaTorso = -1;
    private static bool _gestoDisponible;

    private static bool _conTope;
    private static float _topeActual;
    private static bool _empujando;
    private static bool _gestoNuestro;   // la capa UpperBody la estamos usando nosotros
    private static float _pesoGesto;
    private static int _frameDelCruce;   // el Animator no refleja el CrossFade hasta su siguiente paso

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _jugador = null; _motor = null; _animator = null; _modos = null;
        _capaTorso = -1; _gestoDisponible = false;
        _conTope = false; _topeActual = 0f; _empujando = false; _gestoNuestro = false; _pesoGesto = 0f; _frameDelCruce = 0;
    }
#endif

    /// Un paso del efecto. fuerza 0-1 (ya a cero en interiores), direccion horizontal normalizada
    /// hacia donde sopla.
    public static void Actualizar(float fuerza, Vector3 direccion, float velocidadMinimaContra,
        float arrastreQuieto, float dt)
    {
        if (fuerza <= 0.001f || !Cachear())
        {
            Soltar();
            return;
        }

        bool libre = _motor.enabled && _motor.PisandoSuelo && Time.timeScale > 0f
                     && (_modos == null || _modos.Top == ActionMode.Default)
                     && !(PlayerLockService.HasInstance && PlayerLockService.Instance.IsLocked);
        if (!libre)
        {
            Soltar();
            return;
        }

        Vector3 mov = _motor.DireccionDeMovimiento;
        mov.y = 0f;
        bool quieto = _motor.MagnitudDeEntrada < UmbralDeEntrada || mov.sqrMagnitude < 0.0001f;

        // Cuánto de frente le da el viento: 0 de lado o a favor, 1 de cara.
        float contra = 0f;
        if (!quieto)
        {
            float coseno = Vector3.Dot(mov.normalized, direccion);
            contra = Mathf.InverseLerp(UmbralDeContra, -1f, coseno) * fuerza;
        }

        // Tope de velocidad contra el viento. Se compara con el sprint para que el tope solo
        // muerda cuando el viento sopla de verdad.
        if (contra > 0.01f)
        {
            float libreDeViento = Mathf.Max(_motor.freeSpeed.sprintSpeed, velocidadMinimaContra);
            float tope = Mathf.Lerp(libreDeViento, velocidadMinimaContra, contra);
            // Solo se reaplica al cambiar de verdad: Poner recalcula el tope de todos los sistemas.
            if (!_conTope || Mathf.Abs(tope - _topeActual) > 0.05f)
            {
                TopeDeVelocidadDelJugador.Poner(Dueño, tope);
                _topeActual = tope;
            }
            _conTope = true;
        }
        else QuitarTope();

        // Arrastre si está quieto.
        _motor.empujeExterno = quieto ? direccion * (arrastreQuieto * fuerza) : Vector3.zero;
        _empujando = quieto;

        bool taparse = (quieto && fuerza > 0.35f) || contra > 0.35f;
        ActualizarGesto(taparse, dt);
    }

    /// Lo quita todo de golpe, también el gesto de taparse si aún es nuestro.
    public static void Soltar()
    {
        QuitarTope();
        if (_empujando && _motor != null) _motor.empujeExterno = Vector3.zero;
        _empujando = false;
        if (_gestoNuestro) ActualizarGesto(false, Time.unscaledDeltaTime, inmediato: true);
    }

    private static void QuitarTope()
    {
        if (!_conTope) return;
        TopeDeVelocidadDelJugador.Quitar(Dueño);
        _conTope = false;
    }

    private static bool Cachear()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return false;
        if (jugador == _jugador && _motor != null) return true;

        Soltar();
        _jugador = jugador;
        _motor = jugador.GetComponent<vThirdPersonMotor>();
        _animator = jugador.GetComponent<Animator>();
        _modos = jugador.GetComponent<PlayerActionManager>();
        _capaTorso = _animator != null ? _animator.GetLayerIndex("UpperBody") : -1;
        _gestoDisponible = _capaTorso > 0 && _animator.HasState(_capaTorso, HashGesto);
        _gestoNuestro = false;
        _pesoGesto = 0f;
        return _motor != null;
    }

    private static void ActualizarGesto(bool taparse, float dt, bool inmediato = false)
    {
        if (!_gestoDisponible || _animator == null || !_animator.isActiveAndEnabled) { _gestoNuestro = false; return; }

        if (taparse && !_gestoNuestro)
        {
            // Solo si nadie más usa el torso (diálogo, magia, modo batalla...).
            if (_animator.GetLayerWeight(_capaTorso) > 0.05f) return;
            _animator.CrossFadeInFixedTime(HashGesto, FundidoDelGesto, _capaTorso);
            _gestoNuestro = true;
            _pesoGesto = _animator.GetLayerWeight(_capaTorso);
            _frameDelCruce = Time.frameCount;
        }
        if (!_gestoNuestro) return;

        // Si otro sistema ha cambiado el estado o el peso del torso, la capa ya no es nuestra: se
        // suelta sin tocarla para no pisarle el gesto.
        var estado = _animator.GetCurrentAnimatorStateInfo(_capaTorso);
        bool enGesto = estado.shortNameHash == HashGesto ||
                       (_animator.IsInTransition(_capaTorso) &&
                        _animator.GetNextAnimatorStateInfo(_capaTorso).shortNameHash == HashGesto);
        bool pesoAjeno = Mathf.Abs(_animator.GetLayerWeight(_capaTorso) - _pesoGesto) > 0.01f;
        if ((!enGesto && Time.frameCount - _frameDelCruce > 1) || pesoAjeno)
        {
            _gestoNuestro = false;
            return;
        }

        float objetivo = taparse ? 1f : 0f;
        _pesoGesto = inmediato && !taparse ? 0f : Mathf.MoveTowards(_pesoGesto, objetivo, dt / FundidoDelGesto);
        _animator.SetLayerWeight(_capaTorso, _pesoGesto);

        if (!taparse && _pesoGesto <= 0f)
        {
            if (_animator.HasState(_capaTorso, HashReposo))
                _animator.CrossFadeInFixedTime(HashReposo, 0f, _capaTorso);
            _gestoNuestro = false;
        }
    }
}
