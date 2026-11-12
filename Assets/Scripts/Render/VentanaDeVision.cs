using UnityEngine;

/// Una ventana en el mundo que enseña lo que ve otra cámara: una visión en una bola de cristal,
/// un recuerdo flotando, lo que pasa en otro sitio. La cámara solo dibuja mientras la ventana está
/// encendida, en una textura a baja resolución; la ventana se gira hacia la cámara que mira y se
/// abre y se cierra con un fundido. Si tiene una luz, la hace latir con la visión.
[DisallowMultipleComponent]
public class VentanaDeVision : MonoBehaviour
{
    [Tooltip("La cámara que ve lo que se enseña. Se apaga sola cuando la ventana no está encendida.")]
    [SerializeField] private Camera camaraDeLaVision;
    [Tooltip("La malla donde se pinta la visión (con un material que tenga _MainTex y _Aparicion).")]
    [SerializeField] private Renderer ventana;
    [Tooltip("Luz que acompaña a la visión (opcional): late mientras está abierta.")]
    [SerializeField] private Light luz;
    [SerializeField] private Vector2Int resolucion = new(512, 384);
    [Tooltip("Segundos que tarda en abrirse.")]
    [SerializeField, Min(0.05f)] private float tiempoDeApertura = 0.8f;
    [Tooltip("Girarse hacia la cámara que está mirando.")]
    [SerializeField] private bool mirarALaCamara = true;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int AparicionId = Shader.PropertyToID("_Aparicion");

    private RenderTexture _rt;
    private MaterialPropertyBlock _bloque;
    private float _apertura;
    private float _intensidadDeLaLuz;
    private Camera _camaraPrincipal;

    private void Awake()
    {
        _bloque = new MaterialPropertyBlock();
        if (luz != null) _intensidadDeLaLuz = luz.intensity;
        if (camaraDeLaVision != null) camaraDeLaVision.enabled = false;
    }

    private void OnEnable()
    {
        _apertura = 0f;
        if (camaraDeLaVision == null || ventana == null) return;
        _rt = new RenderTexture(resolucion.x, resolucion.y, 16) { name = name + " (visión)" };
        camaraDeLaVision.targetTexture = _rt;
        camaraDeLaVision.enabled = true;
        Pintar();
    }

    private void OnDisable()
    {
        if (camaraDeLaVision != null)
        {
            camaraDeLaVision.enabled = false;
            camaraDeLaVision.targetTexture = null;
        }
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (luz != null) luz.intensity = _intensidadDeLaLuz;
    }

    private void LateUpdate()
    {
        _apertura = Mathf.MoveTowards(_apertura, 1f, Time.unscaledDeltaTime / tiempoDeApertura);
        Pintar();

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
        if (_rt != null) _bloque.SetTexture(MainTexId, _rt);
        _bloque.SetFloat(AparicionId, Mathf.SmoothStep(0f, 1f, _apertura));
        ventana.SetPropertyBlock(_bloque);
    }
}
