using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Voz sin posición (por el bus de diálogo).
    [Serializable]
    [NarrativeNodeInfo("Audio y cine", "Voz", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class PlayVoiceNode : NarrativeNode
    {
        [SerializeField] private AudioClip clip;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (clip == null)
                Debug.LogWarning("[PlayVoiceNode] No AudioClip asignado.");
            else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlayVoiceNode)))
                audio.PlayVoice(clip, volume);

            onReadyToAdvance?.Invoke();
        }
    }
}
