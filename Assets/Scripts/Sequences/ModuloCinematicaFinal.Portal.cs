using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sendero.Core.Feedback;

/// Acto 1: el portal entre la Piedra Ancestral (Estela, de noche) y el Sendero que se deshace
/// (Will). Cada ventana del portal enseña lo que vería una cámara al otro lado, colocada igual que
/// la que rueda respecto a su ventana: se ve el otro mundo con su perspectiva, como por un hueco.
public partial class ModuloCinematicaFinal
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int AperturaId = Shader.PropertyToID("_Apertura");
    private static readonly int DestelloId = Shader.PropertyToID("_Destello");
    private static readonly int EnPantallaId = Shader.PropertyToID("_EnPantalla");
    private static readonly int ColorLunaId = Shader.PropertyToID("_CieloColorDeLaLuna");
    private static readonly int ColorSolId = Shader.PropertyToID("_CieloColorDelSol");

    private bool _portalAbierto;
    private bool _ventanasEnPantalla;
    private RenderTexture _rtPortalSendero;
    private RenderTexture _rtPortalPiedra;
    private Material _matVentanaSendero;
    private Material _matVentanaPiedra;
    private readonly List<Renderer> _renderersSendero = new();
    private readonly List<Renderer> _renderersPiedra = new();
    private bool _listasDeZonaHechas;
    private Color _lunaDeEsteFotograma;
    private Color _solDeEsteFotograma;

    // ── Abrir y cerrar ───────────────────────────────────────────────────────────────────────

    private void AbrirPortal()
    {
        _portalAbierto = true;
        ResolverZonasSiFaltan();
        MostrarZona(_zonaSenderoRoot, true);
        MostrarZona(_zonaPiedraRoot, true);
        HacerListasDeZona();
        PrepararVentanas();

        if (_portalUmbralSendero != null) _portalUmbralSendero.SetActive(true);
        if (_portalUmbralPiedra != null) _portalUmbralPiedra.SetActive(true);
        if (_ventanaPortalSendero != null) _ventanaPortalSendero.SetActive(true);
        if (_ventanaPortalPiedra != null) _ventanaPortalPiedra.SetActive(true);
        if (_camPortalSendero != null) _camPortalSendero.enabled = true;
        if (_camPortalPiedra != null) _camPortalPiedra.enabled = true;
        FijarVentanas(1f, 0f);
    }

    /// El portal se cierra de golpe: las ventanas se apagan y cada cámara vuelve a ver solo su zona.
    private void CerrarPortal()
    {
        _portalAbierto = false;
        if (_camPortalSendero != null) _camPortalSendero.enabled = false;
        if (_camPortalPiedra != null) _camPortalPiedra.enabled = false;
        if (_ventanaPortalSendero != null) _ventanaPortalSendero.SetActive(false);
        if (_ventanaPortalPiedra != null) _ventanaPortalPiedra.SetActive(false);
        if (_portalUmbralPiedra != null) _portalUmbralPiedra.SetActive(false);
        MostrarZonasEnTodasLasCamaras();
        // La explosión se ha llevado el arco del Sendero.
        if (_portalUmbralTransform != null)
            foreach (var r in _portalUmbralTransform.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    private void LimpiarPortal()
    {
        if (_rtPortalSendero != null) { _rtPortalSendero.Release(); Destroy(_rtPortalSendero); _rtPortalSendero = null; }
        if (_rtPortalPiedra != null) { _rtPortalPiedra.Release(); Destroy(_rtPortalPiedra); _rtPortalPiedra = null; }
        if (_matVentanaSendero != null) { Destroy(_matVentanaSendero); _matVentanaSendero = null; }
        if (_matVentanaPiedra != null) { Destroy(_matVentanaPiedra); _matVentanaPiedra = null; }
        if (_camPortalSendero != null) { _camPortalSendero.enabled = false; _camPortalSendero.ResetProjectionMatrix(); }
        if (_camPortalPiedra != null) { _camPortalPiedra.enabled = false; _camPortalPiedra.ResetProjectionMatrix(); }
    }

    private void PrepararVentanas()
    {
        int ancho = Mathf.Max(640, Screen.width), alto = Mathf.Max(360, Screen.height);
        _rtPortalSendero = AsegurarRT(_rtPortalSendero, "RT_Portal_Sendero", ancho, alto);
        _rtPortalPiedra = AsegurarRT(_rtPortalPiedra, "RT_Portal_Piedra", ancho, alto);

        var shader = _shaderVentanaDePortal != null ? _shaderVentanaDePortal : Shader.Find("El Sendero/Ventana de portal");
        _ventanasEnPantalla = shader != null && shader.isSupported;
        if (!_ventanasEnPantalla) shader = Shader.Find("Universal Render Pipeline/Unlit");
        _matVentanaSendero = AsegurarMaterialDeVentana(_matVentanaSendero, shader, _rtPortalSendero, _ventanaPortalSendero);
        _matVentanaPiedra = AsegurarMaterialDeVentana(_matVentanaPiedra, shader, _rtPortalPiedra, _ventanaPortalPiedra);

        PrepararCamaraDePortal(_camPortalSendero, _rtPortalSendero, _skyboxSendero);
        PrepararCamaraDePortal(_camPortalPiedra, _rtPortalPiedra, _skyboxNoche);
    }

    private static RenderTexture AsegurarRT(RenderTexture rt, string nombre, int ancho, int alto)
    {
        if (rt != null && rt.width == ancho && rt.height == alto) return rt;
        if (rt != null) { rt.Release(); Destroy(rt); }
        rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.DefaultHDR) { name = nombre, filterMode = FilterMode.Bilinear };
        rt.Create();
        return rt;
    }

    private Material AsegurarMaterialDeVentana(Material mat, Shader shader, RenderTexture rt, GameObject ventana)
    {
        if (ventana == null || shader == null) return mat;
        if (mat == null) mat = new Material(shader) { name = "Ventana del portal" };
        mat.SetTexture(BaseMapId, rt);
        if (_ventanasEnPantalla) mat.SetFloat(EnPantallaId, 1f);
        else { mat.mainTexture = rt; mat.SetFloat("_Cull", 0f); }
        var mr = ventana.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        return mat;
    }

    private void PrepararCamaraDePortal(Camera cam, RenderTexture rt, Material cielo)
    {
        if (cam == null) return;
        cam.targetTexture = rt;
        cam.depth = -10f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var sky = cam.GetComponent<Skybox>();
        if (sky == null) sky = cam.gameObject.AddComponent<Skybox>();
        if (cielo != null) sky.material = cielo;
    }

    private void FijarVentanas(float apertura, float destello)
    {
        foreach (var m in new[] { _matVentanaSendero, _matVentanaPiedra })
        {
            if (m == null) continue;
            m.SetFloat(AperturaId, apertura);
            m.SetFloat(DestelloId, destello);
        }
    }

    // ── Cámaras del portal ───────────────────────────────────────────────────────────────────

    private static int ZonaDe(Vector3 p) => p.x < 100f ? 1 : (p.x < 300f ? 2 : 3);

    /// Antes de dibujar el fotograma: coloca la cámara del otro lado del portal que se está viendo.
    private void AntesDeRenderizar(ScriptableRenderContext contexto, List<Camera> camaras)
    {
        _lunaDeEsteFotograma = Shader.GetGlobalColor(ColorLunaId);
        _solDeEsteFotograma = Shader.GetGlobalColor(ColorSolId);
        if (!_portalAbierto || !_ventanasEnPantalla) return;
        var cam = CamaraPrincipal();
        if (cam == null || _ventanaPortalSendero == null || _ventanaPortalPiedra == null) return;

        int zona = ZonaDe(cam.transform.position);
        if (zona == 2)
        {
            // Desde la Piedra se mira por la ventana de la Piedra y se ve el Sendero.
            ColocarCamaraVirtual(cam, _ventanaPortalSendero.transform, _ventanaPortalPiedra.transform,
                PuntoDelGuion("p_will_umbral", new Vector3(0f, 0f, 4.6f)), _camPortalSendero);
            if (_camPortalSendero != null) _camPortalSendero.enabled = true;
            if (_camPortalPiedra != null) _camPortalPiedra.enabled = false;
        }
        else if (zona == 1)
        {
            ColocarCamaraVirtual(cam, _ventanaPortalPiedra.transform, _ventanaPortalSendero.transform,
                PuntoDelGuion("p_piedra_estela", new Vector3(200f, 0f, 4.8f)), _camPortalPiedra);
            if (_camPortalPiedra != null) _camPortalPiedra.enabled = true;
            if (_camPortalSendero != null) _camPortalSendero.enabled = false;
        }
    }

    /// <paramref name="ventanaAqui"/>: la que ve la cámara que rueda. <paramref name="ventanaAlla"/>:
    /// la del otro mundo; <paramref name="escenaAlla"/>, un punto del lado donde pasa la escena allí.
    private static void ColocarCamaraVirtual(Camera principal, Transform ventanaAqui, Transform ventanaAlla, Vector3 escenaAlla, Camera virtualCam)
    {
        if (virtualCam == null) return;
        Vector3 fA = ventanaAqui.forward, fB = ventanaAlla.forward;
        fA.y = 0f; fB.y = 0f;
        if (fA.sqrMagnitude < 1e-4f || fB.sqrMagnitude < 1e-4f) return;
        fA.Normalize(); fB.Normalize();
        Vector3 nA = Vector3.Dot(principal.transform.position - ventanaAqui.position, fA) >= 0f ? fA : -fA;
        Vector3 nB = Vector3.Dot(escenaAlla - ventanaAlla.position, fB) >= 0f ? fB : -fB;

        Quaternion giro = Quaternion.LookRotation(-nB, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(nA, Vector3.up));
        virtualCam.transform.SetPositionAndRotation(
            ventanaAlla.position + giro * (principal.transform.position - ventanaAqui.position),
            giro * principal.transform.rotation);
        virtualCam.fieldOfView = principal.fieldOfView;
        virtualCam.aspect = principal.aspect;
        virtualCam.nearClipPlane = principal.nearClipPlane;
        virtualCam.farClipPlane = principal.farClipPlane;
        virtualCam.ResetProjectionMatrix();

        // Lo que queda entre la cámara virtual y el portal (el arco, el suelo de detrás y la cara
        // blanca del propio arco, que asoma unos centímetros) no se ve: el plano cercano se inclina
        // hasta coincidir con la ventana, un palmo por delante.
        Vector3 puntoDelPlano = ventanaAlla.position + nB * 0.3f;
        float distancia = Vector3.Dot(puntoDelPlano - virtualCam.transform.position, nB);
        if (distancia > 0.05f)
        {
            Matrix4x4 m = virtualCam.worldToCameraMatrix;
            Vector3 p = m.MultiplyPoint(puntoDelPlano);
            Vector3 n = m.MultiplyVector(nB).normalized;
            virtualCam.projectionMatrix = virtualCam.CalculateObliqueMatrix(new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n)));
        }
    }

    /// Mientras el portal está abierto se ven las dos zonas, pero cada cámara solo la suya: la que
    /// rueda en la Piedra no tiene que ver el Sendero flotando a 200 m, ni al revés. Los astros
    /// también son de cada mundo: en el Sendero no hay luna.
    private void AntesDeRenderizarCamara(ScriptableRenderContext contexto, Camera cam)
    {
        if (cam == null || cam.cameraType != CameraType.Game) return;
        bool enElSendero = ZonaDe(cam.transform.position) == 1;
        Shader.SetGlobalColor(ColorLunaId, enElSendero ? Color.clear : _lunaDeEsteFotograma);
        Shader.SetGlobalColor(ColorSolId, enElSendero ? Color.clear : _solDeEsteFotograma);
        if (!_portalAbierto) return;
        FijarVisibilidad(_renderersSendero, enElSendero);
        FijarVisibilidad(_renderersPiedra, !enElSendero);
    }

    private static void FijarVisibilidad(List<Renderer> lista, bool visible)
    {
        for (int i = 0; i < lista.Count; i++)
            if (lista[i] != null) lista[i].forceRenderingOff = !visible;
    }

    private void MostrarZonasEnTodasLasCamaras()
    {
        FijarVisibilidad(_renderersSendero, true);
        FijarVisibilidad(_renderersPiedra, true);
    }

    private void HacerListasDeZona()
    {
        if (_listasDeZonaHechas) return;
        _listasDeZonaHechas = true;
        LlenarLista(_zonaSenderoRoot, _renderersSendero);
        LlenarLista(_zonaPiedraRoot, _renderersPiedra);
    }

    private void LlenarLista(GameObject zona, List<Renderer> lista)
    {
        if (zona == null) return;
        foreach (var r in zona.GetComponentsInChildren<Renderer>(true))
            if (r != null && !EsDeUnActor(r.transform) && !EsDeLasEstrellasNocturnas(r.transform)) lista.Add(r);
    }

    private bool EsDeLosPortales(Transform t)
    {
        return (_ventanaPortalSendero != null && t.IsChildOf(_ventanaPortalSendero.transform))
            || (_ventanaPortalPiedra != null && t.IsChildOf(_ventanaPortalPiedra.transform));
    }

    // ── El Sendero se deshace ────────────────────────────────────────────────────────────────

    private bool _colapsoActivo;
    private float _intensidadDelColapso = 1f;
    private Vector3 _centroQueAguanta = new(0f, 0f, 5.6f);
    private float _radioQueAguanta = 22f;
    private float _radioObjetivo = 4.8f;
    private float _velocidadDelRadio = 0.65f;
    private Renderer _rendererDelSuelo;
    private bool _sueloPartido;
    private readonly List<Transform> _losas = new();
    private readonly List<float> _ruidoDeLosas = new();
    private readonly List<float> _caidaDeLosas = new();
    private readonly List<Vector3> _giroDeLosas = new();
    private readonly List<Vector3> _origenDeLosas = new();
    private readonly List<Transform> _estrellasDelDomo = new();

    private IEnumerator Co_ColapsoDelSendero()
    {
        if (_colapsoActivo) yield break;
        _colapsoActivo = true;
        PartirElSuelo();
        RecogerEstrellasDelDomo();

        float siguienteEstrella = 0.4f, siguienteMeteoro = 1.2f, siguienteTemblor = 0.8f, siguienteTrueno = 2.5f;
        float t = 0f;
        while (_colapsoActivo)
        {
            float dt = Time.deltaTime;
            t += dt;
            _radioQueAguanta = Mathf.MoveTowards(_radioQueAguanta, _radioObjetivo, _velocidadDelRadio * _intensidadDelColapso * dt);
            ActualizarLosas(dt);

            bool camaraEnElSendero = CamaraPrincipal() != null && ZonaDe(CamaraPrincipal().transform.position) == 1;
            bool seVeElSendero = camaraEnElSendero || _portalAbierto;
            if (t >= siguienteEstrella)
            {
                if (seVeElSendero) EstallarUnaEstrella();
                siguienteEstrella = t + Random.Range(0.3f, 0.8f) / _intensidadDelColapso;
            }
            if (t >= siguienteMeteoro)
            {
                if (seVeElSendero) LanzarMeteoro();
                siguienteMeteoro = t + Random.Range(0.6f, 1.6f) / _intensidadDelColapso;
            }
            if (t >= siguienteTemblor)
            {
                if (camaraEnElSendero) FeedbackService.CameraShake(0.06f * _intensidadDelColapso, 0.45f);
                siguienteTemblor = t + Random.Range(1.4f, 2.8f);
            }
            if (t >= siguienteTrueno)
            {
                if (camaraEnElSendero) AudioService.Instance?.PlaySFX("SFX_Prologo_Trueno_Lejano", 0.22f * _intensidadDelColapso);
                siguienteTrueno = t + Random.Range(3f, 6f);
            }
            yield return null;
        }
    }

    /// El suelo del Sendero pasa a ser losas que pueden caer una a una al vacío. Su collider se
    /// queda: Will nunca cae, solo se ve cada vez menos camino.
    private void PartirElSuelo()
    {
        if (_sueloPartido) return;
        var suelo = GameObject.Find("Suelo_Astral_Sendero");
        if (suelo == null) return;
        _rendererDelSuelo = suelo.GetComponent<MeshRenderer>();
        if (_rendererDelSuelo == null) return;
        _sueloPartido = true;

        Bounds b = _rendererDelSuelo.bounds;
        var material = _rendererDelSuelo.sharedMaterial;
        int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / 1.1f));
        int nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / 1.1f));
        float sx = b.size.x / nx, sz = b.size.z / nz;
        var raiz = new GameObject("Losas_Del_Sendero").transform;
        raiz.SetParent(suelo.transform.parent, true);
        _instanciasTemporales.Add(raiz.gameObject);

        var molde = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var malla = molde.GetComponent<MeshFilter>().sharedMesh;
        Destroy(molde);

        var estado = Random.state;
        Random.InitState(824);
        for (int i = 0; i < nx; i++)
        for (int k = 0; k < nz; k++)
        {
            var losa = new GameObject("Losa");
            losa.transform.SetParent(raiz, false);
            losa.transform.position = new Vector3(b.min.x + (i + 0.5f) * sx, b.center.y, b.min.z + (k + 0.5f) * sz);
            losa.transform.localScale = new Vector3(sx * 1.002f, b.size.y, sz * 1.002f);
            losa.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = losa.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            _losas.Add(losa.transform);
            _ruidoDeLosas.Add(Random.Range(0f, 1.8f));
            _caidaDeLosas.Add(-1f);
            _giroDeLosas.Add(new Vector3(Random.Range(-90f, 90f), Random.Range(-60f, 60f), Random.Range(-90f, 90f)));
            _origenDeLosas.Add(losa.transform.position);
            _renderersSendero.Add(mr);
        }
        Random.state = estado;
        _rendererDelSuelo.enabled = false;
        _renderersSendero.Remove(_rendererDelSuelo);
    }

    private void ActualizarLosas(float dt)
    {
        for (int i = 0; i < _losas.Count; i++)
        {
            var losa = _losas[i];
            if (losa == null || !losa.gameObject.activeSelf) continue;
            float c = _caidaDeLosas[i];
            if (c < 0f)
            {
                Vector3 o = _origenDeLosas[i];
                float d = new Vector2(o.x - _centroQueAguanta.x, o.z - _centroQueAguanta.z).magnitude + _ruidoDeLosas[i];
                if (d > _radioQueAguanta) _caidaDeLosas[i] = 0f;
                continue;
            }
            c += dt;
            _caidaDeLosas[i] = c;
            Vector3 origen = _origenDeLosas[i];
            if (c < 0.55f)
            {
                // Tiembla antes de soltarse.
                float a = 0.04f * (c / 0.55f);
                losa.position = origen + new Vector3(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a));
            }
            else
            {
                float tc = c - 0.55f;
                losa.position = origen + Vector3.down * (4.9f * tc * tc + 0.6f * tc);
                losa.rotation = Quaternion.Euler(_giroDeLosas[i] * tc);
                if (tc > 3.2f) losa.gameObject.SetActive(false);
            }
        }
    }

    private void RecogerEstrellasDelDomo()
    {
        if (_estrellasDelDomo.Count > 0) return;
        var campo = GameObject.Find("Campo_Estelar_Sendero");
        if (campo == null) return;
        foreach (Transform hijo in campo.transform) _estrellasDelDomo.Add(hijo);
    }

    private void EstallarUnaEstrella()
    {
        // Una estrella cercana que revienta...
        Vector3 dir = Random.onUnitSphere;
        dir.y = Mathf.Abs(dir.y) * 0.8f + 0.12f;
        dir.Normalize();
        float dist = Random.Range(16f, 42f);
        Vector3 p = CentroSendero + dir * dist;
        Efecto(_prefabEstallidoDeEstrella, p, Quaternion.identity, 3f, Random.Range(2.5f, 5f));
        PulsoDeLuz(p, Random.value < 0.5f ? new Color(0.75f, 0.85f, 1f) : new Color(1f, 0.85f, 0.6f), 5f, dist * 0.7f, 0.55f);

        // ...y una del fondo que se apaga para siempre.
        if (_estrellasDelDomo.Count == 0) return;
        int i = Random.Range(0, _estrellasDelDomo.Count);
        var estrella = _estrellasDelDomo[i];
        _estrellasDelDomo.RemoveAt(i);
        if (estrella != null) StartCoroutine(Co_EstrellaQueMuere(estrella));
    }

    private IEnumerator Co_EstrellaQueMuere(Transform estrella)
    {
        Vector3 escala = estrella.localScale;
        for (float t = 0f; t < 0.25f && estrella != null; t += Time.deltaTime)
        {
            estrella.localScale = escala * Mathf.Lerp(1f, 5f, t / 0.25f);
            yield return null;
        }
        if (estrella == null) yield break;
        Efecto(_prefabEstallidoDeEstrella, estrella.position, Quaternion.identity, 3f, 7f);
        estrella.gameObject.SetActive(false);
    }

    private void LanzarMeteoro()
    {
        float angulo = Random.Range(0f, Mathf.PI * 2f);
        float radio = Random.Range(18f, 40f);
        Vector3 desde = CentroSendero + new Vector3(Mathf.Cos(angulo) * radio, Random.Range(22f, 40f), Mathf.Sin(angulo) * radio);
        Vector3 hacia = new Vector3(-Mathf.Cos(angulo) * Random.Range(0.2f, 0.6f), -1f, -Mathf.Sin(angulo) * Random.Range(0.2f, 0.6f)).normalized;
        StartCoroutine(Co_Meteoro(desde, hacia * Random.Range(16f, 26f)));
    }

    private IEnumerator Co_Meteoro(Vector3 desde, Vector3 velocidad)
    {
        var go = new GameObject("Meteoro");
        go.transform.position = desde;
        _instanciasTemporales.Add(go);
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.6f;
        tr.widthMultiplier = 0.35f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.sharedMaterial = MaterialDeLuz(new Color(2.6f, 2.0f, 1.3f, 1f));
        tr.shadowCastingMode = ShadowCastingMode.Off;
        for (float t = 0f; t < 3f && go != null; t += Time.deltaTime)
        {
            go.transform.position += velocidad * Time.deltaTime;
            yield return null;
        }
        if (go != null)
        {
            _instanciasTemporales.Remove(go);
            Destroy(tr.sharedMaterial);
            Destroy(go);
        }
    }

    // ── La explosión y el portal que se cierra ───────────────────────────────────────────────

    private IEnumerator Co_ExplosionEnElPortal()
    {
        var will = ResolverActor("will", _willActor);
        Vector3 pw = will != null ? will.position : new Vector3(0f, 0f, 4.6f);
        Vector3 ventanaPiedra = _ventanaPortalSendero != null ? _ventanaPortalSendero.transform.position : new Vector3(200f, 1.65f, 0.45f);

        // Dentro: el Sendero revienta detrás de Will y se traga la luz del portal.
        Vector3 estallido = pw + new Vector3(0f, 1.0f, -2.2f);
        Efecto(_prefabExplosion, estallido, Quaternion.identity, 4f, 3.5f);
        Efecto(_prefabEstallidoDeEstrella, pw + Vector3.up * 1.3f, Quaternion.identity, 3f, 4f);
        PulsoDeLuz(estallido, new Color(1f, 0.82f, 0.55f), 40f, 30f, 1.3f);
        _intensidadDelColapso = 2.6f;
        _radioQueAguanta = Mathf.Max(_radioObjetivo, _radioQueAguanta - 6f);
        AudioService.Instance?.PlaySFX("SFX_Prologo_ExplosionFinal", 0.85f);

        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            FijarVentanas(1f, Mathf.SmoothStep(0f, 1f, t / 0.6f));
            yield return null;
        }

        // El portal se cierra sobre sí mismo.
        Efecto(_prefabImplosion, ventanaPiedra, Quaternion.identity, 3f, 2.2f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            FijarVentanas(1f - k * k, 1f);
            yield return null;
        }
        FijarVentanas(0f, 1f);

        // Cerrado: onda de choque y golpe de luz en la Piedra.
        Vector3 suelo = new Vector3(ventanaPiedra.x, 0.05f, ventanaPiedra.z);
        Efecto(_prefabOndaExpansiva, suelo, Quaternion.identity, 3f, 2.5f);
        Efecto(_prefabGolpeSagrado, ventanaPiedra, Quaternion.identity, 3f, 2f);
        PulsoDeLuz(ventanaPiedra, new Color(0.85f, 0.92f, 1f), 22f, 22f, 0.9f);
        FeedbackService.CameraShake(0.75f, 0.9f);
        AudioService.Instance?.PlaySFX("SFX_Prologo_ExplosionFinal", 1f);
        CerrarPortal();
        _intensidadDelColapso = 1.4f;
        _radioObjetivo = 4.0f;
    }

    private void EstelaAlcanzaElPortal()
    {
        var estela = ResolverActor("estela", _estelaActor);
        if (estela == null) return;
        Vector3 ventana = _ventanaPortalSendero != null ? _ventanaPortalSendero.transform.position : new Vector3(200f, 1.65f, 0.45f);
        Actor("estela")?.Face(ventana);
        GestosProcedurales.Alcanzar(estela, ventana + Vector3.up * 0.15f, true, 0.6f);
    }
}
