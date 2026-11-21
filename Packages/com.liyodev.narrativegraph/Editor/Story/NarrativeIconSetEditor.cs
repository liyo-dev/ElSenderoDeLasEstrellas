using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// El asset de dibujos del grafo: botones para traer las categorías y los personajes del
    /// proyecto, y el inspector normal para arrastrar las texturas.
    [CustomEditor(typeof(NarrativeIconSet))]
    public sealed class NarrativeIconSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var set = (NarrativeIconSet)target;
            EditorGUILayout.HelpBox(
                "Los dibujos que falten se quedan con el dibujo vectorial de su categoría y con el retrato del personaje. " +
                "Mejor PNG con fondo transparente: 2:1 para las categorías y cuadrado para los personajes.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Traer las categorías"))
                {
                    Undo.RecordObject(set, "Traer las categorías del grafo");
                    set.FillCategories();
                    EditorUtility.SetDirty(set);
                    serializedObject.Update();
                }
                if (GUILayout.Button("Traer los actores"))
                {
                    Undo.RecordObject(set, "Traer los actores del grafo");
                    set.FillActors();
                    EditorUtility.SetDirty(set);
                    serializedObject.Update();
                }
            }

            if (GUILayout.Button("Quitar todas las texturas") &&
                EditorUtility.DisplayDialog("Dibujos del grafo", "¿Quitar todas las texturas asignadas?", "Quitar", "Cancelar"))
            {
                Undo.RecordObject(set, "Quitar los dibujos del grafo");
                set.ClearImages();
                EditorUtility.SetDirty(set);
                serializedObject.Update();
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();

            if (serializedObject.ApplyModifiedProperties())
                EditorApplication.delayCall += NarrativeIconImages.Refresh;
        }
    }
}
