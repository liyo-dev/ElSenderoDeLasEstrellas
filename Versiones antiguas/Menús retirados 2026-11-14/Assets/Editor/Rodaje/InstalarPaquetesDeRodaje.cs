#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// Instala los paquetes que necesita el rodaje (tráiler y capturas de escaparate):
/// Unity Recorder, para grabar a fotogramas constantes sin interfaz.
/// Se puede lanzar desde el buzón: «menu El Sendero/Rodaje/Instalar paquetes de rodaje».
/// El resultado queda en «Guiones/_ordenes/paquetes.log» (la petición es asíncrona).
public static class InstalarPaquetesDeRodaje
{
    private static readonly string[] Paquetes = { "com.unity.recorder" };
    private static AddAndRemoveRequest s_peticion;

    [MenuItem("El Sendero/Rodaje/Instalar paquetes de rodaje")]
    public static void Instalar()
    {
        if (s_peticion != null && !s_peticion.IsCompleted) return;
        Escribir("Pidiendo: " + string.Join(", ", Paquetes));
        s_peticion = Client.AddAndRemove(packagesToAdd: Paquetes);
        EditorApplication.update += Revisar;
    }

    private static void Revisar()
    {
        if (s_peticion == null || !s_peticion.IsCompleted) return;
        EditorApplication.update -= Revisar;
        if (s_peticion.Status == StatusCode.Success)
        {
            foreach (var p in s_peticion.Result)
                if (Array.IndexOf(Paquetes, p.name) >= 0) Escribir($"Instalado {p.name}@{p.version}");
        }
        else Escribir("ERROR: " + s_peticion.Error?.message);
        s_peticion = null;
    }

    private static void Escribir(string texto)
    {
        Debug.Log("[Rodaje] " + texto);
        try
        {
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "Guiones", "_ordenes");
            File.AppendAllText(Path.Combine(carpeta, "paquetes.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {texto}\n");
        }
        catch { }
    }
}
#endif
