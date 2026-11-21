using UnityEditor;
using UnityEngine;
using GraphAsset = Liyodev.NarrativeGraph.NarrativeGraph;

namespace Liyodev.NarrativeGraph.Editor
{
    public static class NarrativeMenu
    {
        [MenuItem("Liyodev/Grafo narrativo/Nuevo grafo", priority = 1)]
        public static void CreateGraph()
        {
            var g = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(g, AssetDatabase.GenerateUniqueAssetPath("Assets/NewNarrativeGraph.asset"));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(g);
        }
    }
}