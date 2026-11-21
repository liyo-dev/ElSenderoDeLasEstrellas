using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor
{
    /// Jugador de prueba: lo que se cambia del jugador al probar, sea cual sea el paso («con todos
    /// los hechizos», «con poca vida»). Solo guarda los campos que cambia; el resto sale de la
    /// historia hasta el paso. No toca el grafo ni el sitio donde aparece el jugador.
    [CreateAssetMenu(menuName = "Narrative Graph/Jugador de prueba", fileName = "Jugador de prueba")]
    public sealed class NarrativeTestPlayer : ScriptableObject
    {
        [TextArea] public string notes = string.Empty;
        public NarrativePlayerValues values = new();
    }

    /// Los jugadores de prueba del proyecto y cuál se usa al probar (en este ordenador).
    public static class TestPlayers
    {
        static string Key => "NarrativeGraph.Start.TestPlayer." + Application.dataPath;
        public const string FolderName = "Jugadores de prueba";

        public static IReadOnlyList<NarrativeTestPlayer> All()
            => Story.StoryCache.Get("testPlayers", () => AssetDatabase.FindAssets("t:" + nameof(NarrativeTestPlayer))
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeTestPlayer>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name).ToList());

        /// Ha cambiado el jugador de prueba elegido (para quien lo enseña sin consultarlo cada poco).
        public static event System.Action SelectionChanged;

        /// El que se usa al probar, o null: el jugador sale de la historia.
        public static NarrativeTestPlayer Selected
        {
            get
            {
                var guid = EditorPrefs.GetString(Key, string.Empty);
                return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<NarrativeTestPlayer>(AssetDatabase.GUIDToAssetPath(guid));
            }
            set
            {
                EditorPrefs.SetString(Key, value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : string.Empty);
                Story.StoryCache.Invalidate();
                SelectionChanged?.Invoke();
            }
        }

        /// Crea un jugador de prueba junto a la ficha del jugador (carpeta «Jugadores de prueba»)
        /// con los campos <paramref name="fields"/> de <paramref name="from"/> (o vacío).
        public static NarrativeTestPlayer Create(string name, Object from, IEnumerable<string> fields)
        {
            var sheet = NarrativePlayerSheet.Find();
            // AssetDatabase solo acepta rutas con «/»; Path.GetDirectoryName devuelve «\» en Windows.
            var root = sheet != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(sheet)).Replace('\\', '/') : "Assets";
            var folder = $"{root}/{FolderName}";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(root, FolderName);
            var player = ScriptableObject.CreateInstance<NarrativeTestPlayer>();
            if (from != null && fields != null) NarrativePlayerValuesEditor.Set(player.values, from, fields);
            AssetDatabase.CreateAsset(player, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset"));
            AssetDatabase.SaveAssets();
            Story.StoryCache.Invalidate();
            return player;
        }
    }
}
