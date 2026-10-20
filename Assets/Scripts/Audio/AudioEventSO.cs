using System;
using System.Collections.Generic;
using UnityEngine;

/// Un evento de audio por capas. Cada capa entra con su propio retardo, ganancia, rango de
/// pitch y paneo, y puede llevar varios clips para que no suene igual cada vez.
/// Sirve para los hechizos y los impactos: un solo clip suena plano; por capas, con el
/// transitorio delante, el cuerpo después y la cola al final, suena a golpe.
[CreateAssetMenu(menuName = "El Sendero/Audio/Evento de audio", fileName = "NuevoEventoDeAudio")]
public class AudioEventSO : ScriptableObject
{
    [Serializable]
    public class Capa
    {
        [Tooltip("Clips de esta capa. Se elige uno al azar sin repetir el anterior.")]
        public List<AudioClip> clips = new();
        [Tooltip("Segundos que tarda en entrar desde el inicio del evento.")]
        [Min(0f)] public float retardo;
        [Tooltip("Ganancia de la capa en dB.")]
        [Range(-40f, 20f)] public float gananciaDb;
        [Tooltip("Rango de pitch (1 = sin cambio). Se elige uno al azar en cada reproducción.")]
        public Vector2 rangoPitch = new Vector2(1f, 1f);
        [Tooltip("Paneo estéreo (-1 izquierda, 1 derecha).")]
        [Range(-1f, 1f)] public float paneo;
        [Tooltip("Probabilidad de que esta capa suene.")]
        [Range(0f, 1f)] public float probabilidad = 1f;
        [Tooltip("Si está activa y hay posición, el clip se oye en el espacio (3D).")]
        public bool espacial = true;

        int _ultimoElegido = -1;

        /// Elige un clip de la capa evitando repetir el último (si hay más de uno).
        public AudioClip ElegirClip()
        {
            if (clips == null || clips.Count == 0) return null;
            if (clips.Count == 1) return clips[0];
            for (int intento = 0; intento < clips.Count; intento++)
            {
                int i = UnityEngine.Random.Range(0, clips.Count);
                if (i == _ultimoElegido) continue;
                _ultimoElegido = i;
                return clips[i];
            }
            return clips[0];
        }
    }

    [Tooltip("Capas del evento, en orden de entrada.")]
    public List<Capa> capas = new();

    /// ¿Tiene alguna capa que pueda sonar?
    public bool TieneCapas()
    {
        if (capas == null) return false;
        for (int i = 0; i < capas.Count; i++)
            if (capas[i] != null && capas[i].clips != null && capas[i].clips.Count > 0) return true;
        return false;
    }
}
