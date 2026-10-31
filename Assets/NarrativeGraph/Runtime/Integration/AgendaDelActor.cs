using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lo que hace un actor en cada tramo de la historia: si está, dónde, y qué dice si se le habla.
/// Gana la primera entrada cuyo tramo esté en curso; si ninguna lo está, la agenda no toca al
/// actor y sigue con su comportamiento normal. Es solo datos: lo aplica <see cref="AgendaDeActores"/>.
/// Las agendas se cargan de cualquier carpeta Resources/Agendas.
/// </summary>
[CreateAssetMenu(menuName = "Narrative Graph/Agenda del actor", fileName = "Agenda_")]
public sealed class AgendaDelActor : ScriptableObject
{
    [Tooltip("Id persistente del actor (el mismo que usan los nodos del grafo).")]
    public string actorId;

    [Tooltip("En orden de prioridad: vale la primera cuyo tramo esté en curso.")]
    public List<EntradaDeAgenda> entradas = new();

    /// <summary>La entrada que vale ahora, o null si ninguna.</summary>
    public EntradaDeAgenda EntradaActual()
    {
        if (entradas == null) return null;
        foreach (var e in entradas)
            if (e != null && (e.tramo == null || e.tramo.EnCurso())) return e;
        return null;
    }
}

/// <summary>Si el actor está en el mundo durante una entrada de su agenda.</summary>
public enum PresenciaEnAgenda
{
    /// <summary>Se queda donde esté.</summary>
    SinCambios,
    /// <summary>Se coloca en una marca al empezar la entrada.</summary>
    EnUnaMarca,
    /// <summary>No está (se ha ido de viaje, aún no ha llegado…).</summary>
    NoEsta,
}

[Serializable]
public sealed class EntradaDeAgenda
{
    [Tooltip("Nombre para reconocerla en el editor (p. ej. «Tras el Cap. 2 · en la capital»).")]
    public string nombre;

    [Tooltip("Cuándo vale esta entrada. Vacío = siempre.")]
    public TramoDeHistoria tramo = new();

    [Tooltip("Si está y dónde. Los cambios se hacen sin que el jugador lo vea.")]
    public PresenciaEnAgenda presencia = PresenciaEnAgenda.SinCambios;

    [Tooltip("Marca del mundo donde se coloca (solo con «En una marca»).")]
    public string marca;

    [Tooltip("Lo que dice si se le habla y el grafo no tiene otra frase para él: una por charla, " +
             "en orden; al acabar vuelve a empezar. Vacío = no dice nada propio.")]
    public List<NarrativeDialogueAsset> frases = new();
}
