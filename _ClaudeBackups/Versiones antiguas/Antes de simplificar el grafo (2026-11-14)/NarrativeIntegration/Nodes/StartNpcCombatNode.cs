using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.Modules;
using Sendero.Core.Feedback;

/// <summary>
/// Nodo que inicia un combate o duelo contra un NPC (usando su FSM de combate), o contra varios a la
/// vez con 'otrosNpcs', y opcionalmente espera a que sean derrotados todos para avanzar en el grafo.
/// Sale por «Gana» al vencerlos. Si la salida «Pierde» está conectada, caer en el combate no lleva
/// a la pantalla de derrota: el grupo se levanta curado tras un fundido a negro, los NPCs vuelven a
/// estar en paz y el grafo sigue por «Pierde» (p. ej. para repetir el tramo).
/// </summary>
[Serializable]
[NarrativeNodeInfo("Combate", "Iniciar combate NPC", "Pone a uno o varios NPCs en combate contra el jugador y opcionalmente espera a que caigan todos. Con «Pierde» conectada, perder sigue por ahí en vez de ir a la pantalla de derrota.")]
public sealed class StartNpcCombatNode : NarrativeNode, INarrativeStateEffect, IRespuestaALaDerrota
{
    private static readonly string[] Puertos = { "Gana", "Pierde" };
    private const int PuertoGana = 0;
    private const int PuertoPierde = 1;
    private const float FundidoAlPerder = 0.6f;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2 / NarrativeActor).")]
    public string npcId;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Otros NPCs que pelean a la vez (persistenceId). Se gana cuando han caído todos.")]
    public List<string> otrosNpcs = new();

    [Tooltip("Configuración de combate a usar. Si está vacía, se usará la que ya tenga asignada cada NPC.")]
    public NPCCombatConfig combatConfig;

    [Tooltip("Clave del evento emitido cuando cae cada NPC del combate.")]
    public string defeatEventKey = "NPC_DEFEATED";

    [Tooltip("Si es true, el nodo espera a que caigan todos los NPCs del combate para avanzar.")]
    public bool waitForDefeat = true;

    [Tooltip("Si es true, tras la derrota los NPCs vuelven a la capa Interactable y pasan a estado Idle.")]
    public bool restorePeacefulOnDefeat = true;

    [NonSerialized] private Action _onDefeatedCb;
    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private string _registeredKey;
    [NonSerialized] private List<NPCBehaviourManagerV2> _managers;
    [NonSerialized] private int _caidos;
    [NonSerialized] private NarrativeContext _ctx;
    [NonSerialized] private Action _onReady;
    [NonSerialized] private bool _respondeALaDerrota;

    public override string[] GetOutputPorts() => Puertos;

    /// La proyección supone que se gana.
    public override int ProjectionPort => PuertoGana;

    public override string DescribeDecision()
        => $"Combate \"{displayTitle}\" (guid {guid}) contra '{npcId}'{(otrosNpcs.Count > 0 ? " y " + string.Join(", ", otrosNpcs) : "")}: ¿gana el jugador? Salidas: Gana / Pierde.";

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC del combate.");
        foreach (var id in otrosNpcs)
            if (string.IsNullOrWhiteSpace(id) || id == npcId) warnings.Add("Hay un NPC de más vacío o repetido.");
    }

    // «Pierde» conectada: perder no va a la pantalla de derrota.
    private bool PerderSigueElGrafo => outputs != null && outputs.Count > PuertoPierde && !string.IsNullOrEmpty(outputs[PuertoPierde]);

    public void Project(INarrativeStateWriter state)
    {
        if (!waitForDefeat || string.IsNullOrWhiteSpace(npcId)) return;
        state.MarkSignalRaised(!string.IsNullOrEmpty(defeatEventKey) ? defeatEventKey : $"NPC_DEFEATED_{npcId}");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning($"[StartNpcCombatNode:{guid}] npcId vacío → avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        _managers = new List<NPCBehaviourManagerV2>();
        foreach (var id in Ids())
        {
            var m = ResolveManager(id);
            if (m != null) { if (!_managers.Contains(m)) _managers.Add(m); }
            else Debug.LogWarning($"[StartNpcCombatNode:{guid}] No se encontró NPCBehaviourManagerV2 con id='{id}'.");
        }
        if (_managers.Count == 0)
        {
            Debug.LogWarning($"[StartNpcCombatNode:{guid}] Ningún NPC del combate está en la escena → avanzando.");
            onReadyToAdvance?.Invoke();
            return;
        }

        string key = !string.IsNullOrEmpty(defeatEventKey) ? defeatEventKey : $"NPC_DEFEATED_{npcId}";
        _registeredKey = key;
        _caidos = 0;
        foreach (var m in _managers) PrepararParaPelear(m, key);

        // Suscribirse si se espera a la derrota: cada NPC que cae emite la misma clave.
        if (waitForDefeat)
        {
            _signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
            if (_signals != null)
            {
                _onDefeatedCb = () =>
                {
                    _caidos++;
                    if (_caidos < _managers.Count) return;
                    NarrativeLog.Info($"[StartNpcCombatNode:{guid}] Han caído los {_managers.Count} del combate. Avanzando.");
                    CleanupCombatState();
                    AdvanceThrough(ctx, onReadyToAdvance, PuertoGana);
                };
                _signals.OnCustom(_registeredKey, _onDefeatedCb);
                if (PerderSigueElGrafo)
                {
                    _ctx = ctx;
                    _onReady = onReadyToAdvance;
                    GameOverManager.RegistrarRespuestaALaDerrota(this);
                    _respondeALaDerrota = true;
                }
            }
            else
            {
                Debug.LogWarning($"[StartNpcCombatNode:{guid}] Sin proveedor de señales para esperar derrota → avanzando.");
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        // Encontrar objetivo y forzar entrada en combate
        Transform playerTarget = PlayerService.Instance != null
            ? PlayerService.Instance.transform
            : GameObject.FindGameObjectWithTag("Player")?.transform;

        if (playerTarget != null)
        {
            foreach (var m in _managers) m.ForceEnterCombat(playerTarget);
        }
        else
        {
            Debug.LogWarning($"[StartNpcCombatNode:{guid}] No se encontró al jugador para iniciar combate con '{npcId}'.");
        }

        if (!waitForDefeat)
        {
            AdvanceThrough(ctx, onReadyToAdvance, PuertoGana);
        }
    }

    private IEnumerable<string> Ids()
    {
        yield return npcId;
        foreach (var id in otrosNpcs)
            if (!string.IsNullOrWhiteSpace(id) && id != npcId) yield return id;
    }

    // Configuración de combate, vida, ciclo de caída y capa Enemy de un NPC del combate.
    private void PrepararParaPelear(NPCBehaviourManagerV2 manager, string key)
    {
        if (combatConfig != null)
            manager.Configuration.combatConfig = combatConfig;

        var config = manager.Configuration?.combatConfig;
        if (config != null)
        {
            config.sendEventOnDefeat = true;
            config.defeatEventKey = key;
            config.sendDefeatEventBeforeDeath = false;
            config.postDeathBehavior = PostDeathBehavior.GetUpDizzy;
        }

        var dmg = manager.GetComponent<Damageable>();
        float hp = config != null ? config.health : 100f;
        if (dmg == null) dmg = manager.gameObject.AddComponent<Damageable>();
        dmg.SetMaxAndCurrent(hp, hp);
        dmg.SetDestroyOnDeath(false);

        // Si el NPC ya fue derrotado antes (una revancha), se levanta para poder volver a pelear:
        // ForceEnterCombat ignora a un NPC derrotado.
        var lifecycle = manager.GetComponent<NPCCombatLifecycleHandler>();
        if (!lifecycle) manager.gameObject.AddComponent<NPCCombatLifecycleHandler>();
        else if (lifecycle.IsDefeatedAndInactive) lifecycle.Resurrect();

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) manager.gameObject.layer = enemyLayer;
    }

    public override void Exit(NarrativeContext ctx)
    {
        SafeUnsubscribe();
    }

    /// El grupo ha caído en este combate y «Pierde» está conectada.
    public bool Responder()
    {
        var runner = _ctx?.Runner;
        if (runner == null) return false;
        runner.StartCoroutine(Co_AlPerder(_ctx, _onReady));
        return true;
    }

    private IEnumerator Co_AlPerder(NarrativeContext ctx, Action onReady)
    {
        SafeUnsubscribe();
        yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: true);

        GameOverManager.Instance?.ForceResetState();
        if (PlayerService.TryGetComponent(out PlayerActionManager modos, allowSceneLookup: false))
            modos.ResetToDefault();
        SaludDelGrupo.Instance?.CurarATodos();

        if (_managers != null)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            foreach (var m in _managers)
            {
                if (m == null) continue;
                var dmg = m.GetComponent<Damageable>();
                var config = m.Configuration?.combatConfig;
                float hp = config != null ? config.health : 100f;
                if (dmg != null) dmg.SetMaxAndCurrent(hp, hp);
                var lifecycle = m.GetComponent<NPCCombatLifecycleHandler>();
                if (lifecycle != null && lifecycle.IsDefeatedAndInactive) lifecycle.Resurrect();
                if (interactableLayer >= 0) m.gameObject.layer = interactableLayer;
                m.ForceIdle();
            }
        }

        NarrativeLog.Info($"[StartNpcCombatNode:{guid}] El grupo ha caído contra '{npcId}'. Sigue por «Pierde».");
        AdvanceThrough(ctx, onReady, PuertoPierde);
        yield return null;
        // Si el siguiente paso ya ha destapado o vuelto a tapar la pantalla, manda el suyo.
        if (FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: false);
    }

    private void CleanupCombatState()
    {
        SafeUnsubscribe();

        if (!restorePeacefulOnDefeat || _managers == null) return;
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        foreach (var m in _managers)
        {
            if (m == null) continue;
            if (interactableLayer >= 0) m.gameObject.layer = interactableLayer;
            m.ForceIdle();
        }
    }

    private void SafeUnsubscribe()
    {
        if (_respondeALaDerrota)
        {
            GameOverManager.QuitarRespuestaALaDerrota(this);
            _respondeALaDerrota = false;
        }
        if (_signals != null && _onDefeatedCb != null && !string.IsNullOrEmpty(_registeredKey))
        {
            _signals.OffCustom(_registeredKey, _onDefeatedCb);
            _onDefeatedCb = null;
        }
    }

    private static NPCBehaviourManagerV2 ResolveManager(string id)
    {
        if (NPCRegistry.HasInstance)
        {
            var m = NPCRegistry.Instance.GetNPCByID(id);
            if (m != null) return m;
        }

        if (SequenceActor.TryResolve(id, out var actor) && actor != null && actor.Transform != null)
        {
            var m = actor.Transform.GetComponent<NPCBehaviourManagerV2>();
            if (m != null) return m;
        }

        foreach (var m in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include))
        {
            if (m.PersistenceId == id) return m;
        }

        return null;
    }
}
