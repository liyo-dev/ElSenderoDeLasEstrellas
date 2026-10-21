#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AjustarPerfilDeAudioCinematicas
{
    [MenuItem("El Sendero/Archivo/Audio/Actualizar Perfil de Voces Cinematicas")]
    public static void Aplicar()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        if (profile == null)
        {
            Debug.LogError("[Audio] No se encontró Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
            return;
        }

        Undo.RecordObject(profile, "Actualizar balance de audio en cinematicas");

        // Ducking y realce en cinemáticas
        profile.cinematicVoiceDuck.enabled = true;
        profile.cinematicVoiceDuck.musicDb = -8f;
        profile.cinematicVoiceDuck.ambienceDb = -10f;
        profile.cinematicVoiceDuck.sfxDb = -6f;
        profile.cinematicVoiceDuck.voiceGainDb = 0f;
        profile.cinematicVoiceDuck.attackSeconds = 0.12f;
        profile.cinematicVoiceDuck.releaseSeconds = 0.65f;
        profile.cinematicVoiceDuck.holdAfterVoiceSeconds = 0.5f;

        // Ducking de gameplay
        profile.voiceDuck.enabled = true;
        profile.voiceDuck.musicDb = -6f;
        profile.voiceDuck.ambienceDb = -6f;
        profile.voiceDuck.sfxDb = -4f;
        profile.voiceDuck.voiceGainDb = 0f;
        profile.voiceDuck.attackSeconds = 0.15f;
        profile.voiceDuck.releaseSeconds = 0.6f;
        profile.voiceDuck.holdAfterVoiceSeconds = 0.25f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[Audio] AudioGraphProfile actualizado: la música baja solo -8 dB con la voz (antes -16) y el bus de diálogo ya no realza la voz (antes +4 dB).");
    }

    [MenuItem("El Sendero/Audio/Actualizar Tema Liam")]
    public static void ActualizarTemaLiam()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        if (profile == null) return;
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cinematicas/La Melodia de Liam.mp3");
        if (clip == null)
        {
            Debug.LogError("[Audio] No se encontró La Melodia de Liam.mp3");
            return;
        }
        var regla = profile.GetSequenceRule("LIAM_CRYSTAL");
        if (regla != null)
        {
            Undo.RecordObject(profile, "Actualizar musica LIAM_CRYSTAL");
            regla.music = clip;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] LIAM_CRYSTAL actualizado a La Melodia de Liam.mp3 con éxito.");
        }
    }
}
#endif
