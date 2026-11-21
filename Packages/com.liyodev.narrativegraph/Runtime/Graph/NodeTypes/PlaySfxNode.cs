using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Efecto de sonido sin posición.
    [Serializable]
    [NarrativeNodeInfo("Audio y cine", "Efecto de sonido", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class PlaySfxNode : NarrativeNode
    {
        [SerializeField] private AudioClip clip;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (clip == null)
                Debug.LogWarning("[PlaySfxNode] No AudioClip asignado.");
            else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlaySfxNode)))
                audio.PlaySfx(clip, volume);

            onReadyToAdvance?.Invoke();
        }
    }
}
