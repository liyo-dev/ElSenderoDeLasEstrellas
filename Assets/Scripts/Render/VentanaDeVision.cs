using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// Una ventana en el mundo que enseña lo que pasa en otro sitio: una visión en una bola de cristal,
/// un recuerdo flotando, un portal. Lo que se ve sale, por orden de preferencia, de:
///   · una grabación (VideoClip) que se repite: algo que ya pasó, pero en movimiento;
///   · una cámara en vivo: lo que está pasando ahora en otro sitio;
///   · una imagen fija (también sirve de primer fotograma mientras la grabación se prepara).
/// La cámara y la grabación solo trabajan mientras la ventana está encendida, en una textura a baja
/// resolución; la ventana se gira hacia la cámara que mira y se abre y se cierra con un fundido. Si
/// tiene una luz, la hace latir con la visión.
/// La cámara en vivo encuadra la cara de alguien: el objetivo puesto o, si no hay, el personaje
/// (Animator humanoide) más cercano a donde está la cámara al encenderse. Se coloca delante de su
/// cara, de tres cuartos, y la sigue mientras la ventana está abierta. Ver INC-1342.
[DisallowMultipleComponent]
public class VentanaDeVision : MonoBehaviour
{
    [Tooltip("La cámara que ve lo que se enseña. Se apaga sola cuando la ventana no está encendida.")]
    [SerializeField] private Camera camaraDeLaVision;
    [Tooltip("Grabación que se repite en la ventana (algo que ya pasó, en movimiento). Si está puesta, manda sobre la cámara y la imagen.")]
    [SerializeField] private VideoClip grabacion;
    [Tooltip("Imagen fija que se enseña si no hay grabación ni cámara; con grabación, se ve mientras esta se prepara.")]
    [SerializeField] private Texture imagenFija;
    [Tooltip("La malla donde se pinta la visión (con un material que tenga _MainTex y _Aparicion).")]
    [SerializeField] private Renderer ventana;
    [Tooltip("Luz que acompaña a la visión (opcional): late mientras está abierta.")]
    [SerializeField] private Light luz;
    [Tooltip("Tamaño de la textura donde se dibuja la cámara o la grabación.")]
    [SerializeField] private Vector2Int resolucion = new(512, 384);
    [Tooltip("Segundos que tarda en abrirse.")]
    [SerializeField, Min(0.05f)] private float tiempoDeApertura = 0.8f;
    [Tooltip("Girarse hacia la cámara que está mirando.")]
    [SerializeField] private bool mirarALaCamara = true;
    [Tooltip("Tamaño de la ventana respecto al de la malla (1 = tal cual).")]
    [SerializeField, Min(0.1f)] private float tamanoDeLaVentana = 1.4f;

    [Header("Cámara en vivo: a quién encuadra")]
    [Tooltip("Personaje cuya cara encuadra la cámara. Vacío = el más cercano a la cámara al encenderse (ver radio).")]
    [SerializeField] private Transform objetivo;
    [Tooltip("Radio, alrededor de la cámara, en el que se busca al personaje si no hay objetivo. 0 = no buscar: la cámara se queda donde está.")]
    [SerializeField, Min(0f)] private float radioDeBusqueda = 3f;
    [Tooltip("Distancia de la cámara a la cara.")]
    [SerializeField, Min(0.2f)] private float distanciaALaCara = 1.15f;
    [Tooltip("Giro, en grados, desde el frente de la cara (0 = de frente; unos 25 = tres cuartos).")]
    [SerializeField, Range(-80f, 80f)] private float giroDeTresCuartos = 25f;
    [Tooltip("Altura de la cámara sobre los ojos (un poco por encima mira la cara sin picado fuerte).")]
    [SerializeField] private float alturaSobreLosOjos = 0.08f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int AparicionId = Shader.PropertyToID("_Aparicion");

    private RenderTexture _rt;
    private VideoPlayer _reproductor;
    private bool _grabacionLista;
    private MaterialPropertyBlock _bloque;
    private float _apertura;
    private float _intensidadDeLaLuz;
    private Camera _camaraPrincipal;
    private Vector3 _escalaDeLaMalla;
    private bool _escalaGuardada;
    private Transform _cara;
    private Transform _cuerpo;
    private Coroutine _busqueda;

    private void Awake()
    {
        _bloque = new MaterialPropertyBlock();
        if (luz != null) _intensidadDeLaLuz = luz.intensity;
        if (camaraDeLaVision != null) camaraDeLaVision.enabled = false;
    }

    private void OnEnable()
    {
        _apertura = 0f;
        _grabacionLista = false;
        if (ventana == null) return;
        if (!_escalaGuardada) { _escalaDeLaMalla = ventana.transform.localScale; _escalaGuardada = true; }
        ventana.transform.localScale = _escalaDeLaMalla * tamanoDeLaVentana;

        if (grabacion != null) EmpezarGrabacion();
        else if (camaraDeLaVision != null)
        {
            CrearTextura();
            camaraDeLaVision.targetTexture = _rt;
            camaraDeLaVision.enabled = true;
            if (objetivo != null) Fijar(objetivo);
            else if (radioDeBusqueda > 0f) _busqueda = StartCoroutine(BuscarAQuienEncuadrar());
        }
        Pintar();
    }

    private void OnDisable()
    {
        if (_busqueda != null) { StopCoroutine(_busqueda); _busqueda = null; }
        _cara = _cuerpo = null;
        if (ventana != null && _escalaGuardada) ventana.transform.localScale = _escalaDeLaMalla;
        if (camaraDeLaVision != null)
        {
            camaraDeLaVision.enabled = false;
            camaraDeLaVision.targetTexture = null;
        }
        if (_reproductor != null)
        {
            _reproductor.prepareCompleted -= AlPrepararse;
            _reproductor.Stop();
            _reproductor.targetTexture = null;
        }
        _grabacionLista = false;
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (luz != null) luz.intensity = _intensidadDeLaLuz;
    }

    /// El personaje puede aparecer un poco después que la ventana (lo coloca la cinemática): se
    /// busca unas cuantas veces, cada medio segundo, sin hacerlo en cada fotograma.
    private IEnumerator BuscarAQuienEncuadrar()
    {
        var espera = new WaitForSecondsRealtime(0.5f);
        Vector3 centro = camaraDeLaVision.transform.position;
        for (int intento = 0; intento < 10 && _cuerpo == null; intento++)
        {
            Animator mejor = null;
            float mejorDistancia = radioDeBusqueda * radioDeBusqueda;
            foreach (var a in FindObjectsByType<Animator>())
            {
                if (a == null || !a.isActiveAndEnabled || !a.isHuman || a.transform.IsChildOf(transform)) continue;
                float d = (a.transform.position - centro).sqrMagnitude;
                if (d < mejorDistancia) { mejorDistancia = d; mejor = a; }
            }
            if (mejor != null) Fijar(mejor.transform);
            else yield return espera;
        }
        _busqueda = null;
    }

    private void Fijar(Transform quien)
    {
        _cuerpo = quien;
        var animador = quien.GetComponentInChildren<Animator>();
        _cara = animador != null && animador.isHuman ? animador.GetBoneTransform(HumanBodyBones.Head) : null;
        if (_cara == null) _cara = quien;
    }

    /// Delante de la cara, girada a tres cuartos, mirando a los ojos.
    private void EncuadrarLaCara()
    {
        if (_cara == null || _cuerpo == null || camaraDeLaVision == null) return;
        Vector3 frente = _cuerpo.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
        frente = Quaternion.Euler(0f, giroDeTresCuartos, 0f) * frente.normalized;
        Vector3 ojos = _cara.position + Vector3.up * 0.05f;
        Vector3 desde = ojos + frente * distanciaALaCara + Vector3.up * alturaSobreLosOjos;
        camaraDeLaVision.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(ojos - desde, Vector3.up));
    }

    private void CrearTextura()
    {
        if (_rt != null) return;
        _rt = new RenderTexture(Mathf.Max(16, resolucion.x), Mathf.Max(16, resolucion.y), 16) { name = name + " (visión)" };
    }

    /// La grabación se dibuja en la misma textura, sin sonido y en bucle; hasta que tiene el primer
    /// fotograma se sigue viendo la imagen fija (o el fondo del material).
    private void EmpezarGrabacion()
    {
        CrearTextura();
        if (_reproductor == null)
        {
            _reproductor = gameObject.AddComponent<VideoPlayer>();
            _reproductor.playOnAwake = false;
            _reproductor.renderMode = VideoRenderMode.RenderTexture;
            _reproductor.audioOutputMode = VideoAudioOutputMode.None;
            _reproductor.isLooping = true;
            _reproductor.skipOnDrop = true;
            _reproductor.waitForFirstFrame = true;
        }
        _reproductor.source = VideoSource.VideoClip;
        _reproductor.clip = grabacion;
        _reproductor.targetTexture = _rt;
        _reproductor.prepareCompleted -= AlPrepararse;
        _reproductor.prepareCompleted += AlPrepararse;
        _reproductor.Prepare();
    }

    private void AlPrepararse(VideoPlayer reproductor)
    {
        if (!isActiveAndEnabled) return;
        _grabacionLista = true;
        reproductor.Play();
        Pintar();
    }

    private void LateUpdate()
    {
        _apertura = Mathf.MoveTowards(_apertura, 1f, Time.unscaledDeltaTime / tiempoDeApertura);
        Pintar();
        if (grabacion == null) EncuadrarLaCara();

        if (luz != null)
            luz.intensity = _intensidadDeLaLuz * _apertura * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.3f));

        if (!mirarALaCamara) return;
        if (_camaraPrincipal == null || !_camaraPrincipal.isActiveAndEnabled) _camaraPrincipal = Camera.main;
        if (_camaraPrincipal == null) return;
        Vector3 hacia = ventana.transform.position - _camaraPrincipal.transform.position;
        if (hacia.sqrMagnitude > 1e-4f) ventana.transform.rotation = Quaternion.LookRotation(hacia, Vector3.up);
    }

    private void Pintar()
    {
        if (ventana == null) return;
        ventana.GetPropertyBlock(_bloque);
        Texture lo = grabacion != null
            ? (_grabacionLista ? _rt : imagenFija)
            : camaraDeLaVision != null ? _rt : imagenFija;
        if (lo != null) _bloque.SetTexture(MainTexId, lo);
        _bloque.SetFloat(AparicionId, Mathf.SmoothStep(0f, 1f, _apertura));
        ventana.SetPropertyBlock(_bloque);
    }
}
