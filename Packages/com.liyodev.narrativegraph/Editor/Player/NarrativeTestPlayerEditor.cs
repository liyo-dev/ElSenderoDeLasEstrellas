using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// Inspector del jugador de prueba: cada campo de la ficha con una casilla «cambiar»; los que
    /// no se marcan salen de la historia. El sitio donde aparece no se cambia aquí.
    [CustomEditor(typeof(NarrativeTestPlayer))]
    public sealed class NarrativeTestPlayerEditor : Editor
    {
        PlayerScratch _scratch;
        SerializedObject _data;

        void OnDisable()
        {
            _scratch?.Dispose();
            _scratch = null;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var player = (NarrativeTestPlayer)target;
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(NarrativeTestPlayer.notes)), StoryText.T("Notas", "Notes")));

            var sheet = NarrativePlayerSheet.Find();
            if (sheet == null || !sheet.IsReady)
            {
                root.Add(new HelpBox(StoryText.T("Falta la ficha del jugador (⚙ Ajustes del grafo).", "The player sheet is missing (graph ⚙ Settings)."), HelpBoxMessageType.Warning));
                return root;
            }
            root.Add(new HelpBox(StoryText.T("Marca lo que cambia este jugador. Lo que no marques sale de la historia hasta el paso desde el que pruebas.",
                                             "Tick what this player changes. Anything left unticked comes from the story up to the step you test from."), HelpBoxMessageType.Info));

            _scratch?.Dispose();
            _scratch = new PlayerScratch(sheet.DataType);
            if (_scratch.Data == null) return root;
            EditorUtility.CopySerialized(sheet.TemplateData, _scratch.Data);
            NarrativePlayerValuesEditor.Apply(player.values, _scratch.Data);
            _data = new SerializedObject(_scratch.Data);

            foreach (var field in sheet.fields.Where(f => f != sheet.positionField))
            {
                var prop = _data.FindProperty(field);
                if (prop == null) continue;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;
                var on = new Toggle { value = player.values.Has(field), tooltip = StoryText.T("Cambiar este campo", "Change this field") };
                on.style.marginTop = 3;
                var pf = new PropertyField(prop.Copy());
                pf.Bind(_data);
                pf.style.flexGrow = 1;
                pf.SetEnabled(on.value);
                string captured = field;
                on.RegisterValueChangedCallback(e => { pf.SetEnabled(e.newValue); Save(); });
                row.Add(on);
                row.Add(pf);
                root.Add(row);
                _toggles[captured] = on;
            }
            root.TrackSerializedObjectValue(_data, _ => Save());
            return root;
        }

        readonly Dictionary<string, Toggle> _toggles = new();

        void Save()
        {
            var player = (NarrativeTestPlayer)target;
            _data.Update();
            Undo.RecordObject(player, StoryText.T("Cambiar jugador de prueba", "Change test player"));
            player.values = new NarrativePlayerValues();
            NarrativePlayerValuesEditor.Set(player.values, _scratch.Data, _toggles.Where(t => t.Value.value).Select(t => t.Key));
            EditorUtility.SetDirty(player);
            StoryCache.Invalidate();
        }
    }
}
