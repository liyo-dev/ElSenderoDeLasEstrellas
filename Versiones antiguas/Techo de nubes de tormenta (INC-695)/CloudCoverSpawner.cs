using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Forma y retira las nubes cercanas mediante CloudsBuildingUp y CloudsClearing.
/// DayNightCycle.DuracionDeNublarse gobierna la cobertura del cielo procedural.
public class CloudCoverSpawner : MonoBehaviour
{
    public enum CloudShaderMode { QuibliCloud3D, QuibliCloud2D, LegacyBaseColor }

    private class CloudUnit
    {
        public Transform transform;
        public Renderer[] renderers;
        public Vector3 targetLocalPos;
        public Vector3 farLocalPos;
        public float delayOffset01;
        public float durationMultiplier;
        public float formationT;
    }

    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes")]
    [Tooltip("Prefabs de nube a repartir por el techo. Recomendado: QuibliRainCloud3D_1..4 (Assets/Prefabs/VFX/), mallas Cloud3D de Quibli como las del [Demo] SampleSceneWithQuibli. Se elige uno al azar por instancia.")]
    [SerializeField] private GameObject[] cloudPrefabs;
    [Tooltip("Altura mínima del centro del techo sobre el jugador.") ]
    [SerializeField] private float cloudHeight = 140f;
    [Tooltip("Radio horizontal alrededor del jugador que cubre el techo de nubes. Cuanto más grande, menos se nota el borde del área cubierta, pero más instancias hacen falta.")]
    [SerializeField] private float coverRadius = 150f;
    [Tooltip("Separación aproximada entre nubes de la rejilla. Más bajo = más denso = tapa mejor el cielo, pero más nubes instanciadas (y más overdraw con quads transparentes).")]
    [SerializeField] private float cellSize = 30f;
    [Tooltip("Variación aleatoria de posición dentro de cada celda de la rejilla, para que no se note el patrón regular.")]
    [SerializeField, Range(0f, 1f)] private float jitter = 0.5f;
    [Tooltip("Rango de escala de las nubes, como multiplicador del tamaño del prefab.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.5f);
    [Tooltip("Ancho mínimo de cada nube en planta, en múltiplos de cellSize. Si con scaleRange una nube sale más estrecha, se ensancha solo en horizontal (X/Z) hasta ese ancho, para que se solape con sus vecinas y desde abajo formen una capa continua sin que baje el techo. Por encima de 1 porque el shader Cloud3D recorta el borde (_AlphaThreshold) y la silueta visible es menor que la malla. 0 = sin ensanchar.")]
    [SerializeField, Range(0f, 3f)] private float minFootprintInCells = 1.6f;
    [Tooltip("Límite de seguridad de instancias, por si coverRadius/cellSize generan una rejilla enorme.")]
    [SerializeField] private int maxCloudInstances = 300;
    [Tooltip("Máximo de nubes que se instancian por frame durante la construcción del techo.")]
    [SerializeField, Min(1)] private int maxCloudsInstantiatedPerFrame = 25;
    [Tooltip("Variación aleatoria de altura (±) de cada nube respecto al plano del techo. Rompe el plano perfecto (más natural) y evita que todos los quads transparentes queden coplanares, lo que provoca artefactos de ordenación al mirarlos desde abajo.")]
    [SerializeField] private float heightJitter = 16f;
    [Tooltip("Margen mínimo, en unidades de mundo, entre el punto más bajo de la malla de nubes ya instanciada/escalada y el jugador. Tras construir el techo se mide su altura REAL (no solo cloudHeight) y si no deja este margen, se sube el techo entero lo que haga falta. Es la protección contra 'la cámara se queda dentro de la nube' si cloudHeight/scaleRange quedan mal calibrados para el prefab que uses.")]
    [SerializeField] private float minClearanceAboveFollowTarget = 25f;
    [Tooltip("Elevación de las nubes lejanas sobre el horizonte, en grados.") ]
    [SerializeField, Range(5f, 45f)] private float horizonElevationDegrees = 30f;

    [Header("Cobertura total del mundo (FIX INC-074)")]
    [Tooltip("El techo se construye UNA vez y queda fijo (ver comentario de clase), así que solo cubre 'coverRadius' unidades alrededor del punto donde se activó por primera vez. Si el jugador se aleja lo bastante, sale por el borde y ve el skybox despejado en vez de nubes. Cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual y, si supera la mitad de coverRadius, se recoloca el techo YA CONSTRUIDO (solo se mueve _root, sin volver a instanciar nada) centrado en la nueva posición — así las nubes acaban cubriendo todo el mundo según se explora, sin volver a seguir al jugador cada frame (que era lo que causaba el temblor que motivó fijarlo originalmente).")]
    [SerializeField] private float recenterCheckInterval = 2f;
    [Tooltip("Duración del desplazamiento suave del techo al recentrarse alrededor del jugador.")]
    [SerializeField, Min(0.05f)] private float recenterPanDuration = 4f;

    private float _recenterTimer;
    private Coroutine _recenterCoroutine;

    [Header("Aspecto de tormenta")]
    [Tooltip("Color de sombreado de tormenta. Solo se usa en modo QuibliCloud2D (_ShadowColor) y LegacyBaseColor (_BaseColor). En QuibliCloud3D el tono lo pone stormShadingGradient.")]
    [SerializeField] private Color stormCloudColor = new Color(0.42f, 0.43f, 0.47f);
    [Tooltip("Cuánto sombreado de tormenta (_ShadowAmount de Quibli/Cloud2D) tienen las nubes una vez formadas del todo. 0 = nubes blancas de buen tiempo, 1 = panza de tormenta muy marcada. Se anima junto a la formación: mientras la nube llega también se va oscureciendo.")]
    [SerializeField, Range(0f, 1f)] private float stormShadowAmount = 0.55f;
    [Tooltip("Solo con QuibliCloud3D: degradado de sombreado de las nubes de tormenta (de la cara en sombra a la iluminada), en lugar del del material, que es el de nubes blancas de buen tiempo. Nubes blancas sobre el cielo gris de lluvia se leen como nubes sueltas; grises, como un techo continuo. De noche se oscurece según DayNightCycle.PesoDeNoche.")]
    [SerializeField] private Gradient stormShadingGradient = DegradadoDeTormenta();
    [Tooltip("Brillo del degradado de tormenta en plena noche (1 = igual que de día).")]
    [SerializeField, Range(0f, 1f)] private float stormNightBrightness = 0.3f;
    [Tooltip("Shader de los prefabs de nube. QuibliCloud3D (por defecto y recomendado): mallas del Foliage Generator con Quibli/Cloud3D; el fundido 'erosiona' el recorte de alfa (_AlphaThreshold), con efecto de materializarse/disiparse. QuibliCloud2D: quads con Quibli/Cloud2D (_Opacity + _ShadowColor/_ShadowAmount). LegacyBaseColor: comportamiento antiguo (_BaseColor con alfa) para mallas tipo Low Poly.")]
    [SerializeField] private CloudShaderMode cloudShaderMode = CloudShaderMode.QuibliCloud3D;
    [Tooltip("Solo con QuibliCloud3D: valor de _AlphaThreshold cuando la nube está formada del todo (0.5 en el material del demo de Quibli, SampleScene_Cloud3D.mat). El fundido anima desde 1 (invisible) hasta este valor.")]
    [SerializeField, Range(0.05f, 1f)] private float visibleAlphaThreshold = 0.5f;
    [Tooltip("Solo con QuibliCloud2D: activa el billboard del shader (_Billboard) para que cada quad mire siempre a cámara. Si se desactiva, los quads se tumban mirando al suelo con giro aleatorio (aspecto de 'techo' plano).")]
    [SerializeField] private bool billboard = true;

    [Header("Llegada y partida (nubes progresivas)")]
    [Tooltip("Distancia EXTRA (más allá de su hueco en la rejilla, medida en horizontal desde el centro del techo) a la que espera cada nube antes de que le toque formarse, y hasta la que se aleja al irse") ]
    [SerializeField] private float arrivalExtraDistance = 170f;
    [Tooltip("Altura EXTRA sobre su posición final desde la que desciende cada nube al llegar (y a la que vuelve a subir al irse), para reforzar la sensación de que vienen 'de lo alto y lejos' en vez de solo cruzar en horizontal.")]
    [SerializeField] private float arrivalExtraHeight = 65f;
    [Tooltip("Tiempo que separa el inicio de la primera y la última nube de la ola.")]
    [SerializeField] private float waveSpreadDuration = 20f;
    [Tooltip("Duración del recorrido individual de cada nube.")]
    [SerializeField] private float perCloudTravelDuration = 13f;
    [Tooltip("Variación aleatoria (±) del factor que multiplica perCloudTravelDuration en cada nube, para romper la sincronía perfecta entre nubes.")]
    [SerializeField, Range(0f, 0.9f)] private float durationJitter = 0.35f;
    [Tooltip("Multiplicador de duración del recorrido al despejarse mediante CloudsClearing.") ]
    [SerializeField, Range(1f, 3f)] private float departureDurationMultiplier = 1.5f;

    [Header("Corte por cámara")]
    [Tooltip("Completa la retirada de cada nube cuando sus límites quedan fuera del frustum de la cámara de juego.")]
    [SerializeField] private bool cutDepartingCloudsOutsideCamera = true;
    [Tooltip("Cada cuántos frames se recalcula el frustum de la cámara y se comprueba qué nubes en partida han salido de plano. TestPlanesAABB es barato incluso con maxCloudInstances alto, pero no hace falta comprobarlo en TODOS los frames: 1 = cada frame (más preciso, el corte se nota como muchísimo 2-3 frames tarde, imperceptible), 3-5 = casi igual de preciso y bastante más barato con rejillas grandes.")]
    [SerializeField, Range(1, 10)] private int cameraCullCheckEveryNFrames = 3;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int ShadowAmountId = Shader.PropertyToID("_ShadowAmount");
    private static readonly int BillboardId = Shader.PropertyToID("_Billboard");

    private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
    private static readonly int ShadingGradientId = Shader.PropertyToID("_ShadingGradientTexture");
    private const int AnchoDelDegradado = 64;
    private Texture2D _degradadoTormenta;
    private readonly Color32[] _pixelesDelDegradado = new Color32[AnchoDelDegradado];
    private float _brilloDelDegradado = -1f;
    private float _pesoDeNieveDelDegradado = -1f;
    private Color ColorDeNieve => dayNightCycle != null ? dayNightCycle.ColorDeNubesDeNieve : stormCloudColor;

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<CloudUnit> _units = new List<CloudUnit>();
    private MaterialPropertyBlock _mpb;
    private Coroutine _formationCoroutine;
    private Coroutine _buildCoroutine; // FIX (2026-09-05): construcción de nubes ahora repartida en varios frames

    private float _targetFormation;
    private float _safetyHeightBonus;

    private Camera _gameCamera;
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private int _cullFrameCounter;
    private bool _built;

    private bool _building;

    private bool _hiddenByInterior;

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();

        if (cloudShaderMode == CloudShaderMode.QuibliCloud3D)
        {
            _degradadoTormenta = new Texture2D(AnchoDelDegradado, 1, TextureFormat.RGBA32, false)
            {
                name = "[CloudCover] Degradado de tormenta",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            ActualizarDegradado(1f);
        }
    }

    void OnDestroy()
    {
        if (_degradadoTormenta != null) Destroy(_degradadoTormenta);
        _degradadoTormenta = null;
    }

    static Gradient DegradadoDeTormenta()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.30f, 0.32f, 0.36f), 0f),
                new GradientColorKey(new Color(0.42f, 0.44f, 0.49f), 0.45f),
                new GradientColorKey(new Color(0.57f, 0.59f, 0.64f), 0.65f),
                new GradientColorKey(new Color(0.66f, 0.68f, 0.72f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void ActualizarDegradado(float brillo)
    {
        if (_degradadoTormenta == null || stormShadingGradient == null) return;
        for (int i = 0; i < AnchoDelDegradado; i++)
        {
            float nieve = dayNightCycle != null ? dayNightCycle.PesoDeNieve : 0f;
            Color c = Color.Lerp(stormShadingGradient.Evaluate(i / (AnchoDelDegradado - 1f)), ColorDeNieve, nieve) * brillo;
            c.a = 1f;
            _pixelesDelDegradado[i] = c;
        }
        _degradadoTormenta.SetPixels32(_pixelesDelDegradado);
        _degradadoTormenta.Apply(false);
        _brilloDelDegradado = brillo;
        _pesoDeNieveDelDegradado = dayNightCycle != null ? dayNightCycle.PesoDeNieve : 0f;
    }

#if UNITY_EDITOR

    void OnValidate()
    {
        if (scaleRange.x > 5f || scaleRange.y > 5f)
        {
            Debug.LogWarning($"[CloudCoverSpawner] scaleRange ({scaleRange.x:F2}, {scaleRange.y:F2}) parece demasiado alto: los prefabs de nube ya vienen normalizados a ~25-33 unidades de ancho a escala 1, así que valores como (10, 18) producen nubes de 250-600 unidades — probablemente un despiste (multiplicador tecleado como porcentaje entero). Si no es intencional, prueba algo en torno a (0.8, 1.5).", this);
        }
    }
#endif

    void OnEnable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp += HandleCloudsBuildingUp;
            dayNightCycle.CloudsClearing += HandleCloudsClearing;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
        {
            Debug.LogWarning("[CloudCoverSpawner] No se encontró ningún DayNightCycle en la escena; el techo de nubes nunca se activará.");
        }
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _hiddenByInterior = ec != null && ec.IsEffectivelyInterior && DayNightCycle.AnclaDeClima == null; // con una cinemática rodando fuera, el dormitorio no cuenta (INC-416)

        if (!_hiddenByInterior && dayNightCycle != null && dayNightCycle.HayNubesDePrecipitacion)
            HandleCloudsBuildingUp();
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp -= HandleCloudsBuildingUp;
            dayNightCycle.CloudsClearing -= HandleCloudsClearing;
        }

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_formationCoroutine != null)
        {
            StopCoroutine(_formationCoroutine);
            _formationCoroutine = null;
        }

        if (_recenterCoroutine != null)
        {
            StopCoroutine(_recenterCoroutine);
            _recenterCoroutine = null;
        }

        if (_buildCoroutine != null)
        {
            StopCoroutine(_buildCoroutine);
            _buildCoroutine = null;
        }
        _building = false;

        DestroyCover();
    }

    void HandleInteriorEntered()
    {
        _hiddenByInterior = true;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    void HandleInteriorExited()
    {
        _hiddenByInterior = false;

        if (_built && _root != null && (_targetFormation > 0f || AnyUnitVisible()))
            _root.gameObject.SetActive(true);
    }

    bool AnyUnitVisible()
    {
        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i].formationT > 0.001f) return true;
        }
        return false;
    }

    Transform AQuienSigue()
    {
        if (DayNightCycle.AnclaDeClima != null) return DayNightCycle.AnclaDeClima;
        if (PlayerService.Player != null) return PlayerService.Player.transform;
        return Camera.main != null ? Camera.main.transform : null;
    }

    void HandleCloudsBuildingUp()
    {
        if (!_built)
        {
            if (_building) return; // ya en marcha (p.ej. evento + puesta al día de OnEnable en el mismo frame)

            _followTransform = AQuienSigue();

            _building = true;
            _buildCoroutine = StartCoroutine(BuildCoverThenStartWaveRoutine());
            return; // la ola de formación y el ocultado por interior se disparan al terminar de construir (ver corrutina)
        }

        if (_root != null)
        {

            _root.gameObject.SetActive(true);
        }

        StartFormationWave(1f);

        if (_hiddenByInterior && _root != null)
            _root.gameObject.SetActive(false);
    }

    IEnumerator BuildCoverThenStartWaveRoutine()
    {
        yield return StartCoroutine(BuildCoverIfNeededRoutine());
        _building = false;
        _buildCoroutine = null;

        StartFormationWave(1f);

        if (_hiddenByInterior && _root != null)
            _root.gameObject.SetActive(false);
    }

    void HandleCloudsClearing()
    {
        StartFormationWave(0f);
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

        if (!_built || _root == null) return;
        if (_targetFormation <= 0f && _formationCoroutine == null) return;

        if (_degradadoTormenta != null && dayNightCycle != null)
        {
            float brillo = Mathf.Lerp(1f, stormNightBrightness, dayNightCycle.PesoDeNoche);
            if (Mathf.Abs(brillo - _brilloDelDegradado) > 0.03f || Mathf.Abs(dayNightCycle.PesoDeNieve - _pesoDeNieveDelDegradado) > 0.03f) ActualizarDegradado(brillo);
        }

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer < recenterCheckInterval) return;
        _recenterTimer = 0f;

        CheckRecenter();
    }

    void CheckRecenter()
    {
        Transform playerT = AQuienSigue() ?? _followTransform;
        if (playerT == null) return;

        Vector3 rootPos = _root.position;
        float dx = playerT.position.x - rootPos.x;
        float dz = playerT.position.z - rootPos.z;
        float distSqr = dx * dx + dz * dz;

        float recenterThreshold = coverRadius * 0.5f;
        if (distSqr <= recenterThreshold * recenterThreshold) return;

        Vector3 newPos = playerT.position;

        newPos.y = playerT.position.y + cloudHeight + _safetyHeightBonus;
        _followTransform = playerT;

        if (_recenterCoroutine != null)
            StopCoroutine(_recenterCoroutine);
        _recenterCoroutine = StartCoroutine(SmoothRecenterRoutine(newPos));
    }

    IEnumerator SmoothRecenterRoutine(Vector3 targetPos)
    {
        Vector3 start = _root.position;
        float duration = Mathf.Max(0.05f, recenterPanDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            _root.position = Vector3.Lerp(start, targetPos, eased);
            yield return null;
        }

        _root.position = targetPos;
        _recenterCoroutine = null;
    }

    IEnumerator BuildCoverIfNeededRoutine()
    {
        if (_built || cloudPrefabs == null || cloudPrefabs.Length == 0) yield break;

        _root = new GameObject("[CloudCover]").transform;

        _safetyHeightBonus = 0f;
        if (_followTransform != null)
        {
            Vector3 rootPos = _followTransform.position;
            rootPos.y += cloudHeight;
            _root.position = rootPos;
        }

        int half = Mathf.Max(1, Mathf.CeilToInt(coverRadius / Mathf.Max(1f, cellSize)));
        float radiusSqr = coverRadius * coverRadius;
        float jitterRange = cellSize * jitter * 0.5f;

        float tanElev = Mathf.Tan(horizonElevationDegrees * Mathf.Deg2Rad);
        int spawned = 0;
        int spawnedSinceYield = 0;

        for (int gx = -half; gx <= half && spawned < maxCloudInstances; gx++)
        {
            for (int gz = -half; gz <= half && spawned < maxCloudInstances; gz++)
            {

                int staggerRow = (((gz % 2) + 2) % 2); // 0/1 normalizado (válido con gz negativo)
                float baseX = gx * cellSize + staggerRow * (cellSize * 0.5f);
                float baseZ = gz * cellSize;
                if (baseX * baseX + baseZ * baseZ > radiusSqr) continue;

                var prefab = cloudPrefabs[UnityEngine.Random.Range(0, cloudPrefabs.Length)];
                if (prefab == null) continue;

                float x = baseX + UnityEngine.Random.Range(-jitterRange, jitterRange);
                float z = baseZ + UnityEngine.Random.Range(-jitterRange, jitterRange);
                float radial = Mathf.Sqrt(baseX * baseX + baseZ * baseZ);
                float extraHeight = Mathf.Max(0f, radial * tanElev - cloudHeight);
                float y = extraHeight + UnityEngine.Random.Range(-heightJitter, heightJitter);
                var targetLocalPos = new Vector3(x, y, z);

                var instance = Instantiate(prefab, _root);
                instance.transform.localPosition = targetLocalPos;
                instance.transform.localRotation = CloudRotation();
                instance.transform.localScale = prefab.transform.localScale * UnityEngine.Random.Range(scaleRange.x, scaleRange.y);
                EnsureMinFootprint(instance.transform);

                var unit = new CloudUnit
                {
                    transform = instance.transform,
                    targetLocalPos = targetLocalPos,
                    delayOffset01 = UnityEngine.Random.value,
                    durationMultiplier = 1f + UnityEngine.Random.Range(-durationJitter, durationJitter),
                    formationT = 0f,
                };
                unit.renderers = CollectRenderers(instance);
                _units.Add(unit);

                spawned++;
                spawnedSinceYield++;

                if (spawnedSinceYield >= maxCloudsInstantiatedPerFrame)
                {
                    spawnedSinceYield = 0;
                    yield return null;
                }
            }
        }

        ApplySafetyClearance();

        for (int i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            Vector2 horizontal = new Vector2(unit.targetLocalPos.x, unit.targetLocalPos.z);
            Vector2 outwardDir = horizontal.sqrMagnitude > 0.01f
                ? horizontal.normalized
                : UnityEngine.Random.insideUnitCircle.normalized;

            Vector3 far = unit.targetLocalPos + new Vector3(outwardDir.x, 0f, outwardDir.y) * arrivalExtraDistance;
            far.y += arrivalExtraHeight;
            unit.farLocalPos = far;

            unit.transform.localPosition = far;
            ApplyUnitVisual(unit);
        }

        _built = true;
    }

    void EnsureMinFootprint(Transform cloud)
    {
        if (minFootprintInCells <= 0f || cloudShaderMode == CloudShaderMode.QuibliCloud2D) return;

        var filters = cloud.GetComponentsInChildren<MeshFilter>();
        float sizeX = 0f, sizeZ = 0f;
        for (int i = 0; i < filters.Length; i++)
        {
            var mf = filters[i];
            if (mf == null || mf.sharedMesh == null) continue;
            Vector3 size = mf.sharedMesh.bounds.size;
            Vector3 scale = mf.transform.lossyScale;
            sizeX = Mathf.Max(sizeX, size.x * Mathf.Abs(scale.x));
            sizeZ = Mathf.Max(sizeZ, size.z * Mathf.Abs(scale.z));
        }
        if (sizeX <= 0.01f || sizeZ <= 0.01f) return;

        float wanted = minFootprintInCells * cellSize;
        float kx = Mathf.Max(1f, wanted / sizeX);
        float kz = Mathf.Max(1f, wanted / sizeZ);
        if (kx <= 1f && kz <= 1f) return;

        Vector3 local = cloud.localScale;
        cloud.localScale = new Vector3(local.x * kx, local.y, local.z * kz);
    }

    Quaternion CloudRotation()
    {
        if (cloudShaderMode == CloudShaderMode.QuibliCloud2D)
            return billboard
                ? Quaternion.identity
                : Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);

        return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
    }

    void ApplySafetyClearance()
    {
        if (_followTransform == null || _renderers.Count == 0) return;

        Bounds combined = default;
        bool has = false;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        if (!has) return;

        float requiredMinY = _followTransform.position.y + minClearanceAboveFollowTarget;
        float deficit = requiredMinY - combined.min.y;
        if (deficit <= 0f) return;

        _safetyHeightBonus = deficit;
        _root.position += Vector3.up * deficit;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[CloudCoverSpawner] El techo de nubes no dejaba suficiente margen sobre el jugador (faltaban {deficit:F1} unidades); se ha subido automáticamente. Considera aumentar cloudHeight o reducir scaleRange para no depender de esta corrección.");
#endif
    }

    Renderer[] CollectRenderers(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {

            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _renderers.Add(r);
        }
        return renderers;
    }

    void StartFormationWave(float target)
    {
        _targetFormation = target;
        if (_formationCoroutine != null)
            StopCoroutine(_formationCoroutine);
        _formationCoroutine = StartCoroutine(FormationWaveRoutine(target));
    }

    Camera ResolveGameCamera()
    {
        if (_gameCamera != null && _gameCamera.isActiveAndEnabled) return _gameCamera;
        _gameCamera = Camera.main;
        return _gameCamera;
    }

    static Bounds GetUnitWorldBounds(CloudUnit unit)
    {
        var renderers = unit.renderers;
        Bounds combined = default;
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        return combined;
    }

    IEnumerator FormationWaveRoutine(float target)
    {
        bool departing = target <= 0f;
        float perCloudBase = perCloudTravelDuration;
        if (departing)
            perCloudBase *= Mathf.Max(1f, departureDurationMultiplier);

        float maxTime = waveSpreadDuration + perCloudBase * (1f + durationJitter) + 1f;
        float elapsed = 0f;
        bool allDone = false;
        _cullFrameCounter = 0;

        while (!allDone && elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            allDone = true;

            bool doCullCheck = false;
            if (departing && cutDepartingCloudsOutsideCamera)
            {
                var gameCam = ResolveGameCamera();
                if (gameCam != null)
                {
                    _cullFrameCounter++;
                    if (_cullFrameCounter >= Mathf.Max(1, cameraCullCheckEveryNFrames))
                    {
                        _cullFrameCounter = 0;
                        GeometryUtility.CalculateFrustumPlanes(gameCam, _frustumPlanes);
                        doCullCheck = true;
                    }
                }
            }

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                float cloudDelay = unit.delayOffset01 * waveSpreadDuration;
                if (elapsed < cloudDelay)
                {
                    allDone = false;
                    continue;
                }

                float duration = Mathf.Max(0.05f, perCloudBase * unit.durationMultiplier);
                float maxDelta = Time.deltaTime / duration;
                unit.formationT = Mathf.MoveTowards(unit.formationT, target, maxDelta);

                if (doCullCheck && unit.formationT > 0f
                    && !GeometryUtility.TestPlanesAABB(_frustumPlanes, GetUnitWorldBounds(unit)))
                {
                    unit.formationT = 0f;
                }

                ApplyUnitVisual(unit);

                if (Mathf.Abs(unit.formationT - target) > 0.001f)
                    allDone = false;
            }

            yield return null;
        }

        for (int i = 0; i < _units.Count; i++)
        {
            _units[i].formationT = target;
            ApplyUnitVisual(_units[i]);
        }

        _formationCoroutine = null;

        if (target <= 0f)
            DeactivateCover();
    }

    void ApplyUnitVisual(CloudUnit unit)
    {
        float t = Mathf.Clamp01(unit.formationT);
        float eased = t * t * (3f - 2f * t); // smoothstep: acelera al salir, se asienta al llegar

        unit.transform.localPosition = Vector3.Lerp(unit.farLocalPos, unit.targetLocalPos, eased);

        Color colorDeNube = Color.Lerp(stormCloudColor, ColorDeNieve, dayNightCycle != null ? dayNightCycle.PesoDeNieve : 0f)
            * Mathf.Lerp(1f, stormNightBrightness, dayNightCycle != null ? dayNightCycle.PesoDeNoche : 0f);
        Color legacyColor = colorDeNube;
        legacyColor.a = eased;

        float shadowAmount = stormShadowAmount * eased;
        float billboardValue = billboard ? 1f : 0f;

        float alphaThreshold = Mathf.Lerp(1.01f, visibleAlphaThreshold, eased);

        var renderers = unit.renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            switch (cloudShaderMode)
            {
                case CloudShaderMode.QuibliCloud3D:
                    _mpb.SetFloat(AlphaThresholdId, alphaThreshold);
                    if (_degradadoTormenta != null) _mpb.SetTexture(ShadingGradientId, _degradadoTormenta);
                    break;
                case CloudShaderMode.QuibliCloud2D:
                    _mpb.SetFloat(OpacityId, eased);
                    _mpb.SetColor(ShadowColorId, colorDeNube);
                    _mpb.SetFloat(ShadowAmountId, shadowAmount);
                    _mpb.SetFloat(BillboardId, billboardValue);
                    break;
                default:
                    _mpb.SetColor(BaseColorId, legacyColor);
                    break;
            }
            r.SetPropertyBlock(_mpb);
        }
    }

    void DeactivateCover()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    void DestroyCover()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _renderers.Clear();
        _units.Clear();
        _built = false;
        _targetFormation = 0f;
    }

    [ContextMenu("Activar/Desactivar techo de nubes (debug)")]
    public void DebugToggleCover()
    {
        if (_built && (_targetFormation > 0f || AnyUnitVisible()))
            HandleCloudsClearing();
        else
            HandleCloudsBuildingUp();
    }

    public void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r != null) r.enabled = visible;
        }
    }
}
