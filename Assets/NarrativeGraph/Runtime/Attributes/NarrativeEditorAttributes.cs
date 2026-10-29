using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// Atributos de metadatos para el editor del grafo narrativo.
//
// Viven en Runtime (no en Editor) porque se aplican sobre los propios nodos y sus
// campos, que compilan en runtime. Son solo datos: el runtime nunca los lee en un
// hot path (el editor los consulta por reflection, que en editor es aceptable).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Describe un tipo de nodo para el editor: categoría (color/agrupación en la paleta),
/// nombre legible en español y una frase de ayuda.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NarrativeNodeInfoAttribute : Attribute
{
    public string Category { get; }
    public string Title { get; }
    public string Description { get; }

    public NarrativeNodeInfoAttribute(string category, string title, string description = "")
    {
        Category = category;
        Title = title;
        Description = description;
    }
}

/// <summary>Tipos de clave que el editor sabe resolver contra el proyecto.</summary>
public enum NarrativeKeyKind
{
    /// <summary>Id de una misión.</summary>
    Quest,
    /// <summary>conditionId de un paso de la quest referenciada por el campo hermano indicado.</summary>
    QuestStep,
    /// <summary>Clave de señal custom (INarrativeSignals).</summary>
    Signal,
    /// <summary>Id persistente de un actor.</summary>
    Actor,
    /// <summary>Clave de un texto localizable.</summary>
    LocKey,
    /// <summary>Flag narrativo persistente (NarrativeFlags).</summary>
    Flag,
    /// <summary>Marca con nombre del mundo (punto de aparición).</summary>
    Anchor,
    /// <summary>Clave libre (solo texto), sin resolución.</summary>
    None
}

/// <summary>
/// Qué hace con la señal el componente que tiene el campo. Lo lee el índice del grafo para saber
/// quién escucha y quién emite cada señal desde escenas y prefabs.
/// </summary>
public enum SignalRole
{
    /// <summary>No se declara (el índice lo deduce por el nombre del campo).</summary>
    Ninguno,
    /// <summary>El componente levanta esta señal.</summary>
    Emite,
    /// <summary>El componente espera esta señal.</summary>
    Escucha
}

/// <summary>
/// Marca un campo string como "clave de X": el editor lo dibuja con un desplegable
/// alimentado por el índice del proyecto, y el validador comprueba que resuelve.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class NarrativeKeyAttribute : PropertyAttribute
{
    public NarrativeKeyKind Kind { get; }

    /// <summary>
    /// Para <see cref="NarrativeKeyKind.QuestStep"/>: nombre del campo hermano que contiene el questId.
    /// </summary>
    public string QuestIdField { get; }

    /// <summary>
    /// Para <see cref="NarrativeKeyKind.Signal"/> en componentes de escena o prefab: si el campo es
    /// una señal que el componente emite o escucha.
    /// </summary>
    public SignalRole Rol { get; set; }

    public NarrativeKeyAttribute(NarrativeKeyKind kind, string questIdField = null)
    {
        Kind = kind;
        QuestIdField = questIdField;
    }
}
