using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnvironmentController : MonoBehaviour
{
    public static EnvironmentController Instance { get; private set; }

    /// <summary>Disparado al entrar en un interior. Usado por MinimapController para ocultar el minimapa.</summary>
    public static event Action OnInteriorEntered;
    /// <summary>Disparado al volver al exterior. Usado por MinimapController para mostrar el minimapa.</summary>
    public static event Action OnInteriorExited;

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnInteriorEntered = null;
        OnInteriorExited = null;
    }
    #endif

    [Header("Opcional")]
    public Material exteriorSkyboxOverride;   // si lo pones, se usará al volver a exterior
    public Camera targetCamera;               // si lo dejas vacío, se resuelve solo

    EnvironmentMode _modeValue = EnvironmentMode.Unknown;
    // Cada asignación a _mode también sincroniza EnvironmentQuery.IsInterior (definido en
    // Assets/Plugins, ensamblado "first pass"). Es el único modo en que vThirdPersonCamera
    // (Invector, en Plugins) puede saber si estamos en interior sin poder referenciar
    // EnvironmentController directamente (Plugins compila antes que Assembly-CSharp).
    EnvironmentMode _mode
    {
        get => _modeValue;
        set
        {
            _modeValue = value;
            EnvironmentQuery.IsInterior = value == EnvironmentMode.Interior;
        }
    }

    // snapshot del “exterior” (para restaurar al salir)
    Material _savedRenderSettingsSkybox;
    public Material SavedExteriorSkybox => _savedRenderSettingsSkybox; // Exponer para cinemáticas

    Material _savedCameraSkyboxMat;
    CameraClearFlags _savedClearFlags = CameraClearFlags.Skybox;
    bool _savedHadCamSkybox;
    bool _hasSnapshot;

    // tracking de cámara / re-aplicación
    Camera _cam;              // cámara resuelta actual
    Camera _appliedCam;       // cámara sobre la que aplicamos por última vez
    bool _needReapply;        // si true, re-aplicamos en Update
    AnchorEnvironment _currentInterior; // último env de interior aplicado (para re-aplicar)

    // Gestión de zonas visibles
    GameObject[] _hiddenZones;       // zonas que ocultamos al entrar
    // FIX (12 sep 2026, pedido de Raúl): el mundo exterior de MainWorld está repartido en más de un
    // GameObject de nivel superior (p. ej. "WORLD" para terreno/edificios y "AI" para los NPCs) en
    // vez de uno solo. Antes solo se podía ocultar UN root (exteriorWorldRootName, string). Ahora se
    // oculta la unión de exteriorWorldRootName (compatibilidad con anchors ya configurados, p. ej.
    // el de WillHouse.unity) + exteriorWorldRootNames (array nuevo, para el resto).
    ExteriorWorldRoot[] _hiddenExteriors;   // mundo exterior ocultado (uno o varios roots) —
                                      // solo controla renderizado, ver ExteriorWorldRoot.SetVisible
    float _savedFarClipPlane;        // far clip original de la cámara
    bool _farClipModified;

    // === CONTROL CINEMÁTICO ===
    // Permite que las cinemáticas tomen control temporal del entorno sin conflictos
    private bool _cinematicOverrideActive;
    private EnvironmentMode _preCinematicMode;
    private AnchorEnvironment _preCinematicInterior;
    // true cuando ApplyInterior fue bloqueado por cinemática activa: reintentar al terminar
    private bool _cinematicReapplyPending;
    // true mientras el override cinemático está mostrando un interior (última llamada fue
    // ApplyInteriorForCinematic, no ApplyExteriorForCinematic). Necesario porque _mode NO cambia
    // durante el override (a propósito), así que sistemas externos como DayNightCycle (lluvia/niebla)
    // no pueden fiarse solo de CurrentMode para saber si deben suprimirse visualmente. Ver IsEffectivelyInterior.
    private bool _cinematicShowingInterior;

    /// <summary>
    /// Indica si actualmente hay un override cinemático activo
    /// </summary>
    public bool IsCinematicOverrideActive => _cinematicOverrideActive;

    /// <summary>
    /// El modo de entorno actual (Interior/Exterior)
    /// </summary>
    public EnvironmentMode CurrentMode => _mode;

    /// <summary>
    /// El interior actual (si estamos en modo Interior)
    /// </summary>
    public AnchorEnvironment CurrentInterior => _currentInterior;

    /// <summary>
    /// True si, desde el punto de vista de sistemas externos (lluvia/niebla, minimapa, etc.),
    /// el jugador está "efectivamente" en un interior: o bien CurrentMode ya es Interior (flujo
    /// real de ApplyInterior/ApplyExterior), o bien hay un override cinemático activo que está
    /// mostrando un interior (ApplyInteriorForCinematic). Usar esto en vez de CurrentMode a secas
    /// para cualquier efecto que deba desaparecer dentro de un interior (p.ej. lluvia), porque
    /// CurrentMode no se actualiza durante cinemáticas.
    /// </summary>
    public bool IsEffectivelyInterior =>
        _mode == EnvironmentMode.Interior || (_cinematicOverrideActive && _cinematicShowingInterior);

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Garantizar que Update() corra aunque el componente esté desactivado en escena
        enabled = true;
        DontDestroyOnLoad(gameObject);

        // FIX M11 (auditoría 2026-08-07): antes se suscribía con lambdas anónimas, imposibles de
        // desuscribir. Este objeto es DontDestroyOnLoad y normalmente vive toda la sesión, así que
        // en juego real esto no fuga — pero en el editor, al parar Play sin domain reload, Unity
        // destruye los DontDestroyOnLoad y dispara OnDestroy: sin poder desuscribir la lambda, la
        // referencia se quedaba colgada de SceneManager (evento estático que sobrevive entre
        // sesiones de PlayMode) y OnSceneChanged() seguía disparándose sobre una instancia ya
        // "muerta" en la siguiente sesión. Ahora se usan métodos con nombre para poder
        // desuscribirlos en OnDestroy.
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded       += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneLoaded       -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void HandleActiveSceneChanged(Scene _, Scene to) => OnSceneChanged(to.name);
    void HandleSceneLoaded(Scene scene, LoadSceneMode __) => OnSceneChanged(scene.name);

    // Escenas que no son mundo de juego: nunca deben heredar el skybox/ambient "congelado" de la
    // última vez que estuvimos en exterior/interior (ver FIX en OnSceneChanged más abajo).
    static readonly string[] NonGameplaySceneNames =
    {
        "MainMenu", "Credits", "CharacterCreator", "LoadingScreen", "SplashScreen"
    };

    void OnSceneChanged(string sceneName)
    {
        _cam = null; _appliedCam = null;
        _cinematicReapplyPending = false;

        // FIX (iluminación del MainMenu inconsistente tras Game Over): este objeto es
        // DontDestroyOnLoad y, antes de este fix, _mode nunca se reseteaba entre escenas —
        // solo el _needReapply de abajo decidía si tocar RenderSettings o no. Al volver al
        // MainMenu (p.ej. tras morir e ir a GameOverManager.TransitionToMainMenu()), _mode
        // seguía siendo Interior/Exterior de la última sesión de juego, así que en cuanto
        // Update() detectaba la cámara nueva del menú, Reapply()/ApplyExteriorTo() volvía a
        // pintar encima el skybox/clearFlags "congelados" (snapshot de exterior tomado la
        // primera vez que se entró a un interior esa sesión, con el time-of-day de aquel
        // momento) sobre el RenderSettings ya correcto que MainMenu.unity trae horneado — de
        // ahí que la iluminación del menú cambiara según dónde/cuándo había muerto el jugador.
        // Al entrar a una escena que no es mundo de juego, resetear a Unknown para que este
        // controlador no toque nada y se respete siempre la iluminación propia de la escena.
        if (IsNonGameplayScene(sceneName))
        {
            _mode = EnvironmentMode.Unknown;
            _hasSnapshot = false;
            _needReapply = false;
            return;
        }

        // no invalidamos el snapshot: si estabas en interior, lo necesitamos para volver a exterior
        _needReapply = (_mode != EnvironmentMode.Unknown); // re-aplicar el modo actual cuando haya cámara
    }

    static bool IsNonGameplayScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        foreach (var n in NonGameplaySceneNames)
            if (string.Equals(n, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    void Update()
    {
        var camNow = ResolveCamera();
        if (camNow != _appliedCam) _needReapply = true;

        // Re-aplicar configuración de cámara si quedó pendiente porque había cinemática activa
        if (_cinematicReapplyPending)
        {
            bool stillCinematic = (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
                               || Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying;
            if (!stillCinematic) { _needReapply = true; _cinematicReapplyPending = false; }
        }

        if (_needReapply && camNow != null)
        {
            Reapply(camNow);
            _needReapply = false;
        }
    }

    // === API pública ===
    public void ApplyInterior(AnchorEnvironment env)
    {
        _mode = EnvironmentMode.Interior;
        _currentInterior = env;

        // Capturamos el “exterior” una sola vez, antes de forzar interior
        if (!_hasSnapshot) CaptureExteriorSnapshot();

        var cam = ResolveCamera();
        ApplyInteriorTo(cam, env);   // si cam es null, haremos reapply cuando exista
        OnInteriorEntered?.Invoke();
    }

    public void ApplyExterior()
    {
        _mode = EnvironmentMode.Exterior;
        _currentInterior = null;

        var cam = ResolveCamera();
        ApplyExteriorTo(cam);
        OnInteriorExited?.Invoke();
    }

    public void RefreshCameraNow()
    {
        _cam = ResolveCamera();
        _needReapply = (_mode != EnvironmentMode.Unknown);
    }

    // === API PARA CINEMÁTICAS ===
    // Estos métodos permiten que las cinemáticas controlen temporalmente el entorno
    // sin interferir con el sistema normal de anchors
    
    /// <summary>
    /// Inicia el override cinemático. Guarda el estado actual del entorno para restaurarlo después.
    /// Llamar al inicio de una cinemática que necesita controlar el skybox.
    /// </summary>
    public void BeginCinematicOverride()
    {
        if (_cinematicOverrideActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EnvironmentController] BeginCinematicOverride llamado pero ya hay un override activo.");
#endif
            return;
        }
        
        _cinematicOverrideActive = true;
        _preCinematicMode = _mode;
        _preCinematicInterior = _currentInterior;
        // Por defecto asumimos que arrancamos mostrando lo mismo que había antes del override;
        // ApplyInteriorForCinematic/ApplyExteriorForCinematic lo corrigen en cuanto se llamen
        // (normalmente en el mismo frame, en el cut point de la transición de entrada).
        _cinematicShowingInterior = _preCinematicMode == EnvironmentMode.Interior;

        // Capturar snapshot del exterior ahora si no existe todavía.
        // Sin esto, ForceApplyExteriorTo no puede restaurar el skybox cuando el jugador
        // nunca entró en un interior antes de la cinemática (_hasSnapshot sería false).
        if (!_hasSnapshot && _mode != EnvironmentMode.Interior)
            CaptureExteriorSnapshot();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnvironmentController] 🎬 Cinematic Override INICIADO - Modo guardado: {_preCinematicMode}");
#endif
    }
    
    /// <summary>
    /// Finaliza el override cinemático y restaura el estado del entorno según donde está el jugador.
    /// Llamar al finalizar la cinemática.
    /// </summary>
    public void EndCinematicOverride()
    {
        if (!_cinematicOverrideActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EnvironmentController] EndCinematicOverride llamado pero no hay override activo.");
#endif
            return;
        }
        
        // IMPORTANTE: Desactivar el flag DESPUÉS de aplicar, para que la protección cinemática no bloquee
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnvironmentController] 🎬 Cinematic Override FINALIZADO - Restaurando modo: {_preCinematicMode}");
#endif
        
        // Restaurar el estado del entorno según el modo pre-cinemático
        // Usamos métodos de aplicación FORZADA que ignoran la protección cinemática
        var cam = ResolveCamera();
        if (_preCinematicMode == EnvironmentMode.Interior && _preCinematicInterior != null)
        {
            // Forzar re-aplicación del interior con TODOS los parámetros incluyendo clipping
            _mode = EnvironmentMode.Interior;
            _currentInterior = _preCinematicInterior;
            ForceApplyInteriorTo(cam, _preCinematicInterior);
        }
        else if (_preCinematicMode == EnvironmentMode.Exterior)
        {
            // Forzar re-aplicación del exterior
            _mode = EnvironmentMode.Exterior;
            _currentInterior = null;
            ForceApplyExteriorTo(cam);
        }
        
        // Ahora sí desactivamos el flag
        _cinematicOverrideActive = false;
        _cinematicShowingInterior = false;
        _preCinematicInterior = null;
    }
    
    /// <summary>
    /// Aplica configuración de EXTERIOR para una cinemática (skybox visible).
    /// Solo funciona si hay un override cinemático activo.
    /// </summary>
    public void ApplyExteriorForCinematic(Camera cam = null)
    {
        if (!_cinematicOverrideActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EnvironmentController] ApplyExteriorForCinematic requiere BeginCinematicOverride primero.");
#endif
            return;
        }
        
        cam = cam != null ? cam : ResolveCamera();
        if (cam == null) return;

        _cinematicShowingInterior = false;

        // Aplicar skybox/exterior sin cambiar el _mode interno
        cam.clearFlags = CameraClearFlags.Skybox;
        
        if (exteriorSkyboxOverride != null)
        {
            RenderSettings.skybox = exteriorSkyboxOverride;
        }
        else if (_hasSnapshot && _savedRenderSettingsSkybox != null)
        {
            RenderSettings.skybox = _savedRenderSettingsSkybox;
        }
        
        // Asegurar que el componente Skybox de la cámara use el material correcto
        var camSkybox = cam.GetComponent<Skybox>();
        if (camSkybox != null && RenderSettings.skybox != null)
        {
            camSkybox.material = RenderSettings.skybox;
        }

        // Restaurar zonas que se hubieran ocultado en un paso interior anterior de esta
        // misma cinemática (ver ApplyInteriorForCinematic más abajo).
        RestoreZoneVisibility();

        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Aplica configuración de INTERIOR para una cinemática (solid color o skybox de interior).
    /// Solo funciona si hay un override cinemático activo.
    /// Usa la configuración del interior actual del jugador si env es null.
    /// </summary>
    public void ApplyInteriorForCinematic(Camera cam = null, AnchorEnvironment env = null)
    {
        if (!_cinematicOverrideActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EnvironmentController] ApplyInteriorForCinematic requiere BeginCinematicOverride primero.");
#endif
            return;
        }
        
        cam = cam != null ? cam : ResolveCamera();
        if (cam == null) return;

        _cinematicShowingInterior = true;

        // Usar el interior pre-cinemático si no se especifica uno
        env = env != null ? env : _preCinematicInterior;
        
        if (env == null)
        {
            // Si no hay interior configurado, simplemente usar solid color negro
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            RenderSettings.skybox = null;
        }
        else if (env.useSolidColorBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = env.interiorBgColor;
            RenderSettings.skybox = null;
        }
        else
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = env.interiorSkyboxOverride;
        }

        // FIX: al igual que en ApplyInteriorTo/ForceApplyInteriorTo, gestionar la visibilidad
        // de zonas también en el flujo cinemático. Antes de este fix, entrar en un interior
        // "por secuencia" (ej: TabernaSequencer, que nunca pasa por TeleportService/ApplyInterior)
        // dejaba el skybox/color de cámara correctos pero NO ocultaba zonesToHideOnEnter ni el
        // mundo exterior — solo entrar andando (vía AnchorSetter/TeleportService → ApplyInterior)
        // aplicaba esa gestión de zonas.
        ApplyZoneVisibility(env);

        DynamicGI.UpdateEnvironment();
    }

    // === implementación ===
    void Reapply(Camera cam)
    {
        if (_mode == EnvironmentMode.Interior)
        {
            // _currentInterior puede haber sido destruida al descargar la escena anterior.
            // Si está destruida, resetear a Unknown para que WorldBootstrap re-aplique.
            if (!_currentInterior)
            {
                _mode = EnvironmentMode.Unknown;
                return;
            }
            ApplyInteriorTo(cam, _currentInterior, reapply:true);
        }
        else if (_mode == EnvironmentMode.Exterior) ApplyExteriorTo(cam);
    }
    
    /// <summary>
    /// Activa la zona de este interior (zoneRoot), oculta las zonas indicadas en
    /// zonesToHideOnEnter y, si procede, el mundo exterior (exteriorWorldRootName).
    /// Único punto de gestión de visibilidad de zonas: lo usan tanto el flujo normal
    /// (ApplyInteriorTo/ForceApplyInteriorTo) como el flujo cinemático
    /// (ApplyInteriorForCinematic), para que entrar "andando" o "por secuencia"
    /// produzca siempre el mismo resultado de zonas ocultas/visibles.
    /// </summary>
    void ApplyZoneVisibility(AnchorEnvironment env)
    {
        if (!env) return;

        if (env.zoneRoot && !env.zoneRoot.activeSelf)
        {
            env.zoneRoot.SetActive(true);
        }

        if (env.zonesToHideOnEnter != null && env.zonesToHideOnEnter.Length > 0)
        {
            _hiddenZones = env.zonesToHideOnEnter;
            foreach (var zone in _hiddenZones)
            {
                if (zone && zone.activeSelf)
                {
                    zone.SetActive(false);
                }
            }
        }

        // FIX (11 sep 2026): antes usaba GameObject.Find(...)/GameObject.FindWithTag(...) aquí —
        // un recorrido completo de la escena en CADA cruce de anchor, prohibido por AGENTS.md § 2 /
        // TDD.md § 12 ("usar registros" en vez de Find). Además FindWithTag lanza excepción si el
        // string no es un Tag registrado, y con "ExteriorWorld" como nombre normal esa llamada
        // podía fallar en silencio o con excepción no capturada, dejando _hiddenExteriors en null y
        // rompiendo también la restauración al salir (RestoreZoneVisibility depende de esta misma
        // referencia). Se resuelve por registro (ExteriorWorldRoot, O(1), sin recorrer la escena).
        //
        // FIX (14 sep 2026, pedido de Raúl): entre el 11 y el 12 sept esto pasó a resolverse por
        // NOMBRE — cada AnchorEnvironment tenía que listar a mano, en exteriorWorldRootName/
        // exteriorWorldRootNames, cada GameObject raíz que quisiera ocultar. Frágil: un
        // ExteriorWorldRoot nuevo en la escena (con el componente puesto y todo) se quedaba fuera
        // en silencio si nadie se acordaba de añadir también su nombre a la lista de cada anchor/
        // cinemática — pasó de verdad con "CAPITULOS": tenía el componente pero ningún anchor lo
        // listaba, así que su Rigidbody seguía simulando cuando el resto del mundo se ocultaba
        // durante una secuencia, y una caja de quest caía al vacío (ver
        // incidencia-caja-eldran-cae-sin-parar-2026-09-14.md). Ahora, si hideExteriorWorld está
        // activo, se ocultan TODOS los ExteriorWorldRoot registrados en la escena, sin excepción —
        // el propio componente ya es la marca de "esto es mundo exterior, ocúltame"; no hace falta
        // ninguna lista de nombres en ningún sitio más. exteriorWorldRootName/exteriorWorldRootNames
        // en AnchorEnvironment.cs quedan sin uso (no se han borrado para no perder los valores ya
        // serializados en anchors existentes, pero pueden vaciarse con tranquilidad).
        // FIX (15 sep 2026, pedido de Raúl): antes se ocultaba cada root con SetActive(false),
        // lo que además de dejar de renderizarlo paraba TODA su simulación (Update, NavMeshAgent,
        // coroutines) — causaba p. ej. que una coroutine de cinemática en curso sobre un NPC del
        // root "AI" (Oliver) no pudiera pararse ni relanzarse limpiamente al saltarla
        // ("Coroutine couldn't be started because the game object 'Oliver' is inactive"). Ahora
        // se llama a SetVisible(false), que solo apaga Renderer/Light — el mundo exterior sigue
        // sin verse desde el interior, pero su simulación no se congela. Ver ExteriorWorldRoot.cs.
        if (env.hideExteriorWorld)
        {
            var resolved = ExteriorWorldRoot.AllRegistered();
            foreach (var exterior in resolved)
                exterior.SetVisible(false);
            _hiddenExteriors = resolved.ToArray();
        }
    }

    /// <summary>
    /// Contrapartida de ApplyZoneVisibility: restaura las zonas ocultadas al entrar en
    /// un interior. Compartido por ApplyExteriorTo/ForceApplyExteriorTo/ApplyExteriorForCinematic.
    /// </summary>
    void RestoreZoneVisibility()
    {
        if (_hiddenZones != null)
        {
            foreach (var zone in _hiddenZones)
            {
                if (zone && !zone.activeSelf)
                {
                    zone.SetActive(true);
                }
            }
            _hiddenZones = null;
        }

        if (_hiddenExteriors != null)
        {
            foreach (var exterior in _hiddenExteriors)
                if (exterior) exterior.SetVisible(true);
            _hiddenExteriors = null;
        }
    }

    /// <summary>
    /// Aplica configuración de interior FORZADAMENTE, ignorando la protección cinemática.
    /// Usado internamente por EndCinematicOverride para restaurar el estado correcto.
    /// </summary>
    void ForceApplyInteriorTo(Camera cam, AnchorEnvironment env)
    {
        if (!cam)
        {
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            _needReapply = true;
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnvironmentController] 🔧 ForceApplyInteriorTo - Aplicando configuración completa de interior");
#endif

        // Aplicar configuración de cámara (clearFlags, backgroundColor, skybox)
        if (env && env.useSolidColorBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = env.interiorBgColor;
            RenderSettings.skybox = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnvironmentController] - SolidColor: {env.interiorBgColor}");
#endif
        }
        else
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnvironmentController] - Skybox: {(env?.interiorSkyboxOverride != null ? env.interiorSkyboxOverride.name : "null")}");
#endif
        }
        
        // IMPORTANTE: Aplicar ajuste de clipping de cámara
        if (env && env.adjustCameraClipping)
        {
            if (!_farClipModified)
            {
                _savedFarClipPlane = cam.farClipPlane;
                _farClipModified = true;
            }
            cam.farClipPlane = env.interiorFarClipPlane;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnvironmentController] - FarClipPlane ajustado a: {env.interiorFarClipPlane}");
#endif
        }

        // Aplicar gestión de zonas visibles
        ApplyZoneVisibility(env);

        // Gestión de luces
        var dirLight = ServiceLocator.Get<Light>(false);
        if (dirLight && dirLight.type == LightType.Directional)
        {
            bool inside = env && IsChildOf(dirLight.transform, env.transform);
            dirLight.gameObject.SetActive(inside);
        }

        if (env)
        {
            SetActive(env.lightsDisableOnEnter, false);
            SetActive(env.lightsEnableOnEnter, true);
            foreach (var l in env.GetComponentsInChildren<Light>(true))
                if (l) l.gameObject.SetActive(true);
        }

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Aplica configuración de exterior FORZADAMENTE, ignorando la protección cinemática.
    /// Usado internamente por EndCinematicOverride para restaurar el estado correcto.
    /// </summary>
    void ForceApplyExteriorTo(Camera cam)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[EnvironmentController] 🔧 ForceApplyExteriorTo - Aplicando configuración completa de exterior");
#endif
        
        if (cam) cam.clearFlags = _hasSnapshot ? _savedClearFlags : CameraClearFlags.Skybox;

        if (exteriorSkyboxOverride)
        {
            RenderSettings.skybox = exteriorSkyboxOverride;
            var csb = EnsureCameraSkybox(cam);
            if (csb) csb.material = exteriorSkyboxOverride;
        }
        else if (_hasSnapshot)
        {
            if (_savedHadCamSkybox)
            {
                var csb = EnsureCameraSkybox(cam);
                if (csb) csb.material = _savedCameraSkyboxMat;
            }
            else
            {
                RenderSettings.skybox = _savedRenderSettingsSkybox;
                var csb = cam ? cam.GetComponent<Skybox>() : null;
                if (csb) csb.material = null;
            }
        }
        
        // Restaurar far clip plane de la cámara
        if (_farClipModified && cam)
        {
            cam.farClipPlane = _savedFarClipPlane;
            _farClipModified = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnvironmentController] - FarClipPlane restaurado a: {_savedFarClipPlane}");
#endif
        }

        // Restaurar zonas ocultas
        RestoreZoneVisibility();

        var dirLight2 = ServiceLocator.Get<Light>(false);
        if (dirLight2 && dirLight2.type == LightType.Directional) dirLight2.gameObject.SetActive(true);

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    void CaptureExteriorSnapshot()
    {
        var cam = ResolveCamera();

        _savedClearFlags = cam ? cam.clearFlags : CameraClearFlags.Skybox;
        _savedRenderSettingsSkybox = RenderSettings.skybox;

        _savedHadCamSkybox = false;
        _savedCameraSkyboxMat = null;

        if (cam)
        {
            var csb = cam.GetComponent<Skybox>();
            if (csb && csb.material)
            {
                _savedHadCamSkybox = true;
                _savedCameraSkyboxMat = csb.material;
            }
        }

        _hasSnapshot = true;
    }

    void ApplyInteriorTo(Camera cam, AnchorEnvironment env, bool reapply = false)
    {
        // consumir el parámetro para evitar advertencia de "never used"
        _ = reapply;

        // si no hay cámara aún, marca para re-aplicar cuando aparezca
        if (!cam)
        {
            // al menos quita el skybox global para mitigar
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            _needReapply = true;
            return;
        }

        // --- PROTECCIÓN CINEMÁTICA ---
        // Si estamos en modo cinemático (DialogueCinematicController o SimpleCinematicDirector),
        // NO modificar la cámara principal, ya que los sistemas de cinemática tienen su propia gestión.
        // Esto evita conflictos de ClearFlags y Skybox.
        bool isCinematicActive = false;
        
        // Verificar DialogueCinematicController
        if (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
        {
            isCinematicActive = true;
        }
        
        // Verificar SimpleCinematicDirector
        if (Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying)
        {
            isCinematicActive = true;
        }
        
        if (isCinematicActive)
        {
            // Si hay cinemática, solo aplicamos lógica de luces y objetos, pero NO tocamos la cámara.
            // Marcamos para re-aplicar en cuanto la cinemática termine (ver Update).
            _cinematicReapplyPending = true;
        }
        else
        {
            _cinematicReapplyPending = false;

            // Lógica normal de cámara
            if (env && env.useSolidColorBackground)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = env.interiorBgColor;
                RenderSettings.skybox = null;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            }

            // Ajustar far clip plane de la cámara
            if (env && env.adjustCameraClipping)
            {
                if (!_farClipModified)
                {
                    _savedFarClipPlane = cam.farClipPlane;
                    _farClipModified = true;
                }
                cam.farClipPlane = env.interiorFarClipPlane;
            }
        }

        // === GESTIÓN DE ZONAS VISIBLES (Siempre aplicar, incluso en cinemáticas) ===
        ApplyZoneVisibility(env);

        // luces: apaga direccionales que no estén dentro del interior
        var dirLight = ServiceLocator.Get<Light>(false);
        if (dirLight && dirLight.type == LightType.Directional)
        {
            bool inside = env && IsChildOf(dirLight.transform, env.transform);
            dirLight.gameObject.SetActive(inside);
        }

        // enciende luces locales del interior (aunque no estén en los arrays)
        if (env)
        {
            SetActive(env.lightsDisableOnEnter, false);
            SetActive(env.lightsEnableOnEnter, true);
            foreach (var l in env.GetComponentsInChildren<Light>(true))
                if (l) l.gameObject.SetActive(true);
        }

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    void ApplyExteriorTo(Camera cam)
    {
        // --- PROTECCIÓN CINEMÁTICA ---
        bool isCinematicActive = false;
        if (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
        {
            isCinematicActive = true;
        }
        
        // Verificar SimpleCinematicDirector
        if (Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying)
        {
            isCinematicActive = true;
        }

        if (!isCinematicActive)
        {
            if (cam) cam.clearFlags = _hasSnapshot ? _savedClearFlags : CameraClearFlags.Skybox;

            if (exteriorSkyboxOverride)
            {
                RenderSettings.skybox = exteriorSkyboxOverride;
                var csb = EnsureCameraSkybox(cam);
                if (csb) csb.material = exteriorSkyboxOverride;
            }
            else if (_hasSnapshot)
            {
                if (_savedHadCamSkybox)
                {
                    var csb = EnsureCameraSkybox(cam);
                    if (csb) csb.material = _savedCameraSkyboxMat;
                }
                else
                {
                    RenderSettings.skybox = _savedRenderSettingsSkybox;
                    var csb = cam ? cam.GetComponent<Skybox>() : null;
                    if (csb) csb.material = null;
                }
            }
            
            // Restaurar far clip plane de la cámara
            if (_farClipModified && cam)
            {
                cam.farClipPlane = _savedFarClipPlane;
                _farClipModified = false;
            }
        }

        // === RESTAURAR ZONAS OCULTAS ===
        // Restaurar zonas que ocultamos al entrar en un interior
        RestoreZoneVisibility();

        var dirLight2 = ServiceLocator.Get<Light>(false);
        if (dirLight2 && dirLight2.type == LightType.Directional) dirLight2.gameObject.SetActive(true);

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    Camera ResolveCamera()
    {
        if (targetCamera) return targetCamera;
        if (_cam && _cam) return _cam;

        // 1) MainCamera
        var m = Camera.main;
        if (m && m.enabled && m.gameObject.activeInHierarchy) return _cam = m;

        // 2) mejor cámara disponible (incluye inactivas)
        var cam = ServiceLocator.Get<Camera>(false);
        Camera best = null; float scoreBest = float.NegativeInfinity;
        if (cam)
        {
            float s = 0f;
            if (cam.enabled && cam.gameObject.activeInHierarchy) s += 1000f;
            if (cam.targetDisplay == 0) s += 100f;
            s += cam.depth;
            if (s > scoreBest) { scoreBest = s; best = cam; }
        }
        return _cam = best;
    }

    static Skybox EnsureCameraSkybox(Camera cam)
    {
        if (!cam) return null;
        var csb = cam.GetComponent<Skybox>();
        if (!csb) csb = cam.gameObject.AddComponent<Skybox>();
        return csb;
    }

    static void SetActive(Light[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var l in arr) if (l) l.gameObject.SetActive(active);
    }

    static bool IsChildOf(Transform t, Transform root)
    {
        if (!t || !root) return false;
        for (var cur = t; cur != null; cur = cur.parent)
            if (cur == root) return true;
        return false;
    }
}
