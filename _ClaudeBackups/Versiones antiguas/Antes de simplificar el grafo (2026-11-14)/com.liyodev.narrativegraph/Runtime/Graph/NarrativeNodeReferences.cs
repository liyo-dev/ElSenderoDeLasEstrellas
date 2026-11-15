using System.Collections.Generic;

/// Lo que declara un nodo en NarrativeNode.Describe, recogido como datos y sin pintar nada.
///
/// Lo usan las herramientas que solo necesitan saber qué toca un nodo (índice del proyecto,
/// validadores, búsqueda, lanzador de pruebas). Las referencias se guardan tal cual llegan,
/// vacías incluidas; cada consumidor filtra lo que no le sirve. Text() devuelve el texto por
/// defecto porque aquí no se traduce.
public sealed class NarrativeNodeReferences : INarrativeNodeDescriber
{
    public readonly struct QuestUse
    {
        public readonly string QuestId;
        public readonly string Verb;
        public readonly IReadOnlyList<string> Steps;
        public QuestUse(string questId, string verb, IReadOnlyList<string> steps) { QuestId = questId; Verb = verb; Steps = steps; }
    }

    public readonly struct SignalUse
    {
        public readonly string Key;
        public readonly SignalRole Role;
        public readonly string Label;
        public SignalUse(string key, SignalRole role, string label) { Key = key; Role = role; Label = label; }
    }

    public readonly struct ActorUse
    {
        public readonly string ActorId;
        public readonly string Verb;
        public ActorUse(string actorId, string verb) { ActorId = actorId; Verb = verb; }
    }

    public readonly struct TextUse
    {
        public readonly string Key;
        public readonly string Fallback;
        public TextUse(string key, string fallback) { Key = key; Fallback = fallback; }
    }

    public readonly struct LineUse
    {
        public readonly string SpeakerId;
        public readonly bool IsPlayer;
        public readonly string TextId;
        public readonly string Text;
        public LineUse(string speakerId, bool isPlayer, string textId, string text) { SpeakerId = speakerId; IsPlayer = isPlayer; TextId = textId; Text = text; }
    }

    public readonly struct ItemUse
    {
        public readonly UnityEngine.Object Item;
        public readonly int Amount;
        public readonly string Verb;
        public ItemUse(UnityEngine.Object item, int amount, string verb) { Item = item; Amount = amount; Verb = verb; }
    }

    public readonly List<QuestUse> Quests = new();
    public readonly List<SignalUse> Signals = new();
    public readonly List<ActorUse> Actors = new();
    public readonly List<string> Flags = new();
    public readonly List<string> Anchors = new();
    public readonly List<UnityEngine.Object> Assets = new();
    public readonly List<LineUse> Lines = new();
    public readonly List<ItemUse> Items = new();
    public readonly List<TextUse> Texts = new();

    /// Todo el texto de presentación (líneas, campos, etiquetas, detalles), para buscar en él.
    public readonly List<string> Captions = new();

    /// Referencias de un nodo en una lista nueva.
    public static NarrativeNodeReferences Of(NarrativeNode node)
    {
        var refs = new NarrativeNodeReferences();
        node?.Describe(refs);
        return refs;
    }

    /// Vacía la lista y recoge las de otro nodo (para reutilizar la misma instancia en un bucle).
    public NarrativeNodeReferences Collect(NarrativeNode node)
    {
        Quests.Clear(); Signals.Clear(); Actors.Clear(); Flags.Clear(); Anchors.Clear();
        Assets.Clear(); Lines.Clear(); Items.Clear(); Texts.Clear(); Captions.Clear();
        node?.Describe(this);
        return this;
    }

    /// true si el nodo declara que emite (o espera, según role) la señal indicada.
    public bool HasSignal(string key, SignalRole role)
    {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var s in Signals)
            if (s.Role == role && s.Key == key) return true;
        return false;
    }

    public void Quest(string questId, string verb, IReadOnlyList<string> steps = null) => Quests.Add(new QuestUse(questId, verb, steps));
    public void Signal(string key, SignalRole role, string label = null) => Signals.Add(new SignalUse(key, role, label));
    public void Actor(string actorId, string verb) => Actors.Add(new ActorUse(actorId, verb));
    public void Flag(string key, string shownAs = null) => Flags.Add(key);
    public void Anchor(string anchorId) => Anchors.Add(anchorId);

    public void Asset(UnityEngine.Object asset, string kind, string detail = null)
    {
        if (asset != null) Assets.Add(asset);
        AddCaption(detail);
    }

    public void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null)
        => Lines.Add(new LineUse(speakerId, isPlayer, textId, text));

    public void Item(UnityEngine.Object item, int amount, string verb) => Items.Add(new ItemUse(item, amount, verb));

    public string Text(string locKey, string fallback)
    {
        Texts.Add(new TextUse(locKey, fallback));
        return fallback;
    }

    public void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal) => AddCaption(text);
    public void Speaker(string name) => AddCaption(name);
    public void Field(string label, string value) => AddCaption(value);
    public void Tag(string text) => AddCaption(text);

    void AddCaption(string text)
    {
        if (!string.IsNullOrEmpty(text)) Captions.Add(text);
    }
}
