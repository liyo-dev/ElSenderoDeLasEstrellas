using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Ficha del jugador del juego: dónde están sus datos de partida nueva y qué campos se pueden
    /// fijar desde el grafo (en los puntos de arranque) y retocar al probar. El grafo no sabe qué
    /// campos tiene cada juego: los lee de aquí.
    ///
    /// La plantilla puede ser un ScriptableObject (los datos de partida nueva) o un prefab con el
    /// componente que guarda esos datos (<see cref="componentType"/>).
    [CreateAssetMenu(menuName = "Narrative Graph/Ficha del jugador", fileName = "PlayerSheet")]
    public sealed class NarrativePlayerSheet : ScriptableObject
    {
        [Tooltip("Los datos del jugador al empezar una partida nueva: un ScriptableObject o un prefab.")]
        public UnityEngine.Object template;

        [Tooltip("Si la plantilla es un prefab, el componente que guarda los datos (nombre completo del tipo).")]
        public string componentType = string.Empty;

        [Tooltip("Campos que se pueden fijar en los puntos de arranque y retocar al probar.")]
        public List<string> fields = new();

        [Tooltip("El campo que dice dónde aparece el jugador (opcional). Sirve para avisar si la historia cambia de sitio sin un punto que lo diga.")]
        public string positionField = string.Empty;

        [Tooltip("Ficha de un ejemplo: solo se usa si el proyecto no tiene una propia.")]
        public bool isExample;

        /// La ficha del proyecto, o null. Las de los ejemplos solo cuentan si no hay otra.
        public static NarrativePlayerSheet Find()
        {
            var sheets = AssetDatabase.FindAssets("t:" + nameof(NarrativePlayerSheet))
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativePlayerSheet>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).ToList();
            return sheets.FirstOrDefault(s => !s.isExample) ?? sheets.FirstOrDefault();
        }

        /// El objeto con los datos de partida nueva: el ScriptableObject, o el componente del prefab.
        public UnityEngine.Object TemplateData
        {
            get
            {
                if (template is GameObject go)
                {
                    var type = DataType;
                    return type != null ? go.GetComponent(type) : null;
                }
                return template;
            }
        }

        /// Tipo de los datos del jugador.
        public Type DataType
        {
            get
            {
                if (template is GameObject)
                    return string.IsNullOrEmpty(componentType) ? null : TypeCache.GetTypesDerivedFrom<Component>().FirstOrDefault(t => t.FullName == componentType);
                return template != null ? template.GetType() : null;
            }
        }

        public bool IsComponent => template is GameObject;

        public bool IsReady => TemplateData != null;

        /// Campos de la plantilla que se pueden elegir: los de primer nivel visibles en el Inspector.
        public List<(string path, string label)> AvailableFields()
        {
            var list = new List<(string, string)>();
            var data = TemplateData;
            if (data == null) return list;
            var so = new SerializedObject(data);
            var it = so.GetIterator();
            if (!it.NextVisible(true)) return list;
            do
            {
                if (it.propertyPath == "m_Script") continue;
                list.Add((it.propertyPath, it.displayName));
            }
            while (it.NextVisible(false));
            return list;
        }

        /// Nombre legible de un campo ("maxHP" → "Max HP").
        public string LabelOf(string field)
        {
            var data = TemplateData;
            if (data == null) return ObjectNames.NicifyVariableName(field);
            var p = new SerializedObject(data).FindProperty(field);
            return p != null ? p.displayName : ObjectNames.NicifyVariableName(field);
        }
    }
}
