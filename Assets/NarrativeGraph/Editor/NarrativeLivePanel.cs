using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Pestaña «En vivo»: lo que el juego está haciendo ahora mismo mientras se prueba el grafo.
    /// Lee el estado real (sin duplicarlo) del NarrativeGraphHub, del grupo, de las quests, de los
    /// flags y de los NPC de las escenas cargadas. Fuera de Play Mode explica que no hay estado
    /// real y remite a la pestaña Prueba.
    /// </summary>
    public sealed class NarrativeLivePanel : VisualElement
    {
        readonly ScrollView _scroll;
        readonly Label _headline;
        readonly Label _current;
        readonly Button _focusButton;
        readonly Label _player;
        readonly Label _loadout;
        readonly Label _notPlaying;
        readonly Button _projectButton;
        readonly VisualElement _projectedBox;
        readonly VisualElement _playingBox;
        readonly VisualElement _actorsBox;
        readonly VisualElement _questsBox;
        readonly VisualElement _flagsBox;
        readonly VisualElement _blackboardBox;
        readonly Foldout _actorsFold;
        readonly Foldout _questsFold;
        readonly Foldout _flagsFold;
        readonly Foldout _blackboardFold;

        NarrativeGraph _graph;
        NodeView _selected;
        string _lastSignature;
        double _nextRefresh;
        NarrativeGraph _projectedGraph;
        string _projectedNodeGuid;

        public string CurrentNodeGuid { get; private set; }
        public bool Active => style.display != DisplayStyle.None;

        /// <summary>Pide a la ventana centrar el nodo que está ejecutándose.</summary>
        public event Action<string> FocusNodeRequested;

        public NarrativeLivePanel()
        {
            AddToClassList("narrative-live");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("narrative-live__scroll");
            Add(_scroll);

            var title = new Label("Estado en vivo");
            title.AddToClassList("narrative-live__title");
            _scroll.Add(title);

            _notPlaying = new Label("Entra en Play para ver el estado real del juego. En el lienzo, el ▶ de " +
                                    "cada tarjeta lanza una prueba desde ese nodo con la configuración de la pestaña Prueba.");
            _notPlaying.AddToClassList("narrative-live__hint");
            _scroll.Add(_notPlaying);

            _projectButton = new Button(ProjectSelectedNode) { text = "Simular posiciones al llegar al nodo seleccionado" };
            _projectButton.AddToClassList("narrative-test__small-button");
            _scroll.Add(_projectButton);

            _projectedBox = new VisualElement();
            _projectedBox.AddToClassList("narrative-live__list");
            _scroll.Add(_projectedBox);

            _playingBox = new VisualElement();
            _playingBox.AddToClassList("narrative-live__playing");
            _scroll.Add(_playingBox);

            _headline = new Label();
            _headline.AddToClassList("narrative-live__headline");
            _playingBox.Add(_headline);

            _current = new Label();
            _current.AddToClassList("narrative-live__current");
            _playingBox.Add(_current);

            _focusButton = new Button(() =>
            {
                if (!string.IsNullOrEmpty(CurrentNodeGuid)) FocusNodeRequested?.Invoke(CurrentNodeGuid);
            }) { text = "Centrar nodo actual" };
            _focusButton.AddToClassList("narrative-test__small-button");
            _playingBox.Add(_focusButton);

            _player = new Label();
            _player.AddToClassList("narrative-live__line");
            _playingBox.Add(_player);

            _loadout = new Label();
            _loadout.AddToClassList("narrative-live__line");
            _playingBox.Add(_loadout);

            _actorsFold = Fold("NPCs en escena", out _actorsBox);
            _questsFold = Fold("Quests", out _questsBox);
            _flagsFold = Fold("Flags narrativos", out _flagsBox);
            _blackboardFold = Fold("Blackboard del grafo", out _blackboardBox);

            Refresh(_graph, _selected, true);
        }

        Foldout Fold(string text, out VisualElement content)
        {
            var fold = new Foldout { text = text, value = true };
            fold.AddToClassList("narrative-live__section");
            content = new VisualElement();
            content.AddToClassList("narrative-live__list");
            fold.Add(content);
            _scroll.Add(fold);
            return fold;
        }

        public void RefreshNow() => _lastSignature = null;

        /// <summary>
        /// Relee el estado real. Se llama desde la ventana cada pocos cientos de milisegundos; si
        /// nada cambió (misma firma) no reconstruye la UI, para no parpadear ni resetear el scroll.
        /// </summary>
        public void Refresh(NarrativeGraph graph, NodeView selected, bool force = false)
        {
            _graph = graph;
            _selected = selected;

            if (!EditorApplication.isPlaying)
            {
                // La simulación de posiciones es de un nodo concreto: si cambia el grafo o la
                // selección, el resultado anterior ya no corresponde y se limpia.
                string selectedGuid = selected?.Model?.guid;
                if (graph != _projectedGraph || selectedGuid != _projectedNodeGuid)
                {
                    _projectedBox.Clear();
                    _projectedGraph = graph;
                    _projectedNodeGuid = selectedGuid;
                }

                CurrentNodeGuid = null;
                _playingBox.style.display = DisplayStyle.None;
                _notPlaying.style.display = DisplayStyle.Flex;
                _projectButton.style.display = DisplayStyle.Flex;
                _projectedBox.style.display = DisplayStyle.Flex;
                _actorsFold.style.display = DisplayStyle.None;
                _questsFold.style.display = DisplayStyle.None;
                _flagsFold.style.display = DisplayStyle.None;
                _blackboardFold.style.display = DisplayStyle.None;
                return;
            }

            _projectButton.style.display = DisplayStyle.None;
            _projectedBox.style.display = DisplayStyle.None;

            if (!force && EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + 0.4;

            _playingBox.style.display = DisplayStyle.Flex;
            _notPlaying.style.display = DisplayStyle.None;
            _actorsFold.style.display = DisplayStyle.Flex;
            _questsFold.style.display = DisplayStyle.Flex;
            _flagsFold.style.display = DisplayStyle.Flex;
            _blackboardFold.style.display = DisplayStyle.Flex;

            var sb = new StringBuilder(2048);
            var runner = FindRunner(graph);
            var node = runner?.CurrentNode;
            CurrentNodeGuid = node?.guid;
            if (string.IsNullOrEmpty(CurrentNodeGuid) && runner?.Blackboard != null)
                CurrentNodeGuid = runner.Blackboard.Get<string>(NarrativeRunner.CurrentNodeKey, null);

            var scene = SceneManager.GetActiveScene();
            sb.Append("H|").Append(graph?.name).Append('|').Append(scene.name).Append('\n');
            sb.Append("C|").Append(CurrentNodeGuid).Append('|').Append(node?.GetType().Name).Append('|')
              .Append(node?.displayTitle).Append('\n');

            // Jugador
            var active = ControlDelJugador.Activo;
            var salud = SaludDelGrupo.Instance;
            if (salud != null)
            {
                sb.Append("P|").Append(active).Append('|');
                foreach (var slot in new[] { PartyControlManager.CharacterSlot.Will, PartyControlManager.CharacterSlot.Liam, PartyControlManager.CharacterSlot.Estela })
                {
                    if (!ControlDelJugador.EnElGrupo(slot)) continue;
                    sb.Append(slot).Append(':').Append(salud.Vida(slot).ToString("0")).Append('/')
                      .Append(salud.VidaMax(slot).ToString("0")).Append(';');
                    sb.Append(salud.Mana(slot).ToString("0")).Append('/').Append(salud.ManaMax(slot).ToString("0")).Append('|');
                }
                sb.Append('\n');
            }
            else
            {
                sb.Append("P|").Append(active).Append("|sin SaludDelGrupo\n");
            }

            var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
            if (preset != null)
            {
                sb.Append("L|");
                if (preset.abilities != null)
                {
                    var a = preset.abilities;
                    if (a.jump) sb.Append("saltar,");
                    if (a.swim) sb.Append("nadar,");
                    if (a.climb) sb.Append("trepar,");
                    if (a.fly) sb.Append("volar,");
                    if (a.sprint) sb.Append("sprint,");
                    if (a.shield) sb.Append("escudo,");
                    if (a.magic) sb.Append("magia,");
                }
                sb.Append('|').Append(string.Join(",", preset.basicSpellIds ?? new List<SpellId>()));
                sb.Append('|').Append(string.Join(",", preset.unlockedSpells ?? new List<SpellId>()));
                sb.Append('\n');
            }

            // NPCs
            var actors = UnityEngine.Object.FindObjectsByType<NarrativeActor>(FindObjectsInactive.Exclude)
                .Where(a => a != null)
                .Select(a =>
                {
                    var id = string.IsNullOrEmpty(a.ActorId) ? a.name : a.ActorId;
                    var p = a.transform.position;
                    var sceneName = a.gameObject.scene.name;
                    return $"{id}|{sceneName}|{p.x:0.0},{p.y:0.0},{p.z:0.0}";
                })
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var a in actors) sb.Append("A|").Append(a).Append('\n');

            // Quests
            var qm = QuestManager.Instance;
            var quests = new List<string>();
            if (qm != null)
            {
                foreach (var q in qm.GetAll())
                {
                    if (q == null) continue;
                    quests.Add($"{q.Data?.displayName ?? q.Id}|{q.State}|{q.Id}");
                }
            }
            quests.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var q in quests) sb.Append("Q|").Append(q).Append('\n');

            // Flags
            var flags = NarrativeFlags.All().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var f in flags) sb.Append("F|").Append(f).Append('\n');

            // Blackboard
            var bb = runner?.Blackboard?.ExportToSerializable() ?? new List<SimpleBlackboard.Entry>();
            bb.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));
            foreach (var e in bb) sb.Append("B|").Append(e.key).Append('=').Append(e.value).Append('\n');

            var signature = sb.ToString();
            if (!force && signature == _lastSignature) return;
            _lastSignature = signature;

            Apply(scene.name, node, active, salud, preset, actors, quests, flags, bb);
        }

        /// <summary>
        /// Fuera de Play también se puede saber dónde debería estar cada NPC al llegar al nodo:
        /// se proyecta el camino con el mismo Fast-Forward de Quick Test y se listan las marcas.
        /// </summary>
        void ProjectSelectedNode()
        {
            _projectedBox.Clear();
            if (_graph == null || _selected?.Model == null)
            {
                var hint = new Label("Selecciona un nodo en el lienzo para simular el estado al llegar a él.");
                hint.AddToClassList("narrative-live__muted");
                _projectedBox.Add(hint);
                return;
            }

            var preview = NarrativeTestLauncher.Preview(_graph, _selected.Model.guid, NarrativeTestConfig.Load());
            if (preview.actors.Count == 0 && preview.warnings.Count == 0)
            {
                var hint = new Label("No hay actores colocados en el camino hasta este nodo.");
                hint.AddToClassList("narrative-live__muted");
                _projectedBox.Add(hint);
                return;
            }

            foreach (var a in preview.actors)
            {
                var l = new Label("· " + a);
                l.AddToClassList("narrative-live__row");
                _projectedBox.Add(l);
            }
            foreach (var w in preview.warnings)
            {
                var l = new Label("⚠ " + w);
                l.AddToClassList("narrative-test__preview-line");
                l.AddToClassList("is-warning");
                _projectedBox.Add(l);
            }
        }

        static NarrativeRunner FindRunner(NarrativeGraph graph)
        {
            if (graph == null || NarrativeGraphHub.Instance == null) return null;
            return NarrativeGraphHub.Instance.GetAllRunners()?.FirstOrDefault(r => r != null && r.graph == graph);
        }

        void Apply(string sceneName, NarrativeNode node, PartyControlManager.CharacterSlot active,
            SaludDelGrupo salud, PlayerPresetSO preset, List<string> actors, List<string> quests,
            List<string> flags, List<SimpleBlackboard.Entry> bb)
        {
            _headline.text = $"EN PLAY · {(_graph != null ? _graph.name : "sin grafo")} · {sceneName}";
            var shown = node;
            bool branches = false;
            if (shown == null && _graph != null && !string.IsNullOrEmpty(CurrentNodeGuid))
            {
                shown = _graph.FindNode(CurrentNodeGuid);
                branches = shown != null;
            }
            _current.text = shown != null
                ? $"Nodo actual: {shown.displayTitle ?? NarrativeNodeCatalog.TitleOf(shown.GetType())}  ({shown.GetType().Name})" +
                  (branches ? "  ·  ramas en curso" : "")
                : "Sin nodo actual: el grafo no ha empezado o ya terminó.";
            _focusButton.SetEnabled(!string.IsNullOrEmpty(CurrentNodeGuid));

            _player.text = salud != null
                ? $"Al mando: {active}   ·   {PartyLine(salud, active)}"
                : $"Al mando: {active}   ·   (SaludDelGrupo no disponible)";

            if (preset != null)
            {
                var abilities = new List<string>();
                var a = preset.abilities ?? new PlayerAbilities();
                if (a.jump) abilities.Add("saltar");
                if (a.swim) abilities.Add("nadar");
                if (a.climb) abilities.Add("trepar");
                if (a.fly) abilities.Add("volar");
                if (a.sprint) abilities.Add("sprint");
                if (a.shield) abilities.Add("escudo");
                if (a.magic) abilities.Add("magia");
                var spells = preset.basicSpellIds ?? new List<SpellId>();
                var unlocked = preset.unlockedSpells ?? new List<SpellId>();
                _loadout.text = $"Habilidades: {(abilities.Count == 0 ? "ninguna" : string.Join(", ", abilities))}\n" +
                                $"Hechizos equipados: {(spells.Count == 0 ? "ninguno" : string.Join(", ", spells))}\n" +
                                $"Hechizos desbloqueados: {(unlocked.Count == 0 ? "ninguno" : string.Join(", ", unlocked))}";
            }
            else
            {
                _loadout.text = "Habilidades y hechizos: (sin preset activo)";
            }

            Fill(_actorsBox, actors, "No hay NPCs con NarrativeActor en las escenas cargadas.", s =>
            {
                var parts = s.Split('|');
                return $"{parts[0]}  ·  {parts[1]}  ·  ({parts[2]})";
            });

            Fill(_questsBox, quests, "No hay quests en curso.", s =>
            {
                var parts = s.Split('|');
                return $"{parts[0]}  [{parts[1]}]";
            });

            Fill(_flagsBox, flags, "No hay flags narrativos activos.", s => s);

            Fill(_blackboardBox, bb.Select(e => $"{e.key} = {e.value}").ToList(),
                "Blackboard vacío.", s => s);
        }

        static void Fill(VisualElement box, List<string> rows, string empty, Func<string, string> format)
        {
            box.Clear();
            if (rows.Count == 0)
            {
                var l = new Label(empty);
                l.AddToClassList("narrative-live__muted");
                box.Add(l);
                return;
            }
            foreach (var row in rows)
            {
                var l = new Label(format(row));
                l.AddToClassList("narrative-live__row");
                box.Add(l);
            }
        }

        static string PartyLine(SaludDelGrupo salud, PartyControlManager.CharacterSlot active)
        {
            var parts = new List<string>();
            foreach (var slot in new[] { PartyControlManager.CharacterSlot.Will, PartyControlManager.CharacterSlot.Liam, PartyControlManager.CharacterSlot.Estela })
            {
                if (!ControlDelJugador.EnElGrupo(slot)) continue;
                string mark = slot == active ? "▶ " : "";
                parts.Add($"{mark}{slot} {salud.Vida(slot):0}/{salud.VidaMax(slot):0} · maná {salud.Mana(slot):0}/{salud.ManaMax(slot):0}");
            }
            return string.Join("   ", parts);
        }
    }
}
