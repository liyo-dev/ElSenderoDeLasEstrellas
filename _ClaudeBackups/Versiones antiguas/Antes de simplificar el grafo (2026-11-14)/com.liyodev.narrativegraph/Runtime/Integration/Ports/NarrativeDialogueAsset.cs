using UnityEngine;

/// <summary>
/// Diálogo que el grafo puede reproducir y enseñar en el editor. El juego hace que su propio asset
/// de diálogo herede de aquí; así los nodos guardan una referencia de este tipo sin conocer el
/// formato de las líneas.
/// </summary>
public abstract class NarrativeDialogueAsset : ScriptableObject
{
    /// <summary>Número de líneas. Un diálogo sin líneas no se reproduce.</summary>
    public abstract int LineCount { get; }

    /// <summary>Resumen corto para la cabecera de la tarjeta del editor («3 líneas»).</summary>
    public virtual string Summary => $"{LineCount} línea{(LineCount == 1 ? "" : "s")}";

    /// <summary>
    /// Declara cada línea a las herramientas (tarjeta, índice, búsqueda) con
    /// <see cref="INarrativeNodeDescriber.DialogueLine"/>.
    /// </summary>
    public abstract void DescribeLines(INarrativeNodeDescriber d);
}
