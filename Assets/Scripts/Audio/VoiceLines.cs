using UnityEngine;

/// Resuelve voces opcionales por clave de localización e idioma actual.
public static class VoiceLines
{
    /// Reproduce una toma opcional; el servicio sustituye la voz anterior y aplica ducking.
    public static float TryPlay(string textKey, float volume = 1f)
    {
        if (string.IsNullOrWhiteSpace(textKey)) return 0f;
        var audio = AudioService.Instance;
        if (audio == null || !TryGet(textKey, out var clip)) return 0f;
        audio.PlayVoice(clip, volume);
        return clip.length;
    }

    public static bool TryGet(string key, out AudioClip clip)
    {
        clip = null;
        var localization = LocalizationManager.Instance;
        if (localization == null || string.IsNullOrWhiteSpace(key)) return false;

        clip = Resources.Load<AudioClip>($"Voices/{localization.CurrentLocale}/{key}");
        return clip != null;
    }
}
