using System.Collections.Generic;

/// Lugar del mundo expresado por nombre, sin referencias de escena.
///
/// Dos tipos:
///  - World: una marca global del mundo (en este proyecto, el anchorId de un SpawnAnchor).
///  - Scoped: una marca que solo tiene sentido dentro de un contexto con nombre (en este proyecto,
///    las marcas del SequenceStage de una secuencia). Un Scoped sin contexto ("marca local") lo
///    completa quien envuelve al emisor: ver ScopedNarrativeStateWriter. FallbackScope es el
///    contexto del que toma prestadas las marcas si el suyo no existe (una secuencia montada en
///    vivo usa el escenario de su plantilla).
///
/// Quien convierte un lugar en posición real es un INarrativeLocationResolver del juego.
public readonly struct NarrativeLocation
{
    public enum LocationKind { World, Scoped }

    public readonly LocationKind Kind;
    public readonly string Scope;
    public readonly string Mark;
    public readonly string FallbackScope;

    NarrativeLocation(LocationKind kind, string scope, string mark, string fallbackScope = null)
    {
        Kind = kind;
        Scope = scope ?? string.Empty;
        Mark = mark ?? string.Empty;
        FallbackScope = fallbackScope ?? string.Empty;
    }

    /// Marca global del mundo.
    public static NarrativeLocation World(string mark) => new(LocationKind.World, null, mark);

    /// Marca dentro del contexto indicado.
    public static NarrativeLocation InScope(string scope, string mark, string fallbackScope = null)
        => new(LocationKind.Scoped, scope, mark, fallbackScope);

    /// Marca del contexto actual, todavía sin nombre de contexto.
    public static NarrativeLocation Local(string mark) => new(LocationKind.Scoped, null, mark);

    public bool IsValid => !string.IsNullOrWhiteSpace(Mark);
    public bool IsUnboundLocal => Kind == LocationKind.Scoped && string.IsNullOrEmpty(Scope);

    public override string ToString()
        => Kind == LocationKind.World ? $"marca '{Mark}'" : $"marca '{Mark}' de '{(IsUnboundLocal ? "?" : Scope)}'";
}

/// Estado del mundo que se va construyendo al proyectar el grafo (NarrativeStateProjector).
///
/// Es un contrato genérico: lo que cualquier historia puede dejar cambiado (flags, misiones,
/// objetos, dónde está cada actor). Cada juego lo implementa escribiendo en su propio formato de
/// partida. Lo específico de un juego concreto (habilidades, hechizos…) no entra aquí: se pide con
/// TryGetExtension a una interfaz propia del juego, que el escritor implementa si la conoce.
public interface INarrativeStateWriter
{
    bool GetFlag(string key);
    void SetFlag(string key, bool value);

    void StartQuest(string questId);

    /// Pasos completados de una misión, por id de condición y/o por índice.
    void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices);

    void CompleteQuest(string questId);

    void AddItem(string itemId, int amount);

    /// Dónde queda el actor. Si varios nodos lo colocan, manda el último proyectado.
    void PlaceActor(string actorId, NarrativeLocation location);

    void SetActorActive(string actorId, bool active);

    /// Aviso para quien proyecta: algo no se ha podido deducir y hay que revisarlo a mano.
    void Note(string message);

    /// Acceso a una parte del estado propia de un juego. false si este escritor no la conoce.
    bool TryGetExtension<T>(out T extension) where T : class;
}

/// Convierte lugares con nombre en posiciones reales. Lo implementa cada juego.
public interface INarrativeLocationResolver
{
    bool TryResolve(NarrativeLocation location, out UnityEngine.Vector3 position, out UnityEngine.Quaternion rotation);
}
