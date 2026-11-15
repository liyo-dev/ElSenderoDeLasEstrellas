using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lo que el paso «Pedir al juego» (<see cref="GameStepNode"/>) le pide al juego: solo datos. El
/// grafo no sabe qué pasa dentro (una cinemática, un combate, un minijuego): lo emite, el juego lo
/// atiende con su manejador (<see cref="NarrativeRequests"/>) y avisa de cómo ha terminado.
///
/// Cada juego declara sus peticiones como clases [Serializable] que heredan de esta, con
/// [NarrativeNodeInfo] para el editor (categoría, nombre y descripción). Lo que la petición deja
/// hecho en el mundo lo declara ella misma con <see cref="INarrativeStateEffect"/> (o
/// <see cref="INarrativeRouteEffect"/> si depende de la salida), o con [NarrativeNoWorldEffect] si
/// no deja nada: es lo único que el grafo sabe de lo que pasó, y con eso rehace la historia al
/// probar desde un paso.
/// </summary>
[Serializable]
public abstract class NarrativeRequest
{
    static readonly string[] DefaultExits = { "Hecho" };

    /// <summary>Id con el que el juego atiende esta petición (el de su manejador).</summary>
    public virtual string Id => GetType().FullName;

    /// <summary>Salidas por las que puede terminar, en orden.</summary>
    public virtual string[] Exits => DefaultExits;

    /// <summary>
    /// Salida por la que sigue la historia al rehacerla sin jugarla:
    /// un índice de <see cref="Exits"/> o <see cref="NarrativeNode.ProjectionStops"/> si es una
    /// decisión de verdad (p. ej. «¿es de noche?»).
    /// </summary>
    public virtual int ProjectionExit => 0;

    /// <summary>Lo que cuenta la petición a las herramientas (tarjeta, índice, validación).</summary>
    public virtual void Describe(INarrativeNodeDescriber d) {}

    /// <summary>Avisos de contenido (algo obligatorio vacío, valores que no casan).</summary>
    public virtual void CollectWarnings(List<string> warnings) {}

    /// <summary>Explicación de la decisión para una persona, si la petición es una decisión.</summary>
    public virtual string DescribeDecision(NarrativeNode step) => null;
}

/// <summary>
/// Petición sin código: un id que atiende algo de la escena (<see cref="NarrativeGameStepHook"/>)
/// o un <see cref="NarrativeGameSteps.Register"/>, con datos libres, salidas con nombre y, escrito
/// a mano, lo que deja hecho.
/// </summary>
[Serializable]
public sealed class NarrativeRequestValues : NarrativeRequest, INarrativeStateEffect
{
    const string DefaultExitName = "Hecho";

    [NarrativeKey(NarrativeKeyKind.GameStep)]
    [Tooltip("Id que atiende el juego: el de un NarrativeGameStepHook de la escena o el que registra el código con NarrativeGameSteps.Register.")]
    public string stepId;

    [Tooltip("Texto libre que recibe el juego (un nombre, una clave…).")]
    public string text;

    [Tooltip("Número libre que recibe el juego.")]
    public float number;

    [Tooltip("Asset que recibe el juego (un ScriptableObject suyo, un prefab…).")]
    public UnityEngine.Object asset;

    [Tooltip("Salidas por las que puede terminar el juego, en orden. Vacío = una sola, «Hecho».")]
    public List<string> exitNames = new() { DefaultExitName };

    [Tooltip("Espera a que el juego avise de que ha terminado. Si no, lo pide y sigue en el acto por la primera salida.")]
    public bool waitUntilFinished = true;

    [Tooltip("Pone la salida «Sin respuesta» para cuando nadie lo atiende. Si no, en ese caso sigue por la primera salida.")]
    public bool separateFallbackExit = true;

    [Tooltip("Lo que deja hecho en el mundo, para rehacer la historia sin jugarla (categoría, clave y valor; las categorías las define el juego).")]
    public List<NarrativeFact> leaves = new();

    public override string Id => stepId;

    public override string[] Exits
    {
        get
        {
            int count = exitNames?.Count ?? 0;
            if (count == 0) return new[] { DefaultExitName };
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = string.IsNullOrWhiteSpace(exitNames[i]) ? $"Salida {i + 1}" : exitNames[i];
            return names;
        }
    }

    public void Project(INarrativeStateWriter state)
    {
        if (leaves == null) return;
        foreach (var fact in leaves)
            if (fact != null && !string.IsNullOrWhiteSpace(fact.category) && !string.IsNullOrWhiteSpace(fact.key))
                state.SetFact(fact.category, fact.key, fact.value);
    }

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Field("Pide al juego", string.IsNullOrEmpty(stepId) ? "—" : stepId);
        if (!string.IsNullOrEmpty(text)) d.Field("Texto", text);
        if (number != 0f) d.Field("Número", number.ToString("0.##"));
        if (asset != null) d.Asset(asset, "dato de la petición");
        if (!waitUntilFinished) d.Tag("no espera");
        if (leaves != null)
            foreach (var fact in leaves)
                if (fact != null && !string.IsNullOrWhiteSpace(fact.key)) d.Field("Deja", $"{fact.category}: {fact.key} = {fact.value}");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(stepId)) warnings.Add("Falta el id que atiende el juego");
    }
}

/// <summary>Un hecho que deja una petición: categoría (la define el juego), clave y valor.</summary>
[Serializable]
public sealed class NarrativeFact
{
    [Tooltip("Grupo del hecho, como lo llama el juego («Grupo», «Hora del día», «Vestuario»…).")]
    public string category;
    public string key;
    public string value;
}
