using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Concurso por rondas sin derrota (un duelo de reverencias, unas clases de etiqueta): en cada
/// ronda actúa primero el rival, los jueces levantan sus carteles, luego juega el jugador una prueba
/// de botones (<see cref="IPruebaDeRonda"/>: equilibrio, ritmo, mantener…) y los jueces vuelven a
/// puntuar. Todo como datos: las rondas, lo que se dice y se hace en cada momento
/// (<see cref="Paso"/>), los jueces y sus carteles.
///
/// El marcador no decide el final: el concurso acaba siempre al terminar la última ronda (se
/// «gana», <see cref="PruebaDelJugador.alGanar"/>) y quien lo escuche decide qué pasa. Una ronda
/// <see cref="Puntuacion.Guionizada"/> enseña sus carteles tal cual, juegue como juegue el jugador;
/// <see cref="Puntuacion.PorRendimiento"/> los saca de lo bien que le ha salido.
///
/// Es una <see cref="PruebaDelJugador"/>: salida con fundido, etapas (el grupo) y arranque al
/// activarse; el jugador no se mueve mientras dura (el control queda bloqueado y los botones los lee
/// cada ronda). Pinta su marcador y sus rondas con <see cref="ConcursoUI"/>, puede llevar un plano
/// fijo de cámara (<see cref="CinematicCameraDriver"/>) y esconde el HUD y el minimapa.
/// Frases por <see cref="PresentadorDeLinea"/>; las de personajes con nombre las pide a los
/// <see cref="IHablantesDePrueba"/> del mismo objeto.
/// </summary>
public sealed class ConcursoPorRondas : PruebaDelJugador
{
    public enum Puntuacion
    {
        /// <summary>Los carteles de la ronda, tal cual.</summary>
        Guionizada,
        /// <summary>Cada juez da puntos según lo bien que le ha salido al jugador.</summary>
        PorRendimiento,
    }

    /// <summary>Un momento de una ronda: una frase, un gesto, un sonido, un efecto; lo que se rellene, a la vez.</summary>
    [Serializable]
    public sealed class Paso
    {
        [Tooltip("Frase (clave de texto). Vacía = sin frase.")]
        public string clave;
        [Tooltip("Quién la dice, si es de la escena: bocadillo sobre él si está cerca del jugador; si no, subtítulo con su nombre.")]
        public Transform hablante;
        [Tooltip("O quién la dice por su nombre, si lo conoce un IHablantesDePrueba del concurso (el grupo).")]
        public string hablanteConNombre;
        [Tooltip("Nombre (clave) del subtítulo si quien habla está lejos o no está en la escena.")]
        public string claveDelNombre;
        [Tooltip("Sin la voz doblada aunque la haya.")]
        public bool sinVoz;
        [Tooltip("Animator que hace un gesto a la vez (estado de su controlador).")]
        public Animator gestoDe;
        public string gesto;
        [Tooltip("Gesto del jugador a la vez (estado de su Animator).")]
        public string gestoDelJugador;
        public AudioClip sonido;
        [Range(0f, 1f)] public float volumen = 0.8f;
        [Tooltip("Efecto de un solo uso sobre quien habla o hace el gesto.")]
        public GameObject vfx;
        [Tooltip("Segundos mínimos que dura el paso (con frase, al menos lo que dura la frase).")]
        [Min(0f)] public float espera;
    }

    [Serializable]
    public sealed class Juez
    {
        [Tooltip("Sobre quién sale el cartel.")]
        public Transform sitio;
        public Animator animator;
        [Tooltip("Estado del Animator al levantar el cartel (vacío = ninguno).")]
        public string gestoAlLevantar;
    }

    [Serializable]
    public sealed class Cartel
    {
        [Tooltip("Índice del juez que lo levanta.")]
        [Min(0)] public int juez;
        [Tooltip("Texto del cartel (clave). Vacía = los puntos con el formato del concurso.")]
        public string clave;
        [Tooltip("Lo que suma al marcador (multiplicado por el de la ronda).")]
        public int puntos;
    }

    [Serializable]
    public sealed class Ronda
    {
        public string claveDelTitulo;
        public string textoDelTitulo;
        [Tooltip("Antes de nada (el presentador anuncia la ronda).")]
        public List<Paso> anuncio = new List<Paso>();
        [Tooltip("Lo que hace el rival.")]
        public List<Paso> actuacionDelRival = new List<Paso>();
        public List<Cartel> cartelesDelRival = new List<Cartel>();
        [Tooltip("Justo antes de que juegue el jugador.")]
        public List<Paso> turnoDelJugador = new List<Paso>();
        [Tooltip("Componente con la prueba del jugador (IPruebaDeRonda). Vacío = el jugador no juega esta ronda.")]
        public MonoBehaviour prueba;
        [Tooltip("Segundos que se lee la instrucción antes de empezar la prueba.")]
        [Min(0f)] public float segundosDeInstruccion = 1.5f;
        [Tooltip("Pose del jugador mientras juega (estado de su Animator).")]
        public string gestoDelJugador;
        [Tooltip("Gesto del jugador al acabar su prueba.")]
        public string gestoAlAcabar;
        public Puntuacion puntuacion = Puntuacion.Guionizada;
        [Tooltip("Guionizada: los carteles del jugador.")]
        public List<Cartel> cartelesDelJugador = new List<Cartel>();
        [Tooltip("Por rendimiento: puntos de cada juez con la prueba perfecta.")]
        [Min(1)] public int puntosPorJuez = 10;
        [Tooltip("Lo que vale cada punto de la ronda en el marcador.")]
        [Min(1)] public int multiplicador = 1;
        [Tooltip("Después de los carteles del jugador.")]
        public List<Paso> despues = new List<Paso>();
        public UnityEvent alAcabar = new UnityEvent();
    }

    [Serializable]
    public sealed class Colocacion
    {
        public Transform quien;
        public Transform donde;
    }

    [Header("Concurso")]
    [SerializeField] private EstiloDeMenuSencillo estilo;
    [SerializeField] private bool mostrarMarcador = true;
    [Tooltip("Nombre del rival en el marcador (clave).")]
    [SerializeField] private string claveDelRival;
    [Tooltip("Nombre del jugador en el marcador (clave).")]
    [SerializeField] private string claveDelJugador;
    [SerializeField] private List<Juez> jueces = new List<Juez>();
    [SerializeField] private List<Ronda> rondas = new List<Ronda>();
    [Tooltip("Se colocan al preparar el concurso, con la pantalla tapada (el rival en su sitio…).")]
    [SerializeField] private List<Colocacion> colocarAlEmpezar = new List<Colocacion>();

    [Header("Carteles")]
    [Tooltip("Formato de un cartel sin texto propio ({0} = puntos).")]
    [SerializeField] private string claveDeCartelConPuntos = "CONCURSO_PUNTOS";
    [SerializeField] private string textoDeCartelConPuntos = "{0}";
    [SerializeField, Min(0.3f)] private float segundosPorCartel = 1.5f;
    [SerializeField, Min(0f)] private float pausaEntreCarteles = 0.2f;
    [SerializeField] private AudioClip sonidoDelCartel;

    [Header("Frases")]
    [Tooltip("Metros al jugador hasta los que la frase de alguien de la escena sale en bocadillo.")]
    [SerializeField, Min(2f)] private float distanciaParaBocadillo = 16f;
    [Tooltip("Altura del bocadillo sobre los hablantes de la escena y los jueces (0 = la de siempre).")]
    [SerializeField, Min(0f)] private float alturaDelBocadillo = 1.6f;
    [SerializeField, Min(0f)] private float pausaEntreFrases = 0.25f;

    [Header("Plano")]
    [Tooltip("Cámara del concurso (opcional): se fija en el plano mientras dura.")]
    [SerializeField] private CinematicCameraDriver camara;
    [SerializeField] private Transform plano;
    [SerializeField, Range(20f, 80f)] private float campoDeVision = 45f;

    /// <summary>Al acabar cada ronda (índice).</summary>
    public event Action<ConcursoPorRondas, int> RondaAcabada;

    public int PuntosDelRival { get; private set; }
    public int PuntosDelJugador { get; private set; }
    /// <summary>Ronda en curso (0 la primera); -1 antes de empezar.</summary>
    public int RondaActual { get; private set; } = -1;
    public IReadOnlyList<Ronda> Rondas => rondas;

    private readonly List<IHablantesDePrueba> _hablantes = new List<IHablantesDePrueba>();
    private ConcursoUI _ui;
    private Coroutine _concurso;
    private IPruebaDeRonda _pruebaEnCurso;
    private NPCSimpleAnimator _animadorDelJugador;
    private bool _poseDelJugador, _escondido, _conCamara;

    protected override bool SoltarElControlAlEmpezar => false;

    protected override void Awake()
    {
        base.Awake();
        GetComponents(_hablantes);
    }

    // ── La prueba ────────────────────────────────────────────────────────────────────────────

    protected override void PrepararIntento()
    {
        PuntosDelRival = 0;
        PuntosDelJugador = 0;
        RondaActual = -1;
        foreach (var c in colocarAlEmpezar)
            if (c?.quien != null && c.donde != null) c.quien.SetPositionAndRotation(c.donde.position, c.donde.rotation);
    }

    protected override void Arrancar()
    {
        _ui = ConcursoUI.Crear(estilo, mostrarMarcador);
        PintarMarcador();
        _animadorDelJugador = PlayerService.Player != null ? PlayerService.Player.GetComponentInChildren<NPCSimpleAnimator>(true) : null;

        _escondido = true;
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD(this);
        MinimapController.Ocultar(this);
        if (camara != null && plano != null)
        {
            _conCamara = true;
            camara.Activate();
            camara.Cut(plano.position, plano.rotation, campoDeVision);
        }
        _concurso = StartCoroutine(Co_Concurso());
    }

    protected override void Actualizar(float dt) { }

    protected override void Detener()
    {
        if (_concurso != null) StopCoroutine(_concurso);
        _concurso = null;
        _pruebaEnCurso?.Terminar();
        _pruebaEnCurso = null;
        SoltarPoseDelJugador();
        if (_ui != null) Destroy(_ui.gameObject);
        _ui = null;
        if (_escondido)
        {
            _escondido = false;
            Sendero.UI.PlayerHUDV2.Instance?.ShowHUD(this);
            MinimapController.Mostrar(this);
        }
        if (_conCamara)
        {
            _conCamara = false;
            if (camara != null) camara.Deactivate();
        }
    }

    // ── Las rondas ───────────────────────────────────────────────────────────────────────────

    private IEnumerator Co_Concurso()
    {
        for (int i = 0; i < rondas.Count; i++)
        {
            var ronda = rondas[i];
            if (ronda == null) continue;
            RondaActual = i;
            _ui.Titulo(PresentadorDeLinea.Texto(ronda.claveDelTitulo, ronda.textoDelTitulo));

            yield return Co_Pasos(ronda.anuncio);
            yield return Co_Pasos(ronda.actuacionDelRival);
            yield return Co_Carteles(ronda.cartelesDelRival, ronda.multiplicador, delRival: true);
            yield return Co_Pasos(ronda.turnoDelJugador);

            float rendimiento = 0f;
            if (ronda.prueba is IPruebaDeRonda prueba)
            {
                _ui.Panel(prueba.Instruccion);
                if (ronda.segundosDeInstruccion > 0f) yield return new WaitForSeconds(ronda.segundosDeInstruccion);
                PoseDelJugador(ronda.gestoDelJugador);
                _pruebaEnCurso = prueba;
                prueba.Empezar(_ui.Zona, _ui.Estilo);
                while (!prueba.Actualizar(Time.deltaTime)) yield return null;
                rendimiento = prueba.Rendimiento;
                prueba.Terminar();
                _pruebaEnCurso = null;
                _ui.Panel(null);
                SoltarPoseDelJugador();
                if (!string.IsNullOrEmpty(ronda.gestoAlAcabar)) _animadorDelJugador?.PlayOneShot(ronda.gestoAlAcabar);
            }

            var carteles = ronda.puntuacion == Puntuacion.Guionizada
                ? ronda.cartelesDelJugador
                : CartelesPorRendimiento(rendimiento, ronda.puntosPorJuez);
            yield return Co_Carteles(carteles, ronda.multiplicador, delRival: false);
            yield return Co_Pasos(ronda.despues);

            ronda.alAcabar?.Invoke();
            RondaAcabada?.Invoke(this, i);
        }
        _ui.Titulo(null);
        _concurso = null;
        Ganar();
    }

    private IEnumerator Co_Pasos(List<Paso> pasos)
    {
        if (pasos == null) yield break;
        foreach (var paso in pasos)
        {
            if (paso == null) continue;
            float espera = paso.espera;
            Transform donde = paso.hablante != null ? paso.hablante : paso.gestoDe != null ? paso.gestoDe.transform : null;

            Gesto(paso.gestoDe, paso.gesto);
            if (!string.IsNullOrEmpty(paso.gestoDelJugador)) _animadorDelJugador?.PlayOneShot(paso.gestoDelJugador);
            if (paso.sonido != null) AudioService.Instance?.PlaySFX(paso.sonido, paso.volumen);
            if (paso.vfx != null && donde != null && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(paso.vfx, donde.position + Vector3.up, Quaternion.identity, 3f);

            var linea = Decir(paso);
            if (linea.Dicha) espera = Mathf.Max(espera, linea.Duracion + pausaEntreFrases);
            if (espera > 0f) yield return new WaitForSeconds(espera);
        }
    }

    private IEnumerator Co_Carteles(List<Cartel> carteles, int multiplicador, bool delRival)
    {
        if (carteles == null) yield break;
        foreach (var cartel in carteles)
        {
            if (cartel == null) continue;
            var juez = cartel.juez >= 0 && cartel.juez < jueces.Count ? jueces[cartel.juez] : null;
            string texto = !string.IsNullOrEmpty(cartel.clave)
                ? PresentadorDeLinea.Texto(cartel.clave)
                : string.Format(PresentadorDeLinea.Texto(claveDeCartelConPuntos, textoDeCartelConPuntos), cartel.puntos);

            if (juez != null) Gesto(juez.animator, juez.gestoAlLevantar);
            if (sonidoDelCartel != null) AudioService.Instance?.PlaySFX(sonidoDelCartel, 0.6f);
            bool conJuez = juez != null && juez.sitio != null;
            var linea = PresentadorDeLinea.Decir(new FraseADecir
            {
                textoPorDefecto = texto,
                ancla = conJuez ? juez.sitio : null,
                presentacion = conJuez ? PresentacionDeTexto.Bocadillo : PresentacionDeTexto.Subtitulo,
                sinVoz = true,
                duracion = segundosPorCartel,
                desplazamiento = conJuez && alturaDelBocadillo > 0f ? Vector3.up * alturaDelBocadillo : (Vector3?)null,
            });

            int puntos = cartel.puntos * Mathf.Max(1, multiplicador);
            if (delRival) PuntosDelRival += puntos;
            else PuntosDelJugador += puntos;
            PintarMarcador();

            float dura = linea.Dicha ? Mathf.Max(segundosPorCartel, linea.Duracion) : segundosPorCartel;
            yield return new WaitForSeconds(dura + pausaEntreCarteles);
        }
    }

    private List<Cartel> CartelesPorRendimiento(float rendimiento, int puntosPorJuez)
    {
        var lista = new List<Cartel>(jueces.Count);
        for (int j = 0; j < jueces.Count; j++)
        {
            int puntos = Mathf.Clamp(Mathf.RoundToInt(rendimiento * puntosPorJuez * UnityEngine.Random.Range(0.85f, 1.1f)), 0, puntosPorJuez);
            lista.Add(new Cartel { juez = j, puntos = puntos });
        }
        return lista;
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private LineaDicha Decir(Paso paso)
    {
        if (string.IsNullOrEmpty(paso.clave)) return LineaDicha.Nada;
        if (!string.IsNullOrEmpty(paso.hablanteConNombre))
            foreach (var hablantes in _hablantes)
            {
                var dicha = hablantes.Decir(paso.hablanteConNombre, paso.clave);
                if (dicha.Dicha) return dicha;
            }

        var jugador = PlayerService.Player;
        bool cerca = paso.hablante != null && jugador != null &&
                     (paso.hablante.position - jugador.transform.position).sqrMagnitude <= distanciaParaBocadillo * distanciaParaBocadillo;
        return PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = paso.clave,
            ancla = cerca ? paso.hablante : null,
            presentacion = cerca ? PresentacionDeTexto.Bocadillo : PresentacionDeTexto.Subtitulo,
            nombreDelHablante = cerca ? null : PresentadorDeLinea.NombreDelHablante(paso.claveDelNombre),
            sinVoz = paso.sinVoz,
            desplazamiento = cerca && alturaDelBocadillo > 0f ? Vector3.up * alturaDelBocadillo : (Vector3?)null,
        });
    }

    /// <summary>Cruza el Animator a ese estado si lo tiene en alguna capa.</summary>
    private static void Gesto(Animator animator, string estado)
    {
        if (animator == null || string.IsNullOrEmpty(estado) || !animator.isActiveAndEnabled) return;
        int hash = Animator.StringToHash(estado);
        for (int capa = 0; capa < animator.layerCount; capa++)
        {
            if (!animator.HasState(capa, hash)) continue;
            animator.CrossFadeInFixedTime(hash, 0.15f, capa);
            return;
        }
    }

    private void PoseDelJugador(string estado)
    {
        if (string.IsNullOrEmpty(estado) || _animadorDelJugador == null) return;
        _animadorDelJugador.HoldPose(estado);
        _poseDelJugador = true;
    }

    private void SoltarPoseDelJugador()
    {
        if (!_poseDelJugador) return;
        _poseDelJugador = false;
        if (_animadorDelJugador != null) _animadorDelJugador.ReleasePose();
    }

    private void PintarMarcador()
    {
        if (_ui == null) return;
        _ui.Marcador(PresentadorDeLinea.Texto(claveDelRival, claveDelRival), PuntosDelRival,
                     PresentadorDeLinea.Texto(claveDelJugador, claveDelJugador), PuntosDelJugador);
    }
}
