using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// Plays a Timeline of the scene and waits until it ends. Works with any Unity project, no code:
/// the step finds the PlayableDirector by the name of its object.
[Serializable]
[NarrativeNodeInfo("Cinemáticas", "Reproducir Timeline", "Reproduce una Timeline de la escena y espera a que termine.")]
[UnsafeForSave("Timeline en curso")]
[NarrativeNoWorldEffect]
public sealed class PlayTimelineNode : NarrativeNode
{
    [Tooltip("Name of the object with the PlayableDirector, in any loaded scene.")]
    public string directorObject;

    [Tooltip("Optional: Timeline to play. Empty = the one the director already has.")]
    public PlayableAsset timeline;

    [Tooltip("Lock the player while it plays (needs the Player connection).")]
    public bool lockPlayer = true;

    static readonly string[] Ports = { "Hecho", "No encontrada" };
    public override string[] GetOutputPorts() => Ports;
    public override int ProjectionPort => 0;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line("🎬 " + (string.IsNullOrEmpty(directorObject) ? "(sin director)" : directorObject), NarrativeTextTone.Strong);
        if (timeline != null) d.Field("timeline", timeline.name);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(directorObject)) warnings.Add("Falta el objeto con el PlayableDirector");
    }

    [NonSerialized] PlayableDirector _director;
    [NonSerialized] Action<PlayableDirector> _onStopped;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        _director = Find(directorObject);
        if (_director == null)
        {
            Debug.LogWarning($"[PlayTimeline:{guid}] No PlayableDirector on an object called '{directorObject}'.");
            AdvanceThrough(ctx, ready, 1);
            return;
        }

        var player = lockPlayer ? NarrativeServices.Get<INarrativePlayerControl>() : null;
        player?.LockPlayer(this);
        if (timeline != null) _director.playableAsset = timeline;
        _onStopped = _ =>
        {
            Release();
            player?.UnlockPlayer(this);
            AdvanceThrough(ctx, ready, 0);
        };
        _director.stopped += _onStopped;
        _director.time = 0;
        _director.Play();
    }

    public override void Exit(NarrativeContext ctx) => Release();

    void Release()
    {
        if (_director != null && _onStopped != null) _director.stopped -= _onStopped;
        _onStopped = null;
    }

    static PlayableDirector Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var director in root.GetComponentsInChildren<PlayableDirector>(true))
                    if (director.gameObject.name == name) return director;
        }
        return null;
    }
}
