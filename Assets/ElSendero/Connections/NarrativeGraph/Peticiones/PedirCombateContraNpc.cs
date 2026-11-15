using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.Modules;
using Sendero.Core.Feedback;

/// Pone a uno o varios NPCs en combate contra el jugador (con su FSM de combate) y, si se pide,
/// espera a que caigan todos. Sale por «Gana» al vencerlos. Si «Pierde» está conectada, caer en el
/// combate no lleva a la pantalla de derrota: el grupo se levanta curado tras un fundido a negro,
/// los NPCs vuelven a estar en paz y la historia sigue por «Pierde» (p. ej. para repetir el tramo).
[Serializable]
[NarrativeNodeInfo("Combate", "Iniciar combate NPC", "Pone a uno o varios NPCs en combate contra el jugador y opcionalmente espera a que caigan todos. Con «Pierde» conectada, perder sigue por ahí en vez de ir a la pantalla de derrota.")]
public sealed class PedirCombateContraNpc : NarrativeRequest, INarrativeStateEffect
{
    static readonly string[] Salidas = { "Gana", "Pierde" };

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2 / NarrativeActor).")]
    public string npcId;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Otros NPCs que pelean a la vez (persistenceId). Se gana cuando han caído todos.")]
    public List<string> otrosNpcs = new();

    [Tooltip("Configuración de combate a usar. Vacía = la que ya tenga cada NPC.")]
    public NPCCombatConfig combatConfig;

    [Tooltip("Clave del evento que se emite cuando cae cada NPC del combate.")]
    public string defeatEventKey = "NPC_DEFEATED";

    [Tooltip("Espera a que caigan todos los NPCs del combate para seguir.")]
    public bool waitForDefeat = true;

    [Tooltip("Tras la derrota, los NPCs vuelven a la capa Interactable y a estar quietos.")]
    public bool restorePeacefulOnDefeat = true;

    public override string[] Exits => Salidas;

    public override string DescribeDecision(NarrativeNode step)
        => $"Combate \"{step?.displayTitle}\" (guid {step?.guid}) contra '{npcId}'{(otrosNpcs.Count > 0 ? " y " + string.Join(", ", otrosNpcs) : "")}: ¿gana el jugador? Salidas: Gana / Pierde.";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "pelea");
        foreach (var id in otrosNpcs) if (!string.IsNullOrWhiteSpace(id)) d.Actor(id, "pelea");
        if (combatConfig != null) d.Asset(combatConfig, "combate");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC del combate.");
        foreach (var id in otrosNpcs)
            if (string.IsNullOrWhiteSpace(id) || id == npcId) warnings.Add("Hay un NPC de más vacío o repetido.");
    }

    public string ClaveDeDerrota => !string.IsNullOrEmpty(defeatEventKey) ? defeatEventKey : $"NPC_DEFEATED_{npcId}";

    public void Project(INarrativeStateWriter state)
    {
        if (waitForDefeat && !string.IsNullOrWhiteSpace(npcId)) state.MarkSignalRaised(ClaveDeDerrota);
    }

    public IEnumerable<string> Ids()
    {
        yield return npcId;
        foreach (var id in otrosNpcs)
            if (!string.IsNullOrWhiteSpace(id) && id != npcId) yield return id;
    }
}

public sealed class CombateContraNpcEnCurso : NarrativeRequestRun<PedirCombateContraNpc>, IRespuestaALaDerrota
{
    const int SalidaGana = 0;
    const int SalidaPierde = 1;
    const float FundidoAlPerder = 0.6f;

    readonly List<NPCBehaviourManagerV2> _npcs = new();
    INarrativeSignals _signals;
    Action _alCaer;
    int _caidos;
    bool _respondeALaDerrota;

    protected override void Start()
    {
        if (string.IsNullOrWhiteSpace(R.npcId))
        {
            Debug.LogWarning($"[Combate:{StepGuid}] npcId vacío → sigue para no bloquear.");
            Finish(SalidaGana);
            return;
        }

        foreach (var id in R.Ids())
        {
            var m = BuscarNpc(id);
            if (m != null) { if (!_npcs.Contains(m)) _npcs.Add(m); }
            else Debug.LogWarning($"[Combate:{StepGuid}] No hay NPCBehaviourManagerV2 con id='{id}'.");
        }
        if (_npcs.Count == 0)
        {
            Debug.LogWarning($"[Combate:{StepGuid}] Ningún NPC del combate está en la escena → sigue.");
            Finish(SalidaGana);
            return;
        }

        string clave = R.ClaveDeDerrota;
        _caidos = 0;
        foreach (var m in _npcs) PrepararParaPelear(m, clave);

        // Cada NPC que cae emite la misma clave.
        if (R.waitForDefeat)
        {
            _signals = Ctx?.Signals ?? DefaultNarrativeSignals.Instance;
            if (_signals == null)
            {
                Debug.LogWarning($"[Combate:{StepGuid}] Sin proveedor de señales para esperar la derrota → sigue.");
                Finish(SalidaGana);
                return;
            }
            _alCaer = () =>
            {
                _caidos++;
                if (_caidos < _npcs.Count) return;
                if (R.restorePeacefulOnDefeat) DejarEnPaz();
                Finish(SalidaGana);
            };
            _signals.OnCustom(clave, _alCaer);
            if (IsExitConnected(SalidaPierde))
            {
                GameOverManager.RegistrarRespuestaALaDerrota(this);
                _respondeALaDerrota = true;
            }
        }

        if (PlayerService.TryGetPlayer(out GameObject jugador) && jugador != null)
            foreach (var m in _npcs) m.ForceEnterCombat(jugador.transform);
        else
            Debug.LogWarning($"[Combate:{StepGuid}] No se encontró al jugador para empezar el combate con '{R.npcId}'.");

        if (!R.waitForDefeat) Finish(SalidaGana);
    }

    protected override void Stop()
    {
        if (_respondeALaDerrota)
        {
            GameOverManager.QuitarRespuestaALaDerrota(this);
            _respondeALaDerrota = false;
        }
        if (_signals != null && _alCaer != null) _signals.OffCustom(R.ClaveDeDerrota, _alCaer);
        _alCaer = null;
        _signals = null;
    }

    /// El grupo ha caído en este combate y «Pierde» está conectada.
    public bool Responder()
    {
        if (Runner == null) return false;
        Runner.StartCoroutine(Co_AlPerder(Runner));
        return true;
    }

    IEnumerator Co_AlPerder(NarrativeRunner runner)
    {
        Stop();
        yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: true);

        GameOverManager.Instance?.ForceResetState();
        if (PlayerService.TryGetComponent(out PlayerActionManager modos, allowSceneLookup: false)) modos.ResetToDefault();
        SaludDelGrupo.Instance?.CurarATodos();

        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        foreach (var m in _npcs)
        {
            if (m == null) continue;
            var dmg = m.GetComponent<Damageable>();
            var config = m.Configuration?.combatConfig;
            float hp = config != null ? config.health : 100f;
            if (dmg != null) dmg.SetMaxAndCurrent(hp, hp);
            var ciclo = m.GetComponent<NPCCombatLifecycleHandler>();
            if (ciclo != null && ciclo.IsDefeatedAndInactive) ciclo.Resurrect();
            if (capaInteractuable >= 0) m.gameObject.layer = capaInteractuable;
            m.ForceIdle();
        }

        Finish(SalidaPierde);
        yield return null;
        // Si el paso siguiente ya ha destapado o vuelto a tapar la pantalla, manda el suyo.
        if (FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: false);
    }

    // Configuración de combate, vida, ciclo de caída y capa Enemy de un NPC del combate.
    void PrepararParaPelear(NPCBehaviourManagerV2 npc, string clave)
    {
        if (R.combatConfig != null) npc.Configuration.combatConfig = R.combatConfig;

        var config = npc.Configuration?.combatConfig;
        if (config != null)
        {
            config.sendEventOnDefeat = true;
            config.defeatEventKey = clave;
            config.sendDefeatEventBeforeDeath = false;
            config.postDeathBehavior = PostDeathBehavior.GetUpDizzy;
        }

        var dmg = npc.GetComponent<Damageable>();
        float hp = config != null ? config.health : 100f;
        if (dmg == null) dmg = npc.gameObject.AddComponent<Damageable>();
        dmg.SetMaxAndCurrent(hp, hp);
        dmg.SetDestroyOnDeath(false);

        // En una revancha, el NPC ya derrotado se levanta para poder volver a pelear: ForceEnterCombat
        // ignora a un NPC derrotado.
        var ciclo = npc.GetComponent<NPCCombatLifecycleHandler>();
        if (!ciclo) npc.gameObject.AddComponent<NPCCombatLifecycleHandler>();
        else if (ciclo.IsDefeatedAndInactive) ciclo.Resurrect();

        int capaEnemigo = LayerMask.NameToLayer("Enemy");
        if (capaEnemigo >= 0) npc.gameObject.layer = capaEnemigo;
    }

    void DejarEnPaz()
    {
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        foreach (var m in _npcs)
        {
            if (m == null) continue;
            if (capaInteractuable >= 0) m.gameObject.layer = capaInteractuable;
            m.ForceIdle();
        }
    }

    static NPCBehaviourManagerV2 BuscarNpc(string id)
    {
        if (NPCRegistry.HasInstance)
        {
            var m = NPCRegistry.Instance.GetNPCByID(id);
            if (m != null) return m;
        }
        if (SequenceActor.TryResolve(id, out var actor) && actor?.Transform != null)
        {
            var m = actor.Transform.GetComponent<NPCBehaviourManagerV2>();
            if (m != null) return m;
        }
        foreach (var m in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include))
            if (m.PersistenceId == id) return m;
        return null;
    }
}
