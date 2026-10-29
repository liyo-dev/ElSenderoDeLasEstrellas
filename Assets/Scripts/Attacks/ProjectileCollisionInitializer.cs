using UnityEngine;

/// <summary>
/// Servicio singleton que aplica la configuración del choque de proyectiles
/// (ProjectileCollisionSettingsSO) a ProjectileCollisionHandler al arrancar.
/// Persiste entre escenas y se registra en el ServiceLocator. Vive en Start.unity; la clase se
/// llama como el archivo para que Unity pueda cargar el componente.
/// 
/// CONFIGURACIÓN:
/// 1. Añade este componente a un GameObject en la escena START
/// 2. Crea un ProjectileCollisionSettings (Create ▸ El Sendero ▸ Ataques ▸ Projectile Collision Settings)
/// 3. Arrastra el settings al campo 'Settings' en el Inspector
/// 4. Configura el VFX de colisión en el settings
/// 
/// El servicio persistirá entre escenas y estará disponible globalmente.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class ProjectileCollisionInitializer : MonoBehaviour
{
    public static ProjectileCollisionInitializer Instance { get; private set; }
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        _isShuttingDown = false;
    }
    #endif
    
    [Header("Configuración")]
    [Tooltip("ScriptableObject con la configuración de colisiones de proyectiles")]
    [SerializeField] private ProjectileCollisionSettingsSO settings;
    
    [Header("Persistencia")]
    [Tooltip("Mantener este servicio entre cambios de escena")]
    [SerializeField] private bool persistAcrossScenes = true;
    
    private static bool _isShuttingDown;
    
    void Awake()
    {
        // Patrón Singleton con protección contra duplicados
        if (Instance != null && Instance != this)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ProjectileCollisionInitializer] Instancia duplicada detectada en '{name}'. Se destruye el duplicado.");
#endif
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        _isShuttingDown = false;
        
        // DontDestroyOnLoad para persistir entre escenas
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        // Registrar en ServiceLocator
        ServiceLocator.Register(this);
        
        // Inicializar el sistema de colisiones
        InitializeCollisionSystem();
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _isShuttingDown = true;
            
            // Desregistrar del ServiceLocator
            ServiceLocator.Unregister(this);
        }
    }
    
    void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }
    
    private void InitializeCollisionSystem()
    {
        if (settings != null)
        {
            ProjectileCollisionHandler.Initialize(settings.ToConfig());
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (settings.collisionVFX == null)
                Debug.LogWarning("[ProjectileCollisionInitializer] ⚠️ No hay VFX de colisión asignado");
#endif
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ProjectileCollisionInitializer] ⚠️ No hay settings asignado - usando configuración por defecto");
#endif
            // El handler usará configuración por defecto automáticamente
        }
    }
    
    /// <summary>
    /// Permite actualizar la configuración en runtime
    /// </summary>
    public void UpdateSettings(ProjectileCollisionSettingsSO newSettings)
    {
        if (_isShuttingDown) return;

        settings = newSettings;
        InitializeCollisionSystem();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[ProjectileCollisionInitializer] Configuración actualizada en runtime");
#endif
    }
    
    /// <summary>
    /// Obtiene la configuración actual
    /// </summary>
    public ProjectileCollisionSettingsSO GetSettings()
    {
        return settings;
    }
}

