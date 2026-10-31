using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sendero.Core.Feedback;
using Invector.vCharacterController;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Controlador de la peregrinación lineal del Sendero de las Estrellas.
/// Gestiona el sellado del portal de la Tierra a la llegada, la manifestación y colapso
/// secuencial de los portales de las tres pruebas (Will, Estela, Liam) y la llegada al Altar Blanco.
/// </summary>
public class SenderoProgressionController : MonoBehaviour
{
    public enum EstadoSendero
    {
        Llegada,
        HaciaPruebaWill,
        EnPruebaWill,
        HaciaPruebaEstela,
        EnPruebaEstela,
        HaciaPruebaLiam,
        EnPruebaLiam,
        RupturaYReconciliacion,
        FrenteAlAltar,
        BatallaIniciada
    }

    [Header("Estado de la Peregrinación")]
    [SerializeField] private EstadoSendero _estado = EstadoSendero.Llegada;
    public EstadoSendero EstadoActual => _estado;

    [Header("Portales Celestiales")]
    [Tooltip("Portal megalítico de origen desde la Piedra Ancestral (se sella al llegar).")]
    [SerializeField] private GameObject _portalOrigenTierra;

    [Tooltip("Portal de luz dorada y recuerdos de Will (Prueba 1).")]
    [SerializeField] private GameObject _portalWill;

    [Tooltip("Portal de cuarzo rosa y cinta de fuego de Estela (Prueba 2).")]
    [SerializeField] private GameObject _portalEstela;

    [Tooltip("Pedestal con la Caja y marco de espejo de Liam (Prueba 3).")]
    [SerializeField] private GameObject _portalLiam;

    [Header("VFX y Efectos de Transición")]
    [Tooltip("Efecto de un solo uso al cerrarse un portal (pool de VFX).")]
    [SerializeField] private GameObject _vfxColapsoPrefab;
    [Tooltip("Efecto de un solo uso al aparecer un portal (pool de VFX).")]
    [SerializeField] private GameObject _vfxAparicionPrefab;
    [Tooltip("Segundos que dura cada efecto de transición antes de volver al pool.")]
    [SerializeField, Min(0.1f)] private float _duracionVfx = 3f;

    [Header("Escenas de Pruebas")]
    [SerializeField] private string _escenaPruebaWill = "Laberinto";
    [SerializeField] private string _escenaPruebaEstela = "Chuchelandia";
    [SerializeField] private string _escenaPruebaLiam = "Parque";

    [Header("Señal de Batalla Final")]
    [SerializeField] private string _senialBatallaFinal = "SENDERO_FINAL_START";

    [Header("Seguridad en el Vacío")]
    [Tooltip("Altura límite Y bajo la cual el jugador cae al abismo cósmico y es rescatado.")]
    [SerializeField] private float _alturaCaidaVacio = -10f;

    [Tooltip("Dónde se rescata al que cae, por tramo: 0 llegada, 1 tras la puerta de Will, 2 tras la de " +
             "Estela, 3 tras la de Liam, 4 ante el altar. Sin punto: junto a este controlador.")]
    [SerializeField] private Transform[] _puntosDeRescate = new Transform[0];

    private Transform _jugadorTransform;
    private bool _enRespawn = false;

    public static SenderoProgressionController Instance { get; private set; }

    public static event Action<EstadoSendero> OnEstadoCambiado;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnEstadoCambiado = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Al comenzar, los portales futuros están inactivos hasta que los personajes se acerquen
        if (_portalWill != null) _portalWill.SetActive(false);
        if (_portalEstela != null) _portalEstela.SetActive(false);
        if (_portalLiam != null) _portalLiam.SetActive(false);

        if (_estado == EstadoSendero.Llegada)
        {
            StartCoroutine(Co_SecuenciaLlegadaYSellado());
        }
    }

    // El cuerpo del jugador lo da PlayerService: se toma al activarse y se renueva cuando se
    // registra otro (recarga de escena, relevo de cuerpo).
    private void OnEnable()
    {
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered += AlQuitarJugador;
        AlRegistrarJugador(PlayerService.Player);
    }

    private void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered -= AlQuitarJugador;
    }

    private void AlRegistrarJugador(GameObject jugador) => _jugadorTransform = jugador != null ? jugador.transform : null;

    private void AlQuitarJugador() => _jugadorTransform = null;

    private void Update()
    {
        if (_enRespawn || _jugadorTransform == null) return;

        if (_jugadorTransform.position.y < _alturaCaidaVacio)
        {
            StartCoroutine(Co_RespawnJugador());
        }
    }

    private void ReproducirVfx(GameObject prefab, Vector3 posicion)
    {
        if (prefab != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(prefab, posicion, Quaternion.identity, _duracionVfx);
    }

    private IEnumerator Co_RespawnJugador()
    {
        _enRespawn = true;
        FeedbackService.ScreenFlash(new Color(0.15f, 0.05f, 0.35f, 0.8f), 0.4f);

        yield return new WaitForSeconds(0.15f);

        int tramo = _estado switch
        {
            EstadoSendero.Llegada or EstadoSendero.HaciaPruebaWill => 0,
            EstadoSendero.EnPruebaWill or EstadoSendero.HaciaPruebaEstela => 1,
            EstadoSendero.EnPruebaEstela or EstadoSendero.HaciaPruebaLiam => 2,
            EstadoSendero.EnPruebaLiam or EstadoSendero.RupturaYReconciliacion => 3,
            _ => 4,
        };
        Vector3 posDestino;
        Quaternion rotDestino = Quaternion.identity;
        if (tramo < _puntosDeRescate.Length && _puntosDeRescate[tramo] != null)
        {
            posDestino = _puntosDeRescate[tramo].position;
            rotDestino = _puntosDeRescate[tramo].rotation;
        }
        else
        {
            posDestino = transform.position + Vector3.up;
        }

        if (_jugadorTransform != null)
        {
            var motor = _jugadorTransform.GetComponent<vThirdPersonMotor>();
            if (motor != null)
            {
                motor.transform.position = posDestino;
                motor.transform.rotation = rotDestino;
            }
            else
            {
                _jugadorTransform.position = posDestino;
                _jugadorTransform.rotation = rotDestino;
            }

            var rb = _jugadorTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        yield return new WaitForSeconds(0.25f);
        _enRespawn = false;
    }

    /// <summary>
    /// Secuencia inicial: Los personajes llegan y el portal a la Tierra colapsa a sus espaldas.
    /// </summary>
    private IEnumerator Co_SecuenciaLlegadaYSellado()
    {
        yield return new WaitForSeconds(1.8f);

        if (_portalOrigenTierra != null && _portalOrigenTierra.activeSelf)
        {
            Vector3 pos = _portalOrigenTierra.transform.position;
            ReproducirVfx(_vfxColapsoPrefab, pos);

            _portalOrigenTierra.SetActive(false);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[Sendero] El portal a la Tierra se ha cerrado y sellado. No hay vuelta atrás.");
#endif
        }

        CambiarEstado(EstadoSendero.HaciaPruebaWill);
    }

    /// <summary>
    /// Llamado cuando el jugador alcanza el hito astral de la Prueba 1.
    /// </summary>
    public bool ManifestarPortalWill()
    {
        if (_estado != EstadoSendero.HaciaPruebaWill) return false;

        if (_portalWill != null && !_portalWill.activeSelf)
        {
            _portalWill.SetActive(true);
            ReproducirVfx(_vfxAparicionPrefab, _portalWill.transform.position);
        }
        return true;
    }

    /// <summary>
    /// Entrar a la Prueba de Will (Laberinto de Recuerdos).
    /// </summary>
    public bool EntrarPruebaWill()
    {
        if (_estado != EstadoSendero.HaciaPruebaWill) return false;
        CambiarEstado(EstadoSendero.EnPruebaWill);
        CargarEscenaPrueba(_escenaPruebaWill);
        return true;
    }

    /// <summary>
    /// Llamado al terminar con éxito la Prueba de Will.
    /// </summary>
    public void CompletarPruebaWill()
    {
        StartCoroutine(Co_CerrarPortal(_portalWill, EstadoSendero.HaciaPruebaEstela));
    }

    /// <summary>
    /// Llamado cuando el jugador alcanza el hito astral de la Prueba 2.
    /// </summary>
    public bool ManifestarPortalEstela()
    {
        if (_estado != EstadoSendero.HaciaPruebaEstela) return false;

        if (_portalEstela != null && !_portalEstela.activeSelf)
        {
            _portalEstela.SetActive(true);
            ReproducirVfx(_vfxAparicionPrefab, _portalEstela.transform.position);
        }
        return true;
    }

    /// <summary>
    /// Entrar a la Prueba de Estela (Chuchelandia / Duelo del Corazón).
    /// </summary>
    public bool EntrarPruebaEstela()
    {
        if (_estado != EstadoSendero.HaciaPruebaEstela) return false;
        CambiarEstado(EstadoSendero.EnPruebaEstela);
        CargarEscenaPrueba(_escenaPruebaEstela);
        return true;
    }

    /// <summary>
    /// Llamado al terminar con éxito la Prueba de Estela.
    /// </summary>
    public void CompletarPruebaEstela()
    {
        StartCoroutine(Co_CerrarPortal(_portalEstela, EstadoSendero.HaciaPruebaLiam));
    }

    /// <summary>
    /// Llamado cuando el jugador alcanza el hito astral de la Prueba 3.
    /// </summary>
    public bool ManifestarPortalLiam()
    {
        if (_estado != EstadoSendero.HaciaPruebaLiam) return false;

        if (_portalLiam != null && !_portalLiam.activeSelf)
        {
            _portalLiam.SetActive(true);
            ReproducirVfx(_vfxAparicionPrefab, _portalLiam.transform.position);
        }
        return true;
    }

    /// <summary>
    /// Entrar a la Prueba de Liam (el parque de atracciones).
    /// </summary>
    public bool EntrarPruebaLiam()
    {
        if (_estado != EstadoSendero.HaciaPruebaLiam) return false;
        CambiarEstado(EstadoSendero.EnPruebaLiam);
        CargarEscenaPrueba(_escenaPruebaLiam);
        return true;
    }

    /// <summary>
    /// Llamado al terminar con éxito la Prueba de Liam (confesión y verdad revelada).
    /// </summary>
    public void CompletarPruebaLiam()
    {
        StartCoroutine(Co_CerrarPortal(_portalLiam, EstadoSendero.RupturaYReconciliacion));
    }

    /// <summary>
    /// Llamado tras concluir la conversación y reconciliación sobre el Sendero.
    /// </summary>
    public bool AvanzarHaciaElAltar()
    {
        if (_estado != EstadoSendero.RupturaYReconciliacion) return false;
        CambiarEstado(EstadoSendero.FrenteAlAltar);
        return true;
    }

    /// <summary>
    /// Llamado al llegar frente al Altar de Piedra Blanca para detonar la aparición del Mago Oscuro.
    /// </summary>
    public bool DesatarAparicionMagoOscuro()
    {
        if (_estado == EstadoSendero.BatallaIniciada) return false;
        CambiarEstado(EstadoSendero.BatallaIniciada);

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sendero] El aire frente al altar se ha rasgado. Disparando señal '{_senialBatallaFinal}'.");
#endif
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_senialBatallaFinal);
        return true;
    }

    private IEnumerator Co_CerrarPortal(GameObject portal, EstadoSendero siguienteEstado)
    {
        if (portal != null && portal.activeSelf)
        {
            Vector3 pos = portal.transform.position;
            ReproducirVfx(_vfxColapsoPrefab, pos);

            yield return new WaitForSeconds(0.6f);
            portal.SetActive(false);
        }

        CambiarEstado(siguienteEstado);
    }

    private void CargarEscenaPrueba(string nombreEscena)
    {
        if (string.IsNullOrEmpty(nombreEscena)) return;

        if (Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            SceneManager.LoadScene(nombreEscena, LoadSceneMode.Single);
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Sendero] La escena '{nombreEscena}' no está añadida al BuildSettings.");
#endif
        }
    }

    private void CambiarEstado(EstadoSendero nuevoEstado)
    {
        _estado = nuevoEstado;
        OnEstadoCambiado?.Invoke(_estado);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sendero] Transición de peregrinación: {_estado}");
#endif
    }
}
