using System;
using System.Collections;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Corta la cámara a un punto de interés con nombre, aguanta holdDuration segundos y la devuelve
    /// (INarrativeCameraFocus). Si la pantalla llega tapada (p. ej. de una cinemática que acabó en
    /// negro a propósito), es este plano el que la destapa, con la cámara ya en el punto.
    /// Opcionalmente acerca la cámara (zoom) durante el plano.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Mundo", "Enfocar cámara", "Mueve la cámara a un punto de interés.")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class FocusCameraNode : NarrativeNode
    {
        [Tooltip("Id del punto de interés de la escena al que cortar la cámara.")]
        [SerializeField] private string focusId;

        [Tooltip("Segundos que la cámara permanece en el punto de foco.")]
        [SerializeField] private float holdDuration = 3f;

        [Header("Reaparición (solo si la pantalla llega tapada)")]
        [Tooltip("Duración del fundido de reaparición si la pantalla llega tapada. Si no está tapada no se aplica ningún fundido.")]
        [SerializeField] private float revealFadeDuration = 0.3f;

        [Tooltip("Si true, el grafo espera a que acabe el plano. Si false, avanza inmediatamente.")]
        [SerializeField] private bool waitForCompletion = true;

        [Header("Zoom (opcional)")]
        [Tooltip("Factor de FOV relativo durante el hold. 1 = sin zoom. 0.7 = zoom in (FOV al 70%).")]
        [Range(0.3f, 1.5f)]
        [SerializeField] private float zoomFactor = 1f;

        [Tooltip("Duración del zoom in en segundos (0 = instantáneo).")]
        [SerializeField] private float zoomInDuration = 0.3f;

        [Tooltip("Duración del zoom out en segundos (0 = instantáneo).")]
        [SerializeField] private float zoomOutDuration = 0.3f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            IEnumerator plano = null;
            if (NarrativeServices.TryGet(out INarrativeCameraFocus focus, nameof(FocusCameraNode)))
            {
                plano = focus.Focus(focusId, new NarrativeCameraShot
                {
                    Hold = holdDuration,
                    RevealFade = revealFadeDuration,
                    ZoomFactor = zoomFactor,
                    ZoomInSeconds = zoomInDuration,
                    ZoomOutSeconds = zoomOutDuration,
                });
                if (plano == null)
                    Debug.LogWarning($"[FocusCameraNode] No existe el punto de interés '{focusId}'.");
            }

            if (plano == null)
            {
                onReadyToAdvance?.Invoke();
                return;
            }

            if (waitForCompletion)
                ctx.Runner.StartCoroutine(Co_Plano(plano, onReadyToAdvance));
            else
            {
                ctx.Runner.StartCoroutine(plano);
                onReadyToAdvance?.Invoke();
            }
        }

        static IEnumerator Co_Plano(IEnumerator plano, Action done)
        {
            yield return plano;
            done?.Invoke();
        }
    }
}
