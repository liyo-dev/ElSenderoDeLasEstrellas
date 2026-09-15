using System;
using UnityEngine;

[Serializable]
[NarrativeNodeInfo("Audio y cine", "Voz", "")]
public sealed class PlayVoiceNode : NarrativeNode
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip != null)
            AudioService.Instance?.PlayVoice(clip, volume);
        else
            Debug.LogWarning("[PlayVoiceNode] No AudioClip asignado.");

        onReadyToAdvance?.Invoke();
    }
}
