#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Asigna clips a claves del perfil de audio con soporte de deshacer.</summary>
public class AsignarSonidoAClave : EditorWindow
{
    private const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private AudioGraphProfile perfil;
    private string claveElegida = "";
    private string claveNueva = "";
    private bool escribirClave;
    private AudioClip clipNuevo;
    private string mensaje;
    private MessageType tipoMensaje;

    [MenuItem("El Sendero/Audio/Asignar sonido a una clave")]
    public static void Abrir()
    {
        var ventana = GetWindow<AsignarSonidoAClave>("Asignar sonido");
        if (Selection.activeObject is AudioClip clip)
            ventana.clipNuevo = clip;
        ventana.Show();
    }

    private void OnEnable()
    {
        perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (Selection.activeObject is AudioClip clip)
            clipNuevo = clip;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
    }

    private void OnGUI()
    {
        if (perfil == null)
        {
            EditorGUILayout.HelpBox($"No se encuentra el perfil: {RutaPerfil}", MessageType.Error);
            return;
        }

        var claves = new List<string>();
        var unicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (perfil.eventSfx != null)
            foreach (var entrada in perfil.eventSfx)
                if (entrada != null && !string.IsNullOrWhiteSpace(entrada.eventKey) && unicas.Add(entrada.eventKey))
                    claves.Add(entrada.eventKey);
        claves.Sort(StringComparer.OrdinalIgnoreCase);
        int indice = claves.FindIndex(c => string.Equals(c, claveElegida, StringComparison.OrdinalIgnoreCase));
        if (!escribirClave && indice < 0 && claves.Count > 0)
        {
            indice = 0;
            claveElegida = claves[0];
        }

        claves.Add("Escribir una clave nueva…");
        EditorGUI.BeginChangeCheck();
        int seleccion = EditorGUILayout.Popup("Clave", escribirClave ? claves.Count - 1 : Math.Max(0, indice), claves.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            escribirClave = seleccion == claves.Count - 1;
            if (!escribirClave) claveElegida = claves[seleccion];
            mensaje = null;
        }
        if (claves.Count == 1) escribirClave = true;

        EditorGUI.BeginChangeCheck();
        if (escribirClave)
            claveNueva = EditorGUILayout.TextField("Clave nueva", claveNueva);
        clipNuevo = (AudioClip)EditorGUILayout.ObjectField("Clip nuevo", clipNuevo, typeof(AudioClip), false);
        if (EditorGUI.EndChangeCheck()) mensaje = null;

        string clave = escribirClave ? claveNueva.Trim() : claveElegida;
        var actual = Buscar(perfil, clave);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Clip actual", actual?.sfx, typeof(AudioClip), false);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(clave) || clipNuevo == null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Asignar"))
            {
                try
                {
                    string anterior = Nombre(actual?.sfx);
                    Asignar(clave, clipNuevo);
                    mensaje = $"{clave}: {anterior} → {Nombre(clipNuevo)}";
                    tipoMensaje = MessageType.Info;
                    claveElegida = Buscar(perfil, clave).eventKey;
                    escribirClave = false;
                }
                catch (Exception ex)
                {
                    mensaje = ex.Message;
                    tipoMensaje = MessageType.Error;
                }
            }
        }
        if (!string.IsNullOrEmpty(mensaje))
            EditorGUILayout.HelpBox(mensaje, tipoMensaje);
    }

    /// <summary>Reemplaza o añade una clave y guarda el perfil mediante AssetDatabase.</summary>
    public static void Asignar(string clave, AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("La clave no puede estar vacía.", nameof(clave));
        if (clip == null || !AssetDatabase.Contains(clip))
            throw new ArgumentException("El clip debe ser un asset de audio del proyecto.", nameof(clip));
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("La asignación se realiza fuera del modo Play.");
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null)
            throw new InvalidOperationException($"No se encuentra el perfil: {RutaPerfil}");

        clave = clave.Trim();
        var entrada = Buscar(perfil, clave);
        string anterior = Nombre(entrada?.sfx);
        Undo.RecordObject(perfil, "Asignar sonido a una clave");
        if (perfil.eventSfx == null)
            perfil.eventSfx = new List<AudioGraphProfile.EventSfx>();
        if (entrada == null)
        {
            entrada = new AudioGraphProfile.EventSfx { eventKey = clave };
            perfil.eventSfx.Add(entrada);
        }
        entrada.sfx = clip;
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, perfil,
            "{0}: {1} → {2}", entrada.eventKey, anterior, Nombre(clip));
    }

    private static AudioGraphProfile.EventSfx Buscar(AudioGraphProfile perfil, string clave)
    {
        return perfil.eventSfx?.Find(e => e != null && string.Equals(e.eventKey, clave, StringComparison.OrdinalIgnoreCase));
    }

    private static string Nombre(AudioClip clip) => clip == null ? "(sin clip)" : clip.name;
}
#endif
