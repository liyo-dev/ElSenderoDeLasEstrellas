using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Materializa hechizos: proyectiles (con o sin carga en la mano) y zonas. No guarda qué hechizos
/// lleva equipados el jugador ni cobra nada: eso es de <see cref="MagicCaster"/>, que decide qué
/// sale y desde qué mano, y llama a <see cref="Cast"/>. Las cinemáticas usan
/// <see cref="SpawnForCinematic"/>.
/// </summary>
[DisallowMultipleComponent]
public class MagicProjectileSpawner : MonoBehaviour
{
    /// <summary>
    /// Se lanza cada vez que el jugador lanza un hechizo. Los compañeros del grupo lo escuchan para
    /// entrar en combate.
    /// </summary>
    public static event System.Action OnPlayerAttacked;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OnPlayerAttacked = null;
    }
#endif

    [Header("Referencias")]
    [SerializeField] private PlayerTargeting targeting;

    // Cada carga necesita su propio bucle de sonido: el identificador se numera aquí.
    static int _contadorDeCargas;

    [Header("Configuración Global")]
    [SerializeField] private ProjectileSettingsSO projectileSettings;

    [Header("Orígenes (mano izquierda, derecha y centro)")]
    [SerializeField] private Transform leftOrigin;
    [SerializeField] private Transform rightOrigin;
    [Tooltip("Origen de los lanzamientos a dos manos (tercer golpe de la serie, combos).")]
    [SerializeField] private Transform specialOrigin;

    [Header("Opciones")]
    [SerializeField] private bool ignoreCasterColliders = true;
    [SerializeField] private GameObject instigatorOverride;

    [Header("Velocidad dinámica (INC-049)")]
    [Tooltip("Multiplicador de velocidad del proyectil mientras el jugador está volando.")]
    [SerializeField] private float flyingSpeedMultiplier = 1.5f;
    [Tooltip("Multiplicador de velocidad del proyectil mientras el jugador está esprintando (en tierra).")]
    [SerializeField] private float sprintSpeedMultiplier = 1.3f;

    // Proyectiles cargando en la mano (followOriginDuringCharge, kinematic). Si el componente se
    // desactiva a mitad de la carga, la corrutina no llega a soltarlos: OnDisable los destruye
    // para que no se queden pegados a la mano.
    private readonly List<GameObject> _chargingProjectiles = new List<GameObject>();

    // Vuelo y sprint se leen como SprintVFXController: parámetros del Animator de Invector y
    // PlayerFlyingController.
    private Animator _animator;
    private PlayerFlyingController _flyingController;
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

    void Awake()
    {
        if (!targeting)  targeting  = GetComponentInParent<PlayerTargeting>();
        if (!instigatorOverride) instigatorOverride = gameObject;
        if (!_animator) _animator = GetComponentInParent<Animator>();
        if (!_flyingController) _flyingController = GetComponentInParent<PlayerFlyingController>();
    }

    void OnDisable()
    {
        for (int i = 0; i < _chargingProjectiles.Count; i++)
        {
            var go = _chargingProjectiles[i];
            if (go)
            {
                go.transform.SetParent(null, worldPositionStays: true);
                Destroy(go);
            }
        }
        _chargingProjectiles.Clear();
    }

    // === API ==================================================================

    /// <summary>
    /// Lanza un hechizo desde una mano: suena al instante, espera castDelaySeconds (para
    /// sincronizar con la animación) y lo materializa. 'damageMultiplier' escala el daño (golpe
    /// final de la serie). No comprueba ni cobra nada.
    /// </summary>
    public void Cast(MagicSpellSO spell, CastHand hand, float damageMultiplier = 1f)
    {
        if (!spell || !spell.prefab) return;
        OnPlayerAttacked?.Invoke();
        StartCoroutine(Co_SpawnAfterDelay(spell, GetOrigin(hand), damageMultiplier));
    }

    /// <summary>
    /// Disparo de cinemática: sin maná, enfriamientos ni permisos. 'directionOverride' fuerza una
    /// dirección exacta. Devuelve el objeto creado (null si el hechizo no tiene prefab).
    /// </summary>
    public GameObject SpawnForCinematic(MagicSpellSO spell, CastHand hand, Transform originOverride = null, Vector3? directionOverride = null)
    {
        if (spell == null || spell.prefab == null) return null;
        Transform origin = originOverride != null ? originOverride : GetOrigin(hand);
        return SpawnNow(spell, origin, directionOverride: directionOverride);
    }

    /// <summary>Materializa un hechizo en el acto, sin retraso de animación.</summary>
    public GameObject SpawnNow(MagicSpellSO spell, Transform originOverride = null, bool playSFX = true, Vector3? directionOverride = null, float damageMultiplier = 1f)
    {
        if (!spell || !spell.prefab) return null;

        Transform origin = originOverride ? originOverride : transform;

        if (playSFX && !string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(spell.castSFXKey);

        if (spell.kind == MagicKind.Zone)
            return SpawnZoneNow(spell, origin, directionOverride, damageMultiplier);
        if (spell.kind == MagicKind.Teleport)
        {
            Teleport(spell);
            return null;
        }

        return LaunchProjectile(spell, origin, directionOverride, damageMultiplier);
    }

    /// <summary>Punto de salida de una mano (el propio transform si no está asignado).</summary>
    public Transform GetOrigin(CastHand hand)
    {
        Transform t = hand switch
        {
            CastHand.Left  => leftOrigin,
            CastHand.Right => rightOrigin,
            _              => specialOrigin
        };
        return t ? t : transform;
    }

    public void SetInstigator(GameObject instigator) => instigatorOverride = instigator;

    /// <summary>
    /// Pone una zona (MagicKind.Zone) en un punto, a nombre de este lanzador y contra sus capas de
    /// daño, sin gesto ni coste (la del remate aéreo, INC-652).
    /// </summary>
    public GameObject PonerZonaEn(MagicSpellSO zona, Vector3 punto, float damageMultiplier = 1f) =>
        SpawnZoneAt(zona, punto, Instigator, GetDamageLayers(), damageMultiplier);

    // === Lanzamiento ==========================================================

    private IEnumerator Co_SpawnAfterDelay(MagicSpellSO spell, Transform origin, float damageMultiplier)
    {
        // El clip de lanzamiento lleva dentro la carga y la salida (están diseñados así), así
        // que suena al empezar el gesto. La salida aparte es para los hechizos que la llevan
        // por capas: entonces el evento se encarga de la preparation y de la salida.
        SonarLanzamiento(spell);

        float d = Mathf.Max(0f, spell.castDelaySeconds);
        if (d > 0f) yield return new WaitForSeconds(d);

        if (spell.kind == MagicKind.Zone)
        {
            SpawnZoneNow(spell, origin, null, damageMultiplier);
            SonarSalida(spell);
        }
        else if (spell.kind == MagicKind.Teleport)
        {
            Teleport(spell);
            SonarSalida(spell);
        }
        else if (spell.chargeTime > 0f)
        {
            yield return Co_SpawnWithCharge(spell, origin, damageMultiplier);
        }
        else
        {
            SpawnNow(spell, origin, playSFX: false, damageMultiplier: damageMultiplier);
            SonarSalida(spell);
        }
    }

    /// Cierra el bucle de carga si quedó abierto (hechizo cancelado o destruido a mitad).
    private void CerrarCarga(string loopCarga)
    {
        if (string.IsNullOrEmpty(loopCarga) || AudioService.Instance == null) return;
        AudioService.Instance.StopLoopingSFX(loopCarga, 0.2f);
    }

    /// El lanzamiento: el clip de toda la vida (que lleva dentro carga y salida) o el evento
    /// por capas, que es el que separa la preparación de la salida.
    private void SonarLanzamiento(MagicSpellSO spell)
    {
        if (spell == null || AudioService.Instance == null) return;
        if (spell.castEvent != null && spell.castEvent.TieneCapas())
        {
            AudioService.Instance.PlayEvent(spell.castEvent, null);
            return;
        }
        if (!string.IsNullOrEmpty(spell.castSFXKey)) AudioService.Instance.PlaySFX(spell.castSFXKey);
    }

    /// La salida: solo suena si el hechizo tiene su propio sonido de salida. El clip de
    /// lanzamiento ya lleva la suya dentro, así que no se repite.
    private void SonarSalida(MagicSpellSO spell)
    {
        if (spell == null || AudioService.Instance == null) return;
        if (!string.IsNullOrEmpty(spell.releaseSFXKey)) AudioService.Instance.PlaySFX(spell.releaseSFXKey);
    }

    private float GetSpeedMultiplier()
    {
        if (_flyingController != null && _flyingController.IsFlying)
            return flyingSpeedMultiplier;

        if (_animator != null)
        {
            bool isGrounded = _animator.GetBool(HashIsGrounded);
            float inputMag  = _animator.GetFloat(HashInputMagnitude);
            if (isGrounded && inputMag > 1.05f) // InputMagnitude > 1 = sprint en Invector
                return sprintSpeedMultiplier;
        }

        return 1f;
    }

    private GameObject Instigator => instigatorOverride ? instigatorOverride : gameObject;

    private LayerMask GetDamageLayers()
    {
        if (projectileSettings != null)
            return projectileSettings.damageableLayers;
        return LayerMask.GetMask("Enemy", "Boss");
    }

    private MagicProjectile.ProjectileConfig BuildProjectileConfig(MagicSpellSO spell, float speed, float damageMultiplier)
    {
        return new MagicProjectile.ProjectileConfig
        {
            damage          = spell.damage * damageMultiplier,
            aoeRadius       = spell.aoeRadius,
            knockbackForce  = spell.knockbackForce,
            hitLayers       = GetDamageLayers(),
            collisionLayers = GetDamageLayers(),
            destroyOnHit    = spell.destroyOnHit,
            lifeTime        = spell.lifeTime,
            maxRange        = spell.maxRange,
            initialSpeed    = speed,
            useGravity      = spell.useGravity,
            impactVFX       = spell.impactVFX,
            despawnVFX      = spell.despawnVFX,
            vfxLifetime     = spell.vfxLifetime,
            impactSFXKey    = spell.impactSFXKey,
            impactEvent     = spell.impactEvent,
            element         = spell.element,
            isPrecise       = spell.isRuntimePreciseInstance
        };
    }

    /// Posición de salida: el origen, adelantado forwardOffset en la dirección de tiro, más
    /// positionOffset (Y en mundo; X y Z en el espacio local del origen).
    private static Vector3 ComputeSpawnPosition(MagicSpellSO spell, Transform origin, Vector3 dir)
    {
        Vector3 spawnPos = origin.position + dir * spell.forwardOffset;
        if (spell.positionOffset != Vector3.zero)
        {
            spawnPos.y += spell.positionOffset.y;
            if (spell.positionOffset.x != 0f || spell.positionOffset.z != 0f)
                spawnPos += origin.TransformDirection(new Vector3(spell.positionOffset.x, 0f, spell.positionOffset.z));
        }
        return spawnPos;
    }

    private void PlaySpawnVfx(MagicSpellSO spell, Vector3 position, Quaternion rotation)
    {
        if (!spell.spawnVFX) return;
        float lifetime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f;
        var fx = VfxPoolService.Instance.Play(spell.spawnVFX, position, rotation, lifetime);
        if (spell.useScaleOverride && fx != null)
            fx.localScale = spell.scaleOverride;
    }

    private GameObject InstantiateSpellPrefab(MagicSpellSO spell, Vector3 position, Quaternion rotation)
    {
        GameObject go = Instantiate(spell.prefab, position, rotation);
        if (spell.useScaleOverride)
            go.transform.localScale = spell.scaleOverride;
        return go;
    }

    private IEnumerator Co_SpawnWithCharge(MagicSpellSO spell, Transform originOverride, float damageMultiplier)
    {
        if (!spell || !spell.prefab) yield break;

        Transform origin = originOverride ? originOverride : transform;

        // El bucle de carga suena mientras el hechizo se prepara: sin él, la carga es muda.
        string loopCarga = null;
        if (!string.IsNullOrEmpty(spell.chargeSFXKey) && AudioService.Instance != null)
        {
            loopCarga = $"carga_{++_contadorDeCargas}";
            AudioService.Instance.PlayLoopingSFX(loopCarga, spell.chargeSFXKey, 0.9f);
        }

        Vector3 dir = ResolveProjectileDirection(spell, origin, null);
        Vector3 spawnPos = ComputeSpawnPosition(spell, origin, dir);
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        PlaySpawnVfx(spell, spawnPos, spawnRt);
        GameObject go = InstantiateSpellPrefab(spell, spawnPos, spawnRt);
        if (go == null)
        {
            CerrarCarga(loopCarga);
            yield break;
        }

        _chargingProjectiles.Add(go);

        // Física en pausa mientras carga (kinematic; no se toca la velocidad de un cuerpo kinematic).
        Rigidbody cachedRb = null;
        bool cachedKinematic = false;
        if (go.TryGetComponent<Rigidbody>(out var rbDuringCharge))
        {
            cachedRb = rbDuringCharge;
            cachedKinematic = rbDuringCharge.isKinematic;
            rbDuringCharge.isKinematic = true;
            rbDuringCharge.useGravity = false;
        }

        Transform previousParent = null;
        if (spell.followOriginDuringCharge)
        {
            previousParent = go.transform.parent;
            go.transform.SetParent(origin, worldPositionStays: true);
        }

        IgnoreCollisionsBetween(go, Instigator);

        MagicProjectile mp = null;
        if (go.TryGetComponent<MagicProjectile>(out var proj))
        {
            mp = proj;
            mp.Configure(BuildProjectileConfig(spell, spell.initialSpeed * GetSpeedMultiplier(), damageMultiplier), Instigator);
            mp.ConfigureExtras(spell, GetDamageLayers(), damageMultiplier);
            mp.SetKinematic(true);
        }

        Vector3 targetScale = spell.useScaleOverride ? spell.scaleOverride : go.transform.localScale;
        Vector3 initialScale = spell.chargeStartScale > 0f ? targetScale * spell.chargeStartScale : targetScale;
        go.transform.localScale = initialScale;

        float charge = Mathf.Max(0f, spell.chargeTime);
        float elapsed = 0f;
        while (elapsed < charge)
        {
            elapsed += Time.deltaTime;
            float t = charge > 0.001f ? Mathf.Clamp01(elapsed / charge) : 1f;
            if (go != null) go.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
            yield return null;
            if (go == null)
            {
                CerrarCarga(loopCarga);
                yield break;
            }
        }
        if (go != null) go.transform.localScale = targetScale;

        if (spell.followOriginDuringCharge)
            go.transform.SetParent(previousParent, worldPositionStays: true);

        _chargingProjectiles.Remove(go);

        // Recalcular la dirección exacta hacia el objetivo en el fotograma en que se suelta el proyectil
        Vector3 releaseDir = ResolveProjectileDirection(spell, origin, null);
        go.transform.rotation = Quaternion.LookRotation(releaseDir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        // La velocidad se calcula al soltar: durante la carga el jugador puede empezar o dejar de
        // volar o esprintar (INC-049).
        float effectiveSpeed = spell.initialSpeed * GetSpeedMultiplier();

        // Al soltar se cierra el bucle de carga y suena la salida.
        if (loopCarga != null && AudioService.Instance != null)
        {
            AudioService.Instance.StopLoopingSFX(loopCarga, 0.25f);
            loopCarga = null;
        }
        SonarSalida(spell);

        if (mp != null)
        {
            mp.SetKinematic(false);
            mp.Launch(releaseDir, effectiveSpeed, spell.useGravity);
        }
        else if (cachedRb != null)
        {
            cachedRb.isKinematic = cachedKinematic;
            cachedRb.useGravity = spell.useGravity;
            if (!cachedRb.isKinematic)
            {
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.linearVelocity = releaseDir * Mathf.Max(0f, effectiveSpeed);
            }
        }
    }

    /// <summary>
    /// Devuelve el Transform del objetivo actual fijado o enfocado.
    /// Prioriza PlayerTargeting.CurrentTarget, y si no hay, consulta el LockTarget de la cámara o CombatCameraTargeting.
    /// </summary>
    public Transform GetAimedTarget()
    {
        if (targeting == null)
        {
            targeting = GetComponentInParent<PlayerTargeting>() ?? GetComponentInChildren<PlayerTargeting>();
            if (targeting == null && PlayerService.Player != null)
                targeting = PlayerService.Player.GetComponentInChildren<PlayerTargeting>();
        }

        if (targeting != null && targeting.CurrentTarget != null)
            return targeting.CurrentTarget;

        if (CombatCameraTargeting.Instance != null && CombatCameraTargeting.Instance.CurrentTarget != null)
            return CombatCameraTargeting.Instance.CurrentTarget.transform;

        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent<vThirdPersonCamera>(out var tpCam) && tpCam.LockTarget != null)
            return tpCam.LockTarget;

        if (ActiveCombatRegistry.Count > 0 && PlayerService.Player != null)
        {
            var nearest = ActiveCombatRegistry.GetClosestCombatNPC(PlayerService.Player.transform.position, 45f);
            if (nearest != null) return nearest.transform;
        }

        return null;
    }

    private static Vector3 GetTargetCenter(Transform target)
    {
        var col = target.GetComponentInParent<Collider>();
        if (col == null) col = target.GetComponentInChildren<Collider>();
        return col ? col.bounds.center : target.position + Vector3.up;
    }

    /// <summary>
    /// Dirección de salida de un proyectil. Con objetivo fijado o enfocado, se dirige a su centro en 3D
    /// (también si está elevado, levitando o en el aire); sin objetivo, o con dirección forzada sin target,
    /// respeta flattenDirection para tiro libre en horizontal. Ver INC-484, INC-761.
    /// </summary>
    private Vector3 ResolveProjectileDirection(MagicSpellSO spell, Transform origin, Vector3? directionOverride)
    {
        Vector3 baseForward = transform.forward;
        if (directionOverride.HasValue)
        {
            Vector3 custom = directionOverride.Value;
            if (spell.flattenDirection) custom = Vector3.ProjectOnPlane(custom, Vector3.up).normalized;
            return custom.sqrMagnitude < 0.001f ? baseForward : custom;
        }

        Transform target = GetAimedTarget();
        if (target != null)
        {
            Vector3 targetCenter = GetTargetCenter(target);
            Vector3 toTarget = targetCenter - origin.position;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                // Con objetivo fijado, el disparo va en 3D directo a su centro (sin aplanar, para alcanzar
                // enemigos en vuelo o diferente cota).
                return toTarget.normalized;
            }
        }

        Vector3 dir = (targeting != null)
            ? targeting.GetAimDirectionFrom(origin, baseForward)
            : baseForward;
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        return dir.sqrMagnitude < 0.001f ? baseForward : dir;
    }

    private static readonly RaycastHit[] s_groundHits = new RaycastHit[16];

    /// <summary>
    /// Busca la superficie de suelo real debajo de samplePoint usando raycast hacia abajo,
    /// ignorando colisionadores de personajes (jugador, enemigos, NPCs) y triggers.
    /// </summary>
    public static bool TryFindGround(Vector3 samplePoint, LayerMask groundLayers, out Vector3 groundPoint, float checkAbove = 5f, float checkBelow = 100f)
    {
        LayerMask safeMask = groundLayers.value != 0 ? groundLayers.value : LayerMask.GetMask("Floor", "Default");
        int playerLayer = LayerMask.NameToLayer("Player");
        int projectileLayer = LayerMask.NameToLayer("Projectile");
        int enemyProjectileLayer = LayerMask.NameToLayer("ProjectileEnemy");
        int uiLayer = LayerMask.NameToLayer("UI");

        if (playerLayer != -1) safeMask &= ~(1 << playerLayer);
        if (projectileLayer != -1) safeMask &= ~(1 << projectileLayer);
        if (enemyProjectileLayer != -1) safeMask &= ~(1 << enemyProjectileLayer);
        if (uiLayer != -1) safeMask &= ~(1 << uiLayer);

        Vector3 rayStart = samplePoint + Vector3.up * checkAbove;
        float totalDist = checkAbove + checkBelow;
        int count = Physics.RaycastNonAlloc(rayStart, Vector3.down, s_groundHits, totalDist, safeMask, QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        RaycastHit bestHit = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var hit = s_groundHits[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;

            // Regla AGENTS.md: personajes viven en Default o capas de actores. Descartar solo si es personaje real.
            if (hit.collider.GetComponentInParent<NPCSimpleAnimator>() != null ||
                hit.collider.GetComponentInParent<CharacterController>() != null ||
                hit.collider.GetComponentInParent<PlayerHealthSystem>() != null)
            {
                continue;
            }

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (found)
        {
            groundPoint = bestHit.point;
            return true;
        }

        // Fallback: buscar con máscara explícita Floor y Default estático
        int floorAndDefault = LayerMask.GetMask("Floor", "Default");
        count = Physics.RaycastNonAlloc(rayStart, Vector3.down, s_groundHits, totalDist, floorAndDefault, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = s_groundHits[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.GetComponentInParent<NPCSimpleAnimator>() != null ||
                hit.collider.GetComponentInParent<CharacterController>() != null ||
                hit.collider.GetComponentInParent<PlayerHealthSystem>() != null)
            {
                continue;
            }

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (found)
        {
            groundPoint = bestHit.point;
            return true;
        }

        groundPoint = samplePoint;
        return false;
    }

    /// <summary>
    /// Hechizo de MagicKind.Zone: VFX de lanzamiento en la mano y la zona, al instante, en su
    /// sitio: centrada en el objetivo si 'zoneSnapToTarget' y hay uno, o a 'zoneRange' metros
    /// delante. Un raycast hacia abajo la apoya en el suelo real descartando personajes.
    /// </summary>
    private GameObject SpawnZoneNow(MagicSpellSO spell, Transform origin, Vector3? directionOverride, float damageMultiplier)
    {
        if (!spell || !spell.prefab) return null;

        Transform o = origin ? origin : transform;

        Transform aimedTarget = GetAimedTarget();
        Vector3 baseForward = transform.forward;
        Vector3 dir;
        if (directionOverride.HasValue)
        {
            dir = directionOverride.Value;
        }
        else if (aimedTarget != null)
        {
            Vector3 toTarget = aimedTarget.position - o.position;
            toTarget.y = 0f;
            dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : baseForward;
        }
        else if (targeting != null)
        {
            dir = targeting.GetAimDirectionFrom(o, baseForward);
        }
        else
        {
            dir = baseForward;
        }
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = baseForward;

        Vector3 zonePos;
        if (spell.zoneOnCaster)
            zonePos = Instigator.transform.position;   // Nova de Luz, Brisa Sanadora (INC-500/501)
        else if (spell.zoneSnapToTarget && aimedTarget != null)
            zonePos = aimedTarget.position;
        else
            zonePos = Instigator.transform.position + dir * spell.zoneRange;

        // Se eleva zoneGroundOffset sobre el suelo real descartando personajes
        if (TryFindGround(zonePos, spell.zoneGroundLayers, out Vector3 groundHitPoint))
        {
            zonePos.y = groundHitPoint.y + spell.zoneGroundOffset;
        }
        else
        {
            if (TryFindGround(Instigator.transform.position, spell.zoneGroundLayers, out Vector3 casterGround))
                zonePos.y = casterGround.y + spell.zoneGroundOffset;
            else
                zonePos.y = Instigator.transform.position.y + spell.zoneGroundOffset;
        }

        Quaternion handRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);
        PlaySpawnVfx(spell, o.position + dir * spell.forwardOffset, handRt);

        GameObject go = InstantiateSpellPrefab(spell, zonePos, Quaternion.identity);

        if (go.TryGetComponent<MagicZoneEffect>(out var zone))
        {
            var cfg = new MagicZoneEffect.ZoneConfig
            {
                damagePerTick  = spell.damage * damageMultiplier,
                element        = spell.element,
                tickInterval   = spell.zoneTickInterval,
                radius         = spell.zoneRadius,
                duration       = spell.zoneDuration,
                knockbackForce = spell.knockbackForce,
                hitLayers      = GetDamageLayers(),
                tickSFXKey     = spell.impactSFXKey,
                despawnVFX     = spell.despawnVFX,
                vfxLifetime    = spell.vfxLifetime,
                status         = spell.statusEffect,
                statusDuration = spell.statusDuration,
                statusStrength = spell.statusStrength,
                statusVFX      = spell.statusVFX,
                healPerTick        = spell.healPerTick,
                groupShieldSeconds = spell.groupShieldSeconds,
                groupShieldFactor  = spell.groupShieldDamageFactor,
                groupShieldVFX     = spell.groupShieldVFX,
                teamGaugeGain      = spell.teamGaugeGain
            };
            zone.Configure(cfg, Instigator);
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
        {
            Debug.LogWarning($"[MagicProjectileSpawner] El prefab de '{spell.displayName}' es MagicKind.Zone pero no tiene MagicZoneEffect.");
        }
#endif

        return go;
    }

    // === Paso corto (INC-502) ===================================================

    static readonly RaycastHit[] s_teleportHits = new RaycastHit[16];

    /// <summary>
    /// Teletransporta al lanzador hasta spell.teleportDistance metros hacia donde apunta (el
    /// objetivo o su frente). Prueba de lejos a cerca y se queda con el primer punto que tenga suelo
    /// de NavMesh, no esté detrás de una pared y no cambie mucho de altura. Efecto en la salida
    /// (spawnVFX) y en la llegada (prefab del hechizo).
    /// </summary>
    private void Teleport(MagicSpellSO spell)
    {
        GameObject body = Instigator;
        if (body == null) return;
        Transform t = body.transform;

        Vector3 dir = targeting != null ? targeting.GetAimDirectionFrom(t, t.forward) : t.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = t.forward;
        dir.Normalize();

        Vector3 from = t.position;
        Vector3 chest = from + Vector3.up * 1f;
        float max = Mathf.Max(1f, spell.teleportDistance);
        Vector3? destino = null;

        for (float d = max; d >= 1f; d -= 0.5f)
        {
            Vector3 candidate = from + dir * d;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var nav, 1.2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (Mathf.Abs(nav.position.y - from.y) > 2f) continue;
            if (HayParedEntre(chest, nav.position + Vector3.up * 1f, body)) continue;
            destino = nav.position;
            break;
        }

        if (destino == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[MagicProjectileSpawner] {spell.displayName}: no hay sitio libre delante; no se mueve.");
#endif
            return;
        }

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        if (spell.spawnVFX) VfxPoolService.Instance.Play(spell.spawnVFX, from, rot, spell.vfxLifetime > 0f ? spell.vfxLifetime : 2f);

        var rb = body.GetComponent<Rigidbody>();
        var cc = body.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;
        t.SetPositionAndRotation(destino.Value, rot);
        if (rb != null)
        {
            rb.position = destino.Value;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        }
        if (cc != null) cc.enabled = true;

        if (spell.prefab)
        {
            var arrival = Instantiate(spell.prefab, destino.Value, rot);
            Destroy(arrival, spell.vfxLifetime > 0f ? spell.vfxLifetime : 2f);
        }
    }

    private static bool HayParedEntre(Vector3 a, Vector3 b, GameObject ignorar)
    {
        Vector3 delta = b - a;
        float dist = delta.magnitude;
        if (dist < 0.01f) return false;
        int n = Physics.SphereCastNonAlloc(a, 0.3f, delta / dist, s_teleportHits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = s_teleportHits[i].collider;
            if (c == null) continue;
            if (ignorar != null && (c.transform == ignorar.transform || c.transform.IsChildOf(ignorar.transform))) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // objetos sueltos, enemigos con física
            if (c.GetComponentInParent<Damageable>() != null) continue;                     // enemigos y compañeros no son pared
            if (RejaAtravesable.EsAtravesable(c)) continue;                                // rejas que el paso corto cruza
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pone una zona (MagicKind.Zone) en un punto, sin gesto ni coste: la que deja un proyectil al
    /// impactar (INC-497). Se apoya en el suelo que haya debajo.
    /// </summary>
    public static GameObject SpawnZoneAt(MagicSpellSO spell, Vector3 position, GameObject instigator, LayerMask hitLayers, float damageMultiplier = 1f)
    {
        if (spell == null || spell.prefab == null || spell.kind != MagicKind.Zone) return null;

        Vector3 pos = position;
        if (TryFindGround(pos, spell.zoneGroundLayers, out Vector3 groundHitPoint))
            pos.y = groundHitPoint.y + spell.zoneGroundOffset;
        else if (instigator != null)
            pos.y = instigator.transform.position.y + spell.zoneGroundOffset;

        GameObject go = Instantiate(spell.prefab, pos, Quaternion.identity);
        if (spell.useScaleOverride) go.transform.localScale = spell.scaleOverride;

        if (go.TryGetComponent<MagicZoneEffect>(out var zone))
        {
            zone.Configure(new MagicZoneEffect.ZoneConfig
            {
                damagePerTick  = spell.damage * damageMultiplier,
                element        = spell.element,
                tickInterval   = spell.zoneTickInterval,
                radius         = spell.zoneRadius,
                duration       = spell.zoneDuration,
                knockbackForce = spell.knockbackForce,
                hitLayers      = hitLayers,
                tickSFXKey     = spell.impactSFXKey,
                despawnVFX     = spell.despawnVFX,
                vfxLifetime    = spell.vfxLifetime,
                status         = spell.statusEffect,
                statusDuration = spell.statusDuration,
                statusStrength = spell.statusStrength,
                statusVFX      = spell.statusVFX,
                healPerTick        = spell.healPerTick,
                groupShieldSeconds = spell.groupShieldSeconds,
                groupShieldFactor  = spell.groupShieldDamageFactor,
                groupShieldVFX     = spell.groupShieldVFX,
                teamGaugeGain      = spell.teamGaugeGain
            }, instigator);
        }
        return go;
    }

    private GameObject LaunchProjectile(MagicSpellSO spell, Transform origin, Vector3? directionOverride, float damageMultiplier)
    {
        if (!spell || !spell.prefab) return null;

        Transform o = origin ? origin : transform;
        Vector3 baseDir = ResolveProjectileDirection(spell, o, directionOverride);

        // Abanico (INC-497): N proyectiles repartidos en spreadAngle alrededor de la dirección.
        int count = Mathf.Max(1, spell.spreadCount);
        GameObject first = null;
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-spell.spreadAngle * 0.5f, spell.spreadAngle * 0.5f, i / (float)(count - 1));
            Vector3 d = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
            GameObject one = LaunchOne(spell, o, d, damageMultiplier);
            if (first == null) first = one;
        }
        return first;
    }

    private GameObject LaunchOne(MagicSpellSO spell, Transform o, Vector3 dir, float damageMultiplier)
    {
        float effectiveSpeed = spell.initialSpeed * GetSpeedMultiplier();
        Vector3 spawnPos = ComputeSpawnPosition(spell, o, dir);
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        PlaySpawnVfx(spell, spawnPos, spawnRt);
        GameObject go = InstantiateSpellPrefab(spell, spawnPos, spawnRt);

        IgnoreCollisionsBetween(go, Instigator);

        if (go.TryGetComponent<MagicProjectile>(out var mp))
        {
            mp.Configure(BuildProjectileConfig(spell, effectiveSpeed, damageMultiplier), Instigator);
            mp.ConfigureExtras(spell, GetDamageLayers(), damageMultiplier);
            mp.Launch(dir, effectiveSpeed, spell.useGravity);
        }
        else if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = spell.useGravity;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = dir * Mathf.Max(0f, effectiveSpeed);
        }

        return go;
    }

    // Ignora las colisiones del proyectil con el lanzador. Los colliders del proyectil se apagan
    // un paso de física para que el motor procese el IgnoreCollision antes del primer contacto.
    private void IgnoreCollisionsBetween(GameObject projectile, GameObject instigator)
    {
        if (!ignoreCasterColliders || projectile == null || instigator == null) return;

        var projCols = projectile.GetComponentsInChildren<Collider>(true);
        var instigatorCols = instigator.GetComponentsInChildren<Collider>(true);

        foreach (var pc in projCols)
            if (pc) pc.enabled = false;

        foreach (var pc in projCols)
        {
            if (!pc) continue;
            foreach (var ic in instigatorCols)
                if (ic) Physics.IgnoreCollision(pc, ic, true);
        }

        StartCoroutine(ReenableCollidersNextFrame(projCols));
    }

    private IEnumerator ReenableCollidersNextFrame(Collider[] colliders)
    {
        yield return new WaitForFixedUpdate();
        foreach (var pc in colliders)
            if (pc) pc.enabled = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!targeting)  targeting  = GetComponentInParent<PlayerTargeting>();
        if (!instigatorOverride) instigatorOverride = gameObject;
    }
#endif
}
