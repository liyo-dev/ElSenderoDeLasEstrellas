#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Informa de lo que hay ahora mismo en el AudioMixer y de lo que debería haber para que
/// la mezcla suene a cine: compresores por bus, un limiter en el master y envíos a reverb.
///
/// No modifica el mixer (los efectos del AudioMixer no se pueden crear desde el API
/// público): informa de lo que falta y deja la lista de valores exactos para ponerlos
/// en la ventana del Audio Mixer.
public static class InformarMezclaDeAudio
{
    [MenuItem("El Sendero/Audio/Informar de la mezcla (AudioMixer)")]
    public static void Informar()
    {
        var informe = new StringBuilder();
        informe.AppendLine("[Audio] Estado del AudioMixer del proyecto:");

        foreach (var guid in AssetDatabase.FindAssets("t:AudioMixer"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            informe.AppendLine($"  {ruta}");
            var objetos = AssetDatabase.LoadAllAssetsAtPath(ruta);
            var efectos = new Dictionary<Object, string>();
            foreach (var objeto in objetos)
                if (objeto != null && objeto.GetType().Name == "AudioMixerEffectController")
                    efectos[objeto] = NombreEfecto(objeto);

            foreach (var objeto in objetos)
            {
                if (objeto == null || objeto.GetType().Name != "AudioMixerGroupController") continue;
                var lista = new List<string>();
                var serializado = new SerializedObject(objeto);
                var propiedad = serializado.FindProperty("m_Effects");
                if (propiedad != null && propiedad.isArray)
                {
                    for (int i = 0; i < propiedad.arraySize; i++)
                    {
                        var referencia = propiedad.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (referencia == null) continue;
                        lista.Add(efectos.TryGetValue(referencia, out var nombre) ? nombre : referencia.name);
                    }
                }
                informe.AppendLine($"    grupo '{objeto.name}': efectos = {(lista.Count == 0 ? "NINGUNO" : string.Join(", ", lista))}");
            }
        }

        informe.AppendLine();
        informe.AppendLine("[Audio] Lo que debería haber (ventana del Audio Mixer, un efecto por bus):");
        informe.AppendLine("  Master:    EQ (HPF 30 Hz) -> Compresor (2:1, umbral -18 dB, ataque 30 ms, release 200 ms) -> Limiter (ceiling -1 dBTP)");
        informe.AppendLine("  Dialogue:  EQ (HPF 80 Hz, -2 dB @ 300 Hz, +3 dB @ 3-5 kHz) -> Compresor (3:1, -20 dB, 10/120 ms) -> De-Esser -> Saturación suave");
        informe.AppendLine("  Dialogue:  envío a reverb (predelay 50 ms, decay 3 s, wet 20 %, HPF 250 Hz) para que la voz tenga espacio");
        informe.AppendLine("  SFX:       Compresor (2:1, -18 dB, 20/150 ms) + envío a reverb corto (decay 1,2 s)");
        informe.AppendLine("  Music:     Compresor (1,5:1, -20 dB, 50/300 ms). La música NO se duckea: eso lo hace el AudioService por evento");
        informe.AppendLine("  Ambience:  Compresor (2:1, -24 dB, 30/250 ms) + paso bajo a 8 kHz");
        informe.AppendLine("  Exponer en Master: MasterVol, MusicVol, SfxVol, DialogVol (ya los usa el AudioService).");
        informe.AppendLine();
        informe.AppendLine("[Audio] Sin limiter en el master, el prólogo suma hasta +5 dBFS y el digital satura: eso es buena parte del 'amateur'.");

        Debug.Log(informe.ToString());
    }

    static string NombreEfecto(Object efecto)
    {
        var serializado = new SerializedObject(efecto);
        var propiedad = serializado.FindProperty("m_EffectName");
        return propiedad != null ? propiedad.stringValue : efecto.name;
    }
}
#endif
