using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Portada del menú principal: elige qué escena de fondo y qué música se ven según hasta dónde
/// ha llegado la partida guardada. Cada etapa es un <see cref="PortadaEtapa"/>; se enseña la
/// última de la lista cuyo flag narrativo esté en la partida. La primera (sin flag) es la de
/// partida nueva y la que se ve sin partida guardada.
///
/// Qué partida se mira: la que cargaría «Continuar» (el save en disco, o el bootPreset en modo
/// pruebas), no lo que haya en memoria de una sesión sin guardar.
/// </summary>
[DisallowMultipleComponent]
public class PortadaDelMenu : MonoBehaviour
{
    [Tooltip("Etapas en orden de la historia. La primera es la de partida nueva.")]
    [SerializeField] private PortadaEtapa[] etapas;

    [Tooltip("Cámara del menú (la que lleva MainMenuWorldCameraDrift). Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Header("Pruebas")]
    [Tooltip("Solo en el Editor: enseña esta etapa (índice en la lista) sin mirar la partida. -1 = según la partida.")]
    [SerializeField] private int forzarEtapaEnEditor = -1;

    public PortadaEtapa Activa { get; private set; }

    void Awake()
    {
        if (camara == null) camara = Camera.main;
    }

    void Start()
    {
        // En Start y no en Awake: los servicios de Start.unity (SaveSystem, GameBootService)
        // ya están registrados, y AudioService ya ha puesto la música de la escena, que aquí se
        // sustituye por la de la portada.
        Mostrar(ElegirEtapa());
    }

    int ElegirEtapa()
    {
        if (etapas == null || etapas.Length == 0) return -1;

#if UNITY_EDITOR
        if (forzarEtapaEnEditor >= 0 && forzarEtapaEnEditor < etapas.Length)
            return forzarEtapaEnEditor;
#endif

        var flags = FlagsDeLaPartida();
        int elegida = 0;
        if (flags != null)
        {
            for (int i = 1; i < etapas.Length; i++)
            {
                var etapa = etapas[i];
                if (etapa == null || string.IsNullOrWhiteSpace(etapa.FlagQueLaDesbloquea)) continue;
                if (flags.Contains(NarrativeFlags.Key(etapa.FlagQueLaDesbloquea))) elegida = i;
            }
        }
        return elegida;
    }

    /// <summary>Flags de la partida que cargaría «Continuar»; null si no hay partida.</summary>
    static List<string> FlagsDeLaPartida()
    {
        if (GameBootService.IsPresetOverrideActive)
            return GameBootService.Profile != null && GameBootService.Profile.bootPreset != null
                ? GameBootService.Profile.bootPreset.flags
                : null;

        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        if (saveSystem != null && saveSystem.HasSave() && saveSystem.Load(out var data) && data != null)
            return data.flags;
        return null;
    }

    /// <summary>Enciende la etapa indicada, apaga las demás y aplica cámara, ambiente y música.</summary>
    public void Mostrar(int indice)
    {
        if (etapas == null || indice < 0 || indice >= etapas.Length) return;

        for (int i = 0; i < etapas.Length; i++)
        {
            var etapa = etapas[i];
            if (etapa == null) continue;
            bool activa = i == indice;
            if (etapa.gameObject.activeSelf != activa) etapa.gameObject.SetActive(activa);
        }

        Activa = etapas[indice];
        if (Activa == null) return;

        ColocarCamara(Activa);
        Activa.AplicarAmbiente();

        if (Activa.Musica != null && AudioService.Instance != null)
            AudioService.Instance.FijarMusicaDeEscena(Activa.Musica);
    }

    void ColocarCamara(PortadaEtapa etapa)
    {
        if (camara == null) return;

        if (etapa.Encuadre != null)
            camara.transform.SetPositionAndRotation(etapa.Encuadre.position, etapa.Encuadre.rotation);
        if (etapa.CampoDeVision > 0f)
            camara.fieldOfView = etapa.CampoDeVision;

        // La cámara solo se mece donde hay personajes volando (el Sendero); en tierra, quieta.
        // La deriva toma como base el encuadre recién puesto.
        var deriva = camara.GetComponent<MainMenuWorldCameraDrift>();
        if (deriva != null)
        {
            deriva.Reanclar();
            bool seMece = etapa.GetComponentInChildren<MainMenuFlyingCompanion>(true) != null;
            if (deriva.enabled != seMece) deriva.enabled = seMece;
        }
    }

    /// <summary>
    /// Animación de salida de la portada activa al pulsar Nueva Partida o Continuar. Devuelve los
    /// segundos que hay que esperar antes de cargar.
    /// </summary>
    public float ReproducirSalida() => Activa != null ? Activa.ReproducirSalida() : 0f;
}
