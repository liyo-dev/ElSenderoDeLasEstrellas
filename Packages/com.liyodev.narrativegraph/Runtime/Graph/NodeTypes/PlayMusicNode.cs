using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Pone una pista de música con fundido. La música siempre suena en bucle.
    [Serializable]
    [NarrativeNodeInfo("Audio y cine", "Música", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class PlayMusicNode : NarrativeNode
    {
        [SerializeField] private AudioClip clip;
        [Min(0f)] [SerializeField] private float fadeSeconds = 0.5f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (clip == null)
                Debug.LogWarning("[PlayMusicNode] No AudioClip asignado.");
            else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlayMusicNode)))
                audio.PlayMusic(clip, fadeSeconds);

            onReadyToAdvance?.Invoke();
        }
    }
}
