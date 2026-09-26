using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Aro de runas del primer jefe (Paso 6 del refactor Tramo 1, análisis
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §6): "las runas se iluminaban cada
/// vez que el monstruo atacaba". Un hijo con collider propio (trigger) del prefab del jefe que:
///
///  - se ilumina SOLO mientras ImpDemonAI.IsAttacking es true (la ventana de ataque real, no un
///    temporizador aparte -- si la IA cambia de ritmo, el aro la sigue sin tocar este script),
///  - solo puede recibir daño de disparos en modo preciso (MagicProjectile.IsPrecise, ver Paso 5 /
///    MagicSpellSO.BuildPreciseVariant) llegados DURANTE esa ventana -- cualquier otro impacto
///    (normal, o preciso pero fuera de la ventana) lo ignora sin consumir el proyectil,
///  - tras 'hitsToBreak' impactos válidos, se rompe: apaga su propio brillo, desactiva su
///    collider y llama a ImpDemonAI.ForceFallenByCollarBreak() -- el final alternativo del
///    combate ("el demonio caído", sin orbes, sin destruir el GameObject).
///
/// No compite con el daño normal del cuerpo: el Damageable principal del jefe sigue funcionando
/// exactamente igual que hoy en paralelo -- matarlo a golpes (con o sin modo preciso) sigue siendo
/// un final válido y llega antes si el aro no se rompe a tiempo. Este componente es un sistema
/// opcional aparte, nunca sustituye a Damageable ni reduce su vida.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RuneCollar : MonoBehaviour, IExpuestoAlDano
{
    /// El aro brilla: es el momento de hacerle daño al jefe (ver SoloDanoCuandoExpuesto, INC-469).
    public bool Expuesto => !_broken && demonAI != null && !demonAI.IsDead && demonAI.IsAttacking;

    [Header("Referencias")]
    [Tooltip("Si se deja vacío, se busca en los padres (el aro es normalmente un hijo del jefe).")]
    [SerializeField] private ImpDemonAI demonAI;

    [Header("Config")]
    [Tooltip("Impactos precisos, durante la ventana de ataque, necesarios para romper el aro.")]
    [SerializeField, Min(1)] private int hitsToBreak = 3;

    [Header("Visual (sin arte nuevo -- Paso 6)")]
    [Tooltip("Objetos que se activan SOLO mientras el aro está 'encendido' (ventana de ataque). " +
             "Por ejemplo una luz o un VFX de brillo ya existente en el proyecto, colocado como " +
             "hijo de este mismo GameObject. Vacío = sin señal visual propia (el aro sigue " +
             "funcionando por lógica, solo que no se ve iluminarse).")]
    [SerializeField] private GameObject[] glowVisuals;

    [Header("Eventos")]
    [Tooltip("Se dispara UNA vez, al romperse el aro (hitsToBreak alcanzado). ImpDemonAI ya reacciona " +
             "solo (ForceFallenByCollarBreak se llama directamente desde código, no desde este evento) " +
             "-- esto es para que además reaccione cualquier otra cosa en la escena (p. ej. una " +
             "secuencia de Eldran) sin tener que engancharse a ImpDemonAI.OnFellByCollarBreak.")]
    public UnityEvent OnCollarBroken;
    [Tooltip("(hits actuales, hits necesarios) cada vez que un impacto preciso válido cuenta.")]
    public UnityEvent<int, int> OnPreciseHitRegistered;

    private int _hits;
    private bool _broken;
    private bool _wasGlowing;
    private Collider _collider;

    // Evita contar el mismo proyectil dos veces si sus varios colliders (poco habitual, pero
    // MagicProjectile busca en GetComponentsInChildren<Collider> al ignorar colisiones con el
    // instigador) llegaran a solapar el trigger del aro en el mismo frame.
    private readonly HashSet<MagicProjectile> _consumedProjectiles = new HashSet<MagicProjectile>();

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (!demonAI) demonAI = GetComponentInParent<ImpDemonAI>();
        SetGlow(false);
    }

    void Update()
    {
        // demonAI.IsDead cubre el caso de que el jefe muera por HP normal antes de que se rompa
        // el aro: gana quien llegue primero, y en cuanto eso pasa este componente deja de tener
        // nada que hacer (no hace falta un evento propio para enterarse -- isDead ya lo dice todo).
        if (_broken || demonAI == null || demonAI.IsDead) return;

        bool glowing = demonAI.IsAttacking;
        if (glowing != _wasGlowing)
        {
            _wasGlowing = glowing;
            SetGlow(glowing);
        }
    }

    private void SetGlow(bool on)
    {
        if (glowVisuals == null) return;
        for (int i = 0; i < glowVisuals.Length; i++)
            if (glowVisuals[i]) glowVisuals[i].SetActive(on);
    }

    void OnTriggerEnter(Collider other) => TryRegisterHit(other);

    private void TryRegisterHit(Collider other)
    {
        if (_broken || other == null || demonAI == null || demonAI.IsDead) return;

        var projectile = other.GetComponent<MagicProjectile>();
        if (projectile == null) projectile = other.GetComponentInParent<MagicProjectile>();
        if (projectile == null) return;

        if (_consumedProjectiles.Contains(projectile)) return;

        // El aro ignora todo lo que no sea un disparo preciso llegado en plena ventana de ataque
        // -- ni cuenta ni consume el proyectil, que sigue su camino normal (por ejemplo, seguirá
        // haciendo el daño de cuerpo habitual vía MagicProjectile.ResolveHit contra el Damageable
        // principal del jefe, exactamente igual que si el aro no existiera).
        if (!projectile.IsPrecise || !demonAI.IsAttacking) return;

        _consumedProjectiles.Add(projectile);
        _hits++;
        OnPreciseHitRegistered?.Invoke(_hits, hitsToBreak);

        if (_hits >= hitsToBreak)
            Break();
    }

    private void Break()
    {
        if (_broken) return;
        _broken = true;

        SetGlow(false);
        if (_collider) _collider.enabled = false;

        if (demonAI != null) demonAI.ForceFallenByCollarBreak();

        OnCollarBroken?.Invoke();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!demonAI) demonAI = GetComponentInParent<ImpDemonAI>();
    }
#endif
}
