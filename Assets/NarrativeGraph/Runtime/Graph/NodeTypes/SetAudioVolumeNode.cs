using System;
using System.Collections.Generic;
using UnityEngine;

/// Baja un bus de audio a una fracción del volumen que eligió el jugador. No pisa ese volumen:
/// pide una atenuación con clave (INarrativeAudioMix); con 1 la quita y el bus vuelve
/// exactamente al volumen del jugador.
[Serializable]
[NarrativeNodeInfo("Audio y cine", "Volumen", "")]
public sealed class SetAudioVolumeNode : NarrativeNode
{
    /// Clave que usan los nodos de audio del grafo si no se indica otra.
    public const string ClavePorDefecto = "Grafo narrativo";

    public NarrativeAudioBus bus = NarrativeAudioBus.Master;
    [Tooltip("Fracción del volumen del jugador (1 = el volumen del jugador, 0 = silencio).")]
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Quién pide la atenuación. Dos nodos con la misma clave y el mismo bus se pisan entre sí; con claves distintas manda la más baja.")]
    public string clave = ClavePorDefecto;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (NarrativeServices.TryGet(out INarrativeAudioMix mix, nameof(SetAudioVolumeNode)))
        {
            if (volume >= 1f) mix.ReleaseBusAttenuation(clave, bus);
            else mix.AttenuateBus(clave, bus, volume);
        }
        onReadyToAdvance?.Invoke();
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(clave)) warnings.Add("Falta la clave: sin ella el nodo no hace nada.");
    }
}
