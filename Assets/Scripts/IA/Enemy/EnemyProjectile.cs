using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Proyectil disparado por el boss demonio
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour, IDevolvible
{
    [Header("Configuración")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("Segundos que vive el efecto de impacto (pool de VFX).")]
    [SerializeField, Min(0.1f)] private float vidaDelImpacto = 2f;

    [Header("Daño")]
    [Tooltip("Daño usado cuando Initialize() recibe un valor negativo (ej: -1). Permite que el prefab defina su propio daño por defecto.")]
    [SerializeField] private float baseDamage = 10f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al spawnearse el proyectil")]
    [SerializeField] private string spawnSFXKey;
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al impactar/explotar")]
    [SerializeField] private string impactSFXKey;
    
    private Vector3 direction;
    private float damage;
    private bool initialized = false;
    // FIX INC-082: velocidad real usada en vuelo. Antes Initialize()/FixedUpdate() usaban siempre el
    // campo serializado `speed` (fijo en el prefab, ej. 15), ignorando por completo el rockSpeed/
    // rockRainSpeed que configura GolemBossAI por Inspector — SetupRockProjectile/RockThrowAttack
    // asignaban rb.linearVelocity con la velocidad correcta, pero Initialize() la pisaba un instante
    // después con `direction * speed` (el campo del prefab), y FixedUpdate() la seguía reafirmando
    // así en cada physics tick. El resultado: todas las rocas del Golem viajaban siempre a la
    // velocidad fija del prefab, nunca a la que se veía en el Inspector — la predicción de impacto
    // (PredictThrowTargetPosition) se calculaba para una velocidad que el proyectil real nunca tenía.
    // SetSpeed() permite que quien lanza el proyectil fuerce la velocidad real antes de Initialize().
    private float _speedOverride = -1f;
    private float EffectiveSpeed => _speedOverride > 0f ? _speedOverride : speed;

    /// <summary>Daño configurado de este proyectil (usado p. ej. por ProjectileCollisionHandler al sumar daños en un choque de hechizos).</summary>
    public float Damage => damage;
    /// <summary>Dirección de vuelo. Lo usa el contraataque para devolverlo por donde vino (INC-493).</summary>
    public Vector3 Direction => rb != null && rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : direction;

    // Amenaza para el aviso de combate (INC-666): mientras vuela está en AmenazasAlJugador.
    public Vector3 PosicionDeAmenaza => transform.position;
    public Vector3 VelocidadDeAmenaza => rb != null && !rb.isKinematic && rb.linearVelocity.sqrMagnitude > 0.01f
        ? rb.linearVelocity
        : direction * EffectiveSpeed;
    public TipoDeAmenaza TipoDeAmenaza => TipoDeAmenaza.Devolvible;

    public GameObject Lanzador { get; private set; }
    public float DanoEntrante => damage;
    public float VelocidadDeVuelo => EffectiveSpeed;
    public void AsignarLanzador(GameObject lanzador) => Lanzador = lanzador;
    public void Devolver(GameObject nuevoDueno, Transform objetivo, Vector3 direccionSiNoHayObjetivo, float dano)
    {
        if (!initialized || hasHit) return;
        float velocidad = VelocidadDeAmenaza.magnitude;
        hasHit = true;
        var devuelto = GetComponent<ProyectilDevuelto>() ?? gameObject.AddComponent<ProyectilDevuelto>();
        devuelto.TomarControl(this, nuevoDueno, objetivo, direccionSiNoHayObjetivo, velocidad, dano, hitEffectPrefab, Lanzador);
    }
    void OnEnable() { if (initialized && !hasHit) AmenazasAlJugador.Registrar(this); }
    void OnDisable() => AmenazasAlJugador.Quitar(this);
    private bool hasHit = false;
    private float _spawnTime;
    private Rigidbody rb;
    private System.Collections.Generic.List<GameObject> _attachedVfx;
    
    private Collider[] _playerDetectionBuffer = new Collider[8];
    private RaycastHit[] _sweepHitBuffer = new RaycastHit[8];
    private int _playerLayerMask;
    private float _nextProximityCheck;
    // FIX INC-027: posición del último chequeo de proximidad, usada para barrer (sweep) todo el
    // tramo recorrido desde entonces. Antes se comprobaba solo un punto instantáneo cada 0.05s;
    // con rocas rápidas (rockSpeed=18, rockRainSpeed=12) el proyectil podía avanzar más que el
    // radio de detección entre dos chequeos y "pasar por encima" del jugador sin llegar a
    // solaparse con él en ningún muestreo puntual.
    private Vector3 _lastCheckedPosition;
    private bool _hasLastCheckedPosition;


    // Layers cacheados en Awake para evitar string lookups en OnTriggerEnter
    private int _enemyLayer;
    private int _transparentFXLayer;
    private int _enemyProjectileLayer;
    private int _projectileEnemyLayer;
    private int _interactableLayer;
    private int _interactHintLayer;
    private int _minimapLayer;
    private int _uiLayer;
    private int _pauseUILayer;
    private int _playerLayer;
    private int _projectileLayer;
    private int _defaultLayer;
    // FIX INC-082: la sala del Golem (BossArenaController.floorLayer) usa el layer dedicado "Floor"
    // (índice 10 en TagManager.asset), no "Default", para su suelo. Antes este script no lo conocía
    // por nombre — el suelo de esa sala solo caía en el fallback genérico `if (!other.isTrigger)` del
    // final de OnTriggerEnter, que SÍ funciona pero no deja rastro claro en el flujo de código de que
    // el suelo del Golem se maneja aquí. Se cachea explícitamente para que quede documentado y para
    // poder tratarlo igual que "Default" sin depender del fallback silencioso.
    private int _floorLayer;

    // AoE al impactar (configurado en tiempo de ejecución para lluvia de rocas)
    private bool _dealAoEOnImpact;
    private float _aoeRadius;
    private float _aoeDamage;
    private float _groundChargeDuration;
    private Renderer[] _renderersForGroundCharge;
    private MaterialPropertyBlock _groundChargeProperties;
    private bool _bypassInvulnerabilityOnHit;
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    // VFX de "grieta en el suelo" al impactar contra terreno (configurado por GolemBossAI para que
    // sus rocas reutilicen el mismo VFX que el aterrizaje de Fase 3 — ver ConfigureGroundImpactVfx)
    private GameObject _groundCrackVfx;
    private float _groundCrackVfxScale = 1f;
    private GameObject _groundDustVfx;
    private float _groundDustVfxScale = 1f;

    void Awake()
    {
        // ✅ Configurar el collider - NO forzamos isTrigger para permitir OnCollisionEnter con el player
        var col = GetComponent<SphereCollider>();
        if (col) col.radius = 0.5f;

        _playerLayerMask = LayerMask.GetMask("Player");

        // Cachear layers para evitar string lookups en OnTriggerEnter (por frame con muchos proyectiles)
        _enemyLayer          = LayerMask.NameToLayer("Enemy");
        _transparentFXLayer  = LayerMask.NameToLayer("TransparentFX");
        _enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");
        _projectileEnemyLayer = LayerMask.NameToLayer("ProjectileEnemy");
        _interactableLayer   = LayerMask.NameToLayer("Interactable");
        _interactHintLayer   = LayerMask.NameToLayer("InteractHint");
        _minimapLayer        = LayerMask.NameToLayer("Minimap");
        _uiLayer             = LayerMask.NameToLayer("UI");
        _pauseUILayer        = LayerMask.NameToLayer("PauseUI");
        _playerLayer         = LayerMask.NameToLayer("Player");
        _projectileLayer     = LayerMask.NameToLayer("Projectile");
        _defaultLayer        = LayerMask.NameToLayer("Default");
        _floorLayer          = LayerMask.NameToLayer("Floor");

        rb = GetComponent<Rigidbody>();
        _renderersForGroundCharge = GetComponentsInChildren<Renderer>(true);
        _groundChargeProperties = new MaterialPropertyBlock();
        if (rb)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true;
        }

    }

    public void Initialize(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        // FIX INC-024/027: un valor negativo (ej: -1) es el sentinel "usa el daño del prefab".
        // Antes no existía este fallback y el daño quedaba en -1, por lo que
        // PlayerHealthSystem.TakeDamage() lo descartaba (damageAmount <= 0f) y la roca no dañaba nunca.
        damage = dmg >= 0f ? dmg : baseDamage;
        initialized = true;
        AmenazasAlJugador.Registrar(this);
        _spawnTime = Time.time;
        _hasLastCheckedPosition = false; // reinicia el sweep de proximidad (INC-027)

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EnemyProjectile] Inicializado — daño: {damage}, velocidad: {EffectiveSpeed}");
#endif

        // 🔊 Reproducir SFX de spawn
        if (!string.IsNullOrEmpty(spawnSFXKey))
        {
            AudioService.Instance?.PlaySFX(spawnSFXKey, worldPosition: transform.position);
        }

        // ✅ Aplicar velocidad inicial usando física
        // FIX INC-082: usar EffectiveSpeed (respeta SetSpeed(), si se llamó antes) en vez de `speed`
        // a secas — ver comentario en la declaración de _speedOverride.
        if (rb)
        {
            rb.linearVelocity = direction * EffectiveSpeed;
        }
        
        // Destruir después del tiempo de vida
        Invoke(nameof(Expirar), lifetime);
    }

    private void Expirar() => Destroy(gameObject);

    /// <summary>
    /// FIX INC-082: permite que quien lanza el proyectil (GolemBossAI: rockSpeed/rockRainSpeed)
    /// fuerce una velocidad real distinta a la serializada en el prefab (`speed`). Debe llamarse
    /// ANTES de Initialize() — Initialize()/FixedUpdate() usan EffectiveSpeed, que prioriza este
    /// valor sobre el campo del prefab si se ha configurado uno (> 0).
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        if (newSpeed > 0f) _speedOverride = newSpeed;
    }

    /// <summary>
    /// Activa el daño de área cuando el proyectil impacta el suelo u otro obstáculo sólido.
    /// El daño de área ignora los fotogramas de invulnerabilidad y respeta el escudo del jugador.
    /// </summary>
    public void ConfigureAoE(float radius, float aoeDmg, float groundChargeDuration = 0f)
    {
        _dealAoEOnImpact = true;
        _aoeRadius = radius;
        _aoeDamage = aoeDmg;
        _groundChargeDuration = Mathf.Max(0f, groundChargeDuration);
        _bypassInvulnerabilityOnHit = true;
    }

    private bool IniciarCargaDePlasmaEnElSuelo()
    {
        if (!_dealAoEOnImpact || _groundChargeDuration <= 0f) return false;

        CancelInvoke(nameof(Expirar));
        hasHit = true;
        AmenazasAlJugador.Quitar(this);
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        StartCoroutine(CargarYExplotarEnElSuelo());
        return true;
    }

    private IEnumerator CargarYExplotarEnElSuelo()
    {
        float transcurrido = 0f;
        int faseColor = -1;
        while (transcurrido < _groundChargeDuration)
        {
            float progreso = transcurrido / _groundChargeDuration;
            int nuevaFase = Mathf.Min(2, Mathf.FloorToInt(progreso * 3f));
            if (nuevaFase != faseColor)
            {
                faseColor = nuevaFase;
                Color color = faseColor == 0
                    ? new Color(0.2f, 0.9f, 1f, 1f)
                    : faseColor == 1
                        ? new Color(1f, 0.9f, 0.12f, 1f)
                        : new Color(1f, 0.18f, 0.06f, 1f);
                AplicarColorDeCarga(color);
            }

            transcurrido += Time.deltaTime;
            yield return null;
        }

        ApplyAoEImpact();
        DestroyProjectile();
    }

    private void AplicarColorDeCarga(Color color)
    {
        if (_renderersForGroundCharge == null) return;
        _groundChargeProperties ??= new MaterialPropertyBlock();
        foreach (var render in _renderersForGroundCharge)
        {
            if (!render) continue;
            render.GetPropertyBlock(_groundChargeProperties);
            _groundChargeProperties.SetColor(BaseColorProperty, color);
            _groundChargeProperties.SetColor(ColorProperty, color);
            _groundChargeProperties.SetColor(EmissionColorProperty, color * 1.5f);
            render.SetPropertyBlock(_groundChargeProperties);
        }
    }

    /// <summary>
    /// Configura el VFX de "grieta en el suelo" que se reproduce cuando esta roca impacta contra
    /// el suelo/terreno (no contra el jugador). Pensado para que las rocas del Golem (lanzadas o
    /// caídas de la lluvia) reutilicen el mismo VFX que su aterrizaje de Fase 3
    /// (shockwaveVFX + landingDustVFX), en vez de solo el hitEffectPrefab genérico del proyectil.
    /// Cualquiera de los dos prefabs puede ser null.
    /// </summary>
    public void ConfigureGroundImpactVfx(GameObject crackVfx, float crackScale, GameObject dustVfx, float dustScale)
    {
        _groundCrackVfx = crackVfx;
        _groundCrackVfxScale = crackScale;
        _groundDustVfx = dustVfx;
        _groundDustVfxScale = dustScale;
    }

    /// <summary>
    /// Reproduce el VFX de grieta configurado (si lo hay) en la posición actual de impacto, vía
    /// VfxPoolService (regla de proyecto: ningún VFX de un solo uso con Instantiate/Destroy directo).
    /// </summary>
    private void PlayGroundImpactVfx()
    {
        if (_groundCrackVfx)
        {
            Transform vfx = VfxPoolService.Instance.Play(_groundCrackVfx, transform.position, Quaternion.identity, 2f);
            if (vfx != null) vfx.localScale = Vector3.one * _groundCrackVfxScale;
        }

        if (_groundDustVfx)
        {
            Transform dust = VfxPoolService.Instance.Play(_groundDustVfx, transform.position, Quaternion.identity, 2f);
            if (dust != null) dust.localScale = Vector3.one * _groundDustVfxScale;
        }
    }

    /// Daño de área al caer (lluvia de rocas): alcanza a todo el grupo que toque la esfera, cada
    /// uno con su vida (DanoEnZona). Ignora la invulnerabilidad tras un golpe, pero no las reglas
    /// de daño (la defensa de la B la filtra como cualquier golpe).
    private void ApplyAoEImpact() =>
        DanoEnZona.GolpearEnEsfera(transform.position, _aoeRadius, _aoeDamage, instigador: Lanzador,
            ignorarInvulnerabilidad: true);

    // Permite registrar VFX asociados al proyectil para que se destruyan cuando este desaparezca
    public void RegisterAttachedVFX(GameObject vfx, bool parentToProjectile = true)
    {
        if (vfx == null) return;
        _attachedVfx ??= new System.Collections.Generic.List<GameObject>();
        _attachedVfx.Add(vfx);
        if (parentToProjectile)
        {
            try { vfx.transform.SetParent(transform, true); } catch { }
        }
    }

    void FixedUpdate()
    {
        // ✅ Usar SOLO física para movimiento (detección de colisiones garantizada)
        if (!initialized || hasHit) return;

        // Mantener la velocidad constante
        // FIX INC-082: EffectiveSpeed (ver _speedOverride) en vez de `speed` a secas.
        if (rb)
        {
            rb.linearVelocity = direction * EffectiveSpeed;
        }

        // ✅ Detección activa del player throttleada a 20/seg para reducir OverlapSphere por proyectil.
        // CharacterController no dispara OnTriggerEnter de forma confiable, de ahí el fallback.
        if (Time.time >= _nextProximityCheck)
        {
            _nextProximityCheck = Time.time + 0.05f;
            CheckPlayerProximity();
        }
    }
    
    /// <summary>
    /// Detecta si el proyectil está cerca del player o aliados por distancia directa.
    /// Necesario porque CharacterController no dispara OnTriggerEnter de forma confiable.
    /// La defensa del jugador la aplica su PlayerHealthSystem (FiltroDeDefensa) al recibir el golpe.
    /// </summary>
    private void CheckPlayerProximity()
    {
        if (!initialized || hasHit) return;

        const float detectionRadius = 0.8f;
        Vector3 currentPos = transform.position;

        // FIX INC-027: en lugar de comprobar solo el punto actual, barremos (SphereCast) desde la
        // última posición chequeada hasta la actual. Así, aunque el proyectil avance más que el
        // radio de detección entre dos chequeos (rocas rápidas), no puede "saltarse" al jugador.
        int hitCount;
        if (_hasLastCheckedPosition)
        {
            Vector3 segment = currentPos - _lastCheckedPosition;
            float segmentLength = segment.magnitude;

            if (segmentLength > 0.001f)
            {
                hitCount = Physics.SphereCastNonAlloc(_lastCheckedPosition, detectionRadius, segment / segmentLength,
                    _sweepHitBuffer, segmentLength, _playerLayerMask);

                for (int i = 0; i < hitCount; i++)
                {
                    if (TryResolvePlayerHit(_sweepHitBuffer[i].collider))
                        return;
                }
            }
            else
            {
                hitCount = Physics.OverlapSphereNonAlloc(currentPos, detectionRadius, _playerDetectionBuffer, _playerLayerMask);
                for (int i = 0; i < hitCount; i++)
                {
                    if (TryResolvePlayerHit(_playerDetectionBuffer[i]))
                        return;
                }
            }
        }
        else
        {
            hitCount = Physics.OverlapSphereNonAlloc(currentPos, detectionRadius, _playerDetectionBuffer, _playerLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                if (TryResolvePlayerHit(_playerDetectionBuffer[i]))
                    return;
            }
        }

        _lastCheckedPosition = currentPos;
        _hasLastCheckedPosition = true;
    }

    /// <summary>
    /// Evalúa un collider detectado (por proximidad o por sweep) y aplica daño si es el jugador.
    /// Devuelve true si el proyectil impactó y ya fue destruido.
    /// </summary>
    private bool TryResolvePlayerHit(Collider hit)
    {
        if (hit == null) return false;
        if (_groundChargeDuration > 0f) return false;

        // El golpe pasa por PlayerHealthSystem: su defensa (FiltroDeDefensa) decide si la B lo
        // anula o el escudo lo reduce.
        var playerHealth = hit.GetComponent<PlayerHealthSystem>() ?? hit.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            hasHit = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[EnemyProjectile] 🎯 Impacto JUGADOR por proximidad: {damage} daño");
#endif
            playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
            DestroyProjectile();
            return true;
        }

        Transform checkTransform = hit.transform;
        for (int j = 0; j < 5 && checkTransform != null; j++)
        {
            if (checkTransform.CompareTag(GameTags.Player))
            {
                hasHit = true;
                ApplyDamage(checkTransform.gameObject);
                DestroyProjectile();
                return true;
            }
            checkTransform = checkTransform.parent;
        }

        return false;
    }

    // ✅ OnTriggerEnter: PRINCIPAL - Para colisiones con triggers (jugador, obstáculos)
    void OnTriggerEnter(Collider other)
    {
        if (!initialized || hasHit) return;
        // Grace period: ignorar colisiones inmediatas al spawnear (evita explotar dentro del lanzador)
        if (Time.time - _spawnTime < 0.15f) return;

        // Ignorar enemigos y el boss (proyectil de enemy no debe dañar a otros enemies)
        // Nota: "Boss" es un Tag, no un Layer → usar CompareTag, no LayerMask
        int otherLayer = other.gameObject.layer;

        if (_groundChargeDuration > 0f &&
            (otherLayer == _playerLayer || other.CompareTag(GameTags.Player) ||
             other.GetComponentInParent<PlayerHealthSystem>() != null))
            return;

        // FIX INC-082 (diagnóstico): log de cada impacto real para poder ver en Console, sin abrir el
        // profiler ni instrumentar nada, contra qué layer/trigger está chocando cada roca — pensado
        // para confirmar de una vez si el suelo de la sala del Golem tiene un layer/trigger inesperado.
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EnemyProjectile] OnTriggerEnter '{other.name}' layer={LayerMask.LayerToName(otherLayer)} isTrigger={other.isTrigger}");
#endif
        if (other.CompareTag("Enemy") || other.CompareTag("Boss") || otherLayer == _enemyLayer)
            return;

        // Ignorar el arena del boss (layer TransparentFX)
        if (otherLayer == _transparentFXLayer)
            return;

        // Ignorar layers que no deben detener el proyectil
        if (otherLayer == _enemyProjectileLayer || otherLayer == _projectileEnemyLayer ||
            otherLayer == _interactableLayer    || otherLayer == _interactHintLayer    ||
            otherLayer == _minimapLayer         || otherLayer == _uiLayer              ||
            otherLayer == _pauseUILayer)
        {
            return;
        }
        
        // ✅ PRIORIDAD 1: Si impacta contra el escudo del jugador
        if (other.GetComponent<PlayerShieldController.ShieldMarker>() != null)
        {
            hasHit = true;
            DestroyProjectile();
            return;
        }

        if (otherLayer == _playerLayer)
        {
            hasHit = true;
            ApplyDamage(other.gameObject);
            DestroyProjectile();
            return;
        }

        Transform checkTransform = other.transform;
        for (int i = 0; i < 5; i++)
        {
            if (checkTransform.CompareTag(GameTags.Player))
            {
                hasHit = true;
                ApplyDamage(checkTransform.gameObject);
                DestroyProjectile();
                return;
            }

            if (checkTransform.parent != null)
                checkTransform = checkTransform.parent;
            else
                break;
        }

        var playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            DestroyProjectile();
            return;
        }

        // Personajes del grupo que lleva la IA: tienen su propia vida (INC-717). Caídos, se atraviesan.
        var cuerpoDelGrupo = other.GetComponentInParent<CuerpoDelGrupo>();
        if (cuerpoDelGrupo != null)
        {
            if (!cuerpoDelGrupo.PuedeRecibirDano) return;
            hasHit = true;
            cuerpoDelGrupo.RecibirGolpe(damage, gameObject);
            DestroyProjectile();
            return;
        }

        // Otros NPCs del party sin vida propia: el proyectil los atraviesa sin dañarlos
        var partyMember = other.GetComponentInParent<Game.NPC.NPCPartyMember>();
        if (partyMember != null && partyMember.IsInParty)
            return;

        // ✅ Colisión con proyectiles del jugador (layer "Projectile")
        if (otherLayer == _projectileLayer)
        {
            hasHit = true;
            ProjectileCollisionHandler.HandleCollision(other.gameObject, gameObject, other.ClosestPoint(transform.position));
            return;
        }

        if (otherLayer == _defaultLayer || otherLayer == _floorLayer)
        {
            if (other.isTrigger)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[EnemyProjectile] '{other.name}' es suelo (layer {LayerMask.LayerToName(otherLayer)}) pero SU collider es trigger — impacto de suelo ignorado, sin VFX ni AoE.");
#endif
                return;
            }
            if (IniciarCargaDePlasmaEnElSuelo()) return;
            hasHit = true;
            PlayGroundImpactVfx();
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
            return;
        }

        if (!other.isTrigger)
        {
            if (IniciarCargaDePlasmaEnElSuelo()) return;
            hasHit = true;
            PlayGroundImpactVfx();
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
        {
            // Ni jugador, ni Default, ni ningún layer ignorado explícitamente, y el collider de
            // destino SÍ es trigger — la roca lo atraviesa sin más: nunca hay VFX de suelo ni AoE.
            // Si esto aparece para el suelo de la sala del Golem, ese es el bug: hay que quitarle
            // el Is Trigger al collider del suelo, o añadir su layer al chequeo de arriba.
            Debug.Log($"[EnemyProjectile] '{other.name}' (layer={LayerMask.LayerToName(otherLayer)}) ignorado por completo — collider trigger sin manejar. La roca lo atraviesa sin impacto.");
        }
#endif
    }
    
    // OnCollisionEnter: Fallback para colisiones físicas (si el collider NO es trigger)
    void OnCollisionEnter(Collision collision)
    {
        if (!initialized || hasHit) return;
        if (Time.time - _spawnTime < 0.15f) return;

        Collider other = collision.collider;
        OnTriggerEnter(other);
    }

    private void ApplyDamage(GameObject target)
    {
        // GetComponentInParent como fallback por si el collider que recibió el hit es un hijo del root
        var playerHealth = target.GetComponent<PlayerHealthSystem>()
                           ?? target.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
            return;
        }

        var damageable = target.GetComponent<IDamageable>()
                         ?? target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[EnemyProjectile] No se pudo aplicar daño a {target.name}");
#endif
    }

    public void DestroyProjectile()
    {
        // Detener movimiento antes de destruir
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Destruir cualquier VFX registrado/adjunto
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }

        // Efecto visual de impacto (pooled para evitar GC spikes con múltiples proyectiles)
        SpawnHitEffect();
        
        // 🔊 Reproducir SFX de impacto/explosión
        if (!string.IsNullOrEmpty(impactSFXKey))
        {
            AudioService.Instance?.PlaySFX(impactSFXKey, worldPosition: transform.position);
        }

        // Destruir el proyectil
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        AmenazasAlJugador.Quitar(this);
        // Salvaguarda: si se destruye por lifetime o desde fuera, limpiar VFX registrados
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(hitEffectPrefab, transform.position, Quaternion.identity, vidaDelImpacto);
    }
}
