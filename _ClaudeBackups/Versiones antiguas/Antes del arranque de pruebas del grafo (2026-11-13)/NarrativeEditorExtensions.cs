using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// <summary>
    /// Pestaña lateral que un juego añade al editor del grafo, p. ej. «Prueba»: lanzar el juego
    /// desde un nodo con su propio estado de partida. El editor las encuentra solas: basta con una
    /// clase no abstracta, con constructor sin parámetros, que herede de esta.
    /// </summary>
    public abstract class NarrativeGraphSideTab : VisualElement
    {
        /// <summary>Texto del botón de la pestaña.</summary>
        public abstract string TabTitle { get; }

        /// <summary>Orden entre las pestañas del juego (menor = más a la izquierda).</summary>
        public virtual int Order => 100;

        /// <summary>Grafo abierto en el editor (null si ninguno).</summary>
        public virtual void SetGraph(NarrativeGraph graph) { }

        /// <summary>Nodo seleccionado en el lienzo (null si ninguno o varios).</summary>
        public virtual void SetSelectedNode(NarrativeNode node) { }

        /// <summary>
        /// El editor pide probar desde este nodo (▶ de la barra o de la tarjeta). true si esta
        /// pestaña atiende la petición; el editor la enseña entonces.
        /// </summary>
        public virtual bool TryPlayFrom(NarrativeGraph graph, string nodeGuid) => false;

        /// <summary>La pestaña acaba de hacerse visible.</summary>
        public virtual void OnShown() { }
    }

    /// <summary>
    /// Sección que un juego añade a la pestaña «En vivo» (grupo, misiones, actores en escena…).
    /// Solo se consulta en Play, cada pocos cientos de milisegundos.
    /// </summary>
    public interface INarrativeLiveSection
    {
        string Title { get; }

        /// <summary>Orden entre las secciones del juego (menor = más arriba).</summary>
        int Order { get; }

        /// <summary>Texto si no hay filas.</summary>
        string EmptyText { get; }

        /// <summary>Añade las filas de la sección tal y como está el juego ahora.</summary>
        void Collect(List<string> rows);
    }

    /// <summary>
    /// Simulación de cómo queda el mundo al llegar a un nodo sin entrar en Play (dónde está cada
    /// actor y qué decisiones del camino no se pueden adivinar). La aporta el juego, que sabe
    /// convertir marcas en posiciones.
    /// </summary>
    public interface INarrativeArrivalPreview
    {
        void Preview(NarrativeGraph graph, string nodeGuid, List<string> actors, List<string> warnings);
    }

    /// <summary>
    /// Aporta al índice del proyecto (NarrativeProjectIndex) lo que solo el juego sabe leer: sus
    /// misiones, sus actores, sus textos localizados, las señales de sus propios assets y las
    /// convenciones de nombres de campo de sus escenas.
    /// </summary>
    public interface INarrativeProjectCatalog
    {
        void Contribute(NarrativeProjectIndex.Contributions into);
    }

    /// <summary>Busca las extensiones que el juego aporta al editor del grafo.</summary>
    public static class NarrativeEditorExtensions
    {
        /// <summary>Una instancia de cada clase no abstracta que implemente o herede de <typeparamref name="T"/>.</summary>
        public static List<T> CreateAll<T>() where T : class
        {
            var result = new List<T>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is T instance) result.Add(instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NarrativeEditorExtensions] No se pudo crear {type.Name}: {e.Message}");
                }
            }
            return result;
        }

        /// <summary>La primera extensión de este tipo, o null si el juego no aporta ninguna.</summary>
        public static T CreateFirst<T>() where T : class
        {
            var all = CreateAll<T>();
            return all.Count > 0 ? all[0] : null;
        }
    }
}
