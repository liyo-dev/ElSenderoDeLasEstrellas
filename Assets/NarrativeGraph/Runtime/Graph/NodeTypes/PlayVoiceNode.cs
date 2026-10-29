using System;
using UnityEngine;

/// Voz sin posición (por el bus de diálogo).
[Serializable]
[NarrativeNodeInfo("Audio y cine", "Voz", "")]
public sealed class PlayVoiceNode : NarrativeNode
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip == null)
            Debug.LogWarning("[PlayVoiceNode] No AudioClip asignado.");
        else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlayVoiceNode)))
            audio.PlayVoice(clip, volume);

        onReadyToAdvance?.Invoke();
    }
}
