using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarrativeGraphEditor
{
    /// Dónde están en el mundo las marcas y los personajes, si el juego los guarda a su manera
    /// (si no, se buscan por nombre en las escenas abiertas).
    public interface INarrativeWorldPositions
    {
        bool TryMark(string markId, out Vector3 position);
        /// Dónde está un personaje cuando la historia no dice nada (su sitio en la escena).
        bool TryActorHome(string actorId, out Vector3 position);
    }

    /// Posiciones del mundo para el mapa de la historia: primero lo que diga el juego
    /// (INarrativeWorldPositions); si no, el objeto de las escenas abiertas que se llama como la
    /// marca o el personaje; si no, lo que el mapa recordó al hacer su vista (sirve sin la escena).
    public static class NarrativeWorldPositions
    {
        static Dictionary<string, Vector3> s_byName;
        static INarrativeWorldPositions s_game;
        static bool s_gameLooked;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.hierarchyChanged -= Forget;
            EditorApplication.hierarchyChanged += Forget;
            EditorSceneManager.sceneOpened -= OnScene;
            EditorSceneManager.sceneOpened += OnScene;
        }

        static void OnScene(Scene s, OpenSceneMode m) => Forget();
        static void Forget() { s_byName = null; s_map = null; }

        static NarrativeWorldMap s_map;
        static NarrativeWorldMap Map => s_map != null ? s_map : s_map = NarrativeWorldMap.Find();

        static INarrativeWorldPositions Game
        {
            get
            {
                if (!s_gameLooked) { s_game = NarrativeEditorExtensions.CreateFirst<INarrativeWorldPositions>(); s_gameLooked = true; }
                return s_game;
            }
        }

        public static bool Mark(string markId, out Vector3 position)
        {
            position = default;
            if (string.IsNullOrEmpty(markId)) return false;
            if (Game != null && Game.TryMark(markId, out position)) return true;
            return ByName(markId, out position) || Remembered(Map?.markSpots, markId, out position);
        }

        public static bool Home(string actorId, out Vector3 position)
        {
            position = default;
            if (string.IsNullOrEmpty(actorId)) return false;
            if (Game != null && Game.TryActorHome(actorId, out position)) return true;
            return ByName(actorId, out position) || Remembered(Map?.actorSpots, actorId, out position);
        }

        static bool Remembered(List<NarrativeWorldMap.Spot> spots, string id, out Vector3 position)
        {
            position = default;
            var spot = spots?.Find(x => x.id == id);
            if (spot == null) return false;
            position = spot.position;
            return true;
        }

        /// Recuerda en el mapa dónde están ahora las marcas del proyecto y sus personajes.
        public static void Remember(NarrativeWorldMap map)
        {
            if (map == null) return;
            Forget();
            var marks = new List<NarrativeWorldMap.Spot>();
            foreach (var id in NarrativeProjectIndex.Anchors)
                if ((Game != null && Game.TryMark(id, out var p)) || ByName(id, out p)) marks.Add(new NarrativeWorldMap.Spot { id = id, position = p });
            var actors = new List<NarrativeWorldMap.Spot>();
            foreach (var c in map.characters)
                if ((Game != null && Game.TryActorHome(c.actorId, out var p)) || ByName(c.actorId, out p)) actors.Add(new NarrativeWorldMap.Spot { id = c.actorId, position = p });
            if (marks.Count > 0) map.markSpots = marks;
            if (actors.Count > 0) map.actorSpots = actors;
            EditorUtility.SetDirty(map);
        }

        static bool ByName(string name, out Vector3 position)
        {
            if (s_byName == null)
            {
                s_byName = new Dictionary<string, Vector3>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            if (!s_byName.ContainsKey(t.name)) s_byName[t.name] = t.position;
                }
            }
            return s_byName.TryGetValue(name, out position);
        }
    }
}
