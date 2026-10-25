#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Rehornea el guion del prólogo cuando cambia su texto, sin esperar a que alguien se
/// acuerde de pulsar el menú. Se ejecuta al refrescar el editor y se salta Play, donde el
/// horneado no puede abrir la escena.
public static class RehornearPrologoAlCambiar
{
    // El hash guardado evita repetir el horneado cuando el texto no ha cambiado.
    const string CLAVE_HASH = "ElSendero.Audio.HashGuionPrologo.v2";

    [InitializeOnLoadMethod]
    static void Programar()
    {
        EditorApplication.delayCall += Ejecutar;
    }

    static void Ejecutar()
    {
        // En Play no se puede abrir la escena, así que el horneado se aplaza al siguiente
        // refresco (al salir de Play) sin gastar el hash.
        if (EditorApplication.isPlaying) return;

        // Para leer el texto, ruta absoluta (la relativa no resuelve en el proceso del
        // editor). Para hornear, la relativa: el horneador la usa para localizar el asset.
        string rutaAbsoluta = Path.Combine(Application.dataPath, "_SEQUENCES", "Guiones", "Prologo_UltimaNoche.guion.txt");
        if (!File.Exists(rutaAbsoluta)) return;

        string hash = Hash(File.ReadAllText(rutaAbsoluta));
        if (EditorPrefs.GetString(CLAVE_HASH, "") == hash) return;

        EditorPrefs.SetString(CLAVE_HASH, hash);
        HorneadorDeGuion.Hornear(HorneadorDeGuion.RutaPrologo, storyboard: false);
        Debug.Log("[Guion] Prólogo rehorneado (el texto ha cambiado).");
    }

    static string Hash(string texto)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(texto ?? ""))).Replace("-", "");
    }
}
#endif
