using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// El mundo del juego en esquema: zonas con formas sencillas (cada una agrupa marcas del
    /// mundo) y los personajes que se quieren ver en él, con su color y su zona de siempre. Lo usa
    /// la ventana del mapa de la historia para colocar los muñecos según el paso elegido.
    [CreateAssetMenu(menuName = "Narrative Graph/Mapa de la historia", fileName = "Mapa de la historia")]
    public sealed class NarrativeWorldMap : ScriptableObject
    {
        [Serializable]
        public sealed class Zone
        {
            public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            public string name = "Zona";
            public Color color = new(0.36f, 0.62f, 0.9f);
            public Rect rect = new(40, 40, 200, 130);
            public bool round;
            [Tooltip("Marcas del mundo que caen en esta zona.")]
            public List<string> marks = new();
        }

        [Serializable]
        public sealed class Character
        {
            [Tooltip("Id del actor en el grafo ('Player' para el jugador).")]
            public string actorId;
            public string name;
            public Color color = new(0.85f, 0.35f, 0.19f);
            [Tooltip("Zona donde está cuando la historia no dice otra cosa.")]
            public string homeZone;
            [Tooltip("Personaje principal: siempre a la vista.")]
            public bool main;
        }

        [Tooltip("Fondo del mapa: una vista desde arriba del mundo («Hacer vista desde arriba» en el mapa). Opcional.")]
        public Texture2D background;

        [Tooltip("Qué trozo del mundo (X y Z) abarca el fondo: lo pone «Hacer vista desde arriba». Con él, cada personaje va al punto exacto de su marca.")]
        public bool hasWorldRect;
        public Rect worldRect;

        /// Un sitio recordado del mundo: dónde está una marca o un personaje.
        [Serializable]
        public sealed class Spot
        {
            public string id;
            public Vector3 position;
        }

        [Tooltip("Dónde están las marcas, recordado al hacer la vista: el mapa funciona sin la escena abierta.")]
        public List<Spot> markSpots = new();

        [Tooltip("Dónde está cada personaje cuando la historia no dice nada, recordado al hacer la vista.")]
        public List<Spot> actorSpots = new();

        public List<Zone> zones = new();
        public List<Character> characters = new();

        public Zone ZoneOfMark(string mark) => string.IsNullOrEmpty(mark) ? null : zones.FirstOrDefault(z => z.marks.Contains(mark));
        public Zone ZoneById(string id) => zones.FirstOrDefault(z => z.id == id);
        public Character CharacterOf(string actorId) => characters.FirstOrDefault(c => c.actorId == actorId);

        /// El mapa del proyecto (el primero que haya), o null.
        public static NarrativeWorldMap Find()
            => AssetDatabase.FindAssets("t:" + nameof(NarrativeWorldMap))
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeWorldMap>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(m => m != null);

        /// Crea un mapa vacío junto a la ficha del jugador (o en Assets).
        public static NarrativeWorldMap Create()
        {
            var sheet = NarrativePlayerSheet.Find();
            var folder = sheet != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(sheet)).Replace('\\', '/') : "Assets";
            var map = CreateInstance<NarrativeWorldMap>();
            map.characters.Add(new Character { actorId = "Player", name = Story.StoryText.T("Jugador", "Player"), color = new Color(0.22f, 0.54f, 0.87f), main = true });
            AssetDatabase.CreateAsset(map, AssetDatabase.GenerateUniqueAssetPath($"{folder}/Mapa de la historia.asset"));
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
