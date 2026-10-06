using UnityEditor;

/// Aplica únicamente los ajustes de voz utilizados dentro de cinemáticas.
public static class MezclaCinematicasINC575
{
    [MenuItem("El Sendero/Audio/Mezcla de cinemáticas INC-575")]
    public static void Aplicar()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioGraphProfile"))
        {
            var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (perfil == null) continue;
            Undo.RecordObject(perfil, "Mezcla de voces en cinemáticas");
            perfil.cinematicVoiceDuck ??= new AudioGraphProfile.VoiceDuckSettings();
            var ajustes = perfil.cinematicVoiceDuck;
            ajustes.enabled = true;
            ajustes.musicDb = -8f;
            ajustes.ambienceDb = -5f;
            ajustes.sfxDb = -3f;
            ajustes.attackSeconds = 0.15f;
            ajustes.releaseSeconds = 0.6f;
            ajustes.holdAfterVoiceSeconds = 0.25f;
            EditorUtility.SetDirty(perfil);
        }
        AssetDatabase.SaveAssets();
    }
}
