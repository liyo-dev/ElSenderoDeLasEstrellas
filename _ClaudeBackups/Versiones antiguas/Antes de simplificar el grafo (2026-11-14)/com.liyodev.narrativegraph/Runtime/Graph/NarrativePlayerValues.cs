using System;
using System.Collections.Generic;

/// Valores del jugador que fija un punto del grafo: qué campos de la ficha del jugador del juego
/// cambia y con qué valor. El grafo no sabe qué son esos campos; los aplica el editor al probar
/// (ver NarrativePlayerSheet en el editor).
/// Un paso que dice cómo está el jugador al llegar a él (lo lee el arranque de pruebas).
public interface INarrativePlayerSetup
{
    NarrativePlayerValues PlayerValues { get; }
}

/// Parte del escritor de estado del juego que sabe poner los valores de un punto de arranque en
/// los datos del jugador. Con ella, al rehacer la historia hasta un paso, cada punto pisa lo
/// anterior en su sitio y la historia que viene después sigue sumando encima.
public interface INarrativePlayerValuesState
{
    void ApplyPlayerValues(NarrativePlayerValues values);
}

[Serializable]
public sealed class NarrativePlayerValues
{
    /// Rutas de los campos que se fijan (las del SerializedObject de la ficha: "maxHP", "abilities.fly").
    public List<string> fields = new();

    /// Los valores, como el JSON del editor de un objeto del tipo de la ficha. Solo cuentan los
    /// campos de <see cref="fields"/>.
    public string data = string.Empty;

    public bool IsEmpty => fields == null || fields.Count == 0;

    public bool Has(string field) => fields != null && fields.Contains(field);
}
