using System;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que muestra frases dramáticas en pantalla (recuerdos, momentos épicos, urgencia).
/// Configura un DramaticPhraseConfig con las frases, estilos, animaciones y audio de cada frase.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "Texto dramático", "Texto a pantalla completa (sueños, títulos).")]
public sealed class DramaticTextNode : NarrativeNode
{
    [Tooltip("Secuencia de frases dramáticas a mostrar.")]
    public DramaticPhraseConfig config;

    [Tooltip("Si true, el grafo espera a que terminen todas las frases antes de continuar. " +
             "Si false, el grafo avanza de inmediato mientras las frases se reproducen en paralelo.")]
    public bool waitForCompletion = true;

    [Header("Música")]
    [Tooltip("Si true, al terminar las frases pide la música del lugar (zona, interior o escena). " +
             "Si en ese momento otro sistema pone su música (una cinemática que viene detrás), manda la suya.")]
    public bool restoreSceneMusicWhenDone = false;

    [Min(0f)]
    [Tooltip("Segundos de fundido al volver la música del lugar.")]
    public float restoreMusicFade = 2f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = DramaticTextOverlayUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[DramaticTextNode] ❌ DramaticTextOverlayUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[DramaticTextNode] ❌ 'config' no está asignado en el nodo del grafo.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config.phrases == null || config.phrases.Length == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[DramaticTextNode] ❌ El DramaticPhraseConfig '{config.name}' no tiene frases.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (waitForCompletion)
        {
            ui.Play(config, () =>
            {
                PedirMusicaDelLugar();
                onReadyToAdvance?.Invoke();
            });
        }
        else
        {
            ui.Play(config, restoreSceneMusicWhenDone ? (Action)PedirMusicaDelLugar : null);
            onReadyToAdvance?.Invoke();
        }
    }

    private void PedirMusicaDelLugar()
    {
        if (restoreSceneMusicWhenDone) AudioService.Instance?.PedirMusicaDelLugar(restoreMusicFade);
    }
}
