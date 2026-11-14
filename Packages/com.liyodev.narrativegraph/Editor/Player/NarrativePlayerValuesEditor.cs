using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NarrativeGraphEditor.Story;

namespace NarrativeGraphEditor
{
    /// Copia de trabajo de los datos del jugador (fuera de la escena y sin guardar), para leer o
    /// escribir valores sin tocar ningún asset.
    public sealed class PlayerScratch : IDisposable
    {
        public UnityEngine.Object Data { get; }
        readonly GameObject _host;

        public PlayerScratch(Type type)
        {
            if (type == null) return;
            if (typeof(Component).IsAssignableFrom(type))
            {
                // DontSave, no HideAndDontSave: este último incluye NotEditable y el Inspector no dejaría tocarlo.
                _host = new GameObject("Player scratch") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
                Data = _host.AddComponent(type);
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                Data = ScriptableObject.CreateInstance(type);
                Data.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
        }

        public void Dispose()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            else if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
        }
    }

    /// Leer, escribir y enseñar los valores del jugador que guarda un punto del grafo.
    public static class NarrativePlayerValuesEditor
    {
        /// Escribe en <paramref name="destination"/> los campos que fija <paramref name="values"/>.
        public static void Apply(NarrativePlayerValues values, UnityEngine.Object destination)
        {
            if (values == null || values.IsEmpty || destination == null) return;
            using var scratch = new PlayerScratch(destination.GetType());
            if (scratch.Data == null) return;
            if (!string.IsNullOrEmpty(values.data)) EditorJsonUtility.FromJsonOverwrite(values.data, scratch.Data);
            Copy(scratch.Data, destination, values.fields);
        }

        /// Copia los campos indicados de un objeto a otro del mismo tipo.
        public static void Copy(UnityEngine.Object source, UnityEngine.Object destination, IEnumerable<string> fields)
        {
            if (source == null || destination == null || fields == null) return;
            var from = new SerializedObject(source);
            var to = new SerializedObject(destination);
            foreach (var field in fields)
            {
                var p = from.FindProperty(field);
                if (p != null) to.CopyFromSerializedProperty(p);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Guarda en <paramref name="values"/> el valor actual de <paramref name="fields"/> en
        /// <paramref name="source"/>, conservando los demás campos que ya fijaba.
        public static void Set(NarrativePlayerValues values, UnityEngine.Object source, IEnumerable<string> fields)
        {
            if (values == null || source == null || fields == null) return;
            using var scratch = new PlayerScratch(source.GetType());
            if (scratch.Data == null) return;
            if (!string.IsNullOrEmpty(values.data)) EditorJsonUtility.FromJsonOverwrite(values.data, scratch.Data);
            var list = fields.Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();
            Copy(source, scratch.Data, list);
            values.fields ??= new List<string>();
            foreach (var f in list) if (!values.fields.Contains(f)) values.fields.Add(f);
            values.data = EditorJsonUtility.ToJson(scratch.Data);
        }

        /// Fija un campo con un valor escrito a mano (texto, número, sí/no o enum), para montajes por código.
        public static bool SetValue(NarrativePlayerValues values, Type dataType, string field, Action<SerializedProperty> write)
        {
            if (values == null || dataType == null || write == null) return false;
            using var scratch = new PlayerScratch(dataType);
            if (scratch.Data == null) return false;
            if (!string.IsNullOrEmpty(values.data)) EditorJsonUtility.FromJsonOverwrite(values.data, scratch.Data);
            var so = new SerializedObject(scratch.Data);
            var p = so.FindProperty(field);
            if (p == null) return false;
            write(p);
            so.ApplyModifiedPropertiesWithoutUndo();
            values.fields ??= new List<string>();
            if (!values.fields.Contains(field)) values.fields.Add(field);
            values.data = EditorJsonUtility.ToJson(scratch.Data);
            return true;
        }

        public static void Remove(NarrativePlayerValues values, string field)
        {
            values?.fields?.Remove(field);
        }

        /// Texto corto de un valor para el resumen.
        public static string Text(SerializedProperty p)
        {
            if (p == null) return "—";
            switch (p.propertyType)
            {
                case SerializedPropertyType.String: return string.IsNullOrEmpty(p.stringValue) ? "—" : p.stringValue;
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.##");
                case SerializedPropertyType.Boolean: return p.boolValue ? StoryText.T("Sí", "Yes") : StoryText.T("No", "No");
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "—";
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString("0.#");
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("0.#");
            }
            if (p.isArray && p.propertyType != SerializedPropertyType.String)
            {
                if (p.arraySize == 0) return "—";
                if (p.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.Generic)
                    return StoryText.T($"{p.arraySize} elementos", $"{p.arraySize} items");
                var parts = new List<string>();
                for (int i = 0; i < Math.Min(p.arraySize, 4); i++) parts.Add(Text(p.GetArrayElementAtIndex(i)));
                return string.Join(", ", parts) + (p.arraySize > 4 ? $" (+{p.arraySize - 4})" : string.Empty);
            }
            if (p.hasVisibleChildren)
            {
                var children = Children(p).ToList();
                if (children.Count > 0 && children.All(c => c.propertyType == SerializedPropertyType.Boolean))
                {
                    var on = children.Where(c => c.boolValue).Select(c => c.displayName).ToList();
                    return on.Count == 0 ? "—" : string.Join(", ", on);
                }
                return string.Join(" · ", children.Take(3).Select(c => $"{c.displayName} {Text(c)}"));
            }
            return "…";
        }

        static IEnumerable<SerializedProperty> Children(SerializedProperty p)
        {
            var it = p.Copy();
            var end = p.GetEndProperty();
            if (!it.NextVisible(true)) yield break;
            while (!SerializedProperty.EqualContents(it, end))
            {
                yield return it.Copy();
                if (!it.NextVisible(false)) break;
            }
        }
    }
}
