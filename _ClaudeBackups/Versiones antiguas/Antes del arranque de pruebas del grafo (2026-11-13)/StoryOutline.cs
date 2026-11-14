using System;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Left panel: the chapter as an index (sections → numbered steps) and its test points.
    public sealed class StoryOutline : VisualElement
    {
        public event Action<NarrativeNode> StepClicked;
        public event Action AddClicked;
        /// A word of the vocabulary was picked: show the steps that use it.
        public event Action<string> WordClicked;

        readonly ScrollView _words;
        readonly Button _stepsTab;
        readonly Button _wordsTab;
        bool _showWords;
        NarrativeGraph _graph;

        readonly Label _title;
        readonly Label _count;
        readonly ScrollView _list;
        readonly VisualElement _checkpoints;

        public StoryOutline()
        {
            AddToClassList("story-outline");

            var head = new VisualElement();
            head.AddToClassList("story-outline__head");
            var kicker = new Label(StoryText.T("CAPÍTULO", "CHAPTER"));
            kicker.AddToClassList("story-kicker");
            head.Add(kicker);
            _title = new Label();
            _title.AddToClassList("story-outline__title");
            head.Add(_title);
            _count = new Label();
            _count.AddToClassList("story-outline__count");
            head.Add(_count);
            Add(head);

            var add = new Button(() => AddClicked?.Invoke());
            add.AddToClassList("story-outline__add");
            var plus = new Label("+");
            plus.AddToClassList("story-outline__add-plus");
            add.Add(plus);
            add.Add(new Label(StoryText.T("Añadir paso", "Add step")) { name = "add-text" });
            var key = new Label("A");
            key.AddToClassList("story-key");
            add.Add(key);
            Add(add);

            var tip = new Label(StoryText.T("O arrastra desde el borde de una tarjeta hasta un hueco vacío.",
                                            "Or drag from the edge of a card to an empty spot."));
            tip.AddToClassList("story-outline__tip");
            Add(tip);

            var tabs = new VisualElement();
            tabs.AddToClassList("story-segmented");
            _stepsTab = new Button(() => ShowWords(false)) { text = StoryText.T("Pasos", "Steps") };
            _wordsTab = new Button(() => ShowWords(true))
            {
                text = StoryText.T("Vocabulario", "Vocabulary"),
                tooltip = StoryText.T("Personajes, misiones, objetos y datos que usa el capítulo", "Characters, quests, items and data the chapter uses"),
            };
            tabs.Add(_stepsTab);
            tabs.Add(_wordsTab);
            Add(tabs);

            _list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _list.AddToClassList("story-outline__list");
            Add(_list);

            _words = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _words.AddToClassList("story-outline__list");
            Add(_words);
            ShowWords(false);

            _checkpoints = new VisualElement();
            _checkpoints.AddToClassList("story-outline__checkpoints");
            Add(_checkpoints);
        }

        void ShowWords(bool words)
        {
            _showWords = words;
            _stepsTab.EnableInClassList("is-on", !words);
            _wordsTab.EnableInClassList("is-on", words);
            _list.style.display = words ? DisplayStyle.None : DisplayStyle.Flex;
            _words.style.display = words ? DisplayStyle.Flex : DisplayStyle.None;
            if (words) RefreshWords();
        }

        void RefreshWords()
        {
            _words.Clear();
            var vocabulary = StoryVocabulary.Build(_graph);
            if (vocabulary.Groups.Count == 0)
            {
                var none = new Label(StoryText.T("Este capítulo aún no usa personajes, misiones ni datos.", "This chapter uses no characters, quests or data yet."));
                none.AddToClassList("story-outline__tip");
                _words.Add(none);
                return;
            }
            foreach (var group in vocabulary.Groups)
            {
                var head = new Label(group.Title.ToUpperInvariant());
                head.AddToClassList("story-outline__section-name");
                head.AddToClassList("story-outline__word-group");
                _words.Add(head);
                foreach (var pair in group.Uses)
                {
                    var word = pair.Key;
                    var row = new Button(() => WordClicked?.Invoke(word)) { tooltip = StoryText.T("Ver los pasos que lo usan", "Show the steps that use it") };
                    row.AddToClassList("story-outline__row");
                    var title = new Label(word);
                    title.AddToClassList("story-outline__row-title");
                    row.Add(title);
                    var count = new Label(pair.Value.ToString());
                    count.AddToClassList("story-mono");
                    row.Add(count);
                    _words.Add(row);
                }
            }
        }

        public void Refresh(NarrativeGraph graph, StoryStructure structure, NarrativeNode selected, Func<NarrativeNode, bool> onPath)
        {
            _graph = graph;
            if (_showWords) RefreshWords();
            _list.Clear();
            _checkpoints.Clear();
            _title.text = graph != null ? graph.name : "—";
            if (graph == null || structure == null) { _count.text = string.Empty; return; }

            int steps = 0;
            foreach (var section in structure.Sections) steps += section.Steps.Count;
            _count.text = StoryText.T($"{steps} pasos · {structure.Sections.Count} secciones", $"{steps} steps · {structure.Sections.Count} sections");

            foreach (var section in structure.Sections)
            {
                var head = new VisualElement();
                head.AddToClassList("story-outline__section");
                var num = new Label((section.Index + 1).ToString());
                num.AddToClassList("story-mono");
                head.Add(num);
                var name = new Label(StoryStructure.DisplayName(section.Name).ToUpperInvariant());
                name.AddToClassList("story-outline__section-name");
                head.Add(name);
                var count = new Label(section.Steps.Count.ToString());
                count.AddToClassList("story-mono");
                head.Add(count);
                _list.Add(head);

                foreach (var step in section.Steps)
                    _list.Add(Row(step, structure.Number(step), step == selected, onPath != null && onPath(step)));
            }

            bool anyCheckpoint = false;
            foreach (var node in structure.StoryOrder)
            {
                if (node is not CheckpointNode) continue;
                if (!anyCheckpoint)
                {
                    var t = new Label(StoryText.T("PUNTOS DE PRUEBA", "TEST POINTS"));
                    t.AddToClassList("story-kicker");
                    _checkpoints.Add(t);
                    anyCheckpoint = true;
                }
                _checkpoints.Add(Row(node, structure.Number(node), node == selected, false));
            }
        }

        VisualElement Row(NarrativeNode node, string number, bool selected, bool onPath)
        {
            var row = new Button(() => StepClicked?.Invoke(node));
            row.AddToClassList("story-outline__row");
            row.EnableInClassList("is-selected", selected);
            row.EnableInClassList("is-on-path", onPath);
            var num = new Label(number);
            num.AddToClassList("story-outline__row-number");
            row.Add(num);
            var title = new Label(string.IsNullOrWhiteSpace(node.displayTitle) ? NarrativeNodeCatalog.TitleOf(node.GetType()) : node.displayTitle);
            title.AddToClassList("story-outline__row-title");
            row.Add(title);
            return row;
        }
    }
}
