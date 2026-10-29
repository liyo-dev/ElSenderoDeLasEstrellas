using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Módulo de escena para la cinemática final («El Sacrificio» y «El Río con Tobías»).
/// Gestiona los efectos visuales del deseo en el altar, el conjuro de resurrección de Will
/// al estilo anime clásico, la transición entre El Sendero, la Piedra Ancestral y la ribera
/// del río, la despedida del Archimago, la escena de pesca con Tobías, la estrella torcida
/// de madera en el cielo nocturno, la frase final de la novela y el encadenado entre las dos partes.
/// </summary>
public class ModuloCinematicaFinal : SequenceModule
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

    [Header("Efectos de Portal y Conjuro de Resurrección")]
    [SerializeField] private GameObject _vfxDeseoAltar;
    [SerializeField] private GameObject _vfxPortal;
    [SerializeField] private GameObject _vfxEmpujeLuz;
    [SerializeField] private GameObject _vfxCirculoConjuro;
    [SerializeField] private GameObject _vfxAuraConjuroWill;
    [SerializeField] private GameObject _vfxPilarResurreccion;
    [SerializeField] private GameObject _vfxResurreccion;
    [SerializeField] private GameObject _vfxColapsoSendero;

    [Header("Efectos del Archimago y Will")]
    [SerializeField] private GameObject _vfxEspirituArchimago;
    [SerializeField] private GameObject _cintaRojaWill;

    [Header("Efectos del Río y Noche")]
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
    [SerializeField] private Color _colorSolAtardecer = new(1.0f, 0.58f, 0.34f);
    [SerializeField] private Color _colorLuzNoche = new(0.24f, 0.32f, 0.58f);
    [SerializeField] private GameObject _estrellaTorcidaObjeto;
    [SerializeField] private GameObject _estrellasNocturnasRoot;

    [Header("Portales y Cámaras de Umbral")]
    [SerializeField] private GameObject _portalUmbralSendero;
    [SerializeField] private GameObject _portalUmbralPiedra;
    [SerializeField] private GameObject _ventanaPortalSendero;
    [SerializeField] private GameObject _ventanaPortalPiedra;
    [SerializeField] private Camera _camPortalSendero;
    [SerializeField] private Camera _camPortalPiedra;

    private SequenceContext _ctx;
    private readonly List<GameObject> _instanciasTemporales = new();
    private Coroutine _corutinaHacesLuz;
    private Coroutine _corutinaFlotacionEspiritu;
    private Coroutine _corutinaPulsoEstrella;
    private RenderTexture _rtPortalSendero;
    private RenderTexture _rtPortalPiedra;

    // Componentes de la Pantalla Dividida en el Umbral del Portal
    private GameObject _splitScreenCanvasGo;
    private CanvasGroup _splitCanvasGroup;
    private Camera _camSplitWill;
    private Camera _camSplitEstela;
    private Light _luzSplitWill;
    private Light _luzSplitEstela;
    private RenderTexture _rtSplitWill;
    private RenderTexture _rtSplitEstela;
    private Coroutine _corutinaSplitTracking;

    public override string[] Routines => new[]
    {
        "PrepararSendero",
        "AmbienteSendero",
        "AmbientePiedra",
        "AmbientePiedraAncestral",
        "ActivarPantallaDividida",
        "DesactivarPantallaDividida",
        "DeseoEnElAltar",
        "AbrirPortalUmbral",
        "CerrarPortalSendero",
        "EmpujePortal",
        "ConjuroResurreccionInicio",
        "ConjuroResurreccionCarga",
        "ConjuroResurreccionClimax",
        "ColapsoSendero",
        "EspirituArchimago",
        "DespedidaArchimago",
        "RegresoWill",
        "EncadenarParte2",
        "PrepararRioDia",
        "TarjetaSemanasDespues",
        "SaltoRioEstela",
        "TransicionAtardecer",
        "TransicionNoche",
        "EnfocarEstrella",
        "MostrarFraseFinalLibro",
        "IrACreditos"
    };

    public override IEnumerator Run(string routine, SequenceContext ctx)
    {
        _ctx = ctx;
        switch (routine)
        {
            case "PrepararSendero":
            case "AmbienteSendero":
                AplicarAmbienteSendero();
                yield break;

            case "AmbientePiedra":
            case "AmbientePiedraAncestral":
                AplicarAmbienteNochePiedra();
                yield break;

            case "ActivarPantallaDividida":
                yield return Co_ActivarPantallaDividida();
                yield break;

            case "DesactivarPantallaDividida":
                yield return Co_DesactivarPantallaDividida();
                yield break;

            case "DeseoEnElAltar":
                yield return Co_DeseoEnElAltar();
                yield break;

            case "AbrirPortalUmbral":
                yield return Co_AbrirPortalUmbral();
                yield break;

            case "CerrarPortalSendero":
                yield return Co_CerrarPortalSendero();
                yield break;

            case "EmpujePortal":
                yield return Co_EmpujePortal();
                yield break;

            case "ConjuroResurreccionInicio":
                yield return Co_ConjuroResurreccionInicio();
                yield break;

            case "ConjuroResurreccionCarga":
                yield return Co_ConjuroResurreccionCarga();
                yield break;

            case "ConjuroResurreccionClimax":
                yield return Co_ConjuroResurreccionClimax();
                yield break;

            case "ColapsoSendero":
                yield return Co_ColapsoSendero();
                yield break;

            case "EspirituArchimago":
                yield return Co_EspirituArchimago();
                yield break;

            case "DespedidaArchimago":
                yield return Co_DespedidaArchimago();
                yield break;

            case "RegresoWill":
                yield return Co_RegresoWill();
                yield break;

            case "EncadenarParte2":
                yield return Co_EncadenarParte2();
                yield break;

            case "PrepararRioDia":
                PrepararEscenaRio();
                yield break;

            case "TarjetaSemanasDespues":
                yield return Co_TarjetaSemanasDespues();
                yield break;

            case "SaltoRioEstela":
                yield return Co_SaltoRioEstela();
                yield break;

            case "TransicionAtardecer":
                yield return Co_TransicionAtardecer();
                yield break;

            case "TransicionNoche":
                yield return Co_TransicionNoche();
                yield break;

            case "EnfocarEstrella":
                yield return Co_EnfocarEstrella();
                yield break;

            case "MostrarFraseFinalLibro":
                yield return Co_MostrarFraseFinalLibro();
                yield break;

            case "IrACreditos":
                yield return Co_IrACreditos();
                yield break;
        }
    }

    public override void OnSequenceCleanup()
    {
        LimpiarEfectosTemporales();
    }

    private void OnDisable()
    {
        LimpiarEfectosTemporales();
    }

    private void LimpiarEfectosTemporales()
    {
        if (_corutinaHacesLuz != null)
        {
            StopCoroutine(_corutinaHacesLuz);
            _corutinaHacesLuz = null;
        }

        if (_corutinaSplitTracking != null)
        {
            StopCoroutine(_corutinaSplitTracking);
            _corutinaSplitTracking = null;
        }

        if (_corutinaFlotacionEspiritu != null)
        {
            StopCoroutine(_corutinaFlotacionEspiritu);
            _corutinaFlotacionEspiritu = null;
        }

        if (_corutinaPulsoEstrella != null)
        {
            StopCoroutine(_corutinaPulsoEstrella);
            _corutinaPulsoEstrella = null;
        }

        if (_rtPortalSendero != null)
        {
            _rtPortalSendero.Release();
            Destroy(_rtPortalSendero);
            _rtPortalSendero = null;
        }
        if (_rtPortalPiedra != null)
        {
            _rtPortalPiedra.Release();
            Destroy(_rtPortalPiedra);
            _rtPortalPiedra = null;
        }
        if (_camPortalSendero != null) _camPortalSendero.enabled = false;
        if (_camPortalPiedra != null) _camPortalPiedra.enabled = false;

        if (_rtSplitWill != null)
        {
            _rtSplitWill.Release();
            Destroy(_rtSplitWill);
            _rtSplitWill = null;
        }
        if (_rtSplitEstela != null)
        {
            _rtSplitEstela.Release();
            Destroy(_rtSplitEstela);
            _rtSplitEstela = null;
        }
        _splitScreenCanvasGo = null;
        _splitCanvasGroup = null;
        _camSplitWill = null;
        _camSplitEstela = null;
        _luzSplitWill = null;
        _luzSplitEstela = null;

        for (int i = 0; i < _instanciasTemporales.Count; i++)
        {
            if (_instanciasTemporales[i] != null)
                Destroy(_instanciasTemporales[i]);
        }
        _instanciasTemporales.Clear();
    }

    private static void LimpiarSubemisoresDeFlechasEnVfx(GameObject vfxRoot)
    {
        if (vfxRoot == null) return;
        var sistemas = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < sistemas.Length; i++)
        {
            var ps = sistemas[i];
            if (ps == null) continue;
            string n = ps.gameObject.name.ToLowerInvariant();
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            string matName = (pr != null && pr.sharedMaterial != null) ? pr.sharedMaterial.name.ToLowerInvariant() : string.Empty;
            string texName = (pr != null && pr.sharedMaterial != null && pr.sharedMaterial.mainTexture != null)
                ? pr.sharedMaterial.mainTexture.name.ToLowerInvariant()
                : string.Empty;

            if (n.Contains("arrow") || n.Contains("cross") || n.Contains("plus") || n.Contains("icon") ||
                matName.Contains("arrow") || matName.Contains("cross") || matName.Contains("plus") ||
                texName.Contains("arrow") || texName.Contains("cross") || texName.Contains("plus"))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (pr != null) pr.enabled = false;
                ps.gameObject.SetActive(false);
            }
        }
    }

    private void CrearPantallaDivididaSiNecesario()
    {
        if (_splitScreenCanvasGo != null) return;

        int w = Mathf.Max(512, Screen.width / 2);
        int h = Mathf.Max(512, Screen.height);
        _rtSplitWill = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "RT_Split_Will"
        };
        _rtSplitWill.Create();

        _rtSplitEstela = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "RT_Split_Estela"
        };
        _rtSplitEstela.Create();

        var goCamWill = new GameObject("CamSplit_Will");
        _camSplitWill = goCamWill.AddComponent<Camera>();
        _camSplitWill.targetTexture = _rtSplitWill;
        _camSplitWill.clearFlags = CameraClearFlags.Skybox;
        var skyWill = _camSplitWill.gameObject.AddComponent<Skybox>();
        if (_skyboxSendero != null) skyWill.material = _skyboxSendero;
        _camSplitWill.cullingMask = ~0;
        _camSplitWill.fieldOfView = 34f;
        _camSplitWill.nearClipPlane = 0.1f;
        _camSplitWill.farClipPlane = 600f;
        var uacWill = goCamWill.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        uacWill.renderPostProcessing = true;

        var goLuzWill = new GameObject("LuzRostro_SplitWill");
        goLuzWill.transform.SetParent(goCamWill.transform, false);
        _luzSplitWill = goLuzWill.AddComponent<Light>();
        _luzSplitWill.type = LightType.Point;
        _luzSplitWill.color = new Color(1.0f, 0.95f, 0.88f);
        _luzSplitWill.range = 5.0f;
        _luzSplitWill.intensity = 1.6f;

        _instanciasTemporales.Add(goCamWill);

        var goCamEstela = new GameObject("CamSplit_Estela");
        _camSplitEstela = goCamEstela.AddComponent<Camera>();
        _camSplitEstela.targetTexture = _rtSplitEstela;
        _camSplitEstela.clearFlags = CameraClearFlags.Skybox;
        var skyEstela = goCamEstela.AddComponent<Skybox>();
        if (_skyboxNoche != null) skyEstela.material = _skyboxNoche;
        _camSplitEstela.cullingMask = ~0;
        _camSplitEstela.fieldOfView = 34f;
        _camSplitEstela.nearClipPlane = 0.1f;
        _camSplitEstela.farClipPlane = 600f;
        var uacEstela = goCamEstela.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        uacEstela.renderPostProcessing = true;

        var goLuzEstela = new GameObject("LuzRostro_SplitEstela");
        goLuzEstela.transform.SetParent(goCamEstela.transform, false);
        _luzSplitEstela = goLuzEstela.AddComponent<Light>();
        _luzSplitEstela.type = LightType.Point;
        _luzSplitEstela.color = new Color(0.92f, 0.95f, 1.0f);
        _luzSplitEstela.range = 5.0f;
        _luzSplitEstela.intensity = 1.6f;

        _instanciasTemporales.Add(goCamEstela);

        _splitScreenCanvasGo = new GameObject("Canvas_PantallaDividida_Final");
        var canvas = _splitScreenCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;

        var scaler = _splitScreenCanvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _splitCanvasGroup = _splitScreenCanvasGo.AddComponent<CanvasGroup>();
        _splitCanvasGroup.alpha = 0f;

        var goRawWill = new GameObject("RawImage_Will");
        goRawWill.transform.SetParent(_splitScreenCanvasGo.transform, false);
        var rawWill = goRawWill.AddComponent<UnityEngine.UI.RawImage>();
        rawWill.texture = _rtSplitWill;
        var rtW = rawWill.rectTransform;
        rtW.anchorMin = new Vector2(0f, 0f);
        rtW.anchorMax = new Vector2(0.5f, 1f);
        rtW.offsetMin = Vector2.zero;
        rtW.offsetMax = Vector2.zero;

        var goRawEstela = new GameObject("RawImage_Estela");
        goRawEstela.transform.SetParent(_splitScreenCanvasGo.transform, false);
        var rawEstela = goRawEstela.AddComponent<UnityEngine.UI.RawImage>();
        rawEstela.texture = _rtSplitEstela;
        var rtE = rawEstela.rectTransform;
        rtE.anchorMin = new Vector2(0.5f, 0f);
        rtE.anchorMax = new Vector2(1f, 1f);
        rtE.offsetMin = Vector2.zero;
        rtE.offsetMax = Vector2.zero;

        var goCostura = new GameObject("Costura_Portal_Centro");
        goCostura.transform.SetParent(_splitScreenCanvasGo.transform, false);
        var imgCostura = goCostura.AddComponent<UnityEngine.UI.Image>();
        imgCostura.color = new Color(1.0f, 0.92f, 0.65f, 0.95f);
        var rtCostura = imgCostura.rectTransform;
        rtCostura.anchorMin = new Vector2(0.5f, 0f);
        rtCostura.anchorMax = new Vector2(0.5f, 1f);
        rtCostura.sizeDelta = new Vector2(6f, 0f);

        _instanciasTemporales.Add(_splitScreenCanvasGo);
    }

    private void ActualizarPosicionesCamarasSplit()
    {
        var will = ResolverActor("will", _willActor);
        if (_camSplitWill != null)
        {
            Vector3 posBase = will != null ? will.position : new Vector3(0f, 0f, 5.2f);
            Vector3 fwd = will != null ? will.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            else fwd.Normalize();
            Vector3 rgt = Vector3.Cross(Vector3.up, fwd);

            Vector3 targetPos = posBase + Vector3.up * 1.05f;
            Vector3 camPos = posBase + fwd * 2.15f - rgt * 0.32f + Vector3.up * 1.18f;
            _camSplitWill.transform.position = camPos;
            _camSplitWill.transform.LookAt(targetPos);
        }

        var estela = ResolverActor("estela", _estelaActor);
        if (_camSplitEstela != null)
        {
            Vector3 posBase = estela != null ? estela.position : new Vector3(200.0f, 0f, 4.8f);
            Vector3 fwdE = estela != null ? estela.forward : Vector3.back;
            fwdE.y = 0f;
            if (fwdE.sqrMagnitude < 0.01f) fwdE = Vector3.back;
            else fwdE.Normalize();
            Vector3 rgtE = Vector3.Cross(Vector3.up, fwdE);

            Vector3 targetPos = posBase + Vector3.up * 1.02f;
            Vector3 camPos = posBase + fwdE * 2.10f + rgtE * 0.32f + Vector3.up * 1.15f;
            _camSplitEstela.transform.position = camPos;
            _camSplitEstela.transform.LookAt(targetPos);
        }
    }

    private IEnumerator Co_ActualizarCamarasPantallaDividida()
    {
        while (_splitCanvasGroup != null && _splitCanvasGroup.alpha > 0.001f)
        {
            ActualizarPosicionesCamarasSplit();
            yield return null;
        }
    }

    private IEnumerator Co_ActivarPantallaDividida()
    {
        CrearPantallaDivididaSiNecesario();
        ActualizarPosicionesCamarasSplit();

        if (_camSplitWill != null) _camSplitWill.gameObject.SetActive(true);
        if (_camSplitEstela != null) _camSplitEstela.gameObject.SetActive(true);
        if (_splitScreenCanvasGo != null) _splitScreenCanvasGo.SetActive(true);

        if (_corutinaSplitTracking != null)
            StopCoroutine(_corutinaSplitTracking);
        _corutinaSplitTracking = StartCoroutine(Co_ActualizarCamarasPantallaDividida());

        float dur = 0.40f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (_splitCanvasGroup != null)
                _splitCanvasGroup.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        if (_splitCanvasGroup != null) _splitCanvasGroup.alpha = 1f;
    }

    private IEnumerator Co_DesactivarPantallaDividida()
    {
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (_splitCanvasGroup != null)
                _splitCanvasGroup.alpha = 1f - Mathf.Clamp01(t / dur);
            yield return null;
        }
        if (_corutinaSplitTracking != null)
        {
            StopCoroutine(_corutinaSplitTracking);
            _corutinaSplitTracking = null;
        }
        if (_splitCanvasGroup != null) _splitCanvasGroup.alpha = 0f;
        if (_splitScreenCanvasGo != null) _splitScreenCanvasGo.SetActive(false);
        if (_camSplitWill != null) _camSplitWill.gameObject.SetActive(false);
        if (_camSplitEstela != null) _camSplitEstela.gameObject.SetActive(false);
    }

    private Transform ResolverActor(string idOrAlias, Transform respaldo)
    {
        if (_ctx != null)
        {
            var actor = _ctx.GetActor(idOrAlias);
            if (actor != null && actor.Transform != null)
                return actor.Transform;

            string idPrefijo = "Actor_" + char.ToUpper(idOrAlias[0]) + idOrAlias.Substring(1).ToLower();
            actor = _ctx.GetActor(idPrefijo);
            if (actor != null && actor.Transform != null)
                return actor.Transform;
        }

        if (respaldo != null) return respaldo;

        string nombreEsperado = "Actor_" + char.ToUpper(idOrAlias[0]) + idOrAlias.Substring(1).ToLower();
        var go = GameObject.Find(nombreEsperado) ?? GameObject.Find(idOrAlias);
        if (go != null) return go.transform;

        return null;
    }

    private void ResolverZonasSiFaltan()
    {
        if (_zonaSenderoRoot == null) _zonaSenderoRoot = GameObject.Find("ZONA_1_EL_SENDERO");
        if (_zonaPiedraRoot == null) _zonaPiedraRoot = GameObject.Find("ZONA_2_PIEDRA_ANCESTRAL");
        if (_zonaRioRoot == null) _zonaRioRoot = GameObject.Find("ZONA_3_EL_RIO");
    }

    private void AplicarAmbienteSendero()
    {
        ResolverZonasSiFaltan();
        if (_zonaSenderoRoot != null && !_zonaSenderoRoot.activeSelf) _zonaSenderoRoot.SetActive(true);
        if (_zonaPiedraRoot != null && _zonaPiedraRoot.activeSelf) _zonaPiedraRoot.SetActive(false);
        if (_zonaRioRoot != null && _zonaRioRoot.activeSelf) _zonaRioRoot.SetActive(false);

        if (_skyboxSendero != null)
            RenderSettings.skybox = _skyboxSendero;

        ActualizarSkyboxCamaraPrincipal(_skyboxSendero);

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
        if (liam != null)
        {
            var emotion = liam.GetComponentInChildren<NPCEmotionController>();
            if (emotion != null)
            {
                emotion.AplicarOjos("Eye09");
                emotion.SetCaraDeFondo(this, NPCEmotion.Tired);
            }
        }

        if (_archimagoActor != null && _archimagoActor.gameObject.activeSelf)
            _archimagoActor.gameObject.SetActive(false);

        if (_tobiasActor != null && _tobiasActor.gameObject.activeSelf)
            _tobiasActor.gameObject.SetActive(false);

        if (_estrellasNocturnasRoot != null && _estrellasNocturnasRoot.activeSelf)
            _estrellasNocturnasRoot.SetActive(false);

        if (_estrellaTorcidaObjeto != null && _estrellaTorcidaObjeto.activeSelf)
            _estrellaTorcidaObjeto.SetActive(false);

        if (_luzNocheRioGrupo != null && _luzNocheRioGrupo.gameObject.activeSelf)
            _luzNocheRioGrupo.gameObject.SetActive(false);
    }

    private void AplicarAmbienteNochePiedra()
    {
        ResolverZonasSiFaltan();
        if (_zonaSenderoRoot != null && _zonaSenderoRoot.activeSelf) _zonaSenderoRoot.SetActive(false);
        if (_zonaPiedraRoot != null && !_zonaPiedraRoot.activeSelf) _zonaPiedraRoot.SetActive(true);
        if (_zonaRioRoot != null && _zonaRioRoot.activeSelf) _zonaRioRoot.SetActive(false);

        if (_skyboxNoche != null)
            RenderSettings.skybox = _skyboxNoche;

        ActualizarSkyboxCamaraPrincipal(_skyboxNoche);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.20f, 0.26f, 0.42f);
        RenderSettings.fog = false;

        if (_luzSolar != null)
        {
            _luzSolar.color = _colorLuzNoche;
            _luzSolar.intensity = 0.90f;
            _luzSolar.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        }

        var estela = ResolverActor("estela", _estelaActor);
        if (estela != null)
        {
            if (!estela.gameObject.activeSelf)
                estela.gameObject.SetActive(true);
            var renderersEstela = estela.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderersEstela.Length; i++)
                if (renderersEstela[i] != null) renderersEstela[i].enabled = true;
        }

        var liam = ResolverActor("liam", _liamActor);
        if (liam != null)
        {
            if (!liam.gameObject.activeSelf)
                liam.gameObject.SetActive(true);
            var renderersLiam = liam.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderersLiam.Length; i++)
                if (renderersLiam[i] != null) renderersLiam[i].enabled = true;
        }
    }

    private void AplicarAmbienteDia()
    {
        if (_skyboxDia != null)
            RenderSettings.skybox = _skyboxDia;

        ActualizarSkyboxCamaraPrincipal(_skyboxDia);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.69f, 0.75f);
        RenderSettings.fog = false;

        if (_luzSolar != null)
        {
            _luzSolar.color = _colorSolDia;
            _luzSolar.intensity = 1.35f;
            _luzSolar.transform.rotation = Quaternion.Euler(46f, 155f, 0f);
        }

        if (_luzNocheRioGrupo != null && _luzNocheRioGrupo.gameObject.activeSelf)
            _luzNocheRioGrupo.gameObject.SetActive(false);

        var estela = ResolverActor("estela", _estelaActor);
        if (estela != null)
        {
            if (!estela.gameObject.activeSelf)
                estela.gameObject.SetActive(true);
            var renderersEstela = estela.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderersEstela.Length; i++)
                if (renderersEstela[i] != null) renderersEstela[i].enabled = true;
        }

        var liam = ResolverActor("liam", _liamActor);
        if (liam != null)
        {
            if (!liam.gameObject.activeSelf)
                liam.gameObject.SetActive(true);
            var renderersLiam = liam.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderersLiam.Length; i++)
                if (renderersLiam[i] != null) renderersLiam[i].enabled = true;
        }
    }

    private void PrepararEscenaRio()
    {
        ResolverZonasSiFaltan();
        if (_zonaSenderoRoot != null && _zonaSenderoRoot.activeSelf) _zonaSenderoRoot.SetActive(false);
        if (_zonaPiedraRoot != null && _zonaPiedraRoot.activeSelf) _zonaPiedraRoot.SetActive(false);
        if (_zonaRioRoot != null && !_zonaRioRoot.activeSelf) _zonaRioRoot.SetActive(true);

        AplicarAmbienteDia();

        var will = ResolverActor("will", _willActor);
        if (will != null && !will.gameObject.activeSelf)
            will.gameObject.SetActive(true);

        var estela = ResolverActor("estela", _estelaActor);
        if (estela != null && !estela.gameObject.activeSelf)
            estela.gameObject.SetActive(true);

        var liam = ResolverActor("liam", _liamActor);
        if (liam != null && !liam.gameObject.activeSelf)
            liam.gameObject.SetActive(true);

        var tobias = ResolverActor("tobias", _tobiasActor);
        if (tobias != null && !tobias.gameObject.activeSelf)
            tobias.gameObject.SetActive(true);

        if (_canaPescarWill != null && !_canaPescarWill.activeSelf)
            _canaPescarWill.SetActive(true);

        if (_canaPescarTobias != null && !_canaPescarTobias.activeSelf)
            _canaPescarTobias.SetActive(true);

        if (_cuadernoDibujoTobias != null && !_cuadernoDibujoTobias.activeSelf)
            _cuadernoDibujoTobias.SetActive(true);

        if (_estrellasNocturnasRoot != null && _estrellasNocturnasRoot.activeSelf)
            _estrellasNocturnasRoot.SetActive(false);

        if (_estrellaTorcidaObjeto != null && _estrellaTorcidaObjeto.activeSelf)
            _estrellaTorcidaObjeto.SetActive(false);
    }

    private IEnumerator Co_DeseoEnElAltar()
    {
        Vector3 posAltar = _altarTransform != null ? _altarTransform.position + Vector3.up * 1.1f : new Vector3(0f, 1.2f, 0f);

        if (_vfxDeseoAltar != null)
        {
            _vfxDeseoAltar.transform.position = posAltar;
            if (!_vfxDeseoAltar.activeSelf) _vfxDeseoAltar.SetActive(true);
        }

        CrearPulsoLuzTemporal(posAltar, new Color(1.0f, 0.88f, 0.45f), 8f, 14f, 1.8f);
        FeedbackService.CameraShake(0.22f, 0.35f);
        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator Co_AbrirPortalUmbral()
    {
        AsegurarTexturasPortales();

        if (_portalUmbralSendero != null) _portalUmbralSendero.SetActive(true);
        if (_portalUmbralPiedra != null) _portalUmbralPiedra.SetActive(true);
        if (_ventanaPortalSendero != null) _ventanaPortalSendero.SetActive(true);
        if (_ventanaPortalPiedra != null) _ventanaPortalPiedra.SetActive(true);
        if (_camPortalSendero != null) _camPortalSendero.enabled = true;
        if (_camPortalPiedra != null) _camPortalPiedra.enabled = true;
        if (_vfxPortal != null && !_vfxPortal.activeSelf) _vfxPortal.SetActive(true);

        Vector3 posPortal = _portalUmbralTransform != null
            ? _portalUmbralTransform.position + Vector3.up * 1.8f
            : new Vector3(0f, 1.8f, 8.0f);

        CrearPulsoLuzTemporal(posPortal, new Color(0.65f, 0.88f, 1.0f), 10f, 18f, 2.2f);
        FeedbackService.CameraShake(0.35f, 0.5f);
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator Co_CerrarPortalSendero()
    {
        Vector3 posPortal = _portalUmbralTransform != null
            ? _portalUmbralTransform.position + Vector3.up * 1.8f
            : new Vector3(0f, 1.8f, 8.0f);

        CrearPulsoLuzTemporal(posPortal, new Color(1.0f, 0.92f, 0.75f), 14f, 24f, 1.2f);
        FeedbackService.CameraShake(0.95f, 1.4f);

        if (_vfxEmpujeLuz != null)
        {
            _vfxEmpujeLuz.transform.position = posPortal;
            _vfxEmpujeLuz.SetActive(true);
        }

        float dur = 0.55f;
        float t = 0f;
        Vector3 escalaInicial = _vfxPortal != null ? _vfxPortal.transform.localScale : Vector3.one;
        Vector3 escalaSendero = _ventanaPortalSendero != null ? _ventanaPortalSendero.transform.localScale : Vector3.one;
        Vector3 escalaPiedra = _ventanaPortalPiedra != null ? _ventanaPortalPiedra.transform.localScale : Vector3.one;

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            if (_vfxPortal != null)
                _vfxPortal.transform.localScale = escalaInicial * k;
            if (_ventanaPortalSendero != null)
                _ventanaPortalSendero.transform.localScale = escalaSendero * k;
            if (_ventanaPortalPiedra != null)
                _ventanaPortalPiedra.transform.localScale = escalaPiedra * k;
            yield return null;
        }

        if (_camPortalSendero != null) _camPortalSendero.enabled = false;
        if (_camPortalPiedra != null) _camPortalPiedra.enabled = false;
        if (_ventanaPortalSendero != null) _ventanaPortalSendero.SetActive(false);
        if (_ventanaPortalPiedra != null) _ventanaPortalPiedra.SetActive(false);
        if (_portalUmbralPiedra != null) _portalUmbralPiedra.SetActive(false);
        if (_portalUmbralSendero != null) _portalUmbralSendero.SetActive(false);
        if (_zonaPiedraRoot != null) _zonaPiedraRoot.SetActive(false);

        if (_rtPortalSendero != null)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rtPortalSendero;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }
        if (_rtPortalPiedra != null)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rtPortalPiedra;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        if (_vfxPortal != null)
        {
            _vfxPortal.SetActive(false);
            _vfxPortal.transform.localScale = escalaInicial;
        }
        yield return new WaitForSeconds(0.2f);
    }

    private void ActualizarSkyboxCamaraPrincipal(Material skyMat)
    {
        if (skyMat == null) return;
        RenderSettings.skybox = skyMat;
        var cam = _ctx?.Player?.CachedCamera ?? Camera.main;
        if (cam == null)
            cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            var sky = cam.GetComponent<Skybox>();
            if (sky == null) sky = cam.gameObject.AddComponent<Skybox>();
            sky.material = skyMat;
        }
    }

    private void AsegurarTexturasPortales()
    {
        if (_rtPortalSendero == null)
        {
            _rtPortalSendero = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_Portal_Sendero",
                filterMode = FilterMode.Bilinear
            };
            _rtPortalSendero.Create();
        }
        if (_rtPortalPiedra == null)
        {
            _rtPortalPiedra = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_Portal_Piedra",
                filterMode = FilterMode.Bilinear
            };
            _rtPortalPiedra.Create();
        }

        if (_camPortalSendero != null)
        {
            _camPortalSendero.targetTexture = _rtPortalSendero;
            _camPortalSendero.clearFlags = CameraClearFlags.Skybox;
            var sky = _camPortalSendero.GetComponent<Skybox>();
            if (sky == null) sky = _camPortalSendero.gameObject.AddComponent<Skybox>();
            if (_skyboxSendero != null) sky.material = _skyboxSendero;
        }
        if (_camPortalPiedra != null)
        {
            _camPortalPiedra.targetTexture = _rtPortalPiedra;
            _camPortalPiedra.clearFlags = CameraClearFlags.Skybox;
            var sky = _camPortalPiedra.GetComponent<Skybox>();
            if (sky == null) sky = _camPortalPiedra.gameObject.AddComponent<Skybox>();
            if (_skyboxNoche != null) sky.material = _skyboxNoche;
        }

        AsignarTexturaAQuad(_ventanaPortalSendero, _rtPortalSendero);
        AsignarTexturaAQuad(_ventanaPortalPiedra, _rtPortalPiedra);
    }

    private void AsignarTexturaAQuad(GameObject quadGo, RenderTexture rt)
    {
        if (quadGo == null || rt == null) return;
        var mr = quadGo.GetComponent<MeshRenderer>();
        if (mr == null) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        mat.mainTexture = rt;
        mat.color = new Color(0.82f, 0.94f, 1.0f, 1.0f);
        mat.SetFloat("_Cull", 0f);
        mr.sharedMaterial = mat;
    }

    private IEnumerator Co_EmpujePortal()
    {
        var will = ResolverActor("will", _willActor);
        var estela = ResolverActor("estela", _estelaActor);

        if (_vfxEmpujeLuz != null)
        {
            if (will != null)
                _vfxEmpujeLuz.transform.position = will.position + Vector3.up * 1.1f + will.forward * 0.6f;
            if (!_vfxEmpujeLuz.activeSelf)
                _vfxEmpujeLuz.SetActive(true);
        }

        FeedbackService.CameraShake(0.45f, 0.35f);

        if (estela != null)
        {
            Vector3 origen = estela.position;
            Vector3 destino = _portalUmbralTransform != null
                ? new Vector3(_portalUmbralTransform.position.x, origen.y, _portalUmbralTransform.position.z + 0.5f)
                : origen + new Vector3(0f, 0f, 2.0f);

            float dur = 0.55f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                estela.position = Vector3.Lerp(origen, destino, k);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.55f);
        }
    }

    private IEnumerator Co_ConjuroResurreccionInicio()
    {
        ResolverZonasSiFaltan();
        if (_zonaSenderoRoot != null && !_zonaSenderoRoot.activeSelf) _zonaSenderoRoot.SetActive(true);
        if (_zonaPiedraRoot != null && _zonaPiedraRoot.activeSelf) _zonaPiedraRoot.SetActive(false);
        if (_zonaRioRoot != null && _zonaRioRoot.activeSelf) _zonaRioRoot.SetActive(false);

        var will = ResolverActor("will", _willActor);
        Vector3 posWill = will != null ? will.position : new Vector3(0f, 0f, 4.5f);

        if (_vfxCirculoConjuro != null)
        {
            LimpiarSubemisoresDeFlechasEnVfx(_vfxCirculoConjuro);
            _vfxCirculoConjuro.transform.position = posWill + Vector3.up * 0.05f;
            if (!_vfxCirculoConjuro.activeSelf)
                _vfxCirculoConjuro.SetActive(true);
        }

        if (_vfxAuraConjuroWill != null)
        {
            LimpiarSubemisoresDeFlechasEnVfx(_vfxAuraConjuroWill);
            _vfxAuraConjuroWill.transform.position = posWill + Vector3.up * 0.1f;
            if (!_vfxAuraConjuroWill.activeSelf)
                _vfxAuraConjuroWill.SetActive(true);
        }

        CrearAnillosRunicosAnime(posWill);
        CrearPulsoLuzTemporal(posWill + Vector3.up * 1.2f, new Color(1.0f, 0.85f, 0.35f), 7f, 14f, 4.5f);
        FeedbackService.CameraShake(0.3f, 0.45f);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Co_ConjuroResurreccionCarga()
    {
        var will = ResolverActor("will", _willActor);
        var liam = ResolverActor("liam", _liamActor);
        Vector3 posWill = will != null ? will.position : new Vector3(0f, 0f, 4.5f);
        Vector3 posLiam = liam != null ? liam.position : new Vector3(-1.2f, 0f, 4.5f);

        if (_vfxResurreccion != null)
        {
            LimpiarSubemisoresDeFlechasEnVfx(_vfxResurreccion);
            _vfxResurreccion.transform.position = posLiam + Vector3.up * 0.15f;
            if (!_vfxResurreccion.activeSelf)
                _vfxResurreccion.SetActive(true);
        }

        if (_corutinaHacesLuz != null)
            StopCoroutine(_corutinaHacesLuz);
        _corutinaHacesLuz = StartCoroutine(Co_LazosDeLuzWillALiam(posWill + Vector3.up * 1.15f, posLiam + Vector3.up * 0.65f, 4.2f));

        FeedbackService.CameraShake(0.45f, 0.7f);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Co_ConjuroResurreccionClimax()
    {
        var will = ResolverActor("will", _willActor);
        var liam = ResolverActor("liam", _liamActor);

        Vector3 posWill = will != null ? will.position : new Vector3(0f, 0f, 4.2f);
        Vector3 posLiam = liam != null ? liam.position : new Vector3(-1.2f, 0f, 4.2f);
        Vector3 posPortal = _portalUmbralTransform != null ? _portalUmbralTransform.position : new Vector3(0f, 0f, 8.0f);

        if (_vfxPilarResurreccion != null)
        {
            _vfxPilarResurreccion.transform.position = posLiam;
            if (!_vfxPilarResurreccion.activeSelf)
                _vfxPilarResurreccion.SetActive(true);
        }

        CrearPulsoLuzTemporal(posWill + Vector3.up * 1.5f, new Color(1.0f, 0.95f, 0.65f), 14f, 24f, 2.2f);
        FeedbackService.CameraShake(0.85f, 1.2f);

        if (liam != null)
        {
            Vector3 origen = liam.position;
            Vector3 destino = new Vector3(posPortal.x, origen.y + 0.6f, posPortal.z);
            float dur = 0.85f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                liam.position = Vector3.Lerp(origen, destino, k);
                yield return null;
            }
            var renderersLiam = liam.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderersLiam.Length; i++)
                if (renderersLiam[i] != null) renderersLiam[i].enabled = false;
        }

        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator Co_ColapsoSendero()
    {
        var will = ResolverActor("will", _willActor);
        Vector3 posWill = will != null ? will.position : new Vector3(0f, 0f, 4.2f);

        if (_vfxColapsoSendero != null)
        {
            // Colocar el vórtice detrás de Will para no tapar su rostro durante el primer plano del grito
            _vfxColapsoSendero.transform.position = posWill + new Vector3(0f, 1.2f, -3.5f);
            if (!_vfxColapsoSendero.activeSelf)
                _vfxColapsoSendero.SetActive(true);
        }

        FeedbackService.CameraShake(0.95f, 1.8f);
        yield return new WaitForSeconds(1.2f);
    }

    private IEnumerator Co_EspirituArchimago()
    {
        var arch = ResolverActor("archimago", _archimagoActor);
        if (arch != null)
        {
            Vector3 posAparicion = arch.position;
            if (posAparicion.y < 0.2f) posAparicion.y = 0.25f;
            arch.position = posAparicion;

            PrepararAspectoEspiritualArchimago(arch);

            if (!arch.gameObject.activeSelf)
                arch.gameObject.SetActive(true);

            AudioService.Instance?.PlaySFX("Prologue_ActorAppear", 0.9f);

            float tiempoLuz = 0.5f;
            float t = 0f;
            var goLuzCreciente = new GameObject("VFX_LuzCrecienteArchimago");
            goLuzCreciente.transform.position = posAparicion + Vector3.up * 0.9f;
            var lightComp = goLuzCreciente.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = new Color(1.0f, 0.94f, 0.72f);
            lightComp.range = 4f;
            lightComp.intensity = 0f;
            _instanciasTemporales.Add(goLuzCreciente);

            while (t < tiempoLuz)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / tiempoLuz);
                lightComp.intensity = k * 14f;
                lightComp.range = 4f + k * 10f;
                yield return null;
            }

            // Desactivar explícitamente cualquier VFX de flechas de buff sobre el Archimago
            if (_vfxEspirituArchimago != null && _vfxEspirituArchimago.activeSelf)
                _vfxEspirituArchimago.SetActive(false);

            FeedbackService.CameraShake(0.18f, 0.35f);

            float tFade = 0f;
            while (tFade < 0.3f)
            {
                tFade += Time.deltaTime;
                if (lightComp != null) lightComp.intensity = Mathf.Lerp(14f, 3.5f, tFade / 0.3f);
                yield return null;
            }
            if (goLuzCreciente != null)
            {
                _instanciasTemporales.Remove(goLuzCreciente);
                Destroy(goLuzCreciente);
            }

            if (_corutinaFlotacionEspiritu != null)
                StopCoroutine(_corutinaFlotacionEspiritu);
            _corutinaFlotacionEspiritu = StartCoroutine(Co_FlotacionEspiritu(arch));
        }
        yield return new WaitForSeconds(0.10f);
    }

    private void PrepararAspectoEspiritualArchimago(Transform arch)
    {
        if (arch == null) return;

        // Mantener los materiales opacos intactos (para no corromper el pelo ni las pestañas)
        // y añadir un resplandor cálido dorado suave por emisión e iluminación puntual.
        foreach (var rend in arch.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            rend.enabled = true;
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.88f, 0.55f) * 0.38f);
                }
            }
            rend.materials = mats;
        }

        var luzAura = arch.Find("Luz_Aura_Archimago");
        Light lightComp;
        if (luzAura == null)
        {
            var goLuz = new GameObject("Luz_Aura_Archimago");
            goLuz.transform.SetParent(arch, false);
            goLuz.transform.localPosition = new Vector3(0.15f, 1.35f, 0.45f);
            lightComp = goLuz.AddComponent<Light>();
        }
        else
        {
            lightComp = luzAura.GetComponent<Light>();
        }
        if (lightComp != null)
        {
            lightComp.type = LightType.Point;
            lightComp.color = new Color(1.0f, 0.92f, 0.68f);
            lightComp.range = 7.5f;
            lightComp.intensity = 2.8f;
            lightComp.enabled = true;
        }
    }

    private IEnumerator Co_FlotacionEspiritu(Transform arch)
    {
        if (arch == null) yield break;
        Vector3 basePos = arch.position;
        if (basePos.y < 0.2f) basePos.y = 0.25f;
        float tiempo = 0f;
        while (arch != null && arch.gameObject.activeInHierarchy)
        {
            tiempo += Time.deltaTime;
            float dy = Mathf.Sin(tiempo * 2.0f) * 0.08f;
            arch.position = new Vector3(basePos.x, basePos.y + dy, basePos.z);
            yield return null;
        }
    }

    private IEnumerator Co_DespedidaArchimago()
    {
        if (_corutinaFlotacionEspiritu != null)
        {
            StopCoroutine(_corutinaFlotacionEspiritu);
            _corutinaFlotacionEspiritu = null;
        }

        var arch = ResolverActor("archimago", _archimagoActor);
        float dur = 3.4f;
        float t = 0f;
        Vector3 inicio = arch != null ? arch.position : new Vector3(200f, 0.3f, 3.6f);

        CrearPulsoLuzTemporal(inicio + Vector3.up * 1.4f, new Color(1.0f, 0.95f, 0.75f), 14f, 22f, 3.4f);

        // Esfera luminosa de estrella fugaz que envuelve al Archimago mientras asciende al cielo nocturno
        var orbeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbeGo.name = "VFX_EstrellaAscensoArchimago";
        Destroy(orbeGo.GetComponent<Collider>());
        orbeGo.transform.position = inicio + Vector3.up * 1.0f;
        orbeGo.transform.localScale = Vector3.one * 0.15f;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var matOrbe = new Material(sh) { color = new Color(1.0f, 0.95f, 0.72f, 1.0f) };
        var mrOrbe = orbeGo.GetComponent<MeshRenderer>();
        if (mrOrbe != null)
        {
            mrOrbe.sharedMaterial = matOrbe;
            mrOrbe.shadowCastingMode = ShadowCastingMode.Off;
        }

        var tr = orbeGo.AddComponent<TrailRenderer>();
        tr.time = 1.3f;
        tr.startWidth = 0.65f;
        tr.endWidth = 0.02f;
        tr.sharedMaterial = new Material(sh) { color = new Color(1.0f, 0.92f, 0.62f, 0.95f) };
        _instanciasTemporales.Add(orbeGo);

        var renderers = arch != null ? arch.GetComponentsInChildren<Renderer>(true) : null;
        bool mallasOcultadas = false;

        AudioService.Instance?.PlaySFX("Hechizo_EstrellaFugaz", 1.0f);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float curvaSubida = p * p * (3f - 2f * p);
            float altura = (p < 0.25f)
                ? Mathf.Lerp(0f, 1.8f, p / 0.25f)
                : Mathf.Lerp(1.8f, 38f, Mathf.Pow((p - 0.25f) / 0.75f, 1.8f));

            if (orbeGo != null)
            {
                orbeGo.transform.position = inicio + new Vector3(0f, 1.0f + altura, -altura * 0.25f);
                float escalaOrbe = (p < 0.3f) ? Mathf.Lerp(0.15f, 0.75f, p / 0.3f) : Mathf.Lerp(0.75f, 0.35f, (p - 0.3f) / 0.7f);
                orbeGo.transform.localScale = Vector3.one * escalaOrbe;
            }

            if (arch != null)
            {
                arch.position = inicio + new Vector3(0f, altura, -altura * 0.25f);

                // Al convertirse en haz de luz (p > 0.28), ocultar el cuerpo humano para que suba como estrella pura
                if (p > 0.28f && !mallasOcultadas && renderers != null)
                {
                    mallasOcultadas = true;
                    for (int r = 0; r < renderers.Length; r++)
                        if (renderers[r] != null) renderers[r].enabled = false;
                }
            }
            yield return null;
        }

        if (orbeGo != null)
            CrearPulsoLuzTemporal(orbeGo.transform.position, new Color(1.0f, 0.98f, 0.85f), 16f, 32f, 1.0f);

        FeedbackService.CameraShake(0.15f, 0.3f);

        if (orbeGo != null)
        {
            _instanciasTemporales.Remove(orbeGo);
            Destroy(orbeGo, 1.2f);
        }

        if (arch != null && arch.gameObject.activeSelf)
            arch.gameObject.SetActive(false);
        if (_vfxEspirituArchimago != null && _vfxEspirituArchimago.activeSelf)
            _vfxEspirituArchimago.SetActive(false);
    }

    private IEnumerator Co_RegresoWill()
    {
        var will = ResolverActor("will", _willActor);
        if (will != null)
        {
            if (!will.gameObject.activeSelf)
                will.gameObject.SetActive(true);

            // Asegurar que el envoltorio de secuencia y los renderers de Will vuelvan a estar visibles
            var seqActor = _ctx?.GetActor("will") ?? _ctx?.GetActor("Player");
            if (seqActor != null)
            {
                seqActor.SetRenderingVisible(true);
            }

            foreach (var r in will.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.forceRenderingOff = false;
            }

            if (_cintaRojaWill != null && !_cintaRojaWill.activeSelf)
                _cintaRojaWill.SetActive(true);

            CrearPulsoLuzTemporal(will.position + Vector3.up * 1.1f, new Color(1.0f, 0.92f, 0.65f), 9f, 16f, 2.2f);
            AudioService.Instance?.PlaySFX("Prologue_ActorAppear", 0.85f);
            FeedbackService.CameraShake(0.18f, 0.35f);
        }
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Co_EncadenarParte2()
    {
        if (_ctx?.Player != null)
        {
            _ctx.Player.RequestEnd("FINAL_PARTE_02_START", SequenceEndScreen.QuedarseEnNegro);
        }
        yield break;
    }

    private IEnumerator Co_TarjetaSemanasDespues()
    {
        var bubble = SpeechBubbleUI.Instance;
        if (bubble != null)
        {
            string texto = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get("FINAL_SEMANAS_DESPUES", "Semanas después...")
                : "Semanas después...";

            bubble.Show(
                target: null,
                text: texto,
                duration: 2.6f,
                onComplete: null,
                animTrigger: null,
                emphasis: false,
                speakerName: string.Empty,
                worldOffset: null,
                fijoEnPantalla: true,
                presentacion: PresentacionDeTexto.SubtituloGrande,
                colorDelNombre: Color.clear
            );
        }
        yield return new WaitForSeconds(2.8f);
        if (bubble != null) bubble.Hide();
    }

    private IEnumerator Co_SaltoRioEstela()
    {
        var estela = ResolverActor("estela", _estelaActor);
        if (estela != null)
        {
            Vector3 startPos = estela.position;
            Vector3 targetPos = _orillaRioTransform != null
                ? _orillaRioTransform.position
                : new Vector3(397.8f, -0.25f, -2.8f);

            float dur = 0.65f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float height = Mathf.Sin(p * Mathf.PI) * 1.55f;
                estela.position = Vector3.Lerp(startPos, targetPos, p) + Vector3.up * height;
                yield return null;
            }

            if (_vfxSalpicaduraRio != null)
            {
                _vfxSalpicaduraRio.transform.position = targetPos + Vector3.up * 0.15f;
                if (!_vfxSalpicaduraRio.activeSelf)
                    _vfxSalpicaduraRio.SetActive(true);
                var ps = _vfxSalpicaduraRio.GetComponentInChildren<ParticleSystem>();
                if (ps != null) ps.Play(true);
            }
            AudioService.Instance?.PlaySFX("Agua_Zambullida", 1.0f);
            FeedbackService.CameraShake(0.45f, 0.35f);
        }
        yield return new WaitForSeconds(0.45f);
    }

    private IEnumerator Co_TransicionAtardecer()
    {
        if (_skyboxAtardecer != null)
        {
            RenderSettings.skybox = _skyboxAtardecer;
            ActualizarSkyboxCamaraPrincipal(_skyboxAtardecer);
        }

        RenderSettings.fogColor = new Color(0.85f, 0.52f, 0.36f);
        RenderSettings.ambientLight = new Color(0.55f, 0.42f, 0.38f);
        yield return Co_TransicionLuz(_colorSolAtardecer, 0.85f, 1.2f);
    }

    private IEnumerator Co_TransicionNoche()
    {
        if (_canaPescarWill != null && _canaPescarWill.activeSelf)
            _canaPescarWill.SetActive(false);

        if (_canaPescarTobias != null && _canaPescarTobias.activeSelf)
            _canaPescarTobias.SetActive(false);

        if (_cuadernoDibujoTobias != null && _cuadernoDibujoTobias.activeSelf)
            _cuadernoDibujoTobias.SetActive(false);

        if (_skyboxNoche != null)
        {
            RenderSettings.skybox = _skyboxNoche;
            ActualizarSkyboxCamaraPrincipal(_skyboxNoche);
        }

        if (_estrellasNocturnasRoot != null && !_estrellasNocturnasRoot.activeSelf)
            _estrellasNocturnasRoot.SetActive(true);

        if (_estrellaTorcidaObjeto != null && !_estrellaTorcidaObjeto.activeSelf)
            _estrellaTorcidaObjeto.SetActive(true);

        if (_luzNocheRioGrupo != null && !_luzNocheRioGrupo.gameObject.activeSelf)
            _luzNocheRioGrupo.gameObject.SetActive(true);

        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.24f, 0.30f, 0.48f);

        if (_luzSolar != null)
        {
            _luzSolar.color = _colorLuzNoche;
            _luzSolar.intensity = 0.55f;
            _luzSolar.transform.rotation = Quaternion.Euler(48f, 165f, 0f);
        }

        yield return new WaitForSeconds(0.15f);
    }

    private IEnumerator Co_TransicionLuz(Color targetColor, float targetIntensity, float duration)
    {
        if (_luzSolar == null) yield break;
        Color initialColor = _luzSolar.color;
        float initialIntensity = _luzSolar.intensity;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            _luzSolar.color = Color.Lerp(initialColor, targetColor, p);
            _luzSolar.intensity = Mathf.Lerp(initialIntensity, targetIntensity, p);
            yield return null;
        }
    }

    private IEnumerator Co_EnfocarEstrella()
    {
        if (_skyboxNoche != null)
        {
            RenderSettings.skybox = _skyboxNoche;
            ActualizarSkyboxCamaraPrincipal(_skyboxNoche);
        }

        if (_estrellasNocturnasRoot != null && !_estrellasNocturnasRoot.activeSelf)
            _estrellasNocturnasRoot.SetActive(true);

        if (_estrellaTorcidaObjeto != null)
        {
            if (!_estrellaTorcidaObjeto.activeSelf)
                _estrellaTorcidaObjeto.SetActive(true);

            if (_corutinaPulsoEstrella != null)
                StopCoroutine(_corutinaPulsoEstrella);
            _corutinaPulsoEstrella = StartCoroutine(Co_PulsarEstrellaTorcida(_estrellaTorcidaObjeto.transform));
        }

        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator Co_PulsarEstrellaTorcida(Transform estrella)
    {
        if (estrella == null) yield break;
        Vector3 escalaBase = estrella.localScale;
        float t = 0f;
        while (estrella != null && estrella.gameObject.activeInHierarchy)
        {
            t += Time.deltaTime;
            float pulso = 1f + Mathf.Sin(t * 3.2f) * 0.14f;
            estrella.localScale = escalaBase * pulso;
            yield return null;
        }
    }

    private IEnumerator Co_MostrarFraseFinalLibro()
    {
        var bubble = SpeechBubbleUI.Instance;
        if (bubble != null)
        {
            string textoFinal = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get("FINAL_FRASE_LIBRO", "Sobre sus cabezas, entre miles de estrellas, una brillaba un poco más que las demás.")
                : "Sobre sus cabezas, entre miles de estrellas, una brillaba un poco más que las demás.";

            bubble.Show(
                target: null,
                text: textoFinal,
                duration: 6.5f,
                onComplete: null,
                animTrigger: null,
                emphasis: false,
                speakerName: string.Empty,
                worldOffset: null,
                fijoEnPantalla: true,
                presentacion: PresentacionDeTexto.SubtituloGrande,
                colorDelNombre: Color.clear
            );
        }
        yield return new WaitForSeconds(6.8f);
        if (bubble != null) bubble.Hide();
    }

    private IEnumerator Co_IrACreditos()
    {
        yield return new WaitForSeconds(0.8f);
        if (Application.CanStreamedLevelBeLoaded("Credits"))
        {
            SceneManager.LoadScene("Credits");
        }
    }

    private void CrearPulsoLuzTemporal(Vector3 pos, Color color, float intensidadMax, float rango, float duracion)
    {
        var go = new GameObject("VFX_PulsoLuzTemporal");
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = rango;
        light.intensity = intensidadMax;
        _instanciasTemporales.Add(go);
        StartCoroutine(Co_AnimarPulsoLuz(go, light, intensidadMax, duracion));
    }

    private IEnumerator Co_AnimarPulsoLuz(GameObject go, Light light, float maxInt, float dur)
    {
        float t = 0f;
        while (t < dur && light != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            light.intensity = Mathf.Sin(p * Mathf.PI) * maxInt;
            yield return null;
        }
        if (go != null)
        {
            _instanciasTemporales.Remove(go);
            Destroy(go);
        }
    }

    private void CrearAnillosRunicosAnime(Vector3 centro)
    {
        var root = new GameObject("VFX_AnillosConjuroAnime90s");
        root.transform.position = centro + Vector3.up * 0.06f;
        _instanciasTemporales.Add(root);

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material matDorado = new Material(sh);
        matDorado.color = new Color(1.0f, 0.86f, 0.35f, 0.9f);

        Material matCeleste = new Material(sh);
        matCeleste.color = new Color(0.55f, 0.88f, 1.0f, 0.85f);

        CrearAnilloLinea(root.transform, "AnilloExterior", 2.6f, 48, 0.07f, matDorado);
        CrearAnilloLinea(root.transform, "AnilloInterior", 1.75f, 36, 0.05f, matCeleste);
        CrearEstrellaInscrita(root.transform, "HexagramaLuz", 2.45f, 0.045f, matDorado);

        StartCoroutine(Co_RotarAnillosAnime(root.transform, 14.5f));
    }

    private void CrearAnilloLinea(Transform parent, string nombre, float radio, int segmentos, float grosor, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = segmentos;
        lr.startWidth = grosor;
        lr.endWidth = grosor;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        for (int i = 0; i < segmentos; i++)
        {
            float ang = (i / (float)segmentos) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radio, 0f, Mathf.Sin(ang) * radio));
        }
    }

    private void CrearEstrellaInscrita(Transform parent, string nombre, float radio, float grosor, Material mat)
    {
        for (int t = 0; t < 2; t++)
        {
            var go = new GameObject($"{nombre}_{t}");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 3;
            lr.startWidth = grosor;
            lr.endWidth = grosor;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float desfase = t * (Mathf.PI / 3f);
            for (int i = 0; i < 3; i++)
            {
                float ang = desfase + i * (Mathf.PI * 2f / 3f);
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radio, 0.02f, Mathf.Sin(ang) * radio));
            }
        }
    }

    private IEnumerator Co_RotarAnillosAnime(Transform root, float duracion)
    {
        float t = 0f;
        while (t < duracion && root != null)
        {
            t += Time.deltaTime;
            root.Rotate(Vector3.up, 72f * Time.deltaTime, Space.Self);
            yield return null;
        }
    }

    private IEnumerator Co_LazosDeLuzWillALiam(Vector3 origen, Vector3 destino, float duracion)
    {
        var go = new GameObject("VFX_LazosVidaWillALiam");
        _instanciasTemporales.Add(go);

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(sh);
        mat.color = new Color(1.0f, 0.92f, 0.48f, 0.95f);

        LineRenderer[] cintas = new LineRenderer[3];
        for (int i = 0; i < cintas.Length; i++)
        {
            var child = new GameObject($"CintaLuz_{i}");
            child.transform.SetParent(go.transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 20;
            lr.startWidth = 0.09f;
            lr.endWidth = 0.04f;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            cintas[i] = lr;
        }

        float t = 0f;
        while (t < duracion && go != null)
        {
            t += Time.deltaTime;
            for (int c = 0; c < cintas.Length; c++)
            {
                var lr = cintas[c];
                if (lr == null) continue;
                float faseBase = t * 6.5f + c * (Mathf.PI * 2f / cintas.Length);
                for (int i = 0; i < 20; i++)
                {
                    float u = i / 19f;
                    Vector3 p = Vector3.Lerp(origen, destino, u);
                    float arco = Mathf.Sin(u * Mathf.PI) * (0.65f + 0.25f * c);
                    float espiralX = Mathf.Cos(faseBase + u * 5f) * 0.22f * Mathf.Sin(u * Mathf.PI);
                    float espiralZ = Mathf.Sin(faseBase + u * 5f) * 0.22f * Mathf.Sin(u * Mathf.PI);
                    p += new Vector3(espiralX, arco, espiralZ);
                    lr.SetPosition(i, p);
                }
            }
            yield return null;
        }

        if (go != null)
        {
            _instanciasTemporales.Remove(go);
            Destroy(go);
        }
        _corutinaHacesLuz = null;
    }
}
