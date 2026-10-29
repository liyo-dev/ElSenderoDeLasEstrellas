using UnityEngine;

/// <summary>
/// Sistema que maneja las colisiones entre proyectiles del jugador y enemigos.
/// Cuando dos proyectiles colisionan, se empujan mutuamente, reproducen VFX y se destruyen.
/// </summary>
public static class ProjectileCollisionHandler
{
    /// <summary>
    /// Configuración global de colisiones entre proyectiles
    /// </summary>
    [System.Serializable]
    public class CollisionConfig
    {
        [Header("Fuerzas de Empuje")]
        [Tooltip("Fuerza aplicada al NPC cuando su proyectil colisiona")]
        public float npcKnockbackForce = 8f;
        
        [Header("VFX")]
        [Tooltip("Prefab del VFX que se reproduce en el punto de colisión")]
        public GameObject collisionVFX;
        
        [Tooltip("Duración del VFX (0 = autodestrucción del VFX)")]
        public float vfxLifetime = 2f;
        
        [Header("Efectos de Cámara")]
        [Tooltip("Intensidad del camera shake al colisionar")]
        public float cameraShakeIntensity = 0.5f;
        
        [Tooltip("Duración del camera shake")]
        public float cameraShakeDuration = 0.3f;
        
        [Header("Audio")]
        [Tooltip("Clave del SFX para la colisión de proyectiles")]
        public string collisionSFXKey = "ProjectileClash";
    }
    
    private static CollisionConfig _config;
    
    /// <summary>
    /// Inicializa la configuración de colisiones. Llamar desde un ScriptableObject o Manager.
    /// </summary>
    public static void Initialize(CollisionConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Obtiene la configuración actual o una por defecto
    /// </summary>
    private static CollisionConfig GetConfig()
    {
        if (_config == null)
        {
            // Configuración por defecto
            _config = new CollisionConfig
            {
                npcKnockbackForce = 8f,
                vfxLifetime = 2f,
                cameraShakeIntensity = 0.5f,
                cameraShakeDuration = 0.3f,
                collisionSFXKey = "ProjectileClash",
            };
        }
        return _config;
    }
    
    /// <summary>
    /// Maneja la colisión entre un proyectil del jugador y uno del enemigo
    /// </summary>
    /// <param name="playerProjectile">Proyectil del jugador (layer "Projectile")</param>
    /// <param name="enemyProjectile">Proyectil del enemigo (layer "ProjectileEnemy")</param>
    /// <param name="collisionPoint">Punto donde colisionaron</param>
    public static void HandleCollision(
        GameObject playerProjectile, 
        GameObject enemyProjectile, 
        Vector3 collisionPoint)
    {
        var config = GetConfig();

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ProjectileCollision] 💥 Colisión detectada en {collisionPoint}");
#endif

        // El choque solo quita al jugador el daño del hechizo enemigo (no el suyo propio). Se lee
        // antes de destruir los proyectiles en el paso 6, para no perder sus componentes.
        float enemyDamage = GetProjectileDamage(enemyProjectile);

        // 1. Reproducir VFX en el punto de colisión
        SpawnCollisionVFX(collisionPoint, config);

        // 2. Reproducir SFX
        PlayCollisionSound(config);

        // 3. Camera shake para feedback
        ApplyCameraShake(config);

        // 4. Reacción del jugador: animación de daño + pérdida de vida
        ApplyPlayerReaction(enemyDamage);

        // 5. Aplicar knockback al NPC
        ApplyKnockbackToEntities(playerProjectile, enemyProjectile, collisionPoint, config);

        // 6. Destruir ambos proyectiles
        DestroyProjectiles(playerProjectile, enemyProjectile);
    }

    /// <summary>
    /// Lee el daño configurado del proyectil, sea del jugador (MagicProjectile) o del enemigo (EnemyProjectile).
    /// </summary>
    private static float GetProjectileDamage(GameObject projectile)
    {
        if (projectile == null) return 0f;

        var magicProj = projectile.GetComponent<MagicProjectile>();
        if (magicProj != null) return magicProj.Damage;

        var enemyProj = projectile.GetComponent<EnemyProjectile>();
        if (enemyProj != null) return enemyProj.Damage;

        return 0f;
    }
    
    private static void SpawnCollisionVFX(Vector3 position, CollisionConfig config)
    {
        if (config.collisionVFX == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ProjectileCollision] No hay VFX configurado para la colisión");
#endif
            return;
        }
        
        float destroyTime = config.vfxLifetime > 0f ? config.vfxLifetime : 3f;
        VfxPoolService.Instance.Play(config.collisionVFX, position, Quaternion.identity, destroyTime);
        
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ProjectileCollision] ✨ VFX spawneado en {position}");
#endif
    }
    
    private static void PlayCollisionSound(CollisionConfig config)
    {
        if (string.IsNullOrEmpty(config.collisionSFXKey))
            return;
        
        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(config.collisionSFXKey);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[ProjectileCollision] 🔊 SFX reproducido: {config.collisionSFXKey}");
#endif
        }
    }
    
    private static void ApplyCameraShake(CollisionConfig config)
    {
        if (config.cameraShakeIntensity > 0f && config.cameraShakeDuration > 0f)
        {
            Sendero.Core.Feedback.FeedbackService.CameraShake(
                config.cameraShakeIntensity, 
                config.cameraShakeDuration);
            
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[ProjectileCollision] 📹 Camera shake aplicado: {config.cameraShakeIntensity}");
#endif
        }
    }
    
    /// <summary>
    /// Reacción del jugador ante un choque de hechizos: recibe el daño del hechizo enemigo por
    /// PlayerHealthSystem.TakeDamage, que ya gestiona sus reglas de daño, animación, VFX, sonido,
    /// invulnerabilidad y empuje.
    /// </summary>
    private static void ApplyPlayerReaction(float enemyDamage)
    {
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ProjectileCollision] ⚠️ Player NO encontrado");
#endif
            return;
        }

        var playerHealth = playerGo.GetComponent<PlayerHealthSystem>() ?? playerGo.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ProjectileCollision] ⚠️ Player '{playerGo.name}' no tiene PlayerHealthSystem, no se puede aplicar daño de choque de hechizos");
#endif
            return;
        }

        bool applied = playerHealth.TakeDamage(enemyDamage);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ProjectileCollision] 💥 Choque de hechizos: {enemyDamage} de daño (solo hechizo enemigo) {(applied ? "aplicado" : "IGNORADO (invulnerable/muerto/god mode)")} al jugador");
#endif
    }

    private static void ApplyKnockbackToEntities(
        GameObject playerProjectile,
        GameObject enemyProjectile,
        Vector3 collisionPoint,
        CollisionConfig config)
    {
        // El jugador ya se aparta con el empuje de PlayerHealthSystem al recibir el daño
        // (ApplyPlayerReaction); aquí solo se empuja al NPC.
        Vector3 enemyDirection = (enemyProjectile.transform.position - collisionPoint).normalized;
        ApplyKnockbackToNPC(enemyProjectile, enemyDirection, config.npcKnockbackForce);
    }

    private static void ApplyKnockbackToNPC(GameObject enemyProjectile, Vector3 direction, float force)
    {
        // El proyectil del enemigo debería tener referencia real a su instigador (pendiente:
        // requeriría añadir el campo en EnemyProjectile y setearlo en cada AI que lo spawnea).
        // Mientras tanto, usamos el registro de combate activo (mismo patrón que el resto del
        // proyecto, ver ActiveCombatRegistry.GetClosestCombatNPC) en vez de Physics.OverlapSphere:
        // evita la allocation y limita la búsqueda a NPCs realmente en combate, no a cualquier
        // collider en la layer Enemy/Boss en 30m a la redonda.
        GameObject closestNPC = ActiveCombatRegistry.GetClosestCombatNPC(
            enemyProjectile.transform.position,
            maxDistance: 30f);

        if (closestNPC != null)
        {
            var rb = closestNPC.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                // Aplicar fuerza horizontal
                Vector3 knockback = direction;
                knockback.y = 0f;
                knockback = knockback.normalized * force;
                
                rb.AddForce(knockback, ForceMode.Impulse);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[ProjectileCollision] 👊 Knockback aplicado al NPC '{closestNPC.name}': {knockback}");
#endif
            }
            else
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[ProjectileCollision] El NPC '{closestNPC.name}' no tiene Rigidbody o es kinematic");
#endif
            }
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ProjectileCollision] No se encontró NPC cercano para aplicar knockback");
#endif
        }
    }
    
    private static void DestroyProjectiles(GameObject playerProjectile, GameObject enemyProjectile)
    {
        if (playerProjectile != null)
        {
            var magicProj = playerProjectile.GetComponent<MagicProjectile>();
            if (magicProj != null)
            {
                magicProj.End(true); // byImpact = true
            }
            else
            {
                Object.Destroy(playerProjectile);
            }
            
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[ProjectileCollision] 💀 Proyectil del jugador destruido");
#endif
        }
        
        if (enemyProjectile != null)
        {
            var enemyProj = enemyProjectile.GetComponent<EnemyProjectile>();
            if (enemyProj != null)
            {
                enemyProj.DestroyProjectile();
            }
            else
            {
                Object.Destroy(enemyProjectile);
            }
            
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[ProjectileCollision] 💀 Proyectil del enemigo destruido");
#endif
        }
    }
}

