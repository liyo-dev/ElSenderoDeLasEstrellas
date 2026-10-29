using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
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
public partial class DuoSpecialAttackSystem : MonoBehaviour
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

    [Header("VFX de Coreografía y Acción")]
    [Tooltip("Aura/disco de fuego a los pies de Estela mientras levita.")]
    [SerializeField] private GameObject auraLevitacionEstela;
    [Tooltip("Foco/orbe de fuego en la mano alzada de Estela convocando meteoros.")]
    [SerializeField] private GameObject focoManoEstela;
    [Tooltip("Aura de concentración mágica en Will/compañero activo.")]
    [SerializeField] private GameObject auraConcentracionWill;
    [Tooltip("Aura de concentración psíquica/cósmica en Liam.")]
    [SerializeField] private GameObject auraConcentracionLiam;
    [Tooltip("Círculo rúnico y pilares celestiales a los pies de la formación del Trío.")]
    [SerializeField] private GameObject auraFormacionTrio;
    [Tooltip("Impacto sísmico / detonación terrestre al concluir el golpe de Estela.")]
    [SerializeField] private GameObject impactoRemateEstela;
    [Tooltip("Supernova cósmica / detonación gravitacional al concluir el golpe de Liam.")]
    [SerializeField] private GameObject impactoRemateLiam;
    [Tooltip("Cataclismo celestial / detonación suprema de las tres estrellas.")]
    [SerializeField] private GameObject impactoRemateTrio;

    [Header("Alcance")]
    [Tooltip("Si el objetivo está a esta distancia o menos, el golpe cae sobre él; si no, delante.")]
    [SerializeField] private float targetReach = 9f;
    [SerializeField] private float frontDistance = 2.5f;

    [Header("Poses (estados del Animator)")]
    [Tooltip("Brazos arriba: pose para saltos.")]
    [SerializeField] private string poseBrazosArriba = "Cheer01";
    [Tooltip("Pose de conjuración / sostén mágico con ambas manos hacia el agujero negro (Liam y Will).")]
    [SerializeField] private string poseSostenerMagia = "CastingIdle01";
    [Tooltip("Dúo con Estela en el aire: mano alzada/adelante de conjuración mientras caen los meteoritos.")]
    [SerializeField] private string poseInvocacionDuoEstela = "MagicRight";
    [Tooltip("Pose de la Y del combo (cara a cara en el dúo con Liam). Capa superior.")]
    [SerializeField] private string poseDeLaY = "ComboIdle";
    [Tooltip("Pose de cuerpo entero en el aire (capa base).")]
    [SerializeField] private string poseEnElAire = "JumpAir_InPlace_NoWeapon";
    [Tooltip("Pose de carrera (capa base).")]
    [SerializeField] private string poseCarrera = "Run";
    [Tooltip("Dúo con Estela al tocar el suelo: pose firme espalda con espalda.")]
    [SerializeField] private string poseAterrizajeDuoEstela = "Idle_Battle_NoWeapon";
    [Tooltip("Trío: «aquí hemos llegado», mientras salen las caras en las franjas.")]
    [SerializeField] private string poseLlegadaTrio = "Challenging_NoWeapon";
    [Tooltip("Trío, al aterrizar: pose de Will.")]
    [SerializeField] private string poseAterrizajeWill = "Idle_Battle_NoWeapon";
    [Tooltip("Trío, al aterrizar: pose de Estela.")]
    [SerializeField] private string poseAterrizajeEstela = "MagicSpecial";
    [Tooltip("Trío, al aterrizar: pose de Liam.")]
    [SerializeField] private string poseAterrizajeLiam = "HumanM@MagicAttackOmni01 - Cast";

    [Header("Coreografía (segundos reales salvo que se diga)")]
    [Tooltip("Velocidad de la carrera previa al salto (m/s).")]
    [SerializeField, Min(1f)] private float velocidadDeCarrera = 11f;
    [Tooltip("Dúo con Estela: altura del salto sobre el suelo (m).")]
    [SerializeField, Min(0.5f)] private float alturaSaltoDuoEstela = 2.4f;
    [Tooltip("Trío: altura del salto sobre el suelo (m).")]
    [SerializeField, Min(0.5f)] private float alturaSaltoTrio = 3.4f;
    [Tooltip("Dúos y trío: escala de tiempo mientras están en el aire (cámara lenta).")]
    [SerializeField, Range(0.1f, 1f)] private float escalaDeTiempoEnElAire = 0.45f;
    [Tooltip("Dúo con Estela: segundos suspendidos en lo alto, con el hechizo cayendo.")]
    [SerializeField, Min(0.2f)] private float segundosEnLoAltoDuo = 1.5f;
    [Tooltip("Dúo con Liam: segundos que el agujero negro atrae (tiempo de juego).")]
    [SerializeField, Min(0.5f)] private float segundosDelAgujero = 1.8f;
    [Tooltip("Dúo con Liam: radio horizontal bajo el agujero donde los enemigos chocan con él (m).")]
    [SerializeField, Min(0.5f)] private float radioDeChoqueDelAgujero = 2.0f;
    [Tooltip("Trío: segundos con las tres caras en las franjas y la pose de llegada.")]
    [SerializeField, Min(0.5f)] private float segundosDeLasCaras = 1.8f;

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
    public bool HasAnyCompanion =>
        ControlDelJugador.EnElGrupo(Slot.Estela) || ControlDelJugador.EnElGrupo(Slot.Liam);

    /// <summary>Por qué LT+RT no ha lanzado nada.</summary>
    private enum MotivoDeRechazo { Ocupado, NadieCerca, FaltaCarga }

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
        StopAllCoroutines();
        _pendingSide = 0;
        if (_isExecuting)
        {
            LimpiarPuestaEnEscena();
            _isExecuting = false;
        }
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
        if (!AmenazasAlJugador.EsDelGrupoDelJugador(autor) || AmenazasAlJugador.EsDelGrupoDelJugador(victima.gameObject)) return;
        teamGauge.AddCharge(cantidad / damagePerSegment);
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

    /// <summary>Ataque del dúo entre estos dos personajes, o null si no hay.</summary>
    public SpecialAttackSO FindDuo(Slot a, Slot b)
    {
        for (int i = 0; i < duoAttacks.Count; i++)
            if (duoAttacks[i] != null && duoAttacks[i].IsDuoOf(a, b)) return duoAttacks[i];
        return null;
    }

    /// <summary>La misma selección que se ejecuta con LT+RT, para anunciarla en el HUD.</summary>
    public bool TryGetAvailableAttack(out SpecialAttackSO attack)
        => TrySelectAttack(out attack, out _, out _, out _, out _);

    private void TryTeamAttack()
    {
        if (!TrySelectAttack(out var attack, out float cost, out var firstPartner, out var secondPartner, out var motivo))
        {
            Deny(motivo);
            return;
        }
        _partners.Clear();
        _partners.Add(firstPartner);
        if (secondPartner != null) _partners.Add(secondPartner);
        Execute(attack, cost);
    }

    private bool TrySelectAttack(out SpecialAttackSO attack, out float cost,
        out Transform firstPartner, out Transform secondPartner, out MotivoDeRechazo motivo)
    {
        attack = null;
        cost = duoCost;
        firstPartner = secondPartner = null;
        motivo = MotivoDeRechazo.Ocupado;
        if (!isActiveAndEnabled || _isExecuting || !GameState.CanProcessGameplayInput || ComboCastController.IsComposing) return false;
        ResolveCaster();
        if (magicCaster == null || !magicCaster.CanBeginTeamGesture) return false;
        Slot active = ControlDelJugador.Activo;
        _nearby.Clear();
        for (int s = 0; s < ControlDelJugador.Personajes; s++)
        {
            var slot = (Slot)s;
            if (slot == active) continue;
            var duo = FindDuo(active, slot);
            float maxDistance = Mathf.Max(duo != null ? duo.companionMaxDistance : 0f,
                trioAttack != null ? trioAttack.companionMaxDistance : 0f);
            if (TryGetPartnerDistance(slot, active, out Transform t, out float d) && d <= maxDistance)
                _nearby.Add((slot, t, d));
        }
        motivo = MotivoDeRechazo.NadieCerca;
        if (_nearby.Count == 0) return false;
        if (_nearby.Count == 2 && _nearby[1].distance < _nearby[0].distance)
            (_nearby[0], _nearby[1]) = (_nearby[1], _nearby[0]);
        if (_nearby.Count == 2 && trioAttack != null && teamGauge != null && teamGauge.IsFullyCharged
            && _nearby[1].distance <= trioAttack.companionMaxDistance)
        {
            attack = trioAttack;
            cost = teamGauge.MaxCharge;
            firstPartner = _nearby[0].t;
            secondPartner = _nearby[1].t;
            return true;
        }
        for (int i = 0; i < _nearby.Count; i++)
        {
            var partner = _nearby[i];
            var duo = FindDuo(active, partner.slot);
            if (duo == null || partner.distance > duo.companionMaxDistance) continue;
            motivo = MotivoDeRechazo.FaltaCarga;
            if (teamGauge == null || teamGauge.CurrentCharge < duoCost - 0.001f) return false;
            attack = duo;
            firstPartner = partner.t;
            return true;
        }
        return false;
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

        // Un compañero caído no se une a dúos ni al trío (INC-717).
        if (SaludDelGrupo.EstaCaido(slot)) return null;
        var member = ControlDelJugador.NpcDe(slot);
        return member != null && member.isActiveAndEnabled ? member.transform : null;
    }

    private void ResolveCaster()
    {
        // El cuerpo activo cambia al relevar el mando: no conserva el lanzador del anterior.
        PlayerService.TryGetComponent(out magicCaster, allowSceneLookup: false);
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
            if (_absorbidos.Count > 0 && hit.TryGetComponent<Damageable>(out var yaAbsorbido) && _absorbidos.Contains(yaAbsorbido))
                continue;
            if (hit.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(attack.damage);

            // Lo que cada ataque deja en el enemigo (ralentizar, empujar, abrir su protección...)
            // es dato del SpecialAttackSO.
            if (attack.abreProteccionSegundos > 0f)
                ProteccionQueSeAbre.De(hit)?.AbrirProteccion(attack.abreProteccionSegundos, gameObject, null);
            if (attack.statusEffect != EstadoDeCombate.Ninguno && hit.TryGetComponent<Damageable>(out var conEstado))
                EstadosDeCombate.Aplicar(conEstado, attack.statusEffect, attack.statusDuration, attack.statusStrength, center, null);

            if (attack.knockbackForce > 0f && hit.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = (hit.transform.position - center).normalized;
                rb.AddForce(dir * attack.knockbackForce, ForceMode.Impulse);
            }
        }
        Log($"AoE de {attack.displayName}: {count} objetivos en radio {attack.aoeRadius} m");
    }

    private static SequenceActor ResolveActor(Transform t, bool isPlayer)
    {
        if (isPlayer && SequenceActor.TryResolve(SequenceActor.PlayerId, out var playerActor))
            return playerActor;
        if (t != null)
        {
            var mgr = t.GetComponentInParent<NPCBehaviourManagerV2>();
            if (mgr != null && SequenceActor.TryResolve(mgr.PersistenceId, out var npcActor))
                return npcActor;
            return SequenceActor.ForTransform(t.name, t, 1.2f);
        }
        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Transform PlayTeamVfx(SpecialAttackSO attack, Vector3 center, Vector3 facing, Transform body)
    {
        if (attack.vfxPrefab == null || VfxPoolService.Instance == null) return null;
        return VfxPoolService.Instance.Play(
            attack.vfxPrefab,
            center + Vector3.up * attack.elevacionDelVfx,
            Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : body.forward),
            Mathf.Max(0.5f, attack.vfxLifetime));
    }

    private static void TriggerAnimation(GameObject go, string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;
        // El Animator de los compañeros usa estados en la capa de brazos, no triggers.
        var actor = go.GetComponent<NPCSimpleAnimator>();
        if (actor != null) actor.PlaySpellCastSpecial();
    }

    private void Deny(MotivoDeRechazo motivo)
    {
        string messageKey, fallback;
        switch (motivo)
        {
            case MotivoDeRechazo.FaltaCarga: messageKey = "TEAM_DENIED_CHARGE"; fallback = "Falta carga de equipo."; break;
            case MotivoDeRechazo.NadieCerca: messageKey = "TEAM_DENIED_NOBODY"; fallback = "No hay ningún compañero cerca."; break;
            default: messageKey = "TEAM_DENIED_BUSY"; fallback = "Ahora no se puede."; break;
        }
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
