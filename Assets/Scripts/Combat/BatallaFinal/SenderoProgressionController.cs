using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private GameObject _vfxColapsoPrefab;
    [SerializeField] private GameObject _vfxAparicionPrefab;

    [Header("Escenas de Pruebas")]
    [SerializeField] private string _escenaPruebaWill = "Sendero_PruebaWill";
    [SerializeField] private string _escenaPruebaEstela = "CandyLand";
    [SerializeField] private string _escenaPruebaLiam = "Sendero";

    [Header("Señal de Batalla Final")]
    [SerializeField] private string _senialBatallaFinal = "SENDERO_FINAL_START";

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

    /// <summary>
    /// Secuencia inicial: Los personajes llegan y el portal a la Tierra colapsa a sus espaldas.
    /// </summary>
    private IEnumerator Co_SecuenciaLlegadaYSellado()
    {
        yield return new WaitForSeconds(1.8f);

        if (_portalOrigenTierra != null && _portalOrigenTierra.activeSelf)
        {
            Vector3 pos = _portalOrigenTierra.transform.position;
            if (_vfxColapsoPrefab != null)
            {
                Instantiate(_vfxColapsoPrefab, pos, Quaternion.identity);
            }

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
    public void ManifestarPortalWill()
    {
        if (_estado != EstadoSendero.HaciaPruebaWill) return;

        if (_portalWill != null && !_portalWill.activeSelf)
        {
            _portalWill.SetActive(true);
            if (_vfxAparicionPrefab != null)
            {
                Instantiate(_vfxAparicionPrefab, _portalWill.transform.position, Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// Entrar a la Prueba de Will (Laberinto de Recuerdos).
    /// </summary>
    public void EntrarPruebaWill()
    {
        if (_estado != EstadoSendero.HaciaPruebaWill) return;
        CambiarEstado(EstadoSendero.EnPruebaWill);
        CargarEscenaPrueba(_escenaPruebaWill);
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
    public void ManifestarPortalEstela()
    {
        if (_estado != EstadoSendero.HaciaPruebaEstela) return;

        if (_portalEstela != null && !_portalEstela.activeSelf)
        {
            _portalEstela.SetActive(true);
            if (_vfxAparicionPrefab != null)
            {
                Instantiate(_vfxAparicionPrefab, _portalEstela.transform.position, Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// Entrar a la Prueba de Estela (Chuchelandia / Duelo del Corazón).
    /// </summary>
    public void EntrarPruebaEstela()
    {
        if (_estado != EstadoSendero.HaciaPruebaEstela) return;
        CambiarEstado(EstadoSendero.EnPruebaEstela);
        CargarEscenaPrueba(_escenaPruebaEstela);
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
    public void ManifestarPortalLiam()
    {
        if (_estado != EstadoSendero.HaciaPruebaLiam) return;

        if (_portalLiam != null && !_portalLiam.activeSelf)
        {
            _portalLiam.SetActive(true);
            if (_vfxAparicionPrefab != null)
            {
                Instantiate(_vfxAparicionPrefab, _portalLiam.transform.position, Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// Entrar a la Prueba de Liam (La Caja / Pasillo de las Mentiras).
    /// </summary>
    public void EntrarPruebaLiam()
    {
        if (_estado != EstadoSendero.HaciaPruebaLiam) return;
        CambiarEstado(EstadoSendero.EnPruebaLiam);
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
    public void AvanzarHaciaElAltar()
    {
        if (_estado != EstadoSendero.RupturaYReconciliacion) return;
        CambiarEstado(EstadoSendero.FrenteAlAltar);
    }

    /// <summary>
    /// Llamado al llegar frente al Altar de Piedra Blanca para detonar la aparición del Mago Oscuro.
    /// </summary>
    public void DesatarAparicionMagoOscuro()
    {
        if (_estado == EstadoSendero.BatallaIniciada) return;
        CambiarEstado(EstadoSendero.BatallaIniciada);

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sendero] El aire frente al altar se ha rasgado. Disparando señal '{_senialBatallaFinal}'.");
#endif
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_senialBatallaFinal);
    }

    private IEnumerator Co_CerrarPortal(GameObject portal, EstadoSendero siguienteEstado)
    {
        if (portal != null && portal.activeSelf)
        {
            Vector3 pos = portal.transform.position;
            if (_vfxColapsoPrefab != null)
            {
                Instantiate(_vfxColapsoPrefab, pos, Quaternion.identity);
            }

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
