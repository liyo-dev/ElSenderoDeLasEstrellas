#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Ajusta los imports de audio del proyecto a la loudness que se midió (INC-786).
///
/// Lo importante: las voces se importaban con Force To Mono + Normalize, y Normalize
/// lleva cada clip a −1 dBFS de pico. Eso aplasta la interpretación (un susurro y un
/// grito acaban al mismo nivel) y por eso las 255 líneas del prólogo medían 20 dB de
/// dispersión. Aquí se quita ese Normalize: la loudness la fija el propio audio.
///
/// La música pasa a Streaming: son pistas de 2-4 minutos y en DecompressOnLoad ocupan
/// memoria y se cortan al cargar (INC-168).
public static class AjustarImportsDeAudio
{
    const string CARPETA_VOCES = "Assets/Resources/Voices";
    const string CARPETA_MUSICA = "Assets/Audio/Music";
    const string CARPETA_SFX = "Assets/Audio/SFX_Hechizos";

    [MenuItem("El Sendero/Audio/Ajustar imports de audio (voces, música y SFX)")]
    public static void Aplicar()
    {
        int voces = AjustarVoces();
        int musica = AjustarMusica();
        int sfx = AjustarSfx();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Imports ajustados: {voces} voces (sin Normalize), {musica} pistas de música (Streaming), {sfx} SFX de hechizos (DecompressOnLoad).");
    }

    [MenuItem("El Sendero/Audio/Informar de los imports de audio")]
    public static void Informar()
    {
        int voces = 0, vocesNormalizadas = 0, musica = 0, musicaEnMemoria = 0, sfx = 0;
        foreach (var path in Rutas(CARPETA_VOCES, ".mp3", ".wav"))
        {
            voces++;
            if (Normaliza(path)) vocesNormalizadas++;
        }
        foreach (var path in Rutas(CARPETA_MUSICA, ".mp3", ".wav"))
        {
            musica++;
            if (!EsStreaming(path)) musicaEnMemoria++;
        }
        foreach (var path in Rutas(CARPETA_SFX, ".mp3", ".wav")) sfx++;

        Debug.Log($"[Audio] Voces: {voces} ({vocesNormalizadas} con Normalize activado — hay que quitárselo). " +
                  $"Música: {musica} ({musicaEnMemoria} cargadas en memoria — mejor Streaming). SFX de hechizos: {sfx}.");
    }

    static List<string> Rutas(string carpeta, params string[] extensiones)
    {
        var resultado = new List<string>();
        if (!Directory.Exists(carpeta)) return resultado;
        foreach (var baseDir in Directory.GetDirectories(carpeta, "*", SearchOption.AllDirectories))
            foreach (var extension in extensiones)
                resultado.AddRange(Directory.GetFiles(baseDir, "*" + extension));
        return resultado;
    }

    static AudioImporter Importador(string path)
        => AssetImporter.GetAtPath(path) as AudioImporter;

    static bool Normaliza(string path)
    {
        var importador = Importador(path);
        if (importador == null || !importador.forceToMono) return false;
        var serializado = new SerializedObject(importador);
        var propiedad = serializado.FindProperty("normalize");
        return propiedad != null && propiedad.boolValue;
    }

    /// En Unity 6 el *Normalize* no está en el API público del importador: se toca por
    /// la propiedad serializada. Sin esto, cada voz se importa a −1 dBFS de pico.
    static bool QuitarNormalize(AudioImporter importador)
    {
        var serializado = new SerializedObject(importador);
        var propiedad = serializado.FindProperty("normalize");
        if (propiedad == null) return false;
        if (!propiedad.boolValue) return false;
        propiedad.boolValue = false;
        serializado.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool EsStreaming(string path)
    {
        var importador = Importador(path);
        return importador != null && importador.defaultSampleSettings.loadType == AudioClipLoadType.Streaming;
    }

    static int AjustarVoces()
    {
        int cambios = 0;
        foreach (var path in Rutas(CARPETA_VOCES, ".mp3", ".wav"))
        {
            var importador = Importador(path);
            if (importador == null) continue;
            var ajustes = importador.defaultSampleSettings;
            bool yaEsta = importador.forceToMono && !Normaliza(path)
                          && ajustes.loadType == AudioClipLoadType.CompressedInMemory;
            if (yaEsta) continue;
            importador.forceToMono = true;
            QuitarNormalize(importador);      // la loudness la fija el audio, no el pico
            ajustes.loadType = AudioClipLoadType.CompressedInMemory;
            ajustes.preloadAudioData = true;
            ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
            importador.defaultSampleSettings = ajustes;
            importador.SaveAndReimport();
            cambios++;
        }
        return cambios;
    }

    static int AjustarMusica()
    {
        int cambios = 0;
        foreach (var path in Rutas(CARPETA_MUSICA, ".mp3", ".wav"))
        {
            var importador = Importador(path);
            if (importador == null) continue;
            var ajustes = importador.defaultSampleSettings;
            if (ajustes.loadType == AudioClipLoadType.Streaming) continue;
            ajustes.loadType = AudioClipLoadType.Streaming;
            ajustes.preloadAudioData = false;
            ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
            importador.defaultSampleSettings = ajustes;
            importador.forceToMono = false;
            importador.SaveAndReimport();
            cambios++;
        }
        return cambios;
    }

    static int AjustarSfx()
    {
        int cambios = 0;
        foreach (var path in Rutas(CARPETA_SFX, ".mp3", ".wav"))
        {
            var importador = Importador(path);
            if (importador == null) continue;
            var ajustes = importador.defaultSampleSettings;
            if (ajustes.loadType == AudioClipLoadType.DecompressOnLoad) continue;
            ajustes.loadType = AudioClipLoadType.DecompressOnLoad;
            ajustes.preloadAudioData = true;
            importador.defaultSampleSettings = ajustes;
            importador.SaveAndReimport();
            cambios++;
        }
        return cambios;
    }
}
#endif
