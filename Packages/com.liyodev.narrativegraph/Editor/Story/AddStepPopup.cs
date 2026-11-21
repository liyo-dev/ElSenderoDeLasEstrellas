using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// "What happens now?": searchable list of step types, grouped by family (history or game)
    /// and then by category. Opens where the user dropped a connection, double-clicked or pressed
    /// the add button.
    public sealed class AddStepPopup : VisualElement
    {
        const string GameStepsKey = "NarrativeGraph.AddStepPopup.GameSteps";

        readonly TextField _search;
        readonly Label _hint;
        readonly ScrollView _list;
        Action<Type> _onPick;
        Action<StoryTemplate> _onTemplate;

        public bool IsOpen => style.display == DisplayStyle.Flex;

        public AddStepPopup()
        {
            AddToClassList("story-popup");
            style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.AddToClassList("story-popup__header");
            Add(header);

            _search = new TextField();
            _search.AddToClassList("story-popup__search");
            _search.textEdition.placeholder = StoryText.T("¿Qué pasa ahora?", "What happens now?");
            _search.RegisterValueChangedCallback(e => Fill(e.newValue));
            _search.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Close(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    var first = _list.Query<Button>().Where(b => b.userData is Action).First();
                    if (first != null) { (first.userData as Action)?.Invoke(); e.StopPropagation(); }
                }
            }, TrickleDown.TrickleDown);
            header.Add(_search);

            var close = new Button(Close) { text = "×", tooltip = StoryText.T("Cerrar", "Close") };
            close.AddToClassList("story-popup__close");
            header.Add(close);

            _hint = new Label();
            _hint.AddToClassList("story-popup__hint");
            Add(_hint);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("story-popup__list");
            Add(_list);

            RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            RegisterCallback<WheelEvent>(e => e.StopPropagation());
        }

        public void Open(Vector2 localPosition, Vector2 containerSize, string hint, Action<Type> onPick,
                         Action<StoryTemplate> onTemplate = null)
        {
            _onPick = onPick;
            _onTemplate = onTemplate;
            _hint.text = hint ?? string.Empty;
            _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
            style.left = Mathf.Clamp(localPosition.x, 8f, Mathf.Max(8f, containerSize.x - 340f));
            style.top = Mathf.Clamp(localPosition.y, 8f, Mathf.Max(8f, containerSize.y - 430f));
            style.display = DisplayStyle.Flex;
            _search.SetValueWithoutNotify(string.Empty);
            Fill(string.Empty);
            schedule.Execute(() => _search.Q("unity-text-input")?.Focus());
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
            _onPick = null;
            _onTemplate = null;
        }

        void Fill(string query)
        {
            _list.Clear();
            query = query?.Trim() ?? string.Empty;

            var entries = NarrativeNodeCatalog.All
                .Where(e => !e.obsolete)
                .Where(e => query.Length == 0
                            || e.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || (e.description ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || e.category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            bool any = false;
            if (_onTemplate != null)
            {
                var templates = StoryTemplate.All()
                    .Where(t => query.Length == 0
                                || t.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                                || (t.description ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                if (templates.Count > 0)
                {
                    any = true;
                    var title = new Label(StoryText.T("MIS PLANTILLAS", "MY TEMPLATES"));
                    title.AddToClassList("story-popup__group");
                    title.style.color = StoryColors.Accent;
                    _list.Add(title);
                    foreach (var template in templates)
                    {
                        var captured = template;
                        string desc = string.IsNullOrWhiteSpace(template.description)
                            ? (template.steps.Count == 1 ? StoryText.T("1 paso", "1 step")
                                                           : StoryText.T($"{template.steps.Count} pasos", $"{template.steps.Count} steps"))
                            : template.description;
                        _list.Add(Item(template.Title, desc, () =>
                        {
                            var cb = _onTemplate;
                            Close();
                            cb?.Invoke(captured);
                        }));
                    }
                }
            }

            var story = entries.Where(e => e.family != NarrativeStepFamily.Juego).ToList();
            if (story.Count > 0)
            {
                any = true;
                _list.Add(Family(StoryText.T("HISTORIA", "STORY"), story.Count));
                AddGroups(_list, story);
            }

            var game = entries.Where(e => e.family == NarrativeStepFamily.Juego).ToList();
            if (game.Count > 0)
            {
                any = true;
                bool open = query.Length > 0 || EditorPrefs.GetBool(GameStepsKey, false);

                var box = new VisualElement();
                box.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                AddGroups(box, game);

                bool openNow = open;
                Button header = null;
                header = GameHeader(StoryText.T("DEL JUEGO", "FROM THE GAME"), game.Count, openNow, () =>
                {
                    if (query.Length > 0) return;
                    openNow = !openNow;
                    EditorPrefs.SetBool(GameStepsKey, openNow);
                    box.style.display = openNow ? DisplayStyle.Flex : DisplayStyle.None;
                    header.text = FamilyText(StoryText.T("DEL JUEGO", "FROM THE GAME"), game.Count, openNow);
                });
                _list.Add(header);
                _list.Add(box);
            }

            if (!any)
            {
                var none = new Label(StoryText.T("Ningún paso coincide.", "No step matches."));
                none.AddToClassList("story-popup__empty");
                _list.Add(none);
            }
        }

        void AddGroups(VisualElement parent, IEnumerable<NarrativeNodeCatalog.Entry> entries)
        {
            foreach (var group in entries.GroupBy(e => e.category)
                                         .OrderBy(g => NarrativeNodeCatalog.CategoryOrder(g.Key)))
            {
                var title = new Label(group.Key.ToUpperInvariant());
                title.AddToClassList("story-popup__group");
                title.style.color = StoryColors.Category(group.Key);
                parent.Add(title);

                foreach (var entry in group.OrderBy(e => e.title))
                {
                    var captured = entry;
                    parent.Add(Item(entry.title, entry.description, () =>
                    {
                        var cb = _onPick;
                        Close();
                        cb?.Invoke(captured.type);
                    }));
                }
            }
        }

        static Label Family(string title, int count)
        {
            var label = new Label($"{title}  ·  {count}");
            label.AddToClassList("story-popup__family");
            return label;
        }

        static Button GameHeader(string title, int count, bool open, Action toggle)
        {
            var button = new Button(toggle) { text = FamilyText(title, count, open) };
            button.AddToClassList("story-popup__family");
            button.AddToClassList("story-popup__family--button");
            return button;
        }

        static string FamilyText(string title, int count, bool open)
            => $"{title}  ·  {count}   {(open ? "▾" : "▸")}";

        static Button Item(string title, string description, Action pick)
        {
            var item = new Button(pick) { userData = pick };
            item.AddToClassList("story-popup__item");
            var name = new Label(title);
            name.AddToClassList("story-popup__item-title");
            item.Add(name);
            if (!string.IsNullOrEmpty(description))
            {
                var desc = new Label(description);
                desc.AddToClassList("story-popup__item-desc");
                item.Add(desc);
            }
            return item;
        }
    }
}
