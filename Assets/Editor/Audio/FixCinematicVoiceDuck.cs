using UnityEditor;
using UnityEngine;

/// Fuerza los valores de cinematicVoiceDuck en AudioGraphProfile y lo guarda de verdad.
public static class FixAudioProfile
{
    [MenuItem("El Sendero/Audio/Fijar cinematicVoiceDuck (INC-827)")]
    public static void Fijar()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        if (profile == null)
        {
            Debug.LogError("[Audio] No se encontró AudioGraphProfile.asset");
            return;
        }

        Undo.RecordObject(profile, "Fijar cinematicVoiceDuck INC-827");

        // Cinematic voice duck: música -3 dB, ambiente -6, SFX -4, attack 0.08
        var duck = profile.cinematicVoiceDuck;
        duck.musicDb = -3f;
        duck.ambienceDb = -6f;
        duck.sfxDb = -4f;
        duck.attackSeconds = 0.08f;
        // releaseSeconds y holdAfterVoiceSeconds se quedan como están (0.65 y 0.5)

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Audio] cinematicVoiceDuck fijado: musicDb=-3, ambienceDb=-6, sfxDb=-4, attack=0.08");
    }
}