using UnityEditor;
using UnityEngine;
using GraphAsset = global::NarrativeGraph;

namespace NarrativeGraphEditor
{
    public static class NarrativeMenu
    {
        [MenuItem("Tools/Narrative Graph/Nuevo grafo")]
        public static void CreateGraph()
        {
            var g = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(g, "Assets/NarrativeGraph/NewNarrativeGraph.asset");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(g);
        }
    }
}