using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Sendero.Core.Feedback;

/// <summary>
/// Módulo de escena de la cinemática final («El Sacrificio» y «El Río»). El guion horneado pone a
/// los personajes, los planos y las frases; este módulo pone lo que el guion no sabe hacer: el
/// portal que deja ver el otro mundo, el Sendero que se deshace, el conjuro, los efectos de luz de
/// Liam, del Archimago y de Will, la carrera al abrazo a compás de la música, el cielo de cada
/// noche, el río y el final.
///
/// Está repartido por actos: este archivo tiene lo común (actores, zonas, cielo, red de seguridad
/// y utilidades); Portal, Conjuro, Reencuentro y Rio, cada parte.
/// </summary>
[DefaultExecutionOrder(-50)]
public partial class ModuloCinematicaFinal : SequenceModule
{
    [Header("Actores")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _estelaActor;
    [SerializeField] private Transform _liamActor;
    [SerializeField] private Transform _archimagoActor;
    [SerializeField] private Transform _tobiasActor;

    [Header("Escenario y Zonas")]
    [SerializeField] private GameObject _zonaSenderoRoot;
    [SerializeField] private GameObject _zonaPiedraRoot;
    [SerializeField] private GameObject _zonaRioRoot;
    [SerializeField] private Transform _altarTransform;
    [SerializeField] private Transform _portalUmbralTransform;
    [SerializeField] private Transform _piedraAncestralTransform;
    [SerializeField] private Transform _orillaRioTransform;

    [Header("Efectos de escena")]
    [SerializeField] private GameObject _vfxPilarResurreccion;
    [SerializeField] private GameObject _cintaRojaWill;

    [Header("Prefabs de efectos")]
    [Tooltip("Estallido de una estrella (pequeño y brillante).")]
    [SerializeField] private GameObject _prefabEstallidoDeEstrella;
    [Tooltip("Explosión grande (el Sendero que revienta detrás de Will).")]
    [SerializeField] private GameObject _prefabExplosion;
    [Tooltip("Círculo mágico bajo los pies.")]
    [SerializeField] private GameObject _prefabCirculoMagico;
    [Tooltip("Segundo círculo mágico, más grande y lento.")]
    [SerializeField] private GameObject _prefabCirculoMagico2;
    [Tooltip("Aura de estrellas alrededor de un personaje.")]
    [SerializeField] private GameObject _prefabAuraEstelar;
    [Tooltip("Golpe de luz sagrada (aparecer, materializarse).")]
    [SerializeField] private GameObject _prefabGolpeSagrado;
    [Tooltip("Columna de luz que baja del cielo.")]
    [SerializeField] private GameObject _prefabPilarDeLuz;
    [Tooltip("Remolino de viento hacia arriba.")]
    [SerializeField] private GameObject _prefabTornado;
    [Tooltip("Onda expansiva a ras de suelo.")]
    [SerializeField] private GameObject _prefabOndaExpansiva;
    [Tooltip("Implosión (el portal que se cierra de golpe).")]
    [SerializeField] private GameObject _prefabImplosion;
    [Tooltip("Destello de aparición (alguien que llega por un portal).")]
    [SerializeField] private GameObject _prefabTeletransporte;
    [Tooltip("Lluvia de estrellas en el suelo.")]
    [SerializeField] private GameObject _prefabLluviaDeEstrellas;

    [Header("Río y noche")]
    [SerializeField] private GameObject _canaPescarWill;
    [SerializeField] private GameObject _canaPescarTobias;
    [SerializeField] private GameObject _cuadernoDibujoTobias;
    [SerializeField] private GameObject _vfxSalpicaduraRio;
    [SerializeField] private Light _luzSolar;
    [SerializeField] private Light _luzNocheRioGrupo;
    [SerializeField] private Material _skyboxSendero;
    [SerializeField] private Material _skyboxDia;
    [SerializeField] private Material _skyboxAtardecer;
    [SerializeField] private Material _skyboxNoche;
    [SerializeField] private Color _colorSolSendero = new(0.65f, 0.75f, 1.0f);
    [SerializeField] private Color _colorSolDia = new(1.0f, 0.96f, 0.86f);
    [SerializeField] private Color _colorLuzNoche = new(0.24f, 0.32f, 0.58f);
    [SerializeField] private GameObject _estrellaTorcidaObjeto;
    [SerializeField] private GameObject _estrellasNocturnasRoot;

    [Header("Portales")]
    [SerializeField] private GameObject _portalUmbralSendero;
    [SerializeField] private GameObject _portalUmbralPiedra;
    [SerializeField] private GameObject _ventanaPortalSendero;
    [SerializeField] private GameObject _ventanaPortalPiedra;
    [SerializeField] private Camera _camPortalSendero;
    [SerializeField] private Camera _camPortalPiedra;
    [Tooltip("Shader de las ventanas del portal (El Sendero/Ventana de portal).")]
    [SerializeField] private Shader _shaderVentanaDePortal;

    [Header("Textos")]
    [Tooltip("Fuente de los rótulos («Unas semanas después…», «Fin»).")]
    [SerializeField] private TMP_FontAsset _fuenteDeRotulo;
    [Tooltip("El dibujo que Tobías hace de Will en su cuaderno.")]
    [SerializeField] private Texture2D _dibujoDeTobias;

    // Zonas: centro de cada una en el mundo.
    private static readonly Vector3 CentroSendero = new(0f, 0f, 4.5f);
    private static readonly Vector3 CentroPiedra = new(200f, 0f, 0f);
    private static readonly Vector3 CentroRio = new(400f, 0f, 0f);

    // Dónde está la luna en las noches de la Piedra y del río (alta sobre el lado del portal).
    private static readonly Vector3 DireccionDeLaLuna = new Vector3(0.62f, 0.42f, -0.66f).normalized;

    private SequenceContext _ctx;
    private readonly List<GameObject> _instanciasTemporales = new();

    // Zonas escondidas sin apagarlas (ver MostrarZona): qué renderers y luces tenía encendidos cada una.
    private readonly Dictionary<Renderer, bool> _renderersDeZona = new();
    private readonly Dictionary<Light, bool> _lucesDeZona = new();
    private readonly HashSet<GameObject> _zonasOcultas = new();

    // Red de seguridad de Will: el último sitio donde estaba de pie, por si cae por un hueco del suelo.
    private Transform _willEnEscena;
    private Vector3 _ultimoSueloDeWill;
    private bool _haySueloDeWill;
    private bool _enMarcha;
    private bool _suelosCreados;

    // La cámara descarta NaN mientras dura la cinemática: un solo píxel NaN (un cielo sin astros, un
    // efecto mal inicializado) lo convierte el bloom en un lavado blanco de media pantalla.
    private UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _datosDeCamara;
    private bool _stopNaNPrevio;

    private CieloEstrelladoDelFinal _cieloNormal;
    private CieloEstrelladoDelFinal _cieloEspecial;
    private SolYLunaEnElCielo _publicadorDeAstros;

    public override string[] Routines => new[]
    {
        // Comunes
        "AmbientePiedra", "AmbienteSendero", "EncadenarParte2",
        // Acto 1: el portal
        "AbrirPortalUmbral", "ColapsoDelSendero", "ExplosionEnElPortal",
        "EstelaAlcanzaElPortal", "EstelaDejaDeAlcanzar",
        // Acto 2: el conjuro
        "SenderoDestruido", "MusicaDelConjuro", "ConjuroCirculos", "LevitarWill", "ConjuroClimax", "WillSeDesvanece",
        // Acto 3 y 4: Liam y el Archimago
        "OcultarLiam", "LiamSeMaterializa", "MusicaDelArchimago", "EspirituArchimago", "ArchimagoSeHaceEstrella",
        // Acto 5: el regreso
        "PortalDeRegreso", "EsperarSubidaDelTema", "CarreraAlAbrazo", "LagrimasAlCorrer", "SoltarAbrazo", "AbrazoEstelaWill",
        "AbrazoFinalDeLosTres", "TelonANegro", "LiamSinSentido", "LiamDespierta",
        // Parte 2: el río
        "PrepararRioDia", "TarjetaSemanasDespues", "QuitarTelon", "DibujoDeTobias", "TobiasMiraAEstela", "TobiasDejaDeMirar", "TironDeLaCana", "EstelaSaleDelAgua",
        "Anochecer", "NocheEnElRio", "MusicaDeLaNoche", "VozQueSeAleja",
        "LiamDuerme", "TobiasDuerme", "TobiasDespierta", "WillYEstelaSeMiran", "NadieMiraConLaCabeza", "EnfocarEstrella", "FraseFinal", "RotuloFin", "IrACreditos",
    };

    private void Awake()
    {
        enabled = true;
        // El Archimago y Tobías esperan fuera de cuadro, pero el registro de NPCs solo apunta a los
        // que han despertado: si empiezan apagados, ningún plano ni frase los encuentra. Se
        // despiertan aquí, antes de que arranque la secuencia, y el guion los esconde («desaparece»).
        if (_archimagoActor == null) _archimagoActor = BuscarActorApagado("Actor_Archimago");
        if (_tobiasActor == null) _tobiasActor = BuscarActorApagado("Actor_Tobias");
        Despertar(_archimagoActor, "Actor_Archimago");
        Despertar(_tobiasActor, "Actor_Tobias");
    }

    /// Un actor de la escena por su nombre, aunque esté apagado (GameObject.Find no ve los apagados).
    private static Transform BuscarActorApagado(string nombre)
    {
        foreach (var npc in FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(FindObjectsInactive.Include))
            if (npc != null && npc.gameObject.name == nombre && npc.gameObject.scene.IsValid()) return npc.transform;
        return null;
    }

    /// Lo enciende y, si su NPC no tiene ID propio con el que apuntarse, lo apunta con el de la escena.
    private static void Despertar(Transform actor, string id)
    {
        if (actor == null) return;
        if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
        var registro = Game.NPC.NPCRegistry.Instance;
        if (registro == null || registro.TryGetNPCByID(id, out _)) return;
        var npc = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (npc != null) registro.RegisterNPC(id, null, npc);
    }

    public override IEnumerator Run(string routine, SequenceContext ctx)
    {
        _ctx = ctx;
        if (!_enMarcha) EmpezarCinematica(ctx);

        switch (routine)
        {
            case "AmbientePiedra": AplicarNochePiedra(); break;
            case "AmbienteSendero": AplicarAmbienteSendero(); break;
            case "EncadenarParte2": EncadenarParte2(); break;

            case "AbrirPortalUmbral": AbrirPortal(); break;
            case "ColapsoDelSendero": Lanzar(Co_ColapsoDelSendero()); break;
            case "ExplosionEnElPortal": Lanzar(Co_ExplosionEnElPortal()); break;
            case "EstelaAlcanzaElPortal": EstelaAlcanzaElPortal(); break;
            case "EstelaDejaDeAlcanzar": GestosProcedurales.DejarDeAlcanzar(ResolverActor("estela", _estelaActor), 0.9f); break;

            case "SenderoDestruido": AplicarSenderoDestruido(); break;
            case "MusicaDelConjuro": Lanzar(Co_MusicaDelConjuro()); break;
            case "ConjuroCirculos": Lanzar(Co_ConjuroCirculos()); break;
            case "LevitarWill": Lanzar(Co_LevitarWill()); break;
            case "ConjuroClimax": Lanzar(Co_ConjuroClimax()); break;
            case "WillSeDesvanece": Lanzar(Co_WillSeDesvanece()); break;

            case "OcultarLiam": OcultarLiam(); break;
            case "LiamSeMaterializa": Lanzar(Co_LiamSeMaterializa()); break;
            case "MusicaDelArchimago": MusicaDelArchimago(); break;
            case "EspirituArchimago": Lanzar(Co_EspirituArchimago()); break;
            case "ArchimagoSeHaceEstrella": Lanzar(Co_ArchimagoSeHaceEstrella()); break;

            case "PortalDeRegreso": Lanzar(Co_PortalDeRegreso()); break;
            case "EsperarSubidaDelTema": Lanzar(Co_EsperarSubidaDelTema()); break;
            case "CarreraAlAbrazo": Lanzar(Co_CarreraAlAbrazo()); break;
            case "SoltarAbrazo":
                GestosProcedurales.Soltar(0.7f, ResolverActor("estela", _estelaActor), ResolverActor("will", _willActor), ResolverActor("liam", _liamActor));
                break;
            case "AbrazoEstelaWill":
                MirarseDeFrente();
                GestosProcedurales.Abrazar(ResolverActor("estela", _estelaActor), ResolverActor("will", _willActor), 0.45f);
                break;
            case "AbrazoFinalDeLosTres":
                GestosProcedurales.RodearConLosBrazos(ResolverActor("liam", _liamActor), ResolverActor("will", _willActor), ResolverActor("estela", _estelaActor), 0.6f);
                break;
            case "LagrimasAlCorrer": Lanzar(Co_LagrimasAlCorrer()); break;
            case "TelonANegro": Lanzar(Co_TelonANegro(3f)); break;
            case "LiamSinSentido": MantenerOjosCerrados(ResolverActor("liam", _liamActor), true); break;
            case "LiamDespierta": MantenerOjosCerrados(ResolverActor("liam", _liamActor), false); break;

            case "PrepararRioDia": PrepararRioDia(); break;
            case "TarjetaSemanasDespues": Lanzar(Co_RotuloEnNegro(Texto("FINAL_SEMANAS_DESPUES", "Unas semanas después…"), 64f, 1.2f, 2.4f, 1.2f)); break;
            case "QuitarTelon": Lanzar(Co_QuitarTelon(2.2f)); break;
            case "TobiasMiraAEstela": MirarConLaCabeza(ResolverActor("tobias", _tobiasActor), ResolverActor("estela", _estelaActor)); break;
            case "TobiasDejaDeMirar": MirarConLaCabeza(ResolverActor("tobias", _tobiasActor), null); break;
            case "DibujoDeTobias": Lanzar(Co_DibujoDeTobias()); break;
            case "TironDeLaCana": Lanzar(Co_TironDeLaCana()); break;
            case "EstelaSaleDelAgua": Lanzar(Co_EstelaSaleDelAgua()); break;
            case "Anochecer": Lanzar(Co_Anochecer()); break;
            case "NocheEnElRio": PrepararNocheEnElRio(); break;
            case "MusicaDeLaNoche": MusicaDeLaNoche(); break;
            case "VozQueSeAleja": Lanzar(Co_VozQueSeAleja()); break;
            case "EnfocarEstrella": Lanzar(Co_EnfocarEstrella()); break;
            case "LiamDuerme": Lanzar(Co_LiamDuerme()); break;
            case "TobiasDuerme": MantenerOjosCerrados(ResolverActor("tobias", _tobiasActor), true); break;
            case "TobiasDespierta": MantenerOjosCerrados(ResolverActor("tobias", _tobiasActor), false); break;
            case "WillYEstelaSeMiran":
                MirarConLaCabeza(ResolverActor("will", _willActor), ResolverActor("estela", _estelaActor));
                MirarConLaCabeza(ResolverActor("estela", _estelaActor), ResolverActor("will", _willActor));
                break;
            case "NadieMiraConLaCabeza":
                foreach (var alias in new[] { "will", "estela", "liam", "tobias" }) MirarConLaCabeza(ResolverActor(alias, null), null);
                break;
            case "FraseFinal": Lanzar(Co_FraseFinal()); break;
            case "RotuloFin": Lanzar(Co_RotuloEnNegro(Texto("FINAL_FIN", "Fin"), 110f, 2.2f, 3.5f, 2.5f)); break;
            case "IrACreditos": Lanzar(Co_IrACreditos()); break;
        }
        yield break;
    }

    /// Las rutinas del módulo corren de fondo: el guion sigue su reloj y les da el tiempo con «espera».
    private void Lanzar(IEnumerator rutina)
    {
        if (rutina != null) StartCoroutine(rutina);
    }

    private void EmpezarCinematica(SequenceContext ctx)
    {
        _enMarcha = true;
        _willEnEscena = ResolverActor("will", _willActor);
        _haySueloDeWill = false;
        CrearSuelosDeSeguridad();
        CongelarFisicaDeWill();
        PrepararActoresParaRodar();
        PrepararPosesDeSuelo();
        OcultarEstrellasViejas();

        var camara = CamaraPrincipal();
        if (camara != null && camara.TryGetComponent(out _datosDeCamara))
        {
            _stopNaNPrevio = _datosDeCamara.stopNaN;
            _datosDeCamara.stopNaN = true;
        }
        RenderPipelineManager.beginContextRendering += AntesDeRenderizar;
        RenderPipelineManager.beginCameraRendering += AntesDeRenderizarCamara;
    }

    public override void OnSequenceCleanup()
    {
        TerminarCinematica();
    }

    private void OnDisable()
    {
        TerminarCinematica();
    }

    private void TerminarCinematica()
    {
        RenderPipelineManager.beginContextRendering -= AntesDeRenderizar;
        RenderPipelineManager.beginCameraRendering -= AntesDeRenderizarCamara;
        _enMarcha = false;
        _portalAbierto = false;
        _colapsoActivo = false;
        _levitando = false;
        if (_datosDeCamara != null)
        {
            _datosDeCamara.stopNaN = _stopNaNPrevio;
            _datosDeCamara = null;
        }
        if (_ctx != null && _relojAcompasado) { _ctx.SoltarRelojAlCompas(); _relojAcompasado = false; }
        if (_ctx != null && _relojRetenidoPorMi) { _ctx.SoltarReloj(); _relojRetenidoPorMi = false; }
        TimeScaleArbiterService.Release(this);
        AudioService.Instance?.SoltarAtenuacionDeBus(ClaveVozLejana, AudioBus.Dialogue);
        SolYLunaEnElCielo.AstrosImpuestos = null;
        var personajes = new[] { ResolverActor("estela", _estelaActor), ResolverActor("will", _willActor), ResolverActor("liam", _liamActor) };
        GestosProcedurales.Soltar(0f, personajes);
        foreach (var p in personajes) AspectoFantasma.Quitar(p);
        AspectoFantasma.Quitar(ResolverActor("archimago", _archimagoActor));
        DevolverFisicaDeWill();
        DescongelarFisicaDeWill();
        MostrarZonasEnTodasLasCamaras();
        LimpiarPortal();
        LimpiarEfectosTemporales();
        // Lo creado para esta parte ya no existe: la siguiente lo vuelve a crear.
        _suelosCreados = false;
        _listasDeZonaHechas = false;
        _renderersSendero.Clear();
        _renderersPiedra.Clear();
        if (_rendererDelSuelo != null) _rendererDelSuelo.enabled = true;
        _sueloPartido = false;
        _losas.Clear();
        _ruidoDeLosas.Clear();
        _caidaDeLosas.Clear();
        _giroDeLosas.Clear();
        _origenDeLosas.Clear();
        _estrellasDelDomo.Clear();
        _canas.Clear();
        _archimagoFlotando = false;
        SoltarPosesDeSuelo();
        _conLosOjosCerrados.Clear();
        foreach (var alias in new[] { "will", "estela", "liam", "tobias" }) MirarConLaCabeza(ResolverActor(alias, null), null);
#if UNITY_EDITOR
        EscribirDesfases();
#endif
        // El telón negro pasa a la parte 2; si no se encadena (se corta la cinemática), se quita.
        if (!_encadenandoParte2) DestruirTelon();
        _encadenandoParte2 = false;
    }

    private void LimpiarEfectosTemporales()
    {
        StopAllCoroutines();
        for (int i = 0; i < _instanciasTemporales.Count; i++)
            if (_instanciasTemporales[i] != null) Destroy(_instanciasTemporales[i]);
        _instanciasTemporales.Clear();
    }

    // ── Actores ──────────────────────────────────────────────────────────────────────────────

    private Transform ResolverActor(string idOrAlias, Transform respaldo)
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor(IdDeActor(idOrAlias));
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        if (respaldo != null) return respaldo;
        var go = GameObject.Find(IdDeActor(idOrAlias)) ?? GameObject.Find(idOrAlias);
        return go != null ? go.transform : null;
    }

    private static string IdDeActor(string alias)
    {
        if (alias == "will") return SequenceActor.PlayerId;
        return "Actor_" + char.ToUpper(alias[0]) + alias.Substring(1).ToLower();
    }

    private SequenceActor Actor(string alias) => _ctx?.GetActor(IdDeActor(alias));

    /// Todo el cuerpo a la vista: renderers encendidos y sin el «no dibujar» que deja una
    /// ocultación. Las mallas con hueso se actualizan aunque estén fuera de cámara, para que un
    /// teletransporte de 200 m no las deje con los límites de antes (y el cuerpo sin dibujar).
    private static void MostrarActorEntero(Transform actor)
    {
        if (actor == null) return;
        if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
        foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            r.enabled = true;
            r.forceRenderingOff = false;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
    }

    private void PrepararActoresParaRodar()
    {
        foreach (var t in new[] { ResolverActor("will", _willActor), ResolverActor("estela", _estelaActor), ResolverActor("liam", _liamActor), ResolverActor("archimago", _archimagoActor), ResolverActor("tobias", _tobiasActor) })
        {
            if (t == null) continue;
            foreach (var smr in t.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        }
    }

    private bool EsDeUnActor(Transform t)
    {
        return Contiene(_willActor, t) || Contiene(_estelaActor, t) || Contiene(_liamActor, t)
            || Contiene(_archimagoActor, t) || Contiene(_tobiasActor, t);
    }

    private static bool Contiene(Transform raiz, Transform t) => raiz != null && t != null && t.IsChildOf(raiz);

    /// Estela y Will se giran para quedar de frente.
    private void MirarseDeFrente()
    {
        var estela = ResolverActor("estela", _estelaActor);
        var will = ResolverActor("will", _willActor);
        if (estela == null || will == null) return;
        Actor("estela")?.Face(will.position);
        Actor("will")?.Face(estela.position);
    }

    // ── Red de seguridad de Will ─────────────────────────────────────────────────────────────

    /// Suelos invisibles a ras de suelo bajo la Piedra y el río. Will es el único con física: si
    /// el suelo copiado de MainWorld tiene un hueco bajo el punto donde le pone el guion, cae al
    /// vacío y no sale en ningún plano.
    private void CrearSuelosDeSeguridad()
    {
        if (_suelosCreados) return;
        _suelosCreados = true;
        int capa = CapaDelSuelo();
        CrearSuelo("Suelo_Seguridad_Piedra", CentroPiedra + new Vector3(0f, -0.5f, 2f), new Vector3(70f, 1f, 70f), capa);
        CrearSuelo("Suelo_Seguridad_Rio", CentroRio + new Vector3(0f, -0.5f, -1f), new Vector3(70f, 1f, 70f), capa);
    }

    private void CrearSuelo(string nombre, Vector3 centro, Vector3 tamano, int capa)
    {
        var go = new GameObject(nombre) { layer = capa };
        go.transform.SetParent(transform, false);
        go.transform.position = centro;
        go.AddComponent<BoxCollider>().size = tamano;
        _instanciasTemporales.Add(go);
    }

    private readonly List<(Rigidbody rb, bool cinematico, bool gravedad)> _cuerposDeWill = new();
    private readonly List<(CharacterController cc, bool encendido)> _controladoresDeWill = new();

    /// En la cinemática Will es un títere del guion: sin gravedad ni empujones de la física, que
    /// le hacían caer por debajo del suelo aunque el guion le pusiera en su sitio.
    private void CongelarFisicaDeWill()
    {
        if (_willEnEscena == null || _cuerposDeWill.Count > 0 || _controladoresDeWill.Count > 0) return;
        foreach (var rb in _willEnEscena.GetComponentsInChildren<Rigidbody>(true))
        {
            _cuerposDeWill.Add((rb, rb.isKinematic, rb.useGravity));
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (var cc in _willEnEscena.GetComponentsInChildren<CharacterController>(true))
        {
            _controladoresDeWill.Add((cc, cc.enabled));
            cc.enabled = false;
        }
    }

    private void DescongelarFisicaDeWill()
    {
        foreach (var (rb, cinematico, gravedad) in _cuerposDeWill)
        {
            if (rb == null) continue;
            rb.isKinematic = cinematico;
            rb.useGravity = gravedad;
        }
        _cuerposDeWill.Clear();
        foreach (var (cc, encendido) in _controladoresDeWill)
            if (cc != null) cc.enabled = encendido;
        _controladoresDeWill.Clear();
    }

    private int CapaDelSuelo()
    {
        var suelo = GameObject.Find("Suelo_Astral_Sendero");
        return suelo != null ? suelo.layer : 0;
    }

    /// Si Will cae por debajo del suelo de la zona, vuelve al último sitio donde estaba de pie.
    private void VigilarCaidaDeWill()
    {
        if (_willEnEscena == null || _levitando) return;
        Vector3 p = _willEnEscena.position;
        if (p.y > -1.5f)
        {
            if (p.y < 0.6f) { _ultimoSueloDeWill = p; _haySueloDeWill = true; }
            return;
        }
        if (!_haySueloDeWill) _ultimoSueloDeWill = new Vector3(p.x, 0.05f, p.z);
        LlevarWillA(_ultimoSueloDeWill);
    }

    private void LlevarWillA(Vector3 destino)
    {
        var will = _willEnEscena != null ? _willEnEscena : ResolverActor("will", _willActor);
        if (will == null) return;
        if (will.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.position = destino;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        will.position = destino;
        Actor("will")?.ForzarEnElSuelo();
    }

    private void FrenarAWill()
    {
        var will = _willEnEscena != null ? _willEnEscena : ResolverActor("will", _willActor);
        if (will == null) return;
        if (will.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Actor("will")?.ForzarEnElSuelo();
    }

    private void LateUpdate()
    {
        if (!_enMarcha) return;
        VigilarCaidaDeWill();
        if (_levitando) AplicarLevitacion();
        if (_archimagoFlotando) AplicarFlotacionDelArchimago();
        ApoyarPosesDeSuelo();
        CerrarOjosRetenidos();
        if (_canas.Count > 0) ActualizarCanas();
    }

    // ── Zonas ────────────────────────────────────────────────────────────────────────────────

    private void ResolverZonasSiFaltan()
    {
        if (_zonaSenderoRoot == null) _zonaSenderoRoot = GameObject.Find("ZONA_1_EL_SENDERO");
        if (_zonaPiedraRoot == null) _zonaPiedraRoot = GameObject.Find("ZONA_2_PIEDRA_ANCESTRAL");
        if (_zonaRioRoot == null) _zonaRioRoot = GameObject.Find("ZONA_3_EL_RIO");
    }

    private void MostrarSoloZona(GameObject zona)
    {
        ResolverZonasSiFaltan();
        MostrarZona(_zonaSenderoRoot, zona == _zonaSenderoRoot);
        MostrarZona(_zonaPiedraRoot, zona == _zonaPiedraRoot);
        MostrarZona(_zonaRioRoot, zona == _zonaRioRoot);
    }

    /// Enseña o esconde una zona SIN apagarla: se apagan sus renderers y sus luces, pero sus
    /// colliders siguen ahí (apagar el objeto quitaba el suelo y Will caía al vacío). Los
    /// personajes y los cielos estrellados no se tocan aunque cuelguen de una zona. Ver INC-864.
    private void MostrarZona(GameObject zona, bool visible)
    {
        if (zona == null) return;
        if (!zona.activeSelf) zona.SetActive(true);
        bool oculta = _zonasOcultas.Contains(zona);
        if (visible != oculta) return;

        foreach (var r in zona.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || EsDeLasEstrellasNocturnas(r.transform) || EsDeUnActor(r.transform) || EsDeLosPortales(r.transform)) continue;
            if (_sueloPartido && r == _rendererDelSuelo) continue;
            if (visible)
            {
                if (_renderersDeZona.TryGetValue(r, out bool estaba)) r.enabled = estaba;
            }
            else
            {
                _renderersDeZona[r] = r.enabled;
                r.enabled = false;
            }
        }
        foreach (var l in zona.GetComponentsInChildren<Light>(true))
        {
            if (l == null || EsDeLasEstrellasNocturnas(l.transform) || EsDeUnActor(l.transform)) continue;
            if (visible)
            {
                if (_lucesDeZona.TryGetValue(l, out bool estaba)) l.enabled = estaba;
            }
            else
            {
                _lucesDeZona[l] = l.enabled;
                l.enabled = false;
            }
        }
        if (visible) _zonasOcultas.Remove(zona);
        else _zonasOcultas.Add(zona);
    }

    private bool EsDeLasEstrellasNocturnas(Transform t)
    {
        return (_estrellasNocturnasRoot != null && t.IsChildOf(_estrellasNocturnasRoot.transform))
            || (_estrellaTorcidaObjeto != null && t.IsChildOf(_estrellaTorcidaObjeto.transform));
    }

    /// Las estrellas del río de antes (esferas sueltas y la estrella torcida de cubos) se quedan
    /// apagadas: el cielo de cada noche lo pone CieloEstrelladoDelFinal y la estrella de Will, la
    /// constelación.
    private void OcultarEstrellasViejas()
    {
        if (_estrellasNocturnasRoot != null) _estrellasNocturnasRoot.SetActive(false);
        if (_estrellaTorcidaObjeto != null) _estrellaTorcidaObjeto.SetActive(false);
    }

    // ── Cielo y luz ──────────────────────────────────────────────────────────────────────────

    private Camera CamaraPrincipal()
    {
        var cam = _ctx?.Player?.CachedCamera;
        if (cam == null) cam = Camera.main;
        return cam;
    }

    /// El cielo de la cámara que rueda (y el de RenderSettings, para los reflejos).
    private void PonerCielo(Material cielo)
    {
        if (cielo == null) return;
        RenderSettings.skybox = cielo;
        var cam = CamaraPrincipal();
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.Skybox;
        var sky = cam.GetComponent<Skybox>();
        if (sky == null) sky = cam.gameObject.AddComponent<Skybox>();
        sky.material = cielo;
    }

    /// Quien publica el sol y la luna para el cielo del juego. En esta escena no hay ciclo
    /// día/noche, así que las direcciones las impone la cinemática (SolYLunaEnElCielo.AstrosImpuestos).
    private void AsegurarPublicadorDeAstros()
    {
        if (_publicadorDeAstros != null) return;
        _publicadorDeAstros = FindAnyObjectByType<SolYLunaEnElCielo>();
        if (_publicadorDeAstros == null) _publicadorDeAstros = gameObject.AddComponent<SolYLunaEnElCielo>();
    }

    private static Vector3 DireccionConAltura(Vector3 direccion, float grados)
    {
        Vector3 plano = new Vector3(direccion.x, 0f, direccion.z);
        if (plano.sqrMagnitude < 1e-4f) plano = Vector3.forward;
        plano.Normalize();
        float r = grados * Mathf.Deg2Rad;
        return (plano * Mathf.Cos(r) + Vector3.up * Mathf.Sin(r)).normalized;
    }

    private CieloEstrelladoDelFinal Cielo(bool especial)
    {
        if (especial)
        {
            if (_cieloEspecial == null) { _cieloEspecial = CieloEstrelladoDelFinal.Crear(transform, true, 2610); _instanciasTemporales.Add(_cieloEspecial.gameObject); }
            return _cieloEspecial;
        }
        if (_cieloNormal == null) { _cieloNormal = CieloEstrelladoDelFinal.Crear(transform, false, 1997); _instanciasTemporales.Add(_cieloNormal.gameObject); }
        return _cieloNormal;
    }

    /// La noche del mundo real: el cielo nocturno del juego con su luna y las estrellas.
    private void CieloDeNocheDelJuego(bool especial, float fundido)
    {
        PonerCielo(_skyboxNoche);
        AsegurarPublicadorDeAstros();
        SolYLunaEnElCielo.AstrosImpuestos = (DireccionConAltura(-DireccionDeLaLuna, -25f), DireccionDeLaLuna);
        var cielo = Cielo(especial);
        if (fundido > 0f) cielo.FundirA(1f, fundido); else cielo.BrilloYa(1f);
        var otro = especial ? _cieloNormal : _cieloEspecial;
        if (otro != null) otro.FundirA(0f, fundido);
    }

    private void ApagarEstrellas(float fundido)
    {
        if (_cieloNormal != null) _cieloNormal.FundirA(0f, fundido);
        if (_cieloEspecial != null) _cieloEspecial.FundirA(0f, fundido);
    }

    private void LuzDeNoche(float intensidad, Color ambiente)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambiente;
        RenderSettings.fog = false;
        if (_luzSolar != null)
        {
            _luzSolar.color = _colorLuzNoche;
            _luzSolar.intensity = intensidad;
            // La luz viene de la luna.
            _luzSolar.transform.rotation = Quaternion.LookRotation(-DireccionDeLaLuna, Vector3.up);
        }
    }

    // ── Ambientes ────────────────────────────────────────────────────────────────────────────

    private void AplicarNochePiedra()
    {
        ResolverZonasSiFaltan();
        MostrarZona(_zonaPiedraRoot, true);
        MostrarZona(_zonaRioRoot, false);
        MostrarZona(_zonaSenderoRoot, _portalAbierto);
        if (!_portalAbierto) _colapsoActivo = false;
        FrenarAWill();
        CieloDeNocheDelJuego(false, 0f);
        LuzDeNoche(0.5f, new Color(0.11f, 0.14f, 0.26f));
        MostrarActorEntero(ResolverActor("estela", _estelaActor));
        MostrarActorEntero(ResolverActor("liam", _liamActor));
    }

    private void AplicarAmbienteSendero()
    {
        MostrarSoloZona(_zonaSenderoRoot);
        ApagarEstrellas(0f);
        PonerCielo(_skyboxSendero);
        AsegurarPublicadorDeAstros();
        // En el Sendero no hay sol ni luna.
        SolYLunaEnElCielo.AstrosImpuestos = (Vector3.down, Vector3.down);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.24f, 0.28f, 0.44f);
        RenderSettings.fog = false;
        if (_luzSolar != null)
        {
            _luzSolar.color = _colorSolSendero;
            _luzSolar.intensity = 1.15f;
            _luzSolar.transform.rotation = Quaternion.Euler(38f, -30f, 0f);
        }
        var liam = ResolverActor("liam", _liamActor);
        MostrarActorEntero(liam);
        var emotion = liam != null ? liam.GetComponentInChildren<NPCEmotionController>() : null;
        if (emotion != null)
        {
            emotion.AplicarOjos("Eye09");
            emotion.SetCaraDeFondo(this, NPCEmotion.Tired);
        }
    }

    private bool _encadenandoParte2;

    private void EncadenarParte2()
    {
        _encadenandoParte2 = true;
        _ctx?.Player?.RequestEnd("FINAL_PARTE_02_START", SequenceEndScreen.QuedarseEnNegro);
    }

    // ── Reloj del guion y música ─────────────────────────────────────────────────────────────

    private bool _relojRetenidoPorMi;
    private bool _relojAcompasado;

    /// Segundo del guion en curso (el reloj horneado), o -1 si no hay guion.
    private static float TiempoDelGuion() => GuionEnMarcha.Actual != null ? GuionEnMarcha.Actual.Tiempo : -1f;

    /// Segundo del guion en que cae la etiqueta (@nombre), o -1 si no existe.
    private static float Etiqueta(string nombre)
    {
        var g = GuionEnMarcha.Actual?.Guion;
        if (g?.etiquetas == null) return -1f;
        foreach (var e in g.etiquetas) if (e.nombre == nombre) return e.t;
        return -1f;
    }

    /// Un punto del guion (PUNTOS), o el respaldo si no está.
    private static Vector3 PuntoDelGuion(string nombre, Vector3 respaldo)
    {
        var g = GuionEnMarcha.Actual?.Guion;
        if (g?.puntos != null)
            foreach (var p in g.puntos) if (p.nombre == nombre) return p.posicion;
        return respaldo;
    }

    /// Pone un tema empezando en el segundo justo para que el segundo <paramref name="segundoDelTema"/>
    /// suene cuando el guion llegue a la etiqueta <paramref name="etiqueta"/>.
    private void TemaACompas(string musicId, string etiqueta, float segundoDelTema, float fundido, float minimo)
    {
        float ahora = TiempoDelGuion();
        float t = Etiqueta(etiqueta);
        float desde = t >= 0f && ahora >= 0f ? segundoDelTema - (t - ahora) : minimo;
        desde = Mathf.Max(minimo, desde);
        _ctx?.Player?.PlayMusic(musicId, false, fundido, desde);
    }

    // ── Efectos ──────────────────────────────────────────────────────────────────────────────

    /// Un efecto de una vez. Si no lleva escala ni padre va al pool; si no, se instancia y se
    /// recoge al acabar su vida o la cinemática.
    private Transform Efecto(GameObject prefab, Vector3 posicion, Quaternion rotacion, float vida, float escala = 1f, Transform padre = null)
    {
        if (prefab == null) return null;
        if (Mathf.Approximately(escala, 1f) && padre == null && vida > 0f && VfxPoolService.Instance != null)
            return VfxPoolService.Instance.Play(prefab, posicion, rotacion, vida);
        var go = Instantiate(prefab, posicion, rotacion, padre);
        go.transform.localScale = prefab.transform.localScale * escala;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        _instanciasTemporales.Add(go);
        if (vida > 0f) Destroy(go, vida);
        return go.transform;
    }

    /// Apaga un efecto de bucle dejando que sus partículas terminen, y lo recoge después.
    private void ApagarEfecto(Transform efecto, float recogerEn = 3f)
    {
        if (efecto == null) return;
        foreach (var ps in efecto.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (var l in efecto.GetComponentsInChildren<Light>(true))
            StartCoroutine(Co_ApagarLuz(l, 0.8f));
        Destroy(efecto.gameObject, recogerEn);
    }

    private IEnumerator Co_ApagarLuz(Light l, float segundos)
    {
        if (l == null) yield break;
        float desde = l.intensity;
        for (float t = 0f; t < segundos && l != null; t += Time.deltaTime)
        {
            l.intensity = Mathf.Lerp(desde, 0f, t / segundos);
            yield return null;
        }
        if (l != null) l.enabled = false;
    }

    private Light CrearLuz(string nombre, Vector3 posicion, Color color, float intensidad, float rango, Transform padre = null)
    {
        var go = new GameObject(nombre);
        if (padre != null) go.transform.SetParent(padre, false);
        go.transform.position = posicion;
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = rango;
        luz.shadows = LightShadows.None;
        _instanciasTemporales.Add(go);
        return luz;
    }

    /// Un fogonazo de luz que sube y baja en <paramref name="duracion"/> segundos.
    private void PulsoDeLuz(Vector3 posicion, Color color, float intensidadMax, float rango, float duracion)
    {
        var luz = CrearLuz("Pulso_De_Luz", posicion, color, 0f, rango);
        StartCoroutine(Co_PulsoDeLuz(luz, intensidadMax, duracion));
    }

    private IEnumerator Co_PulsoDeLuz(Light luz, float maximo, float duracion)
    {
        for (float t = 0f; t < duracion && luz != null; t += Time.deltaTime)
        {
            luz.intensity = Mathf.Sin(Mathf.Clamp01(t / duracion) * Mathf.PI) * maximo;
            yield return null;
        }
        if (luz != null)
        {
            _instanciasTemporales.Remove(luz.gameObject);
            Destroy(luz.gameObject);
        }
    }

    /// Material unlit transparente y brillante para chispas, fragmentos y orbes hechos aquí.
    private Material MaterialDeLuz(Color colorHdr)
    {
        var m = new Material(Shader.Find("Sprites/Default")) { color = colorHdr, name = "Luz de la cinemática" };
        return m;
    }

    /// Chispas de luz que suben desde un punto (alguien que se desvanece o se materializa).
    private ParticleSystem Chispas(Vector3 posicion, Color color, float porSegundo, float radio, float subida, Transform padre = null)
    {
        var go = new GameObject("Chispas_De_Luz");
        if (padre != null) go.transform.SetParent(padre, false);
        go.transform.position = posicion;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        var emision = ps.emission;
        emision.rateOverTime = porSegundo;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(radio * 2f, 1.6f, radio * 2f);
        forma.position = new Vector3(0f, 0.8f, 0f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        vel.y = new ParticleSystem.MinMaxCurve(subida * 0.6f, subida);
        vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = MaterialDeLuz(color * 2.4f);
        mat.mainTexture = TexturaDeChispa();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        _instanciasTemporales.Add(go);
        ps.Play();
        return ps;
    }

    private static Texture2D s_texturaDeChispa;

    private static Texture2D TexturaDeChispa()
    {
        if (s_texturaDeChispa != null) return s_texturaDeChispa;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Chispa de la cinemática", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f));
        }
        tex.SetPixels(px);
        tex.Apply();
        s_texturaDeChispa = tex;
        return tex;
    }

    // ── Créditos ─────────────────────────────────────────────────────────────────────────────

    private IEnumerator Co_IrACreditos()
    {
        yield return new WaitForSecondsRealtime(0.8f);
        if (Application.CanStreamedLevelBeLoaded("Credits"))
            SceneManager.LoadScene("Credits");
    }
}
