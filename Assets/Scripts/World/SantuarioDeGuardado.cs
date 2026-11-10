using UnityEngine;

/// El cristal del santuario de un punto de guardado: flota, gira despacio y late; al guardar la
/// partida da un pulso de luz. No toca el guardado: solo escucha <see cref="SavePoint.OnSaveCompleted"/>.
/// Solo se anima mientras se ve el cristal (o dura el pulso), para no gastar nada lejos o de espaldas.
/// Ver INC-1009.
[DisallowMultipleComponent]
public class SantuarioDeGuardado : MonoBehaviour
{
    [Header("Piezas")]
    [SerializeField] Transform cristal;
    [SerializeField] Renderer cristalRenderer;
    [SerializeField] Light luz;
    [Tooltip("El SavePoint de este santuario. Si se deja vacío se busca en el padre al encenderse.")]
    [SerializeField] SavePoint puntoDeGuardado;

    [Header("Movimiento del cristal")]
    [SerializeField, Min(0f)] float gradosPorSegundo = 24f;
    [Tooltip("Cuánto sube y baja el cristal, en metros del mundo.")]
    [SerializeField, Min(0f)] float alturaDeFlote = 0.07f;
    [SerializeField, Min(0.1f)] float periodoDeFlote = 3.4f;

    [Header("Luz")]
    [SerializeField, Min(0f)] float intensidadBase = 1.1f;
    [Tooltip("Cuánto late la luz sin guardar (fracción de la intensidad base).")]
    [SerializeField, Range(0f, 0.6f)] float latido = 0.18f;
    [SerializeField, Min(0f)] float intensidadDelPulso = 3.2f;
    [SerializeField, Min(0.1f)] float duracionDelPulso = 1.4f;
    [Tooltip("Cuánto crece el cristal en lo más alto del pulso (0,3 = un 30 %).")]
    [SerializeField, Range(0f, 1f)] float crecimientoDelPulso = 0.3f;

    Vector3 _posicionBase;
    Vector3 _escalaBase;
    bool _listo;
    float _fase;
    float _pulso; // 1 = recién guardado, baja a 0

    void Awake()
    {
        if (cristal == null) return;
        _posicionBase = cristal.localPosition;
        _escalaBase = cristal.localScale;
        _fase = Random.value * Mathf.PI * 2f; // dos santuarios a la vez no flotan sincronizados
        _listo = true;
    }

    void OnEnable()
    {
        if (puntoDeGuardado == null) puntoDeGuardado = GetComponentInParent<SavePoint>(true);
        if (puntoDeGuardado == null && transform.parent != null)
            puntoDeGuardado = transform.parent.GetComponentInChildren<SavePoint>(true);
        if (puntoDeGuardado != null) puntoDeGuardado.OnSaveCompleted += AlGuardar;
    }

    void OnDisable()
    {
        if (puntoDeGuardado != null) puntoDeGuardado.OnSaveCompleted -= AlGuardar;
        _pulso = 0f;
    }

    void AlGuardar() => _pulso = 1f;

    void Update()
    {
        if (!_listo) return;
        if (_pulso <= 0f && cristalRenderer != null && !cristalRenderer.isVisible) return;

        float dt = Time.deltaTime;
        _fase += dt * (Mathf.PI * 2f / periodoDeFlote);
        if (_pulso > 0f) _pulso = Mathf.Max(0f, _pulso - dt / duracionDelPulso);
        float p = _pulso * _pulso; // el pulso arranca fuerte y se apaga suave

        // La altura va en metros del mundo; se pasa al espacio del padre del cristal.
        var padre = cristal.parent;
        float escalaY = padre != null ? Mathf.Max(0.0001f, padre.lossyScale.y) : 1f;
        float subida = Mathf.Sin(_fase) * alturaDeFlote / escalaY;
        cristal.localPosition = _posicionBase + Vector3.up * subida;
        cristal.Rotate(0f, gradosPorSegundo * dt, 0f, Space.Self);
        cristal.localScale = _escalaBase * (1f + crecimientoDelPulso * p);

        if (luz != null)
            luz.intensity = intensidadBase * (1f + latido * Mathf.Sin(_fase * 1.7f)) + intensidadDelPulso * p;
    }
}
