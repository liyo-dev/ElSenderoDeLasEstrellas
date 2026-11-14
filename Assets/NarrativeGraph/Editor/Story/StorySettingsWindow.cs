using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Settings and tools of the story editor, in one place (the ⚙ button).
    public sealed class StorySettingsWindow : EditorWindow
    {
        Func<NarrativeGraph> _graph;
        Action _languageChanged;

        public static void Open(Func<NarrativeGraph> graph, Action languageChanged)
        {
            var w = GetWindow<StorySettingsWindow>(true, StoryText.T("Ajustes y herramientas", "Settings and tools"));
            w.minSize = new Vector2(460, 560);
            w._graph = graph;
            w._languageChanged = languageChanged;
            w.Build();
        }

        void CreateGUI() => Build();

        void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/NarrativeGraph/Editor/Story/StoryEditor.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.AddToClassList("story-root");

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("story-panel__scroll");
            root.Add(scroll);

            // Help
            var help = Section(scroll, StoryText.T("Ayuda", "Help"));
            Tool(help, StoryText.T("Ver el recorrido del editor", "Take the editor tour"),
                StoryText.T("Un minuto por cada parte del editor.", "A minute through each part of the editor."),
                () => { Close(); StoryGraphWindow.ShowTour(); });
            Tool(help, StoryText.T("Abrir el manual de usuario", "Open the user manual"),
                StoryText.T("El manual completo en PDF.", "The full manual as a PDF."),
                () => StoryTour.OpenManual());

            // Tools
            var tools = Section(scroll, StoryText.T("Herramientas", "Tools"));
            var graph = _graph?.Invoke();
            Tool(tools, StoryText.T("Conexiones del juego", "Game connections"),
                StoryText.T("Qué parte de tu juego responde a cada cosa que pide la historia.", "Which part of your game answers each request of the story."),
                StoryConnectionsWindow.Open);
            Tool(tools, StoryText.T("Exportar textos para traducir y doblar", "Export texts for translation and voice"),
                StoryText.T("Diálogos y textos del capítulo, en orden, en una hoja de cálculo.", "The chapter's lines and texts, in order, in a spreadsheet."),
                () => StoryScriptExport.ExportWithDialog(_graph?.Invoke()), graph != null);
            Tool(tools, StoryText.T("Copiar el capítulo como texto", "Copy the chapter as text"),
                StoryText.T("Para leerlo, revisarlo o pegárselo a otra herramienta.", "To read it, review it or paste it into another tool."),
                () => EditorGUIUtility.systemCopyBuffer = NarrativeGraphText.Chapter(_graph?.Invoke()), graph != null);
            Tool(tools, StoryText.T("Guardar el manual de pasos", "Save the step manual"),
                StoryText.T("Todos los tipos de paso con sus campos, en STEP_TYPES.md.", "Every step type with its fields, in STEP_TYPES.md."),
                () => EditorApplication.ExecuteMenuItem("Tools/Narrative Graph/Guardar el manual de pasos"));

            // Play from a step
            var play = Section(scroll, StoryText.T("Probar desde un paso", "Play from a step"));
            var launcher = NarrativePlayLaunchers.Current;
            Hint(play, launcher is BasicPlayLauncher
                ? StoryText.T("Arranque básico: entra en Play, pone la historia anterior al paso a través de las conexiones del juego y empieza el grafo en ese paso. Si tu juego tiene su propio arranque (partidas, pantallas de carga…), puede sustituirlo implementando INarrativePlayLauncher.",
                              "Basic start: enters Play, applies the story before the step through the game connections and starts the graph at that step. If your game has its own boot (save slots, loading screens...), it can replace it by implementing INarrativePlayLauncher.")
                : StoryText.T($"Lo hace el arranque de tu juego: {launcher.Name}.", $"Your game's own start does it: {launcher.Name}."));
            if (launcher is BasicPlayLauncher)
            {
                var scene = new UnityEditor.UIElements.ObjectField(StoryText.T("Escena de arranque", "Start scene"))
                {
                    objectType = typeof(SceneAsset),
                    value = AssetDatabase.LoadAssetAtPath<SceneAsset>(BasicPlayLauncher.StartScene),
                    tooltip = StoryText.T("Vacío = la escena que tengas abierta.", "Empty = the scene you have open."),
                };
                scene.RegisterValueChangedCallback(e => BasicPlayLauncher.StartScene = e.newValue != null ? AssetDatabase.GetAssetPath(e.newValue) : string.Empty);
                play.Add(scene);
            }

            // Player sheet
            BuildPlayerSheet(Section(scroll, StoryText.T("Ficha del jugador", "Player sheet")));

            // Language
            var language = Section(scroll, StoryText.T("Idioma del editor", "Editor language"));
            var lang = new DropdownField(new[] { "Español", "English" }.ToList(), StoryText.English ? 1 : 0);
            lang.RegisterValueChangedCallback(e =>
            {
                StoryText.English = lang.index == 1;
                _languageChanged?.Invoke();
                Build();
            });
            language.Add(lang);

            // Assistant
            var assistant = Section(scroll, StoryText.T("Asistente", "Assistant"));
            Hint(assistant, StoryText.T(
                "Un chat dentro del editor que lee el capítulo y te propone cambios. Cada uno usa su propio servicio y su clave; se guardan solo en este ordenador.",
                "A chat inside the editor that reads the chapter and proposes changes. Everyone uses their own service and key, stored only on this computer."));
            var visible = new Toggle(StoryText.T("Mostrar el botón del asistente", "Show the assistant button")) { value = StoryAssistantSettings.Visible };
            visible.RegisterValueChangedCallback(e => StoryAssistantSettings.Visible = e.newValue);
            assistant.Add(visible);

            var providers = Enum.GetValues(typeof(StoryAssistantSettings.Provider)).Cast<StoryAssistantSettings.Provider>().ToList();
            var provider = new DropdownField(StoryText.T("Servicio", "Service"), providers.Select(StoryAssistantSettings.Name).ToList(), providers.IndexOf(StoryAssistantSettings.Current));
            assistant.Add(provider);
            var key = new TextField(StoryText.T("Clave de API", "API key")) { isPasswordField = true };
            var model = new TextField(StoryText.T("Modelo", "Model"));
            var address = new TextField(StoryText.T("Dirección", "Address"));
            var addressHint = new Label(StoryText.T("Cualquier servicio con el formato de OpenAI. Ollama en tu ordenador: http://localhost:11434/v1 (sin clave).",
                                                    "Any service with the OpenAI format. Ollama on your computer: http://localhost:11434/v1 (no key)."));
            addressHint.AddToClassList("story-muted");
            assistant.Add(key);
            assistant.Add(model);
            assistant.Add(address);
            assistant.Add(addressHint);

            void Fill()
            {
                var p = StoryAssistantSettings.Current;
                key.SetValueWithoutNotify(StoryAssistantSettings.Key(p));
                model.SetValueWithoutNotify(StoryAssistantSettings.Model(p));
                address.SetValueWithoutNotify(StoryAssistantSettings.Address);
                bool compatible = p == StoryAssistantSettings.Provider.Compatible;
                address.style.display = compatible ? DisplayStyle.Flex : DisplayStyle.None;
                addressHint.style.display = compatible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Fill();
            provider.RegisterValueChangedCallback(_ => { StoryAssistantSettings.Current = providers[provider.index]; Fill(); });
            key.RegisterCallback<FocusOutEvent>(_ => StoryAssistantSettings.SetKey(StoryAssistantSettings.Current, key.value));
            model.RegisterCallback<FocusOutEvent>(_ => StoryAssistantSettings.SetModel(StoryAssistantSettings.Current, model.value));
            address.RegisterCallback<FocusOutEvent>(_ => StoryAssistantSettings.Address = address.value);

            // Outside tools
            var outside = Section(scroll, StoryText.T("Herramientas externas", "Outside tools"));
            Hint(outside, StoryText.T(
                $"Deja que asistentes como Claude Desktop o Claude Code, u otros programas, lean y cambien los grafos mientras Unity está abierto (servidor MCP en Assets/NarrativeGraph/Tools/mcp). Solo se escucha en este ordenador, puerto {StoryBridge.Port}.",
                $"Lets assistants such as Claude Desktop or Claude Code, or other programs, read and change graphs while Unity is open (MCP server in Assets/NarrativeGraph/Tools/mcp). It only listens on this computer, port {StoryBridge.Port}."));
            var bridge = new Toggle(StoryText.T("Permitir herramientas externas", "Allow outside tools")) { value = StoryBridge.Enabled };
            bridge.RegisterValueChangedCallback(e => StoryBridge.Enabled = e.newValue);
            outside.Add(bridge);
            var readme = new Button(() => EditorUtility.RevealInFinder(System.IO.Path.GetFullPath("Assets/NarrativeGraph/Tools/mcp/README.md")))
                { text = StoryText.T("Ver cómo conectarlo", "How to connect it") };
            readme.AddToClassList("story-button");
            readme.style.alignSelf = Align.FlexStart;
            readme.style.marginLeft = 0;
            outside.Add(readme);
        }

        /// Qué datos del jugador puede fijar el grafo: la plantilla de partida nueva y los campos.
        void BuildPlayerSheet(VisualElement box)
        {
            Hint(box, StoryText.T(
                "Los datos del jugador al empezar una partida nueva (un ScriptableObject, o un prefab con el componente que los guarda) y qué campos pueden fijar los puntos de arranque (◆) y retocarse al probar.",
                "The player's data when starting a new game (a ScriptableObject, or a prefab with the component that holds it) and which fields start points (◆) can set and tests can tweak."));
            var sheet = NarrativePlayerSheet.Find();
            if (sheet == null)
            {
                var create = new Button(() =>
                {
                    var path = EditorUtility.SaveFilePanelInProject(StoryText.T("Ficha del jugador", "Player sheet"), "PlayerSheet", "asset", string.Empty, "Assets");
                    if (string.IsNullOrEmpty(path)) return;
                    AssetDatabase.CreateAsset(CreateInstance<NarrativePlayerSheet>(), path);
                    AssetDatabase.SaveAssets();
                    Build();
                }) { text = StoryText.T("Crear la ficha del jugador", "Create the player sheet") };
                create.AddToClassList("story-button");
                create.style.alignSelf = Align.FlexStart;
                create.style.marginLeft = 0;
                box.Add(create);
                return;
            }

            void Change(Action<NarrativePlayerSheet> change, bool rebuild)
            {
                Undo.RecordObject(sheet, StoryText.T("Ficha del jugador", "Player sheet"));
                change(sheet);
                EditorUtility.SetDirty(sheet);
                AssetDatabase.SaveAssetIfDirty(sheet);
                if (rebuild) Build();
            }

            var asset = new UnityEditor.UIElements.ObjectField(StoryText.T("Ficha", "Sheet")) { objectType = typeof(NarrativePlayerSheet), value = sheet };
            asset.SetEnabled(false);
            box.Add(asset);

            var template = new UnityEditor.UIElements.ObjectField(StoryText.T("Partida nueva", "New game"))
            {
                objectType = typeof(UnityEngine.Object), allowSceneObjects = false, value = sheet.template,
                tooltip = StoryText.T("Un ScriptableObject con los datos del jugador, o un prefab con el componente que los guarda.", "A ScriptableObject with the player data, or a prefab with the component that holds it."),
            };
            template.RegisterValueChangedCallback(e => Change(s => { s.template = e.newValue is ScriptableObject || e.newValue is GameObject ? e.newValue : null; }, true));
            box.Add(template);

            if (sheet.template is GameObject prefab)
            {
                var types = prefab.GetComponents<Component>().Where(c => c != null && c is not Transform).Select(c => c.GetType().FullName).Distinct().ToList();
                var component = new DropdownField(StoryText.T("Componente", "Component"), types, Math.Max(0, types.IndexOf(sheet.componentType)));
                component.RegisterValueChangedCallback(e => Change(s => s.componentType = e.newValue, true));
                if (string.IsNullOrEmpty(sheet.componentType) && types.Count > 0) Change(s => s.componentType = types[0], false);
                box.Add(component);
            }

            var available = sheet.AvailableFields();
            if (available.Count == 0) return;

            var positions = new List<string> { StoryText.T("(ninguno)", "(none)") };
            positions.AddRange(available.Select(f => f.path));
            var position = new DropdownField(StoryText.T("Dónde aparece", "Where it appears"), positions, Math.Max(0, positions.IndexOf(sheet.positionField)))
            {
                tooltip = StoryText.T("El campo que dice dónde aparece el jugador. Con él, el editor avisa si la historia cambia de sitio sin un punto que lo diga.",
                                      "The field that says where the player appears. With it, the editor warns when the story moves somewhere without a point saying so."),
            };
            position.RegisterValueChangedCallback(e => Change(s => s.positionField = position.index <= 0 ? string.Empty : e.newValue, false));
            box.Add(position);

            var list = new Label(StoryText.T("Campos que se pueden fijar y retocar:", "Fields that can be set and tweaked:"));
            list.AddToClassList("story-muted");
            list.style.marginTop = 8;
            box.Add(list);
            foreach (var (path, label) in available)
            {
                var field = path;
                var toggle = new Toggle(label) { value = sheet.fields.Contains(field), tooltip = field };
                toggle.RegisterValueChangedCallback(e => Change(s =>
                {
                    s.fields.Remove(field);
                    if (e.newValue)
                    {
                        // Se guardan en el orden de la plantilla.
                        s.fields.Add(field);
                        var order = available.Select(f => f.path).ToList();
                        s.fields.Sort((x, y) => order.IndexOf(x).CompareTo(order.IndexOf(y)));
                    }
                }, false));
                box.Add(toggle);
            }
        }

        static VisualElement Section(VisualElement parent, string title)
        {
            var box = new VisualElement();
            box.AddToClassList("story-panel__section");
            box.style.marginBottom = 20;
            var k = new Label(title.ToUpperInvariant());
            k.AddToClassList("story-kicker");
            box.Add(k);
            parent.Add(box);
            return box;
        }

        static void Hint(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("story-muted");
            l.style.marginBottom = 8;
            parent.Add(l);
        }

        static void Tool(VisualElement parent, string title, string description, Action action, bool enabled = true)
        {
            var b = new Button(action);
            b.AddToClassList("story-settings__tool");
            b.SetEnabled(enabled);
            var t = new Label(title);
            t.AddToClassList("story-settings__tool-title");
            b.Add(t);
            var d = new Label(description);
            d.AddToClassList("story-muted");
            b.Add(d);
            parent.Add(b);
        }
    }
}
