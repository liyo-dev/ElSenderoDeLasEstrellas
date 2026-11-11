using UnityEngine;

/// El farol de estrellas de un punto de guardado (INC-1041; sustituye al santuario de INC-1009).
///
/// · Apagado mientras el punto no está descubierto: la estrella de dentro es de piedra gris y no hay luz.
/// · Se enciende cuando el punto se desbloquea en <see cref="TeleportRegistry"/> (al llegar a él): la
///   estrella se vuelve amarilla, salen chispas y la luz sube poco a poco. Como ese registro ya se guarda
///   con la partida, el farol sigue encendido al volver a cargar.
/// · Encendido, la estrella flota y gira despacio y la luz late un poco; al guardar da un pulso de luz.
/// · Un punto sin id de teletransporte se queda siempre encendido.
///
/// No toca el guardado ni el teletransporte: solo escucha. Solo se anima mientras se ve la estrella.
[DisallowMultipleComponent]
public class FarolDeEstrellas : MonoBehaviour
{
    [Header("Piezas")]
    [SerializeField] Transform estrella;
    [SerializeField] Renderer estrellaRenderer;
    [SerializeField] Light luz;
    [SerializeField] Material materialEncendida;
    [SerializeField] Material materialApagada;
    [Tooltip("Chispas al encenderse por primera vez (se lanzan con VfxPoolService). Opcional.")]
    [SerializeField] GameObject vfxAlEncenderse;

    [Header("Movimiento de la estrella (encendida)")]
    [SerializeField, Min(0f)] float gradosPorSegundo = 30f;
    [Tooltip("Cuánto sube y baja la estrella, en metros del mundo.")]
    [SerializeField, Min(0f)] float alturaDeFlote = 0.04f;
    [SerializeField, Min(0.1f)] float periodoDeFlote = 3.2f;

    [Header("Luz")]
    [SerializeField, Min(0f)] float intensidadBase = 1.4f;
    [SerializeField, Range(0f, 0.6f)] float latido = 0.15f;
    [Tooltip("Segundos que tarda la luz en subir al encenderse.")]
    [SerializeField, Min(0.05f)] float duracionDelEncendido = 1.2f;
    [SerializeField, Min(0f)] float intensidadDelPulso = 3f;
    [SerializeField, Min(0.1f)] float duracionDelPulso = 1.4f;
    [Tooltip("Cuánto crece la estrella en lo más alto del pulso (0,3 = un 30 %).")]
    [SerializeField, Range(0f, 1f)] float crecimientoDelPulso = 0.3f;

    SavePoint _punto;
    SavePointTeleportTrigger _teletransporte;
    Vector3 _posicionBase;
    Vector3 _escalaBase;
    Quaternion _giroBase;
    bool _listo;
    bool _encendido;
    bool _estadoAplicado;
    float _fase;
    float _encendiendo = 1f; // 0 → 1 mientras sube la luz
    float _pulso;            // 1 = recién guardado, baja a 0

    void Awake()
    {
        if (estrella == null) return;
        _posicionBase = estrella.localPosition;
        _escalaBase = estrella.localScale;
        _giroBase = estrella.localRotation;
        _fase = Random.value * Mathf.PI * 2f; // dos faroles a la vez no flotan sincronizados
        _listo = true;
    }

    void OnEnable()
    {
        _punto = GetComponentInParent<SavePoint>(true);
        if (_punto == null && transform.parent != null) _punto = transform.parent.GetComponentInChildren<SavePoint>(true);
        _teletransporte = GetComponentInParent<SavePointTeleportTrigger>(true);

        if (_punto != null) _punto.OnSaveCompleted += AlGuardar;
        TeleportRegistry.OnRegistryChanged += Comprobar;
        TeleportRegistry.OnPointUnlocked += AlDesbloquearse;
        _estadoAplicado = false;
        Comprobar();
    }

    void OnDisable()
    {
        if (_punto != null) _punto.OnSaveCompleted -= AlGuardar;
        TeleportRegistry.OnRegistryChanged -= Comprobar;
        TeleportRegistry.OnPointUnlocked -= AlDesbloquearse;
        _pulso = 0f;
    }

    string Id => _teletransporte != null ? _teletransporte.AnchorId : null;

    bool Descubierto()
    {
        string id = Id;
        return string.IsNullOrEmpty(id) || TeleportRegistry.IsUnlocked(id);
    }

    /// Pone el farol como diga el registro, sin efectos (al cargar la partida o al empezar otra).
    void Comprobar() => Poner(Descubierto(), conEfecto: false);

    void AlDesbloquearse(TeleportPointData punto)
    {
        if (punto == null || punto.anchorId != Id) return;
        Poner(true, conEfecto: true);
    }

    void AlGuardar()
    {
        if (!_encendido) Poner(true, conEfecto: true);
        _pulso = 1f;
    }

    void Poner(bool encendido, bool conEfecto)
    {
        if (_estadoAplicado && encendido == _encendido) return;
        _estadoAplicado = true;
        _encendido = encendido;

        if (estrellaRenderer != null)
        {
            var m = encendido ? materialEncendida : materialApagada;
            if (m != null) estrellaRenderer.sharedMaterial = m;
        }
        if (luz != null)
        {
            luz.enabled = encendido;
            luz.intensity = encendido && !conEfecto ? intensidadBase : 0f;
        }
        _encendiendo = encendido && conEfecto ? 0f : 1f;

        if (!encendido && _listo)
        {
            estrella.localPosition = _posicionBase;
            estrella.localRotation = _giroBase;
            estrella.localScale = _escalaBase;
        }

        if (encendido && conEfecto && vfxAlEncenderse != null && estrella != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxAlEncenderse, estrella.position, Quaternion.identity, 2.5f);
    }

    void Update()
    {
        if (!_listo || !_encendido) return;
        bool animando = _pulso > 0f || _encendiendo < 1f;
        if (!animando && estrellaRenderer != null && !estrellaRenderer.isVisible) return;

        float dt = Time.deltaTime;
        _fase += dt * (Mathf.PI * 2f / periodoDeFlote);
        if (_pulso > 0f) _pulso = Mathf.Max(0f, _pulso - dt / duracionDelPulso);
        if (_encendiendo < 1f) _encendiendo = Mathf.Min(1f, _encendiendo + dt / duracionDelEncendido);
        float p = _pulso * _pulso;                                   // arranca fuerte y se apaga suave
        float e = _encendiendo * _encendiendo * (3f - 2f * _encendiendo); // subida suave

        // La altura va en metros del mundo; se pasa al espacio del padre de la estrella.
        var padre = estrella.parent;
        float escalaY = padre != null ? Mathf.Max(0.0001f, padre.lossyScale.y) : 1f;
        estrella.localPosition = _posicionBase + Vector3.up * (Mathf.Sin(_fase) * alturaDeFlote * e / escalaY);
        estrella.Rotate(0f, gradosPorSegundo * dt, 0f, Space.World);
        // Al encenderse, la estrella da un saltito de tamaño además del pulso de guardar.
        float salto = _encendiendo < 1f ? Mathf.Sin(_encendiendo * Mathf.PI) * 0.35f : 0f;
        estrella.localScale = _escalaBase * (1f + crecimientoDelPulso * p + salto);

        if (luz != null)
            luz.intensity = e * intensidadBase * (1f + latido * Mathf.Sin(_fase * 1.7f)) + intensidadDelPulso * p;
    }
}
