using System;
using UnityEngine;
using Core;
using Invector.vCharacterController;

/// <summary>
/// Esquiva del jugador: un dash rodando hacia donde apunta el stick (sin stick, hacia atrás): sale
/// disparado y frena al final, dejando un rastro detrás mientras dura. Se devuelve el control a mitad del clip, sin frenar a cero y
/// sin fundido largo, y si se mantiene el stick sigue corriendo al acabar. Con unos instantes de
/// invulnerabilidad al empezar y una espera corta antes de la siguiente. El movimiento lo hace <see cref="VolteretaDelJugador"/> (recorta contra
/// paredes, aterriza en el suelo real y bloquea moverse y lanzar mientras dura); la animación se
/// elige entre cuatro (adelante, atrás, izquierda, derecha) según la dirección respecto a donde
/// mira el personaje, así que con un objetivo fijado rueda en lateral sin dejar de mirarlo.
/// El botón lo lee <see cref="PlayerCombatInput"/>. Ver INC-1349.
/// </summary>
[DisallowMultipleComponent]
public class EsquivaDelJugador : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Voltereta del personaje. Se busca sola si está vacía.")]
    [SerializeField] private VolteretaDelJugador voltereta;
    [Tooltip("Vida del personaje (para la invulnerabilidad). Se busca sola si está vacía.")]
    [SerializeField] private PlayerHealthSystem salud;

    [Header("Movimiento")]
    [Tooltip("Metros que recorre la esquiva (menos si hay una pared).")]
    [SerializeField, Min(0.5f)] private float distancia = DistanciaPorDefecto;
    [Tooltip("Segundos del clip entero (el clip se acelera para durar eso); el control vuelve antes, en 'devuelveElControlEn'.")]
    [SerializeField, Min(0.1f)] private float duracionAtras = DuracionPorDefecto;
    [Tooltip("Segundos de desplazamiento del dash en suelo y en aire.")]
    [SerializeField, Min(0.1f)] private float duracionDelDash = 0.22f;
    [Tooltip("Punto del clip (0-1) en el que acaba el avance y se devuelve el control; lo que queda del clip se corta.")]
    [SerializeField, Range(0.3f, 1f)] private float devuelveElControlEn = DevuelveElControlPorDefecto;
    [Tooltip("Velocidad de la animación de rodar (1 = la del clip). Más rápida = esquiva más corta: el recorrido va con el clip.")]
    [SerializeField, Range(0.5f, 4f)] private float velocidad = VelocidadPorDefecto;
    [Tooltip("Saltito al rodar, en alturas de la cabeza (0 = pegado al suelo).")]
    [SerializeField, Range(0f, 1f)] private float saltito = SaltitoPorDefecto;

    /// <summary>Valores de partida (el montaje los vuelve a poner en el cuerpo del grupo).</summary>
    public const float VelocidadPorDefecto = 1.4f;
    public const float DistanciaPorDefecto = 4.5f;
    public const float DuracionPorDefecto = 0.5f;
    public const float EsperaPorDefecto = 0.18f;
    /// Fundido (s) del clip de rodar al entrar, y a la locomoción al salir con el stick mantenido o sin él.
    public const float FundidoDeEntrada = 0.04f;
    public const float FundidoDeSalidaCorriendo = 0.05f;
    public const float FundidoDeSalidaParado = 0.12f;
    public const float SaltitoPorDefecto = 0f;
    [Tooltip("Inclinación mínima del stick para rodar hacia él; por debajo, rueda hacia atrás.")]
    [SerializeField, Range(0.05f, 0.9f)] private float stickMinimo = 0.3f;

    [Header("Salto del león (con el stick)")]
    [Tooltip("Con el stick, salta hacia donde apunta y da la voltereta en el aire mientras baja. Si no, rueda sin saltar.")]
    // Apagado desde el 20 nov 2026 (Raúl: «está mal el esquivar, vamos a usar la primera versión
    // que trae el propio pack»): con o sin stick, rueda con las animaciones del paquete
    // (RollFWD/BWD/LFT/RGT) hacia donde apunta, sin salto. El salto con voltereta en el aire
    // (INC-1374) sigue aquí por si se quiere volver a probar.
    [SerializeField] private bool saltoDelLeon = false;
    [Tooltip("Pose de cuerpo entero del salto (la crea la tabla de poses de actuación).")]
    [SerializeField] private string estadoDelSalto = EstadoDelSaltoPorDefecto;
    [Tooltip("Altura del salto, en alturas de la cabeza. Bajo = rasante, como un dash.")]
    [SerializeField, Range(0f, 2f)] private float alturaDelSalto = AlturaDelSaltoPorDefecto;
    [Tooltip("Metros que recorre en el aire, a velocidad constante (menos si hay una pared).")]
    [SerializeField, Min(0f)] private float distanciaDelSalto = DistanciaDelSaltoPorDefecto;
    [Tooltip("Segundos en el aire, del despegue a tocar suelo (voltereta incluida).")]
    [SerializeField, Min(0.1f)] private float duracionDelSalto = DuracionDelSaltoPorDefecto;
    [Tooltip("Metros de más que recorre el arco con la voltereta (se suman a los del salto).")]
    [SerializeField, Min(0f)] private float distanciaRodando = DistanciaRodandoPorDefecto;

    [Tooltip("Pose de cuerpo entero en lo alto: brazos abiertos y piernas juntas, preparando la voltereta.")]
    [SerializeField] private string estadoDePreparar = EstadoDePrepararPorDefecto;
    [Tooltip("Voltereta en el aire mientras baja (el clip se acelera para caber).")]
    [SerializeField] private string estadoDeLaVoltereta = "JumpAirSpin_InPlace_NoWeapon";
    [Tooltip("Caída de un salto normal, al tocar suelo.")]
    [SerializeField] private string estadoDeLaCaida = "JumpEnd_InPlace_NoWeapon";
    [Tooltip("Parte del arco (0-1) en la que deja de subir y se prepara.")]
    [SerializeField, Range(0f, 1f)] private float empiezaAPrepararEn = 0.35f;
    [Tooltip("Parte del arco (0-1) en la que empieza la voltereta.")]
    [SerializeField, Range(0f, 1f)] private float empiezaLaVolteretaEn = 0.5f;
    [Tooltip("Segundos de la caída en el suelo antes de devolver el control.")]
    [SerializeField, Min(0f)] private float segundosDeCaida = 0.12f;

    /// <summary>Pose de lo alto del salto (la crea la tabla de poses de actuación).</summary>
    public const string EstadoDePrepararPorDefecto = "Esquiva_Preparar";

    /// <summary>Valores de partida del salto del león (el montaje los pone en el cuerpo del grupo).</summary>
    public const float AlturaDelSaltoPorDefecto = 0.9f;
    public const float DistanciaDelSaltoPorDefecto = 3.2f;
    public const float DuracionDelSaltoPorDefecto = 0.7f;
    public const float DistanciaRodandoPorDefecto = 2.2f;
    public const float DevuelveElControlPorDefecto = 0.7f;

    /// <summary>Pose del salto del león (la crea la tabla de poses de actuación).</summary>
    public const string EstadoDelSaltoPorDefecto = "Esquiva_Salto";

    [Header("Reglas")]
    [Tooltip("Segundos de invulnerabilidad desde que empieza a rodar.")]
    [SerializeField, Min(0f)] private float segundosInvulnerable = 0.3f;
    [Tooltip("Segundos tras acabar una esquiva antes de poder empezar otra.")]
    [SerializeField, Min(0f)] private float espera = EsperaPorDefecto;
    [Tooltip("Segundos que se guarda una pulsación si aún no se puede rodar (p. ej. al acabar de lanzar).")]
    [SerializeField, Min(0f)] private float guardarPulsacion = 0.2f;

    [Header("Animator")]
    [SerializeField] private string estadoAdelante = "RollFWD_Battle_InPlace_NoWeapon";
    [SerializeField] private string estadoAtras = "RollBWD_Battle_InPlace_NoWeapon";
    [SerializeField] private string estadoIzquierda = "RollLFT_Battle_InPlace_NoWeapon";
    [SerializeField] private string estadoDerecha = "RollRGT_Battle_InPlace_NoWeapon";
    [Tooltip("Parámetro float del Animator que multiplica la velocidad de los estados de rodar.")]
    [SerializeField] private string parametroDeVelocidad = ParametroDeVelocidadPorDefecto;

    /// <summary>Parámetro que crea el montaje en el Animator para la velocidad de rodar.</summary>
    public const string ParametroDeVelocidadPorDefecto = "VelocidadDeEsquiva";

    [Header("Rastro")]
    [Tooltip("Rastro que deja el dash. Vacío = uno sencillo creado por código.")]
    [SerializeField] private TrailRenderer rastro;
    [Tooltip("Altura del rastro sobre los pies, en metros.")]
    [SerializeField, Min(0f)] private float alturaDelRastro = 0.32f;
    [Tooltip("Segundos que tarda el rastro en apagarse.")]
    [SerializeField, Min(0.05f)] private float duracionDelRastro = 0.10f;
    [SerializeField, Min(0.01f)] private float anchoDelRastro = 0.23f;
    [SerializeField] private Color colorDelRastro = new Color(0.85f, 0.93f, 1f, 0.32f);

    [Header("Sonido")]
    [Tooltip("Clave de SFX al empezar a rodar. Vacío = sin sonido.")]
    [SerializeField] private string sfxAlRodar = "";

    private vThirdPersonController _controller;
    private PlayerActionManager _acciones;
    private MagicCaster _caster;
    private Camera _camara;
    private Animator _animator;
    private int _hashVelocidad;
    private bool _tieneVelocidad;
    private float _largoAdelante, _largoAtras, _largoIzquierda, _largoDerecha;
    private bool _mia;
    private bool _rastroDuranteImpulso;
    private bool _dashAereo;
    private bool _dashAereoConsumido;
    private float _finDelDashAereo;
    private Vector3 _direccion;
    private Vector3 _entradaAntes;
    private Vector3 _velocidadHorizontalAntes;
    private float _velocidadAntes, _magnitudAntes;
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private float _libreDesde;
    private float _invulnerableHasta = -1f;
    private float _guardadaHasta = -1f;
    private Vector2 _stickGuardado;

    /// <summary>Hay una esquiva en curso.</summary>
    public bool EnCurso => _mia;

    /// <summary>Está en los instantes invulnerables de la esquiva: no le alcanzan ni golpes ni derribos.</summary>
    public bool EsInvulnerable => _mia && Time.time < _invulnerableHasta;

    /// <summary>Ha empezado una esquiva (dirección en mundo).</summary>
    public event Action<Vector3> OnEsquiva;

    void Awake()
    {
        if (!voltereta) voltereta = GetComponentInParent<VolteretaDelJugador>();
        if (!salud) salud = GetComponentInParent<PlayerHealthSystem>();
        _controller = GetComponentInParent<vThirdPersonController>();
        _acciones = GetComponentInParent<PlayerActionManager>();
        _caster = GetComponentInParent<MagicCaster>();
        if (_controller) _controller.TryGetComponent(out _animator);
        if (!_animator) _animator = GetComponentInChildren<Animator>();
        _hashVelocidad = Animator.StringToHash(parametroDeVelocidad);
        _tieneVelocidad = TieneParametroFloat(_hashVelocidad);
        _largoAdelante = LargoDelClip(estadoAdelante);
        _largoAtras = LargoDelClip(estadoAtras);
        _largoIzquierda = LargoDelClip(estadoIzquierda);
        _largoDerecha = LargoDelClip(estadoDerecha);
        if (!rastro) rastro = CrearRastro();
        if (rastro) rastro.emitting = false;
    }

    /// Estela baja que se estrecha hacia atrás.
    private TrailRenderer CrearRastro()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        var go = new GameObject("_RastroDelDash");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * alturaDelRastro;
        var t = go.AddComponent<TrailRenderer>();
        t.material = new Material(shader);
        t.time = duracionDelRastro;
        t.minVertexDistance = 0.05f;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        t.widthMultiplier = anchoDelRastro;
        var opaco = colorDelRastro;
        var transparente = colorDelRastro; transparente.a = 0f;
        t.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(opaco, 0f), new GradientColorKey(opaco, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(opaco.a, 0f), new GradientAlphaKey(0f, 1f) }
        };
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.receiveShadows = false;
        t.alignment = LineAlignment.View;
        return t;
    }

    private void Rastro(bool encendido)
    {
        if (!rastro || rastro.emitting == encendido) return;
        if (encendido) rastro.Clear();
        rastro.emitting = encendido;
    }

    /// Segundos del clip que usa ese estado (el clip se llama como el estado en el paquete), o 0.
    private float LargoDelClip(string estado)
    {
        if (!_animator || _animator.runtimeAnimatorController == null || string.IsNullOrEmpty(estado)) return 0f;
        var clips = _animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].name.EndsWith(estado, StringComparison.Ordinal)) return clips[i].length;
        return 0f;
    }

    /// Velocidad del Animator para que un clip de 'largo' segundos dure 'segundos'.
    private float VelocidadPara(float largo, float segundos)
    {
        if (largo <= 0.01f || segundos <= 0.01f) return velocidad;
        return Mathf.Clamp(largo / segundos, 0.5f, 4f);
    }

    private bool TieneParametroFloat(int hash)
    {
        if (!_animator || _animator.runtimeAnimatorController == null) return false;
        for (int i = 0; i < _animator.parameterCount; i++)
        {
            var p = _animator.GetParameter(i);
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Float) return true;
        }
        return false;
    }

    void OnEnable()
    {
        if (voltereta) voltereta.OnTerminada += AlTerminar;
    }

    void OnDisable()
    {
        if (voltereta) voltereta.OnTerminada -= AlTerminar;
        if (_dashAereo && _controller) _controller.SoltarImpulsoAereo();
        else if (_mia && voltereta) voltereta.Cancelar();
        _dashAereo = false;
        _mia = false;
        Rastro(false);
        _guardadaHasta = -1f;
    }

    void Update()
    {
        if (_controller && _controller.PisandoSuelo) _dashAereoConsumido = false;
        if (_dashAereo && (Time.time >= _finDelDashAereo || !_controller ||
            !_controller.isActiveAndEnabled || _controller.PisandoSuelo || _controller.suppressAirMovement))
        {
            if (_controller) _controller.SoltarImpulsoAereo();
            AlTerminar();
        }

        if (_mia && _rastroDuranteImpulso && voltereta)
        {
            float progreso = voltereta.ProgresoDesdeElSuelo;
            Rastro(progreso < 0.5f);
        }

        if (_guardadaHasta < 0f) return;
        if (Time.time > _guardadaHasta) { _guardadaHasta = -1f; return; }
        if (Intentar(_stickGuardado)) _guardadaHasta = -1f;
    }

    /// <summary>
    /// Pide una esquiva con la inclinación del stick de movimiento (relativa a la cámara). Si
    /// ahora no se puede pero se podrá enseguida, la pulsación se guarda un instante.
    /// </summary>
    public void Pedir(Vector2 stick)
    {
        if (Intentar(stick)) { _guardadaHasta = -1f; return; }
        _stickGuardado = stick;
        _guardadaHasta = Time.time + guardarPulsacion;
    }

    private bool Intentar(Vector2 stick)
    {
        if (!PuedeEsquivar()) return false;

        Vector3 direccion = DireccionEnMundo(stick);
        bool conStick = stick.sqrMagnitude >= stickMinimo * stickMinimo;
        bool enAire = _controller && !_controller.PisandoSuelo;
        GuardarImpulso();
        _rastroDuranteImpulso = !enAire && !(saltoDelLeon && conStick);
        float ventanaInvulnerable = segundosInvulnerable;

        if (enAire)
        {
            if (_dashAereoConsumido) return false;
            if (!conStick) direccion = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (!_controller.PedirImpulsoAereo(direccion, distancia, duracionDelDash)) return false;
            _dashAereoConsumido = _dashAereo = true;
            _finDelDashAereo = Time.time + duracionDelDash;
            ventanaInvulnerable = Mathf.Min(ventanaInvulnerable, duracionDelDash);
        }
        else if (saltoDelLeon && conStick)
        {
            // Un solo arco (dibujo de Raúl, 20 nov 2026): sube, en lo alto abre los brazos y junta
            // las piernas preparando la voltereta, la da mientras baja y cae como un salto normal.
            // Antes esperaba a caer del salto para rodar por el suelo.
            Quaternion antes = transform.rotation;
            transform.rotation = Quaternion.LookRotation(direccion, Vector3.up);
            var salto = new VolteretaDelJugador.SaltoConVoltereta
            {
                altura = alturaDelSalto,
                desplazamiento = direccion * (distanciaDelSalto + distanciaRodando),
                duracion = duracionDelSalto,
                estadoSubida = estadoDelSalto,
                estadoPreparar = estadoDePreparar,
                estadoVoltereta = estadoDeLaVoltereta,
                estadoCaida = estadoDeLaCaida,
                empiezaAPrepararEn = empiezaAPrepararEn,
                empiezaLaVolteretaEn = empiezaLaVolteretaEn,
                caida = segundosDeCaida,
            };
            if (!voltereta.ConVolteretaEnElAire(salto))
            {
                transform.rotation = antes;
                return false;
            }
        }
        else
        {
            string estado = EstadoPara(direccion, out float largo);
            if (_tieneVelocidad) _animator.SetFloat(_hashVelocidad, VelocidadPara(largo, duracionAtras));
            var rodar = new VolteretaDelJugador.Tramo
            {
                altura = saltito,
                desplazamiento = direccion * distancia,
                estado = estado,
                tramoDeMovimiento = new Vector2(0f, devuelveElControlEn),
                terminaEn = devuelveElControlEn,
                duracionFija = duracionDelDash / devuelveElControlEn,
                avance = VolteretaDelJugador.Avance.Frenando,
                velocidadDeSalida = conStick ? Mathf.Max(0f, Vector3.Dot(_velocidadHorizontalAntes, direccion)) : 0f,
                fundidoDeEntrada = FundidoDeEntrada,
                fundidoDeSalida = conStick ? FundidoDeSalidaCorriendo : FundidoDeSalidaParado
            };
            if (!voltereta.DesdeElSuelo(rodar)) return false;
            ventanaInvulnerable = Mathf.Min(ventanaInvulnerable, duracionDelDash);
        }

        _direccion = direccion;
        _mia = true;
        Rastro(true);
        _invulnerableHasta = Time.time + ventanaInvulnerable;
        if (salud) salud.HacerInvulnerable(ventanaInvulnerable, parpadeo: false);
        if (!string.IsNullOrEmpty(sfxAlRodar) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxAlRodar);
        OnEsquiva?.Invoke(direccion);
        return true;
    }

    private bool PuedeEsquivar()
    {
        if (!isActiveAndEnabled || !voltereta || voltereta.EnCurso) return false;
        if (Time.time < _libreDesde) return false;
        if (salud && !salud.IsAlive) return false;
        if (_acciones && !_acciones.CanUse(PlayerAbility.Roll)) return false;
        if (_controller && _controller.suppressAirMovement) return false;
        if (_caster && _caster.IsCasting) return false;
        return true;
    }

    private void AlTerminar()
    {
        if (!_mia) return;
        _mia = false;
        Rastro(false);
        _libreDesde = Time.time + espera;
        if (!_dashAereo) RecuperarImpulso();
        _dashAereo = false;
    }

    /// La carrera que llevaba al empezar, para seguir corriendo al acabar en vez de arrancar de cero.
    private void GuardarImpulso()
    {
        _entradaAntes = _controller ? _controller.EntradaSuavizada : Vector3.zero;
        _velocidadAntes = _controller ? _controller.VelocidadDeMovimiento : 0f;
        _velocidadHorizontalAntes = _controller ? _controller.VelocidadHorizontal : Vector3.zero;
        _magnitudAntes = _animator ? _animator.GetFloat(HashInputMagnitude) : 0f;
    }

    private void RecuperarImpulso()
    {
        if (!_controller || _velocidadAntes <= 0.01f) return;
        Vector2 stick = GamepadInputReader.Move;
        if (stick.sqrMagnitude < stickMinimo * stickMinimo || (_acciones && !_acciones.CanUse(PlayerAbility.Move))) return;
        _controller.RecuperarImpulso(_entradaAntes, _velocidadAntes);
        Vector3 direccion = DireccionEnMundo(stick);
        _controller.RecuperarVelocidadHorizontal(direccion * Mathf.Max(0f, Vector3.Dot(_velocidadHorizontalAntes, direccion)));
        if (_animator) _animator.SetFloat(HashInputMagnitude, _magnitudAntes);
    }

    /// Dirección plana en mundo: el stick respecto a la cámara o, sin stick, hacia atrás.
    private Vector3 DireccionEnMundo(Vector2 stick)
    {
        if (stick.sqrMagnitude < stickMinimo * stickMinimo)
        {
            Vector3 atras = -transform.forward;
            atras.y = 0f;
            return atras.sqrMagnitude > 0.0001f ? atras.normalized : Vector3.back;
        }

        if (_camara == null || !_camara.isActiveAndEnabled) _camara = Camera.main;
        Transform referencia = _camara != null ? _camara.transform : transform;
        Vector3 delante = referencia.forward; delante.y = 0f;
        Vector3 derecha = referencia.right; derecha.y = 0f;
        Vector3 dir = delante.normalized * stick.y + derecha.normalized * stick.x;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;
    }

    /// La animación que corresponde a la dirección vista desde el personaje (y lo que dura su clip).
    private string EstadoPara(Vector3 direccion, out float largo)
    {
        Vector3 local = transform.InverseTransformDirection(direccion);
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
        {
            if (local.z >= 0f) { largo = _largoAdelante; return estadoAdelante; }
            largo = _largoAtras; return estadoAtras;
        }
        if (local.x >= 0f) { largo = _largoDerecha; return estadoDerecha; }
        largo = _largoIzquierda; return estadoIzquierda;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!voltereta) voltereta = GetComponentInParent<VolteretaDelJugador>();
    }
#endif
}
