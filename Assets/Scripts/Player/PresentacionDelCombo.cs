using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Todo lo que se ve y se oye del combo mágico de la Y (<see cref="ComboCastController"/>), sin
/// tocar su lógica: solo escucha sus eventos.
/// <list type="bullet">
/// <item>Al abrir: frenazo breve del tiempo, el personaje se eleva (<see cref="ElevacionVisual"/>),
/// ráfaga de polvo por el suelo, chispas que suben en espiral, luz cálida desde abajo, viento en
/// bucle y un círculo propio a los pies que aparece con un rebote.</item>
/// <item>Mientras teclea: sostiene entre las manos un orbe de energía (<see cref="ManosIK"/>), con un
/// temblor suave; la luz sale del orbe.</item>
/// <item>Con cada botón: el orbe crece y toma el color del botón, las manos se separan y dan un
/// empujón, salen chispas, se enciende un anillo del círculo (de dentro afuera), el círculo gira más
/// rápido y suena una nota más aguda.</item>
/// <item>Al cerrar: lanzado (el orbe sale disparado al frente, destello, el círculo se encoge y sube),
/// fallo (parpadea en rojo y se apaga), golpe (revienta) o cancelado (se apaga suave).</item>
/// </list>
/// Las animaciones usan tiempo real para que el círculo «salte» durante el frenazo. Ver INC-643.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // después de ElevacionVisual y ManosIK, para colocar el orbe entre las manos ya elevadas
[RequireComponent(typeof(ComboCastController))]
public class PresentacionDelCombo : MonoBehaviour
{
    private const string LoopDeViento = "ComboCirculo";

    [Header("Elevación")]
    [Tooltip("Metros que sube el personaje mientras teclea.")]
    [SerializeField, Min(0f)] private float altura = 0.35f;
    [Tooltip("Segundos (tiempo real) que tarda en subir.")]
    [SerializeField, Min(0f)] private float segundosSubida = 0.25f;
    [Tooltip("Segundos que tarda en bajar al lanzar el hechizo.")]
    [SerializeField, Min(0f)] private float bajadaAlLanzar = 0.12f;
    [Tooltip("Segundos que tarda en bajar si la secuencia falla.")]
    [SerializeField, Min(0f)] private float bajadaAlFallar = 0.25f;
    [Tooltip("Segundos que tarda en caer si le golpean.")]
    [SerializeField, Min(0f)] private float bajadaAlRomperse = 0.08f;
    [Tooltip("Segundos que tarda en bajar si se cierra sin teclear.")]
    [SerializeField, Min(0f)] private float bajadaAlCancelar = 0.2f;

    [Header("Orbe entre las manos")]
    [Tooltip("Material del orbe (Hovl GlowFree1).")]
    [SerializeField] private Material materialOrbe;
    [Tooltip("Diámetro del orbe al abrir, en metros.")]
    [SerializeField, Min(0f)] private float diametroOrbe = 0.16f;
    [Tooltip("Lo que crece el orbe con cada botón.")]
    [SerializeField, Min(0f)] private float crecimientoPorBoton = 0.05f;
    [SerializeField] private Color colorNucleo = new Color(1f, 0.95f, 0.85f, 1f);
    [Tooltip("Hueco entre las manos y el orbe.")]
    [SerializeField, Min(0f)] private float holguraManos = 0.04f;
    [Tooltip("Cuánto adelanta las manos respecto a la animación.")]
    [SerializeField] private float manosAdelante = 0.12f;
    [Tooltip("Cuánto sube las manos respecto a la animación.")]
    [SerializeField] private float manosArriba = 0.05f;
    [Tooltip("Amplitud del temblor de las manos, en metros.")]
    [SerializeField, Min(0f)] private float temblorManos = 0.012f;
    [SerializeField, Min(0f)] private float frecuenciaTemblor = 1.3f;
    [Tooltip("Empujón de las manos al frente con cada botón, en metros.")]
    [SerializeField, Min(0f)] private float empujePorBoton = 0.06f;
    [Tooltip("Metros que recorre el orbe al lanzar.")]
    [SerializeField, Min(0f)] private float distanciaLanzamiento = 1.5f;
    [Tooltip("Sin ManosIK: altura del orbe sobre los pies.")]
    [SerializeField] private float alturaOrbeSinManos = 0.7f;
    [Tooltip("Chispas que salen del orbe con cada botón.")]
    [SerializeField, Min(0)] private int chispasDelOrbe = 12;

    [Header("Frenazo al abrir")]
    [Tooltip("Escala de tiempo durante el frenazo (1 = sin frenazo).")]
    [SerializeField, Range(0f, 1f)] private float escalaFrenazo = 0.1f;
    [Tooltip("Duración del frenazo en segundos reales.")]
    [SerializeField, Min(0f)] private float duracionFrenazo = 0.12f;

    [Header("Círculo")]
    [Tooltip("Escala de todo el efecto (círculo, chispas y polvo).")]
    [SerializeField, Min(0.01f)] private float escalaGeneral = 1f;
    [Tooltip("Altura del círculo sobre el suelo.")]
    [SerializeField] private float alturaSobreElSuelo = 0.04f;
    [Tooltip("Capa base con runas (Hovl MagicCircle).")]
    [SerializeField] private Material materialBase;
    [Tooltip("Anillo exterior segmentado (Hovl TechCircle).")]
    [SerializeField] private Material materialExterior;
    [Tooltip("Anillo que se enciende con cada botón (Hovl Circle2).")]
    [SerializeField] private Material materialAnillo;
    [Tooltip("Resplandor del centro (Hovl GlowFree1).")]
    [SerializeField] private Material materialDestello;
    [SerializeField, Min(0f)] private float diametroBase = 2.2f;
    [SerializeField, Min(0f)] private float diametroExterior = 2.7f;
    [Tooltip("Diámetro del anillo del primer botón.")]
    [SerializeField, Min(0f)] private float diametroAnilloInterior = 0.7f;
    [Tooltip("Diámetro del anillo del último botón posible.")]
    [SerializeField, Min(0f)] private float diametroAnilloExterior = 1.9f;
    [Tooltip("Segundos que tarda el círculo en aparecer.")]
    [SerializeField, Min(0.01f)] private float duracionAparicion = 0.2f;
    [Tooltip("Giro del círculo en reposo, en grados por segundo.")]
    [SerializeField] private float giroBase = 30f;
    [Tooltip("Giro que se suma por cada botón tecleado.")]
    [SerializeField] private float giroPorBoton = 45f;
    [Tooltip("Multiplicador de brillo (los materiales de Hovl trabajan en HDR).")]
    [SerializeField, Min(0f)] private float intensidad = 2.4f;
    [SerializeField] private Color colorCirculo = new Color(1f, 0.78f, 0.35f, 1f);
    [Tooltip("Color de los anillos aún sin encender.")]
    [SerializeField] private Color colorApagado = new Color(1f, 0.85f, 0.6f, 0.12f);
    [Tooltip("Destello al lanzar el hechizo.")]
    [SerializeField] private Color colorLanzado = new Color(1f, 0.95f, 0.8f, 1f);
    [Tooltip("Color al disiparse por fallo.")]
    [SerializeField] private Color colorFallo = new Color(1f, 0.2f, 0.15f, 1f);
    [Tooltip("Segundos que dura el destello de un anillo al encenderse.")]
    [SerializeField, Min(0.01f)] private float destelloDeAnillo = 0.25f;

    [Header("Colores de los botones")]
    [SerializeField] private Color colorA = new Color(0.35f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color colorB = new Color(0.95f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color colorX = new Color(0.25f, 0.55f, 1f, 1f);
    [SerializeField] private Color colorY = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Cierre (segundos)")]
    [SerializeField, Min(0.01f)] private float cierreAlLanzar = 0.3f;
    [SerializeField, Min(0.01f)] private float cierreAlFallar = 0.3f;
    [SerializeField, Min(0.01f)] private float cierreAlRomperse = 0.1f;
    [SerializeField, Min(0.01f)] private float cierreAlCancelar = 0.2f;
    [Tooltip("Metros que sube el círculo mientras se desvanece al lanzar.")]
    [SerializeField] private float subidaAlLanzar = 0.5f;

    [Header("Viento")]
    [Tooltip("Material de las chispas que suben (Hovl Point).")]
    [SerializeField] private Material materialChispas;
    [Tooltip("Material del polvo de la ráfaga (Hovl SmokeFree1).")]
    [SerializeField] private Material materialPolvo;
    [SerializeField, Min(0f)] private float chispasPorSegundo = 45f;
    [Tooltip("Chispas extra que salen con cada botón.")]
    [SerializeField, Min(0)] private int chispasPorBoton = 10;
    [Tooltip("Velocidad de subida de las chispas, en metros por segundo.")]
    [SerializeField, Min(0f)] private float velocidadSubida = 2.2f;
    [Tooltip("Giro de la espiral de las chispas, en radianes por segundo.")]
    [SerializeField] private float giroEspiral = 3f;
    [Tooltip("Nubecillas de polvo de la ráfaga al abrir.")]
    [SerializeField, Min(0)] private int polvoEnRafaga = 20;
    [Tooltip("Velocidad con la que sale el polvo hacia fuera.")]
    [SerializeField, Min(0f)] private float velocidadRafaga = 4.5f;
    [SerializeField] private Color colorPolvo = new Color(0.95f, 0.9f, 0.8f, 0.55f);

    [Header("Luz")]
    [SerializeField] private Color colorLuz = new Color(1f, 0.75f, 0.4f, 1f);
    [SerializeField, Min(0f)] private float intensidadLuz = 3f;
    [Tooltip("Intensidad que se suma en el pulso de cada botón.")]
    [SerializeField, Min(0f)] private float intensidadPulso = 4f;
    [SerializeField, Min(0.01f)] private float duracionPulso = 0.25f;
    [SerializeField, Min(0f)] private float alcanceLuz = 4f;

    [Header("Sonido (claves del AudioGraphProfile)")]
    [SerializeField] private string sfxApertura = "Prologue_SpellInstantiate";
    [SerializeField] private string sfxRafaga = "Impacto_Viento_Pequeno";
    [Tooltip("Viento en bucle mientras el círculo está abierto.")]
    [SerializeField] private string sfxViento = "SFX_Prologo_Viento";
    [SerializeField, Range(0f, 1f)] private float volumenViento = 0.6f;
    [Tooltip("Nota de cada botón; sube de tono con cada uno.")]
    [SerializeField] private string sfxBoton = "RuneActivate";
    [Tooltip("Subida de tono por cada botón tecleado.")]
    [SerializeField, Min(0f)] private float subidaDeTono = 0.12f;
    [Tooltip("Fallo, golpe o círculo no disponible.")]
    [SerializeField] private string sfxFallo = "SpellFail";

    private enum Estado { Apagado, Abierto, Cerrando }

    private ComboCastController _combo;
    private ElevacionVisual _elevacion;
    private Transform _personaje;

    private Transform _raiz;
    private MeshRenderer _base, _exterior, _destello;
    private MeshRenderer[] _anillos;
    private Color[] _colorAnillo;
    private float[] _brilloAnillo;
    private Light _luz;
    private ManosIK _manos;
    private Transform _orbe;
    private MeshRenderer _nucleo, _halo;
    private ParticleSystem _chispasOrbe;
    private Mesh _quadVertical;
    private Camera _camara;
    private float _diamOrbe, _velDiamOrbe, _sepManos, _velSepManos, _empuje, _faseTemblor;
    private Color _colorOrbe;
    private Vector3 _orbeAlCerrar, _frenteAlCerrar;
    private ParticleSystem _chispas, _rafaga;
    private MaterialPropertyBlock _mpb;
    private Mesh _quad;

    private Estado _estado;
    private ComboCastController.Result _cierre;
    private float _tAbierto, _tCierre, _duracionCierre, _alfaAlCerrar;
    private int _encendidos;
    private float _giro, _velGiro;
    private float _pulso;
    private Color _colorPulso;
    private float _brilloDestello;
    private Color _colorDestello;
    private bool _frenazoActivo;
    private float _frenazoHasta;

    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _combo = GetComponent<ComboCastController>();
    }

    void OnEnable()
    {
        _combo.OnOpened += AlAbrir;
        _combo.OnInput += AlTeclear;
        _combo.OnClosed += AlCerrar;
        _combo.OnDenied += AlDenegar;
    }

    void OnDisable()
    {
        _combo.OnOpened -= AlAbrir;
        _combo.OnInput -= AlTeclear;
        _combo.OnClosed -= AlCerrar;
        _combo.OnDenied -= AlDenegar;

        LiberarFrenazo();
        if (_estado != Estado.Apagado)
        {
            DetenerViento(0f);
            if (_elevacion != null) _elevacion.Soltar(this, 0f);
            if (_manos != null) _manos.Soltar(this, 0f);
            if (_chispas != null) _chispas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Apagar();
        }
    }

    void OnDestroy()
    {
        if (_quad != null) Destroy(_quad);
        if (_quadVertical != null) Destroy(_quadVertical);
    }

    // ── Eventos del combo ──────────────────────────────────────────────────

    private void AlAbrir(IReadOnlyList<MagicSpellSO> _)
    {
        Construir();

        _estado = Estado.Abierto;
        _tAbierto = 0f;
        _encendidos = 0;
        for (int i = 0; i < _brilloAnillo.Length; i++) _brilloAnillo[i] = 0f;
        _velGiro = giroBase * 6f; // arranca girando rápido y se asienta
        _brilloDestello = 1f;
        _colorDestello = colorCirculo;
        _pulso = 1f;
        _colorPulso = colorCirculo;

        if (!_raiz.gameObject.activeSelf) _raiz.gameObject.SetActive(true);
        if (!_orbe.gameObject.activeSelf) _orbe.gameObject.SetActive(true);
        if (!_luz.enabled) _luz.enabled = true;
        if (_camara == null) _camara = Camera.main;

        _diamOrbe = 0f;
        _velDiamOrbe = 0f;
        _sepManos = holguraManos;
        _velSepManos = 0f;
        _empuje = 0f;
        _colorOrbe = colorCirculo;
        if (_manos != null) _manos.Sostener(this, 0.15f);

        var main = _chispas.main;
        main.startColor = colorCirculo;
        if (!_chispas.isEmitting) _chispas.Play();
        _rafaga.Play();
        _rafaga.Emit(polvoEnRafaga);

        if (escalaFrenazo < 1f && duracionFrenazo > 0f)
        {
            TimeScaleArbiterService.Request(this, escalaFrenazo);
            _frenazoActivo = true;
            _frenazoHasta = Time.unscaledTime + duracionFrenazo;
        }

        if (_elevacion != null) _elevacion.Elevar(this, altura, segundosSubida, tiempoReal: true);

        Sonar(sfxApertura);
        Sonar(sfxRafaga);
        if (!string.IsNullOrEmpty(sfxViento) && AudioService.Instance != null)
            AudioService.Instance.PlayLoopingSFX(LoopDeViento, sfxViento, volumenViento);
    }

    private void AlTeclear(IReadOnlyList<ComboButton> tecleado, IReadOnlyList<bool> _)
    {
        if (_estado != Estado.Abierto || tecleado.Count == 0) return;
        int i = tecleado.Count - 1;
        Color c = ColorDe(tecleado[i]);

        if (i < _anillos.Length)
        {
            _colorAnillo[i] = c;
            _brilloAnillo[i] = 1f;
        }
        _encendidos = Mathf.Min(tecleado.Count, _anillos.Length);

        _pulso = 1f;
        _colorPulso = c;
        _brilloDestello = Mathf.Max(_brilloDestello, 0.6f);
        _colorDestello = c;

        var main = _chispas.main;
        main.startColor = c;
        if (chispasPorBoton > 0) _chispas.Emit(chispasPorBoton);

        Sonar(sfxBoton, 1f + subidaDeTono * i);

        _colorOrbe = c;
        _empuje = 1f;
        if (chispasDelOrbe > 0)
        {
            var mainOrbe = _chispasOrbe.main;
            mainOrbe.startColor = c;
            _chispasOrbe.transform.position = _orbe.position;
            _chispasOrbe.Play();
            _chispasOrbe.Emit(chispasDelOrbe);
        }
    }

    private void AlCerrar(ComboCastController.Result resultado, MagicSpellSO _)
    {
        if (_estado != Estado.Abierto) return;

        _alfaAlCerrar = Mathf.Clamp01(_tAbierto / duracionAparicion);
        _orbeAlCerrar = _orbe.position;
        _frenteAlCerrar = _personaje.forward;
        _estado = Estado.Cerrando;
        _cierre = resultado;
        _tCierre = 0f;

        LiberarFrenazo();
        _chispas.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        switch (resultado)
        {
            case ComboCastController.Result.Cast:
                _duracionCierre = cierreAlLanzar;
                _brilloDestello = 1f;
                _colorDestello = colorLanzado;
                _pulso = 1f;
                _colorPulso = colorLanzado;
                Soltar(bajadaAlLanzar);
                SoltarManos(0.1f);
                DetenerViento(0.15f);
                break;
            case ComboCastController.Result.Fizzled:
                _duracionCierre = cierreAlFallar;
                _pulso = 1f;
                _colorPulso = colorFallo;
                Soltar(bajadaAlFallar);
                SoltarManos(0.25f);
                DetenerViento(0.2f);
                Sonar(sfxFallo);
                break;
            case ComboCastController.Result.Interrupted:
                _duracionCierre = cierreAlRomperse;
                Soltar(bajadaAlRomperse);
                SoltarManos(0.08f);
                DetenerViento(0.05f);
                Sonar(sfxFallo);
                break;
            default:
                _duracionCierre = cierreAlCancelar;
                Soltar(bajadaAlCancelar);
                SoltarManos(0.2f);
                DetenerViento(0.25f);
                break;
        }
    }

    private void AlDenegar(string _) => Sonar(sfxFallo);

    // ── Animación ──────────────────────────────────────────────────────────

    void Update()
    {
        if (_estado == Estado.Apagado) return;

        float dt = Time.unscaledDeltaTime;
        if (_frenazoActivo && Time.unscaledTime >= _frenazoHasta) LiberarFrenazo();

        float escala, alfa, subida = 0f, temblor = 0f;
        bool fallo = false;
        if (_estado == Estado.Abierto)
        {
            _tAbierto += dt;
            float k = Mathf.Clamp01(_tAbierto / duracionAparicion);
            escala = EaseOutBack(k, 1.7f);
            alfa = k;
        }
        else
        {
            _tCierre += dt;
            if (_tCierre >= _duracionCierre) { Apagar(); return; }
            float k = _tCierre / _duracionCierre;
            switch (_cierre)
            {
                case ComboCastController.Result.Cast:
                    escala = Mathf.Lerp(1f, 0.35f, k * k);
                    subida = subidaAlLanzar * k;
                    alfa = 1f - k;
                    break;
                case ComboCastController.Result.Fizzled:
                    fallo = true;
                    escala = 1f + 0.06f * k;
                    alfa = (1f - k) * (Mathf.Sin(_tCierre * 70f) > -0.2f ? 1f : 0.2f);
                    temblor = 0.05f * (1f - k);
                    break;
                case ComboCastController.Result.Interrupted:
                    escala = 1f + 0.25f * k;
                    alfa = 1f - k;
                    break;
                default:
                    escala = 1f;
                    alfa = (1f - k) * (1f - k);
                    break;
            }
            alfa *= _alfaAlCerrar;
        }

        // Giro: cada botón lo acelera.
        float objetivo = giroBase + giroPorBoton * _encendidos;
        _velGiro = Mathf.Lerp(_velGiro, objetivo, 1f - Mathf.Exp(-8f * dt));
        _giro = Mathf.Repeat(_giro + _velGiro * dt, 360f);

        _brilloDestello = Mathf.Max(0f, _brilloDestello - dt / 0.25f);
        _pulso = Mathf.Max(0f, _pulso - dt / duracionPulso);

        // Raíz del círculo.
        Vector3 pos = new Vector3(0f, alturaSobreElSuelo + subida, 0f);
        if (temblor > 0f)
        {
            Vector2 r = Random.insideUnitCircle * temblor;
            pos.x += r.x; pos.z += r.y;
        }
        _raiz.localPosition = pos;

        float s = escala * escalaGeneral;
        float brilloBase = 1f + 0.15f * _encendidos;
        Pintar(_base, diametroBase * s, _giro, Tinte(fallo ? colorFallo : colorCirculo, brilloBase, alfa));
        Pintar(_exterior, diametroExterior * s, -_giro * 0.6f, Tinte(fallo ? colorFallo : colorCirculo, 1f, alfa * 0.6f));

        int n = _anillos.Length;
        for (int i = 0; i < n; i++)
        {
            _brilloAnillo[i] = Mathf.Max(0f, _brilloAnillo[i] - dt / destelloDeAnillo);
            bool encendido = i < _encendidos;
            Color c = encendido ? (fallo ? colorFallo : _colorAnillo[i]) : colorApagado;
            float d = n > 1 ? Mathf.Lerp(diametroAnilloInterior, diametroAnilloExterior, i / (float)(n - 1)) : diametroAnilloInterior;
            float pop = 1f + 0.15f * _brilloAnillo[i];
            float sentido = (i & 1) == 0 ? 1.2f : -0.9f;
            Pintar(_anillos[i], d * s * pop, _giro * sentido, Tinte(c, 1f + 1.5f * _brilloAnillo[i], alfa));
        }

        float tamDestello = diametroBase * s * (0.5f + 0.6f * _brilloDestello);
        Pintar(_destello, tamDestello, 0f, Tinte(_colorDestello, 1f + _brilloDestello, alfa * _brilloDestello));

        // Luz desde el orbe.
        float luzBase = _estado == Estado.Abierto ? Mathf.Clamp01(_tAbierto / 0.12f) : alfa;
        _luz.intensity = intensidadLuz * luzBase + intensidadPulso * _pulso;
        _luz.color = Color.Lerp(colorLuz, _colorPulso, _pulso);

        AnimarOrbeYManos(dt, alfa, fallo);
    }

    private void AnimarOrbeYManos(float dt, float alfa, bool fallo)
    {
        // Tamaño: crece con cada botón, con muelle, y da un salto con el pulso.
        float objetivo = _estado == Estado.Abierto ? diametroOrbe + crecimientoPorBoton * _encendidos : _diamOrbe;
        _diamOrbe = Mathf.SmoothDamp(_diamOrbe, objetivo, ref _velDiamOrbe, 0.08f, Mathf.Infinity, dt);
        _empuje = Mathf.Max(0f, _empuje - dt * 6f);

        float tam = _diamOrbe * escalaGeneral * (1f + 0.35f * _pulso);
        if (_estado == Estado.Cerrando)
        {
            float k = _tCierre / _duracionCierre;
            switch (_cierre)
            {
                case ComboCastController.Result.Cast: tam *= 1f + 0.6f * k; break;
                case ComboCastController.Result.Interrupted: tam *= 1f + 2f * k; break;
                default: tam *= 1f - 0.7f * k; break;
            }
        }

        Color c = fallo ? colorFallo : _colorOrbe;
        PintarOrbe(_halo, tam * 2.2f, Tinte(c, 1f, alfa * 0.8f));
        PintarOrbe(_nucleo, tam, Tinte(Color.Lerp(colorNucleo, c, 0.25f), 1.4f + _pulso, alfa));

        // Manos: separadas lo justo para el orbe, con temblor y el empujón de cada botón.
        if (_manos == null) return;
        _sepManos = Mathf.SmoothDamp(_sepManos, tam * 0.9f + holguraManos, ref _velSepManos, 0.06f, Mathf.Infinity, dt);
        _faseTemblor += dt * frecuenciaTemblor;
        float tx = (Mathf.PerlinNoise(_faseTemblor, 0.1f) - 0.5f) * 2f * temblorManos;
        float ty = (Mathf.PerlinNoise(0.7f, _faseTemblor) - 0.5f) * 2f * temblorManos;
        _manos.Separacion = _sepManos;
        _manos.Desplazamiento = new Vector3(tx, manosArriba + ty, manosAdelante + empujePorBoton * Mathf.Sin(_empuje * Mathf.PI) * escalaGeneral);
    }

    void LateUpdate()
    {
        if (_estado == Estado.Apagado) return;

        Vector3 pos;
        if (_estado == Estado.Cerrando && _cierre == ComboCastController.Result.Cast)
        {
            float k = _tCierre / _duracionCierre;
            pos = _orbeAlCerrar + _frenteAlCerrar * (distanciaLanzamiento * escalaGeneral * k);
        }
        else if (_manos != null && _manos.Activo)
            pos = _manos.PuntoMedio + _personaje.forward * (0.03f * escalaGeneral);
        else if (_estado == Estado.Cerrando)
            pos = _orbeAlCerrar;
        else
            pos = _personaje.position + Vector3.up * alturaOrbeSinManos + _personaje.forward * 0.3f;

        _orbe.position = pos;
        if (_camara != null) _orbe.rotation = Quaternion.LookRotation(_camara.transform.forward, _camara.transform.up);
    }

    private void PintarOrbe(MeshRenderer r, float diametro, Color color)
    {
        r.transform.localScale = new Vector3(diametro, diametro, diametro);
        _mpb.SetColor(IdColor, color);
        _mpb.SetColor(IdBaseColor, color);
        r.SetPropertyBlock(_mpb);
    }

    private void SoltarManos(float segundos)
    {
        if (_manos != null) _manos.Soltar(this, segundos);
    }

    private void Pintar(MeshRenderer r, float diametro, float grados, Color color)
    {
        var t = r.transform;
        t.localScale = new Vector3(diametro, 1f, diametro);
        t.localRotation = Quaternion.Euler(0f, grados, 0f);
        _mpb.SetColor(IdColor, color);
        _mpb.SetColor(IdBaseColor, color);
        r.SetPropertyBlock(_mpb);
    }

    private Color Tinte(Color c, float brillo, float alfa)
    {
        float k = intensidad * brillo;
        return new Color(c.r * k, c.g * k, c.b * k, c.a * alfa);
    }

    private Color ColorDe(ComboButton boton)
    {
        switch (boton)
        {
            case ComboButton.A: return colorA;
            case ComboButton.B: return colorB;
            case ComboButton.X: return colorX;
            default: return colorY;
        }
    }

    // ── Estado ─────────────────────────────────────────────────────────────

    private void Apagar()
    {
        _estado = Estado.Apagado;
        if (_raiz != null && _raiz.gameObject.activeSelf) _raiz.gameObject.SetActive(false);
        if (_orbe != null && _orbe.gameObject.activeSelf) _orbe.gameObject.SetActive(false);
        if (_luz != null && _luz.enabled) _luz.enabled = false;
    }

    private void Soltar(float segundos)
    {
        if (_elevacion != null) _elevacion.Soltar(this, segundos);
    }

    private void LiberarFrenazo()
    {
        if (!_frenazoActivo) return;
        _frenazoActivo = false;
        TimeScaleArbiterService.Release(this);
    }

    private static void DetenerViento(float fundido)
    {
        if (AudioService.Instance != null) AudioService.Instance.StopLoopingSFX(LoopDeViento, fundido);
    }

    private static void Sonar(string clave, float tono = 1f)
    {
        if (!string.IsNullOrEmpty(clave) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(clave, 1f, null, tono);
    }

    // ── Construcción (una vez, al abrir por primera vez) ───────────────────

    private void Construir()
    {
        if (_raiz != null) return;

        var controller = _combo.Personaje;
        _personaje = controller != null ? controller.transform : transform;
        _elevacion = _personaje.GetComponent<ElevacionVisual>();
        if (_elevacion == null) _elevacion = GetComponentInParent<ElevacionVisual>();
        _manos = _personaje.GetComponent<ManosIK>();
        _camara = Camera.main;

        _mpb = new MaterialPropertyBlock();
        _quad = CrearQuad();

        _raiz = new GameObject("CirculoDelCombo").transform;
        _raiz.SetParent(_personaje, false);
        _raiz.localPosition = new Vector3(0f, alturaSobreElSuelo, 0f);

        int orden = 0;
        _exterior = CrearCapa("Exterior", materialExterior, orden++);
        _base = CrearCapa("Base", materialBase, orden++);
        int n = Mathf.Max(1, _combo.LongitudMaxima);
        _anillos = new MeshRenderer[n];
        _colorAnillo = new Color[n];
        _brilloAnillo = new float[n];
        for (int i = 0; i < n; i++) _anillos[i] = CrearCapa("Anillo" + (i + 1), materialAnillo, orden++);
        _destello = CrearCapa("Destello", materialDestello, orden++);

        _quadVertical = CrearQuadVertical();
        _orbe = new GameObject("OrbeDelCombo").transform;
        _orbe.SetParent(_personaje, false);
        _halo = CrearCapaDelOrbe("Halo", 20);
        _nucleo = CrearCapaDelOrbe("Nucleo", 21);
        _chispasOrbe = CrearChispasDelOrbe();

        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(_orbe, false);
        _luz = luzGo.AddComponent<Light>();
        _luz.type = LightType.Point;
        _luz.range = alcanceLuz * escalaGeneral;
        _luz.shadows = LightShadows.None;
        _luz.color = colorLuz;
        _luz.intensity = 0f;
        _luz.enabled = false;

        _chispas = CrearChispas();
        _rafaga = CrearRafaga();

        _raiz.gameObject.SetActive(false);
        _orbe.gameObject.SetActive(false);
    }

    private MeshRenderer CrearCapaDelOrbe(string nombre, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(_orbe, false);
        go.AddComponent<MeshFilter>().sharedMesh = _quadVertical;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = materialOrbe;
        r.sortingOrder = orden;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enabled = materialOrbe != null;
        return r;
    }

    private static Mesh CrearQuadVertical()
    {
        var m = new Mesh { name = "QuadDelOrbe" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    private ParticleSystem CrearChispasDelOrbe()
    {
        var go = new GameObject("ChispasDelOrbe");
        go.transform.SetParent(_personaje, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * escalaGeneral, 2.2f * escalaGeneral);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * escalaGeneral, 0.06f * escalaGeneral);
        main.startColor = colorCirculo;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 64;

        var emision = ps.emission;
        emision.enabled = false;

        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.05f * escalaGeneral;

        var freno = ps.limitVelocityOverLifetime;
        freno.enabled = true;
        freno.limit = new ParticleSystem.MinMaxCurve(0.3f * escalaGeneral);
        freno.dampen = 0.3f;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ConfigurarRender(go, materialChispas);
        return ps;
    }

    private MeshRenderer CrearCapa(string nombre, Material material, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(_raiz, false);
        // Un pelo de separación entre capas para que no parpadeen al ordenarse.
        go.transform.localPosition = new Vector3(0f, orden * 0.002f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = _quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.sortingOrder = orden;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enabled = material != null;
        return r;
    }

    private static Mesh CrearQuad()
    {
        var m = new Mesh { name = "QuadDelCombo" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    private ParticleSystem CrearChispas()
    {
        var go = new GameObject("ChispasDelCombo");
        go.transform.SetParent(_personaje, false);
        go.transform.localPosition = new Vector3(0f, alturaSobreElSuelo, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f * escalaGeneral, 0.12f * escalaGeneral);
        main.startColor = colorCirculo;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;
        main.maxParticles = 256;

        var emision = ps.emission;
        emision.rateOverTime = chispasPorSegundo;

        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = diametroBase * 0.45f * escalaGeneral;
        forma.radiusThickness = 0.35f;
        forma.rotation = new Vector3(90f, 0f, 0f);

        // Suben en espiral y se cierran un poco hacia el centro.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(velocidadSubida * 0.7f * escalaGeneral, velocidadSubida * 1.3f * escalaGeneral);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(giroEspiral * 0.8f, giroEspiral * 1.2f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.4f, -0.2f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Desvanecer(0.15f, 0.6f);

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ConfigurarRender(go, materialChispas);
        return ps;
    }

    private ParticleSystem CrearRafaga()
    {
        var go = new GameObject("RafagaDelCombo");
        go.transform.SetParent(_personaje, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidadRafaga * 0.8f * escalaGeneral, velocidadRafaga * 1.1f * escalaGeneral);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f * escalaGeneral, 0.8f * escalaGeneral);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = colorPolvo;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 64;

        var emision = ps.emission;
        emision.enabled = false;

        // Sale en anillo a ras de suelo, hacia fuera.
        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = 0.3f * escalaGeneral;
        forma.radiusThickness = 0f;
        forma.rotation = new Vector3(90f, 0f, 0f);

        var freno = ps.limitVelocityOverLifetime;
        freno.enabled = true;
        freno.limit = new ParticleSystem.MinMaxCurve(0.5f * escalaGeneral);
        freno.dampen = 0.25f;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Desvanecer(0.05f, 0.4f);

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

        ConfigurarRender(go, materialPolvo);
        return ps;
    }

    private static ParticleSystem.MinMaxGradient Desvanecer(float entrada, float salida)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entrada),
                new GradientAlphaKey(1f, salida), new GradientAlphaKey(0f, 1f),
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigurarRender(GameObject go, Material material)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = material != null;
    }

    private static float EaseOutBack(float k, float s)
    {
        float c3 = s + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + s * x * x;
    }
}
