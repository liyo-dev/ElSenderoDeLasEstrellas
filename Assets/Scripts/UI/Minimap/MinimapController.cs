using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Singleton que orquesta el sistema de minimapa circular:
/// - Sigue al jugador con la cámara ortográfica
/// - Rota la flecha del jugador según su orientación
/// - Oculta el minimapa al entrar en interiores
/// </summary>
[DisallowMultipleComponent]
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [Header("Cámara")]
    [SerializeField] Camera minimapCamera;
    [SerializeField] RenderTexture renderTexture;
    [Tooltip("Altura mínima de la cámara. Si hay terreno más alto, la cámara se coloca por encima de él " +
             "(más Margen Sobre Terreno): lo que queda por encima de la cámara no se dibuja y por el hueco " +
             "se vería lo que hay debajo, normalmente el mar. Ver INC-505.")]
    [SerializeField] float cameraHeight = 200f;
    [Tooltip("Metros que la cámara queda por encima del punto más alto del terreno cargado.")]
    [SerializeField] float margenSobreTerreno = 20f;
    [SerializeField] float defaultZoom = 25f;

    [Header("UI")]
    [SerializeField] GameObject minimapRoot;
    [SerializeField] RawImage minimapImage;
    [SerializeField] RectTransform playerArrow;

    [Tooltip("Lado de la flecha del jugador en el minimapa normal (px).")]
    [SerializeField] float playerArrowSize = 36f;

    [Header("Bounds (opcional)")]
    [SerializeField] MinimapBounds worldBounds;

    [Header("Mapa grande")]
    [Tooltip("Tamaño ortográfico de la cámara cuando el mapa grande está abierto (ver BigMapController), " +
             "para mostrar más área del mundo que el zoom normal (defaultZoom/worldBounds).")]
    [SerializeField] float bigMapZoom = 60f;

    [Tooltip("Zoom más cercano al que se puede acercar el mapa grande (tamaño ortográfico).")]
    [SerializeField] float bigMapZoomMin = 20f;

    [Tooltip("Zoom más lejano al que se puede alejar el mapa grande (tamaño ortográfico).")]
    [SerializeField] float bigMapZoomMax = 160f;

    [Tooltip("Distancia máxima (m) a la que se puede desplazar la vista del mapa grande desde el jugador.")]
    [SerializeField] float bigMapMaxPan = 250f;

    [Tooltip("Tamaño en pantalla de la flecha y los iconos en el mapa grande, respecto al minimapa normal. " +
             "El mapa se amplía, pero ellos no crecen con él.")]
    [SerializeField] float bigMapMarkerScale = 1.4f;

    [Header("Agua en el minimapa")]
    [Tooltip("El agua del mundo (mar, ríos) usa shaders pensados para verse a ras de suelo (reflejos, " +
             "espuma, profundidad calculada a partir de la textura de profundidad de la propia cámara) " +
             "que, vistos desde la cámara ortográfica del minimapa, se leen como una " +
             "mancha plana sin detalle — y si el terreno de alrededor queda por debajo del nivel del mar " +
             "en algún punto, esa mancha puede llegar a tapar terreno e iconos (INC-197, 12 sept 2026). " +
             "Con esto activo, mientras renderiza esta cámara se sustituye el material de esas superficies " +
             "por uno plano y simple (Resources/Shaders/Mat_MinimapAgua), pensado para leerse bien desde " +
             "arriba, sin tocar su aspecto en la cámara principal.")]
    [SerializeField] bool overrideWaterOnMinimap = true;

    Transform _playerTransform;
    float _alturaCamara;
    bool _bigMapMode;
    float _rootScale = 1f;
    Vector2 _panOffset;
    bool _hiddenByInterior;
    bool _hiddenByBattle;
    bool _hiddenByMenu;
    bool _hiddenByCinematic;
    float _normalOrthoSize;
    bool _fogWasEnabledBeforeMinimap;
    bool _minimapFogOverrideActive;

    Renderer[] _waterRenderers;
    Material[] _waterOriginalMaterials;
    Material _minimapWaterMaterial;
    bool _waterCacheReady;
    bool _minimapWaterOverrideActive;

    // ── API para MinimapUIController ─────────────────────────────────────────
    public Vector3 PlayerPosition => _playerTransform != null ? _playerTransform.position : Vector3.zero;
    public float OrthoSize => minimapCamera != null ? minimapCamera.orthographicSize : 0f;

    /// <summary>Punto del mundo en el centro del minimapa (el jugador, más el desplazamiento del mapa grande).</summary>
    public Vector3 ViewCenter => PlayerPosition + new Vector3(_panOffset.x, 0f, _panOffset.y);

    /// <summary>
    /// Escala local que deben llevar flecha e iconos para verse a su tamaño correcto:
    /// 1 en el minimapa; en el mapa grande compensa la ampliación del mapa.
    /// </summary>
    public float MarkerScale => _bigMapMode ? bigMapMarkerScale / Mathf.Max(0.01f, _rootScale) : 1f;

    float MapRadius => minimapImage != null ? minimapImage.rectTransform.rect.width * 0.5f : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Suscribir aquí (no en OnEnable) para que los eventos lleguen aunque
        // minimapRoot esté desactivado (lo que haría llamar OnDisable en este componente).
        EnvironmentController.OnInteriorEntered += OnInteriorEntered;
        EnvironmentController.OnInteriorExited  += OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  += OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    += OnBattleEnded;
        MenuManager.MenuOpened                  += OnMenuOpened;
        MenuManager.MenuClosed                  += OnMenuClosed;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering   += OnEndCameraRendering;
        SceneManager.sceneLoaded                   += OnSceneLoaded;
        SceneManager.sceneUnloaded                 += OnSceneUnloaded;

        SetupCamera();

        if (playerArrow != null)
            playerArrow.sizeDelta = Vector2.one * playerArrowSize;
    }

    void OnDestroy()
    {
        EnvironmentController.OnInteriorEntered -= OnInteriorEntered;
        EnvironmentController.OnInteriorExited  -= OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  -= OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    -= OnBattleEnded;
        MenuManager.MenuOpened                  -= OnMenuOpened;
        MenuManager.MenuClosed                  -= OnMenuClosed;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering   -= OnEndCameraRendering;
        SceneManager.sceneLoaded                   -= OnSceneLoaded;
        SceneManager.sceneUnloaded                 -= OnSceneUnloaded;
    }

    void Start()
    {
        RefreshMinimapVisibility();
    }

    void SetupCamera()
    {
        if (minimapCamera == null) return;

        if (renderTexture != null)
        {
            minimapCamera.targetTexture = renderTexture;

            if (minimapImage != null)
                minimapImage.texture = renderTexture;
        }

        // La cámara siempre mira hacia abajo
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Si hay bounds definidos, ajustar el zoom para cubrir todo el mundo
        if (worldBounds != null)
        {
            var size = worldBounds.WorldSize;
            minimapCamera.orthographicSize = Mathf.Max(size.x, size.y) * 0.5f;
        }
        else
        {
            minimapCamera.orthographicSize = defaultZoom;
        }

        _normalOrthoSize = minimapCamera.orthographicSize;
        RecalcularAlturaCamara();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RecalcularAlturaCamara();
    void OnSceneUnloaded(Scene scene) => RecalcularAlturaCamara();

    /// <summary>
    /// Coloca la cámara por encima del terreno más alto cargado y alarga el plano lejano hasta el más
    /// bajo. El terreno que queda por encima de la cámara no se dibuja, y por ese hueco se ve lo que hay
    /// debajo (el plano del mar), con la forma de la curva de nivel. Solo se calcula al cargar o
    /// descargar escenas. Ver INC-505.
    /// </summary>
    void RecalcularAlturaCamara()
    {
        _alturaCamara = cameraHeight;
        if (minimapCamera == null) return;

        float techo = float.NegativeInfinity;
        float suelo = float.PositiveInfinity;
        foreach (var terreno in Terrain.activeTerrains)
        {
            if (terreno == null || terreno.terrainData == null) continue;
            var limites = terreno.terrainData.bounds;
            float baseY = terreno.GetPosition().y;
            techo = Mathf.Max(techo, baseY + limites.max.y);
            suelo = Mathf.Min(suelo, baseY + limites.min.y);
        }
        if (float.IsInfinity(techo)) return;

        _alturaCamara = Mathf.Max(cameraHeight, techo + margenSobreTerreno);
        minimapCamera.farClipPlane = Mathf.Max(minimapCamera.farClipPlane, _alturaCamara - suelo + margenSobreTerreno);
    }

    /// <summary>
    /// Entra o sale del modo mapa grande (ver BigMapController). <paramref name="rootScale"/> es
    /// cuánto se ha ampliado la UI del minimapa, para que flecha e iconos no crezcan con ella.
    /// Al entrar y al salir la vista vuelve a centrarse en el jugador.
    /// </summary>
    public void SetBigMapMode(bool active, float rootScale)
    {
        _bigMapMode = active;
        _rootScale = active ? rootScale : 1f;
        _panOffset = Vector2.zero;

        if (minimapCamera != null)
            minimapCamera.orthographicSize = active ? Mathf.Clamp(bigMapZoom, bigMapZoomMin, bigMapZoomMax) : _normalOrthoSize;

        if (playerArrow != null)
        {
            playerArrow.localScale = Vector3.one * MarkerScale;
            playerArrow.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Desplaza la vista del mapa grande (metros en X/Z del mundo).</summary>
    public void PanBigMap(Vector2 worldDelta)
    {
        if (!_bigMapMode) return;
        _panOffset = Vector2.ClampMagnitude(_panOffset + worldDelta, bigMapMaxPan);
    }

    /// <summary>Desplaza la vista del mapa grande según un arrastre en píxeles de pantalla.</summary>
    public void PanBigMapByScreenDelta(Vector2 screenDelta)
    {
        if (!_bigMapMode || minimapImage == null || minimapCamera == null) return;

        float radiusOnScreen = MapRadius * minimapImage.rectTransform.lossyScale.x;
        if (radiusOnScreen <= 0f) return;

        PanBigMap(screenDelta * (minimapCamera.orthographicSize / radiusOnScreen));
    }

    /// <summary>Multiplica el zoom del mapa grande (&lt;1 acerca, &gt;1 aleja), dentro de sus límites.</summary>
    public void ZoomBigMap(float factor)
    {
        if (!_bigMapMode || minimapCamera == null || factor <= 0f) return;
        minimapCamera.orthographicSize = Mathf.Clamp(minimapCamera.orthographicSize * factor, bigMapZoomMin, bigMapZoomMax);
    }

    /// <summary>Vuelve a centrar el mapa grande en el jugador.</summary>
    public void RecenterBigMap() => _panOffset = Vector2.zero;


    void Update()
    {
        ResolvePlayer();

        if (minimapCamera == null) return;

        // Mantener la rotación top-down cada frame (por si algo la resetea)
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_playerTransform == null) return;

        // Seguir al jugador en XZ (más el desplazamiento del mapa grande)
        Vector3 center = ViewCenter;
        minimapCamera.transform.position = new Vector3(center.x, _alturaCamara, center.z);

        // Rotar la flecha según la orientación Y del jugador (offset 90° porque el sprite apunta a la derecha)
        if (playerArrow != null)
        {
            playerArrow.localEulerAngles = new Vector3(0f, 0f, 90f - _playerTransform.eulerAngles.y);

            // Con la vista desplazada, la flecha se queda sobre el jugador (la máscara la recorta si sale).
            if (_bigMapMode && minimapCamera.orthographicSize > 0f)
                playerArrow.anchoredPosition = -_panOffset * (MapRadius / minimapCamera.orthographicSize);
        }
    }

    void ResolvePlayer()
    {
        if (_playerTransform != null) return;

        _playerTransform = PlayerService.PlayerTransform;

        if (_playerTransform == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) _playerTransform = go.transform;
        }
    }

    void OnInteriorEntered()
    {
        _hiddenByInterior = true;
        RefreshMinimapVisibility();
    }

    void OnInteriorExited()
    {
        _hiddenByInterior = false;
        RefreshMinimapVisibility();
    }

    void OnBattleStarted()
    {
        _hiddenByBattle = true;
        RefreshMinimapVisibility();
    }

    void OnBattleEnded()
    {
        _hiddenByBattle = false;
        RefreshMinimapVisibility();
    }

    void OnMenuOpened(MenuKind kind)
    {
        // El mapa grande ES el minimapa (minimapRoot ampliado por BigMapController), no un menú
        // aparte por encima: ocultarlo aquí lo apagaría justo al abrirlo. Los demás menús sí deben
        // ocultar el minimapa como hasta ahora.
        if (kind == MenuKind.BigMap) return;

        _hiddenByMenu = true;
        RefreshMinimapVisibility();
    }

    void OnMenuClosed(MenuKind kind)
    {
        if (kind == MenuKind.BigMap) return;

        _hiddenByMenu = MenuManager.AnyOpenExcept(MenuKind.BigMap);
        RefreshMinimapVisibility();
    }

    // ── Niebla global desactivada solo mientras renderiza esta cámara ──────────
    // RenderSettings.fog es un estado GLOBAL (no existe culling mask para niebla), así
    // que sin esto la niebla de lluvia/tormenta/niebla de DayNightCycle (rainFogDensityMultiplier,
    // etc.) se aplicaba también al minimapa. Al ser una cámara ortográfica top-down con altura
    // fija (cameraHeight), la distancia cámara-suelo es prácticamente constante en toda la
    // imagen — a diferencia de la cámara principal, donde la niebla degrada con la distancia,
    // aquí toda la textura del minimapa se "blanqueaba" de golpe y uniformemente en cuanto subía
    // la densidad de niebla (INC: minimapa en blanco durante la lluvia, 1 sep 2026).
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != minimapCamera) return;

        _fogWasEnabledBeforeMinimap = RenderSettings.fog;
        RenderSettings.fog = false;
        _minimapFogOverrideActive = true;

        if (overrideWaterOnMinimap)
        {
            CacheWaterRenderersOnce();
            ApplyMinimapWaterOverride();
        }
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != minimapCamera) return;

        if (_minimapFogOverrideActive)
        {
            RenderSettings.fog = _fogWasEnabledBeforeMinimap;
            _minimapFogOverrideActive = false;
        }

        RestoreMinimapWaterOverride();
    }

    // ── Agua "plana" solo para el minimapa (INC-197) ───────────────────────────
    // Los shaders de agua del mundo (mar/ríos de la maqueta de Eldoria, y el agua base de MainWorld)
    // están pensados para verse a ras de suelo y, en algunos casos, leen la textura de profundidad de
    // la propia cámara para teñir orilla/espuma — algo que en la cámara ortográfica del minimapa (200 m
    // de altura, sin relación con la vista normal) se lee como una mancha plana y, si el terreno de
    // alrededor queda por debajo del nivel del mar en algún punto, puede tapar terreno e iconos que
    // deberían verse. Se detecta una sola vez (no en Update) cualquier Renderer cuyo material use un
    // shader de agua conocido del proyecto, y se sustituye su material SOLO mientras renderiza esta
    // cámara por uno plano (Resources/Shaders/Mat_MinimapAgua) — igual de "agua" a simple vista, pero
    // sin depender de la cámara que lo mira.
    static bool EsShaderDeAgua(Shader shader)
    {
        if (shader == null) return false;
        var nombre = shader.name;
        return nombre.Contains("Eldoria/Agua") || nombre.Contains("WaterURP") || nombre.Contains("Water_Final");
    }

    void CacheWaterRenderersOnce()
    {
        if (_waterCacheReady) return;
        _waterCacheReady = true;

        _minimapWaterMaterial = Resources.Load<Material>("Shaders/Mat_MinimapAgua");
        if (_minimapWaterMaterial == null)
        {
            _waterRenderers = System.Array.Empty<Renderer>();
            return;
        }

        var encontrados = new System.Collections.Generic.List<Renderer>();
        foreach (var renderer in FindObjectsByType<Renderer>())
        {
            if (EsShaderDeAgua(renderer.sharedMaterial != null ? renderer.sharedMaterial.shader : null))
                encontrados.Add(renderer);
        }
        _waterRenderers = encontrados.ToArray();
        _waterOriginalMaterials = new Material[_waterRenderers.Length];
    }

    void ApplyMinimapWaterOverride()
    {
        if (_minimapWaterOverrideActive || _waterRenderers == null || _minimapWaterMaterial == null) return;

        for (int i = 0; i < _waterRenderers.Length; i++)
        {
            var renderer = _waterRenderers[i];
            if (renderer == null) continue; // pudo destruirse tras el cacheo inicial
            _waterOriginalMaterials[i] = renderer.sharedMaterial;
            renderer.sharedMaterial = _minimapWaterMaterial;
        }
        _minimapWaterOverrideActive = true;
    }

    void RestoreMinimapWaterOverride()
    {
        if (!_minimapWaterOverrideActive) return;

        for (int i = 0; i < _waterRenderers.Length; i++)
        {
            var renderer = _waterRenderers[i];
            if (renderer == null) continue;
            renderer.sharedMaterial = _waterOriginalMaterials[i];
        }
        _minimapWaterOverrideActive = false;
    }

    /// <summary>
    /// Oculta/muestra el minimapa durante cinemáticas que no pasan por interior/batalla/menú
    /// (p. ej. KingdomExitTransitionNode al revelar el título del juego).
    /// </summary>
    public void SetHiddenByCinematic(bool hidden)
    {
        _hiddenByCinematic = hidden;
        RefreshMinimapVisibility();
    }

    void RefreshMinimapVisibility()
    {
        if (minimapRoot != null)
            minimapRoot.SetActive(!_hiddenByInterior && !_hiddenByBattle && !_hiddenByMenu && !_hiddenByCinematic);
    }
}
