using System.Collections.Generic;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// Lo que fija un punto de arranque, en claro: campo y valor. Se cambia desde la pestaña
    /// Arranque (retocar y «◆ Fijar aquí»); aquí solo se puede quitar un campo.
    [CustomPropertyDrawer(typeof(NarrativePlayerValues))]
    public sealed class NarrativePlayerValuesDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var title = new Label(StoryText.T("JUGADOR AQUÍ", "PLAYER HERE"));
            title.AddToClassList("story-kicker");
            title.style.marginTop = 8;
            root.Add(title);

            var fields = property.FindPropertyRelative(nameof(NarrativePlayerValues.fields));
            var data = property.FindPropertyRelative(nameof(NarrativePlayerValues.data)).stringValue;
            var sheet = NarrativePlayerSheet.Find();
            if (fields.arraySize == 0)
            {
                root.Add(Muted(StoryText.T("No fija nada: el jugador sigue como venía. Retócalo en la pestaña Arranque y pulsa «◆ Fijar aquí».",
                                           "Sets nothing: the player stays as before. Tweak it in the Start tab and press “◆ Set here”.")));
                return root;
            }
            if (sheet == null || sheet.DataType == null)
            {
                root.Add(Muted(StoryText.T("Falta la ficha del jugador (⚙ Ajustes).", "The player sheet is missing (⚙ Settings).")));
                return root;
            }

            using var scratch = new PlayerScratch(sheet.DataType);
            if (scratch.Data != null && !string.IsNullOrEmpty(data)) EditorJsonUtility.FromJsonOverwrite(data, scratch.Data);
            var so = scratch.Data != null ? new SerializedObject(scratch.Data) : null;
            var paths = new List<string>();
            for (int i = 0; i < fields.arraySize; i++) paths.Add(fields.GetArrayElementAtIndex(i).stringValue);

            foreach (var path in paths)
            {
                var row = new VisualElement();
                row.AddToClassList("story-panel__row");
                var name = new Label(sheet.LabelOf(path)) { tooltip = path };
                name.AddToClassList("story-panel__row-name");
                row.Add(name);
                var value = new Label(NarrativePlayerValuesEditor.Text(so?.FindProperty(path)));
                value.AddToClassList("story-panel__row-value");
                row.Add(value);
                string captured = path;
                var remove = new Button(() =>
                {
                    property.serializedObject.Update();
                    var list = property.FindPropertyRelative(nameof(NarrativePlayerValues.fields));
                    for (int i = 0; i < list.arraySize; i++)
                        if (list.GetArrayElementAtIndex(i).stringValue == captured) { list.DeleteArrayElementAtIndex(i); break; }
                    property.serializedObject.ApplyModifiedProperties();
                    row.RemoveFromHierarchy();
                }) { text = "×", tooltip = StoryText.T("Que este punto no lo fije", "Stop setting it here") };
                remove.AddToClassList("story-button");
                row.Add(remove);
                root.Add(row);
            }
            return root;
        }

        static Label Muted(string text)
        {
            var l = new Label(text);
            l.AddToClassList("story-muted");
            return l;
        }
    }
}
