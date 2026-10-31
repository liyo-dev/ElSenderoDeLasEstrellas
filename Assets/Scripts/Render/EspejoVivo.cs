using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Espejo plano con reflejo en tiempo real (personajes incluidos). Una cámara propia, colocada en
/// el lado opuesto del plano, dibuja la escena en una RenderTexture que el shader Sendero/Espejo
/// proyecta sobre la superficie. Para no pagar una pasada de escena por cada espejo, solo se
/// encienden los <see cref="MaximoActivos"/> espejos más cercanos que estén en pantalla y de
/// cara a la cámara; el resto muestra su versión de plata pulida (sonda de reflejos), igual que
/// cualquier espejo cuando se le ve dentro de otro reflejo.
///
/// La cámara del espejo no invierte la geometría: se coloca en la posición reflejada con un giro
/// normal y el shader lee la textura volteada en horizontal, así que no hace falta invertir el
/// culling. El plano de recorte oblicuo quita lo que queda detrás del espejo.
///
/// Con <see cref="VentanaA"/> puesto, el espejo deja de reflejar y hace de ventana a otro sitio
/// de la escena: la cámara se coloca respecto a ese punto igual que la del juego respecto al
/// espejo, así que lo de allí se ve con perspectiva y paralaje, como un recuerdo detrás del cristal.
///
/// Va en el objeto de la superficie (un Quad, que mira hacia -Z local). Pieza genérica del kit
/// de laberintos de espejos. Ver INC-870.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class EspejoVivo : MonoBehaviour
{
    /// <summary>Espejos con reflejo vivo a la vez, como máximo (sin contar los que se están apagando).</summary>
    public const int MaximoActivos = 2;

    /// <summary>Segundos del fundido entre plata y reflejo vivo, para que el cambio no salte.</summary>
    private const float DuracionFundido = 0.35f;

    /// <summary>Un espejo ya vivo cuenta su distancia multiplicada por esto: el que llega tiene que estar bastante más cerca para quitarle el sitio.</summary>
    private const float VentajaDelVivo = 0.75f;

    [Tooltip("Distancia máxima (m) desde la cámara para que el espejo tenga reflejo vivo.")]
    [SerializeField] private float distanciaMaxima = 30f;

    [Tooltip("Resolución de la textura del reflejo respecto a la pantalla (0,5 = la mitad).")]
    [Range(0.25f, 1f)]
    [SerializeField] private float escalaDeResolucion = 0.5f;

    [Tooltip("Separación del plano de recorte respecto a la superficie, para que no se vea a sí mismo.")]
    [SerializeField] private float desfaseDelPlano = 0.02f;

    [Tooltip("Capas que dibuja la cámara del espejo (se combinan con las de la cámara del juego).")]
    [SerializeField] private LayerMask capasReflejadas = ~0;

    [Tooltip("Vacío = espejo. Con un punto, el espejo es una ventana a lo que hay detrás de ese punto " +
             "(su -Z local hace de cara del espejo): se ve aquel sitio como si el espejo estuviera allí.")]
    [SerializeField] private Transform ventanaA;

    /// <summary>Sitio que se ve a través del espejo, o null si refleja. Se puede cambiar en marcha (p. ej. desde una secuencia).</summary>
    public Transform VentanaA
    {
        get => ventanaA;
        set { ventanaA = value; AplicarBloque(); }
    }

    private static readonly List<EspejoVivo> Registrados = new List<EspejoVivo>();
    private static readonly EspejoVivo[] Elegidos = new EspejoVivo[MaximoActivos];
    private static readonly float[] DistanciasElegidas = new float[MaximoActivos];
    private static readonly Plane[] PlanosDeCamara = new Plane[6];
    private static int _ultimoFrameDeEleccion = -1;
    private static Camera _camaraDelJuego;
    private static bool _suscrito;

    private static readonly int IdTextura = Shader.PropertyToID("_ReflejoTex");
    private static readonly int IdVivo = Shader.PropertyToID("_Vivo");
    private static readonly int IdVentana = Shader.PropertyToID("_Ventana");
    private static readonly int IdEnReflejo = Shader.PropertyToID("_EspejoEnReflejo");

    private Renderer _superficie;
    private MaterialPropertyBlock _bloque;
    private Camera _camaraEspejo;
    private RenderTexture _textura;
    private bool _elegido;
    private bool _listo;   // su cámara ya ha dibujado al menos una vez desde que se encendió
    private float _mezcla; // 0 = plata, 1 = reflejo vivo

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Registrados.Clear();
        _ultimoFrameDeEleccion = -1;
        _camaraDelJuego = null;
        _suscrito = false;
    }
#endif

    private void Awake()
    {
        _superficie = GetComponent<Renderer>();
        _bloque = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Registrados.Add(this);
        AplicarBloque();
        if (!_suscrito)
        {
            RenderPipelineManager.beginCameraRendering += AlEmpezarCamara;
            RenderPipelineManager.endCameraRendering += AlTerminarCamara;
            _suscrito = true;
        }
    }

    private void OnDisable()
    {
        Registrados.Remove(this);
        Apagar();
        if (Registrados.Count == 0 && _suscrito)
        {
            RenderPipelineManager.beginCameraRendering -= AlEmpezarCamara;
            RenderPipelineManager.endCameraRendering -= AlTerminarCamara;
            _suscrito = false;
            Shader.SetGlobalFloat(IdEnReflejo, 0f);
        }
    }

    private void OnDestroy()
    {
        LiberarTextura();
        if (_camaraEspejo != null) Destroy(_camaraEspejo.gameObject);
    }

    private void LateUpdate()
    {
        // El primer espejo que llega al LateUpdate de este frame elige por todos.
        if (_ultimoFrameDeEleccion == Time.frameCount) return;
        _ultimoFrameDeEleccion = Time.frameCount;
        ElegirEspejosVivos();
    }

    private static void ElegirEspejosVivos()
    {
        if (_camaraDelJuego == null || !_camaraDelJuego.isActiveAndEnabled) _camaraDelJuego = Camera.main;
        for (int i = 0; i < MaximoActivos; i++) { Elegidos[i] = null; DistanciasElegidas[i] = float.MaxValue; }

        Camera cam = _camaraDelJuego;
        if (cam != null)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, PlanosDeCamara);
            Vector3 posCam = cam.transform.position;
            for (int i = 0; i < Registrados.Count; i++)
            {
                EspejoVivo e = Registrados[i];
                Vector3 normal = e.Normal;
                Vector3 haciaCamara = posCam - e.transform.position;
                if (Vector3.Dot(haciaCamara, normal) <= 0f) continue;
                float d = haciaCamara.magnitude;
                if (d > e.distanciaMaxima) continue;
                if (!GeometryUtility.TestPlanesAABB(PlanosDeCamara, e._superficie.bounds)) continue;
                InsertarElegido(e, e._elegido ? d * VentajaDelVivo : d);
            }
        }

        for (int i = 0; i < Registrados.Count; i++)
        {
            EspejoVivo e = Registrados[i];
            bool elegido = false;
            for (int j = 0; j < MaximoActivos; j++) if (Elegidos[j] == e) { elegido = true; break; }
            e.Actualizar(elegido, cam);
        }
    }

    private static void InsertarElegido(EspejoVivo e, float d)
    {
        for (int i = 0; i < MaximoActivos; i++)
        {
            if (d >= DistanciasElegidas[i]) continue;
            for (int k = MaximoActivos - 1; k > i; k--)
            {
                Elegidos[k] = Elegidos[k - 1];
                DistanciasElegidas[k] = DistanciasElegidas[k - 1];
            }
            Elegidos[i] = e;
            DistanciasElegidas[i] = d;
            return;
        }
    }

    /// <summary>Normal del plano del espejo, hacia el lado desde el que se mira (un Quad mira a -Z local).</summary>
    private Vector3 Normal => -transform.forward;

    /// <summary>
    /// Lleva el espejo hacia vivo o hacia plata con un fundido. No empieza a mostrar el reflejo hasta
    /// que su cámara ha dibujado una vez (si no, se vería una textura vieja un frame) y mantiene la
    /// cámara encendida mientras se apaga.
    /// </summary>
    private void Actualizar(bool elegido, Camera camaraDelJuego)
    {
        if (camaraDelJuego == null) elegido = false;
        _elegido = elegido;
        if (elegido)
        {
            AsegurarCamara();
            AsegurarTextura(camaraDelJuego);
            _camaraEspejo.depth = camaraDelJuego.depth - 1f;
        }

        float objetivo = elegido && _listo ? 1f : 0f;
        float nueva = Mathf.MoveTowards(_mezcla, objetivo, Time.unscaledDeltaTime / DuracionFundido);

        bool necesitaCamara = elegido || nueva > 0f;
        if (_camaraEspejo != null && _camaraEspejo.enabled != necesitaCamara)
        {
            _camaraEspejo.enabled = necesitaCamara;
            if (!necesitaCamara) _listo = false;
        }

        if (nueva != _mezcla)
        {
            _mezcla = nueva;
            AplicarBloque();
        }
    }

    private void Apagar()
    {
        if (_camaraEspejo != null && _camaraEspejo.enabled) _camaraEspejo.enabled = false;
        _elegido = false;
        _listo = false;
        _mezcla = 0f;
        AplicarBloque();
    }

    private void AplicarBloque()
    {
        if (_superficie == null) return;
        _superficie.GetPropertyBlock(_bloque);
        _bloque.SetFloat(IdVivo, _mezcla);
        _bloque.SetFloat(IdVentana, ventanaA != null ? 1f : 0f);
        if (_textura != null) _bloque.SetTexture(IdTextura, _textura);
        _superficie.SetPropertyBlock(_bloque);
    }

    private void AsegurarCamara()
    {
        if (_camaraEspejo != null) return;
        var go = new GameObject("Cámara del espejo") { hideFlags = HideFlags.HideAndDontSave };
        _camaraEspejo = go.AddComponent<Camera>();
        _camaraEspejo.enabled = false;
        _camaraEspejo.allowMSAA = false;
        _camaraEspejo.allowHDR = true;
        var datos = go.AddComponent<UniversalAdditionalCameraData>();
        datos.renderPostProcessing = false;
        datos.renderShadows = false;
        datos.antialiasing = AntialiasingMode.None;
        datos.requiresColorOption = CameraOverrideOption.Off;
        datos.requiresDepthOption = CameraOverrideOption.Off;
    }

    private void AsegurarTextura(Camera camaraDelJuego)
    {
        int ancho = Mathf.Max(64, Mathf.RoundToInt(camaraDelJuego.pixelWidth * escalaDeResolucion));
        int alto = Mathf.Max(64, Mathf.RoundToInt(camaraDelJuego.pixelHeight * escalaDeResolucion));
        if (_textura != null && _textura.width == ancho && _textura.height == alto) return;

        LiberarTextura();
        _textura = new RenderTexture(ancho, alto, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "Reflejo de " + name,
            hideFlags = HideFlags.HideAndDontSave
        };
        _textura.Create();
        _camaraEspejo.targetTexture = _textura;
        _listo = false;
        _mezcla = 0f;
        AplicarBloque();
    }

    private void LiberarTextura()
    {
        if (_textura == null) return;
        if (_camaraEspejo != null) _camaraEspejo.targetTexture = null;
        _textura.Release();
        Destroy(_textura);
        _textura = null;
    }

    private static void AlEmpezarCamara(ScriptableRenderContext contexto, Camera camara)
    {
        for (int i = 0; i < Registrados.Count; i++)
        {
            EspejoVivo e = Registrados[i];
            if (e._camaraEspejo != camara) continue;
            e.ColocarCamara();
            Shader.SetGlobalFloat(IdEnReflejo, 1f);
            return;
        }
        // La vista de escena y las cámaras de previsualización ven la plata: el reflejo vivo
        // está calculado para la cámara del juego.
        Shader.SetGlobalFloat(IdEnReflejo, camara.cameraType == CameraType.Game ? 0f : 1f);
    }

    private static void AlTerminarCamara(ScriptableRenderContext contexto, Camera camara)
    {
        for (int i = 0; i < Registrados.Count; i++)
        {
            if (Registrados[i]._camaraEspejo != camara) continue;
            Registrados[i]._listo = true;
            break;
        }
        Shader.SetGlobalFloat(IdEnReflejo, 0f);
    }

    /// <summary>
    /// Pone la cámara del espejo, ya con la cámara del juego en su sitio definitivo de este frame:
    /// en la posición reflejada (espejo) o en la misma posición relativa respecto a la ventana.
    /// </summary>
    private void ColocarCamara()
    {
        Camera fuente = _camaraDelJuego;
        if (fuente == null) return;

        Transform t = fuente.transform;
        Vector3 n, p;
        if (ventanaA != null)
        {
            // Solo posición y giro: la escala del quad no cuenta (la ventana se ve a tamaño real).
            Quaternion haciaVentana = ventanaA.rotation * Quaternion.Inverse(transform.rotation);
            Vector3 pos = ventanaA.position + haciaVentana * (t.position - transform.position);
            _camaraEspejo.transform.SetPositionAndRotation(pos, haciaVentana * t.rotation);
            // Se recorta por el plano de la ventana: solo se ve lo que queda detrás de ella.
            n = ventanaA.forward;
            p = ventanaA.position;
        }
        else
        {
            n = Normal;
            p = transform.position;
            Vector3 pos = Reflejar(t.position - p, n) + p;
            Vector3 adelante = Reflejar(t.forward, n);
            Vector3 arriba = Reflejar(t.up, n);
            _camaraEspejo.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(adelante, arriba));
        }

        _camaraEspejo.fieldOfView = fuente.fieldOfView;
        _camaraEspejo.aspect = fuente.aspect;
        _camaraEspejo.nearClipPlane = fuente.nearClipPlane;
        _camaraEspejo.farClipPlane = fuente.farClipPlane;
        _camaraEspejo.cullingMask = fuente.cullingMask & capasReflejadas;
        _camaraEspejo.clearFlags = fuente.clearFlags;
        _camaraEspejo.backgroundColor = fuente.backgroundColor;

        Matrix4x4 proyeccion = fuente.projectionMatrix;
        _camaraEspejo.ResetProjectionMatrix();
        _camaraEspejo.ResetCullingMatrix();
        Matrix4x4 vista = _camaraEspejo.worldToCameraMatrix;

        Vector3 puntoPlano = vista.MultiplyPoint(p + n * desfaseDelPlano);
        Vector3 normalPlano = vista.MultiplyVector(n).normalized;
        var plano = new Vector4(normalPlano.x, normalPlano.y, normalPlano.z, -Vector3.Dot(puntoPlano, normalPlano));

        _camaraEspejo.projectionMatrix = fuente.CalculateObliqueMatrix(plano);
        _camaraEspejo.cullingMatrix = proyeccion * vista;
    }

    private static Vector3 Reflejar(Vector3 v, Vector3 n) => v - 2f * Vector3.Dot(v, n) * n;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, -transform.forward * 1.5f);
    }
}
