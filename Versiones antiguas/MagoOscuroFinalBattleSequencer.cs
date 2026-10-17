using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.Common;
using Sendero.Core.Feedback;
using Sendero.UI;
using Invector.vCharacterController;

/// <summary>
/// Orquestador cinemático de la Batalla Final según el canon de la novela (Capítulo XXVI:
/// "El Mago Oscuro y la verdad"):
///   1. Encuentro frente al altar de piedra blanca y cara a cara con el Mago Oscuro.
///   2. El brillo en el pecho y el Vacío Tibio (diálogo íntimo y revelación con el Archimago:
///      dos almas distintas, Will aprende a crear hechizos queriendo con todas sus fuerzas).
///   3. Despertar en Primera Persona (POV): Will abre los ojos y ve a Estela y Liam a los lados
///      sujetándole los brazos preocupados ("Eh, idiota... ¿estás bien?"), con mirada a la cinta roja.
///   4. El Momentazo: Will da un paso al frente ("Esta vez no he venido solo"), Estela se coloca
///      a su derecha, Liam a su izquierda, y el Mago Oscuro deja de sonreír.
///   5. Handoff a gameplay: arranque directo de la Fase 1 del combate con MagoOscuroBossAI.
/// </summary>
public class MagoOscuroFinalBattleSequencer : CinematicSequencerBase
{
    [Header("Actores")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _estelaActor;
    [SerializeField] private Transform _liamActor;
    [SerializeField] private Transform _magoOscuroActor;
    [SerializeField] private Damageable _magoOscuroHealth;

    [Header("Caras (NPCEmotionController)")]
    [SerializeField] private NPCEmotionController _estelaEmotion;
    [SerializeField] private NPCEmotionController _liamEmotion;
    [SerializeField] private NPCEmotionController _willEmotion;
    [SerializeField] private NPCEmotionController _magoEmotion;

    [Header("Jefe (Boss AI)")]
    [Tooltip("Controlador del combate del Mago Oscuro. Si no se asigna, se busca en _magoOscuroActor o en la escena.")]
    [SerializeField] private MagoOscuroBossAI _magoBossAI;
    [SerializeField] private BossArenaController _arena;

    [Header("Cámara — shot points")]
    [SerializeField] private Transform _shotAltarWide;
    [SerializeField] private Transform _shotMagoCloseup;
    [SerializeField] private Transform _shotWillEstelaLiam;
    [SerializeField] private Transform _shotRevelationWill;
    [SerializeField] private Transform _shotPOVWill;
    [SerializeField] private Transform _shotMomentazo;

    [Header("Efectos y Prefabs")]
    [Tooltip("Aura que despierta en el pecho de Will.")]
    [SerializeField] private GameObject _willAwakenedAuraVfx;
    [Tooltip("Efecto de oscuridad del Mago Oscuro junto al altar.")]
    [SerializeField] private GameObject _vfxOscuroMago;
    [Tooltip("Círculo luminoso en el suelo del Vacío Tibio.")]
    [SerializeField] private GameObject _vfxSueloVision;
    [Tooltip("Copia de Will para la visión (Assets/Prefabs/_WILL.prefab).")]
    [SerializeField] private GameObject _willVisionPrefab;
    [Tooltip("El Archimago / La Voz (Assets/Prefabs/_WILL_ORIGINAL.prefab).")]
    [SerializeField] private GameObject _goodWizardVisionPrefab;
    [SerializeField] private float _visionFadeDuration = 0.8f;
    [SerializeField] private string _visionMusicId = "MAGOOSCURO_VISION";
    [SerializeField] private float _visionCardTransitionOverhead = 0.7f;

    private GameObject _willAuraInstance;
    private GameObject _cintaRojaRuntime;
    private GameObject _brazoPovRuntime;
    private GameObject _runtimeShotHolder;
    private GameObject _runtimeVisionStage;
    private Coroutine _planoActivo;
    private Renderer[] _willRenderersCache;
    private bool _willOcultoEnPov;

    private bool _nieblaGuardada;
    private bool _nieblaActivaPrevia;
    private Color _luzAmbientePrevia;
    private CameraClearFlags _clearFlagsPrevios;
    private Color _colorFondoPrevio;
    private bool _camaraModificadaEnVision;

    private SequenceActor _seqWill;
    private SequenceActor _seqEstela;
    private SequenceActor _seqLiam;
    private SequenceActor _seqMago;

    // Colores canónicos de subtítulos
    private static readonly Color ColorWill = new Color(1f, 0.82f, 0.48f);       // #FFD27A
    private static readonly Color ColorEstela = new Color(1f, 0.62f, 0.72f);     // #FF9FB8
    private static readonly Color ColorLiam = new Color(0.54f, 0.84f, 1f);       // #8AD6FF
    private static readonly Color ColorMago = new Color(0.71f, 0.55f, 1f);       // #B48CFF
    private static readonly Color ColorArchimago = new Color(1f, 0.93f, 0.72f);  // #FFEDB8

    protected override IEnumerator Co_Sequence()
    {
        ResolverActores();
        DesactivarActoresParaCinematica();
        AsegurarShotPoints();

        BandasDeCineUI.Obtener(transform).Mostrar(true, 0.12f, 0.4f);

        // Pose inicial antes del fundido de entrada para arrancar ya con lente de cine (36°)
        PosicionarActoresInicial();
        CalcularPlanoApertura(out Vector3 posIni, out Quaternion rotIni, out float fovIni,
            out Vector3 posFin, out Quaternion rotFin, out float fovFin);
        if (_shotAltarWide != null)
        {
            _shotAltarWide.SetPositionAndRotation(posIni, rotIni);
            var camShot = _shotAltarWide.GetComponent<Camera>() ?? _shotAltarWide.gameObject.AddComponent<Camera>();
            camShot.enabled = false;
            camShot.fieldOfView = fovIni;
        }

        yield return Co_BeginCinematicWithTransition(_shotAltarWide);
        PlaySequenceMusic("MAGOOSCURO_REVEAL");

        // Acto 1: Encuentro en el altar y cara a cara con el Mago
        yield return Co_Acto1_EncuentroAltar(posIni, rotIni, fovIni, posFin, rotFin, fovFin);

        // Acto 2: El Vacío Tibio y diálogo con el Archimago
        yield return Co_Acto2_VacioTibioArchimago();

        // Acto 3: Despertar en Primera Persona (POV) con Estela, Liam y la cinta roja
        yield return Co_Acto3_DespertarPOV();

        // Acto 4: Handoff a 3ª persona y EL MOMENTAZO (paso al frente y formación)
        yield return Co_Acto4_ElMomentazo();

        // Acto 5: Handoff a Gameplay y arranque del combate
        yield return Co_Acto5_HandoffACombate();
    }

    // ── Acto 1: Encuentro en el Altar ─────────────────────────────────────────

    private IEnumerator Co_Acto1_EncuentroAltar(
        Vector3 posIni, Quaternion rotIni, float fovIni,
        Vector3 posFin, Quaternion rotFin, float fovFin)
    {
        Transform will = _willActor;
        Transform estela = _estelaActor;
        Transform liam = _liamActor;
        Transform mago = _magoOscuroActor;

        Vector3 dir = DireccionPlana(will, mago);
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        // 1.1. Travelling de llegada: el grupo avanza hacia el altar mientras la cámara desciende (36° -> 30°)
        RodarPlano(posIni, rotIni, fovIni, posFin, rotFin, fovFin, 2.5f, mano: 0.14f);

        if (_vfxOscuroMago != null && mago != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(_vfxOscuroMago, mago.position + Vector3.up * 0.9f, Quaternion.identity, 3.2f);
        AudioService.Instance?.PlaySFX("Prologo_PresenciaOscura", worldPosition: mago != null ? mago.position : Vector3.zero);

        Vector3 willLlegada = new Vector3(0f, will != null ? will.position.y : 0.1f, -6.5f);
        Vector3 estelaLlegada = willLlegada + right * 1.45f - dir * 0.35f;
        Vector3 liamLlegada = willLlegada - right * 1.45f - dir * 0.35f;

        var estelaAnim = estela != null ? estela.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        var liamAnim = liam != null ? liam.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        var estelaAgent = estela != null ? estela.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) : null;
        var liamAgent = liam != null ? liam.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) : null;

        Coroutine cWill = will != null ? StartCoroutine(Co_SimpleWalkTo(will, null, null, willLlegada, 2.0f)) : null;
        Coroutine cEstela = estela != null ? StartCoroutine(Co_SimpleWalkTo(estela, estelaAnim, estelaAgent, estelaLlegada, 2.1f)) : null;
        Coroutine cLiam = liam != null ? StartCoroutine(Co_SimpleWalkTo(liam, liamAnim, liamAgent, liamLlegada, 2.15f)) : null;

        if (cWill != null) yield return cWill;
        if (cEstela != null) yield return cEstela;
        if (cLiam != null) yield return cLiam;

        FaceTarget(will, mago);
        FaceTarget(estela, mago);
        FaceTarget(liam, mago);
        FaceTarget(mago, will);
        yield return new WaitForSeconds(0.35f);

        // 1.2. Primer plano / medio corto de Estela (lente 26°, 3/4 izquierda)
        _estelaEmotion?.SetEmotion(NPCEmotion.Annoyed);
        PlanoTresCuartos(estela, mago, lado: -1f, distancia: 2.35f, lenteIni: 26f, lenteFin: 24.5f,
            alturaOffset: -0.02f, encuadreX: 0.10f, duracion: 3.0f, mano: 0.12f);
        yield return ShowSubtitleLocalized(estela, "BF_INTRO_ESTELA_01", 2.4f,
            animTrigger: "Question01", speakerName: "Estela", speakerColor: ColorEstela);

        // 1.3. Contrapicado sobre el hombro de Will hacia el Mago Oscuro (lente 24° -> 22°)
        _magoEmotion?.SetEmotion(NPCEmotion.Smirk);
        PlanoSobreElHombro(will, mago, ladoHombro: -1f, lenteIni: 24f, lenteFin: 22f,
            contrapicado: true, duracion: 4.8f, mano: 0.12f);
        yield return ShowSubtitleLocalized(mago, "BF_INTRO_MAGO_01", 4.2f,
            animTrigger: "Talk01", speakerName: "Mago Oscuro", speakerColor: ColorMago);

        // 1.4. Primer plano de Will reconociéndolo de su pesadilla (lente 24° -> 21.5°, 3/4 derecha)
        _willEmotion?.SetEmotion(NPCEmotion.Worried);
        PlanoTresCuartos(will, mago, lado: 1f, distancia: 2.10f, lenteIni: 24f, lenteFin: 21.5f,
            alturaOffset: -0.04f, encuadreX: -0.09f, duracion: 3.2f, mano: 0.15f);
        yield return ShowSubtitleLocalized(will, "BF_INTRO_WILL_01", 2.6f,
            animTrigger: "Surprised", speakerName: "Will", speakerColor: ColorWill);

        // 1.5. Primer plano contrapicado del Mago Oscuro (lente 22° -> 20.5°, 3/4 izquierda)
        _magoEmotion?.SetEmotion(NPCEmotion.Smirk);
        PlanoTresCuartos(mago, will, lado: -1f, distancia: 2.20f, lenteIni: 22f, lenteFin: 20.5f,
            alturaOffset: -0.32f, encuadreX: 0.08f, duracion: 3.2f, mano: 0.12f);
        yield return ShowSubtitleLocalized(mago, "BF_INTRO_MAGO_02", 2.6f,
            animTrigger: "Talk02", speakerName: "Mago Oscuro", speakerColor: ColorMago);

        // 1.6. Primer plano de Liam furioso (lente 24°, 3/4 derecha)
        _liamEmotion?.SetEmotion(NPCEmotion.Angry);
        PlanoTresCuartos(liam, mago, lado: 1f, distancia: 2.15f, lenteIni: 24f, lenteFin: 22.5f,
            alturaOffset: -0.04f, encuadreX: -0.10f, duracion: 2.8f, mano: 0.18f);
        yield return ShowSubtitleLocalized(liam, "BF_INTRO_LIAM_01", 2.4f,
            animTrigger: "Angry02", speakerName: "Liam", speakerColor: ColorLiam);

        // 1.7. Sobre el hombro del Mago Oscuro hacia los tres héroes (lente 26° -> 24°)
        _magoEmotion?.SetEmotion(NPCEmotion.Smirk);
        PlanoSobreElHombro(mago, will, ladoHombro: 1f, lenteIni: 26f, lenteFin: 24f,
            contrapicado: false, duracion: 4.2f, mano: 0.12f);
        yield return ShowSubtitleLocalized(mago, "BF_INTRO_MAGO_03", 3.8f,
            animTrigger: "Talk01", speakerName: "Mago Oscuro", speakerColor: ColorMago);

        // 1.8. Primerísimo plano contrapicado del Mago Oscuro revelando la verdad del puente (lente 21° -> 18.5°)
        PlanoTresCuartos(mago, will, lado: 1f, distancia: 2.0f, lenteIni: 21f, lenteFin: 18.5f,
            alturaOffset: -0.35f, encuadreX: -0.07f, duracion: 4.8f, mano: 0.14f);
        yield return ShowSubtitleLocalized(mago, "BF_INTRO_MAGO_04", 4.4f,
            animTrigger: "Talk02", speakerName: "Mago Oscuro", speakerColor: ColorMago);

        // 1.9. Clímax del Acto 1: el pecho de Will arde con luz dorada, líneas de concentración y reacción del grupo
        _willEmotion?.SetEmotion(NPCEmotion.Pain);
        PlanoTresCuartos(will, mago, lado: -1f, distancia: 1.95f, lenteIni: 24f, lenteFin: 18f,
            alturaOffset: -0.08f, encuadreX: 0.0f, duracion: 1.9f, mano: 0.35f);

        var willAnim = will != null ? will.GetComponentInChildren<PlayerDialogueAnimator>(true) : null;
        willAnim?.PlayGesture("Pain01");

        if (_willAwakenedAuraVfx != null && will != null)
        {
            Vector3 auraPos = will.position + Vector3.up * 0.95f;
            _willAuraInstance = Instantiate(_willAwakenedAuraVfx, auraPos, Quaternion.identity, will);
        }

        AudioService.Instance?.PlaySFX("Prologo_ProteccionAbsoluta", worldPosition: will != null ? will.position : Vector3.zero);
        FeedbackService.ScreenFlash(new Color(1f, 0.94f, 0.68f, 0.55f), 0.28f);
        FeedbackService.CameraShake(0.55f, 1.1f);
        LineasDeConcentracionUI.Obtener(transform).Mostrar(
            true, new Color(1f, 0.94f, 0.72f, 0.55f), 0.42f, 0.12f, 1.5f);

        yield return new WaitForSeconds(0.55f);

        // Franjas de reacción de Estela y Liam al ver a Will desplomarse
        _estelaEmotion?.SetEmotion(NPCEmotion.Worried);
        _liamEmotion?.SetEmotion(NPCEmotion.Worried);
        var cutIn = CutInUI.Obtener(transform);
        if (_seqEstela != null)
            cutIn.Mostrar(_seqEstela, EncuadreCutIn.Cara, LadoCutIn.Izquierda, 0.68f, 1.1f, ranura: 0);
        if (_seqLiam != null)
            cutIn.Mostrar(_seqLiam, EncuadreCutIn.Cara, LadoCutIn.Derecha, 0.32f, 1.1f, ranura: 1);
        AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");

        yield return new WaitForSeconds(1.15f);
        cutIn.Retirar();
        LineasDeConcentracionUI.Instance?.Mostrar(false, Color.white, 0.42f, 0.15f, 0f);

        // Fundido a blanco hacia el Vacío Tibio
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: true);
    }

    // ── Acto 2: El Vacío Tibio (Archimago) ────────────────────────────────────

    private IEnumerator Co_Acto2_VacioTibioArchimago()
    {
        if (_willVisionPrefab == null || _goodWizardVisionPrefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] Falta _willVisionPrefab o _goodWizardVisionPrefab. Saltando visión.");
#endif
            yield break;
        }

        PlaySequenceMusic(_visionMusicId);

        // Guardar y apagar la niebla oscura de la escena para que el Vacío Tibio sea blanco-dorado limpio
        _nieblaGuardada = true;
        _nieblaActivaPrevia = RenderSettings.fog;
        _luzAmbientePrevia = RenderSettings.ambientLight;
        RenderSettings.fog = false;
        RenderSettings.ambientLight = new Color(0.92f, 0.88f, 0.78f);

        Vector3 stageAnchor = new Vector3(0f, 4000f, 0f);
        Vector3 eje = Vector3.right;
        Vector3 willPos = stageAnchor - eje * 1.15f;
        Vector3 wizardPos = stageAnchor + eje * 1.15f;

        _runtimeVisionStage = new GameObject("VisionStage_Runtime");
        _runtimeVisionStage.transform.position = stageAnchor;

        // Suelo etéreo luminoso bajo sus pies para dar referencia espacial cálida
        var sueloVision = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sueloVision.name = "SueloVacioTibio";
        sueloVision.transform.SetParent(_runtimeVisionStage.transform, false);
        sueloVision.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        sueloVision.transform.localScale = new Vector3(14f, 0.05f, 14f);
        var colSuelo = sueloVision.GetComponent<Collider>();
        if (colSuelo != null) Destroy(colSuelo);
        var renSuelo = sueloVision.GetComponent<Renderer>();
        if (renSuelo != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh);
            mat.color = new Color(0.99f, 0.96f, 0.89f, 1f);
            renSuelo.material = mat;
        }

        if (_vfxSueloVision != null)
        {
            var circulo = Instantiate(_vfxSueloVision, stageAnchor + Vector3.up * 0.02f, Quaternion.identity, _runtimeVisionStage.transform);
            circulo.transform.localScale = Vector3.one * 1.6f;
        }

        GameObject willVision = Instantiate(_willVisionPrefab, willPos, Quaternion.identity, _runtimeVisionStage.transform);
        GameObject wizardVision = Instantiate(_goodWizardVisionPrefab, wizardPos, Quaternion.identity, _runtimeVisionStage.transform);
        PrepareVisionActorInstance(willVision);
        PrepareVisionActorInstance(wizardVision);
        FaceTarget(willVision.transform, wizardVision.transform);
        FaceTarget(wizardVision.transform, willVision.transform);

        var willVisionEmotion = EmotionControllerResolver.Resolve(willVision);
        var wizardVisionEmotion = EmotionControllerResolver.Resolve(wizardVision);
        var willVisionAnimator = willVision.GetComponentInChildren<PlayerDialogueAnimator>(true);
        var wizardVisionAnimator = wizardVision.GetComponentInChildren<NPCSimpleAnimator>(true);

        // Iluminación cálida de tres puntos (clave dorada, relleno suave y contraluz)
        CrearLuzVision(_runtimeVisionStage.transform, stageAnchor + new Vector3(0f, 2.8f, -2.2f), new Color(1f, 0.96f, 0.86f), 3.2f, 14f);
        CrearLuzVision(_runtimeVisionStage.transform, stageAnchor + new Vector3(-2.4f, 1.8f, 1.5f), new Color(1f, 0.90f, 0.72f), 2.0f, 12f);
        CrearLuzVision(_runtimeVisionStage.transform, stageAnchor + new Vector3(2.4f, 1.8f, 1.5f), new Color(1f, 0.92f, 0.78f), 2.0f, 12f);

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            _camaraModificadaEnVision = true;
            _clearFlagsPrevios = mainCam.clearFlags;
            _colorFondoPrevio = mainCam.backgroundColor;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.99f, 0.96f, 0.90f);
        }

        Transform tWill = willVision.transform;
        Transform tArch = wizardVision.transform;

        wizardVisionAnimator?.BeginInteraction();

        // 2.1. Plano dos inicial (lente 30° -> 28°) mientras se disipa el blanco
        willVisionEmotion?.SetEmotion(NPCEmotion.Surprised);
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Neutral);
        PlanoDos(tWill, tArch, lenteIni: 30f, lenteFin: 28f, escorzoGrados: 14f, duracion: 2.2f, mano: 0.08f);
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: false);

        // Will: "¿Dónde estoy? ¿Estoy muerto?" (Primer plano de Will, lente 24°)
        willVisionEmotion?.SetEmotion(NPCEmotion.Worried);
        willVisionAnimator?.PlayGesture("Question02");
        PlanoTresCuartos(tWill, tArch, lado: -1f, distancia: 2.05f, lenteIni: 24f, lenteFin: 22.5f,
            alturaOffset: -0.03f, encuadreX: 0.09f, duracion: 3.4f, mano: 0.12f);
        yield return ShowSubtitleLocalized(tWill, "BF_VISION_WHERE_AM_I", 2.4f,
            speakerName: "Will", speakerColor: ColorWill);

        // Archimago: "No. Estás conmigo." (Sobre el hombro de Will hacia el Archimago, lente 24°)
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Happy);
        PlanoSobreElHombro(tWill, tArch, ladoHombro: -1f, lenteIni: 24f, lenteFin: 23f,
            contrapicado: false, duracion: 3.0f, mano: 0.10f);
        yield return ShowSubtitleLocalized(tArch, "BF_VISION_WITH_ME", 2.4f,
            animTrigger: "Talk03", speakerName: "Archimago", speakerColor: ColorArchimago);

        // Will: "Él sabe algo de mí que yo no sé." (Primer plano de Will, lente 24°)
        willVisionEmotion?.SetEmotion(NPCEmotion.Worried);
        willVisionAnimator?.PlayGesture("Question02");
        PlanoTresCuartos(tWill, tArch, lado: -1f, distancia: 2.0f, lenteIni: 24f, lenteFin: 22.5f,
            alturaOffset: -0.04f, encuadreX: 0.09f, duracion: 3.2f, mano: 0.12f);
        yield return ShowSubtitleLocalized(tWill, "BF_VISION_HE_KNOWS", 2.6f,
            speakerName: "Will", speakerColor: ColorWill);

        // Archimago: Historia del puente y del niño (Plano medio corto -> primer plano 26° -> 22°)
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Sad);
        PlanoTresCuartos(tArch, tWill, lado: 1f, distancia: 2.25f, lenteIni: 26f, lenteFin: 22f,
            alturaOffset: -0.03f, encuadreX: -0.09f, duracion: 9.5f, mano: 0.10f);
        yield return ShowSubtitleLocalized(tArch, "BF_VISION_BRIDGE_STORY", 3.2f,
            animTrigger: "Talk03", speakerName: "Archimago", speakerColor: ColorArchimago);

        // Will: "Eras tú. El de mis sueños, el que lloraba sin cara. ¿Entonces yo soy tú?" (Sobre el hombro del Archimago a Will, lente 24°)
        willVisionEmotion?.SetEmotion(NPCEmotion.Surprised);
        willVisionAnimator?.PlayGesture("Talk01");
        PlanoSobreElHombro(tArch, tWill, ladoHombro: 1f, lenteIni: 24f, lenteFin: 22f,
            contrapicado: false, duracion: 5.5f, mano: 0.12f);
        yield return ShowSubtitleLocalized(tWill, "BF_VISION_AM_I_YOU", 2.8f,
            speakerName: "Will", speakerColor: ColorWill);

        // Archimago: "No. Somos dos almas..." (Primer plano del Archimago, lente 24° -> 21.5°)
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Determined);
        PlanoTresCuartos(tArch, tWill, lado: 1f, distancia: 2.05f, lenteIni: 24f, lenteFin: 21.5f,
            alturaOffset: -0.03f, encuadreX: -0.08f, duracion: 8.5f, mano: 0.10f);
        yield return ShowSubtitleLocalized(tArch, "BF_VISION_TWO_SOULS", 3.2f,
            animTrigger: "HeadShake01", speakerName: "Archimago", speakerColor: ColorArchimago);

        // Will: "Tengo miedo. Tú eras mucho más fuerte que yo y perdiste." (Primer plano de Will, lente 24°)
        willVisionEmotion?.SetEmotion(NPCEmotion.Sad);
        willVisionAnimator?.PlayGesture("Talk01");
        PlanoTresCuartos(tWill, tArch, lado: -1f, distancia: 1.98f, lenteIni: 24f, lenteFin: 22.5f,
            alturaOffset: -0.02f, encuadreX: 0.08f, duracion: 4.5f, mano: 0.14f);
        yield return ShowSubtitleLocalized(tWill, "BF_VISION_FEAR", 2.6f,
            speakerName: "Will", speakerColor: ColorWill);

        // Archimago: "Lo encerré solo. Ese fue mi error..." (Plano dos cálido -> acerca a 26°)
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Sad);
        PlanoDos(tWill, tArch, lenteIni: 29f, lenteFin: 26f, escorzoGrados: -14f, duracion: 6.2f, mano: 0.10f);
        yield return ShowSubtitleLocalized(tArch, "BF_VISION_ALONE_MISTAKE", 3.0f,
            animTrigger: "Talk03", speakerName: "Archimago", speakerColor: ColorArchimago);

        // Will: "Yo no estoy solo." (Primer plano decidido de Will, lente 23° -> 20.5°)
        willVisionEmotion?.SetEmotion(NPCEmotion.Determined);
        willVisionAnimator?.PlayGesture("Talk01");
        PlanoTresCuartos(tWill, tArch, lado: -1f, distancia: 1.95f, lenteIni: 23f, lenteFin: 20.5f,
            alturaOffset: -0.04f, encuadreX: 0.07f, duracion: 3.2f, mano: 0.10f);
        yield return ShowSubtitleLocalized(tWill, "BF_VISION_NOT_ALONE", 2.4f,
            speakerName: "Will", speakerColor: ColorWill);

        // Archimago: "Por eso esta vez puede salir distinto... Los hechizos se crean... Vuelve con ellos."
        wizardVisionEmotion?.SetEmotion(NPCEmotion.Happy);
        PlanoTresCuartos(tArch, tWill, lado: 1f, distancia: 2.0f, lenteIni: 24f, lenteFin: 19.5f,
            alturaOffset: -0.04f, encuadreX: -0.07f, duracion: 10.5f, mano: 0.10f);
        LineasDeConcentracionUI.Obtener(transform).Mostrar(
            true, new Color(1f, 0.95f, 0.76f, 0.38f), 0.48f, 0.4f, 6.5f);
        yield return ShowSubtitleLocalized(tArch, "BF_VISION_CREATE_SPELLS", 3.2f,
            animTrigger: "Talk03", speakerName: "Archimago", speakerColor: ColorArchimago);

        wizardVisionAnimator?.EndInteraction();
        LineasDeConcentracionUI.Instance?.Mostrar(false, Color.white, 0.48f, 0.25f, 0f);

        PlayerHUDV2.Instance?.HideHUD(this);
        MinimapController.Ocultar(this);
        TimeOfDayIndicator.Instance?.Hide();

        // Fundido a blanco para regresar al altar
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: true);

        RestaurarEntornoVision();
        PlaySequenceMusic("MAGOOSCURO_REVEAL");
    }

    // ── Acto 3: Despertar en Primera Persona (POV) ───────────────────────────

    private IEnumerator Co_Acto3_DespertarPOV()
    {
        DetenerPlanoActivo();

        if (_willActor != null)
        {
            // Ocultar las mallas de la cabeza/cuerpo de Will en POV para que su pelo nunca atraviese la cámara
            OcultarWillEnPov(true);

            Vector3 forward = _willActor.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 povPos = _willActor.position + Vector3.up * 1.05f;
            _shotPOVWill.position = povPos;
            _shotPOVWill.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // Estela (derecha) y Liam (izquierda) en encuadre cercano (1.35 m) mirando directamente a los ojos de Will
            if (_estelaActor != null)
            {
                _estelaActor.position = _willActor.position + right * 0.46f + forward * 1.32f;
                Vector3 lookDir = (povPos - Vector3.up * 0.12f) - _estelaActor.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    _estelaActor.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                _estelaEmotion?.SetEmotion(NPCEmotion.Worried);
            }

            if (_liamActor != null)
            {
                _liamActor.position = _willActor.position - right * 0.46f + forward * 1.36f;
                Vector3 lookDir = (povPos - Vector3.up * 0.12f) - _liamActor.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    _liamActor.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                _liamEmotion?.SetEmotion(NPCEmotion.Worried);
            }

            AsegurarCintaRoja();
            CrearBrazoPovConCinta(povPos, forward, right);
        }

        // Lente de 32° en primera persona: encuadra a Estela y Liam en primer plano sin deformar sus caras
        Vector3 basePos = _shotPOVWill.position;
        Quaternion lookForward = _shotPOVWill.rotation;
        RodarPlano(basePos, lookForward, 32f, basePos, lookForward, 30f, 3.4f, mano: 0.18f);

        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: false);

        // Estela: "Eh, idiota. ¿Estás bien?"
        yield return ShowSubtitleLocalized(_estelaActor, "BF_POV_ESTELA_IDIOT", 2.6f,
            animTrigger: "Talk01", speakerName: "Estela", speakerColor: ColorEstela);

        // Will baja la mirada hacia su muñeca izquierda donde reluce la cinta roja de Estela
        Quaternion lookWrist = lookForward * Quaternion.Euler(27f, -15f, 0f);
        RodarPlano(basePos, lookForward, 30f, basePos, lookWrist, 25f, 0.65f, mano: 0.10f);
        yield return new WaitForSeconds(0.65f);

        var liamAnimInit = _liamActor?.GetComponentInChildren<NPCSimpleAnimator>(true);
        liamAnimInit?.PlaySocialGesture("HeadNod01");
        _liamEmotion?.SetEmotion(NPCEmotion.Neutral);
        _estelaEmotion?.SetEmotion(NPCEmotion.Smirk);

        // Will: "Sí... Estoy aquí."
        RodarPlano(basePos, lookWrist, 25f, basePos, lookWrist, 24f, 2.5f, mano: 0.08f);
        yield return ShowSubtitleLocalized(_willActor, "BF_POV_WILL_HERE", 2.2f,
            speakerName: "Will", speakerColor: ColorWill);
        yield return new WaitForSeconds(0.25f);

        // La mirada vuelve a subir hacia Estela, Liam y el altar al fondo
        RodarPlano(basePos, lookWrist, 24f, basePos, lookForward, 30f, 0.65f, mano: 0.10f);
        yield return new WaitForSeconds(0.65f);

        OcultarWillEnPov(false);
    }

    // ── Acto 4: Handoff a 3ª Persona y EL MOMENTAZO ───────────────────────────

    private IEnumerator Co_Acto4_ElMomentazo()
    {
        OcultarWillEnPov(false);

        Transform will = _willActor;
        Transform estela = _estelaActor;
        Transform liam = _liamActor;
        Transform mago = _magoOscuroActor;

        Vector3 dir = DireccionPlana(will, mago);
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        // Colocar a Estela y Liam a los lados de Will mirando hacia el Mago Oscuro (nunca de espaldas)
        if (will != null)
        {
            FaceTarget(will, mago);
            if (estela != null)
            {
                estela.position = will.position + right * 1.45f - dir * 0.35f;
                FaceTarget(estela, mago);
            }
            if (liam != null)
            {
                liam.position = will.position - right * 1.45f - dir * 0.35f;
                FaceTarget(liam, mago);
            }
        }

        // 4.1. Primer plano contrapicado del Mago Oscuro: "Ya lo sabes. Entonces te acordarás de mí."
        _magoEmotion?.SetEmotion(NPCEmotion.Smirk);
        PlanoTresCuartos(mago, will, lado: -1f, distancia: 2.15f, lenteIni: 24f, lenteFin: 22f,
            alturaOffset: -0.30f, encuadreX: 0.08f, duracion: 3.4f, mano: 0.12f);
        yield return ShowSubtitleLocalized(mago, "BF_MOMENT_MAGO_REMEMBER", 3.0f,
            animTrigger: "Talk01", speakerName: "Mago Oscuro", speakerColor: ColorMago);

        // 4.2. Primer plano decidido de Will: "Me acuerdo de todo."
        _willEmotion?.SetEmotion(NPCEmotion.Determined);
        PlanoTresCuartos(will, mago, lado: 1f, distancia: 2.0f, lenteIni: 24f, lenteFin: 21.5f,
            alturaOffset: -0.04f, encuadreX: -0.08f, duracion: 2.4f, mano: 0.12f);
        yield return ShowSubtitleLocalized(will, "BF_MOMENT_WILL_EVERYTHING", 2.0f,
            animTrigger: "Talk01", speakerName: "Will", speakerColor: ColorWill);

        // 4.3. Plano medio corto de Estela mirando a Will: "¿De todo? ¿De qué está hablando?"
        if (estela != null && will != null) FaceTarget(estela, will);
        _estelaEmotion?.SetEmotion(NPCEmotion.Confused);
        PlanoTresCuartos(estela, will, lado: -1f, distancia: 2.2f, lenteIni: 26f, lenteFin: 24.5f,
            alturaOffset: -0.03f, encuadreX: 0.09f, duracion: 2.6f, mano: 0.12f);
        yield return ShowSubtitleLocalized(estela, "BF_MOMENT_ESTELA_WHAT", 2.2f,
            animTrigger: "Question01", speakerName: "Estela", speakerColor: ColorEstela);

        // 4.4. Primer plano de Will respondiendo a sus amigos: "Es largo. Os lo cuento después. Todo, prometido."
        _willEmotion?.SetEmotion(NPCEmotion.Neutral);
        PlanoTresCuartos(will, estela, lado: 1f, distancia: 2.15f, lenteIni: 25f, lenteFin: 23.5f,
            alturaOffset: -0.03f, encuadreX: -0.08f, duracion: 3.0f, mano: 0.12f);
        yield return ShowSubtitleLocalized(will, "BF_MOMENT_WILL_EXPLAIN_LATER", 2.6f,
            animTrigger: "Talk02", speakerName: "Will", speakerColor: ColorWill);

        // 4.5. Plano de Estela y Liam reaccionando: "Más te vale. Ya tenemos a uno en el grupo que se calla las cosas."
        if (estela != null && mago != null) FaceTarget(estela, mago);
        if (liam != null && mago != null) FaceTarget(liam, mago);
        _estelaEmotion?.SetEmotion(NPCEmotion.Annoyed);
        _liamEmotion?.SetEmotion(NPCEmotion.Worried);
        var liamActorAnim = liam != null ? liam.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        liamActorAnim?.PlaySocialGesture("HeadShake02");
        PlanoTresCuartos(estela, mago, lado: -1f, distancia: 2.4f, lenteIni: 27f, lenteFin: 25.5f,
            alturaOffset: -0.03f, encuadreX: 0.08f, duracion: 3.2f, mano: 0.12f);
        yield return ShowSubtitleLocalized(estela, "BF_MOMENT_ESTELA_BETTER", 2.8f,
            animTrigger: "Talk01", speakerName: "Estela", speakerColor: ColorEstela);

        // 4.6. EL PASO AL FRENTE DE WILL: travelling contrapicado de tres cuartos (28° -> 23°)
        Vector3 willDest = will.position + dir * 1.5f;
        Vector3 focoIni = will.position + Vector3.up * MedirAlturaOjos(will);
        Vector3 focoFin = willDest + Vector3.up * MedirAlturaOjos(will);
        Vector3 camPasoIni = focoIni + (dir * 2.35f + right * 1.15f) + Vector3.down * 0.28f;
        Vector3 camPasoFin = focoFin + (dir * 2.05f + right * 0.95f) + Vector3.down * 0.32f;
        Quaternion rotPasoIni = RotacionConComposicion(camPasoIni, focoIni, 28f, new Vector2(-0.08f, -0.06f));
        Quaternion rotPasoFin = RotacionConComposicion(camPasoFin, focoFin, 23f, new Vector2(-0.06f, -0.06f));

        RodarPlano(camPasoIni, rotPasoIni, 28f, camPasoFin, rotPasoFin, 23f, 3.6f, mano: 0.15f);
        yield return Co_SimpleWalkTo(will, null, null, willDest, 0.95f);
        FaceTarget(will, mago);

        _willEmotion?.SetEmotion(NPCEmotion.Determined);
        yield return ShowSubtitleLocalized(will, "BF_MOMENT_WILL_NOT_ALONE", 2.6f,
            animTrigger: "Talk01", speakerName: "Will", speakerColor: ColorWill);

        // 4.7. LA FORMACIÓN DEL TRÍO: plano conjunto heroico contrapicado (30° -> 26°) + franjas Cut-In
        Vector3 estelaDest = willDest + right * 1.50f;
        Vector3 liamDest = willDest - right * 1.50f;

        Vector3 focoGrupo = willDest + Vector3.up * (MedirAlturaOjos(will) - 0.05f);
        Vector3 camGrupoIni = focoGrupo + (dir * 3.55f - right * 1.15f) + Vector3.down * 0.35f;
        Vector3 camGrupoFin = focoGrupo + (dir * 3.15f - right * 0.85f) + Vector3.down * 0.38f;
        Quaternion rotGrupoIni = RotacionConComposicion(camGrupoIni, focoGrupo, 30f, new Vector2(0f, -0.06f));
        Quaternion rotGrupoFin = RotacionConComposicion(camGrupoFin, focoGrupo, 26f, new Vector2(0f, -0.06f));

        RodarPlano(camGrupoIni, rotGrupoIni, 30f, camGrupoFin, rotGrupoFin, 26f, 2.2f, mano: 0.16f);

        var estelaAnim = estela != null ? estela.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        var liamAnim = liam != null ? liam.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        var estelaAgent = estela != null ? estela.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) : null;
        var liamAgent = liam != null ? liam.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) : null;

        var moveEstela = StartCoroutine(Co_SimpleWalkTo(estela, estelaAnim, estelaAgent, estelaDest, 0.85f));
        var moveLiam = StartCoroutine(Co_SimpleWalkTo(liam, liamAnim, liamAgent, liamDest, 0.85f));

        yield return moveEstela;
        yield return moveLiam;

        FaceTarget(estela, mago);
        FaceTarget(liam, mago);
        _estelaEmotion?.SetEmotion(NPCEmotion.Determined);
        _liamEmotion?.SetEmotion(NPCEmotion.Determined);
        estelaAnim?.PlaySocialGesture("Cheer01");
        liamAnim?.PlaySocialGesture("Challenging_NoWeapon");

        LineasDeConcentracionUI.Obtener(transform).Mostrar(
            true, new Color(1f, 0.95f, 0.80f, 0.45f), 0.45f, 0.12f, 1.5f);
        var cutIn = CutInUI.Obtener(transform);
        if (_seqEstela != null)
            cutIn.Mostrar(_seqEstela, EncuadreCutIn.Cara, LadoCutIn.Izquierda, 0.68f, 1.35f, ranura: 0);
        if (_seqLiam != null)
            cutIn.Mostrar(_seqLiam, EncuadreCutIn.Cara, LadoCutIn.Derecha, 0.32f, 1.35f, ranura: 1);
        AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");

        yield return new WaitForSeconds(1.4f);
        cutIn.Retirar();

        // 4.8. EL MAGO OSCURO DEJA DE SONREÍR: primerísimo plano contrapicado (22° -> 18°) + erupción oscura
        _magoEmotion?.SetEmotion(NPCEmotion.Angry);
        var magoAnim = mago != null ? mago.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        magoAnim?.PlaySocialGesture("Angry01");

        PlanoTresCuartos(mago, will, lado: -1f, distancia: 1.95f, lenteIni: 22f, lenteFin: 18f,
            alturaOffset: -0.35f, encuadreX: 0.0f, duracion: 1.6f, mano: 0.28f);

        if (_seqMago != null)
        {
            cutIn.Mostrar(_seqMago, EncuadreCutIn.Ojos, LadoCutIn.Derecha, 0.50f, 1.25f, ranura: 0);
            AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");
        }
        if (_vfxOscuroMago != null && mago != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(_vfxOscuroMago, mago.position + Vector3.up * 0.9f, Quaternion.identity, 2.5f);
        AudioService.Instance?.PlaySFX("Prologo_Golpe", worldPosition: mago != null ? mago.position : Vector3.zero);
        FeedbackService.CameraShake(0.55f, 0.7f);

        yield return new WaitForSeconds(1.35f);
        cutIn.Retirar();
        LineasDeConcentracionUI.Instance?.Mostrar(false, Color.white, 0.45f, 0.15f, 0f);
    }

    // ── Acto 5: Handoff a Gameplay y Arranque del Combate ─────────────────────

    private IEnumerator Co_Acto5_HandoffACombate()
    {
        DetenerPlanoActivo();
        OcultarWillEnPov(false);
        RestaurarActoresTrasCinematica();

        BandasDeCineUI.Instance?.Mostrar(false, 0f, 0.45f);
        LineasDeConcentracionUI.Instance?.OcultarInmediato();
        CutInUI.Instance?.Retirar();

        yield return Co_EndCinematicStayBlack(fadeDuration: 0.25f);
        yield return FeedbackService.ScreenFadeAsync(Color.black, 0.3f, fadeIn: false);

        if (_magoBossAI != null)
        {
            _magoBossAI.EmpezarCombate();
        }
        else if (_arena != null)
        {
            _arena.TriggerStartBattle();
        }

        RaiseSignalOut();
    }

    // ── Dirección de Cámara (Gramática de Prólogo / HornoCamaras) ─────────────

    /// Calcula el plano de apertura (crane/travelling descendiendo tras el grupo hacia el altar).
    private void CalcularPlanoApertura(
        out Vector3 posIni, out Quaternion rotIni, out float fovIni,
        out Vector3 posFin, out Quaternion rotFin, out float fovFin)
    {
        Transform will = _willActor != null ? _willActor : transform;
        Transform mago = _magoOscuroActor != null ? _magoOscuroActor : transform;
        Vector3 dir = DireccionPlana(will, mago);
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        Vector3 willLlegada = new Vector3(0f, will.position.y, -6.5f);
        Vector3 focoIni = Vector3.Lerp(will.position, mago.position, 0.35f) + Vector3.up * 1.15f;
        Vector3 focoFin = Vector3.Lerp(willLlegada, mago.position, 0.42f) + Vector3.up * 1.10f;

        posIni = will.position - dir * 3.8f - right * 1.6f + Vector3.up * 2.35f;
        posFin = willLlegada - dir * 3.4f - right * 1.15f + Vector3.up * 1.45f;
        fovIni = 36f;
        fovFin = 30f;

        rotIni = RotacionConComposicion(posIni, focoIni, fovIni, new Vector2(0f, -0.04f));
        rotFin = RotacionConComposicion(posFin, focoFin, fovFin, new Vector2(0f, -0.05f));
    }

    /// Plano de tres cuartos (primer plano o plano medio) con tercio superior, aire en la mirada y zoom suave.
    private void PlanoTresCuartos(
        Transform sujeto, Transform referencia, float lado, float distancia,
        float lenteIni, float lenteFin, float alturaOffset, float encuadreX,
        float duracion, float mano)
    {
        if (sujeto == null) return;
        float ojos = MedirAlturaOjos(sujeto);
        Vector3 foco = sujeto.position + Vector3.up * ojos;

        Vector3 frente = referencia != null ? DireccionPlana(sujeto, referencia) : sujeto.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = sujeto.forward;
        frente.Normalize();

        float angulo = lado * 30f;
        Vector3 dirCam = Quaternion.AngleAxis(angulo, Vector3.up) * frente;
        Vector3 posIni = foco + dirCam * distancia + Vector3.up * alturaOffset;
        Vector3 posFin = foco + dirCam * (distancia * 0.93f) + Vector3.up * alturaOffset;

        Vector2 comp = new Vector2(encuadreX, -0.07f);
        Quaternion rotIni = RotacionConComposicion(posIni, foco, lenteIni, comp);
        Quaternion rotFin = RotacionConComposicion(posFin, foco, lenteFin, comp);

        RodarPlano(posIni, rotIni, lenteIni, posFin, rotFin, lenteFin, duracion, mano);
    }

    /// Contraplano sobre el hombro de 'hombro' mirando a 'sujeto' con lente teleobjetivo (24°).
    private void PlanoSobreElHombro(
        Transform hombro, Transform sujeto, float ladoHombro,
        float lenteIni, float lenteFin, bool contrapicado,
        float duracion, float mano)
    {
        if (sujeto == null) return;
        if (hombro == null)
        {
            PlanoTresCuartos(sujeto, null, ladoHombro, 2.2f, lenteIni, lenteFin, contrapicado ? -0.3f : -0.04f, 0f, duracion, mano);
            return;
        }

        Vector3 haciaSujeto = DireccionPlana(hombro, sujeto);
        Vector3 derecha = Vector3.Cross(Vector3.up, haciaSujeto).normalized;
        float distEntreActores = Vector3.Distance(hombro.position, sujeto.position);

        float ojosHombro = MedirAlturaOjos(hombro);
        float ojosSujeto = MedirAlturaOjos(sujeto);

        Vector3 foco = sujeto.position + Vector3.up * ojosSujeto;
        Vector3 posIni;
        if (distEntreActores < 4.5f)
        {
            // Conversación cercana (p. ej. Vacío Tibio): geometría exacta de HornoCamaras.Hombro
            posIni = hombro.position - haciaSujeto * 1.15f + derecha * (ladoHombro * 0.42f)
                     + Vector3.up * (ojosHombro + 0.04f);
        }
        else
        {
            // Duelo a media distancia (Altar): hombro de referencia en primer término a 1.35 m
            posIni = hombro.position - haciaSujeto * 1.35f + derecha * (ladoHombro * 0.46f)
                     + Vector3.up * (ojosHombro + (contrapicado ? -0.10f : 0.06f));
        }

        Vector3 posFin = posIni + haciaSujeto * 0.18f;
        Vector2 comp = new Vector2(-ladoHombro * 0.10f, -0.06f);
        Quaternion rotIni = RotacionConComposicion(posIni, foco, lenteIni, comp);
        Quaternion rotFin = RotacionConComposicion(posFin, foco, lenteFin, comp);

        RodarPlano(posIni, rotIni, lenteIni, posFin, rotFin, lenteFin, duracion, mano);
    }

    /// Plano de dos personajes con escorzo para dar profundidad.
    private void PlanoDos(
        Transform a, Transform b, float lenteIni, float lenteFin,
        float escorzoGrados, float duracion, float mano)
    {
        if (a == null || b == null) return;
        float ojos = (MedirAlturaOjos(a) + MedirAlturaOjos(b)) * 0.5f;
        Vector3 centro = (a.position + b.position) * 0.5f + Vector3.up * (ojos * 0.94f);
        Vector3 ab = b.position - a.position;
        ab.y = 0f;
        float sep = Mathf.Max(1.6f, ab.magnitude);
        Vector3 normal = Vector3.Cross(Vector3.up, ab.normalized).normalized;
        if (normal.z > 0f) normal = -normal; // Mirar desde el frente del escenario (-Z hacia +Z)

        Vector3 dirCam = Quaternion.AngleAxis(escorzoGrados, Vector3.up) * normal;
        float tanV = Mathf.Tan(lenteIni * 0.5f * Mathf.Deg2Rad) * 0.76f;
        float tanH = Mathf.Tan(lenteIni * 0.5f * Mathf.Deg2Rad) * (16f / 9f);
        float dist = Mathf.Max((sep + 1.15f) / (2f * tanH), 1.45f / (2f * tanV)) + 0.25f;

        Vector3 posIni = centro + dirCam * dist + Vector3.down * 0.05f;
        Vector3 posFin = centro + dirCam * (dist * 0.94f) + Vector3.down * 0.05f;
        Vector2 comp = new Vector2(0f, -0.05f);
        Quaternion rotIni = RotacionConComposicion(posIni, centro, lenteIni, comp);
        Quaternion rotFin = RotacionConComposicion(posFin, centro, lenteFin, comp);

        RodarPlano(posIni, rotIni, lenteIni, posFin, rotFin, lenteFin, duracion, mano);
    }

    private static Quaternion RotacionConComposicion(Vector3 camPos, Vector3 foco, float fov, Vector2 encuadre)
    {
        Vector3 d = foco - camPos;
        if (d.sqrMagnitude < 1e-4f) return Quaternion.identity;
        float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (16f / 9f);
        return Quaternion.LookRotation(d.normalized, Vector3.up)
               * Quaternion.Euler(
                   -Mathf.Atan(2f * encuadre.y * tanV) * Mathf.Rad2Deg,
                   -Mathf.Atan(2f * encuadre.x * tanH) * Mathf.Rad2Deg,
                   0f);
    }

    private void RodarPlano(
        Vector3 posIni, Quaternion rotIni, float fovIni,
        Vector3 posFin, Quaternion rotFin, float fovFin,
        float duracion, float mano)
    {
        DetenerPlanoActivo();
        _planoActivo = StartCoroutine(Co_RodarPlano(posIni, rotIni, fovIni, posFin, rotFin, fovFin, duracion, mano));
    }

    private void DetenerPlanoActivo()
    {
        if (_planoActivo != null)
        {
            StopCoroutine(_planoActivo);
            _planoActivo = null;
        }
    }

    private IEnumerator Co_RodarPlano(
        Vector3 posIni, Quaternion rotIni, float fovIni,
        Vector3 posFin, Quaternion rotFin, float fovFin,
        float duracion, float mano)
    {
        if (_cinematicCamera == null) yield break;
        float t = 0f;
        float dur = Mathf.Max(0.05f, duracion);
        float semilla = Random.Range(0f, 100f);

        _cinematicCamera.Cut(posIni, rotIni, fovIni);

        while (true)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Vector3 pos = Vector3.Lerp(posIni, posFin, k);
            Quaternion rot = Quaternion.Slerp(rotIni, rotFin, k);
            float fov = Mathf.Lerp(fovIni, fovFin, k);

            if (mano > 0.001f)
            {
                float tiempoRuido = Time.unscaledTime * 0.85f + semilla;
                float rx = (Mathf.PerlinNoise(tiempoRuido, 1.3f) - 0.5f) * 2f * mano * 0.65f;
                float ry = (Mathf.PerlinNoise(2.7f, tiempoRuido) - 0.5f) * 2f * mano * 0.65f;
                rot *= Quaternion.Euler(rx, ry, 0f);
            }

            _cinematicCamera.SetPose(pos, rot, fov);
            yield return null;
        }
    }

    private static float MedirAlturaOjos(Transform actor)
    {
        if (actor == null) return 1.0f;
        var seq = SequenceActor.ForTransform(actor.name, actor);
        float h = seq != null ? seq.AlturaDeOjos : 1.0f;
        return Mathf.Clamp(h, 0.78f, 1.45f);
    }

    private static Vector3 DireccionPlana(Transform desde, Transform hacia)
    {
        if (desde == null || hacia == null) return Vector3.forward;
        Vector3 d = hacia.position - desde.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward;
    }

    // ── Utilidades de Configuración y Escenario ───────────────────────────────

    private void PosicionarActoresInicial()
    {
        if (_willActor == null || _magoOscuroActor == null) return;

        Vector3 dir = DireccionPlana(_willActor, _magoOscuroActor);
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        // Colocar al grupo al comienzo del paseo de llegada (-10.2 m) para entrar andando en el Plano 1
        _willActor.position = new Vector3(0f, _willActor.position.y, -10.2f);
        FaceTarget(_magoOscuroActor, _willActor);
        FaceTarget(_willActor, _magoOscuroActor);

        if (_estelaActor != null)
        {
            _estelaActor.position = _willActor.position + right * 1.45f - dir * 0.4f;
            FaceTarget(_estelaActor, _magoOscuroActor);
        }

        if (_liamActor != null)
        {
            _liamActor.position = _willActor.position - right * 1.45f - dir * 0.4f;
            FaceTarget(_liamActor, _magoOscuroActor);
        }
    }

    private void ResolverActores()
    {
        if (_willActor == null)
        {
            var p = PlayerService.Player;
            if (p != null) _willActor = p.transform;
            else _willActor = GameObject.Find("BF_JUGADOR_WILL")?.transform ?? GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (_magoOscuroActor == null)
        {
            var m = GameObject.Find("MAGO_OSCURO (jefe)") ?? GameObject.Find("_MAGO_OSCURO");
            if (m != null) _magoOscuroActor = m.transform;
        }

        if (_estelaActor == null)
        {
            var e = GameObject.Find("LAB_Estela") ?? GameObject.Find("Estela") ?? GameObject.Find("NPC_Estela");
            if (e != null) _estelaActor = e.transform;
        }

        if (_liamActor == null)
        {
            var l = GameObject.Find("LAB_Liam") ?? GameObject.Find("Liam") ?? GameObject.Find("NPC_Liam");
            if (l != null) _liamActor = l.transform;
        }

        if (_magoBossAI == null && _magoOscuroActor != null)
            _magoBossAI = _magoOscuroActor.GetComponentInChildren<MagoOscuroBossAI>(true);
        if (_magoBossAI == null)
            _magoBossAI = FindAnyObjectByType<MagoOscuroBossAI>();

        // Usar EmotionControllerResolver.Resolve (Will lleva dos NPCEmotionController y solo uno tiene las mallas asignadas)
        if (_estelaEmotion == null && _estelaActor != null)
            _estelaEmotion = EmotionControllerResolver.Resolve(_estelaActor.gameObject);
        if (_liamEmotion == null && _liamActor != null)
            _liamEmotion = EmotionControllerResolver.Resolve(_liamActor.gameObject);
        if (_willEmotion == null && _willActor != null)
            _willEmotion = EmotionControllerResolver.Resolve(_willActor.gameObject);
        if (_magoEmotion == null && _magoOscuroActor != null)
            _magoEmotion = EmotionControllerResolver.Resolve(_magoOscuroActor.gameObject);

        if (_willActor != null)
            _seqWill = SequenceActor.ForTransform("Player", _willActor, MedirAlturaOjos(_willActor),
                _willActor.GetComponentInChildren<Animator>(true), _willEmotion);
        if (_estelaActor != null)
            _seqEstela = SequenceActor.ForTransform("NPC_Estela", _estelaActor, MedirAlturaOjos(_estelaActor),
                _estelaActor.GetComponentInChildren<Animator>(true), _estelaEmotion);
        if (_liamActor != null)
            _seqLiam = SequenceActor.ForTransform("NPC_Liam", _liamActor, MedirAlturaOjos(_liamActor),
                _liamActor.GetComponentInChildren<Animator>(true), _liamEmotion);
        if (_magoOscuroActor != null)
            _seqMago = SequenceActor.ForTransform("NPC_MagoOscuro", _magoOscuroActor, MedirAlturaOjos(_magoOscuroActor),
                _magoOscuroActor.GetComponentInChildren<Animator>(true), _magoEmotion);
    }

    private void DesactivarActoresParaCinematica()
    {
        DisableActorAutoRotation(_magoOscuroActor);
        DisableActorAutoRotation(_estelaActor);
        DisableActorAutoRotation(_liamActor);
        DisableActorAutoRotation(_willActor);

        DisableActorBehaviour(_magoOscuroActor);
        DisableActorBehaviour(_estelaActor);
        DisableActorBehaviour(_liamActor);

        if (_willActor != null)
        {
            var input = _willActor.GetComponentInChildren<vThirdPersonInput>(true);
            if (input != null) input.enabled = false;
        }
    }

    private void RestaurarActoresTrasCinematica()
    {
        EnableActorAutoRotation(_magoOscuroActor);
        EnableActorAutoRotation(_estelaActor);
        EnableActorAutoRotation(_liamActor);
        EnableActorAutoRotation(_willActor);

        EnableActorBehaviour(_estelaActor);
        EnableActorBehaviour(_liamActor);

        if (_willActor != null)
        {
            var input = _willActor.GetComponentInChildren<vThirdPersonInput>(true);
            if (input != null) input.enabled = true;
        }
    }

    private void AsegurarShotPoints()
    {
        if (_runtimeShotHolder == null)
            _runtimeShotHolder = new GameObject("Shots_BatallaFinal_Runtime");

        Transform will = _willActor != null ? _willActor : transform;
        Transform mago = _magoOscuroActor != null ? _magoOscuroActor : transform;
        Vector3 dir = DireccionPlana(will, mago);

        if (_shotAltarWide == null)
            _shotAltarWide = CrearShotAuto("Shot_AltarWide_Auto", will.position - dir * 3.8f + Vector3.up * 2.2f, Quaternion.LookRotation(dir, Vector3.up), 34f);

        if (_shotMagoCloseup == null)
            _shotMagoCloseup = CrearShotAuto("Shot_MagoCloseup_Auto", mago.position - dir * 2.15f + Vector3.up * 1.15f, Quaternion.LookRotation(dir, Vector3.up), 24f);

        if (_shotWillEstelaLiam == null)
            _shotWillEstelaLiam = CrearShotAuto("Shot_WillEstelaLiam_Auto", will.position + dir * 2.4f + Vector3.up * 1.05f, Quaternion.LookRotation(-dir, Vector3.up), 26f);

        if (_shotRevelationWill == null)
            _shotRevelationWill = CrearShotAuto("Shot_RevelationWill_Auto", will.position + dir * 1.95f + Vector3.up * 1.0f, Quaternion.LookRotation(-dir, Vector3.up), 22f);

        if (_shotPOVWill == null)
            _shotPOVWill = CrearShotAuto("Shot_POVWill_Auto", will.position + Vector3.up * 1.05f, Quaternion.LookRotation(dir, Vector3.up), 32f);

        if (_shotMomentazo == null)
            _shotMomentazo = CrearShotAuto("Shot_Momentazo_Auto", will.position + dir * 3.1f + Vector3.up * 0.85f, Quaternion.LookRotation(-dir, Vector3.up), 28f);
    }

    private Transform CrearShotAuto(string nombre, Vector3 pos, Quaternion rot, float fov)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(_runtimeShotHolder.transform);
        go.transform.SetPositionAndRotation(pos, rot);
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = fov;
        return go.transform;
    }

    private void OcultarWillEnPov(bool ocultar)
    {
        if (_willActor == null) return;
        if (ocultar == _willOcultoEnPov) return;
        _willOcultoEnPov = ocultar;

        _willRenderersCache ??= _willActor.GetComponentsInChildren<Renderer>(true);
        foreach (var r in _willRenderersCache)
        {
            if (r == null) continue;
            if (_cintaRojaRuntime != null && r.transform.IsChildOf(_cintaRojaRuntime.transform)) continue;
            r.forceRenderingOff = ocultar;
        }

        if (!ocultar && _brazoPovRuntime != null)
        {
            Destroy(_brazoPovRuntime);
            _brazoPovRuntime = null;
        }
    }

    private void AsegurarCintaRoja()
    {
        if (_cintaRojaRuntime != null || _willActor == null) return;

        var anim = _willActor.GetComponentInChildren<Animator>();
        Transform muneca = anim != null ? (anim.GetBoneTransform(HumanBodyBones.LeftLowerArm) ?? anim.GetBoneTransform(HumanBodyBones.LeftHand)) : null;
        if (muneca == null) muneca = _willActor;

        _cintaRojaRuntime = CrearVisualCintaRoja("CintaRoja_Runtime", muneca, new Vector3(0f, 0.04f, 0f), Quaternion.Euler(0f, 0f, 90f), 1f);
    }

    /// Crea un antebrazo y muñeca en primer término para el plano subjetivo (POV) de modo que al bajar
    /// la vista en el Acto 3 se vea claramente la cinta roja atada a la muñeca izquierda de Will.
    private void CrearBrazoPovConCinta(Vector3 povPos, Vector3 forward, Vector3 right)
    {
        if (_brazoPovRuntime != null) Destroy(_brazoPovRuntime);

        _brazoPovRuntime = new GameObject("BrazoPOV_CintaRoja_Runtime");
        Vector3 posMuneca = povPos + forward * 0.46f - right * 0.12f - Vector3.up * 0.23f;
        Quaternion rotMuneca = Quaternion.LookRotation(forward + right * 0.35f, Vector3.up) * Quaternion.Euler(18f, 0f, -52f);
        _brazoPovRuntime.transform.SetPositionAndRotation(posMuneca, rotMuneca);

        var shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Manga azul oscura de la túnica de Will
        var manga = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        manga.name = "MangaWill_POV";
        manga.transform.SetParent(_brazoPovRuntime.transform, false);
        manga.transform.localPosition = new Vector3(0f, -0.10f, 0f);
        manga.transform.localScale = new Vector3(0.068f, 0.11f, 0.068f);
        if (manga.TryGetComponent<Collider>(out var c1)) Destroy(c1);
        if (manga.TryGetComponent<Renderer>(out var r1))
        {
            var matManga = new Material(shaderLit) { color = new Color(0.16f, 0.24f, 0.42f) };
            r1.material = matManga;
        }

        // Muñeca y mano de Will
        var mano = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        mano.name = "ManoWill_POV";
        mano.transform.SetParent(_brazoPovRuntime.transform, false);
        mano.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        mano.transform.localScale = new Vector3(0.056f, 0.075f, 0.056f);
        if (mano.TryGetComponent<Collider>(out var c2)) Destroy(c2);
        if (mano.TryGetComponent<Renderer>(out var r2))
        {
            var matPiel = new Material(shaderLit) { color = new Color(0.96f, 0.80f, 0.68f) };
            r2.material = matPiel;
        }

        CrearVisualCintaRoja("CintaRoja_POV", _brazoPovRuntime.transform, new Vector3(0f, 0.005f, 0f), Quaternion.identity, 1.15f);
    }

    private static GameObject CrearVisualCintaRoja(string nombre, Transform padre, Vector3 localPos, Quaternion localRot, float escala)
    {
        var raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre, false);
        raiz.transform.localPosition = localPos;
        raiz.transform.localRotation = localRot;
        raiz.transform.localScale = Vector3.one * escala;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var matRojo = new Material(shader) { color = new Color(0.90f, 0.10f, 0.16f) };
        if (matRojo.HasProperty("_EmissionColor"))
        {
            matRojo.EnableKeyword("_EMISSION");
            matRojo.SetColor("_EmissionColor", new Color(0.32f, 0.02f, 0.04f));
        }

        // Aro de tela carmesí alrededor de la muñeca
        var aro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aro.name = "AroCinta";
        aro.transform.SetParent(raiz.transform, false);
        aro.transform.localScale = new Vector3(0.068f, 0.014f, 0.068f);
        if (aro.TryGetComponent<Collider>(out var colAro)) Destroy(colAro);
        if (aro.TryGetComponent<Renderer>(out var renAro)) renAro.material = matRojo;

        // Nudo y dos cabos sueltos de la cinta roja de Estela
        var cabo1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabo1.name = "CaboCinta_1";
        cabo1.transform.SetParent(raiz.transform, false);
        cabo1.transform.localPosition = new Vector3(0.038f, -0.024f, 0.018f);
        cabo1.transform.localRotation = Quaternion.Euler(18f, 12f, -28f);
        cabo1.transform.localScale = new Vector3(0.016f, 0.065f, 0.005f);
        if (cabo1.TryGetComponent<Collider>(out var colC1)) Destroy(colC1);
        if (cabo1.TryGetComponent<Renderer>(out var renC1)) renC1.material = matRojo;

        var cabo2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabo2.name = "CaboCinta_2";
        cabo2.transform.SetParent(raiz.transform, false);
        cabo2.transform.localPosition = new Vector3(0.044f, -0.016f, -0.012f);
        cabo2.transform.localRotation = Quaternion.Euler(-14f, -10f, -42f);
        cabo2.transform.localScale = new Vector3(0.014f, 0.055f, 0.005f);
        if (cabo2.TryGetComponent<Collider>(out var colC2)) Destroy(colC2);
        if (cabo2.TryGetComponent<Renderer>(out var renC2)) renC2.material = matRojo;

        return raiz;
    }

    private static void CrearLuzVision(Transform padre, Vector3 pos, Color color, float intensidad, float rango)
    {
        var go = new GameObject("VisionLight_Runtime");
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = rango;
        light.intensity = intensidad;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    private void RestaurarEntornoVision()
    {
        if (_nieblaGuardada)
        {
            RenderSettings.fog = _nieblaActivaPrevia;
            RenderSettings.ambientLight = _luzAmbientePrevia;
            _nieblaGuardada = false;
        }

        if (_camaraModificadaEnVision)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = _clearFlagsPrevios;
                mainCam.backgroundColor = _colorFondoPrevio;
            }
            _camaraModificadaEnVision = false;
        }

        if (_runtimeVisionStage != null)
        {
            Destroy(_runtimeVisionStage);
            _runtimeVisionStage = null;
        }
    }

    private static void PrepareVisionActorInstance(GameObject actor)
    {
        if (actor == null) return;

        var agent = actor.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        if (agent != null) agent.enabled = false;

        var behaviourManager = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (behaviourManager != null) behaviourManager.enabled = false;

        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.DisableAutoRotation();

        var thirdPersonInput = actor.GetComponentInChildren<vThirdPersonInput>(true);
        if (thirdPersonInput != null) thirdPersonInput.enabled = false;
        var thirdPersonController = actor.GetComponentInChildren<vThirdPersonController>(true);
        if (thirdPersonController != null) thirdPersonController.enabled = false;

        var charController = actor.GetComponentInChildren<CharacterController>(true);
        if (charController != null) charController.enabled = false;

        var rb = actor.GetComponentInChildren<Rigidbody>(true);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var animator = actor.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;

        foreach (var cam in actor.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
        foreach (var listener in actor.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
    }

    private static void DisableActorAutoRotation(Transform actor)
    {
        if (actor == null) return;
        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.DisableAutoRotation();
    }

    private static void EnableActorAutoRotation(Transform actor)
    {
        if (actor == null) return;
        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.EnableAutoRotation();
    }

    private static void DisableActorBehaviour(Transform actor)
    {
        if (actor == null) return;
        var mgr = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (mgr != null) mgr.enabled = false;
    }

    private static void EnableActorBehaviour(Transform actor)
    {
        if (actor == null) return;
        var mgr = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (mgr != null) mgr.enabled = true;
    }

    private static IEnumerator Co_SimpleWalkTo(Transform actor, NPCSimpleAnimator anim,
        UnityEngine.AI.NavMeshAgent agent, Vector3 targetPos, float duration)
    {
        if (actor == null) yield break;

        bool agentWasEnabled = agent != null && agent.enabled;
        if (agentWasEnabled) agent.enabled = false;

        var motor = anim == null ? actor.GetComponent<vThirdPersonMotor>() : null;
        Vector3 startPos = actor.position;
        anim?.TransitionToLocomotion();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.position = Vector3.Lerp(startPos, targetPos, t);

            Vector3 faceDir = targetPos - startPos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.001f)
                actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);

            if (anim != null) anim.SetMovementSpeed(0.65f);
            else if (motor != null) motor.overrideInputMagnitude = 0.65f;
            yield return null;
        }

        actor.position = targetPos;
        if (anim != null)
        {
            anim.ResetMovement();
            anim.TransitionToIdle();
        }
        else if (motor != null)
        {
            motor.overrideInputMagnitude = -1f;
        }

        if (agentWasEnabled)
            Game.NPC.Common.NavMeshAgentUtility.SafeEnable(agent, actor, actor.position);
    }

    private IEnumerator ShowSubtitleLocalized(Transform target, string textKey, float durationPerPage,
        string animTrigger = null, bool loopAnim = false, string speakerName = null, Color speakerColor = default)
    {
        if (SpeechBubbleUI.Instance == null) yield break;
        string text = Loc(textKey);
        float voz = VoiceLines.TryPlay(textKey);
        int paginas = Mathf.Max(1, text.Split('\n').Length);
        if (voz > 0f) durationPerPage = Mathf.Max(durationPerPage, (voz + 0.3f) / paginas);

        var npcAnim = target != null ? target.GetComponentInChildren<NPCSimpleAnimator>(true) : null;
        var playerAnim = npcAnim == null && target != null ? target.GetComponentInChildren<PlayerDialogueAnimator>(true) : null;
        npcAnim?.BeginInteraction();
        npcAnim?.SetTalking(true);

        string[] pages = text.Split('\n');
        for (int i = 0; i < pages.Length; i++)
        {
            string page = pages[i].Trim();
            if (string.IsNullOrEmpty(page)) continue;
            bool done = false;
            string triggerThisPage = (i == 0 || loopAnim) ? animTrigger : null;
            if (!string.IsNullOrEmpty(triggerThisPage) && playerAnim != null)
                playerAnim.PlayGesture(triggerThisPage);

            SpeechBubbleUI.Instance.Show(
                target,
                page,
                durationPerPage,
                () => done = true,
                npcAnim != null ? triggerThisPage : null,
                speakerName: speakerName,
                presentacion: PresentacionDeTexto.Subtitulo,
                colorDelNombre: speakerColor
            );
            yield return new WaitUntil(() => done);
        }

        npcAnim?.SetTalking(false);
        npcAnim?.EndInteraction();
    }

    protected override void OnSkipCleanup()
    {
        Time.timeScale = 1f;
        DetenerPlanoActivo();
        OcultarWillEnPov(false);
        RestaurarEntornoVision();
        BandasDeCineUI.Instance?.Mostrar(false, 0f, 0f);
        LineasDeConcentracionUI.Instance?.OcultarInmediato();
        CutInUI.Instance?.Retirar();
        if (_willAuraInstance != null) Destroy(_willAuraInstance);
        if (_runtimeShotHolder != null) Destroy(_runtimeShotHolder);

        // Asegurar posición final limpia frente al altar si se salta a mitad
        if (_willActor != null && _magoOscuroActor != null)
        {
            Vector3 dir = DireccionPlana(_willActor, _magoOscuroActor);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 willDest = new Vector3(0f, _willActor.position.y, -5.0f);
            _willActor.position = willDest;
            FaceTarget(_willActor, _magoOscuroActor);
            if (_estelaActor != null)
            {
                _estelaActor.position = willDest + right * 1.5f;
                FaceTarget(_estelaActor, _magoOscuroActor);
            }
            if (_liamActor != null)
            {
                _liamActor.position = willDest - right * 1.5f;
                FaceTarget(_liamActor, _magoOscuroActor);
            }
        }

        RestaurarActoresTrasCinematica();

        if (_magoBossAI != null)
            _magoBossAI.EmpezarCombate();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        DetenerPlanoActivo();
        OcultarWillEnPov(false);
        RestaurarEntornoVision();
        if (_runtimeShotHolder != null) Destroy(_runtimeShotHolder);
    }
}
