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
    private string _ojosDeReposo, _bocaDeReposo, _bocaDeLaCara, _bocaDuranteHabla;
    private NPCEmotion _currentEmotion = NPCEmotion.Neutral;
    private bool _isInDialogue, _hablando, _controladorDeHablaResuelto, _esControladorDeHabla, _haTenidoVoz, _avisadoSinPerfil, _avisadoSinBocasDelHabla;
    private bool _tieneOjos, _tieneBoca;
    private float _finDelHabla, _proximoCambioDeBoca;
    private float _finDeReaccion = float.PositiveInfinity;

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
    }
    void Start() => VolverAReposo();
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
            if (child.name.StartsWith(eyePrefix) && !_eyeMeshes.ContainsKey(child.name)) _eyeMeshes[child.name] = child.gameObject;
            if (child.name.StartsWith(mouthPrefix) && !_mouthMeshes.ContainsKey(child.name)) _mouthMeshes[child.name] = child.gameObject;
            CacheMeshesRecursive(child);
        }
    }
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
        _currentEmotion = emotion;
        var datos = emotionProfile.GetEmotionData(emotion);
        AplicarCara(string.IsNullOrEmpty(datos.eyeMeshName) ? _ojosDeReposo : datos.eyeMeshName,
            string.IsNullOrEmpty(datos.mouthMeshName) ? _bocaDeReposo : datos.mouthMeshName);
    }
    /// Sustituye los ojos sin cambiar boca, emoci?n ni pose.
    public void AplicarOjos(string malla)
    {
        if (!string.IsNullOrEmpty(malla)) ActivateMesh(_eyeMeshes, malla, "ojos", false);
    }
    public void Reaccionar(NPCEmotion emotion, float segundos = -1f)
    {
        if (emotion == NPCEmotion.None) return;
        SetEmotion(emotion);
        if (emotion != NPCEmotion.Neutral && emotionProfile != null)
            _finDeReaccion = Time.unscaledTime + (segundos > 0f ? segundos : emotionProfile.segundosDeReaccion);
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
        _currentEmotion = _caraDeFondo == NPCEmotion.None ? NPCEmotion.Neutral : _caraDeFondo;
        if (_caraDeFondo != NPCEmotion.None && _caraDeFondo != NPCEmotion.Neutral && emotionProfile != null)
        {
            var datos = emotionProfile.GetEmotionData(_caraDeFondo);
            AplicarCara(_fondoSuave || string.IsNullOrEmpty(datos.eyeMeshName) ? _ojosDeReposo : datos.eyeMeshName,
                string.IsNullOrEmpty(datos.mouthMeshName) ? _bocaDeReposo : datos.mouthMeshName);
        }
        else AplicarCara(_ojosDeReposo, _bocaDeReposo);
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
    }
    private void ActivateMesh(Dictionary<string, GameObject> mallas, string nombre, string tipo, bool diagnostico = true)
    {
        if ((mallas == _eyeMeshes && !_tieneOjos) || (mallas == _mouthMeshes && !_tieneBoca)) return;
        if (string.IsNullOrEmpty(nombre) || !mallas.TryGetValue(nombre, out var destino) || destino == null) return;
        if (mallas == _eyeMeshes) _renderersDeOjos.TryGetValue(nombre, out _ojosVisibles);
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
    private bool CaraConOjosCerrados()
    {
        if (_ojosDeLaCara == ojosDelParpadeo) return true;
        if (ojosYaCerrados == null) return false;
        for (int i = 0; i < ojosYaCerrados.Length; i++)
            if (_ojosDeLaCara == ojosYaCerrados[i]) return true;
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
        if (!SeVenLosOjos() || string.IsNullOrEmpty(ojosDelParpadeo) ||
            !_eyeMeshes.TryGetValue(ojosDelParpadeo, out var cerrados) || cerrados == null)
        {
            _faseParpadeo = 0;
            ProgramarParpadeo();
            return;
        }
        if (_faseParpadeo == 0) _dobleParpadeo = Random.value < probabilidadDobleParpadeo;
        _faseParpadeo = _faseParpadeo == 2 ? 3 : 1;
        ActivateMesh(_eyeMeshes, ojosDelParpadeo, "ojos", false);
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
    public void ForceReset() => VolverAReposo();
    public void SetEmotionProfile(EmotionProfile profile)
    {
        emotionProfile = profile;
        ResolverReposo();
        VolverAReposo();
        if (_hablando && !TieneBocasDelHabla()) PararHabla();
    }
}
