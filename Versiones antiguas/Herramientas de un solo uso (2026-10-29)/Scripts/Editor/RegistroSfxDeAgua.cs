using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Da de alta en AudioGraphProfile las claves de sonido del nado con clips del pack que ya está
/// en el proyecto: Agua_Zambullida (Water_Splash-013), Agua_Entrada (Water_Splash-003) y
/// Agua_SalirAFlote (Bubble-001). Se ejecuta sola al cargar el Editor y solo añade las claves
/// que falten, así que una clave ya registrada (o cambiada a mano en el perfil) no se toca.
/// Va por AssetDatabase porque el perfil es un asset que Unity tiene cargado (INC-441). Ver INC-512.
/// </summary>
[InitializeOnLoad]
static class RegistroSfxDeAgua
{
    const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";

    static readonly (string clave, string guidClip)[] Entradas =
    {
        ("Agua_Zambullida",  "c4a619d419f485e3c8274090402a9e52"),
        ("Agua_Entrada",     "5613f2e09fe5d491789612c3870d24a3"),
        ("Agua_SalirAFlote", "1c0370f9553392603b24a0fa17629e5a"),
    };

    static RegistroSfxDeAgua()
    {
        EditorApplication.delayCall += Registrar;
    }

    static void Registrar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null)
            return;

        int nuevas = 0;
        foreach (var (clave, guid) in Entradas)
        {
            if (perfil.eventSfx.Exists(e => e != null && string.Equals(e.eventKey, clave, StringComparison.OrdinalIgnoreCase)))
                continue;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null)
                continue;

            perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = clave, sfx = clip });
            nuevas++;
        }

        if (nuevas == 0)
            return;

        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RegistroSfxDeAgua] {nuevas} sonido(s) de agua dados de alta en AudioGraphProfile.");
    }
}
