#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Aplica solo lo que estaba pendiente del trabajo de audio (INC-786 a INC-800).
///
/// Se ejecuta en el siguiente refresco del editor: ajusta los imports (la voz sin
/// Normalize y la música en Streaming), da de alta en el perfil las piezas de música
/// del conjuro y rehornea el guion del prólogo si ha cambiado. Así el autor no tiene
/// que acordarse de la lista de clics: basta con que Unity refresque.
public static class AplicarPendientesDeAudio
{
    const string CLAVE_HASH = "ElSendero.Audio.HashGuionPrologo";

    [InitializeOnLoadMethod]
    static void Programar()
    {
        EditorApplication.delayCall += Ejecutar;
    }

    static void Ejecutar()
    {
        // Corre en cada recarga: todo lo que hace es idempotente (los imports se saltan
        // si ya estan bien, el rehorneado solo si el guion cambio, el balance siempre
        // escribe los mismos valores).
        try
        {
            AjustarImportsDeAudio.Aplicar();
            RegistrarMusicaDelConjuro.Aplicar();
            AjustarBalanceDeVoz();
            RehornearSiHaCambiado();
            CrearEventosDeHechizoSiFalta();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Audio] No se pudieron aplicar los pendientes de audio: {e.Message}");
        }
    }

    /// El bus de diálogo no realza la voz (las voces ya están normalizadas a −18 LUFS) y la
    /// música solo se aparta lo justo cuando alguien habla: con +4 dB de voz y −16 dB de
    /// música, la música quedaba 20 dB bajo la voz (INC-804).
    static void AjustarBalanceDeVoz()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        if (profile == null) return;

        Undo.RecordObject(profile, "Balance de voz y musica");
        profile.voiceDuck.voiceGainDb = 0f;
        profile.voiceDuck.musicDb = -6f;
        profile.voiceDuck.ambienceDb = -6f;
        profile.voiceDuck.sfxDb = -4f;
        profile.cinematicVoiceDuck.voiceGainDb = 0f;
        profile.cinematicVoiceDuck.musicDb = -8f;
        profile.cinematicVoiceDuck.ambienceDb = -10f;
        profile.cinematicVoiceDuck.sfxDb = -6f;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    static void CrearEventosDeHechizoSiFalta()
    {
        const string clave = "ElSendero.Audio.EventosDeHechizo";
        if (EditorPrefs.GetString(clave, "") == "v1") return;
        EditorPrefs.SetString(clave, "v1");
        CrearEventosDeHechizo.Aplicar();
    }

    static void RehornearSiHaCambiado()
    {
        string ruta = HorneadorDeGuion.RutaPrologo;
        if (!File.Exists(ruta)) return;
        string hash = Hash(File.ReadAllText(ruta));
        if (EditorPrefs.GetString(CLAVE_HASH, "") == hash) return;

        EditorPrefs.SetString(CLAVE_HASH, hash);
        HorneadorDeGuion.Hornear(ruta, storyboard: false);
        Debug.Log("[Audio] Guion del prólogo rehorneado (cambió desde la última vez).");
    }

    static string Hash(string texto)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(texto ?? ""))).Replace("-", "");
    }
}
#endif
