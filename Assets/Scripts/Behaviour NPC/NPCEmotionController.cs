using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// Controla la cara de reposo, las reacciones y las bocas del habla del personaje.
public class NPCEmotionController : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private EmotionProfile emotionProfile;
    [Header("Cara de reposo")]
    [Tooltip("Ojos neutros de este personaje.")]
    [FormerlySerializedAs("originalEyeMesh")]
    [SerializeField] private GameObject ojosDeReposo;
    [Tooltip("Boca neutra de este personaje.")]
    [FormerlySerializedAs("originalMouthMesh")]
    [SerializeField] private GameObject bocaDeReposo;
    [Header("Búsqueda automática")]
    [SerializeField] private string eyePrefix = "Eye";
    [SerializeField] private string mouthPrefix = "Mouth";
    [Tooltip("Prefijo de las piezas de símbolos de anime (lágrimas, sudor, vena…), que pone cada emoción del perfil.")]
    [SerializeField] private string simboloPrefix = "Fx";
    [Tooltip("Segundos por fotograma de un símbolo cuando su emoción no dice otra cosa.")]
    [SerializeField, Min(0.02f)] private float segundosPorSimboloPorDefecto = 0.12f;

    [Header("Parpadeo automático")]
    [SerializeField] private bool parpadeoAutomatico = true;
    [SerializeField, Min(0.1f)] private float intervaloMinimoParpadeo = 2f;
    [SerializeField, Min(0.1f)] private float intervaloMaximoParpadeo = 6f;
    [SerializeField, Min(0.01f)] private float duracionParpadeo = 0.12f;
    [SerializeField, Range(0f, 1f)] private float probabilidadDobleParpadeo = 0.15f;
    [Tooltip("Malla de ojos cerrados del sistema modular. Vacío desactiva el parpadeo.")]
    [SerializeField] private string ojosDelParpadeo = "Eye09";
    [Tooltip("Expresiones que ya representan ojos cerrados y no se interrumpen con parpadeos.")]
    [SerializeField] private string[] ojosYaCerrados = { "Eye07", "Eye09" };

    [Header("Barba que tapa la boca")]
    [Tooltip("Piezas (por nombre) que tapan la boca, como las barbas largas. Mientras habla se estira hacia abajo solo su parte de abajo y de delante (la barba), como si moviera la mandíbula; lo de arriba (pelo, cráneo) no se mueve. La malla tiene que tener Read/Write. Ver INC-1090 e INC-1184.")]
    [SerializeField] private string[] piezasQueBajanAlHablar = { "Head05_Santa" };
    [Tooltip("Metros que baja la punta de la barba con la boca abierta (con la entreabierta, la mitad).")]
    [SerializeField, Min(0f)] private float metrosQueBajaAlHablar = 0.02f;
    [Tooltip("Altura de la línea de la boca, en fracción de la pieza contada desde abajo: lo que está por encima no se mueve.")]
    [SerializeField, Range(0f, 1f)] private float alturaDeLaBoca = 0.30f;
    [Tooltip("Por debajo de esta altura (fracción de la pieza desde abajo) la barba baja entera; entre esta y la de la boca, cada vez menos.")]
    [SerializeField, Range(0f, 1f)] private float alturaQueBajaEntera = 0.12f;
    [Tooltip("Qué parte de la pieza, de atrás (-1) a delante (1), se mueve: nada detrás del primer valor, todo delante del segundo. Así la nuca no baja con la barba.")]
    [SerializeField] private Vector2 franjaDeDelante = new Vector2(-0.1f, 0.5f);
    [Tooltip("Si hay una barba que tapa la boca, las bocas no se pintan: al bajar la barba asomarían por encima de ella.")]
    [SerializeField] private bool ocultarBocaBajoLaBarba = true;

    // Una sola corrutina actualiza todas las caras; su propietario se releva al desactivarse.
    private static readonly List<NPCEmotionController> CarasActivas = new List<NPCEmotionController>();
    private static NPCEmotionController _duenoDelTick;
    private static Coroutine _tick;
    private readonly Dictionary<string, Renderer[]> _renderersDeOjos = new Dictionary<string, Renderer[]>();
    private Renderer[] _ojosVisibles;
    private string _ojosDeLaCara;
    private float _proximoParpadeo;
    private int _faseParpadeo;
    private bool _dobleParpadeo;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (_duenoDelTick != null && _tick != null) _duenoDelTick.StopCoroutine(_tick);
        _tick = null;
        _duenoDelTick = null;
        CarasActivas.Clear();
    }
#endif

    private Game.NPC.NPCBehaviourManagerV2 _npcManager;
    private readonly Dictionary<string, GameObject> _eyeMeshes = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _mouthMeshes = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _simbolos = new Dictionary<string, GameObject>();
    private string[] _fotogramasDelSimbolo;
    private float _segundosPorFotograma, _proximoFotograma;
    private int _fotograma;
    private string _simboloVisible = "\0";   // ninguno resuelto todavía: el primer cambio repasa todas las piezas
    private string _ojosDeReposo, _bocaDeReposo, _bocaDeLaCara, _bocaDuranteHabla;
    private NPCEmotion _currentEmotion = NPCEmotion.Neutral;
    private bool _isInDialogue, _hablando, _controladorDeHablaResuelto, _esControladorDeHabla, _haTenidoVoz, _avisadoSinPerfil, _avisadoSinBocasDelHabla;
    private bool _tieneOjos, _tieneBoca;
    private float _finDelHabla, _proximoCambioDeBoca;
    private float _finDeReaccion = float.PositiveInfinity;
    private BarbaQueBaja[] _barbas;
    private float _aperturaDeLaBarba;
    private bool _bocaBajoLaBarba;

    /// Se dispara cuando cambia la emoción actual, incluida la vuelta al reposo.
    public event System.Action<NPCEmotion> OnEmotionChanged;

    void Awake()
    {
        _npcManager = GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        CacheMeshesRecursive(transform);
        foreach (var ojos in _eyeMeshes)
            _renderersDeOjos.Add(ojos.Key, ojos.Value.GetComponentsInChildren<Renderer>(true));
        // Una parte sin malla de reposo asignada ni malla activa está tapada: no se toca. Ver INC-562.
        _tieneOjos = TieneParte(ojosDeReposo, _eyeMeshes);
        _tieneBoca = TieneParte(bocaDeReposo, _mouthMeshes);
        ResolverReposo();
        BuscarPiezasQueBajan();
    }
    void Start() => VolverAReposo();
    void OnDestroy() => SoltarBarbas();
    void OnEnable()
    {
        ProgramarParpadeo();
        if (!CarasActivas.Contains(this)) CarasActivas.Add(this);
        AsegurarTick();
        SenalesDeHabla.OnEmpiezaAHablar += EmpezarHabla;
        SenalesDeHabla.OnDejaDeHablar += AlPararHabla;
        DialogueManager.OnDialogueStarted += OnDialogueStarted;
        DialogueManager.OnDialogueClosed += OnDialogueClosed;
        DialogueManager.OnDialogueLineChanged += OnDialogueLineChanged;
    }
    void OnDisable()
    {
        BajarPiezas(0f);
        CarasActivas.Remove(this);
        if (_duenoDelTick == this)
        {
            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
            _duenoDelTick = null;
            AsegurarTick();
        }
        SenalesDeHabla.OnEmpiezaAHablar -= EmpezarHabla;
        SenalesDeHabla.OnDejaDeHablar -= AlPararHabla;
        DialogueManager.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        DialogueManager.OnDialogueLineChanged -= OnDialogueLineChanged;
        PararHabla();
        VolverAReposo();
        _isInDialogue = false;
    }
    private void CacheMeshesRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (EsPiezaDelPrefijo(child.name, eyePrefix) && !_eyeMeshes.ContainsKey(child.name)) _eyeMeshes[child.name] = child.gameObject;
            if (EsPiezaDelPrefijo(child.name, mouthPrefix) && !_mouthMeshes.ContainsKey(child.name)) _mouthMeshes[child.name] = child.gameObject;
            if (EsPiezaDelPrefijo(child.name, simboloPrefix) && !_simbolos.ContainsKey(child.name)) _simbolos[child.name] = child.gameObject;
            CacheMeshesRecursive(child);
        }
    }

    /// Una pieza del rig modular se llama por prefijo y número («Eye01», «Mouth07»). El dígito
    /// que va justo detrás del prefijo es lo que distingue una pieza de ojo de una de ceja:
    /// «Eyebrow01» empieza igual que «Eye…», pero no es un ojo. Si se recogiera como malla de
    /// ojo, cada cambio de cara apagaría la ceja y la ceja equipada desaparecería.
    private static bool EsPiezaDelPrefijo(string nombre, string prefijo)
        => !string.IsNullOrEmpty(prefijo)
           && nombre.StartsWith(prefijo, System.StringComparison.Ordinal)
           && nombre.Length > prefijo.Length
           && char.IsDigit(nombre[prefijo.Length]);

    private static bool TieneParte(GameObject asignada, Dictionary<string, GameObject> mallas)
    {
        if (asignada != null) return true;
        foreach (var malla in mallas)
            if (malla.Value != null && malla.Value.activeSelf) return true;
        return false;
    }
    private void ResolverReposo()
    {
        _ojosDeReposo = ResolverMalla(ojosDeReposo, _eyeMeshes, true);
        _bocaDeReposo = ResolverMalla(bocaDeReposo, _mouthMeshes, false);
    }
    private string ResolverMalla(GameObject asignada, Dictionary<string, GameObject> mallas, bool ojos)
    {
        if (!(ojos ? _tieneOjos : _tieneBoca)) return null;
        if (asignada != null) return asignada.name;
        if (emotionProfile == null) return null;
        foreach (var malla in mallas)
            if (malla.Value != null && malla.Value.activeSelf &&
                (ojos ? emotionProfile.EsOjoNeutro(malla.Key) : emotionProfile.EsBocaNeutra(malla.Key))) return malla.Key;
        string defecto = ojos ? emotionProfile.ojosDeReposoPorDefecto : emotionProfile.bocaDeReposoPorDefecto;
        return !string.IsNullOrEmpty(defecto) && mallas.ContainsKey(defecto) ? defecto : null;
    }
    private void OnDialogueStarted(Transform npcInvolved)
    {
        if (npcInvolved == transform) _isInDialogue = true;
    }
    private void OnDialogueClosed(Transform npcInvolved)
    {
        if (!_isInDialogue) return;
        _isInDialogue = false;
        VolverAReposo();
    }
    private void OnDialogueLineChanged(DialogueLine line, Transform npcInvolved)
    {
        string id = _npcManager?.DialogueCharacterId;
        if (npcInvolved != transform && (string.IsNullOrEmpty(id) || line.speakerNameId != id)) return;
        _isInDialogue = true;
        if (line.emotion != NPCEmotion.None) Reaccionar(line.emotion);
    }
    public void SetEmotion(NPCEmotion emotion)
    {
        if (emotion == NPCEmotion.None) return;
        _finDeReaccion = float.PositiveInfinity;
        if (emotion == NPCEmotion.Neutral) { VolverAReposo(); return; }
        if (emotionProfile == null)
        {
            // Avisa una vez si falta el perfil necesario para cambiar la expresión.
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (!_avisadoSinPerfil)
            {
                _avisadoSinPerfil = true;
                Debug.LogWarning($"[NPCEmotionController:{name}] No tiene EmotionProfile asignado; no puede cambiar de expresión.", this);
            }
#endif
            return;
        }
        var datos = emotionProfile.GetEmotionData(emotion);
        AplicarCara(string.IsNullOrEmpty(datos.eyeMeshName) ? _ojosDeReposo : datos.eyeMeshName,
            string.IsNullOrEmpty(datos.mouthMeshName) ? _bocaDeReposo : datos.mouthMeshName);
        PonerSimbolo(datos.simbolos, datos.segundosPorSimbolo);
        FijarEmocion(emotion);
    }

    private void FijarEmocion(NPCEmotion emocion)
    {
        if (_currentEmotion == emocion) return;
        _currentEmotion = emocion;
        OnEmotionChanged?.Invoke(emocion);
    }
    /// Sustituye los ojos sin cambiar boca, emoción ni pose (hasta el próximo cambio de cara; el parpadeo los respeta).
    /// Los ojos cerrados del pack («Eye09», el ojosDelParpadeo del controlador) valen como «los ojos cerrados de este
    /// personaje»: si su perfil trae otros (los juegos de caras nuevos), se ponen esos.
    public void AplicarOjos(string malla)
    {
        if (string.IsNullOrEmpty(malla)) return;
        if (malla == ojosDelParpadeo) malla = OjosDelParpadeo;
        if (!_eyeMeshes.ContainsKey(malla)) return;
        _faseParpadeo = 0;
        _ojosDeLaCara = malla;
        ActivateMesh(_eyeMeshes, malla, "ojos", false);
    }
    public void Reaccionar(NPCEmotion emotion, float segundos = -1f)
    {
        if (emotion == NPCEmotion.None) return;
        SetEmotion(emotion);
        if (emotion != NPCEmotion.Neutral && emotionProfile != null)
        {
            float dura = segundos > 0f ? segundos : emotionProfile.segundosDeReaccion;
            // Las reacciones de sorpresa (ojos Eye05) son un espasmo breve; no deben congelarse durante toda una frase larga.
            if (emotion == NPCEmotion.Surprised) dura = Mathf.Min(dura, 0.9f);
            _finDeReaccion = Time.unscaledTime + dura;
        }
    }
    private object _propietarioDeFondo;
    private NPCEmotion _caraDeFondo = NPCEmotion.None;
    private bool _fondoSuave;
    public void SetCaraDeFondo(object propietario, NPCEmotion emocion, bool suave = false, bool aplicarAhora = true)
    {
        _propietarioDeFondo = propietario;
        _caraDeFondo = emocion;
        _fondoSuave = suave;
        if (aplicarAhora) VolverAReposo();
    }
    /// Quién ha puesto la cara de fondo (null si nadie): para no quitársela a otro sistema.
    public object PropietarioDeCaraDeFondo => _propietarioDeFondo;
    public void ClearCaraDeFondo(object propietario)
    {
        if (!ReferenceEquals(_propietarioDeFondo, propietario)) return;
        _propietarioDeFondo = null;
        _caraDeFondo = NPCEmotion.None;
        VolverAReposo();
    }
    public void VolverAReposo()
    {
        _finDeReaccion = float.PositiveInfinity;
        NPCEmotion emocion = _caraDeFondo == NPCEmotion.None ? NPCEmotion.Neutral : _caraDeFondo;
        if (_caraDeFondo != NPCEmotion.None && _caraDeFondo != NPCEmotion.Neutral && emotionProfile != null)
        {
            var datos = emotionProfile.GetEmotionData(_caraDeFondo);
            AplicarCara(_fondoSuave || string.IsNullOrEmpty(datos.eyeMeshName) ? _ojosDeReposo : datos.eyeMeshName,
                string.IsNullOrEmpty(datos.mouthMeshName) ? _bocaDeReposo : datos.mouthMeshName);
            // Un fondo suave es solo la boca: sin símbolo
            if (_fondoSuave) PonerSimbolo(null, 0f); else PonerSimbolo(datos.simbolos, datos.segundosPorSimbolo);
        }
        else
        {
            AplicarCara(_ojosDeReposo, _bocaDeReposo);
            PonerSimbolo(null, 0f);
        }
        FijarEmocion(emocion);
    }
    private void AplicarCara(string ojos, string boca)
    {
        _faseParpadeo = 0;
        ProgramarParpadeo();
        // Conserva la expresión real incluso cuando se sustituye temporalmente por ojos cerrados.
        if (!string.IsNullOrEmpty(ojos) && _eyeMeshes.ContainsKey(ojos)) _ojosDeLaCara = ojos;
        ActivateMesh(_eyeMeshes, ojos, "ojos", false);
        _bocaDeLaCara = boca;
        if (!_hablando) ActivateMesh(_mouthMeshes, boca, "boca", false);
    }
    /// Enciende el primer fotograma del símbolo de la emoción (o apaga todos); los demás los pasa el tick.
    private void PonerSimbolo(string[] fotogramas, float segundos)
    {
        _fotogramasDelSimbolo = _tieneOjos && fotogramas != null && fotogramas.Length > 0 ? fotogramas : null;
        _segundosPorFotograma = segundos > 0f ? segundos : segundosPorSimboloPorDefecto;
        _fotograma = 0;
        _proximoFotograma = Time.unscaledTime + _segundosPorFotograma;
        EncenderSimbolo(_fotogramasDelSimbolo != null ? _fotogramasDelSimbolo[0] : null);
    }
    private void EncenderSimbolo(string nombre)
    {
        if (nombre == _simboloVisible) return;
        _simboloVisible = nombre;
        foreach (var simbolo in _simbolos)
        {
            if (simbolo.Value == null) continue;
            bool activa = simbolo.Key == nombre;
            if (simbolo.Value.activeSelf != activa) simbolo.Value.SetActive(activa);
        }
    }
    private void ActualizarSimbolo()
    {
        if (_fotogramasDelSimbolo == null || _fotogramasDelSimbolo.Length < 2) return;
        float ahora = Time.unscaledTime;
        if (ahora < _proximoFotograma) return;
        _fotograma = (_fotograma + 1) % _fotogramasDelSimbolo.Length;
        _proximoFotograma = ahora + _segundosPorFotograma;
        EncenderSimbolo(_fotogramasDelSimbolo[_fotograma]);
    }
    private bool EsEsteHablante(Transform quien)
        => quien != null && (quien == transform || transform.IsChildOf(quien) || quien.IsChildOf(transform));
    private bool ExisteBoca(string nombre)
        => !string.IsNullOrEmpty(nombre) && _mouthMeshes.TryGetValue(nombre, out var boca) && boca != null;
    private bool TieneBocasDelHabla()
        => emotionProfile != null && ExisteBoca(emotionProfile.bocaHablandoEntreabierta)
            && ExisteBoca(emotionProfile.bocaHablandoAbierta);
    private void EmpezarHabla(Transform quien, float segundos)
    {
        if (!EsEsteHablante(quien)) return;
        if (!_controladorDeHablaResuelto)
        {
            // Compara las mallas cacheadas tras Awake para resolver el controlador del hablante.
            _esControladorDeHabla = Game.NPC.Common.EmotionControllerResolver.Resolve(gameObject) == this;
            _controladorDeHablaResuelto = true;
        }
        if (!_esControladorDeHabla) return;
        if (!TieneBocasDelHabla())
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (!_avisadoSinBocasDelHabla && emotionProfile != null)
            {
                _avisadoSinBocasDelHabla = true;
                string faltantes = "";
                if (!ExisteBoca(emotionProfile.bocaHablandoEntreabierta))
                    faltantes = string.IsNullOrEmpty(emotionProfile.bocaHablandoEntreabierta) ? "(entreabierta sin configurar)" : emotionProfile.bocaHablandoEntreabierta;
                if (!ExisteBoca(emotionProfile.bocaHablandoAbierta))
                    faltantes += (faltantes.Length > 0 ? ", " : "") + (string.IsNullOrEmpty(emotionProfile.bocaHablandoAbierta) ? "(abierta sin configurar)" : emotionProfile.bocaHablandoAbierta);
                Debug.LogWarning($"[NPCEmotionController:{name}] Faltan mallas de hablar: {faltantes}. Usa El Sendero/Diálogos/Completar caras de todos los personajes.", this);
            }
#endif
            return;
        }
        if (!_tieneBoca) return;
        if (!_hablando)
        {
            _bocaDuranteHabla = _bocaDeLaCara;
            _haTenidoVoz = false;
            _hablando = true;
            AplicarBocaDelHabla(emotionProfile.bocaHablandoAbierta);
        }
        _hablando = true;
        _finDelHabla = segundos > 0f ? Time.unscaledTime + segundos : float.PositiveInfinity;
        var audio = AudioService.Instance;
        float intervalo = audio != null && audio.HayVozSonando
            ? emotionProfile.tiempoMinimoPorBocaConVoz : emotionProfile.segundosPorBoca;
        _proximoCambioDeBoca = Time.unscaledTime + Mathf.Max(0.01f, intervalo);
    }
    private void AlPararHabla(Transform quien)
    {
        if (EsEsteHablante(quien)) PararHabla();
    }
    private void PararHabla()
    {
        if (!_hablando) return;
        _hablando = false;
        _haTenidoVoz = false;
        AplicarBocaDelHabla(_bocaDeLaCara);
    }
    private void AplicarBocaDelHabla(string boca)
    {
        if (boca == _bocaDuranteHabla) return;
        ActivateMesh(_mouthMeshes, boca, "boca", false);
        _bocaDuranteHabla = boca;
        float apertura = !_hablando || emotionProfile == null ? 0f
            : boca == emotionProfile.bocaHablandoAbierta ? 1f
            : boca == emotionProfile.bocaHablandoEntreabierta ? 0.5f : 0f;
        BajarPiezas(apertura);
    }

    /// Las piezas activas que tapan la boca (barbas): para cada una se preparan dos copias de su malla con
    /// la barba entreabierta y abierta, que se cambian por la de reposo al hablar. Ver INC-1090 e INC-1184.
    private void BuscarPiezasQueBajan()
    {
        if (piezasQueBajanAlHablar == null || piezasQueBajanAlHablar.Length == 0) return;
        var barbas = new List<BarbaQueBaja>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.gameObject.activeSelf || System.Array.IndexOf(piezasQueBajanAlHablar, t.name) < 0) continue;
            var barba = BarbaQueBaja.Preparar(t, transform, metrosQueBajaAlHablar, alturaDeLaBoca, alturaQueBajaEntera, franjaDeDelante);
            if (barba != null) barbas.Add(barba);
        }
        if (barbas.Count == 0) return;
        _barbas = barbas.ToArray();
        _bocaBajoLaBarba = ocultarBocaBajoLaBarba;
        if (!_bocaBajoLaBarba) return;
        foreach (var boca in _mouthMeshes.Values)
        {
            var r = boca != null ? boca.GetComponent<Renderer>() : null;
            if (r != null) r.enabled = false;
        }
    }

    private void BajarPiezas(float apertura)
    {
        if (_barbas == null || apertura == _aperturaDeLaBarba) return;
        _aperturaDeLaBarba = apertura;
        for (int i = 0; i < _barbas.Length; i++) _barbas[i]?.Poner(apertura);
    }

    private void SoltarBarbas()
    {
        if (_barbas == null) return;
        for (int i = 0; i < _barbas.Length; i++) _barbas[i]?.Soltar();
        _barbas = null;
    }

    /// Una pieza con barba y sus tres mallas: reposo (la del modelo), entreabierta y abierta.
    private sealed class BarbaQueBaja
    {
        private MeshFilter _filtro;
        private SkinnedMeshRenderer _piel;
        private Mesh _reposo, _entreabierta, _abierta;

        public static BarbaQueBaja Preparar(Transform pieza, Transform personaje, float metros,
                                             float alturaDeLaBoca, float alturaQueBajaEntera, Vector2 franjaDeDelante)
        {
            var filtro = pieza.GetComponent<MeshFilter>();
            var piel = filtro == null ? pieza.GetComponent<SkinnedMeshRenderer>() : null;
            var malla = filtro != null ? filtro.sharedMesh : piel != null ? piel.sharedMesh : null;
            if (malla == null) return null;
            if (!malla.isReadable)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[NPCEmotionController] La malla {malla.name} de {personaje.name} no tiene Read/Write: su barba no se moverá al hablar.", pieza);
#endif
                return null;
            }
            // Arriba, delante y la bajada, en el espacio de la malla, con el personaje de pie.
            var arriba = pieza.InverseTransformDirection(personaje.up).normalized;
            var delante = pieza.InverseTransformDirection(personaje.forward).normalized;
            var bajada = pieza.InverseTransformVector(-personaje.up * metros);
            var vertices = malla.vertices;
            float hMin = float.MaxValue, hMax = float.MinValue, dMin = float.MaxValue, dMax = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                float h = Vector3.Dot(vertices[i], arriba), d = Vector3.Dot(vertices[i], delante);
                if (h < hMin) hMin = h; if (h > hMax) hMax = h;
                if (d < dMin) dMin = d; if (d > dMax) dMax = d;
            }
            if (hMax - hMin < 1e-5f || dMax - dMin < 1e-5f) return null;
            float dCentro = (dMin + dMax) * 0.5f, dMitad = (dMax - dMin) * 0.5f;
            var pesos = new float[vertices.Length];
            bool algo = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                float h = (Vector3.Dot(vertices[i], arriba) - hMin) / (hMax - hMin);
                float d = (Vector3.Dot(vertices[i], delante) - dCentro) / dMitad;
                pesos[i] = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alturaQueBajaEntera, alturaDeLaBoca, h)))
                         * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(franjaDeDelante.x, franjaDeDelante.y, d));
                algo |= pesos[i] > 0f;
            }
            if (!algo) return null;
            return new BarbaQueBaja
            {
                _filtro = filtro, _piel = piel, _reposo = malla,
                _entreabierta = Copiar(malla, vertices, pesos, bajada * 0.5f, "entreabierta"),
                _abierta = Copiar(malla, vertices, pesos, bajada, "abierta"),
            };
        }

        private static Mesh Copiar(Mesh malla, Vector3[] vertices, float[] pesos, Vector3 bajada, string sufijo)
        {
            var copia = Object.Instantiate(malla);
            copia.name = $"{malla.name} ({sufijo})";
            copia.hideFlags = HideFlags.DontSave;
            var movidos = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) movidos[i] = vertices[i] + bajada * pesos[i];
            copia.vertices = movidos;
            copia.RecalculateBounds();
            return copia;
        }

        public void Poner(float apertura)
        {
            var malla = apertura >= 0.75f ? _abierta : apertura > 0f ? _entreabierta : _reposo;
            if (_filtro != null) { if (_filtro.sharedMesh != malla) _filtro.sharedMesh = malla; }
            else if (_piel != null && _piel.sharedMesh != malla) _piel.sharedMesh = malla;
        }

        /// Devuelve la malla del modelo y libera las copias.
        public void Soltar()
        {
            Poner(0f);
            if (_entreabierta != null) Object.Destroy(_entreabierta);
            if (_abierta != null) Object.Destroy(_abierta);
        }
    }

    private void ActivateMesh(Dictionary<string, GameObject> mallas, string nombre, string tipo, bool diagnostico = true)
    {
        if ((mallas == _eyeMeshes && !_tieneOjos) || (mallas == _mouthMeshes && !_tieneBoca)) return;
        if (string.IsNullOrEmpty(nombre) || !mallas.TryGetValue(nombre, out var destino) || destino == null) return;
        if (mallas == _eyeMeshes && _renderersDeOjos.TryGetValue(nombre, out _ojosVisibles) && _ojosVisibles != null)
        {
            for (int i = 0; i < _ojosVisibles.Length; i++)
                if (_ojosVisibles[i] != null && !_ojosVisibles[i].enabled) _ojosVisibles[i].enabled = true;
        }
        else if (mallas == _mouthMeshes)
        {
            var rBoca = destino.GetComponent<Renderer>();
            if (rBoca != null && rBoca.enabled == _bocaBajoLaBarba) rBoca.enabled = !_bocaBajoLaBarba;
        }
        foreach (var malla in mallas)
        {
            if (malla.Value == null) continue;
            bool activa = malla.Key == nombre;
            if (malla.Value.activeSelf != activa) malla.Value.SetActive(activa);
        }
    }
    private static void AsegurarTick()
    {
        if (_duenoDelTick != null && _duenoDelTick.isActiveAndEnabled &&
            _duenoDelTick.gameObject.activeInHierarchy && _tick != null) return;
        if (_duenoDelTick != null && _tick != null) _duenoDelTick.StopCoroutine(_tick);
        _duenoDelTick = null;
        _tick = null;
        foreach (var candidata in CarasActivas)
        {
            // Al desactivar una jerarquía, otros OnDisable pueden seguir pendientes.
            if (candidata == null || !candidata.isActiveAndEnabled ||
                !candidata.gameObject.activeInHierarchy) continue;
            _duenoDelTick = candidata;
            _tick = candidata.StartCoroutine(ActualizarCaras());
            break;
        }
    }
    private static IEnumerator ActualizarCaras()
    {
        while (true)
        {
            yield return null;
            for (int i = CarasActivas.Count - 1; i >= 0; i--)
            {
                if (i >= CarasActivas.Count) continue;
                var cara = CarasActivas[i];
                if (cara == null || !cara.isActiveAndEnabled) continue;
                cara.ActualizarParpadeo();
                cara.ActualizarSimbolo();
                cara.ActualizarCara();
            }
        }
    }
    private void ProgramarParpadeo()
    {
        float minimo = Mathf.Max(0.1f, intervaloMinimoParpadeo);
        _proximoParpadeo = Time.time + Random.Range(minimo, Mathf.Max(minimo, intervaloMaximoParpadeo));
    }
    private bool SeVenLosOjos()
    {
        if (_ojosVisibles == null) return false;
        for (int i = 0; i < _ojosVisibles.Length; i++)
        {
            var renderer = _ojosVisibles[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.isVisible) return true;
        }
        return false;
    }
    /// Los ojos cerrados los pone el perfil si los trae (cada juego de caras tiene los suyos); si no, los de aquí.
    private string OjosDelParpadeo => emotionProfile != null && !string.IsNullOrEmpty(emotionProfile.ojosDelParpadeo) ? emotionProfile.ojosDelParpadeo : ojosDelParpadeo;
    private string[] OjosYaCerrados => emotionProfile != null && emotionProfile.ojosYaCerrados != null && emotionProfile.ojosYaCerrados.Length > 0 ? emotionProfile.ojosYaCerrados : ojosYaCerrados;
    private bool CaraConOjosCerrados()
    {
        if (_ojosDeLaCara == OjosDelParpadeo) return true;
        var cerrados = OjosYaCerrados;
        if (cerrados == null) return false;
        for (int i = 0; i < cerrados.Length; i++)
            if (_ojosDeLaCara == cerrados[i]) return true;
        return false;
    }
    private void ActualizarParpadeo()
    {
        if (Time.deltaTime <= 0f) return;
        if (!parpadeoAutomatico || !_tieneOjos || CaraConOjosCerrados())
        {
            if (_faseParpadeo != 0) ActivateMesh(_eyeMeshes, _ojosDeLaCara, "ojos", false);
            _faseParpadeo = 0;
            return;
        }
        if (Time.time < _proximoParpadeo) return;
        if (_faseParpadeo == 1 || _faseParpadeo == 3)
        {
            ActivateMesh(_eyeMeshes, _ojosDeLaCara, "ojos", false);
            if (_faseParpadeo == 1 && _dobleParpadeo)
            {
                _faseParpadeo = 2;
                _proximoParpadeo = Time.time + 0.1f;
            }
            else { _faseParpadeo = 0; ProgramarParpadeo(); }
            return;
        }
        if (!SeVenLosOjos() || string.IsNullOrEmpty(OjosDelParpadeo) ||
            !_eyeMeshes.TryGetValue(OjosDelParpadeo, out var cerrados) || cerrados == null)
        {
            _faseParpadeo = 0;
            ProgramarParpadeo();
            return;
        }
        if (_faseParpadeo == 0) _dobleParpadeo = Random.value < probabilidadDobleParpadeo;
        _faseParpadeo = _faseParpadeo == 2 ? 3 : 1;
        ActivateMesh(_eyeMeshes, OjosDelParpadeo, "ojos", false);
        _proximoParpadeo = Time.time + Mathf.Max(0.01f, duracionParpadeo);
    }
    private void ActualizarCara()
    {
        float ahora = Time.unscaledTime;
        if (ahora >= _finDeReaccion) VolverAReposo();
        if (!_hablando) return;
        if (emotionProfile == null || !EsEsteHablante(SenalesDeHabla.HablanteActual)) { PararHabla(); return; }
        var audio = AudioService.Instance;
        bool hayVoz = audio != null && audio.HayVozSonando;
        if (hayVoz)
        {
            _haTenidoVoz = true;
            if (ahora < _proximoCambioDeBoca) return;
            float nivel = audio.NivelDeVoz();
            string boca = nivel < emotionProfile.umbralVozAbierta ? emotionProfile.bocaHablandoEntreabierta : emotionProfile.bocaHablandoAbierta;
            if (boca != _bocaDuranteHabla)
            {
                AplicarBocaDelHabla(boca);
                _proximoCambioDeBoca = ahora + Mathf.Max(0.01f, emotionProfile.tiempoMinimoPorBocaConVoz);
            }
            return;
        }
        if (_haTenidoVoz || ahora >= _finDelHabla) { PararHabla(); return; }
        if (ahora < _proximoCambioDeBoca) return;
        AplicarBocaDelHabla(_bocaDuranteHabla == emotionProfile.bocaHablandoAbierta
            ? emotionProfile.bocaHablandoEntreabierta : emotionProfile.bocaHablandoAbierta);
        _proximoCambioDeBoca = ahora + Mathf.Max(0.01f, emotionProfile.segundosPorBoca);
    }
    public EmotionProfile EmotionProfile => emotionProfile;
    public NPCEmotion CurrentEmotion => _currentEmotion;
    public bool IsInDialogue => _isInDialogue;
    public int EyeMeshCount => _eyeMeshes.Count;
    public int MouthMeshCount => _mouthMeshes.Count;
    public int SimboloMeshCount => _simbolos.Count;
    public void ForceReset() => VolverAReposo();
    public void SetEmotionProfile(EmotionProfile profile)
    {
        emotionProfile = profile;
        ResolverReposo();
        VolverAReposo();
        if (_hablando && !TieneBocasDelHabla()) PararHabla();
    }
}
