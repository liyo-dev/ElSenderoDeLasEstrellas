// Assets/Scripts/Audio/AudioGraphProfile.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="El Sendero/Audio/Audio Graph Profile")]
public class AudioGraphProfile : ScriptableObject
{
    [Serializable] public class SceneMusic { public string sceneName; public AudioClip music; }
    [Serializable] public class EventSfx   { public string eventKey;  public AudioClip sfx;   }
    
    [Serializable]
    public class BattleRule
    {
        [Tooltip("ID o subcadena del arena/batalla. Debe coincidir con el battleId que usa tu StartBattleNode o el nombre del GO/arena.")]
        public string battleId;
        public AudioClip music;
        [Min(0f)] public float fade = 0.5f;
    }
    
    [Serializable]
    public class MinigameRule
    {
        [Tooltip("ID del minijuego (debe coincidir con TagMinigameController.MinigameId o similar)")]
        public string minigameId;
        [Tooltip("Música a reproducir durante el minijuego")]
        public AudioClip music;
        [Tooltip("Tiempo de fade para la transición")]
        [Min(0f)] public float fade = 0.5f;
        [Tooltip("Si true, la música hace loop")]
        public bool loop = true;
    }
    
    [Serializable]
    public class AmbientZoneRule
    {
        [Tooltip("ID de la zona (debe coincidir con el musicZoneId del AmbientPreset asignado al AmbientZone)")]
        public string zoneId;
        [Tooltip("Música a reproducir en esta zona")]
        public AudioClip music;
        [Tooltip("Tiempo de fade para la transición")]
        [Min(0f)] public float fade = 1.5f;
        [Tooltip("Si está activo, la música hace loop")]
        public bool loop = true;
    }

    [Serializable]
    public class VoiceDuckSettings
    {
        public bool enabled = true;
        [Range(-80f, 0f)] public float musicDb;
        [Range(-80f, 0f)] public float ambienceDb;
        [Range(-80f, 0f)] public float sfxDb;
        [Tooltip("Ganancia en dB para realzar el canal de diálogo durante las voces")]
        [Range(0f, 12f)] public float voiceGainDb = 0f;
        [Min(0f)] public float attackSeconds = 0.15f;
        [Min(0f)] public float releaseSeconds = 0.6f;
        [Min(0f)] public float holdAfterVoiceSeconds = 0.25f;
    }

    // Los tres ajustes de atenuación bajo las voces viven solo aquí: AudioService no guarda
    // valores propios y, sin perfil, no atenúa. Los valores por defecto son la mezcla vigente,
    // para que un perfil nuevo suene igual que el del juego.

    [Header("Atenuación durante las voces")]
    [Tooltip("Cuánto se apartan música, ambiente y efectos mientras suena una voz fuera de cinemática.")]
    public VoiceDuckSettings voiceDuck = new()
    {
        musicDb = -6f, ambienceDb = -6f, sfxDb = -4f, voiceGainDb = 0f,
        attackSeconds = 0.15f, releaseSeconds = 0.6f, holdAfterVoiceSeconds = 0.25f
    };

    [Header("Atenuación de voces en cinemáticas")]
    [Tooltip("Cuánto se apartan música, ambiente y efectos mientras suena una voz durante una cinemática.")]
    public VoiceDuckSettings cinematicVoiceDuck = new()
    {
        musicDb = -3f, ambienceDb = -6f, sfxDb = -4f, voiceGainDb = 0f,
        attackSeconds = 0.08f, releaseSeconds = 0.65f, holdAfterVoiceSeconds = 0.5f
    };

    [Header("Atenuación de voces con efecto épico")]
    [Tooltip("Cuánto se apartan música, ambiente y efectos mientras suena una voz con el efecto Épico (conjuros). La música ritual va aparte y no se atenúa.")]
    public VoiceDuckSettings epicVoiceDuck = new()
    {
        musicDb = -6f, ambienceDb = -12f, sfxDb = -4f, voiceGainDb = 0f,
        attackSeconds = 0.08f, releaseSeconds = 0.65f, holdAfterVoiceSeconds = 0.5f
    };

    public List<BattleRule> battles = new();
    public List<MinigameRule> minigames = new();
    public List<AmbientZoneRule> ambientZones = new();
    
    [Serializable]
    public class SequenceRule
    {
        [Tooltip("ID de la secuencia de gameplay (debe coincidir con el campo musicId de su SequenceDefinition)")]
        public string sequenceId;
        public AudioClip music;
        [Min(0f)] public float fadeIn  = 0.5f;
        [Min(0f)] public float fadeOut = 0.8f;
        [Tooltip("El tema suena una vez y se acaba: al terminar no vuelve a empezar (un tema de " +
                 "cinemática que tiene su final). Vale para cualquier sitio que lo ponga.")]
        public bool sinBucle;
    }

    /// ¿Es un tema que se acaba (suena una vez, sin bucle)? Ver SequenceRule.sinBucle.
    public bool EsTemaQueAcaba(AudioClip clip)
    {
        if (clip == null) return false;
        foreach (var rule in sequences)
            if (rule != null && rule.sinBucle && rule.music == clip) return true;
        return false;
    }

    public List<SceneMusic> sceneMusic = new();
    public List<EventSfx>   eventSfx   = new();
    public List<SequenceRule> sequences = new();
    
    /// Una canción del juego y dónde suena (grupo: Escenas, Batallas, Minijuegos, Zonas o Secuencias).
    public readonly struct CancionDelJuego
    {
        public readonly AudioClip clip;
        public readonly string grupo;
        public readonly string donde;
        public CancionDelJuego(AudioClip clip, string grupo, string donde) { this.clip = clip; this.grupo = grupo; this.donde = donde; }
    }

    /// Toda la música del juego, sin repetir: si una canción suena en varios sitios, cuenta el primero.
    public void ReunirMusica(List<CancionDelJuego> destino)
    {
        if (destino == null) return;
        foreach (var r in sceneMusic) Anadir(destino, r?.music, "Escenas", r?.sceneName);
        foreach (var r in battles) Anadir(destino, r?.music, "Batallas", r?.battleId);
        foreach (var r in minigames) Anadir(destino, r?.music, "Minijuegos", r?.minigameId);
        foreach (var r in ambientZones) Anadir(destino, r?.music, "Zonas", r?.zoneId);
        foreach (var r in sequences) Anadir(destino, r?.music, "Secuencias", r?.sequenceId);
    }

    private static void Anadir(List<CancionDelJuego> destino, AudioClip clip, string grupo, string donde)
    {
        if (clip == null) return;
        foreach (var c in destino) if (c.clip == clip) return;
        destino.Add(new CancionDelJuego(clip, grupo, donde));
    }

    public SequenceRule GetSequenceRule(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId)) return null;
        foreach (var rule in sequences)
        {
            if (string.Equals(rule.sequenceId, sequenceId, StringComparison.OrdinalIgnoreCase))
                return rule;
        }
        return null;
    }

    /// <summary>
    /// Busca la regla de música para una zona de ambiente específica
    /// </summary>
    public AmbientZoneRule GetAmbientZoneRule(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;
        
        foreach (var rule in ambientZones)
        {
            if (string.IsNullOrEmpty(rule.zoneId)) continue;
            if (zoneId.Contains(rule.zoneId, StringComparison.OrdinalIgnoreCase) ||
                rule.zoneId.Contains(zoneId, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }
        return null;
    }
}
