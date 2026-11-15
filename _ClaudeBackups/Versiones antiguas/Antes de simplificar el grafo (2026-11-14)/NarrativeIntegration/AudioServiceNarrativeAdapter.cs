using UnityEngine;

/// <summary>Lo que el grafo pide de sonido, atendido por <see cref="AudioService"/>.</summary>
public sealed class AudioServiceNarrativeAdapter : INarrativeAudio, INarrativeAudioMix
{
    public void PlayMusic(AudioClip clip, float fadeSeconds) => AudioService.Instance?.PlayMusic(clip, fadeSeconds);

    public void StopMusic(float fadeSeconds) => AudioService.Instance?.StopMusic(fadeSeconds);

    public void PlaySfx(AudioClip clip, float volume) => AudioService.Instance?.PlaySFX(clip, volume);

    public void PlayVoice(AudioClip clip, float volume) => AudioService.Instance?.PlayVoice(clip, volume);

    public void RequestLocationMusic(float fadeSeconds) => AudioService.Instance?.PedirMusicaDelLugar(fadeSeconds);

    public void AttenuateBus(string key, NarrativeAudioBus bus, float volume01)
        => AudioService.Instance?.AtenuarBus(key, ToAudioBus(bus), volume01);

    public void ReleaseBusAttenuation(string key, NarrativeAudioBus bus)
        => AudioService.Instance?.SoltarAtenuacionDeBus(key, ToAudioBus(bus));

    static AudioBus ToAudioBus(NarrativeAudioBus bus) => bus switch
    {
        NarrativeAudioBus.Music => AudioBus.Music,
        NarrativeAudioBus.Sfx => AudioBus.Sfx,
        NarrativeAudioBus.Dialogue => AudioBus.Dialogue,
        _ => AudioBus.Master,
    };
}
