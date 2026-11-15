using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Desbloquea habilidades y hechizos del jugador.
[Serializable]
[NarrativeNodeInfo("Jugador", "Desbloquear habilidades", "Desbloquea habilidades y hechizos.")]
public sealed class PedirDesbloqueo : NarrativeRequest, INarrativeStateEffect
{
    [Tooltip("Habilidades principales (nadar, saltar, magia, volar…) a desbloquear.")]
    [NarrativeChipList("Habilidades a desbloquear")]
    public List<AbilityKey> abilityKeysToUnlock = new();

    [Tooltip("Hechizos a desbloquear.")]
    [NarrativeChipList("Hechizos a desbloquear")]
    public List<SpellId> spellsToUnlock = new();

    [Tooltip("Si se desbloquean hechizos, intentar ponerlos en ranuras vacías.")]
    public bool assignSpellsToEmptySlot = true;

    [Tooltip("Si hay hechizos que desbloquear, se pasa el mando a Will antes de aplicarlos, para que quien " +
             "se ve en pantalla sea quien aprende el hechizo.")]

    public bool forceWillBeforeSpellUnlock = true;

    [Tooltip("Si se pone, se apunta este flag al desbloquear y, si ya estaba, no se repite.")]
    public string oneShotFlag = "";

    [Tooltip("Aplica el preset al jugador después de desbloquear (sin tocar el inventario).")]
    public bool applyPresetAfterUnlock = true;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (abilityKeysToUnlock.Count > 0) d.Field("Habilidades", string.Join(", ", abilityKeysToUnlock));
        if (spellsToUnlock.Count > 0) d.Field("Hechizos", string.Join(", ", spellsToUnlock));
        if (!string.IsNullOrEmpty(oneShotFlag)) d.Flag(oneShotFlag);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (abilityKeysToUnlock.Count == 0 && spellsToUnlock.Count == 0) warnings.Add("No desbloquea nada");
    }

    public void Project(INarrativeStateWriter state)
    {
        if (state.TryGetExtension<IPlayerLoadoutState>(out var loadout))
        {
            foreach (var key in abilityKeysToUnlock) loadout.UnlockAbility(key);
            foreach (var spell in spellsToUnlock)
                if (spell != SpellId.None) loadout.UnlockSpell(spell, assignSpellsToEmptySlot);
        }
        if (!string.IsNullOrEmpty(oneShotFlag)) state.SetFlag(oneShotFlag, true);
    }
}

public sealed class DesbloqueoEnCurso : NarrativeRequestRun<PedirDesbloqueo>
{
    Coroutine _rutina;

    protected override void Start()
    {
        if (!string.IsNullOrEmpty(R.oneShotFlag) && UnlockService.HasFlag(R.oneShotFlag))
        {
            Finish(0);
            return;
        }
        // Se aplica dos fotogramas después (y con el diálogo ya cerrado): el paso suele llegar
        // desde el cierre de un diálogo y el input aún no se ha soltado.
        if (Runner != null) _rutina = Runner.StartCoroutine(Co_TrasElDialogo());
        else Desbloquear();
    }

    protected override void Stop()
    {
        if (_rutina != null && Runner != null) Runner.StopCoroutine(_rutina);
        _rutina = null;
    }

    IEnumerator Co_TrasElDialogo()
    {
        yield return null;
        yield return null;
        var dialogo = DialogueManager.Instance;
        if (dialogo != null && dialogo.IsOpen)
        {
            while (dialogo != null && dialogo.IsOpen) yield return null;
            yield return null;
        }
        _rutina = null;
        Desbloquear();
    }

    void Desbloquear()
    {
        bool cambiado = false;
        try
        {
            foreach (var key in R.abilityKeysToUnlock) cambiado |= UnlockService.UnlockAbility(key);
            if (R.spellsToUnlock.Count > 0)
            {
                if (R.forceWillBeforeSpellUnlock) PartyControlManager.Instance?.ForceSwitch(PartyControlManager.CharacterSlot.Will);
                foreach (var spell in R.spellsToUnlock) cambiado |= UnlockService.UnlockSpell(spell, R.assignSpellsToEmptySlot);
            }
            if (!string.IsNullOrEmpty(R.oneShotFlag)) cambiado |= UnlockService.AddFlag(R.oneShotFlag);

            if (cambiado && R.applyPresetAfterUnlock)
            {
                var ps = ServiceLocator.Get<PlayerPresetService>(false);
                if (ps == null && PlayerService.TryGetPlayer(out GameObject jugador) && jugador != null)
                    ps = jugador.GetComponentInChildren<PlayerPresetService>(true);
                if (ps != null) ps.ApplyCurrentPreset(includeInventory: false);
                else Debug.LogWarning("[Desbloquear] No hay PlayerPresetService: el preset queda actualizado sin aplicar.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Desbloquear] Error al desbloquear: {ex.Message}");
        }
        Finish(0);
    }
}

/// Desbloquea una prenda del vestuario.
[Serializable]
[NarrativeNodeInfo("Jugador", "Desbloquear vestuario", "Desbloquea una prenda del vestuario.")]
public sealed class PedirVestuario : NarrativeRequest, INarrativeStateEffect
{
    [Header("Vestuario")]
    public WardrobeItemSO wardrobeItem;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (wardrobeItem != null) d.Asset(wardrobeItem, "prenda", wardrobeItem.DisplayName);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (wardrobeItem == null) warnings.Add("Falta la prenda");
    }

    public void Project(INarrativeStateWriter state)
    {
        if (wardrobeItem != null) state.SetFact(HechosDelSendero.Vestuario, wardrobeItem.WardrobeId, HechosDelSendero.Si);
    }
}

public sealed class VestuarioEnCurso : NarrativeRequestRun<PedirVestuario>
{
    protected override void Start()
    {
        // Si ya estaba desbloqueada, WardrobeService devuelve false: es normal al recargar.
        WardrobeService.UnlockWardrobeItem(R.wardrobeItem, false);
        Finish(0);
    }
}
