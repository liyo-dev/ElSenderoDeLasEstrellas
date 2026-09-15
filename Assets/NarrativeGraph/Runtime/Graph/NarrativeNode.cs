using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class NarrativeNode
{
    public enum PortAnchor
    {
        Left,
        Right,
        Top,
        Bottom
    }

    // Título visible en la card
    public string displayTitle;

    // Capítulo al que pertenece este nodo (para organización visual en el editor)
    public string chapter = "";

    [Tooltip("Si esta marcado, el jugador NO podra guardar partida mientras esta en este nodo narrativo.")]
    public bool blockSaving = false;

    // Campos legacy (ocultos) para mantener compatibilidad con grafos existentes.
    [HideInInspector] public PortAnchor inputAnchor = PortAnchor.Left;
    [HideInInspector] public PortAnchor outputAnchor = PortAnchor.Right;
    [HideInInspector] public bool overrideNodeColor;
    [HideInInspector] public Color nodeColor = new Color(0.35f, 0.35f, 0.35f);

    // Técnicos (ocultos en inspector normal)
    [HideInInspector] public string guid = Guid.NewGuid().ToString();
    [HideInInspector] public Vector2 position;
    [HideInInspector] public List<string> outputs = new();

    public abstract void Enter(NarrativeContext ctx, Action onReadyToAdvance);
    public virtual void Exit(NarrativeContext ctx) {}

    // ─────────────────────────────────────────────────────────────────────────
    // Puertos de salida con nombre (Septiembre 2026 — sistema narrativo único)
    //
    // Hasta ahora un nodo tenía UN solo puerto de salida y varias aristas desde él
    // significaban FORK (todas las ramas corren en paralelo). Eso hacía imposible
    // expresar una bifurcación real ("si la quest está activa → A, si no → B").
    //
    // Un nodo que devuelva aquí un array de nombres declara N puertos de salida
    // independientes: `outputs[i]` es el destino del puerto i (cadena vacía = sin
    // conectar). El nodo elige por qué puerto avanzar llamando a
    // AdvanceThrough(ctx, ready, i). El runner NUNCA trata esos nodos como fork.
    //
    // null (por defecto) = comportamiento legacy: un puerto, multi-arista = fork.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Nombres de los puertos de salida, o null si el nodo usa el puerto único legacy.</summary>
    public virtual string[] GetOutputPorts() => null;

    /// <summary>true si el nodo declara puertos con nombre (bifurcación real, nunca fork).</summary>
    public bool HasNamedOutputs => GetOutputPorts() != null;

    /// <summary>Destino (guid) del puerto i, o null si no está conectado.</summary>
    public string GetOutputGuid(int portIndex)
    {
        if (outputs == null || portIndex < 0 || portIndex >= outputs.Count) return null;
        var g = outputs[portIndex];
        return string.IsNullOrEmpty(g) ? null : g;
    }

    /// <summary>
    /// Avanza por el puerto indicado. Registra la elección en el runner y luego invoca
    /// el callback de "listo para avanzar" que recibió Enter().
    /// </summary>
    protected void AdvanceThrough(NarrativeContext ctx, Action onReadyToAdvance, int portIndex)
    {
        ctx?.Runner?.SelectOutput(this, portIndex);
        onReadyToAdvance?.Invoke();
    }
}

public sealed class NarrativeContext
{
    public NarrativeGraph Graph;
    public NarrativeRunner Runner;
    public SimpleBlackboard Blackboard;
    public ExposedPropertyTable Exposed;
    public INarrativeSignals Signals;
}
