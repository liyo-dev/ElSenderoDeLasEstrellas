using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// Encuentra los dibujos propios del proyecto y los sirve a las tarjetas y al mapa. El asset
    /// es opcional: si no existe, se usan los dibujos vectoriales y los retratos.
    public static class NarrativeIconImages
    {
        const string PrefSet = "NarrativeGraph.Icons.Set";
        const string Folder = "Assets/NarrativeGraph/Iconos";
        const string AssetPath = Folder + "/Dibujos del grafo.asset";

        static NarrativeIconSet s_set;
        static bool s_looked;

        public static NarrativeIconSet Set
        {
            get
            {
                if (!s_looked) { s_set = Find(); s_looked = true; }
                return s_set;
            }
        }

        public static Texture2D CategoryArt(string category) => Set != null ? Set.CategoryOf(category) : null;
        public static Texture2D ActorArt(string actorId) => Set != null ? Set.ActorOf(actorId) : null;

        /// Los dibujos han cambiado: se vuelven a leer y las tarjetas se repintan.
        public static void Refresh()
        {
            s_set = null;
            s_looked = false;
            StoryCardImages.NotifyChanged();
        }

        static NarrativeIconSet Find()
        {
            var path = EditorPrefs.GetString(PrefSet, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                var set = AssetDatabase.LoadAssetAtPath<NarrativeIconSet>(path);
                if (set != null) return set;
            }

            var guids = AssetDatabase.FindAssets("t:NarrativeIconSet");
            if (guids.Length == 0) return null;
            var found = AssetDatabase.LoadAssetAtPath<NarrativeIconSet>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (found != null) EditorPrefs.SetString(PrefSet, AssetDatabase.GetAssetPath(found));
            return found;
        }

        [MenuItem("Liyodev/Grafo narrativo/Herramientas/Dibujos del grafo...", priority = 104)]
        public static void Open()
        {
            var set = Set;
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NarrativeGraph"))
                    AssetDatabase.CreateFolder("Assets", "NarrativeGraph");
                if (!AssetDatabase.IsValidFolder(Folder))
                    AssetDatabase.CreateFolder("Assets/NarrativeGraph", "Iconos");

                set = AssetDatabase.LoadAssetAtPath<NarrativeIconSet>(AssetPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<NarrativeIconSet>();
                    set.name = "Dibujos del grafo";
                    set.FillCategories();
                    set.FillActors();
                    AssetDatabase.CreateAsset(set, AssetPath);
                    AssetDatabase.SaveAssets();
                }
                EditorPrefs.SetString(PrefSet, AssetPath);
                s_set = set;
                s_looked = true;
            }

            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
        }
    }
}
