using System;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// El mapa de la historia en su propia ventana (lo que pasa al sacarlo del editor con ⧉).
    /// Al cerrarla, el mapa vuelve a su sitio dentro del editor.
    public sealed class StoryMapWindow : EditorWindow
    {
        // Para seguir en el mismo paso tras recompilar.
        [SerializeField] NarrativeGraph _graph;
        [SerializeField] string _stepGuid;

        /// Se ha abierto o cerrado la ventana (el editor esconde o enseña su mapa de abajo).
        public static event Action<bool> OpenChanged;
        public static bool IsOpen => Resources.FindObjectsOfTypeAll<StoryMapWindow>().Length > 0;

        [MenuItem("Window/Narrative Graph/Mapa de la historia")]
        public static StoryMapWindow Open()
        {
            var w = Resources.FindObjectsOfTypeAll<StoryMapWindow>();
            if (w.Length > 0) { w[0].Focus(); return w[0]; }
            var win = CreateInstance<StoryMapWindow>();
            win.titleContent = new GUIContent(StoryText.T("Mapa de la historia", "Story map"));
            win.minSize = new Vector2(760, 420);
            // Centrada en la ventana principal de Unity: así nunca queda fuera de la pantalla.
            var main = EditorGUIUtility.GetMainWindowPosition();
            var size = new Vector2(Mathf.Min(1280, main.width * 0.8f), Mathf.Min(820, main.height * 0.8f));
            win.position = new Rect(main.center - size / 2f, size);
            win.Show();
            return win;
        }

        void CreateGUI()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(NarrativeGraphPaths.Root + "/Editor/Story/StoryEditor.uss");
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            var view = new StoryMapView(false, _graph, _stepGuid);
            view.StepChanged += (g, s) => { _graph = g; _stepGuid = s; };
            rootVisualElement.Add(view);
            OpenChanged?.Invoke(true);
        }

        void OnDestroy() => OpenChanged?.Invoke(false);
    }
}
