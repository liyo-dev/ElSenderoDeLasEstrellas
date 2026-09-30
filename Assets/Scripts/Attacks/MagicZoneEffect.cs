using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de efecto mágica: a diferencia de <see cref="MagicProjectile"/>, no viaja — se instancia
/// ya en su posición final y aplica daño periódico a todo lo que esté dentro de su radio mientras
/// dura. Pensada para MagicKind.Zone (ver Identifiers.cs): el VFX de casteo sigue saliendo de la
/// mano del lanzador, con el mismo timing que un hechizo Projectile normal (para aprovechar la
/// animación de casteo ya existente), pero en vez de instanciarse en la mano y volar, este
/// prefab se instancia directamente en el punto de impacto calculado por
/// MagicProjectileSpawner.SpawnZoneNow() — "sale de la mano pero se materializa al instante como
/// zona", que es el pedido de diseño original (30 ago 2026).
///
/// No requiere Collider: usa Physics.OverlapSphereNonAlloc en vez de triggers físicos, mismo
/// criterio que la rama AOE de MagicProjectile.ResolveHit(), para no depender de que los
/// colliders de enemigos en movimiento entren/salgan limpiamente de un trigger.
/// </summary>
[DisallowMultipleComponent]
public class MagicZoneEffect : MonoBehaviour
{
    [System.Serializable]
    public struct ZoneConfig
    {
        public float damagePerTick;
        public float tickInterval;
        public float radius;
        public float duration;
        public float knockbackForce;      // 0 = sin empuje
        public LayerMask hitLayers;       // Capas que reciben daño (Enemy, Boss, etc.)
        public string tickSFXKey;         // opcional: SFX en cada tick que golpea a alguien
        public GameObject despawnVFX;     // VFX al terminar la duración
        public float vfxLifetime;         // tiempo antes de destruir despawnVFX (0 = 3s por defecto)
        public EstadoDeCombate status;    // estado que pone a los de dentro en cada tick (INC-499)
        public float statusDuration;
        public float statusStrength;
        public GameObject statusVFX;
        public float healPerTick;         // curación al grupo en cada tick (INC-500)
        public float groupShieldSeconds;  // escudo al grupo al aparecer (INC-500)
        public float groupShieldFactor;
        public GameObject groupShieldVFX;
        public float teamGaugeGain;       // tramos de carga de equipo al curar/proteger (una vez)
    }

    readonly List<GrupoCercano.Miembro> _grupo = new List<GrupoCercano.Miembro>(3);
    bool _shieldDone;
    bool _gaugeDone;

    ZoneConfig _cfg;
    GameObject _instigator;
    bool _configured;

    // Buffer reutilizable para no generar basura en cada tick (mismo criterio que MagicProjectile).
    readonly Collider[] _hitBuffer = new Collider[32];

    /// <summary>Inyecta la configuración de la zona y quién la lanzó. Llamar justo tras instanciar.</summary>
    public void Configure(in ZoneConfig cfg, GameObject instigator)
    {
        _cfg = cfg;
        _instigator = instigator;
        _configured = true;
    }

    void OnEnable()
    {
        if (!_configured)
        {
            // Fallback de seguridad: si se activa sin Configure() (p.ej. probado suelto en
            // escena desde el Editor), usar valores razonables para no romper nada.
            if (_cfg.radius <= 0f) _cfg.radius = 3f;
            if (_cfg.tickInterval <= 0f) _cfg.tickInterval = 0.5f;
            if (_cfg.duration <= 0f) _cfg.duration = 3f;
        }
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        float elapsed = 0f;
        // Primer tick inmediato al aparecer (sensación de "trampa que ya está mordiendo"),
        // luego uno cada tickInterval hasta agotar la duración.
        while (elapsed < _cfg.duration)
        {
            DoTick();
            float wait = Mathf.Max(0.05f, _cfg.tickInterval);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }
        End();
    }

    void DoTick()
    {
        TickSupport();

        int count = Physics.OverlapSphereNonAlloc(transform.position, _cfg.radius, _hitBuffer, ~0, QueryTriggerInteraction.Collide);
        bool hitSomething = false;
        HashSet<Damageable> alreadyHit = null;

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (!col) continue;

            // Mismo filtro que la rama AOE de MagicProjectile: solo se aplica si hitLayers está
            // explícitamente configurado (!= 0); si se deja vacío no se filtra por capa.
            if (_cfg.hitLayers.value != 0 && ((1 << col.gameObject.layer) & _cfg.hitLayers.value) == 0)
                continue;

            var d = col.GetComponent<Damageable>() ?? col.GetComponentInParent<Damageable>();
            if (d == null) continue;
            if (_instigator != null && d.gameObject == _instigator) continue; // no dañarse a sí mismo

            alreadyHit ??= new HashSet<Damageable>();
            if (!alreadyHit.Add(d)) continue; // evita doble tick si el enemigo tiene varios colliders

            if (_cfg.damagePerTick > 0f)
                d.TakeDamage(FormulasDeCombate.DanoDe(_instigator, _cfg.damagePerTick), _instigator);
            hitSomething = true;

            // Estado (INC-499): se renueva en cada tick; atraer tira hacia el centro de la zona.
            if (_cfg.status != EstadoDeCombate.Ninguno)
                EstadosDeCombate.Aplicar(d, _cfg.status, Mathf.Max(_cfg.statusDuration, _cfg.tickInterval + 0.1f),
                                         _cfg.statusStrength, transform.position, _cfg.statusVFX);

            if (_cfg.knockbackForce > 0f)
            {
                var rb = col.attachedRigidbody ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 dir = rb.worldCenterOfMass - transform.position;
                    dir.y = 0f;
                    dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
                    rb.AddForce(dir * _cfg.knockbackForce, ForceMode.Impulse);
                }
            }
        }

        if (hitSomething && !string.IsNullOrEmpty(_cfg.tickSFXKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(_cfg.tickSFXKey, worldPosition: transform.position);
        }
    }

    /// Apoyo al grupo (INC-500): curar en cada tick y escudo una vez, a los del grupo dentro.
    void TickSupport()
    {
        bool heal = _cfg.healPerTick > 0f;
        bool shield = _cfg.groupShieldSeconds > 0f && !_shieldDone;
        if (!heal && !shield) return;

        GrupoCercano.Buscar(transform.position, _cfg.radius, _grupo);
        bool helped = false;
        foreach (var m in _grupo)
        {
            if (heal) { m.Curar(_cfg.healPerTick); helped = true; }
            if (shield)
            {
                EscudoTemporal.Poner(m.ObjetoDeVida, m.cuerpo, _cfg.groupShieldSeconds, _cfg.groupShieldFactor, _cfg.groupShieldVFX);
                helped = true;
            }
        }
        if (shield) _shieldDone = true;

        if (helped && !_gaugeDone && _cfg.teamGaugeGain > 0f)
        {
            _gaugeDone = true;
            if (PlayerService.TryGetComponent<DuoSpecialAttackSystem>(out var duo, includeInactive: true, allowSceneLookup: true)
                && duo != null && duo.TeamGauge != null)
                duo.TeamGauge.AddCharge(_cfg.teamGaugeGain);
        }
    }

    void End()
    {
        if (_cfg.despawnVFX)
        {
            float lifetime = _cfg.vfxLifetime > 0f ? _cfg.vfxLifetime : 3f;
            VfxPoolService.Instance.Play(_cfg.despawnVFX, transform.position, Quaternion.identity, lifetime);
        }
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.25f);
        float r = _configured && _cfg.radius > 0f ? _cfg.radius : 4f;
        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}
