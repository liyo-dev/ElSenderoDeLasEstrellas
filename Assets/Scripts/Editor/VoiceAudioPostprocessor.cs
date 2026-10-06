using System;
using UnityEditor;
using UnityEngine;

/// Aplica los ajustes de importación a las voces opcionales de Resources.
public sealed class VoiceAudioPostprocessor : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Voices/", StringComparison.Ordinal)) return;

        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        var settings = importer.defaultSampleSettings;
        settings.preloadAudioData = false;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.defaultSampleSettings = settings;
    }
}
