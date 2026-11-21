using System;

namespace Liyodev.NarrativeGraph
{
    /// Para la música con fundido.
    [Serializable]
    [NarrativeNodeInfo("Audio y cine", "Parar música", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class StopMusicNode : NarrativeNode
    {
        [UnityEngine.SerializeField] private float fadeSeconds = 0.5f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(StopMusicNode)))
                audio.StopMusic(fadeSeconds);
            onReadyToAdvance?.Invoke();
        }
    }
}
