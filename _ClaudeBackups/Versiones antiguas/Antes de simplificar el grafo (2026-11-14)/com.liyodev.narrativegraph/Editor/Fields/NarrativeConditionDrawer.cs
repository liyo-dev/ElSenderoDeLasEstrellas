using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// One condition on one line: what it looks at and, depending on that, only the fields it uses.
    [CustomPropertyDrawer(typeof(NarrativeCondition))]
    public sealed class NarrativeConditionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexWrap = Wrap.Wrap;
            root.style.alignItems = Align.Center;

            var kind = property.FindPropertyRelative(nameof(NarrativeCondition.kind));
            var key = Field(property, nameof(NarrativeCondition.key), 120);
            var quest = Field(property, nameof(NarrativeCondition.questState), 90);
            var comparison = Field(property, nameof(NarrativeCondition.comparison), 80);
            var number = Field(property, nameof(NarrativeCondition.number), 44);
            var item = Field(property, nameof(NarrativeCondition.item), 120);

            var kindField = new PropertyField(kind, string.Empty);
            kindField.style.width = 110;
            root.Add(kindField);
            root.Add(item);
            root.Add(key);
            root.Add(quest);
            root.Add(comparison);
            root.Add(number);

            void Refresh()
            {
                var k = (NarrativeCondition.Kind)kind.enumValueIndex;
                Show(key, k != NarrativeCondition.Kind.HasItem);
                Show(item, k == NarrativeCondition.Kind.HasItem);
                Show(quest, k == NarrativeCondition.Kind.QuestIs);
                bool numeric = k == NarrativeCondition.Kind.Counter || k == NarrativeCondition.Kind.HasItem;
                Show(comparison, numeric);
                Show(number, numeric);
            }

            Refresh();
            kindField.RegisterValueChangeCallback(_ => Refresh());
            return root;
        }

        static PropertyField Field(SerializedProperty parent, string name, float minWidth)
        {
            var field = new PropertyField(parent.FindPropertyRelative(name), string.Empty);
            field.style.minWidth = minWidth;
            field.style.flexGrow = 1;
            return field;
        }

        static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
