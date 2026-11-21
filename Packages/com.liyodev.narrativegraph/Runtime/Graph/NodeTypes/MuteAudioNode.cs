using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Silencia un bus de audio o lo devuelve a su volumen. No toca el volumen que eligió el
    /// jugador: pide una atenuación con clave (INarrativeAudioMix) y, al quitarla, el bus vuelve
    /// exactamente al volumen del jugador. Comparte la clave con el nodo «Volumen», así que
    /// cualquiera de los dos deshace lo que dejó el otro con la misma clave.
    [Serializable]
    [NarrativeNodeInfo("Audio y cine", "Silenciar", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class MuteAudioNode : NarrativeNode
    {
        [SerializeField] private NarrativeAudioBus bus = NarrativeAudioBus.Master;
        [Tooltip("Activo: silencia el bus. Inactivo: quita el silencio (y cualquier volumen puesto con la misma clave).")]
        [SerializeField] private bool mute = true;
        [Tooltip("Quién pide la atenuación. Dos nodos con la misma clave y el mismo bus se pisan entre sí; con claves distintas manda la más baja.")]
        [SerializeField] private string clave = SetAudioVolumeNode.ClavePorDefecto;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (NarrativeServices.TryGet(out INarrativeAudioMix mix, nameof(MuteAudioNode)))
            {
                if (mute) mix.AttenuateBus(clave, bus, 0f);
                else mix.ReleaseBusAttenuation(clave, bus);
            }
            onReadyToAdvance?.Invoke();
        }

        public override void CollectWarnings(List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(clave)) warnings.Add("Falta la clave: sin ella el nodo no hace nada.");
        }
    }
}
