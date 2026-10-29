using System;
using UnityEngine;

/// Pone una pista de música con fundido. La música siempre suena en bucle.
[Serializable]
[NarrativeNodeInfo("Audio y cine", "Música", "")]
public sealed class PlayMusicNode : NarrativeNode
{
    public AudioClip clip;
    [Min(0f)] public float fadeSeconds = 0.5f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip == null)
            Debug.LogWarning("[PlayMusicNode] No AudioClip asignado.");
        else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlayMusicNode)))
            audio.PlayMusic(clip, fadeSeconds);

        onReadyToAdvance?.Invoke();
    }
}
