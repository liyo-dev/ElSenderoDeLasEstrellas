using System.Collections.Generic;

/// Envuelve un INarrativeStateWriter para que las marcas locales (NarrativeLocation.Local) queden
/// ligadas a un contexto con nombre. Lo usa quien proyecta un contenedor de efectos cuyas marcas
/// solo existen dentro de él (una secuencia y su escenario, por ejemplo). Todo lo demás pasa tal cual.
public sealed class ScopedNarrativeStateWriter : INarrativeStateWriter
{
    readonly INarrativeStateWriter _inner;
    readonly string _scope;
    readonly string _fallbackScope;

    public ScopedNarrativeStateWriter(INarrativeStateWriter inner, string scope, string fallbackScope = null)
    {
        _inner = inner;
        _scope = scope;
        _fallbackScope = fallbackScope;
    }

    public bool GetFlag(string key) => _inner.GetFlag(key);
    public void SetFlag(string key, bool value) => _inner.SetFlag(key, value);
    public void StartQuest(string questId) => _inner.StartQuest(questId);

    public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices)
        => _inner.CompleteQuestSteps(questId, stepConditionIds, stepIndices);

    public void CompleteQuest(string questId) => _inner.CompleteQuest(questId);
    public void AddItem(string itemId, int amount) => _inner.AddItem(itemId, amount);

    public void PlaceActor(string actorId, NarrativeLocation location)
        => _inner.PlaceActor(actorId, location.IsUnboundLocal ? NarrativeLocation.InScope(_scope, location.Mark, _fallbackScope) : location);

    public void SetActorActive(string actorId, bool active) => _inner.SetActorActive(actorId, active);
    public void Note(string message) => _inner.Note(message);
    public bool TryGetExtension<T>(out T extension) where T : class => _inner.TryGetExtension(out extension);
}
