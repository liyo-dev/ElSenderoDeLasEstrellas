using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// Lo que guarda un punto de arranque: dónde aparece el jugador (el campo de posición de la
    /// ficha). El resto del jugador sale de la historia; para probar con otro, un jugador de prueba.
    [CustomPropertyDrawer(typeof(NarrativePlayerValues))]
    public sealed class NarrativePlayerValuesDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var title = new Label(StoryText.T("DÓNDE APARECE EL JUGADOR", "WHERE THE PLAYER APPEARS"));
            title.AddToClassList("story-kicker");
            title.style.marginTop = 8;
            root.Add(title);

            var sheet = NarrativePlayerSheet.Find();
            if (sheet == null || sheet.DataType == null || string.IsNullOrEmpty(sheet.positionField))
            {
                root.Add(Muted(StoryText.T("Falta la ficha del jugador o su campo de posición (⚙ Ajustes).", "The player sheet or its position field is missing (⚙ Settings).")));
                return root;
            }

            var scratch = new PlayerScratch(sheet.DataType);
            root.RegisterCallback<DetachFromPanelEvent>(_ => scratch.Dispose());
            if (scratch.Data == null) return root;
            var data = property.FindPropertyRelative(nameof(NarrativePlayerValues.data)).stringValue;
            if (!string.IsNullOrEmpty(data)) EditorJsonUtility.FromJsonOverwrite(data, scratch.Data);
            var so = new SerializedObject(scratch.Data);
            var position = so.FindProperty(sheet.positionField);
            if (position == null) return root;

            var holder = property.serializedObject;
            var path = property.propertyPath;
            var field = new PropertyField(position.Copy(), sheet.LabelOf(sheet.positionField));
            field.Bind(so);
            field.TrackPropertyValue(position, p =>
            {
                holder.Update();
                var target = holder.FindProperty(path);
                var values = new NarrativePlayerValues();
                bool empty = p.propertyType == SerializedPropertyType.String && string.IsNullOrEmpty(p.stringValue);
                if (!empty) NarrativePlayerValuesEditor.Set(values, scratch.Data, new[] { sheet.positionField });
                target.FindPropertyRelative(nameof(NarrativePlayerValues.data)).stringValue = values.data ?? string.Empty;
                var list = target.FindPropertyRelative(nameof(NarrativePlayerValues.fields));
                list.ClearArray();
                for (int i = 0; i < values.fields.Count; i++) { list.InsertArrayElementAtIndex(i); list.GetArrayElementAtIndex(i).stringValue = values.fields[i]; }
                holder.ApplyModifiedProperties();
            });
            root.Add(field);
            root.Add(Muted(StoryText.T("Vacío: aparece donde le deje la historia. Lo demás del jugador sale de la historia.",
                                       "Empty: appears wherever the story leaves them. The rest of the player comes from the story.")));
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
