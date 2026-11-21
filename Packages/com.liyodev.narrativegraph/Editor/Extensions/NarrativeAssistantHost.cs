using System;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor
{
    /// <summary>
    /// Lo que el editor del grafo usa del asistente de IA. Lo implementa el ensamblado
    /// Liyodev.NarrativeGraph.Editor.Assistant, que solo se compila si el paquete Liyodev Assistant
    /// (com.liyodev.assistant) está en el proyecto: sin él, el grafo funciona entero y solo faltan el
    /// chat y las herramientas externas.
    /// </summary>
    public interface INarrativeAssistantHost
    {
        /// <summary>Si se enseña el botón del asistente en la barra del editor.</summary>
        bool ButtonVisible { get; }

        /// <summary>Han cambiado los ajustes del asistente (botón visible, IA conectada…).</summary>
        event Action SettingsChanged;

        /// <summary>El panel del chat para la ventana del grafo.</summary>
        VisualElement CreatePanel(Action settingsRequested);

        /// <summary>Controles de la sección «Asistente» de los ajustes.</summary>
        void BuildAssistantSettings(VisualElement section);

        /// <summary>Controles de la sección «Herramientas externas» (el servidor MCP compartido).</summary>
        void BuildOutsideToolsSettings(VisualElement section);
    }

    /// <summary>Encuentra el asistente instalado, si lo hay.</summary>
    public static class NarrativeAssistant
    {
        static INarrativeAssistantHost s_host;
        static bool s_searched;

        /// <summary>El asistente, o null si el paquete no está en el proyecto.</summary>
        public static INarrativeAssistantHost Host
        {
            get
            {
                if (s_searched) return s_host;
                s_searched = true;
                s_host = NarrativeEditorExtensions.CreateFirst<INarrativeAssistantHost>();
                return s_host;
            }
        }
    }
}
