using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Servicios básicos del grafo: una versión sencilla de cada conexión para que una historia
/// funcione sin escribir código (diálogos con caja y respuestas, misiones, objetos, datos de
/// partida, fundidos y bocadillos, todo dibujado con IMGUI). Ponlo en un objeto de la escena.
/// Solo se registra lo que el juego no haya registrado ya: si conectas tus sistemas, mandan los tuyos.
[DisallowMultipleComponent]
public sealed class NarrativeBasicServices : MonoBehaviour
{
    [Tooltip("Enseña en una esquina las misiones en curso y los objetos.")]
    public bool showJournal = true;

    static NarrativeBasicServices s_current;

    /// true mientras hay un diálogo o una pregunta en pantalla (los botones de charla se esconden).
    public static bool Busy => s_current != null && (s_current._dialogue != null || s_current._prompt != null);

    readonly List<Action> _unregister = new();
    readonly Flags _flags = new();
    readonly Quests _quests = new();
    readonly Inventory _inventory = new();

    NarrativeBasicDialogue _dialogue;
    int _line;
    Action _onClosed;

    string _prompt, _optionA, _optionB;
    Action _onA, _onB;

    readonly List<Bubble> _bubbles = new();
    Color _screen = Color.clear;
    readonly HashSet<string> _curtains = new();

    void Awake()
    {
        s_current = this;
        Offer<INarrativeFlagStore>(_flags);
        Offer<IQuestService>(_quests);
        Offer<INarrativeInventory>(_inventory);
        Offer<INarrativeSaveData>(new Save());
        Offer<INarrativeDialoguePlayer>(new DialoguePort(this));
        Offer<INarrativeChoicePresenter>(new ChoicePort(this));
        Offer<INarrativeSpeechBubbles>(new BubblePort(this));
        Offer<INarrativeScreen>(new ScreenPort(this));
        Offer<INarrativeActorDirectory>(new Actors());
        DefaultNarrativeSignals.EnsureInstance();
    }

    void OnDestroy()
    {
        foreach (var undo in _unregister) undo();
        if (s_current == this) s_current = null;
    }

    void Offer<T>(T service) where T : class
    {
        if (NarrativeServices.Get<T>() != null) return;
        NarrativeServices.Register(service);
        _unregister.Add(() => NarrativeServices.Unregister(service));
    }

    // ── Dibujo ──────────────────────────────────────────────────────────────

    static GUIStyle s_box, s_name, s_text, s_button, s_small;
    static Texture2D s_white;

    public static GUIStyle ButtonStyle { get { EnsureStyles(); return s_button; } }

    static void EnsureStyles()
    {
        if (s_box != null) return;
        s_white = Texture2D.whiteTexture;
        var dark = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        dark.SetPixel(0, 0, new Color(0.11f, 0.10f, 0.09f, 0.94f));
        dark.Apply();
        var accent = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        accent.SetPixel(0, 0, new Color(1f, 0.48f, 0.27f, 1f));
        accent.Apply();
        s_box = new GUIStyle(GUI.skin.box) { normal = { background = dark }, padding = new RectOffset(22, 22, 16, 16) };
        s_name = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.55f, 0.35f) } };
        s_text = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, normal = { textColor = new Color(0.95f, 0.93f, 0.90f) } };
        s_small = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.75f, 0.71f, 0.66f) } };
        s_button = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { background = accent, textColor = new Color(0.1f, 0.07f, 0.05f) }, hover = { background = accent, textColor = Color.black } };
    }

    void OnGUI()
    {
        EnsureStyles();
        GUI.depth = -100;

        for (int i = _bubbles.Count - 1; i >= 0; i--) DrawBubble(_bubbles[i]);

        if (showJournal) DrawJournal();
        if (_dialogue != null) DrawDialogue();
        else if (_prompt != null) DrawChoice();

        if (_screen.a > 0.001f)
        {
            var old = GUI.color;
            GUI.color = _screen;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), s_white);
            GUI.color = old;
        }
    }

    Rect BottomBox(float height) => new Rect(Screen.width * 0.12f, Screen.height - height - 28f, Screen.width * 0.76f, height);

    void DrawDialogue()
    {
        var l = _dialogue.lines[_line];
        var r = BottomBox(150f);
        GUI.Box(r, GUIContent.none, s_box);
        GUILayout.BeginArea(new Rect(r.x + 22, r.y + 14, r.width - 44, r.height - 28));
        if (!string.IsNullOrEmpty(l.speaker)) GUILayout.Label(l.speaker, s_name);
        GUILayout.Label(l.text, s_text);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{_line + 1}/{_dialogue.lines.Count}", s_small);
        GUILayout.FlexibleSpace();
        bool next = GUILayout.Button(_line + 1 < _dialogue.lines.Count ? "Seguir  ▸" : "Cerrar", s_button, GUILayout.Width(130), GUILayout.Height(32));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        var e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return)) { next = true; e.Use(); }
        if (next) AdvanceDialogue();
    }

    void AdvanceDialogue()
    {
        _line++;
        if (_dialogue != null && _line < _dialogue.lines.Count) return;
        var done = _onClosed;
        _dialogue = null;
        _onClosed = null;
        done?.Invoke();
    }

    void DrawChoice()
    {
        var r = BottomBox(150f);
        GUI.Box(r, GUIContent.none, s_box);
        GUILayout.BeginArea(new Rect(r.x + 22, r.y + 14, r.width - 44, r.height - 28));
        GUILayout.Label(_prompt, s_text);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Action chosen = null;
        if (GUILayout.Button(_optionA, s_button, GUILayout.MinWidth(150), GUILayout.Height(34))) chosen = _onA;
        GUILayout.Space(12);
        if (GUILayout.Button(_optionB, s_button, GUILayout.MinWidth(150), GUILayout.Height(34))) chosen = _onB;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        if (chosen == null) return;
        _prompt = null;
        chosen();
    }

    void DrawJournal()
    {
        var lines = new List<string>();
        foreach (var q in _quests.States) if (q.Value == NarrativeQuestState.Active) lines.Add("◆ " + Humanize(q.Key));
        foreach (var i in _inventory.Items) if (i.Value > 0) lines.Add("· " + i.Key + (i.Value > 1 ? $" ×{i.Value}" : string.Empty));
        if (lines.Count == 0) return;
        var r = new Rect(Screen.width - 300f, 20f, 280f, 34f + lines.Count * 22f);
        GUI.Box(r, GUIContent.none, s_box);
        GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 32, 20), "EN CURSO", s_small);
        for (int i = 0; i < lines.Count; i++) GUI.Label(new Rect(r.x + 16, r.y + 28 + i * 22, r.width - 32, 22), lines[i], s_text);
    }

    void DrawBubble(Bubble b)
    {
        if (Time.time > b.Until && !b.Line.UntilHidden) { Hide(b); return; }
        var cam = Camera.main;
        Vector2 at = new Vector2(Screen.width * 0.5f, Screen.height * 0.25f);
        if (b.Line.Anchor != null && cam != null)
        {
            var p = cam.WorldToScreenPoint(b.Line.Anchor.position + Vector3.up * 2.2f);
            if (p.z > 0f) at = new Vector2(p.x, Screen.height - p.y);
        }
        var text = string.IsNullOrEmpty(b.Line.SpeakerName) ? b.Line.Text : $"{b.Line.SpeakerName}: {b.Line.Text}";
        var size = s_text.CalcSize(new GUIContent(text));
        float w = Mathf.Min(size.x + 32f, 420f);
        float h = s_text.CalcHeight(new GUIContent(text), w - 32f) + 22f;
        var r = new Rect(at.x - w * 0.5f, at.y - h, w, h);
        GUI.Box(r, GUIContent.none, s_box);
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, h - 20), text, s_text);
    }

    void Hide(Bubble b)
    {
        if (!_bubbles.Remove(b)) return;
        b.OnHidden?.Invoke();
    }

    static string Humanize(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var s = id.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // ── Conexiones ──────────────────────────────────────────────────────────

    sealed class Flags : INarrativeFlagStore
    {
        readonly HashSet<string> _set = new();
        public bool IsAvailable => true;
        public bool Has(string flag) => _set.Contains(flag);
        public void Set(string flag, bool value) { if (value) _set.Add(flag); else _set.Remove(flag); }
        public IEnumerable<string> All() => _set;
    }

    sealed class Quests : IQuestService
    {
        public readonly Dictionary<string, NarrativeQuestState> States = new();
        readonly Dictionary<string, Action> _done = new();

        public void Offer(string questId, object npcCtx) => StartQuest(questId);
        public bool IsCompleted(string questId) => GetState(questId) == NarrativeQuestState.Completed;
        public void OnCompleted(string questId, Action cb) { _done.TryGetValue(questId, out var a); _done[questId] = a + cb; }
        public void OffCompleted(string questId, Action cb) { if (_done.TryGetValue(questId, out var a)) _done[questId] = a - cb; }
        public void StartQuest(string questId) { if (!string.IsNullOrEmpty(questId) && GetState(questId) == NarrativeQuestState.NotStarted) States[questId] = NarrativeQuestState.Active; }
        public void CompleteStep(string questId, int stepIndex) => StartQuest(questId);
        public void CompleteStepByConditionId(string questId, string stepConditionId) => StartQuest(questId);
        public NarrativeQuestState GetState(string questId) => questId != null && States.TryGetValue(questId, out var s) ? s : NarrativeQuestState.NotStarted;

        public void Complete(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            States[questId] = NarrativeQuestState.Completed;
            if (_done.TryGetValue(questId, out var a)) a?.Invoke();
        }
    }

    sealed class Inventory : INarrativeInventory
    {
        public readonly Dictionary<string, int> Items = new();
        event Action<NarrativeItemAsset, int, int> Added;

        static string Key(NarrativeItemAsset item) => item is NarrativeBasicItem b && !string.IsNullOrEmpty(b.displayName) ? b.displayName : item != null ? item.ItemId : null;

        public bool IsAvailable => true;
        public int Count(NarrativeItemAsset item) => item != null && Items.TryGetValue(Key(item), out var n) ? n : 0;
        public bool Has(NarrativeItemAsset item, int amount) => Count(item) >= Mathf.Max(1, amount);

        public void Add(NarrativeItemAsset item, int amount)
        {
            if (item == null || amount <= 0) return;
            int total = Count(item) + amount;
            Items[Key(item)] = total;
            Added?.Invoke(item, amount, total);
        }

        public bool TryConsume(NarrativeItemAsset item, int amount)
        {
            if (!Has(item, amount)) return false;
            Items[Key(item)] = Count(item) - Mathf.Max(1, amount);
            return true;
        }

        public bool WatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded) { Added += onAdded; return true; }
        public void UnwatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded) => Added -= onAdded;
    }

    sealed class Save : INarrativeSaveData
    {
        public bool IsLoaded => false;
        public int LoadCount => 0;
        public IReadOnlyList<NarrativeBlackboardRecord> GetSavedBlackboards() => Array.Empty<NarrativeBlackboardRecord>();
    }

    sealed class Actors : INarrativeActorDirectory
    {
        public Transform FindActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            foreach (var t in FindObjectsByType<NarrativeBasicTalkTarget>(FindObjectsInactive.Exclude))
                if (t.actorId == actorId) return t.transform;
            var go = GameObject.Find(actorId);
            return go != null ? go.transform : null;
        }
    }

    sealed class DialoguePort : INarrativeDialoguePlayer
    {
        readonly NarrativeBasicServices _s;
        public DialoguePort(NarrativeBasicServices s) => _s = s;
        public bool IsOpen => _s != null && _s._dialogue != null;

        public bool TryPlay(NarrativeDialogueAsset dialogue, Transform speaker, Action onClosed)
        {
            if (_s == null || dialogue is not NarrativeBasicDialogue basic || basic.LineCount == 0) return false;
            _s._dialogue = basic;
            _s._line = 0;
            _s._onClosed = onClosed;
            return true;
        }
    }

    sealed class ChoicePort : INarrativeChoicePresenter
    {
        readonly NarrativeBasicServices _s;
        public ChoicePort(NarrativeBasicServices s) => _s = s;

        public bool TryShowChoice(string prompt, string optionA, string optionB, Action onA, Action onB)
        {
            if (_s == null) return false;
            _s._prompt = prompt ?? string.Empty;
            _s._optionA = optionA;
            _s._optionB = optionB;
            _s._onA = onA;
            _s._onB = onB;
            return true;
        }
    }

    sealed class Bubble : INarrativeLineHandle
    {
        public NarrativeBasicServices Owner;
        public NarrativeBubbleLine Line;
        public float Until;
        public Action OnHidden;
        public void Cancel() => Owner?.Hide(this);
    }

    sealed class BubblePort : INarrativeSpeechBubbles
    {
        readonly NarrativeBasicServices _s;
        public BubblePort(NarrativeBasicServices s) => _s = s;
        public bool IsAvailable => _s != null;

        public INarrativeLineHandle Say(NarrativeBubbleLine line, Action onHidden)
        {
            var b = new Bubble { Owner = _s, Line = line, OnHidden = onHidden, Until = Time.time + (line.Duration > 0f ? line.Duration : 3f) };
            _s._bubbles.Add(b);
            return b;
        }
    }

    sealed class ScreenPort : INarrativeScreen
    {
        readonly NarrativeBasicServices _s;
        public ScreenPort(NarrativeBasicServices s) => _s = s;
        public bool IsCovered => _s._screen.a > 0.99f;

        public IEnumerator Fade(Color color, float seconds, bool cover)
        {
            var from = _s._screen;
            var to = new Color(color.r, color.g, color.b, cover ? 1f : 0f);
            if (!cover && from.a <= 0.001f) yield break;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _s._screen = Color.Lerp(from, to, t / Mathf.Max(0.01f, seconds));
                yield return null;
            }
            _s._screen = to;
        }

        public void SetImmediate(Color color) => _s._screen = color;

        public IEnumerator UncoverWithTransition(Color color, float recolorSeconds, ScriptableObject transition, Action onUncovered)
        {
            yield return Fade(color, Mathf.Max(0.4f, recolorSeconds), false);
            onUncovered?.Invoke();
        }

        public void CloseCurtain(string key) { _s._curtains.Add(key ?? string.Empty); _s._screen = Color.black; }
        public void KeepCurtainClosed(string key) => _s._curtains.Add(key ?? string.Empty);

        public void ReleaseCurtain(string key)
        {
            _s._curtains.Remove(key ?? string.Empty);
            if (_s._curtains.Count == 0) _s._screen = Color.clear;
        }
    }
}
