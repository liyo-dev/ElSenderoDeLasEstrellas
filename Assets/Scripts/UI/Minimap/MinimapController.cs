using UnityEngine;
using UnityEngine.Rendering;
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
    [SerializeField] float cameraHeight = 200f;
    [SerializeField] float defaultZoom = 25f;

    [Header("UI")]
    [SerializeField] GameObject minimapRoot;
    [SerializeField] RawImage minimapImage;
    [SerializeField] RectTransform playerArrow;

    [Header("Bounds (opcional)")]
    [SerializeField] MinimapBounds worldBounds;

    [Header("Mapa grande")]
    [Tooltip("Tamaño ortográfico de la cámara cuando el mapa grande está abierto (ver BigMapController), " +
             "para mostrar más área del mundo que el zoom normal (defaultZoom/worldBounds).")]
    [SerializeField] float bigMapZoom = 60f;

    [Header("Agua en el minimapa")]
    [Tooltip("El agua del mundo (mar, ríos) usa shaders pensados para verse a ras de suelo (reflejos, " +
             "espuma, profundidad calculada a partir de la textura de profundidad de la propia cámara) " +
             "que, vistos desde la cámara ortográfica del minimapa a 200 m de altura, se leen como una " +
             "mancha plana sin detalle — y si el terreno de alrededor queda por debajo del nivel del mar " +
             "en algún punto, esa mancha puede llegar a tapar terreno e iconos (INC-197, 12 sept 2026). " +
             "Con esto activo, mientras renderiza esta cámara se sustituye el material de esas superficies " +
             "por uno plano y simple (Resources/Shaders/Mat_MinimapAgua), pensado para leerse bien desde " +
             "arriba, sin tocar su aspecto en la cámara principal.")]
    [SerializeField] bool overrideWaterOnMinimap = true;

    Transform _playerTransform;
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

        SetupCamera();
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
    }

    /// <summary>
    /// Activa/desactiva el zoom ampliado de la cámara para el mapa grande (ver BigMapController).
    /// Al desactivarlo restaura el zoom normal (el mismo que calculó SetupCamera a partir de
    /// worldBounds o defaultZoom).
    /// </summary>
    public void SetBigMapMode(bool active)
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographicSize = active ? bigMapZoom : _normalOrthoSize;
    }


    void Update()
    {
        ResolvePlayer();

        if (minimapCamera == null) return;

        // Mantener la rotación top-down cada frame (por si algo la resetea)
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_playerTransform == null) return;

        // Seguir al jugador en XZ
        minimapCamera.transform.position = new Vector3(
            _playerTransform.position.x,
            cameraHeight,
            _playerTransform.position.z);

        // Rotar la flecha según la orientación Y del jugador (offset 90° porque el sprite apunta a la derecha)
        if (playerArrow != null)
            playerArrow.localEulerAngles = new Vector3(0f, 0f, 90f - _playerTransform.eulerAngles.y);
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
        return nombre.Contains("Eldoria/Agua") || nombre.Contains("WaterURP");
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
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
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
