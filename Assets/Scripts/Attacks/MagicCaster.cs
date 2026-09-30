using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Invector.vCharacterController;

/// <summary>
/// Magia del jugador: los hechizos básicos que lleva (hasta <see cref="BasicSlotCount"/>, que se
/// rotan en combate), la serie de la X y el único punto por el que sale cualquier hechizo,
/// <see cref="Cast"/>: permisos, maná, giro hacia el objetivo, compromiso de
/// movimiento, sostén en el aire, gesto de la capa superior y materialización en el spawner.
/// Ver INC-483, INC-484 y INC-486.
/// </summary>
[DisallowMultipleComponent]
public class MagicCaster : MonoBehaviour
{
    public const int BasicSlotCount = 4;

    [Header("Referencias")]
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private PlayerActionManager actionManager;
    [SerializeField] private MagicProjectileSpawner spawner;
    [SerializeField] private PlayerShieldController shieldController;
    [Tooltip("Controlador del personaje: giro, bloqueo de movimiento, sostén en el aire y gestos. Se busca solo si está vacío.")]
    [FormerlySerializedAs("motor")]
    [SerializeField] private vThirdPersonController controller;

    [Header("Serie básica (X)")]
    [Tooltip("Segundos tras un lanzamiento en los que el siguiente continúa la serie (derecha, izquierda, centro). Pasado este tiempo vuelve a empezar.")]
    [SerializeField, Min(0.1f)] private float seriesWindow = 1.1f;
    [Tooltip("Multiplicador de daño del tercer golpe de la serie (centro, a dos manos).")]
    [SerializeField, Min(1f)] private float finisherDamageMultiplier = 1.5f;

    [Header("Gestos (rutas completas en la capa superior)")]
    [SerializeField] private string leftHandState = "UpperBody.Magic.MagicLeft";
    [SerializeField] private string rightHandState = "UpperBody.Magic.MagicRight";
    [SerializeField] private string centerState = "UpperBody.Magic.MagicSpecial";

    [Header("Gestos por estilo (MagicSpellSO.castStyle, fuera de la serie)")]
    [SerializeField] private string twoHandedState = "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Cast";
    [SerializeField] private string omniState = "UpperBody.Magic.HumanM@MagicAttackOmni01 - Cast";
    [Tooltip("Pose de carga previa al gesto omni (brazos juntos arriba). Es la misma del combo mágico.")]
    [SerializeField] private string omniLoadState = "UpperBody.Magic.ComboIdle";
    [Tooltip("Segundos desde que empieza el gesto omni hasta que abre los brazos. El gesto se retrasa para que ese momento coincida con la salida del hechizo (castDelaySeconds + chargeTime).")]
    [SerializeField, Min(0f)] private float omniBurstTime = 0.08f;
    [SerializeField] private string callState = "UpperBody.Magic.HumanM@MagicAttackCall1H01_L";
    [Tooltip("Gesto de los hechizos de área: brazo arriba en lo alto del saltito.")]
    [SerializeField] private string areaState = "UpperBody.Cheer01";
    [Tooltip("Impulso del saltito de los hechizos de área, respecto al salto normal.")]
    [SerializeField, Range(0.2f, 1f)] private float areaHopFactor = 0.55f;
    [Tooltip("Segundos mínimos del gesto de área (subir, brazos arriba y bajar).")]
    [SerializeField] private float areaMinSeconds = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private readonly MagicSpellSO[] _basics = new MagicSpellSO[BasicSlotCount];
    private int _activeBasic;
    private int _nextSeriesStep;
    private float _lastSeriesCastTime = -999f;
    private float _castingUntil;
    private ITargetProvider _targets;
    private Coroutine _omniRelease;

    /// <summary>Cambió la lista de básicos o el activo.</summary>
    public event Action OnLoadoutChanged;

    /// <summary>Salió un hechizo: el hechizo y la mano.</summary>
    public event Action<MagicSpellSO, CastHand> OnSpellCast;

    /// <summary>True mientras dura el gesto del último lanzamiento (retraso + carga).</summary>
    public bool IsCasting => Time.time < _castingUntil;

    public IReadOnlyList<MagicSpellSO> BasicSpells => _basics;
    public int ActiveBasicIndex => _activeBasic;
    public MagicSpellSO ActiveBasic => _basics[_activeBasic];

    /// <summary>Paso de la serie que saldrá con la próxima X: 0 derecha, 1 izquierda, 2 centro.</summary>
    public int NextSeriesStep => Time.time - _lastSeriesCastTime <= seriesWindow ? _nextSeriesStep : 0;

    void Awake()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        // La pose de carga del gesto omni no baja sola: si se corta la espera, se suelta aquí.
        if (_omniRelease == null) return;
        StopCoroutine(_omniRelease);
        _omniRelease = null;
        if (controller) controller.ReleaseUpperBodyPose();
    }

    private void ResolveReferences()
    {
        if (!manaPool) manaPool = GetComponentInParent<ManaPool>();
        if (!actionManager) actionManager = GetComponentInParent<PlayerActionManager>();
        if (!spawner) spawner = GetComponentInParent<MagicProjectileSpawner>();
        if (!shieldController) shieldController = GetComponentInParent<PlayerShieldController>();
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!controller) controller = GetComponentInChildren<vThirdPersonController>();
        _targets ??= GetComponentInParent<ITargetProvider>() ?? GetComponentInChildren<ITargetProvider>();
    }

    // === Hechizos básicos ======================================================

    /// <summary>
    /// Equipa los básicos en orden de rotación (los que sobren de <see cref="BasicSlotCount"/> se
    /// ignoran; los huecos se permiten). Conserva el activo si sigue ocupado.
    /// </summary>
    public void SetBasicSpells(IReadOnlyList<MagicSpellSO> spells)
    {
        for (int i = 0; i < BasicSlotCount; i++)
            _basics[i] = spells != null && i < spells.Count ? spells[i] : null;

        if (_basics[_activeBasic] == null)
            _activeBasic = FirstOccupiedFrom(0);
        _nextSeriesStep = 0;
        OnLoadoutChanged?.Invoke();
    }

    /// <summary>Pasa al siguiente básico ocupado. False si no hay otro al que pasar.</summary>
    public bool RotateBasic()
    {
        int next = FirstOccupiedFrom(_activeBasic + 1);
        if (next == _activeBasic) return false;
        _activeBasic = next;
        _nextSeriesStep = 0;
        OnLoadoutChanged?.Invoke();
        return true;
    }

    private int FirstOccupiedFrom(int start)
    {
        for (int k = 0; k < BasicSlotCount; k++)
        {
            int i = (start + k) % BasicSlotCount;
            if (_basics[i] != null) return i;
        }
        return 0;
    }

    /// <summary>
    /// Lanza el básico activo como siguiente golpe de la serie: derecha, izquierda y centro, el
    /// último con más daño. 'precise' pide la variante precisa si el hechizo la admite. La
    /// levitación no sale por aquí (PlayerLevitationController).
    /// </summary>
    public bool CastBasic(bool precise)
    {
        var spell = ActiveBasic;
        if (spell == null || spell.kind == MagicKind.Levitation) return false;

        int step = NextSeriesStep;
        CastHand hand = step switch { 0 => CastHand.Right, 1 => CastHand.Left, _ => CastHand.Center };
        float multiplier = step == 2 ? finisherDamageMultiplier : 1f;

        if (!Cast(spell, hand, multiplier, precise)) return false;

        _nextSeriesStep = (step + 1) % 3;
        _lastSeriesCastTime = Time.time;
        return true;
    }

    // === Lanzamiento ==========================================================

    /// <summary>
    /// Único punto de salida de un hechizo del jugador. Comprueba permisos, cobra maná, gira hacia
    /// el objetivo, bloquea el movimiento mientras dura el gesto, sostiene en el aire, reproduce el
    /// gesto de la mano y lo materializa. Sin enfriamientos: el ritmo lo marcan el gesto y el maná.
    /// </summary>
    public bool Cast(MagicSpellSO spell, CastHand hand, float damageMultiplier = 1f, bool precise = false)
    {
        if (!CanCast(spell, out string reason))
        {
            Log($"No se puede lanzar {(spell ? spell.displayName : "(nada)")}: {reason}");
            return false;
        }

        if (manaPool && !manaPool.TrySpend(spell.manaCost))
        {
            Log($"Sin maná para {spell.displayName} (coste {spell.manaCost})");
            return false;
        }

        float castLock = GetCastingLockDuration(spell);
        // Fuera de la serie (combos y centro), cada hechizo puede tener su gesto (INC-495).
        MagicCastStyle style = hand == CastHand.Center && spell.castStyle != MagicCastStyle.Hand
            ? spell.castStyle : MagicCastStyle.Hand;
        if (style == MagicCastStyle.Area) castLock = Mathf.Max(castLock, areaMinSeconds);
        _castingUntil = Time.time + castLock;

        if (controller) PlayCastGesture(style, hand, castLock, GetDirectionToTarget());

        var toSpawn = precise && spell.supportsPreciseMode ? spell.BuildPreciseVariant() : spell;
        if (spawner) spawner.Cast(toSpawn, hand, damageMultiplier);

        OnSpellCast?.Invoke(spell, hand);
        Log($"Lanzado {spell.displayName} ({hand}, x{damageMultiplier:0.##}, preciso={precise && spell.supportsPreciseMode})");
        return true;
    }

    /// <summary>
    /// Gesto del jugador en un ataque de equipo (dúo o trío, <see cref="DuoSpecialAttackSystem"/>):
    /// mismas reglas de permiso, giro hacia el objetivo, bloqueo y sostén en el aire que un hechizo,
    /// con el gesto del centro. No cobra maná (los ataques de equipo gastan la carga de equipo).
    /// Devuelve la dirección horizontal hacia la que queda mirando.
    /// </summary>
    public bool BeginTeamGesture(float lockSeconds, out Vector3 facing) =>
        BeginTeamGesture(lockSeconds, MagicCastStyle.TwoHanded, out facing);

    /// <summary>Como el anterior, con el gesto que se pida (el trío usa el de área).</summary>
    public bool BeginTeamGesture(float lockSeconds, MagicCastStyle style, out Vector3 facing)
    {
        facing = controller ? controller.transform.forward : transform.forward;

        if (IsCasting) return false;
        if (actionManager && !actionManager.CanCastMagic()) return false;
        if (shieldController != null && shieldController.IsDefending) return false;

        Vector3 toTarget = GetDirectionToTarget();
        if (toTarget.sqrMagnitude > 0.0001f) facing = toTarget.normalized;
        facing.y = 0f;

        lockSeconds = Mathf.Max(0.1f, lockSeconds);
        if (style == MagicCastStyle.Area) lockSeconds = Mathf.Max(lockSeconds, areaMinSeconds);
        _castingUntil = Time.time + lockSeconds;

        if (controller) PlayCastGesture(style, CastHand.Center, lockSeconds, facing);
        return true;
    }

    /// <summary>
    /// Contraataque (B en el momento justo, INC-493): devuelve el golpe como un hechizo propio, al
    /// instante y sin maná. Usa el básico activo o, si no es un proyectil, el siguiente que lo sea.
    /// Apunta al objetivo si lo hay; si no, a 'fallbackDirection' (por donde vino el proyectil).
    /// </summary>
    public bool CastCounter(float damage, Vector3 fallbackDirection, float lockSeconds)
    {
        MagicSpellSO spell = FindCounterSpell();
        if (!spell || !spawner) return false;

        Vector3 toTarget = GetDirectionToTarget();
        bool hasTarget = toTarget.sqrMagnitude > 0.0001f;
        Vector3 facing = hasTarget ? toTarget : fallbackDirection;
        facing.y = 0f;

        lockSeconds = Mathf.Max(0.1f, lockSeconds);
        _castingUntil = Time.time + lockSeconds;
        if (controller)
        {
            controller.CommitToAction(lockSeconds, facing);
            controller.HoldAirborne(lockSeconds);
            controller.PlayUpperBodyAction(centerState);
        }

        Vector3? direction = hasTarget || fallbackDirection.sqrMagnitude < 0.0001f ? (Vector3?)null : fallbackDirection.normalized;
        float multiplier = Mathf.Max(0.1f, damage / Mathf.Max(1f, spell.damage));
        spawner.SpawnNow(spell, spawner.GetOrigin(CastHand.Center), true, direction, multiplier);

        OnSpellCast?.Invoke(spell, CastHand.Center);
        Log($"Contraataque con {spell.displayName} ({damage:0.#} de daño)");
        return true;
    }

    private MagicSpellSO FindCounterSpell()
    {
        for (int k = 0; k < BasicSlotCount; k++)
        {
            var s = _basics[(_activeBasic + k) % BasicSlotCount];
            if (s && s.prefab && s.kind == MagicKind.Projectile) return s;
        }
        return null;
    }

    /// <summary>Cuerpo del jugador (el del controlador), para situar efectos de equipo.</summary>
    public Transform Body => controller ? controller.transform : transform;

    /// <summary>Objetivo actual (fijado o automático), si lo hay.</summary>
    public bool TryGetTarget(out Transform target)
    {
        target = null;
        return _targets != null && _targets.TryGetTarget(out target) && target;
    }

    /// <summary>Gesto, giro, bloqueo y sostén en el aire de un lanzamiento según su estilo.</summary>
    private void PlayCastGesture(MagicCastStyle style, CastHand hand, float lockSeconds, Vector3 facing)
    {
        if (_omniRelease != null) { StopCoroutine(_omniRelease); _omniRelease = null; }

        if (style == MagicCastStyle.Omni && lockSeconds > omniBurstTime)
        {
            // Carga con los brazos juntos y abre justo cuando sale el hechizo, no al empezar.
            controller.CommitToAction(lockSeconds, facing);
            controller.HoldAirborne(lockSeconds);
            controller.HoldUpperBodyPose(omniLoadState);
            _omniRelease = StartCoroutine(Co_OmniRelease(lockSeconds - omniBurstTime));
            return;
        }

        if (style == MagicCastStyle.Area)
        {
            // Saltito desde el suelo y brazos arriba; el sostén en el aire llega en lo alto del salto
            // (sostener antes anularía el impulso). En el aire, sin saltito: se sostiene ya.
            bool hopped = controller.Hop(areaHopFactor);
            controller.CommitToAction(lockSeconds, facing);
            controller.PlayUpperBodyActionFor(areaState, lockSeconds);
            if (hopped) StartCoroutine(Co_HoldAtApex(lockSeconds));
            else controller.HoldAirborne(lockSeconds);
            return;
        }

        controller.CommitToAction(lockSeconds, facing);
        controller.HoldAirborne(lockSeconds);
        controller.PlayUpperBodyAction(style switch
        {
            MagicCastStyle.TwoHanded => twoHandedState,
            MagicCastStyle.Omni => omniState,
            MagicCastStyle.Call => callState,
            _ => GetGestureState(hand),
        });
    }

    private System.Collections.IEnumerator Co_OmniRelease(float wait)
    {
        yield return new WaitForSeconds(wait);
        _omniRelease = null;
        if (controller) controller.PlayUpperBodyAction(omniState);
    }

    private System.Collections.IEnumerator Co_HoldAtApex(float lockSeconds)
    {
        float start = Time.time;
        yield return new WaitForSeconds(0.28f);
        float left = lockSeconds - (Time.time - start);
        if (controller && left > 0.05f) controller.HoldAirborne(left);
    }

    /// <summary>¿Se puede lanzar este hechizo ahora? 'reason' explica el porqué si no.</summary>
    public bool CanCast(MagicSpellSO spell, out string reason)
    {
        reason = "";

        if (!spell) { reason = "Sin hechizo"; return false; }

        if (actionManager && !actionManager.CanCastMagic()) { reason = "Acción bloqueada"; return false; }

        if (shieldController != null && shieldController.IsDefending) { reason = "Defendiendo"; return false; }

        // En tierra, de cara a una pendiente imposible o una pared (stopMove), no se lanza; en el
        // aire sí (INC-483).
        if (controller && !controller.IsAirborne && controller.stopMove) { reason = "Bloqueado contra una pared"; return false; }

        if (manaPool && manaPool.Current < spell.manaCost)
        {
            reason = $"Maná insuficiente ({spell.manaCost:F1} requerido, {manaPool.Current:F1} disponible)";
            return false;
        }

        return true;
    }

    public bool CanCast(MagicSpellSO spell) => CanCast(spell, out _);

    private float GetCastingLockDuration(MagicSpellSO spell)
    {
        if (!spell) return 0.1f;
        return Mathf.Max(0.1f, spell.castDelaySeconds + spell.chargeTime);
    }

    private string GetGestureState(CastHand hand) => hand switch
    {
        CastHand.Left  => leftHandState,
        CastHand.Right => rightHandState,
        _              => centerState
    };

    /// Dirección horizontal hacia el objetivo actual (fijado o automático); cero si no hay.
    private Vector3 GetDirectionToTarget()
    {
        if (_targets == null || !controller || !_targets.TryGetTarget(out Transform t) || !t)
            return Vector3.zero;
        Vector3 to = t.position - controller.transform.position;
        to.y = 0f;
        return to;
    }

    private void Log(string message)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (showDebugLogs) Debug.Log($"[MagicCaster] {message}", this);
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ResolveReferences();
    }
#endif
}
