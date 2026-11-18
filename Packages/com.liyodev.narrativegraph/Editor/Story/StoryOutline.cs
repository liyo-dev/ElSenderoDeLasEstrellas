using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Panel izquierdo: el capítulo como índice (secciones → pasos numerados; los puntos de arranque
    /// llevan ◆) y su vocabulario. Con un paso elegido, su camino se marca y el resto se apaga.
    public sealed class StoryOutline : VisualElement
    {
        public event Action<NarrativeNode> StepClicked;
        public event Action AddClicked;
        /// A word of the vocabulary was picked: show the steps that use it.
        public event Action<string> WordClicked;
        /// Another graph that contains the searched text was picked.
        public event Action<NarrativeGraph> GraphClicked;

        readonly ScrollView _words;
        readonly Button _stepsTab;
        readonly Button _wordsTab;
        bool _showWords;
        NarrativeGraph _graph;

        readonly Label _title;
        readonly Label _count;
        readonly ScrollView _list;

        StoryStructure _structure;
        NarrativeNode _selected;
        Func<NarrativeNode, bool> _onPath;
        string _query = string.Empty;
        Func<NarrativeNode, bool> _matches;
        IReadOnlyList<StorySearch.GraphHits> _elsewhere;

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
                    var row = new Button(() => { ShowWords(false); WordClicked?.Invoke(word); }) { tooltip = StoryText.T("Ver los pasos que lo usan", "Show the steps that use it") };
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
            _structure = structure;
            _selected = selected;
            _onPath = onPath;
            if (_showWords) RefreshWords();
            Draw();
        }

        /// With a text to search, only the steps that contain it are listed.
        public void SetSearch(string query, Func<NarrativeNode, bool> matches)
        {
            _query = query ?? string.Empty;
            _matches = matches;
            _elsewhere = null;
            Draw();
        }

        /// Other graphs that contain the searched text; shown when this one has none.
        public void SetElsewhere(IReadOnlyList<StorySearch.GraphHits> found)
        {
            _elsewhere = found;
            Draw();
        }

        void Draw()
        {
            var graph = _graph;
            var structure = _structure;
            var selected = _selected;
            var onPath = _onPath;
            _list.Clear();
            _title.text = graph != null ? graph.name : "—";
            if (graph == null || structure == null) { _count.text = string.Empty; return; }

            int steps = 0;
            foreach (var section in structure.Sections) steps += section.Steps.Count;
            bool searching = _query.Length > 0 && _matches != null;
            int shown = searching ? structure.Sections.Sum(section => section.Steps.Count(_matches)) : steps;
            _count.text = searching
                ? StoryText.T($"{shown} de {steps} pasos con «{_query}»", $"{shown} of {steps} steps with \"{_query}\"")
                : StoryText.T($"{steps} pasos · {structure.Sections.Count} secciones", $"{steps} steps · {structure.Sections.Count} sections");

            if (searching && shown == 0)
            {
                DrawNotFound();
                return;
            }

            VisualElement selectedRow = null;
            foreach (var section in structure.Sections)
            {
                var sectionSteps = searching ? section.Steps.Where(_matches).ToList() : section.Steps;
                if (sectionSteps.Count == 0) continue;
                var head = new VisualElement();
                head.AddToClassList("story-outline__section");
                var num = new Label((section.Index + 1).ToString());
                num.AddToClassList("story-mono");
                head.Add(num);
                var name = new Label(StoryStructure.DisplayName(section.Name).ToUpperInvariant());
                name.AddToClassList("story-outline__section-name");
                head.Add(name);
                var count = new Label(sectionSteps.Count.ToString());
                count.AddToClassList("story-mono");
                head.Add(count);
                _list.Add(head);

                foreach (var step in sectionSteps)
                {
                    var row = Row(step, structure.Number(step), step == selected, onPath != null && onPath(step), selected != null);
                    _list.Add(row);
                    if (step == selected) selectedRow = row;
                }
            }

            // El índice baja solo hasta el paso elegido.
            if (selectedRow != null)
                _list.schedule.Execute(() => { if (_list.contentContainer.Contains(selectedRow)) _list.ScrollTo(selectedRow); }).StartingIn(30);
        }

        void DrawNotFound()
        {
            AddNote(StoryText.T($"«{_query}» no aparece en este capítulo.", $"\"{_query}\" is not in this chapter."));
            if (_elsewhere == null)
            {
                AddNote(StoryText.T("Buscando en los demás capítulos…", "Looking in the other chapters…"));
                return;
            }
            if (_elsewhere.Count == 0)
            {
                AddNote(StoryText.T("Tampoco aparece en los demás capítulos.", "It is not in the other chapters either."));
                return;
            }

            var head = new Label(StoryText.T("APARECE EN", "FOUND IN"));
            head.AddToClassList("story-outline__section-name");
            head.AddToClassList("story-outline__word-group");
            _list.Add(head);
            foreach (var hit in _elsewhere)
            {
                var graph = hit.Graph;
                var row = new Button(() => GraphClicked?.Invoke(graph)) { tooltip = StoryText.T("Abrir este capítulo con la misma búsqueda", "Open this chapter with the same search") };
                row.AddToClassList("story-outline__row");
                var title = new Label(graph.name);
                title.AddToClassList("story-outline__row-title");
                row.Add(title);
                var count = new Label(StoryText.T($"{hit.Count} pasos", $"{hit.Count} steps"));
                count.AddToClassList("story-mono");
                row.Add(count);
                _list.Add(row);
            }
        }

        void AddNote(string text)
        {
            var note = new Label(text);
            note.AddToClassList("story-outline__tip");
            _list.Add(note);
        }

        VisualElement Row(NarrativeNode node, string number, bool selected, bool onPath, bool anySelected)
        {
            var row = new Button(() => StepClicked?.Invoke(node));
            row.AddToClassList("story-outline__row");
            row.EnableInClassList("is-selected", selected);
            row.EnableInClassList("is-on-path", onPath && !selected);
            row.EnableInClassList("is-off-path", anySelected && !onPath && !selected);
            var num = new Label(number);
            num.AddToClassList("story-outline__row-number");
            row.Add(num);
            var name = string.IsNullOrWhiteSpace(node.displayTitle) ? NarrativeNodeCatalog.TitleOf(node) : node.displayTitle;
            var title = new Label(string.IsNullOrEmpty(node.Badge) ? name : node.Badge + " " + name) { tooltip = name };
            title.AddToClassList("story-outline__row-title");
            row.Add(title);
            return row;
        }
    }
}
