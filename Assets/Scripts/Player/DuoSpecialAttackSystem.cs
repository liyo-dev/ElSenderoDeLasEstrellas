using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Core;
using Game.NPC;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Ataques de equipo (INC-491): una sola carga de equipo en tres tramos
/// (<see cref="SpecialChargeMeter"/>, máximo 3) que se llena con el daño que hace el grupo.
/// <list type="bullet">
/// <item>LT + RT a la vez = ataque de equipo con quien esté cerca: un compañero cerca = dúo con él
/// (un tramo); los dos cerca y la carga llena = trío (la carga entera). Con los dos cerca y la carga
/// sin llenar, dúo con el más cercano. Para elegir compañero se le acerca uno (o se usa el sígueme).</item>
/// </list>
/// Los compañeros tienen que estar en el grupo y cerca. El gesto del personaje activo pasa por
/// <see cref="MagicCaster.BeginTeamGesture"/> (giro al objetivo, bloqueo y sostén en el aire como
/// cualquier hechizo). El escudo ya no va en LT+RT: es mantener B (PlayerShieldController).
/// Vive en GrupoDelJugador (INC-484).
/// </summary>
[DisallowMultipleComponent]
public class DuoSpecialAttackSystem : MonoBehaviour
{
    [Header("Ataques")]
    [Tooltip("Un dúo por pareja (Will+Estela, Will+Liam, Estela+Liam).")]
    [SerializeField] private List<SpecialAttackSO> duoAttacks = new List<SpecialAttackSO>();
    [Tooltip("Ataque de los tres (LT+RT).")]
    [SerializeField] private SpecialAttackSO trioAttack;

    [Header("Carga de equipo")]
    [Tooltip("Medidor de la carga de equipo. Máximo 3 = tres tramos.")]
    [FormerlySerializedAs("estelaChargeMeter")]
    [SerializeField] private SpecialChargeMeter teamGauge;
    [Tooltip("Daño hecho por el grupo para llenar un tramo.")]
    [SerializeField, Min(1f)] private float damagePerSegment = 150f;
    [Tooltip("Tramos que gasta un dúo.")]
    [SerializeField, Min(0.1f)] private float duoCost = 1f;

    [Header("Entrada")]
    [Tooltip("Margen entre pulsar un gatillo y el otro para que cuente como LT+RT.")]
    [SerializeField, Range(0.02f, 0.3f)] private float chordWindow = 0.12f;

    [Header("Referencias")]
    [SerializeField] private MagicCaster magicCaster;

    [Header("Alcance")]
    [Tooltip("Si el objetivo está a esta distancia o menos, el golpe cae sobre él; si no, delante.")]
    [SerializeField] private float targetReach = 9f;
    [SerializeField] private float frontDistance = 2.5f;

    [Header("Feedback de no disponible")]
    [SerializeField] private string notAvailableSFXKey = "ui_denied";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    private bool _isExecuting;
    private bool _ltWasDown, _rtWasDown;
    private int _pendingSide;            // 0 nada, -1 LT, +1 RT
    private float _pendingSince;

    private readonly Collider[] _aoeHitsBuffer = new Collider[32];
    private readonly List<Transform> _partners = new List<Transform>(2);
    private readonly List<(Slot slot, Transform t, float distance)> _nearby = new List<(Slot, Transform, float)>(2);
    private int _enemyBossLayerMask;

    /// <summary>Carga de equipo (0..3).</summary>
    public SpecialChargeMeter TeamGauge => teamGauge;

    /// <summary>Hay al menos un compañero en el grupo (sin compañeros no hay dúos ni trío).</summary>
    public bool HasAnyCompanion
    {
        get
        {
            var party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
            return party != null && party.Members != null && party.Members.Count > 0;
        }
    }

    /// <summary>Se lanzó un ataque de equipo (el ataque).</summary>
    public event Action<SpecialAttackSO> OnTeamAttack;
    /// <summary>LT+RT no pudo lanzar nada: mensaje ya traducido para el jugador.</summary>
    public event Action<string> OnDenied;

    // ── Ciclo de vida ──────────────────────────────────────────────────────

    void Awake()
    {
        _enemyBossLayerMask = LayerMask.GetMask("Enemy", "Boss");
    }

    void OnEnable()
    {
        AvisosDeCombate.AlPedirseRemate += AlPedirseRemate;
        Damageable.AlRecibirDanoCualquiera += AlHacerDanoAlguien;
    }

    void OnDisable()
    {
        AvisosDeCombate.AlPedirseRemate -= AlPedirseRemate;
        Damageable.AlRecibirDanoCualquiera -= AlHacerDanoAlguien;
        _pendingSide = 0;
        _isExecuting = false;
    }

    // ── Carga ──────────────────────────────────────────────────────────────

    /// <summary>Añade tramos a la carga de equipo (orbes, objetos, laboratorio).</summary>
    public void AddTeamCharge(float segments)
    {
        if (teamGauge != null) teamGauge.AddCharge(segments);
    }

    /// Un jefe ya solo cae con un golpe especial (RemateObligatorio): se llena la carga para que el
    /// remate esté siempre al alcance. Ver INC-489.
    private void AlPedirseRemate(GameObject _)
    {
        if (teamGauge != null) teamGauge.AddCharge(teamGauge.MaxCharge);
    }

    private void AlHacerDanoAlguien(Damageable victima, float cantidad, GameObject autor)
    {
        if (_isExecuting || teamGauge == null || autor == null || victima == null) return;
        if (!EsDelGrupo(autor) || EsDelGrupo(victima.gameObject)) return;
        teamGauge.AddCharge(cantidad / damagePerSegment);
    }

    private static bool EsDelGrupo(GameObject go)
    {
        var cuerpo = PlayerService.Player;
        if (cuerpo != null && (go == cuerpo || go.transform.IsChildOf(cuerpo.transform))) return true;
        return go.GetComponentInParent<NPCPartyMember>() != null;
    }

    // ── Entrada ────────────────────────────────────────────────────────────

    void Update()
    {
        // Del dispositivo, como el resto de botones de combate (GamepadInputReader): las acciones
        // LT/RT del asset no llegaban en el laboratorio de combate.
        bool lt = GamepadInputReader.LeftTriggerHeld;
        bool rt = GamepadInputReader.RightTriggerHeld;
        bool ltPressed = lt && !_ltWasDown;
        bool rtPressed = rt && !_rtWasDown;
        _ltWasDown = lt;
        _rtWasDown = rt;

        if (!GameState.CanProcessGameplayInput || ComboCastController.IsComposing)
        {
            _pendingSide = 0;
            return;
        }

        // LT+RT a la vez (con margen de chordWindow entre uno y otro). En teclado los dos van en la
        // misma tecla (Ctrl), así que se pulsan juntos siempre.
        if ((ltPressed && rt) || (rtPressed && lt))
        {
            _pendingSide = 0;
#if UNITY_EDITOR
            Debug.Log("[EquipoEspecial] LT+RT pulsados.", this);
#endif
            TryTeamAttack();
            return;
        }
        if (_pendingSide == 0 && (ltPressed || rtPressed))
        {
            _pendingSide = ltPressed ? -1 : 1;
            _pendingSince = Time.unscaledTime;
        }
        if (_pendingSide != 0 && Time.unscaledTime - _pendingSince >= chordWindow)
            _pendingSide = 0; // un gatillo solo no hace nada
    }

    // ── Reglas ─────────────────────────────────────────────────────────────

    private static Slot ActiveSlot =>
        PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;

    /// <summary>Ataque del dúo entre estos dos personajes, o null si no hay.</summary>
    public SpecialAttackSO FindDuo(Slot a, Slot b)
    {
        for (int i = 0; i < duoAttacks.Count; i++)
            if (duoAttacks[i] != null && duoAttacks[i].IsDuoOf(a, b)) return duoAttacks[i];
        return null;
    }

    /// <summary>
    /// LT+RT: ataque de equipo con quien esté cerca (Raúl: «acercándome al personaje; si quiero
    /// solo a uno uso el sígueme»). Los dos compañeros cerca y la carga llena = trío; uno cerca (o
    /// los dos sin la carga llena) = dúo con el más cercano, un tramo.
    /// </summary>
    private void TryTeamAttack()
    {
        Slot active = ActiveSlot;
        float maxDistance = 0f;
        for (int i = 0; i < duoAttacks.Count; i++)
            if (duoAttacks[i] != null) maxDistance = Mathf.Max(maxDistance, duoAttacks[i].companionMaxDistance);
        if (trioAttack != null) maxDistance = Mathf.Max(maxDistance, trioAttack.companionMaxDistance);

        _nearby.Clear();
        for (int s = 0; s < 3; s++)
        {
            var slot = (Slot)s;
            if (slot == active) continue;
            if (TryGetPartnerDistance(slot, active, out Transform t, out float d) && d <= maxDistance)
                _nearby.Add((slot, t, d));
        }
        if (_nearby.Count == 0) { Deny("No hay ningún compañero cerca", "TEAM_DENIED_NOBODY", "No hay ningún compañero cerca."); return; }
        _nearby.Sort((a, b) => a.distance.CompareTo(b.distance));

        if (_nearby.Count >= 2 && trioAttack != null && teamGauge != null && teamGauge.IsFullyCharged
            && _nearby[1].distance <= trioAttack.companionMaxDistance)
        {
            _partners.Clear();
            _partners.Add(_nearby[0].t);
            _partners.Add(_nearby[1].t);
            Execute(trioAttack, teamGauge.MaxCharge);
            return;
        }

        var partner = _nearby[0];
        var attack = FindDuo(active, partner.slot);
        if (attack == null) { Deny($"No hay dúo {active}+{partner.slot}", "TEAM_DENIED_BUSY", "Ahora no se puede."); return; }
        if (partner.distance > attack.companionMaxDistance) { Deny($"{partner.slot} está lejos", "TEAM_DENIED_NOBODY", "No hay ningún compañero cerca."); return; }
        if (teamGauge == null || teamGauge.CurrentCharge < duoCost - 0.001f) { Deny($"Carga insuficiente ({teamGauge?.CurrentCharge:0.##}/{duoCost})", "TEAM_DENIED_CHARGE", "Falta carga de equipo."); return; }

        _partners.Clear();
        _partners.Add(partner.t);
        Execute(attack, duoCost);
    }

    private bool TryGetPartnerDistance(Slot slot, Slot active, out Transform t, out float distance)
    {
        ResolveCaster();
        distance = float.MaxValue;
        Transform body = magicCaster != null ? magicCaster.Body : null;
        t = GetMemberTransform(slot, active);
        if (t == null || body == null) return false;
        distance = Vector3.Distance(t.position, body.position);
        return true;
    }

    private Transform GetMemberTransform(Slot slot, Slot active)
    {
        if (slot == active) return magicCaster != null ? magicCaster.Body : null;

        if (slot == Slot.Will)
        {
            var will = ActiveCharacterSwapper.Instance != null ? ActiveCharacterSwapper.Instance.WillNpcInstance : null;
            return will != null && will.isActiveAndEnabled ? will.transform : null;
        }

        var party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
        var member = party != null ? party.GetMemberByName(slot == Slot.Liam ? "Liam" : "Estela") : null;
        return member != null && member.isActiveAndEnabled ? member.transform : null;
    }

    private void ResolveCaster()
    {
        if (!magicCaster) PlayerService.TryGetComponent(out magicCaster, allowSceneLookup: false);
    }

    // ── Ejecución ──────────────────────────────────────────────────────────

    private void Execute(SpecialAttackSO attack, float cost)
    {
        if (_isExecuting) return;
        ResolveCaster();
        if (magicCaster == null) { Deny("Sin MagicCaster", "TEAM_DENIED_BUSY", "Ahora no se puede."); return; }
        if (!magicCaster.BeginTeamGesture(attack.gestureSeconds, attack.isTrio ? MagicCastStyle.Area : MagicCastStyle.TwoHanded, out Vector3 facing)) { Deny("El personaje no puede lanzar ahora", "TEAM_DENIED_BUSY", "Ahora no se puede."); return; }
        if (!teamGauge.TryConsume(cost)) { Deny("Carga insuficiente", "TEAM_DENIED_CHARGE", "Falta carga de equipo."); return; }

        StartCoroutine(Co_Execute(attack, facing));
        OnTeamAttack?.Invoke(attack);
        Log($"{attack.displayName} (gasta {cost:0.#} tramo/s)");
    }

    private IEnumerator Co_Execute(SpecialAttackSO attack, Vector3 facing)
    {
        _isExecuting = true;
        Transform body = magicCaster.Body;

        for (int i = 0; i < _partners.Count; i++)
        {
            var p = _partners[i];
            if (p == null) continue;
            Vector3 look = body.position - p.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) p.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : look);
            TriggerAnimation(p.gameObject, attack.companionAnimationTrigger);
        }

        if (!string.IsNullOrEmpty(attack.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(attack.castSFXKey);

        Vector3 center = body.position + facing * frontDistance;
        if (magicCaster.TryGetTarget(out Transform target))
        {
            Vector3 flat = target.position - body.position; flat.y = 0f;
            if (flat.sqrMagnitude <= targetReach * targetReach) center = target.position;
        }

        if (attack.vfxPrefab != null && VfxPoolService.Instance != null)
        {
            VfxPoolService.Instance.Play(
                attack.vfxPrefab,
                center,
                Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : body.forward),
                Mathf.Max(0.5f, attack.vfxLifetime));
        }

        if (attack.damageDelay > 0f)
            yield return new WaitForSeconds(attack.damageDelay);

        ApplyAoeDamage(attack, center);
        _isExecuting = false;
    }

    private void ApplyAoeDamage(SpecialAttackSO attack, Vector3 center)
    {
        if (attack.damage <= 0f && attack.knockbackForce <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(center, attack.aoeRadius, _aoeHitsBuffer, _enemyBossLayerMask);
        // Un ataque de equipo es un golpe de remate: lo que solo cae así (RemateObligatorio) cae con esto.
        using var remate = GolpeDeRemate.Abrir();
        for (int i = 0; i < count; i++)
        {
            var hit = _aoeHitsBuffer[i];
            if (hit.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(attack.damage);

            if (attack.knockbackForce > 0f && hit.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = (hit.transform.position - center).normalized;
                rb.AddForce(dir * attack.knockbackForce, ForceMode.Impulse);
            }
        }
        Log($"AoE de {attack.displayName}: {count} objetivos en radio {attack.aoeRadius} m");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void TriggerAnimation(GameObject go, string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;
        var anim = go.GetComponentInChildren<Animator>();
        if (anim == null) return;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger) { anim.SetTrigger(trigger); return; }
    }

    private void Deny(string reason, string messageKey, string fallback)
    {
#if UNITY_EDITOR
        Debug.Log("[EquipoEspecial] LT+RT sin efecto: " + reason, this);
#endif
        string msg = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(messageKey, fallback) : fallback;
        OnDenied?.Invoke(msg);
        if (!string.IsNullOrEmpty(notAvailableSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(notAvailableSFXKey);
    }

    private void Log(string message)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (showDebugLogs) Debug.Log($"[EquipoEspecial] {message}", this);
#endif
    }
}
