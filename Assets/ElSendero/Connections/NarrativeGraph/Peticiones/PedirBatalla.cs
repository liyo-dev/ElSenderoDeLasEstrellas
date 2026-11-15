using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Arranca una batalla de arena (BossArenaController) y espera a ganarla. Busca la arena por su
/// id en las escenas cargadas; si no hay ninguna y hay encuentro, crea la arena alrededor del
/// jugador con los datos del encuentro.
[Serializable]
[NarrativeNodeInfo("Combate", "Iniciar batalla", "Arranca una arena de batalla y espera la victoria.")]
public sealed class PedirBatalla : NarrativeRequest, INarrativeStateEffect
{
    [Tooltip("Id de la batalla (BossArenaController.BattleId).")]
    public string battleId;

    [Header("Encuentro (opcional)")]
    [Tooltip("Si se asigna, sustituye los datos de spawn de la arena (prefab, nombre, radio, VFX) por los de este encuentro y usa el radio alrededor del jugador. Sin arena en la escena, la crea con él.")]
    public BattleEncounterSO encounter;

    [Tooltip("Índice del perfil de spawn del encuentro (BattleEncounterSO.spawnProfiles).")]
    public int spawnProfileIndex = 0;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line(string.IsNullOrEmpty(battleId) ? "(sin battleId)" : battleId, NarrativeTextTone.Strong);
        if (encounter != null) d.Asset(encounter, "encuentro");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(battleId)) warnings.Add("Falta el id de la batalla");
    }

    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(battleId)) state.SetFact(HechosDelSendero.BatallaGanada, battleId, HechosDelSendero.Si);
    }
}

public sealed class BatallaEnCurso : NarrativeRequestRun<PedirBatalla>
{
    INarrativeSignals _signals;
    Action _alGanar;

    protected override void Start()
    {
        _signals = Ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        if (_signals == null || string.IsNullOrWhiteSpace(R.battleId))
        {
            Debug.LogWarning($"[Batalla:{StepGuid}] Sin señales o sin battleId: sigue para no bloquear.");
            Finish(0);
            return;
        }

        _alGanar = () => Finish(0);
        _signals.OnBattleWon(R.battleId, _alGanar);

        var arena = BuscarArena(R.battleId);
        if (arena == null && R.encounter != null) arena = BossArenaController.CreateRuntimeArena(R.battleId, R.encounter);
        if (arena == null)
        {
            Debug.LogWarning($"[Batalla:{StepGuid}] No hay arena '{R.battleId}' en la escena ni encuentro para crearla: sigue sin batalla.");
            Finish(0);
            return;
        }

        // Al cargar una partida, la batalla puede estar ya ganada.
        if (!string.IsNullOrEmpty(arena.BattleId) && BossProgressTracker.TryGetInstance(out var tracker) && tracker.IsDefeated(arena.BattleId))
        {
            Finish(0);
            return;
        }

        // Red de seguridad: si el arranque falla por tiempos, entrar en la arena la arranca igualmente.
        arena.SetStartBarrierOnPlayerEnter(true);
        try
        {
            if (R.encounter != null) arena.TriggerStartBattle(R.encounter, R.spawnProfileIndex);
            else arena.TriggerStartBattle();
            DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{R.battleId}", $"[Batalla] {R.battleId}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Batalla:{StepGuid}] Error al arrancar la arena '{R.battleId}': {ex.Message}. Sigue.");
            Finish(0);
        }
    }

    protected override void Stop()
    {
        if (_signals != null && _alGanar != null) _signals.OffBattleWon(R.battleId, _alGanar);
        _signals = null;
        _alGanar = null;
    }

    /// La arena con ese id: primero el registro (se llena en Awake), luego las escenas cargadas,
    /// también los objetos apagados.
    static BossArenaController BuscarArena(string id)
    {
        if (BossArenaController.TryGetById(id, out var arena) && arena != null) return arena;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.IsValid() || !escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var a in raiz.GetComponentsInChildren<BossArenaController>(true))
                    if (a.BattleId == id) return a;
        }
        return null;
    }
}
