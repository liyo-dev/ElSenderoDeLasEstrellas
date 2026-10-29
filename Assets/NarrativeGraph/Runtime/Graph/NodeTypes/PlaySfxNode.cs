using System;
using UnityEngine;

/// Efecto de sonido sin posición.
[Serializable]
[NarrativeNodeInfo("Audio y cine", "Efecto de sonido", "")]
public sealed class PlaySfxNode : NarrativeNode
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip == null)
            Debug.LogWarning("[PlaySfxNode] No AudioClip asignado.");
        else if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(PlaySfxNode)))
            audio.PlaySfx(clip, volume);

        onReadyToAdvance?.Invoke();
    }
}
