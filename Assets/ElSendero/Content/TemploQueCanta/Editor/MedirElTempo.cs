using UnityEditor;
using UnityEngine;

/// Mide el tempo y el desfase de una canción (temporal): lee las muestras del clip, saca la energía por
/// tramas, detecta los ataques y busca el periodo que mejor los encaja (autocorrelación entre 60 y 200
/// BPM). Devuelve también en qué segundo cae el primer tiempo. Se usa para poner `MusicPulse.bpm` y su
/// desfase a la canción de verdad y no a un 120 de mentira.
public static class MedirElTempo
{
    private const string Ruta = "Assets/Audio/Music/Zonas/El guardian canta.mp3";

    [MenuItem("El Sendero/Escenario/Medir el tempo de la canción del templo (temporal)")]
    public static void Medir()
    {
        var importador = AssetImporter.GetAtPath(Ruta) as AudioImporter;
        if (importador == null) { Avisar("no encuentro el audio"); return; }

        // Para leer muestras hace falta que no sea Streaming: se importa un momento en memoria.
        var ajustes = importador.defaultSampleSettings;
        var original = ajustes.loadType;
        ajustes.loadType = AudioClipLoadType.DecompressOnLoad;
        ajustes.preloadAudioData = true;
        importador.defaultSampleSettings = ajustes;
        importador.SaveAndReimport();

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Ruta);
        if (clip == null) { Avisar("no carga el clip"); return; }

        var datos = new float[clip.samples * clip.channels];
        clip.GetData(datos, 0);

        const int Hop = 512;
        var hopDur = Hop / (float)clip.frequency;
        int tramas = datos.Length / clip.channels / Hop;
        var energia = new float[tramas];
        for (int i = 0; i < tramas; i++)
        {
            float suma = 0f;
            int baseMuestra = i * Hop * clip.channels;
            for (int j = 0; j < Hop * clip.channels; j++) suma += datos[baseMuestra + j] * datos[baseMuestra + j];
            energia[i] = suma;
        }

        var ataque = new float[tramas];
        for (int i = 1; i < tramas; i++) ataque[i] = Mathf.Max(0f, energia[i] - energia[i - 1]);

        // Lags posibles: de 200 a 60 BPM.
        int lagMin = Mathf.Max(2, Mathf.RoundToInt(0.30f / hopDur));
        int lagMax = Mathf.RoundToInt(1.00f / hopDur);
        float mejorPunto = -1f;
        int mejorLag = lagMin;
        for (int lag = lagMin; lag <= lagMax; lag++)
        {
            float suma = 0f;
            for (int i = 0; i + lag < tramas; i++) suma += ataque[i] * ataque[i + lag];
            if (suma <= mejorPunto) continue;
            mejorPunto = suma;
            mejorLag = lag;
        }

        float bpm = 60f / (mejorLag * hopDur);

        // Desfase: en qué punto del periodo caen más ataques (el primer tiempo de la música).
        float mejorFase = -1f;
        int mejorP = 0;
        for (int p = 0; p < mejorLag; p++)
        {
            float suma = 0f;
            for (int i = p; i < tramas; i += mejorLag) suma += ataque[i];
            if (suma <= mejorFase) continue;
            mejorFase = suma;
            mejorP = p;
        }
        float desfase = mejorP * hopDur;

        // Se deja como estaba (Streaming, sin precargar).
        ajustes.loadType = original;
        ajustes.preloadAudioData = false;
        importador.defaultSampleSettings = ajustes;
        importador.SaveAndReimport();

        string informe = $"BPM={bpm:0.0} · compás={60f / bpm * 4f:0.00} s · desfase={desfase:0.000} s · tramas={tramas}";
        Debug.Log("[MedirTempo] " + informe);
        OrdenesDelEditor.Avisar("Tempo de la canción", informe);
    }

    private static void Avisar(string texto)
    {
        Debug.LogWarning("[MedirTempo] " + texto);
        OrdenesDelEditor.Avisar("Tempo de la canción", texto);
    }
}
