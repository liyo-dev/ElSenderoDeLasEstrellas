using System;

[Serializable]
[NarrativeNodeInfo("Audio y cine", "Parar música", "")]
public sealed class StopMusicNode : NarrativeNode
{
     public float fadeSeconds = 0.5f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        AudioService.Instance?.StopMusic(fadeSeconds);
        onReadyToAdvance?.Invoke();
    }
}

