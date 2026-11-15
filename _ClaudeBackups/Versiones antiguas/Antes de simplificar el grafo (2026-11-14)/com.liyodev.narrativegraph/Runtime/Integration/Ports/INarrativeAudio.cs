using UnityEngine;

/// <summary>Música, efectos y voces que piden los nodos.</summary>
public interface INarrativeAudio
{
    /// <summary>Pone una pista de música (en bucle) con fundido.</summary>
    void PlayMusic(AudioClip clip, float fadeSeconds);

    void StopMusic(float fadeSeconds);

    void PlaySfx(AudioClip clip, float volume);

    void PlayVoice(AudioClip clip, float volume);

    /// <summary>
    /// Pide la música que corresponde al lugar donde está el jugador (zona, interior o escena). Si
    /// otro sistema pone la suya a la vez, manda la de ese sistema.
    /// </summary>
    void RequestLocationMusic(float fadeSeconds);
}

/// <summary>Buses de mezcla que un nodo puede atenuar.</summary>
public enum NarrativeAudioBus
{
    Master = 0,
    Music = 1,
    Sfx = 2,
    Dialogue = 3
}

/// <summary>
/// Atenuaciones con clave sobre los buses de mezcla. No pisan el volumen que eligió el jugador:
/// cada clave pide una fracción y, al soltarla, el bus vuelve exactamente a ese volumen. Con
/// varias claves sobre el mismo bus manda la más baja.
/// </summary>
public interface INarrativeAudioMix
{
    void AttenuateBus(string key, NarrativeAudioBus bus, float volume01);

    void ReleaseBusAttenuation(string key, NarrativeAudioBus bus);
}
