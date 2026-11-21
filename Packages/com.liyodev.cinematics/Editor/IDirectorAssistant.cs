using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Cinematics
{
    /// La caja de la IA de la Mesa del Director. La implementa el ensamblado
    /// Liyodev.Cinematics.Editor.Assistant, que solo se compila si el paquete Liyodev Assistant
    /// (com.liyodev.assistant) está en el proyecto: sin él, el Director funciona entero sin chat.
    public interface IDirectorAssistant : IDisposable
    {
        /// Hoja de estilos del chat para la ventana (null si no hay).
        string StyleSheetPath { get; }

        /// El chat con el contexto de cinemáticas, o null si no está disponible.
        /// <paramref name="applied"/>: la IA ha aplicado cambios; <paramref name="busyChanged"/>: empieza o acaba de trabajar.
        VisualElement CreateView(Action applied, Action busyChanged);

        /// True mientras la IA está trabajando.
        bool Busy { get; }

        /// Pide a la IA que resuelva los encargos pendientes.
        void SendQuickAction();

        /// Texto del botón de conexión («IA: …» o «Conectar mi IA»).
        string ConnectLabel { get; }

        /// Abre la ventana para elegir o cambiar la IA.
        void OpenConnect();

        /// Han cambiado los ajustes del asistente.
        event Action SettingsChanged;
    }

    /// Encuentra la caja de la IA si el asistente está instalado.
    public static class DirectorAssistant
    {
        /// Una caja nueva por ventana, o null sin asistente.
        public static IDirectorAssistant Create()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDirectorAssistant>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                try
                {
                    if (Activator.CreateInstance(type) is IDirectorAssistant box) return box;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Director] No se pudo crear la caja de la IA ({type.Name}): {e.Message}");
                }
            }
            return null;
        }
    }
}
