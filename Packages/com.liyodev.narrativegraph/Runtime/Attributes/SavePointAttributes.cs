using System;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Marca un nodo como punto seguro para guardar.
    /// Los nodos sin este atributo pueden causar problemas si se guarda durante su ejecución.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SavePointAttribute : Attribute
    {
        public string Description { get; }

        public SavePointAttribute(string description = "")
        {
            Description = description;
        }

        static readonly System.Collections.Generic.Dictionary<Type, bool> s_porTipo = new();

        /// <summary>true si el tipo de paso lleva <see cref="SavePointAttribute"/>. Se consulta una vez por tipo.</summary>
        public static bool IsOn(Type nodeType)
        {
            if (nodeType == null) return false;
            if (!s_porTipo.TryGetValue(nodeType, out var marcado))
            {
                marcado = IsDefined(nodeType, typeof(SavePointAttribute), false);
                s_porTipo[nodeType] = marcado;
            }
            return marcado;
        }

        /// <summary>
        /// true si se puede guardar mientras <paramref name="node"/> está en curso: su tipo es un punto
        /// seguro y el paso no está marcado «no guardar» (<see cref="NarrativeNode.blockSaving"/>).
        /// </summary>
        /// En «Pedir al juego» cuenta la petición: la marca va en el tipo de la petición (<see cref="NarrativeNode.KindType"/>).
        public static bool IsSafe(NarrativeNode node)
            => node != null && !node.BlockSaving && (IsOn(node.GetType()) || IsOn(node.KindType));
    }

    /// <summary>
    /// Marca un nodo como NO SEGURO para guardar.
    /// El sistema debería advertir si se intenta guardar durante la ejecución de estos nodos.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class UnsafeForSaveAttribute : Attribute
    {
        public string Reason { get; }

        public UnsafeForSaveAttribute(string reason = "")
        {
            Reason = reason;
        }
    }
}
