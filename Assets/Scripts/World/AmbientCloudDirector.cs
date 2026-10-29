using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Mantiene las nubes de buen tiempo y responde a CloudsBuildingUp y CloudsClearing.
/// La cobertura del cielo depende de DayNightCycle.DuracionDeNublarse.
/// No decide el clima: informa a DayNightCycle de su cobertura (InformarCoberturaDeNubes) y es el
/// sorteo de clima de DayNightCycle el que, con más nubes, tiene más probabilidad de lluvia.
public class AmbientCloudDirector : MonoBehaviour
{
    #region Singleton

    public static AmbientCloudDirector Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes sueltas")]
    [Tooltip("Prefabs de nube para los vuelos ambientales (recomendado: los mismos QuibliRainCloud3D_1..4 de Assets/Prefabs/VFX, o variantes 'de buen tiempo'). Cada hueco del pool elige uno al azar UNA vez en Awake y lo reutiliza siempre.")]
    [SerializeField] private GameObject[] ambientCloudPrefabs;
    [Tooltip("Nº máximo de nubes sueltas vivas a la vez. Si se necesita una nueva y el pool está lleno (todas en pleno vuelo), ese ciclo de spawn simplemente se salta.")]
    [SerializeField] private int poolSize = 6;
    [Tooltip("Distancia desde el jugador a la que nace/muere cada nube. La ruta completa mide el doble (nace a un lado, muere al otro).")]
    [SerializeField] private float spawnDistance = 180f;
    [Tooltip("Radio horizontal del punto de paso más cercano al jugador.")]
    [SerializeField] private float passOffsetRadius = 90f;
    [Tooltip("Altura máxima de las nubes ambientales.")]
    [SerializeField] private float cloudAltitude = 100f;
    [Tooltip("Altura mínima de las nubes ambientales.")]
    [SerializeField] private float minCloudAltitude = 32f;
    [SerializeField] private float altitudeJitter = 8f;
    [Tooltip("Ángulo de elevación de las nubes sobre el horizonte visto desde el jugador.")]
    [SerializeField, Range(5f, 45f)] private float horizonElevationDegrees = 30f;
    [SerializeField] private Vector2 speedRange = new Vector2(3f, 6f);

    [Header("Viento")]
    [Tooltip("Dirección compartida de las nubes ambientales, en grados: 0 hacia +Z, 90 hacia +X.")]
    [SerializeField, Range(0f, 360f)] private float windDirectionDegrees = 45f;
    [Tooltip("Desviación aleatoria máxima (± grados) que cada nube individual puede tomar respecto a windDirectionDegrees, para que no todas vuelen en una línea perfectamente recta.")]
    [SerializeField, Range(0f, 90f)] private float windDirectionJitterDegrees = 20f;
    [SerializeField] private float fadeInDuration = 6f;
    [SerializeField] private float fadeOutDuration = 6f;
    [Tooltip("Probabilidad de que, tras spawnear una nube, salgan 1-2 más detrás en vez de una suelta — para que a veces se vea 'un grupito' bonito pasando.")]
    [SerializeField, Range(0f, 1f)] private float clusterChance = 0.3f;
    [SerializeField] private int clusterExtraMax = 2;
    [SerializeField] private Vector2 clusterDelayRange = new Vector2(4f, 10f);
    [Tooltip("Rango de espera entre nubes con cielo despejado.")]
    [SerializeField] private Vector2 idleSpawnIntervalRange = new Vector2(30f, 55f);
    [Tooltip("Rango de espera entre spawns con cobertura alta (cielo nublado/aclarando tras lluvia): mucho más seguido que en despejado.") ]
    [SerializeField] private Vector2 busySpawnIntervalRange = new Vector2(10f, 20f);

    [Header("Paseo aleatorio de cobertura")]
    [Tooltip("Cobertura (0-1) a partir de la cual la frecuencia de nubes sueltas ya está al máximo (busySpawnIntervalRange).")]
    [SerializeField, Range(0.05f, 1f)] private float lightCloudThreshold = 0.4f;
    [SerializeField] private Vector2 walkStepIntervalRange = new Vector2(10f, 25f);
    [Tooltip("Máximo cambio aleatorio de cobertura por paso (hacia arriba o hacia abajo).")]
    [SerializeField] private float walkStepSize = 0.15f;
    [Tooltip("Cuánto tira la cobertura hacia 0 en cada paso (por encima del paso aleatorio), para que un cielo nublado no se quede así para siempre sin motivo.")]
    [SerializeField] private float coverageDecayPerStep = 0.05f;
    [Tooltip("Cobertura a la que se fija el paseo aleatorio justo cuando termina una lluvia de verdad, para que el cielo tarde un rato en despejarse del todo en vez de volver a 0 de golpe.")]
    [SerializeField, Range(0f, 1f)] private float postRainResidualCoverage = 0.6f;

    private readonly List<AmbientCloudDrifter> _pool = new List<AmbientCloudDrifter>();
    private readonly Queue<AmbientCloudDrifter> _free = new Queue<AmbientCloudDrifter>();

    private Coroutine _walkRoutine;
    private Coroutine _spawnRoutine;
    private float _coverage;
    private bool _stormActive;

    private bool _hiddenByInterior;
    private bool _visibleRequested = true;

    private bool _zoneCloudBoostActive;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        BuildPool();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp += HandleStormStarting;
            dayNightCycle.CloudsClearing += HandleCloudsClearing;
            _stormActive = dayNightCycle.HayNubesDePrecipitacion;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
        {
            Debug.LogWarning("[AmbientCloudDirector] No se encontró ningún DayNightCycle en la escena; la nubosidad ambiental no sabrá cuándo hay tormenta real.");
        }
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _hiddenByInterior = ec != null && ec.IsEffectivelyInterior;
        ApplyCloudVisibility();

        if (_walkRoutine == null)
            _walkRoutine = StartCoroutine(CoverageWalkLoop());
        if (_spawnRoutine == null)
            _spawnRoutine = StartCoroutine(CloudSpawnLoop());
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp -= HandleStormStarting;
            dayNightCycle.CloudsClearing -= HandleCloudsClearing;
        }

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_walkRoutine != null) { StopCoroutine(_walkRoutine); _walkRoutine = null; }
        if (_spawnRoutine != null) { StopCoroutine(_spawnRoutine); _spawnRoutine = null; }

        if (dayNightCycle != null) dayNightCycle.InformarCoberturaDeNubes(0f);
    }

    void HandleStormStarting() => _stormActive = true;

    void HandleCloudsClearing()
    {
        _stormActive = false;
        _coverage = Mathf.Max(_coverage, postRainResidualCoverage);
        if (dayNightCycle != null) dayNightCycle.InformarCoberturaDeNubes(_coverage);
    }

    void HandleInteriorEntered()
    {
        _hiddenByInterior = true;
        ApplyCloudVisibility();
    }

    void HandleInteriorExited()
    {
        _hiddenByInterior = false;
        ApplyCloudVisibility();
    }

    void Update()
    {

        var ec = EnvironmentController.Instance;
        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior;
        if (effectivelyInteriorNow != _hiddenByInterior)
        {
            if (effectivelyInteriorNow) HandleInteriorEntered();
            else HandleInteriorExited();
        }
    }

    void BuildPool()
    {
        if (ambientCloudPrefabs == null || ambientCloudPrefabs.Length == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[AmbientCloudDirector] ambientCloudPrefabs está vacío; no habrá nubes sueltas de buen tiempo.");
#endif
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var prefab = ambientCloudPrefabs[UnityEngine.Random.Range(0, ambientCloudPrefabs.Length)];
            if (prefab == null) continue;

            var instance = Instantiate(prefab, transform);
            instance.SetActive(false);

            var drifter = instance.GetComponent<AmbientCloudDrifter>();
            if (drifter == null)
                drifter = instance.AddComponent<AmbientCloudDrifter>();

            _pool.Add(drifter);
            _free.Enqueue(drifter);
        }
    }

    IEnumerator CoverageWalkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(walkStepIntervalRange.x, walkStepIntervalRange.y));

            if (_stormActive || dayNightCycle == null || dayNightCycle.HayNubesDePrecipitacion)
                continue;

            float delta = UnityEngine.Random.Range(-walkStepSize, walkStepSize);
            _coverage = Mathf.Clamp01(_coverage + delta);
            _coverage = Mathf.MoveTowards(_coverage, 0f, coverageDecayPerStep);
            dayNightCycle.InformarCoberturaDeNubes(_coverage);
        }
    }

    IEnumerator CloudSpawnLoop()
    {
        while (true)
        {
            float coverageFactor = _zoneCloudBoostActive ? 1f : Mathf.Clamp01(_coverage / Mathf.Max(0.01f, lightCloudThreshold));
            float lo = Mathf.Lerp(idleSpawnIntervalRange.x, busySpawnIntervalRange.x, coverageFactor);
            float hi = Mathf.Lerp(idleSpawnIntervalRange.y, busySpawnIntervalRange.y, coverageFactor);
            yield return new WaitForSeconds(UnityEngine.Random.Range(lo, hi));

            if (_stormActive || _hiddenByInterior)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                if (_zoneCloudBoostActive)
                    Debug.Log($"[AmbientCloudDirector] CloudSpawnLoop salta spawn -- _stormActive={_stormActive} _hiddenByInterior={_hiddenByInterior}");
#endif
                continue;
            }

            SpawnPassingCloud();

            if (UnityEngine.Random.value < clusterChance)
            {
                int extra = UnityEngine.Random.Range(1, clusterExtraMax + 1);
                for (int i = 0; i < extra; i++)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(clusterDelayRange.x, clusterDelayRange.y));
                    if (_stormActive || _hiddenByInterior) break;
                    SpawnPassingCloud();
                }
            }
        }
    }

    void SpawnPassingCloud()
    {
        Transform followT = PlayerService.Player != null ? PlayerService.Player.transform : null;
        if (followT == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (_zoneCloudBoostActive)
                Debug.Log("[AmbientCloudDirector] SpawnPassingCloud: PlayerService.Player es null, se salta este ciclo.");
#endif
            return;
        }

        if (_free.Count == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (_zoneCloudBoostActive)
                Debug.Log("[AmbientCloudDirector] SpawnPassingCloud: pool de nubes lleno, se salta este ciclo.");
#endif
            return; // pool lleno de nubes ya en vuelo: se salta este ciclo.
        }

        var drifter = _free.Dequeue();

        Vector2 offset2D = UnityEngine.Random.insideUnitCircle * passOffsetRadius;
        Vector3 passPoint = followT.position + new Vector3(offset2D.x, 0f, offset2D.y);

        float offsetMagnitude = offset2D.magnitude;
        float elevationHeight = offsetMagnitude * Mathf.Tan(horizonElevationDegrees * Mathf.Deg2Rad);
        float baseHeight = Mathf.Clamp(elevationHeight, minCloudAltitude, cloudAltitude);
        passPoint.y = followT.position.y + baseHeight + UnityEngine.Random.Range(-altitudeJitter, altitudeJitter);

        float angle = (windDirectionDegrees + UnityEngine.Random.Range(-windDirectionJitterDegrees, windDirectionJitterDegrees)) * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        Vector3 start = passPoint - dir * spawnDistance;
        Vector3 end = passPoint + dir * spawnDistance;

        float speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);

        drifter.gameObject.SetActive(true);
        drifter.Play(start, end, speed, fadeInDuration, fadeOutDuration, OnCloudFinished);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (_zoneCloudBoostActive)
            Debug.Log("[AmbientCloudDirector] Nube ambiental generada (boost de zona activo).");
#endif
    }

    void OnCloudFinished(AmbientCloudDrifter drifter)
    {
        drifter.gameObject.SetActive(false);
        _free.Enqueue(drifter);
    }

    [ContextMenu("Forzar una nube ambiental ahora (debug)")]
    void DebugForceSpawnCloud() => SpawnPassingCloud();

    public void SetAmbientCloudsVisible(bool visible)
    {
        _visibleRequested = visible;
        ApplyCloudVisibility();
    }

    public void SetZoneCloudBoost(bool active)
    {
        _zoneCloudBoostActive = active;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AmbientCloudDirector] SetZoneCloudBoost({active})");
#endif
    }

    void ApplyCloudVisibility()
    {
        bool visible = _visibleRequested && !_hiddenByInterior;
        for (int i = 0; i < _pool.Count; i++)
        {
            var drifter = _pool[i];
            if (drifter != null) drifter.SetRenderersVisible(visible);
        }
    }
}
