using System.Collections;
using UnityEngine;

/// <summary>
/// La ventana de un portal (shader «El Sendero/Ventana de portal» con vista panorámica): por el óvalo
/// se ve el otro mundo con perspectiva, como por un hueco. Gira la panorámica para que mirar a través
/// de la ventana sea mirar, allí, hacia donde se queda mirando quien cruza. Al encenderse durante el
/// juego se abre desde un punto de luz; al cargar la escena (y en el Editor) aparece ya abierta.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class VentanaDePortal : MonoBehaviour
{
    private static readonly int AperturaId = Shader.PropertyToID("_Apertura");
    private static readonly int DestelloId = Shader.PropertyToID("_Destello");
    private static readonly int GiroId = Shader.PropertyToID("_GiroDeLaVista");

    [Tooltip("Dirección, en el mundo del otro lado, hacia la que mira quien acaba de cruzar. Lo que se ve por la ventana es esa vista.")]
    [SerializeField] private Vector3 miradaAlOtroLado = Vector3.forward;

    [Tooltip("Segundos que tarda en abrirse cuando se enciende durante el juego.")]
    [SerializeField] private float segundosDeApertura = 1.8f;

    [Tooltip("Luz que acompaña a la ventana; su intensidad sigue a la apertura.")]
    [SerializeField] private Light luz;

    [Tooltip("Segundos desde que carga la escena durante los que la ventana aparece ya abierta, sin animación.")]
    [SerializeField] private float margenAlCargar = 1.5f;

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _bloque;
    private Coroutine _apertura;
    private float _intensidadDeLaLuz;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _bloque = new MaterialPropertyBlock();
        if (luz != null) _intensidadDeLaLuz = luz.intensity;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || Time.timeSinceLevelLoad < margenAlCargar) { Fijar(1f, 0f); return; }
        if (_apertura == null) _apertura = StartCoroutine(Abrir());
    }

    private void OnDisable()
    {
        if (_apertura != null) { StopCoroutine(_apertura); _apertura = null; }
    }

    /// Nace como un punto de luz que se ensancha con un pequeño rebote; el destello se apaga a la vez.
    private IEnumerator Abrir()
    {
        float duracion = Mathf.Max(0.05f, segundosDeApertura);
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = t / duracion;
            Fijar(RebotarAlFinal(k), 1f - Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        Fijar(1f, 0f);
        _apertura = null;
    }

    private static float RebotarAlFinal(float k)
    {
        const float c = 1.4f;
        float x = k - 1f;
        return 1f + (c + 1f) * x * x * x + c * x * x;
    }

    private void Fijar(float apertura, float destello)
    {
        if (_renderer == null) Awake();
        _renderer.GetPropertyBlock(_bloque);
        _bloque.SetFloat(AperturaId, Mathf.Max(0f, apertura));
        _bloque.SetFloat(DestelloId, destello);
        _bloque.SetMatrix(GiroId, Matrix4x4.Rotate(GiroDeLaVista()));
        _renderer.SetPropertyBlock(_bloque);
        if (luz != null && Application.isPlaying) luz.intensity = _intensidadDeLaLuz * Mathf.Clamp01(apertura) * (1f + destello);
    }

    /// De la mirada aquí (entrando por la ventana, en el sentido de su forward) a la mirada allí.
    private Quaternion GiroDeLaVista()
    {
        Vector3 aqui = transform.forward; aqui.y = 0f;
        Vector3 alla = miradaAlOtroLado; alla.y = 0f;
        if (aqui.sqrMagnitude < 1e-4f || alla.sqrMagnitude < 1e-4f) return Quaternion.identity;
        return Quaternion.LookRotation(alla.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(aqui.normalized, Vector3.up));
    }
}
