using System.Collections.Generic;

/// Estilo de una línea de texto en la descripción de un nodo.
public enum NarrativeTextTone
{
    Normal,
    /// Lo principal del nodo (nombre de la cinemática, id del checkpoint…).
    Strong,
    /// Información secundaria o «sin asignar».
    Muted,
    /// Texto libre de una nota del grafo.
    Note
}

/// Lo que un nodo cuenta de sí mismo a las herramientas: la tarjeta y el panel lateral del editor,
/// la búsqueda, el índice de señales y misiones, los validadores y el lanzador de pruebas.
///
/// El nodo lo declara en NarrativeNode.Describe y cada herramienta implementa esta interfaz
/// quedándose con lo que necesita. Así un tipo de nodo nuevo se ve, se busca y se valida en todas
/// ellas sin que ninguna lo conozca por su nombre.
///
/// Hay dos clases de llamadas:
///  - Referencias: lo que el nodo lee o escribe del mundo por clave (misiones, señales, actores,
///    flags, marcas, textos localizables, assets y objetos). Se declaran aunque estén vacías cuando
///    son obligatorias: quien las consume decide qué hacer (la tarjeta enseña «sin …», el índice
///    las ignora). Las opcionales vacías no se declaran.
///  - Presentación: texto libre para la tarjeta (líneas, pares etiqueta-valor, etiquetas).
///
/// Lo que el nodo deja cambiado en el estado del mundo no va aquí: eso es INarrativeStateEffect.
public interface INarrativeNodeDescriber
{
    /// Misión que toca el nodo. verb: qué hace con ella, para una persona («Se inicia»).
    /// steps: ids de los pasos que marca, si marca alguno.
    void Quest(string questId, string verb, IReadOnlyList<string> steps = null);

    /// Señal que el nodo emite (SignalRole.Emite) o espera (SignalRole.Escucha). label: nombre de
    /// la señal dentro del nodo («entra», «hecho»); sin label es la señal principal del nodo.
    void Signal(string key, SignalRole role, string label = null);

    /// Actor (id persistente de un personaje) al que se refiere el nodo. verb: para una persona.
    void Actor(string actorId, string verb);

    /// Flag narrativo que el nodo lee o escribe. shownAs: cómo enseñarlo («CLAVE = sí»).
    void Flag(string key, string shownAs = null);

    /// Marca del mundo con nombre (punto de aparición) que usa el nodo.
    void Anchor(string anchorId);

    /// Asset del que depende el nodo. kind: qué es, para una persona («diálogo»).
    /// detail: resumen corto de su contenido.
    void Asset(UnityEngine.Object asset, string kind, string detail = null);

    /// Línea de diálogo del último asset declarado. detail: datos extra de la línea (emoción…).
    void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null);

    /// Objeto de inventario con cantidad. verb: para una persona («da», «espera»).
    void Item(UnityEngine.Object item, int amount, string verb);

    /// Texto localizable (clave y texto por defecto). Devuelve el texto que leerá una persona
    /// (traducido si la herramienta sabe traducir, si no el texto por defecto). No se pinta solo:
    /// el nodo usa lo devuelto en sus líneas.
    string Text(string locKey, string fallback);

    /// Línea de texto para la tarjeta.
    void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal);

    /// Nombre de quien habla, encima de la línea siguiente.
    void Speaker(string name);

    /// Par etiqueta-valor para la tarjeta.
    void Field(string label, string value);

    /// Etiqueta corta (botón, categoría…) para la tarjeta.
    void Tag(string text);
}
