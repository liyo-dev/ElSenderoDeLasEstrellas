using UnityEngine;

/// <summary>
/// Configuración global para las colisiones entre proyectiles del jugador y enemigos.
/// Se crea en Create ▸ El Sendero ▸ Ataques ▸ Projectile Collision Settings; lo aplica
/// ProjectileCollisionInitializer (en Start.unity) al arrancar.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Ataques/Projectile Collision Settings", fileName = "ProjectileCollisionSettings")]
public class ProjectileCollisionSettingsSO : ScriptableObject
{
    [Header("VFX de Colisión")]
    [Tooltip("Prefab del VFX que se reproduce cuando colisionan proyectiles")]
    public GameObject collisionVFX;
    
    [Tooltip("Duración del VFX (0 = autodestrucción del VFX)")]
    [Min(0f)]
    public float vfxLifetime = 2f;
    
    [Header("Fuerzas de Empuje")]
    [Tooltip("Fuerza aplicada al NPC cuando su proyectil colisiona")]
    [Min(0f)]
    public float npcKnockbackForce = 8f;
    
    [Header("Efectos de Cámara")]
    [Tooltip("Intensidad del camera shake al colisionar")]
    [Range(0f, 2f)]
    public float cameraShakeIntensity = 0.5f;
    
    [Tooltip("Duración del camera shake")]
    [Range(0f, 1f)]
    public float cameraShakeDuration = 0.3f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX para la colisión de proyectiles")]
    public string collisionSFXKey = "ProjectileClash";

    /// <summary>
    /// Convierte este ScriptableObject en la configuración para el handler
    /// </summary>
    public ProjectileCollisionHandler.CollisionConfig ToConfig()
    {
        return new ProjectileCollisionHandler.CollisionConfig
        {
            collisionVFX = collisionVFX,
            vfxLifetime = vfxLifetime,
            npcKnockbackForce = npcKnockbackForce,
            cameraShakeIntensity = cameraShakeIntensity,
            cameraShakeDuration = cameraShakeDuration,
            collisionSFXKey = collisionSFXKey
        };
    }
}

