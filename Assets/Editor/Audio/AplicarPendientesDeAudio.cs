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
        if (SessionState.GetBool("audioPendientesAplicados", false)) return;
        SessionState.SetBool("audioPendientesAplicados", true);

        try
        {
            AjustarImportsDeAudio.Aplicar();
            RegistrarMusicaDelConjuro.Aplicar();
            RehornearSiHaCambiado();
            CrearEventosDeHechizoSiFalta();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Audio] No se pudieron aplicar los pendientes de audio: {e.Message}");
        }
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
