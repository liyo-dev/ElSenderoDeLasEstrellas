using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Catálogo de tipos de nodo para el editor: descubre por reflection (solo editor) todas las
    /// clases NarrativeNode no abstractas ni obsoletas, y expone categoría / título / descripción
    /// / color a partir de [NarrativeNodeInfo]. Es la única fuente de colores del editor — antes
    /// había dos paletas a mano que habían divergido (NodeView.ExplicitPalette vs timeline).
    /// </summary>
    public static class NarrativeNodeCatalog
    {
        public sealed class Entry
        {
            public Type type;
            public string category;
            public string title;
            public string description;
            public bool obsolete;
        }

        // Orden de categorías en la paleta y color de cada una.
        public static readonly (string name, Color color)[] Categories =
        {
            ("Flujo",        new Color(0.42f, 0.47f, 0.58f)),
            ("Quests",       new Color(0.22f, 0.66f, 0.42f)),
            ("Diálogo",      new Color(0.20f, 0.62f, 0.80f)),
            ("NPCs",         new Color(0.85f, 0.56f, 0.22f)),
            ("Señales",      new Color(0.55f, 0.45f, 0.85f)),
            ("Cinemáticas",  new Color(0.72f, 0.36f, 0.72f)),
            ("Combate",      new Color(0.86f, 0.32f, 0.32f)),
            ("Objetos",      new Color(0.78f, 0.62f, 0.25f)),
            ("Jugador",      new Color(0.92f, 0.48f, 0.30f)),
            ("Mundo",        new Color(0.28f, 0.58f, 0.62f)),
            ("Audio y cine", new Color(0.36f, 0.50f, 0.78f)),
            ("Minijuegos",   new Color(0.60f, 0.70f, 0.30f)),
            ("Notas",        new Color(0.80f, 0.75f, 0.40f)),
            ("Otros",        new Color(0.45f, 0.45f, 0.45f)),
        };

        static List<Entry> _entries;
        static readonly Dictionary<Type, Entry> _byType = new();

        public static IReadOnlyList<Entry> All
        {
            get { EnsureBuilt(); return _entries; }
        }

        public static Entry Get(Type t)
        {
            EnsureBuilt();
            if (t == null) return null;
            if (_byType.TryGetValue(t, out var e)) return e;
            e = Build(t);
            _byType[t] = e;
            return e;
        }

        public static string TitleOf(Type t) => Get(t)?.title ?? t?.Name ?? "?";
        public static string CategoryOf(Type t) => Get(t)?.category ?? "Otros";

        public static Color ColorOf(string category)
        {
            foreach (var c in Categories)
                if (c.name == category) return c.color;
            return Categories[^1].color;
        }

        public static Color ColorOf(Type t) => ColorOf(CategoryOf(t));

        public static int CategoryOrder(string category)
        {
            for (int i = 0; i < Categories.Length; i++)
                if (Categories[i].name == category) return i;
            return Categories.Length;
        }

        static void EnsureBuilt()
        {
            if (_entries != null) return;
            _entries = new List<Entry>();
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(NarrativeNode).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
            foreach (var t in types)
            {
                var e = Build(t);
                _byType[t] = e;
                if (!e.obsolete) _entries.Add(e);
            }
            _entries = _entries
                .OrderBy(e => CategoryOrder(e.category))
                .ThenBy(e => e.title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static Entry Build(Type t)
        {
            var info = t.GetCustomAttribute<NarrativeNodeInfoAttribute>(false);
            var obsolete = t.GetCustomAttribute<ObsoleteAttribute>(false) != null;
            return new Entry
            {
                type = t,
                category = info?.Category ?? "Otros",
                title = info?.Title ?? PrettyName(t.Name),
                description = info?.Description ?? "",
                obsolete = obsolete
            };
        }

        static string PrettyName(string typeName)
        {
            if (typeName.EndsWith("Node")) typeName = typeName.Substring(0, typeName.Length - 4);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1])) sb.Append(' ');
                sb.Append(typeName[i]);
            }
            return sb.ToString();
        }
    }
}
