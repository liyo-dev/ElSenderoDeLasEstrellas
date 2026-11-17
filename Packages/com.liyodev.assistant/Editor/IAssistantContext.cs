using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Assistant
{
    /// Lo que ha quedado de aplicar un bloque {"ops":[...]} propuesto por el asistente.
    public sealed class AssistantApplyResult
    {
        public bool Applied;
        public readonly List<string> Done = new();
        public readonly List<string> Diff = new();
        public readonly List<string> Warnings = new();
        public readonly List<string> Errors = new();

        public override string ToString()
        {
            if (!Applied) return "No aplicado:\n" + string.Join("\n", Errors);
            var partes = new List<string>();
            if (Done.Count > 0) partes.AddRange(Done);
            if (Diff.Count > 0) partes.Add("Diff: " + string.Join(" · ", Diff));
            if (Warnings.Count > 0) partes.Add("Avisos: " + string.Join(" · ", Warnings));
            return string.Join("\n", partes);
        }
    }

    /// Un elemento que un contexto puede seleccionar desde fuera (por ejemplo, una cinemática).
    [Serializable]
    public sealed class AssistantTarget
    {
        public string id;
        public string name;
    }

    /// Un contexto que puede elegir su elemento activo desde fuera (el puente MCP).
    public interface IAssistantContextSelection
    {
        IReadOnlyList<AssistantTarget> Targets();
        bool Select(string id);
    }

    /// Contrato que implementa cada herramienta (el grafo narrativo, el Director de cinemáticas)
    /// para usar el asistente sin que un paquete conozca al otro.
    public interface IAssistantContext
    {
        /// Identificador estable del contexto (p. ej. "narrative-graph", "cinematics").
        string Id { get; }

        /// Nombre legible de la herramienta (p. ej. "Historia", "Director").
        string Name { get; }

        /// Nombre del elemento abierto ahora mismo (p. ej. "Cap3" o "Cap3_03_ElCarro").
        string TargetName { get; }

        /// True si hay un elemento abierto sobre el que conversar.
        bool HasTarget { get; }

        /// Texto de ayuda en la caja de entrada.
        string Placeholder { get; }

        /// Explicación corta cuando aún no se ha configurado la clave de API.
        string Intro { get; }

        /// Se dispara cuando cambia el estado del elemento activo (por ejemplo, tras aplicar).
        event Action Changed;

        /// Instrucciones base del rol y formato de las operaciones JSON.
        string RolePrompt();

        /// Manual de referencia de la herramienta (también consumible por MCP en el futuro).
        string Manual();

        /// Estado actual del elemento seleccionado (capítulo/informe o guion/encargos).
        string State();

        /// Resume en una línea legible una operación individual del array "ops".
        string DescribeOp(JObject op);

        /// Valida y aplica el bloque JSON {"ops":[...]} completo o nada si hay errores.
        AssistantApplyResult Apply(string json);

        /// Etiqueta opcional de un botón de acción rápida en la cabecera (null si no aplica).
        string QuickActionLabel { get; }

        /// Prompt que se envía al pulsar el botón de acción rápida (null si no aplica).
        string QuickActionPrompt();

        /// Avisa de que el elemento activo o su estado ha cambiado fuera del chat.
        void NotifyChanged();
    }

    /// Registro de contextos. Las herramientas con ventana propia se apuntan al abrirse; las que
    /// se pueden construir sin estado se descubren solas por TypeCache.
    public static class AssistantContexts
    {
        private static readonly List<IAssistantContext> s_registered = new();
        private static List<IAssistantContext> s_discovered;

        public static event Action Changed;

        public static IReadOnlyList<IAssistantContext> All
        {
            get
            {
                s_discovered ??= Discover();
                var all = new List<IAssistantContext>(s_registered.Count + s_discovered.Count);
                foreach (var context in s_registered)
                    if (context != null) all.Add(context);
                foreach (var context in s_discovered)
                    if (context != null && !all.Contains(context)) all.Add(context);
                return all;
            }
        }

        public static void Register(IAssistantContext context)
        {
            if (context == null || s_registered.Contains(context)) return;
            s_registered.Add(context);
            Changed?.Invoke();
        }

        public static void Unregister(IAssistantContext context)
        {
            if (context == null || !s_registered.Remove(context)) return;
            Changed?.Invoke();
        }

        public static IAssistantContext Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var context in All)
                if (context != null && context.Id == id) return context;
            return null;
        }

        private static List<IAssistantContext> Discover()
        {
            var list = new List<IAssistantContext>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IAssistantContext>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is IAssistantContext context) list.Add(context);
                }
                catch (Exception)
                {
                    // Un contexto que no se puede construir no debe tumbar la ventana.
                }
            }
            return list;
        }
    }
}
