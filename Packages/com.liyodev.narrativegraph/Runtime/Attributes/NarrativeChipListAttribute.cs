using System;

/// <summary>
/// Marca un campo List&lt;enum&gt; de un nodo para que el panel del editor lo edite como etiquetas
/// con botón de quitar y un desplegable para añadir (sin repetir valores ni ofrecer «None»), en vez
/// de la lista en crudo. Solo son datos para el editor: el runtime no lo lee.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class NarrativeChipListAttribute : Attribute
{
    /// <summary>Título encima de las etiquetas («Habilidades a desbloquear»).</summary>
    public string Title { get; }

    public NarrativeChipListAttribute(string title = null)
    {
        Title = title;
    }
}
