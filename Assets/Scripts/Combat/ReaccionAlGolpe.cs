using System;
using Sendero.Core.Feedback;
using UnityEngine;

/// Lo que se ve y se oye cuando a un enemigo le entra un golpe, igual para todos: el cuerpo da un
/// destello, el modelo se encoge y retrocede un palmo (solo lo visual: no mueve la IA ni la física),
/// salen unas chispas propias donde le han dado, suena el golpe y, si es fuerte, la acción se para
/// un instante. Un golpe que no entra (AvisosDeCombate.GolpeMalDado sobre este enemigo) tiene su
/// propio efecto y sonido. La animación de golpe la pone la IA de cada enemigo escuchando
/// AlReaccionar, para que decida si corta lo que está haciendo. Ver INC-1333.
/// Si el golpe entra con debilidad o resistencia elemental (<see cref="AfinidadElemental"/>), el
/// destello sale con el color y la duración de <see cref="SenalesDeAfinidad"/>.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class ReaccionAlGolpe : MonoBehaviour
{
    [Tooltip("Hueso o pieza que se sacude y retrocede (la pelvis del esqueleto). Vacío = el hueso raíz de su primer SkinnedMeshRenderer.")]
    [SerializeField] private Transform modelo;

    [Header("Destello")]
    [Tooltip("Multiplicador del color del cuerpo en el destello (por encima de 1 aclara; con bloom brilla).")]
    [SerializeField, ColorUsage(false, true)] private Color destello = new Color(3.2f, 2.8f, 2.6f);
    [SerializeField, Min(0.02f)] private float duracionDelDestello = 0.12f;

    [Header("Sacudida")]
    [Tooltip("Metros que retrocede el modelo, alejándose de quien golpea.")]
    [SerializeField, Min(0f)] private float retroceso = 0.22f;
    [Tooltip("Cuánto se aplasta el modelo en el golpe (0,15 = un 15 %).")]
    [SerializeField, Range(0f, 0.5f)] private float aplaste = 0.14f;
    [SerializeField, Min(0.05f)] private float duracionDeLaSacudida = 0.22f;

    [Header("Efecto y sonido")]
    [Tooltip("Efecto de un solo uso donde entra el golpe (por el pool).")]
    [SerializeField] private GameObject vfxGolpe;
    [SerializeField, Min(0.1f)] private float vidaDelVfx = 1f;
    [Tooltip("Altura sobre los pies a la que sale el efecto.")]
    [SerializeField] private float alturaDelGolpe = 1.2f;
    [Tooltip("Clave del sonido del golpe en el perfil de audio.")]
    [SerializeField] private string sonidoGolpe = "";
    [SerializeField, Range(0f, 1f)] private float volumen = 0.9f;

    [Header("Golpe fuerte")]
    [Tooltip("Daño a partir del cual el golpe es fuerte: pausa breve y temblor de cámara.")]
    [SerializeField, Min(0f)] private float danoFuerte = 20f;
    [SerializeField, Range(0.02f, 0.3f)] private float escalaDeTiempoEnLaPausa = 0.08f;
    [SerializeField, Range(0f, 0.2f)] private float pausaDelGolpeFuerte = 0.06f;
    [SerializeField, Min(0f)] private float temblorDeCamara = 0.25f;

    [Header("Golpe que no entra")]
    [SerializeField] private GameObject vfxRechazo;
    [SerializeField] private string sonidoRechazo = "";
    [Tooltip("Segundos mínimos entre dos avisos de golpe rechazado (una ráfaga no repite el sonido en cada bala).")]
    [SerializeField, Min(0f)] private float separacionDeRechazos = 0.25f;

    /// Le ha entrado un golpe: cantidad y si es fuerte. Para que la IA ponga su animación.
    public event Action<float, bool> AlReaccionar;

    private Damageable _vida;
    private AfinidadElemental _afinidad;
    private DestelloDelCuerpo _destello;

    private float _destelloHasta = -1f;
    private float _sacudidaDesde = -1f;
    private Vector3 _direccionLocal;
    private Vector3 _posLimpia, _posEscrita, _escalaLimpia, _escalaEscrita;
    private bool _modeloTocado;
    private float _ultimoRechazo = -999f;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _afinidad = GetComponent<AfinidadElemental>();
        _destello = new DestelloDelCuerpo(gameObject);
        if (modelo == null)
        {
            var piel = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (piel != null) modelo = piel.rootBone;
        }

        // Escucha siempre; el componente solo se enciende (LateUpdate) mientras dura una reacción.
        _vida.OnDamagedBy += AlRecibirDano;
        AvisosDeCombate.AlGolpeMalDado += AlGolpeMalDado;
        enabled = false;
    }

    void OnDestroy()
    {
        if (_vida != null) _vida.OnDamagedBy -= AlRecibirDano;
        AvisosDeCombate.AlGolpeMalDado -= AlGolpeMalDado;
    }

    void OnDisable()
    {
        // El componente se apaga solo al acabar cada reacción; si lo apaga otro (o el objeto) a
        // mitad, se deja el cuerpo como estaba.
        _destello?.Quitar();
        DevolverModelo();
        _destelloHasta = -1f;
        _sacudidaDesde = -1f;
    }

    private void AlRecibirDano(float cantidad, GameObject quien)
    {
        if (!gameObject.activeInHierarchy) return;
        bool fuerte = cantidad >= danoFuerte;

        Vector3 centro = transform.position + Vector3.up * alturaDelGolpe;
        Vector3 desde = quien != null ? quien.transform.position : centro - transform.forward;
        Vector3 fuera = centro - desde;
        fuera.y = 0f;
        if (fuera.sqrMagnitude < 0.0001f) fuera = -transform.forward;
        fuera.Normalize();

        if (vfxGolpe != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxGolpe, centro - fuera * 0.35f, Quaternion.LookRotation(-fuera), vidaDelVfx);
        if (!string.IsNullOrEmpty(sonidoGolpe))
            AudioService.Instance?.PlaySFX(sonidoGolpe, fuerte ? volumen : volumen * 0.8f, centro, fuerte ? 0.92f : 1f);
        if (fuerte)
        {
            if (pausaDelGolpeFuerte > 0f) FeedbackService.HitStop(escalaDeTiempoEnLaPausa, pausaDelGolpeFuerte);
            if (temblorDeCamara > 0f) FeedbackService.CameraShake(temblorDeCamara, 0.18f);
        }

        // Golpe con debilidad o resistencia elemental: el destello de su señal.
        var golpe = _afinidad != null ? _afinidad.GolpeEnCurso : default;
        if (golpe.activo && golpe.afinidad != AfinidadAlElemento.Normal)
        {
            var senal = SenalesDeAfinidad.Tabla.De(golpe.elemento, golpe.afinidad);
            _destello.Poner(senal.destello);
            _destelloHasta = Time.time + Mathf.Max(duracionDelDestello, senal.duracionDelDestello);
        }
        else
        {
            _destello.Poner(destello);
            _destelloHasta = Time.time + duracionDelDestello;
        }
        if (modelo != null && modelo.parent != null)
        {
            _direccionLocal = modelo.parent.InverseTransformDirection(fuera);
            _sacudidaDesde = Time.time;
        }
        enabled = true;

        AlReaccionar?.Invoke(cantidad, fuerte);
    }

    private void AlGolpeMalDado(GameObject jefe)
    {
        if (jefe != gameObject || !gameObject.activeInHierarchy || Time.time < _ultimoRechazo + separacionDeRechazos) return;
        _ultimoRechazo = Time.time;
        Vector3 centro = transform.position + Vector3.up * alturaDelGolpe;
        if (vfxRechazo != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRechazo, centro, Quaternion.identity, vidaDelVfx);
        if (!string.IsNullOrEmpty(sonidoRechazo))
            AudioService.Instance?.PlaySFX(sonidoRechazo, volumen * 0.8f, centro);
    }

    void LateUpdate()
    {
        bool sigue = false;

        if (_destelloHasta > 0f)
        {
            if (Time.time >= _destelloHasta) { _destello.Quitar(); _destelloHasta = -1f; }
            else sigue = true;
        }

        if (_sacudidaDesde > 0f && modelo != null)
        {
            float k = (Time.time - _sacudidaDesde) / duracionDeLaSacudida;
            if (k >= 1f) { DevolverModelo(); _sacudidaDesde = -1f; }
            else
            {
                // Sale de golpe y vuelve con un rebote pequeño.
                float curva = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
                AplicarAlModelo(_direccionLocal * (retroceso * curva), 1f - aplaste * curva);
                sigue = true;
            }
        }

        if (!sigue) enabled = false;
    }

    // El esqueleto lo escribe el Animator cada fotograma; si un hueso no está animado, lo que se
    // escribió el fotograma anterior sigue ahí y no debe acumularse.
    private void AplicarAlModelo(Vector3 desplazamiento, float escalaVertical)
    {
        Vector3 pos = modelo.localPosition, esc = modelo.localScale;
        if (!_modeloTocado || pos != _posEscrita) _posLimpia = pos;
        if (!_modeloTocado || esc != _escalaEscrita) _escalaLimpia = esc;
        _posEscrita = _posLimpia + desplazamiento;
        float ancho = 1f + (1f - escalaVertical) * 0.5f;
        _escalaEscrita = new Vector3(_escalaLimpia.x * ancho, _escalaLimpia.y * escalaVertical, _escalaLimpia.z * ancho);
        modelo.localPosition = _posEscrita;
        modelo.localScale = _escalaEscrita;
        _modeloTocado = true;
    }

    private void DevolverModelo()
    {
        if (!_modeloTocado || modelo == null) { _modeloTocado = false; return; }
        if (modelo.localPosition == _posEscrita) modelo.localPosition = _posLimpia;
        if (modelo.localScale == _escalaEscrita) modelo.localScale = _escalaLimpia;
        _modeloTocado = false;
    }
}
