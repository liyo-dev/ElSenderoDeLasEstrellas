using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// "Game connections": each system the story talks to (dialogue, quests, inventory, camera...)
    /// and which class of the game answers for it. Nothing to fill in here; it is a map.
    public sealed class StoryConnectionsWindow : EditorWindow
    {
        readonly struct Port
        {
            public readonly Type Type;
            public readonly string Name;
            public readonly string Purpose;
            public readonly bool Optional;
            public Port(Type type, string name, string purpose, bool optional = false) { Type = type; Name = name; Purpose = purpose; Optional = optional; }
        }

        static Port[] Ports => new[]
        {
            new Port(typeof(INarrativeDialoguePlayer), StoryText.T("Diálogos", "Dialogue"), StoryText.T("Enseña las conversaciones.", "Shows conversations.")),
            new Port(typeof(INarrativeChoicePresenter), StoryText.T("Respuestas", "Choices"), StoryText.T("Enseña las preguntas con respuestas.", "Shows questions with answers.")),
            new Port(typeof(IQuestService), StoryText.T("Misiones", "Quests"), StoryText.T("Empieza, avanza y cierra misiones.", "Starts, advances and completes quests.")),
            new Port(typeof(INarrativeInventory), StoryText.T("Inventario", "Inventory"), StoryText.T("Da, quita y cuenta objetos.", "Gives, takes and counts items.")),
            new Port(typeof(INarrativeFlagStore), StoryText.T("Datos de partida", "Story flags"), StoryText.T("Guarda los hechos de la historia.", "Saves story facts.")),
            new Port(typeof(INarrativeCounterStore), StoryText.T("Contadores", "Counters"), StoryText.T("Opcional: sin él, van con los datos de partida.", "Optional: without it they live with the story flags."), true),
            new Port(typeof(INarrativeSaveData), StoryText.T("Guardado", "Saving"), StoryText.T("Guarda por dónde va cada capítulo.", "Saves where each chapter is.")),
            new Port(typeof(INarrativeActorDirectory), StoryText.T("Personajes", "Characters"), StoryText.T("Encuentra a cada personaje por su id.", "Finds each character by id.")),
            new Port(typeof(INarrativeActorPlacement), StoryText.T("Colocar personajes", "Placing characters"), StoryText.T("Mueve personajes a una marca.", "Moves characters to a mark.")),
            new Port(typeof(INarrativeActorPresence), StoryText.T("Personajes en escena", "Characters on stage"), StoryText.T("Quita o pone personajes en escena.", "Shows or hides characters.")),
            new Port(typeof(INarrativeAttentionMarkers), StoryText.T("Marcadores", "Markers"), StoryText.T("El icono sobre quien tiene algo que decir.", "The marker above who has something to say.")),
            new Port(typeof(INarrativePlayerControl), StoryText.T("Jugador", "Player"), StoryText.T("Bloquea y suelta el control.", "Locks and releases control.")),
            new Port(typeof(INarrativeCameraFocus), StoryText.T("Cámara", "Camera"), StoryText.T("Enfoca a alguien o algo.", "Focuses on someone or something.")),
            new Port(typeof(INarrativeAudio), StoryText.T("Audio", "Audio"), StoryText.T("Música, efectos y voces.", "Music, sound and voices.")),
            new Port(typeof(INarrativeScreen), StoryText.T("Pantalla", "Screen"), StoryText.T("Fundidos y textos grandes.", "Fades and big texts.")),
            new Port(typeof(INarrativeSpeechBubbles), StoryText.T("Bocadillos", "Speech bubbles"), StoryText.T("Frases cortas sobre la cabeza.", "Short lines above heads.")),
            new Port(typeof(INarrativeTitleCardPresenter), StoryText.T("Rótulos", "Title cards"), StoryText.T("Rótulos de capítulo o lugar.", "Chapter or place cards.")),
            new Port(typeof(INarrativeSceneHooks), StoryText.T("Escenas", "Scenes"), StoryText.T("Carga escenas y avisa al cambiar.", "Loads scenes and reports changes.")),
            new Port(typeof(INarrativeTextLocalizer), StoryText.T("Traducción", "Localization"), StoryText.T("Traduce las claves de texto.", "Translates text keys.")),
        };

        public static void Open()
        {
            var w = GetWindow<StoryConnectionsWindow>(true, StoryText.T("Conexiones del juego", "Game connections"));
            w.minSize = new Vector2(560, 420);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/NarrativeGraph/Editor/Story/StoryEditor.uss");
            if (sheet != null) root.styleSheets.Add(sheet);
            root.AddToClassList("story-root");

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("story-panel__scroll");
            root.Add(scroll);

            var intro = new Label(StoryText.T(
                "Lo que hace la historia lo hace pidiéndoselo a tu juego. Aquí ves quién responde a cada cosa. Si algo sale sin conectar, los pasos que lo usan avisan y siguen sin colgarse.",
                "The story does things by asking your game. Here you see who answers each request. If something is not connected, the steps that use it warn and carry on without hanging."));
            intro.AddToClassList("story-muted");
            intro.style.marginBottom = 12;
            scroll.Add(intro);

            int missing = 0;
            foreach (var port in Ports)
            {
                var implementations = TypeCache.GetTypesDerivedFrom(port.Type)
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Select(t => t.Name).OrderBy(n => n).ToList();
                bool notConnected = implementations.Count == 0 && !port.Optional;
                if (notConnected) missing++;

                var row = new VisualElement();
                row.AddToClassList("story-panel__row");
                row.EnableInClassList("is-warning", notConnected);
                var name = new Label(port.Name) { tooltip = port.Purpose };
                name.AddToClassList("story-panel__row-name");
                row.Add(name);
                var value = new Label(implementations.Count > 0 ? string.Join(", ", implementations)
                                      : port.Optional ? StoryText.T("el básico", "built-in") : StoryText.T("sin conectar", "not connected"))
                {
                    tooltip = port.Type.Name,
                };
                value.AddToClassList("story-panel__row-value");
                row.Add(value);
                scroll.Add(row);
            }

            var summary = new Label(missing == 0
                ? StoryText.T("Todo conectado.", "Everything is connected.")
                : StoryText.T($"{missing} sin conectar.", $"{missing} not connected."));
            summary.AddToClassList("story-notice");
            summary.AddToClassList(missing == 0 ? "is-ok" : "is-warning");
            scroll.Insert(1, summary);
        }
    }
}
