using UnityEngine;

/// Una frase suelta de un personaje durante un combate con guion (la batalla final), fuera de la
/// guía: el Mago se burla, Liam, el consentimiento de Estela... Devuelve cuánto hay que esperar
/// para que se lea. Ver INC-509.
public static class Bocadillos
{
    public static float Decir(Transform quien, string key, string nombre, float duracion = 2.8f, bool fijo = false)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null || string.IsNullOrEmpty(key)) return 0f;

        string texto = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, key) : key;
        duracion = Mathf.Max(duracion, bocadillo.TiempoDeLectura(texto));
        float voz = VoiceLines.TryPlay(key);
        if (voz > 0f) duracion = Mathf.Max(duracion, voz + 0.3f);
        bocadillo.Show(quien, texto, duration: duracion, speakerName: nombre, fijoEnPantalla: fijo || quien == null);
        return duracion + 0.3f;
    }
}
