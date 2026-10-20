using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Módulo de escena para la secuencia de apertura de la Batalla Final (`SEQ_BatallaFinal`).
/// Gestiona el cambio de atmósfera al Vacío Tibio, el plano subjetivo (POV) de la cinta roja de
/// Estela atada a la muñeca de Will, la activación de la cinta sobre el hueso de la muñeca del
/// modelo de Will y la entrega final de la escena al combate (`MagoOscuroBossAI.EmpezarCombate`),
/// tanto si la secuencia termina entera como si el jugador la salta con Hold-to-Skip.
/// </summary>
public class ModuloBatallaFinal : SequenceModule
{
    [Header("Referencias de escena")]
    [SerializeField] private MagoOscuroBossAI _magoBossAI;
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _estelaActor;
    [SerializeField] private Transform _liamActor;
    [SerializeField] private Transform _magoActor;
    [SerializeField] private Transform _archimagoActor;

    [Header("Posiciones de entrega al combate")]
    [SerializeField] private Vector3 _willCombatPos = new(0f, 0f, -5f);
    [SerializeField] private Vector3 _estelaCombatPos = new(1.5f, 0f, -5f);
    [SerializeField] private Vector3 _liamCombatPos = new(-1.5f, 0f, -5f);
    [SerializeField] private Vector3 _magoCombatPos = new(0f, 0f, 8f);

    [Header("VFX opcionales")]
    [SerializeField] private GameObject _willAwakenedAuraVfx;
    [SerializeField] private GameObject _vfxAparicionMago;
    [SerializeField] private GameObject _vfxDestelloPechoWill;

    private bool _enVacioTibio;
    private bool _fogGuardado;
    private Color _fogColorGuardado;
    private float _fogDensityGuardada;
    private AmbientMode _ambientModeGuardado;
    private Color _ambientLightGuardada;
    private Camera _camGuardada;
    private CameraClearFlags _clearFlagsGuardados;
    private Color _bgColorGuardado;

    private GameObject _auraPechoInstancia;
    private Light _luzPecho;
    private Coroutine _pulsoCoroutine;
    private GameObject _brazoPovInstancia;
    private readonly List<Renderer> _renderersWillOcultos = new();
    private readonly List<Renderer> _renderersWillPov = new();
    private AudioSource _tinnitusSource;
    private SequenceContext _ctx;

    public override string[] Routines => new[]
    {
        "RasgarAireMago",
        "PulsoPechoWill",
        "ExpandirLuzPecho",
        "FusionArchimagoWill",
        "DespertarWillSuelo",
        "IniciarDespertarPov",
        "FinalizarDespertarPov",
        "ReaccionAnimeLiam",
        "EntrarVacioTibio",
        "SalirVacioTibio",
        "EntrarPovCinta",
        "SalirPovCinta",
        "EmpezarCombate",
    };

    public override IEnumerator Run(string routine, SequenceContext ctx)
    {
        _ctx = ctx;
        switch (routine)
        {
            case "RasgarAireMago":
                yield return Co_RasgarAireMago();
                yield break;

            case "PulsoPechoWill":
                ActivarPulsoPechoWill();
                yield break;

            case "ExpandirLuzPecho":
                yield return Co_ExpandirLuzPecho();
                yield break;

            case "FusionArchimagoWill":
                yield return Co_FusionArchimagoWill();
                yield break;

            case "DespertarWillSuelo":
                yield return Co_DespertarWillSuelo();
                yield break;

            case "IniciarDespertarPov":
                yield return Co_IniciarDespertarPov();
                yield break;

            case "FinalizarDespertarPov":
                yield return Co_FinalizarDespertarPov();
                yield break;

            case "ReaccionAnimeLiam":
                yield return Co_ReaccionAnimeLiam();
                yield break;

            case "EntrarVacioTibio":
                ActivarVacioTibio();
                yield break;

            case "SalirVacioTibio":
                RestaurarAtmosferaArena();
                yield break;

            case "EntrarPovCinta":
                ActivarPovCinta();
                yield break;

            case "SalirPovCinta":
                DesactivarPovCinta();
                yield break;

            case "EmpezarCombate":
                EntregarAlCombate();
                yield break;
        }
        yield break;
    }

    public override void OnSequenceCleanup()
    {
        if (_pulsoCoroutine != null)
        {
            StopCoroutine(_pulsoCoroutine);
            _pulsoCoroutine = null;
        }
        if (_auraPechoInstancia != null)
        {
            Destroy(_auraPechoInstancia);
            _auraPechoInstancia = null;
        }

        if (_tinnitusSource != null)
        {
            _tinnitusSource.Stop();
            Destroy(_tinnitusSource);
            _tinnitusSource = null;
        }
        foreach (var r in _renderersWillPov)
        {
            if (r != null) r.enabled = true;
        }
        _renderersWillPov.Clear();

        DesactivarPovCinta();
        RestaurarAtmosferaArena();

        if (_archimagoActor != null && _archimagoActor.gameObject.activeSelf)
            _archimagoActor.gameObject.SetActive(false);

        EntregarAlCombate();
    }

    private Transform GetWillTransform()
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor("will") ?? _ctx.GetActor("Player");
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        return _willActor;
    }

    private Transform GetMagoTransform()
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor("mago") ?? _ctx.GetActor("NPC_MagoOscuro");
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        return _magoActor;
    }

    private Transform GetArchimagoTransform()
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor("archimago") ?? _ctx.GetActor("NPC_Archimago");
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        return _archimagoActor;
    }

    private Transform GetEstelaTransform()
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor("estela") ?? _ctx.GetActor("NPC_Estela");
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        return _estelaActor;
    }

    private Transform GetLiamTransform()
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor("liam") ?? _ctx.GetActor("NPC_Liam");
            if (actor != null && actor.Transform != null) return actor.Transform;
        }
        return _liamActor;
    }

    private GameObject CrearVfxDesgarroDimensional(Vector3 posGrieta)
    {
        var root = new GameObject("VFX_DesgarroDimensional");
        root.transform.position = posGrieta;

        Quaternion rotCorte = Quaternion.Euler(0f, 180f, 90f);

#if UNITY_EDITOR
        var pChargeSlash = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash purple.prefab");
        if (pChargeSlash != null)
        {
            var slash = Object.Instantiate(pChargeSlash, posGrieta, rotCorte, root.transform);
            slash.transform.localScale = Vector3.one * 1.8f;
        }

        var pElectroSlash = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Electro slash.prefab");
        if (pElectroSlash != null)
        {
            var electro = Object.Instantiate(pElectroSlash, posGrieta, rotCorte, root.transform);
            electro.transform.localScale = Vector3.one * 1.6f;
        }

        var pDarkSmoke = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect5.prefab");
        if (pDarkSmoke != null)
        {
            var dark = Object.Instantiate(pDarkSmoke, posGrieta, Quaternion.identity, root.transform);
            dark.transform.localScale = Vector3.one * 1.5f;
        }
#endif

        var luzGrieta = root.AddComponent<Light>();
        luzGrieta.type = LightType.Point;
        luzGrieta.color = new Color(0.75f, 0.20f, 1.0f);
        luzGrieta.intensity = 35f;
        luzGrieta.range = 10f;
        luzGrieta.shadows = LightShadows.None;

        return root;
    }

    private IEnumerator Co_RasgarAireMago()
    {
        Transform magoT = GetMagoTransform();
        Vector3 posFinal = magoT != null ? magoT.position : new Vector3(0f, 0f, 6.2f);
        Vector3 posGrieta = new Vector3(posFinal.x, 1.40f, posFinal.z);
        Vector3 posDentroGrieta = new Vector3(posFinal.x, 0f, posFinal.z + 1.25f);

        // Fase 1 (0.0s - 0.7s): Tensión precursora, temblor y zumbido oscuro en el aire
        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX("SFX_Prologo_AgujeroNegro_Carga", 0.95f, posGrieta);

        Sendero.Core.Feedback.FeedbackService.CameraShake(0.35f, 0.70f);
        yield return new WaitForSeconds(0.65f);

        // Fase 2 (0.7s - 1.8s): Desgarro violento del tejido de la realidad
        Sendero.Core.Feedback.FeedbackService.CameraShake(0.55f, 0.85f);
        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0.40f, 0.05f, 0.65f, 0.75f), 0.50f);

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("Portal_in", 1.0f, posGrieta);
            AudioService.Instance.PlaySFX("SFX_Prologo_EscudoNace", 0.90f, posGrieta);
        }

        GameObject vfxTear = CrearVfxDesgarroDimensional(posGrieta);
        if (vfxTear != null)
        {
            vfxTear.transform.localScale = Vector3.zero;
            float tOpen = 0f;
            while (tOpen < 0.35f)
            {
                tOpen += Time.deltaTime;
                float k = Mathf.Clamp01(tOpen / 0.35f);
                vfxTear.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.6f, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.35f);

        // Fase 3 (1.4s - 2.6s): Mago Oscuro emerge cruzando la fisura desde posDentroGrieta hacia posFinal
        if (magoT != null)
        {
            magoT.position = posDentroGrieta;
            magoT.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (!magoT.gameObject.activeSelf)
                magoT.gameObject.SetActive(true);

            foreach (var r in magoT.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.forceRenderingOff = false;
            }

            var simpleAnim = magoT.GetComponentInChildren<NPCSimpleAnimator>();
            if (simpleAnim != null)
                simpleAnim.PlaySocialGesture("MagicAttackOmni01_Load");

            float durPaso = 1.25f;
            float elapsedPaso = 0f;
            while (elapsedPaso < durPaso)
            {
                elapsedPaso += Time.deltaTime;
                float k = Mathf.Clamp01(elapsedPaso / durPaso);
                float suave = Mathf.SmoothStep(0f, 1f, k);
                magoT.position = Vector3.Lerp(posDentroGrieta, posFinal, suave);
                yield return null;
            }
            magoT.position = posFinal;
        }
        else
        {
            yield return new WaitForSeconds(1.25f);
        }

        // Fase 4 (2.6s - 3.2s): Colapso e implosión del desgarro dimensional
        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX("SFX_Ki_Disparo_Luz", 0.85f, posGrieta);

        Sendero.Core.Feedback.FeedbackService.CameraShake(0.30f, 0.40f);
        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0.25f, 0.05f, 0.40f, 0.40f), 0.25f);

        if (vfxTear != null)
        {
            float tClose = 0f;
            Vector3 initScale = vfxTear.transform.localScale;
            while (tClose < 0.25f && vfxTear != null)
            {
                tClose += Time.deltaTime;
                float k = Mathf.Clamp01(tClose / 0.25f);
                vfxTear.transform.localScale = Vector3.Lerp(initScale, Vector3.zero, k * k);
                yield return null;
            }
            Destroy(vfxTear);
        }

        yield return new WaitForSeconds(0.20f);
    }

    private void ActivarPulsoPechoWill()
    {
        Transform willT = GetWillTransform();
        if (willT == null) return;
        if (_auraPechoInstancia != null)
        {
            if (_pulsoCoroutine != null) StopCoroutine(_pulsoCoroutine);
            Destroy(_auraPechoInstancia);
            _auraPechoInstancia = null;
        }

        Transform chestBone = null;
        var anim = willT.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
            chestBone = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.UpperChest);

        var root = new GameObject("AuraPechoWill");
        Vector3 posPecho = (chestBone != null ? chestBone.position : (willT.position + Vector3.up * 1.18f)) + willT.forward * 0.18f + Vector3.up * 0.02f;
        root.transform.position = posPecho;
        root.transform.rotation = Quaternion.LookRotation(willT.forward, Vector3.up);
        root.transform.SetParent(chestBone != null ? chestBone : willT, true);

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("SFX_Prologo_EscudoNace", 0.9f, willT.position);
            AudioService.Instance.PlaySFX("SFX_Ki_Disparo_Luz", 0.85f, willT.position);
        }

        // Esfera pura de luz estelar brillante sin partículas de chispas ni estrellas
        var sphereGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereGlow.name = "NucleoBrillo";
        sphereGlow.transform.SetParent(root.transform, false);
        sphereGlow.transform.localPosition = Vector3.zero;
        sphereGlow.transform.localScale = Vector3.one * 0.08f;
        var col = sphereGlow.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        Color goldenHdr = new Color(5.5f, 4.6f, 2.2f, 1.0f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", goldenHdr);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", goldenHdr);
        var sphRen = sphereGlow.GetComponent<Renderer>();
        if (sphRen != null) sphRen.sharedMaterial = mat;

        _luzPecho = root.AddComponent<Light>();
        _luzPecho.type = LightType.Point;
        Color colorLuz = new Color(1f, 0.96f, 0.75f, 1f);
        _luzPecho.color = colorLuz;
        _luzPecho.intensity = 16f;
        _luzPecho.range = 6.5f;
        _luzPecho.shadows = LightShadows.None;

        _auraPechoInstancia = root;
        _pulsoCoroutine = StartCoroutine(Co_PulsoPechoLoop(root.transform, sphereGlow.transform, _luzPecho));
    }

    private IEnumerator Co_PulsoPechoLoop(Transform root, Transform sphereTransform, Light luz)
    {
        float timer = 0f;
        float baseScale = 0.08f;
        while (root != null)
        {
            timer += Time.deltaTime;
            // Crece suavemente desde 0.08m hasta 0.28m de forma natural
            baseScale = Mathf.MoveTowards(baseScale, 0.28f, Time.deltaTime * 0.18f);
            float pulse = Mathf.Sin(timer * 7f);
            float norm = (pulse + 1f) * 0.5f;
            if (sphereTransform != null) sphereTransform.localScale = Vector3.one * (baseScale * Mathf.Lerp(0.85f, 1.18f, norm));
            if (luz != null) luz.intensity = Mathf.Lerp(14f, 45f, Mathf.Clamp01(baseScale / 0.28f) * norm);
            yield return null;
        }
    }

    public void ReaccionAnimeLiam()
    {
        StartCoroutine(Co_ReaccionAnimeLiam());
    }

    private IEnumerator Co_ReaccionAnimeLiam()
    {
        Transform liamT = GetLiamTransform();
        if (liamT == null) yield break;

        var simpleAnim = liamT.GetComponentInChildren<NPCSimpleAnimator>();
        if (simpleAnim != null)
        {
            simpleAnim.PlaySocialGesture("HeadShake01");
        }

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("SFX_Menu_Atras", 0.90f, liamT.position);
        }

        Transform headBone = null;
        var anim = liamT.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);

        var rootGota = new GameObject("AnimeSweatDrop");
        Vector3 posInicial = (headBone != null ? headBone.position + liamT.right * 0.14f + liamT.forward * 0.12f : liamT.position + liamT.right * 0.15f + liamT.forward * 0.15f + Vector3.up * 1.55f);

        rootGota.transform.position = posInicial;
        rootGota.transform.SetParent(headBone != null ? headBone : liamT, true);

        var dropMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dropMesh.name = "GotaMesh";
        dropMesh.transform.SetParent(rootGota.transform, false);
        dropMesh.transform.localPosition = Vector3.zero;
        dropMesh.transform.localScale = new Vector3(0.065f, 0.16f, 0.065f);
        dropMesh.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        var c = dropMesh.GetComponent<Collider>();
        if (c != null) Destroy(c);

        var matDrop = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        Color cDrop = new Color(0.65f, 0.90f, 1.0f, 0.92f);
        if (matDrop.HasProperty("_BaseColor")) matDrop.SetColor("_BaseColor", cDrop);
        if (matDrop.HasProperty("_Color")) matDrop.SetColor("_Color", cDrop);
        var r = dropMesh.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = matDrop;

        float dur = 1.6f;
        float elapsed = 0f;
        while (elapsed < dur && rootGota != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / dur);
            dropMesh.transform.localPosition = Vector3.down * (k * 0.12f);
            float scaleY = Mathf.Lerp(0.16f, 0.22f, k);
            dropMesh.transform.localScale = new Vector3(0.065f, scaleY, 0.065f);
            yield return null;
        }

        if (rootGota != null) Destroy(rootGota);
    }

    private IEnumerator Co_DespertarWillSuelo()
    {
        Transform willT = GetWillTransform();
        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX("SFX_Prologo_AgujeroNegro_Carga", 0.7f, willT != null ? willT.position : Vector3.zero);

        Sendero.Core.Feedback.FeedbackService.ScreenFlash(Color.black, 0.45f);
        yield return new WaitForSeconds(0.45f);

        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0f, 0f, 0f, 0.6f), 0.35f);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Co_IniciarDespertarPov()
    {
        Transform willT = GetWillTransform();
        if (willT != null)
        {
            _renderersWillPov.Clear();
            foreach (var r in willT.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    _renderersWillPov.Add(r);
                }
            }
        }

        if (_tinnitusSource == null)
        {
            _tinnitusSource = gameObject.AddComponent<AudioSource>();
#if UNITY_EDITOR
            _tinnitusSource.clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX_Prologo/tinnitus.mp3");
#endif
            if (_tinnitusSource.clip != null)
            {
                _tinnitusSource.loop = true;
                _tinnitusSource.volume = 0.65f;
                _tinnitusSource.Play();
            }
        }

        Sendero.Core.Feedback.FeedbackService.ScreenFlash(Color.black, 0.65f);
        yield return new WaitForSeconds(0.40f);
        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0f, 0f, 0f, 0.55f), 0.35f);
        yield return new WaitForSeconds(0.30f);
    }

    private IEnumerator Co_FinalizarDespertarPov()
    {
        foreach (var r in _renderersWillPov)
        {
            if (r != null) r.enabled = true;
        }
        _renderersWillPov.Clear();

        if (_tinnitusSource != null)
        {
            float initVol = _tinnitusSource.volume;
            float durFade = 1.4f;
            float el = 0f;
            while (el < durFade && _tinnitusSource != null)
            {
                el += Time.deltaTime;
                _tinnitusSource.volume = Mathf.Lerp(initVol, 0f, el / durFade);
                yield return null;
            }
            if (_tinnitusSource != null)
            {
                _tinnitusSource.Stop();
                Destroy(_tinnitusSource);
                _tinnitusSource = null;
            }
        }
    }

    private IEnumerator Co_FusionArchimagoWill()
    {
        Transform archimagoT = GetArchimagoTransform();
        Transform willT = GetWillTransform();
        if (archimagoT == null || willT == null) yield break;

        var archimagoSimpleAnim = archimagoT.GetComponentInChildren<NPCSimpleAnimator>();
        if (archimagoSimpleAnim != null) archimagoSimpleAnim.PlaySocialGesture("Cheer01");

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("SFX_Prologo_EscudoNace", 0.9f, archimagoT.position);
            AudioService.Instance.PlaySFX("SFX_Ki_Disparo_Luz", 0.85f, archimagoT.position);
        }

        Vector3 origen = archimagoT.position + Vector3.up * 1.25f;
        Vector3 destino = willT.position + Vector3.up * 1.18f;

        // Esfera pura de luz estelar dorada (sin VFX de batalla)
        var goAlma = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goAlma.name = "AlmaArchimagoPura";
        goAlma.transform.position = origen;
        goAlma.transform.localScale = Vector3.one * 0.35f;
        var c = goAlma.GetComponent<Collider>();
        if (c != null) Destroy(c);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        Color colorAlma = new Color(5.0f, 4.2f, 2.2f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colorAlma);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", colorAlma);
        var rAlma = goAlma.GetComponent<Renderer>();
        if (rAlma != null) rAlma.sharedMaterial = mat;

        var luzAlma = goAlma.AddComponent<Light>();
        luzAlma.type = LightType.Point;
        luzAlma.color = new Color(1f, 0.95f, 0.75f);
        luzAlma.intensity = 25f;
        luzAlma.range = 8.5f;

        float duracion = 1.35f;
        float elapsed = 0f;
        bool archimagoDesvanecido = false;
        while (elapsed < duracion)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duracion);
            float suave = Mathf.SmoothStep(0f, 1f, k);
            float arco = Mathf.Sin(k * Mathf.PI) * 0.45f;
            goAlma.transform.position = Vector3.Lerp(origen, destino, suave) + Vector3.up * arco;
            float escala = Mathf.Lerp(0.35f, 0.55f, Mathf.Sin(k * Mathf.PI));
            goAlma.transform.localScale = Vector3.one * escala;

            if (k >= 0.45f && !archimagoDesvanecido)
            {
                archimagoDesvanecido = true;
                foreach (var r in archimagoT.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.forceRenderingOff = true;
                }
            }
            yield return null;
        }

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("SFX_Hechizo_NovaDeLuz", 0.95f, destino);
        }
        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(1f, 0.98f, 0.90f, 0.95f), 0.65f);

        var willSimpleAnim = willT.GetComponentInChildren<NPCSimpleAnimator>();
        if (willSimpleAnim != null)
            willSimpleAnim.PlaySocialGesture("Defend_NoWeapon");

        ActivarPulsoPechoWill();
        Destroy(goAlma);

        if (archimagoT != null)
            archimagoT.gameObject.SetActive(false);
    }

    private IEnumerator Co_ExpandirLuzPecho()
    {
        Transform willT = GetWillTransform();
        if (willT == null) yield break;

        if (_pulsoCoroutine != null)
        {
            StopCoroutine(_pulsoCoroutine);
            _pulsoCoroutine = null;
        }

        Light luz = _luzPecho;
        GameObject luzGo = null;
        if (luz == null)
        {
            luzGo = new GameObject("LuzExpansivaPecho");
            luzGo.transform.SetParent(willT, false);
            luzGo.transform.localPosition = new Vector3(0f, 1.15f, 0.16f);
            luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.98f, 0.85f);
            luz.shadows = LightShadows.None;
        }

        Transform sphereT = null;
        if (_auraPechoInstancia != null)
        {
            var tr = _auraPechoInstancia.transform.Find("NucleoBrillo");
            if (tr != null) sphereT = tr;
        }

        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("SFX_Prologo_Subida", 1.0f, willT.position);
            AudioService.Instance.PlaySFX("SFX_Hechizo_NovaDeLuz", 0.95f, willT.position);
        }

        Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(1f, 0.98f, 0.90f, 0.98f), 1.25f);
        Sendero.Core.Feedback.FeedbackService.CameraShake(0.55f, 1.3f);

        float t = 0f;
        while (t < 1.25f)
        {
            t += Time.deltaTime;
            float k = t / 1.25f;
            float expK = k * k;
            if (luz != null)
            {
                luz.intensity = Mathf.Lerp(25f, 95f, expK);
                luz.range = Mathf.Lerp(8.0f, 50f, expK);
            }
            if (sphereT != null)
            {
                sphereT.localScale = Vector3.one * Mathf.Lerp(0.28f, 22.0f, expK);
            }
            yield return null;
        }

        yield return Sendero.Core.Feedback.FeedbackService.ScreenFadeAsync(new Color(0.98f, 0.96f, 0.92f), 0.6f, fadeIn: true);

        if (luzGo != null) Destroy(luzGo);
        if (_auraPechoInstancia != null)
        {
            Destroy(_auraPechoInstancia);
            _auraPechoInstancia = null;
        }
    }

    private void ActivarVacioTibio()
    {
        if (_enVacioTibio) return;
        _enVacioTibio = true;

        _fogGuardado = RenderSettings.fog;
        _fogColorGuardado = RenderSettings.fogColor;
        _fogDensityGuardada = RenderSettings.fogDensity;
        _ambientModeGuardado = RenderSettings.ambientMode;
        _ambientLightGuardada = RenderSettings.ambientLight;

        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.92f, 0.88f, 0.78f, 1f);

        var cam = Camera.main;
        if (cam != null)
        {
            _camGuardada = cam;
            _clearFlagsGuardados = cam.clearFlags;
            _bgColorGuardado = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.99f, 0.96f, 0.90f, 1f);
        }
    }

    private void RestaurarAtmosferaArena()
    {
        if (_auraPechoInstancia != null)
        {
            Destroy(_auraPechoInstancia);
            _auraPechoInstancia = null;
        }

        if (!_enVacioTibio) return;
        _enVacioTibio = false;

        RenderSettings.fog = _fogGuardado;
        RenderSettings.fogColor = _fogColorGuardado;
        RenderSettings.fogDensity = _fogDensityGuardada;
        RenderSettings.ambientMode = _ambientModeGuardado;
        RenderSettings.ambientLight = _ambientLightGuardada;

        if (_camGuardada != null)
        {
            _camGuardada.clearFlags = _clearFlagsGuardados;
            _camGuardada.backgroundColor = _bgColorGuardado;
            _camGuardada = null;
        }
    }

    private void ActivarPovCinta()
    {
        DesactivarPovCinta();
        Transform willT = GetWillTransform();
        if (willT != null)
            AsegurarCintaRojaEnMuneca(willT);

        OcultarWillEnPov(true);
        var cam = Camera.main;
        if (cam != null)
            _brazoPovInstancia = CrearBrazoPovConCinta(cam.transform);
    }

    private void DesactivarPovCinta()
    {
        if (_brazoPovInstancia != null)
        {
            Destroy(_brazoPovInstancia);
            _brazoPovInstancia = null;
        }
        OcultarWillEnPov(false);
    }

    private void OcultarWillEnPov(bool ocultar)
    {
        if (ocultar)
        {
            _renderersWillOcultos.Clear();
            Transform willT = GetWillTransform();
            if (willT == null) return;
            var renderers = willT.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    renderers[i].enabled = false;
                    _renderersWillOcultos.Add(renderers[i]);
                }
            }
        }
        else
        {
            for (int i = 0; i < _renderersWillOcultos.Count; i++)
            {
                if (_renderersWillOcultos[i] != null)
                    _renderersWillOcultos[i].enabled = true;
            }
            _renderersWillOcultos.Clear();
        }
    }

    private void EntregarAlCombate()
    {
        Transform willT = GetWillTransform();
        Transform estelaT = GetEstelaTransform();
        Transform liamT = GetLiamTransform();
        Transform magoT = GetMagoTransform();

        ColocarEnArena(willT, _willCombatPos, _magoCombatPos);
        ColocarEnArena(estelaT, _estelaCombatPos, _magoCombatPos);
        ColocarEnArena(liamT, _liamCombatPos, _magoCombatPos);
        if (magoT != null)
            ColocarEnArena(magoT, _magoCombatPos, _willCombatPos);

        vThirdPersonCamera.lockCameraForCinematic = false;
        CameraDirectorService.Release(this);

        var cam = Camera.main;
        var tpCam = cam != null ? cam.GetComponent<vThirdPersonCamera>() : Object.FindFirstObjectByType<vThirdPersonCamera>();
        if (tpCam != null)
            tpCam.Init();

        if (_magoBossAI != null)
            _magoBossAI.EmpezarCombate();
    }

    private static void ColocarEnArena(Transform actor, Vector3 destino, Vector3 mirarHacia)
    {
        if (actor == null) return;
        if (!actor.gameObject.activeSelf)
            actor.gameObject.SetActive(true);

        foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null) r.forceRenderingOff = false;
        }

        Vector3 dir = mirarHacia - destino;
        dir.y = 0f;
        Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized) : actor.rotation;

        var cc = actor.GetComponentInChildren<CharacterController>(true);
        bool ccEstabaActivo = cc != null && cc.enabled;
        if (ccEstabaActivo) cc.enabled = false;

        var rb = actor.GetComponentInChildren<Rigidbody>(true);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var agent = actor.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        bool agentEstabaActivo = agent != null && agent.enabled;
        if (agentEstabaActivo) agent.enabled = false;

        actor.SetPositionAndRotation(destino, rot);

        if (ccEstabaActivo) cc.enabled = true;

        if (agentEstabaActivo)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(destino);
        }
    }

    private static Transform AsegurarCintaRojaEnMuneca(Transform willRoot)
    {
        if (willRoot == null) return null;
        var anim = willRoot.GetComponentInChildren<Animator>(true);
        Transform wrist = null;
        if (anim != null && anim.isHuman)
            wrist = anim.GetBoneTransform(HumanBodyBones.LeftHand) ?? anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (wrist == null)
            wrist = willRoot;

        var existing = wrist.Find("CintaRojaEstela");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        var root = new GameObject("CintaRojaEstela").transform;
        root.SetParent(wrist, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = new Color(0.88f, 0.10f, 0.14f, 1f) };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.88f, 0.10f, 0.14f, 1f));
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.45f, 0.04f, 0.06f, 1f));
        }

        var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        band.name = "Banda";
        band.transform.SetParent(root, false);
        band.transform.localPosition = Vector3.zero;
        band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        band.transform.localScale = new Vector3(0.085f, 0.018f, 0.085f);
        Destroy(band.GetComponent<Collider>());
        band.GetComponent<Renderer>().sharedMaterial = mat;

        var tail1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail1.name = "Cabo1";
        tail1.transform.SetParent(root, false);
        tail1.transform.localPosition = new Vector3(0.02f, -0.055f, 0.03f);
        tail1.transform.localRotation = Quaternion.Euler(15f, 0f, -18f);
        tail1.transform.localScale = new Vector3(0.018f, 0.09f, 0.006f);
        Destroy(tail1.GetComponent<Collider>());
        tail1.GetComponent<Renderer>().sharedMaterial = mat;

        var tail2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail2.name = "Cabo2";
        tail2.transform.SetParent(root, false);
        tail2.transform.localPosition = new Vector3(-0.01f, -0.05f, 0.035f);
        tail2.transform.localRotation = Quaternion.Euler(-10f, 0f, 14f);
        tail2.transform.localScale = new Vector3(0.016f, 0.08f, 0.006f);
        Destroy(tail2.GetComponent<Collider>());
        tail2.GetComponent<Renderer>().sharedMaterial = mat;

        return root;
    }

    private static GameObject CrearBrazoPovConCinta(Transform camTransform)
    {
        var root = new GameObject("POV_BrazoConCinta");
        root.transform.SetParent(camTransform, false);
        root.transform.localPosition = new Vector3(-0.04f, -0.16f, 0.42f);
        root.transform.localRotation = Quaternion.Euler(-18f, 28f, -52f);

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var sleeveMat = new Material(shader) { color = new Color(0.16f, 0.22f, 0.36f, 1f) };
        if (sleeveMat.HasProperty("_BaseColor")) sleeveMat.SetColor("_BaseColor", new Color(0.16f, 0.22f, 0.36f, 1f));

        var skinMat = new Material(shader) { color = new Color(0.92f, 0.76f, 0.64f, 1f) };
        if (skinMat.HasProperty("_BaseColor")) skinMat.SetColor("_BaseColor", new Color(0.92f, 0.76f, 0.64f, 1f));

        var ribbonMat = new Material(shader) { color = new Color(0.90f, 0.09f, 0.14f, 1f) };
        if (ribbonMat.HasProperty("_BaseColor")) ribbonMat.SetColor("_BaseColor", new Color(0.90f, 0.09f, 0.14f, 1f));
        if (ribbonMat.HasProperty("_EmissionColor"))
        {
            ribbonMat.EnableKeyword("_EMISSION");
            ribbonMat.SetColor("_EmissionColor", new Color(0.55f, 0.05f, 0.08f, 1f));
        }

        var forearm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        forearm.name = "Antebrazo";
        forearm.transform.SetParent(root.transform, false);
        forearm.transform.localPosition = new Vector3(0f, -0.14f, 0f);
        forearm.transform.localScale = new Vector3(0.085f, 0.16f, 0.085f);
        Destroy(forearm.GetComponent<Collider>());
        forearm.GetComponent<Renderer>().sharedMaterial = sleeveMat;

        var wristSkin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wristSkin.name = "Muneca";
        wristSkin.transform.SetParent(root.transform, false);
        wristSkin.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        wristSkin.transform.localScale = new Vector3(0.072f, 0.045f, 0.072f);
        Destroy(wristSkin.GetComponent<Collider>());
        wristSkin.GetComponent<Renderer>().sharedMaterial = skinMat;

        var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = "ManoCerrada";
        hand.transform.SetParent(root.transform, false);
        hand.transform.localPosition = new Vector3(0f, 0.10f, 0.01f);
        hand.transform.localScale = new Vector3(0.095f, 0.105f, 0.078f);
        Destroy(hand.GetComponent<Collider>());
        hand.GetComponent<Renderer>().sharedMaterial = skinMat;

        var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        band.name = "CintaRoja_Banda";
        band.transform.SetParent(root.transform, false);
        band.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        band.transform.localScale = new Vector3(0.084f, 0.018f, 0.084f);
        Destroy(band.GetComponent<Collider>());
        band.GetComponent<Renderer>().sharedMaterial = ribbonMat;

        var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knot.name = "CintaRoja_Nudo";
        knot.transform.SetParent(root.transform, false);
        knot.transform.localPosition = new Vector3(0.015f, 0.02f, -0.042f);
        knot.transform.localScale = new Vector3(0.032f, 0.028f, 0.028f);
        Destroy(knot.GetComponent<Collider>());
        knot.GetComponent<Renderer>().sharedMaterial = ribbonMat;

        var tail1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail1.name = "CintaRoja_Cabo1";
        tail1.transform.SetParent(root.transform, false);
        tail1.transform.localPosition = new Vector3(0.045f, -0.035f, -0.048f);
        tail1.transform.localRotation = Quaternion.Euler(12f, -10f, -28f);
        tail1.transform.localScale = new Vector3(0.022f, 0.11f, 0.006f);
        Destroy(tail1.GetComponent<Collider>());
        tail1.GetComponent<Renderer>().sharedMaterial = ribbonMat;

        var tail2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail2.name = "CintaRoja_Cabo2";
        tail2.transform.SetParent(root.transform, false);
        tail2.transform.localPosition = new Vector3(-0.01f, -0.042f, -0.048f);
        tail2.transform.localRotation = Quaternion.Euler(-8f, 8f, 18f);
        tail2.transform.localScale = new Vector3(0.020f, 0.095f, 0.006f);
        Destroy(tail2.GetComponent<Collider>());
        tail2.GetComponent<Renderer>().sharedMaterial = ribbonMat;

        var warmLight = new GameObject("LuzCinta").AddComponent<Light>();
        warmLight.transform.SetParent(root.transform, false);
        warmLight.transform.localPosition = new Vector3(0f, 0.05f, -0.25f);
        warmLight.type = LightType.Point;
        warmLight.color = new Color(1f, 0.85f, 0.72f, 1f);
        warmLight.intensity = 1.6f;
        warmLight.range = 1.8f;
        warmLight.shadows = LightShadows.None;

        return root;
    }
}
