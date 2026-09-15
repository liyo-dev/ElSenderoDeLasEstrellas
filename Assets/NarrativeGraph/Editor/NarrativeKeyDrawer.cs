using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Dibuja un campo string marcado con [NarrativeKey] como "texto + desplegable con búsqueda"
    /// alimentado por NarrativeProjectIndex. Si el valor no resuelve contra el proyecto, el campo
    /// se tiñe de ámbar y el tooltip explica por qué. Funciona igual en el Inspector normal y
    /// dentro del editor de grafo (PropertyField → IMGUI).
    /// </summary>
    [CustomPropertyDrawer(typeof(NarrativeKeyAttribute))]
    public sealed class NarrativeKeyDrawer : PropertyDrawer
    {
        const float ButtonWidth = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var attr = (NarrativeKeyAttribute)attribute;
            string questId = attr.Kind == NarrativeKeyKind.QuestStep ? ResolveSibling(property, attr.QuestIdField) : null;
            bool resolves = string.IsNullOrEmpty(property.stringValue) || NarrativeProjectIndex.Resolves(attr.Kind, property.stringValue, questId);

            var fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
            var btnRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            var prevColor = GUI.backgroundColor;
            if (!resolves) GUI.backgroundColor = new Color(1f, 0.78f, 0.35f);

            EditorGUI.BeginProperty(fieldRect, label, property);
            var content = new GUIContent(label.text, resolves ? Tooltip(attr.Kind, property.stringValue) : $"'{property.stringValue}' no existe en el proyecto ({attr.Kind}).");
            EditorGUI.PropertyField(fieldRect, property, content);
            EditorGUI.EndProperty();

            GUI.backgroundColor = prevColor;

            if (GUI.Button(btnRect, new GUIContent("▾", "Elegir del proyecto"), EditorStyles.miniButton))
            {
                var options = NarrativeProjectIndex.OptionsFor(attr.Kind, questId);
                var target = property.serializedObject;
                var path = property.propertyPath;
                var kind = attr.Kind;
                SearchablePopup.Show(btnRect, options, property.stringValue, kind, value =>
                {
                    var p = target.FindProperty(path);
                    if (p == null) return;
                    target.Update();
                    p.stringValue = value;
                    target.ApplyModifiedProperties();
                });
            }
        }

        static string Tooltip(NarrativeKeyKind kind, string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            switch (kind)
            {
                case NarrativeKeyKind.Quest:
                    {
                        var q = NarrativeProjectIndex.FindQuest(value);
                        return q != null ? $"{q.displayName} — {q.stepConditionIds.Length} pasos" : "";
                    }
                case NarrativeKeyKind.LocKey: return NarrativeProjectIndex.Loc(value, "") ?? "";
                case NarrativeKeyKind.Signal:
                    {
                        var s = NarrativeProjectIndex.FindSignal(value);
                        if (s == null) return "";
                        return $"Emiten: {(s.emitters.Count == 0 ? "nadie" : string.Join(", ", s.emitters))}\nEscuchan: {(s.listeners.Count == 0 ? "nadie" : string.Join(", ", s.listeners))}";
                    }
                case NarrativeKeyKind.Actor:
                    {
                        var a = NarrativeProjectIndex.FindActor(value);
                        return a != null ? a.source : "";
                    }
                default: return "";
            }
        }

        static string ResolveSibling(SerializedProperty property, string siblingName)
        {
            if (string.IsNullOrEmpty(siblingName)) return null;
            var path = property.propertyPath;
            // quitar ".Array.data[i]" si somos elemento de lista
            int arr = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (arr >= 0 && path.EndsWith("]")) path = path.Substring(0, arr);
            int dot = path.LastIndexOf('.');
            var parent = dot >= 0 ? path.Substring(0, dot) : "";
            var siblingPath = string.IsNullOrEmpty(parent) ? siblingName : parent + "." + siblingName;
            var sp = property.serializedObject.FindProperty(siblingPath);
            return sp != null && sp.propertyType == SerializedPropertyType.String ? sp.stringValue : null;
        }
    }

    /// <summary>Popup con caja de búsqueda y lista de opciones (IMGUI, reutilizable).</summary>
    public sealed class SearchablePopup : PopupWindowContent
    {
        readonly List<string> _options;
        readonly string _current;
        readonly Action<string> _onPick;
        readonly NarrativeKeyKind _kind;
        string _search = "";
        Vector2 _scroll;
        bool _focusSearch = true;

        public static void Show(Rect anchor, List<string> options, string current, NarrativeKeyKind kind, Action<string> onPick)
        {
            PopupWindow.Show(anchor, new SearchablePopup(options, current, kind, onPick));
        }

        SearchablePopup(List<string> options, string current, NarrativeKeyKind kind, Action<string> onPick)
        {
            _options = options ?? new List<string>();
            _current = current;
            _kind = kind;
            _onPick = onPick;
        }

        public override Vector2 GetWindowSize() => new Vector2(360f, 320f);

        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName("narrative-key-search");
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (_focusSearch) { EditorGUI.FocusTextInControl("narrative-key-search"); _focusSearch = false; }

            if (_options.Count == 0)
            {
                EditorGUILayout.HelpBox($"No hay valores de tipo {_kind} en el proyecto todavía. Escribe uno nuevo en el campo.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string q = _search.Trim();
            int shown = 0;
            foreach (var opt in _options)
            {
                if (q.Length > 0 && opt.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                bool isCurrent = string.Equals(opt, _current, StringComparison.Ordinal);
                var style = isCurrent ? EditorStyles.boldLabel : EditorStyles.label;
                var label = SecondaryText(opt);
                if (GUILayout.Button(string.IsNullOrEmpty(label) ? opt : $"{opt}   <color=#888>{label}</color>", RichLabel(style)))
                {
                    _onPick?.Invoke(opt);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
                if (shown > 400) { EditorGUILayout.LabelField("… (afina la búsqueda)"); break; }
            }
            EditorGUILayout.EndScrollView();

            if (q.Length > 0 && !_options.Contains(q))
            {
                if (GUILayout.Button($"Usar '{q}' (nuevo)", EditorStyles.miniButton))
                {
                    _onPick?.Invoke(q);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
            }
        }

        string SecondaryText(string opt)
        {
            switch (_kind)
            {
                case NarrativeKeyKind.Quest: return NarrativeProjectIndex.FindQuest(opt)?.displayName;
                case NarrativeKeyKind.LocKey:
                    {
                        var t = NarrativeProjectIndex.Loc(opt, "");
                        if (string.IsNullOrEmpty(t)) return null;
                        return t.Length > 48 ? t.Substring(0, 48) + "…" : t;
                    }
                case NarrativeKeyKind.Actor: return NarrativeProjectIndex.FindActor(opt)?.displayName;
                case NarrativeKeyKind.Signal:
                    {
                        var s = NarrativeProjectIndex.FindSignal(opt);
                        return s == null ? null : $"{s.emitters.Count}↑ {s.listeners.Count}↓";
                    }
                default: return null;
            }
        }

        static readonly Dictionary<GUIStyle, GUIStyle> _rich = new();
        static GUIStyle RichLabel(GUIStyle baseStyle)
        {
            if (!_rich.TryGetValue(baseStyle, out var s))
            {
                s = new GUIStyle(baseStyle) { richText = true, alignment = TextAnchor.MiddleLeft };
                _rich[baseStyle] = s;
            }
            return s;
        }
    }
}
