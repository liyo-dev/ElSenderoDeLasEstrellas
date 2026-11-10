using System;
using System.Collections;
using UnityEngine;
using Game.NPC;
using Game.NPC.Modules;
using Sendero.Core.Feedback;

/// <summary>
/// Nodo que inicia un combate o duelo contra un NPC (usando su FSM de combate)
/// y opcionalmente espera a que sea derrotado para avanzar en el grafo.
/// Sale por «Gana» al vencerle. Si la salida «Pierde» está conectada, caer en el combate no lleva
/// a la pantalla de derrota: el grupo se levanta curado tras un fundido a negro, el NPC vuelve a
/// estar en paz y el grafo sigue por «Pierde» (p. ej. para repetir el tramo).
/// </summary>
[Serializable]
[NarrativeNodeInfo("Combate", "Iniciar combate NPC", "Pone a un NPC en combate contra el jugador y opcionalmente espera a que sea derrotado. Con «Pierde» conectada, perder sigue por ahí en vez de ir a la pantalla de derrota.")]
public sealed class StartNpcCombatNode : NarrativeNode, INarrativeStateEffect, IRespuestaALaDerrota
{
    private static readonly string[] Puertos = { "Gana", "Pierde" };
    private const int PuertoGana = 0;
    private const int PuertoPierde = 1;
    private const float FundidoAlPerder = 0.6f;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2 / NarrativeActor).")]
    public string npcId;

    [Tooltip("Configuración de combate a usar. Si está vacía, se usará la que ya tenga asignada el NPC.")]
    public NPCCombatConfig combatConfig;

    [Tooltip("Clave del evento emitido cuando el NPC es derrotado.")]
    public string defeatEventKey = "NPC_DEFEATED";

    [Tooltip("Si es true, el nodo espera a que el NPC sea derrotado para avanzar.")]
    public bool waitForDefeat = true;

    [Tooltip("Si es true, tras la derrota el NPC vuelve a la capa Interactable y pasa a estado Idle.")]
    public bool restorePeacefulOnDefeat = true;

    [NonSerialized] private Action _onDefeatedCb;
    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private string _registeredKey;
    [NonSerialized] private NPCBehaviourManagerV2 _cachedManager;
    [NonSerialized] private NarrativeContext _ctx;
    [NonSerialized] private Action _onReady;
    [NonSerialized] private bool _respondeALaDerrota;

    public override string[] GetOutputPorts() => Puertos;

    /// La proyección supone que se gana.
    public override int ProjectionPort => PuertoGana;

    public override string DescribeDecision()
        => $"Combate \"{displayTitle}\" (guid {guid}) contra '{npcId}': ¿gana el jugador? Salidas: Gana / Pierde.";

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

        var manager = ResolveManager(npcId);
        if (manager == null)
        {
            Debug.LogWarning($"[StartNpcCombatNode:{guid}] No se encontró NPCBehaviourManagerV2 con id='{npcId}' → avanzando.");
            onReadyToAdvance?.Invoke();
            return;
        }

        _cachedManager = manager;

        if (combatConfig != null)
        {
            manager.Configuration.combatConfig = combatConfig;
        }

        var config = manager.Configuration?.combatConfig;
        string key = !string.IsNullOrEmpty(defeatEventKey) ? defeatEventKey : $"NPC_DEFEATED_{npcId}";
        _registeredKey = key;

        if (config != null)
        {
            config.sendEventOnDefeat = true;
            config.defeatEventKey = key;
            config.sendDefeatEventBeforeDeath = false;
            config.postDeathBehavior = PostDeathBehavior.GetUpDizzy;
        }

        // Asegurar Damageable
        var dmg = manager.GetComponent<Damageable>();
        float hp = config != null ? config.health : 100f;
        if (dmg == null)
        {
            dmg = manager.gameObject.AddComponent<Damageable>();
            dmg.SetMaxAndCurrent(hp, hp);
        }
        else
        {
            dmg.SetMaxAndCurrent(hp, hp);
        }
        dmg.SetDestroyOnDeath(false);

        // Asegurar LifecycleHandler. Si el NPC ya fue derrotado antes (una revancha), se levanta
        // para poder volver a pelear: ForceEnterCombat ignora a un NPC derrotado.
        var lifecycle = manager.GetComponent<NPCCombatLifecycleHandler>();
        if (!lifecycle)
        {
            manager.gameObject.AddComponent<NPCCombatLifecycleHandler>();
        }
        else if (lifecycle.IsDefeatedAndInactive)
        {
            lifecycle.Resurrect();
        }

        // Suscribirse si se espera a la derrota
        if (waitForDefeat)
        {
            _signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
            if (_signals != null)
            {
                _onDefeatedCb = () =>
                {
                    NarrativeLog.Info($"[StartNpcCombatNode:{guid}] NPC '{npcId}' derrotado. Avanzando.");
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

        // Cambiar capa a Enemy
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            manager.gameObject.layer = enemyLayer;
        }

        // Encontrar objetivo y forzar entrada en combate
        Transform playerTarget = PlayerService.Instance != null 
            ? PlayerService.Instance.transform 
            : GameObject.FindGameObjectWithTag("Player")?.transform;

        if (playerTarget != null)
        {
            manager.ForceEnterCombat(playerTarget);
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

        if (_cachedManager != null)
        {
            var dmg = _cachedManager.GetComponent<Damageable>();
            var config = _cachedManager.Configuration?.combatConfig;
            float hp = config != null ? config.health : 100f;
            if (dmg != null) dmg.SetMaxAndCurrent(hp, hp);
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0) _cachedManager.gameObject.layer = interactableLayer;
            _cachedManager.ForceIdle();
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

        if (restorePeacefulOnDefeat && _cachedManager != null)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                _cachedManager.gameObject.layer = interactableLayer;
            }
            _cachedManager.ForceIdle();
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
