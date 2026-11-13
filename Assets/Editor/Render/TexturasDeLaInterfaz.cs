#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Aligera las texturas de la interfaz (Assets/Art/UI): los iconos se importan a 512 px como mucho,
/// el resto a 1024 salvo las imágenes de pantalla completa (más de 1600 px de ancho), que se quedan
/// en 2048; y todo con compresión de alta calidad (BC7), que no ensucia el papel ni la tinta.
/// Los PNG originales no se tocan: solo cambia cómo los importa Unity. Lo anterior de cada textura
/// se apunta en un registro para volver atrás. Ver INC-1138.
public static class TexturasDeLaInterfaz
{
    private const string Carpeta = "Assets/Art/UI";
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/TexturasDeLaInterfazAntes.json";

    /// Carpetas de iconos: se ven pequeños en pantalla.
    private static readonly string[] CarpetasDeIconos =
    {
        "Attacks", "Items", "Abilities", "NpcStatus", "Wardrobe", "Grimorio", "InputGlyphs", "Iconos",
        "InformeDeVictoria", "Cursor", "DialogueIcons",
    };

    [Serializable] private sealed class Entrada { public string ruta; public int tamano; public int compresion; }
    [Serializable] private sealed class Registro { public List<Entrada> texturas = new List<Entrada>(); }

    [MenuItem("El Sendero/UI/Aligerar las texturas de la interfaz")]
    public static void Aligerar()
    {
        var registro = Leer();
        var apuntadas = new HashSet<string>();
        foreach (var e in registro.texturas) apuntadas.Add(e.ruta);
        int cambiadas = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp)) continue;
                imp.GetSourceTextureWidthAndHeight(out int ancho, out int alto);
                int lado = Mathf.Max(ancho, alto);
                bool icono = EsIcono(ruta);
                int tope = icono ? 512 : (ancho > 1600 ? 2048 : 1024);
                int nuevo = Mathf.Min(imp.maxTextureSize, Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Min(lado, tope))));
                if (nuevo > tope) nuevo = tope;
                var compresion = TextureImporterCompression.CompressedHQ;
                if (imp.maxTextureSize == nuevo && imp.textureCompression == compresion) continue;
                if (apuntadas.Add(ruta))
                    registro.texturas.Add(new Entrada { ruta = ruta, tamano = imp.maxTextureSize, compresion = (int)imp.textureCompression });
                imp.maxTextureSize = nuevo;
                imp.textureCompression = compresion;
                imp.crunchedCompression = false;
                imp.SaveAndReimport();
                cambiadas++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Guardar(registro);
        Debug.Log($"[TexturasDeLaInterfaz] {cambiadas} texturas de la interfaz aligeradas.");
    }

    [MenuItem("El Sendero/UI/Aligerar las texturas de la interfaz: volver a como estaban")]
    public static void Volver()
    {
        var registro = Leer();
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var e in registro.texturas)
            {
                if (!(AssetImporter.GetAtPath(e.ruta) is TextureImporter imp)) continue;
                imp.maxTextureSize = e.tamano;
                imp.textureCompression = (TextureImporterCompression)e.compresion;
                imp.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Guardar(new Registro());
    }

    private static bool EsIcono(string ruta)
    {
        string resto = ruta.Substring(Carpeta.Length + 1);
        foreach (var c in CarpetasDeIconos)
            if (resto.StartsWith(c + "/", StringComparison.Ordinal)) return true;
        return false;
    }

    private static Registro Leer() =>
        File.Exists(RutaDelRegistro) ? JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro() : new Registro();

    private static void Guardar(Registro registro)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelRegistro));
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(registro, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
    }
}
#endif
