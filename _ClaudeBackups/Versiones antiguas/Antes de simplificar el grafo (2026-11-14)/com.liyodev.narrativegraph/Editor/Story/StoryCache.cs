using System;
using System.Collections.Generic;

namespace NarrativeGraphEditor.Story
{
    /// Resultados caros (reconstruir la historia hasta un paso) guardados hasta que cambie algún grafo.
    /// Así elegir pasos o refrescar «En vivo» en Play no vuelve a calcular lo mismo.
    public static class StoryCache
    {
        static int s_version;
        static readonly Dictionary<string, object> s_values = new();

        /// Algo ha cambiado en un grafo (edición, deshacer, retoques): todo se vuelve a calcular.
        public static void Invalidate()
        {
            s_version++;
            s_values.Clear();
        }

        public static T Get<T>(string key, Func<T> build) where T : class
        {
            if (s_values.TryGetValue(key, out var v) && v is T cached) return cached;
            var value = build();
            s_values[key] = value;
            return value;
        }
    }
}
