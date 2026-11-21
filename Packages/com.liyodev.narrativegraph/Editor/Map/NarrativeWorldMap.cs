using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor
{
    /// El mundo del juego en esquema: escenas, cada una con sus sitios (donde pueden estar los
    /// personajes u objetos) y los personajes que se quieren ver. Lo usa la ventana del mapa para
    /// colocar los muñecos según el paso elegido. Sin fotos: el mapa es el esquema.
    [CreateAssetMenu(menuName = "Narrative Graph/Mapa de la historia", fileName = "Mapa de la historia")]
    public sealed class NarrativeWorldMap : ScriptableObject
    {
        [Serializable]
        public sealed class Scene
        {
            public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            public string name = "Escena";
            [Tooltip("Orden de la banda en el mapa.")]
            public int order;
            public Color color = new(0.16f, 0.20f, 0.27f);
        }

        [Serializable]
        public sealed class Zone
        {
            public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            public string name = "Sitio";
            [Tooltip("Escena a la que pertenece este sitio.")]
            public string scene;
            public Color color = new(0.36f, 0.62f, 0.9f);
            [Tooltip("Marcas del mundo que caen en este sitio.")]
            public List<string> marks = new();
        }

        [Serializable]
        public sealed class Character
        {
            [Tooltip("Id del actor en el grafo ('Player' para el jugador).")]
            public string actorId;
            public string name;
            public Color color = new(0.85f, 0.35f, 0.19f);
            [Tooltip("Sitio donde está cuando la historia no dice otra cosa.")]
            public string homeZone;
            [Tooltip("Personaje principal: siempre a la vista.")]
            public bool main;
        }

        [Tooltip("Las escenas del juego, en orden. Cada sitio pertenece a una.")]
        public List<Scene> scenes = new();

        [Tooltip("Los sitios donde pueden estar los personajes u objetos, con las marcas que caen en cada uno.")]
        public List<Zone> zones = new();

        [Tooltip("Los personajes que el mapa puede enseñar, con su color y su sitio de siempre.")]
        public List<Character> characters = new();

        public Scene SceneOf(string id) => scenes.FirstOrDefault(s => s.id == id);
        public Zone ZoneOfMark(string mark) => string.IsNullOrEmpty(mark) ? null : zones.FirstOrDefault(z => z.marks.Contains(mark));
        public Zone ZoneById(string id) => zones.FirstOrDefault(z => z.id == id);
        public Character CharacterOf(string actorId) => characters.FirstOrDefault(c => c.actorId == actorId);

        /// El mapa del proyecto (el primero que haya), o null.
        public static NarrativeWorldMap Find()
            => AssetDatabase.FindAssets("t:" + nameof(NarrativeWorldMap))
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeWorldMap>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(m => m != null);

        /// Crea un mapa vacío junto a la ficha del jugador (o en Assets), con una escena general.
        public static NarrativeWorldMap Create()
        {
            var sheet = NarrativePlayerSheet.Find();
            var folder = sheet != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(sheet)).Replace('\\', '/') : "Assets";
            var map = CreateInstance<NarrativeWorldMap>();
            map.scenes.Add(new Scene { id = "general", name = Story.StoryText.T("General", "General") });
            map.characters.Add(new Character
            {
                actorId = "Player",
                name = Story.StoryText.T("Jugador", "Player"),
                color = new Color(0.22f, 0.54f, 0.87f),
                main = true,
            });
            AssetDatabase.CreateAsset(map, AssetDatabase.GenerateUniqueAssetPath($"{folder}/Mapa de la historia.asset"));
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
