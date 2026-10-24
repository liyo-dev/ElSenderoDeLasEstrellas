using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Pestaña «Prueba» del editor del grafo: la misma configuración de Quick Test (cuerpo del
    /// jugador, spawn anchor, Fast-Forward) sin abrir otra ventana, con un botón para lanzar la
    /// prueba desde el nodo seleccionado y otro para pararla.
    /// </summary>
    public sealed class NarrativeTestPanel : VisualElement
    {
        NarrativeGraph _graph;
        string _targetGuid;
        readonly NarrativeTestConfig _config;

        readonly Label _targetLabel;
        readonly Label _statusLabel;
        readonly Button _playButton;
        readonly Button _restoreButton;
        readonly VisualElement _previewBox;
        readonly ScrollView _scroll;
        readonly TextField _graphLabelField;
        readonly TextField _spawnField;
        readonly DropdownField _anchorDropdown;
        readonly Label _labelWarning;
        readonly Toggle _fastForwardToggle;
        readonly Toggle _autoRestoreToggle;
        readonly List<EnumField> _basicSpellFields = new();
        EnumField _comboSpellField;

        public NarrativeTestPanel()
        {
            _config = NarrativeTestConfig.Load();
            AddToClassList("narrative-test");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("narrative-test__scroll");
            Add(_scroll);

            var title = new Label("Prueba desde el nodo");
            title.AddToClassList("narrative-test__title");
            _scroll.Add(title);

            _targetLabel = new Label("Selecciona un nodo en el lienzo.");
            _targetLabel.AddToClassList("narrative-test__target");
            _scroll.Add(_targetLabel);

            // ── Dónde apareces ──────────────────────────────────────────
            var where = Section("Dónde apareces");
            _graphLabelField = new TextField("Etiqueta del grafo") { isDelayed = true, value = _config.graphLabel ?? "" };
            _graphLabelField.tooltip = "Etiqueta con la que el NarrativeGraphHub de Start.unity registra este grafo.";
            _graphLabelField.RegisterValueChangedCallback(evt =>
            {
                _config.graphLabel = evt.newValue;
                _config.Save();
                _labelWarning.style.display = DisplayStyle.None;
            });
            where.Add(_graphLabelField);

            _labelWarning = new Label("No se encontró este grafo en el Hub de Start.unity. Revisa la etiqueta antes de lanzar.");
            _labelWarning.AddToClassList("narrative-test__warning");
            _labelWarning.style.display = DisplayStyle.None;
            where.Add(_labelWarning);

            var detectLabel = new Button(() =>
            {
                NarrativeTestLauncher.InvalidateLabelCache();
                var label = NarrativeTestLauncher.DetectGraphLabel(_graph);
                if (string.IsNullOrEmpty(label))
                {
                    SetStatus("No se encontró este grafo en el Hub de Start.unity.");
                    return;
                }
                _config.graphLabel = label;
                _config.Save();
                _graphLabelField.SetValueWithoutNotify(label);
                SetStatus($"Etiqueta detectada: {label}");
            }) { text = "Detectar en el Hub" };
            detectLabel.AddToClassList("narrative-test__small-button");
            where.Add(detectLabel);

            _spawnField = new TextField("Spawn Anchor") { isDelayed = true, value = _config.spawnAnchorId ?? "Bedroom" };
            _spawnField.tooltip = "Dónde aparece Will físicamente, independientemente del nodo del grafo.";
            _spawnField.RegisterValueChangedCallback(evt => { _config.spawnAnchorId = evt.newValue; _config.Save(); });
            where.Add(_spawnField);

            _anchorDropdown = new DropdownField("Anchors en escenas abiertas");
            _anchorDropdown.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue)) return;
                _config.spawnAnchorId = evt.newValue;
                _config.Save();
                _spawnField.SetValueWithoutNotify(evt.newValue);
            });
            where.Add(_anchorDropdown);

            var detectAnchors = new Button(() => RefreshAnchors(true)) { text = "Detectar en escenas abiertas" };
            detectAnchors.AddToClassList("narrative-test__small-button");
            where.Add(detectAnchors);

            // ── Cuerpo del jugador ──────────────────────────────────────
            var body = Section("Cuerpo del jugador");
            var level = new IntegerField("Nivel") { value = _config.level };
            level.RegisterValueChangedCallback(evt => { _config.level = evt.newValue; _config.Save(); });
            body.Add(level);
            var hp = new FloatField("Vida máxima") { value = _config.maxHP };
            hp.RegisterValueChangedCallback(evt => { _config.maxHP = evt.newValue; _config.Save(); });
            body.Add(hp);
            var mp = new FloatField("Maná máximo") { value = _config.maxMP };
            mp.RegisterValueChangedCallback(evt => { _config.maxMP = evt.newValue; _config.Save(); });
            body.Add(mp);

            body.Add(new Label("Habilidades") { name = "abilities-title" });
            var abilities = new VisualElement();
            abilities.AddToClassList("narrative-test__grid");
            abilities.Add(AbilityToggle("Saltar", _config.abilityJump, v => _config.abilityJump = v));
            abilities.Add(AbilityToggle("Nadar", _config.abilitySwim, v => _config.abilitySwim = v));
            abilities.Add(AbilityToggle("Trepar", _config.abilityClimb, v => _config.abilityClimb = v));
            abilities.Add(AbilityToggle("Volar", _config.abilityFly, v => _config.abilityFly = v));
            abilities.Add(AbilityToggle("Sprint", _config.abilitySprint, v => _config.abilitySprint = v));
            abilities.Add(AbilityToggle("Escudo", _config.abilityShield, v => _config.abilityShield = v));
            abilities.Add(AbilityToggle("Magia (casts)", _config.abilityMagic, v => _config.abilityMagic = v));
            body.Add(abilities);

            body.Add(new Label("Hechizos equipados") { name = "spells-title" });
            _config.EnsureSpellSlots();
            for (int i = 0; i < MagicCaster.BasicSlotCount; i++)
            {
                int slot = i;
                var field = new EnumField($"Básico {i + 1}", _config.basicSpellIds[slot]);
                field.RegisterValueChangedCallback(evt =>
                {
                    _config.basicSpellIds[slot] = (SpellId)evt.newValue;
                    _config.Save();
                });
                _basicSpellFields.Add(field);
                body.Add(field);
            }
            _comboSpellField = new EnumField("Combo (Y)", _config.comboSpellId);
            _comboSpellField.RegisterValueChangedCallback(evt =>
            {
                _config.comboSpellId = (SpellId)evt.newValue;
                _config.Save();
            });
            body.Add(_comboSpellField);

            // ── Fast-Forward ────────────────────────────────────────────
            var forward = Section("Progreso previo (Fast-Forward)");
            var help = new Label("Simula el camino desde el inicio del grafo hasta el nodo: quests, objetos, " +
                                 "flags, habilidades y dónde queda cada actor. Si el camino pasa por una decisión " +
                                 "(flag, quest, pregunta...) se para y te lo avisa en vez de adivinar.");
            help.AddToClassList("narrative-test__hint");
            forward.Add(help);

            _fastForwardToggle = new Toggle("Autocompletar progreso previo") { value = _config.fastForward };
            _fastForwardToggle.RegisterValueChangedCallback(evt => { _config.fastForward = evt.newValue; _config.Save(); });
            forward.Add(_fastForwardToggle);

            var previewBtn = new Button(Preview) { text = "Vista previa (sin lanzar Play)" };
            previewBtn.AddToClassList("narrative-test__small-button");
            forward.Add(previewBtn);

            _previewBox = new VisualElement();
            _previewBox.AddToClassList("narrative-test__preview");
            forward.Add(_previewBox);

            // ── Al salir ────────────────────────────────────────────────
            var exit = Section("Al salir");
            _autoRestoreToggle = new Toggle("Restaurar boot preset original") { value = _config.autoRestore };
            _autoRestoreToggle.RegisterValueChangedCallback(evt => { _config.autoRestore = evt.newValue; _config.Save(); });
            exit.Add(_autoRestoreToggle);

            // ── Lanzar ──────────────────────────────────────────────────
            var actions = new VisualElement();
            actions.AddToClassList("narrative-test__actions");
            _playButton = new Button(TogglePlay) { text = "▶ Probar desde el nodo" };
            _playButton.AddToClassList("narrative-test__play");
            actions.Add(_playButton);

            _restoreButton = new Button(() =>
            {
                NarrativeTestLauncher.RestoreOriginalBootPreset();
                RefreshPlayButton();
            }) { text = "Restaurar boot preset original" };
            _restoreButton.AddToClassList("narrative-test__small-button");
            _restoreButton.style.display = DisplayStyle.None;
            actions.Add(_restoreButton);
            _scroll.Add(actions);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("narrative-test__status");
            _scroll.Add(_statusLabel);

            RefreshAnchors(false);
            RefreshPlayButton();
            schedule.Execute(RefreshPlayButton).Every(250);
        }

        // ── API que usa NarrativeGraphWindow ────────────────────────────

        public void SetGraph(NarrativeGraph graph)
        {
            _graph = graph;
            _targetGuid = null;
            _targetLabel.text = "Selecciona un nodo en el lienzo.";

            var label = NarrativeTestLauncher.DetectGraphLabel(graph);
            if (!string.IsNullOrEmpty(label))
            {
                if (label != _config.graphLabel)
                {
                    _config.graphLabel = label;
                    _config.Save();
                    _graphLabelField.SetValueWithoutNotify(label);
                }
                _labelWarning.style.display = DisplayStyle.None;
            }
            else
            {
                _labelWarning.style.display = graph != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshAnchors(false);
            RefreshPlayButton();
        }

        public void SetTarget(NodeView view)
        {
            _targetGuid = view?.Model?.guid;
            if (view?.Model == null)
            {
                _targetLabel.text = "Selecciona un nodo en el lienzo.";
                RefreshPlayButton();
                return;
            }
            var title = string.IsNullOrEmpty(view.Model.displayTitle)
                ? NarrativeNodeCatalog.TitleOf(view.Model.GetType())
                : view.Model.displayTitle;
            _targetLabel.text = $"Nodo: {title}";
            RefreshPlayButton();
        }

        /// <summary>Lanza una prueba desde el nodo indicado (o para la que esté en curso).</summary>
        public void PlayFrom(string nodeGuid)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                SetStatus("Parando la prueba...");
                return;
            }

            if (_graph == null)
            {
                SetStatus("Abre un grafo antes de lanzar la prueba.");
                return;
            }
            if (string.IsNullOrEmpty(nodeGuid))
            {
                SetStatus("Selecciona un nodo (o usa el ▶ de su tarjeta) para lanzar la prueba.");
                return;
            }

            _targetGuid = nodeGuid;
            var cfg = _config;
            if (!NarrativeTestLauncher.Launch(_graph, nodeGuid, cfg, out var status, out var preview))
            {
                SetStatus(status);
                return;
            }
            ApplyPreview(preview);
            SetStatus(status);
            RefreshPlayButton();
        }

        // ── Interno ─────────────────────────────────────────────────────

        VisualElement Section(string title)
        {
            var fold = new Foldout { text = title, value = true };
            fold.AddToClassList("narrative-test__section");
            _scroll.Add(fold);
            return fold;
        }

        Toggle AbilityToggle(string label, bool value, Action<bool> setter)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.AddToClassList("narrative-test__ability");
            toggle.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                _config.Save();
            });
            return toggle;
        }

        void RefreshAnchors(bool announce)
        {
            var anchors = NarrativeTestLauncher.DetectSpawnAnchors();
            var choices = new List<string>(anchors);
            if (!string.IsNullOrEmpty(_config.spawnAnchorId) && !choices.Contains(_config.spawnAnchorId))
                choices.Insert(0, _config.spawnAnchorId);
            if (choices.Count == 0) choices.Add(string.Empty);

            _anchorDropdown.choices = choices;
            int idx = choices.IndexOf(_config.spawnAnchorId);
            _anchorDropdown.SetValueWithoutNotify(idx >= 0 ? choices[idx] : choices[0]);

            if (announce)
            {
                SetStatus(anchors.Length == 0
                    ? "No hay SpawnAnchor en las escenas abiertas. Abre la escena del nodo primero."
                    : $"{anchors.Length} anchor(s) detectado(s).");
            }
        }

        void TogglePlay()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                SetStatus("Parando la prueba...");
                return;
            }
            PlayFrom(_targetGuid);
        }

        void Preview()
        {
            if (_graph == null || string.IsNullOrEmpty(_targetGuid))
            {
                SetStatus("Selecciona un nodo antes de previsualizar.");
                return;
            }
            ApplyPreview(NarrativeTestLauncher.Preview(_graph, _targetGuid, _config));
            SetStatus("Vista previa calculada.");
        }

        void ApplyPreview(NarrativeFastForwardPreview preview)
        {
            _previewBox.Clear();
            if (preview == null) return;
            if (!preview.ran)
            {
                AddPreviewLine("Fast-Forward desactivado.", "is-muted");
                return;
            }

            if (preview.warnings.Count == 0 && preview.reachedTarget)
                AddPreviewLine("Camino simulado sin bifurcaciones.", "is-ok");
            if (!preview.reachedTarget)
                AddPreviewLine("No se pudo llegar al nodo automáticamente: el camino tiene una bifurcación.", "is-error");
            foreach (var w in preview.warnings.Take(6))
                AddPreviewLine("⚠ " + w, "is-warning");
            foreach (var a in preview.actors.Take(6))
                AddPreviewLine("· " + a, "is-muted");
            foreach (var n in preview.notes.Take(4))
                AddPreviewLine("· " + n, "is-muted");
        }

        void AddPreviewLine(string text, string modifier)
        {
            var label = new Label(text);
            label.AddToClassList("narrative-test__preview-line");
            label.AddToClassList(modifier);
            _previewBox.Add(label);
        }

        void RefreshPlayButton()
        {
            bool playing = EditorApplication.isPlaying;
            _playButton.text = playing ? "■ Parar prueba" : "▶ Probar desde el nodo";
            _playButton.EnableInClassList("is-playing", playing);
            _playButton.SetEnabled(playing || (!string.IsNullOrEmpty(_targetGuid) && _graph != null));
            if (_restoreButton != null)
            {
                _restoreButton.style.display = NarrativeTestLauncher.NeedsRestore
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        void SetStatus(string text)
        {
            _statusLabel.text = text ?? "";
        }
    }
}
