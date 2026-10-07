#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// «La mañana después» suena con el tema de Eldran. No arranca con la secuencia: la vela va en
/// silencio y el tema entra cuando llega Eldran (MusicBeat en @bollo del guion). Así además dura
/// lo mismo que la escena (184 s de tema) y no vuelve a empezar al final.
public static class Cap2MusicaDeEldran
{
    const string Regla = "MANANA_DESPUES";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Tema de Eldran en la mañana después")]
    public static void Aplicar()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset") ?? throw new InvalidOperationException("Falta el perfil de audio.");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/la-melodia-de-eldran.mp3") ?? throw new InvalidOperationException("Falta la melodía de Eldran.");
        var regla = perfil.GetSequenceRule(Regla);
        if (regla == null) { regla = new AudioGraphProfile.SequenceRule { sequenceId = Regla }; perfil.sequences.Add(regla); }
        regla.music = clip; regla.fadeIn = 2f; regla.fadeOut = 1.5f;
        EditorUtility.SetDirty(perfil);
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>("Assets/_SEQUENCES/SEQ_MananaDespues.asset") ?? throw new InvalidOperationException("Falta SEQ_MananaDespues.");
        def.musicId = "";
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cap2] La mañana después: tema de Eldran listo; entra con Eldran (MusicBeat del guion), no al empezar.");
    }
}
#endif
