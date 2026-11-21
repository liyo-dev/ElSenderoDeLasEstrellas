using System;
using System.Collections;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Tapa o destapa la pantalla (INarrativeScreen).
    ///
    /// Al destapar con una transición asignada, la pantalla se destapa con la animación de esa
    /// transición en lugar de con el fundido: si estaba tapada de otro color, primero pasa a 'color'
    /// en 'duration' sin llegar a destaparse, y luego la transición hace el resto. El nodo avanza
    /// cuando la pantalla está destapada.
    [Serializable]
    [NarrativeNodeInfo("Mundo", "Fundido de pantalla", "")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class ScreenFadeNode : NarrativeNode
    {
        [SerializeField] private Color color = Color.black;
        public Color FadeColor { get => color; set => color = value; }
        [Min(0f)] [SerializeField] private float duration = 0.5f;
        public float Duration { get => duration; set => duration = value; }
        /// <summary>
        /// true = tapar la pantalla con el color.
        /// false = destaparla.
        /// </summary>
        [SerializeField] private bool fadeIn = true;
        public bool FadeIn { get => fadeIn; set => fadeIn = value; }

        [Tooltip("Solo al destapar (Fade In desmarcado). Transición del juego con la que se destapa la " +
                 "pantalla. Vacío = fundido normal. Con transición, 'Duration' es lo que tarda la pantalla " +
                 "en pasar a 'Color' si estaba tapada de otro color.")]
        [SerializeField] private ScriptableObject transicion;

        [Tooltip("Al terminar de destapar, pedir la música del lugar en el que se está.")]
        [SerializeField] private bool restaurarMusicaDeEscena = false;
        public bool RestaurarMusicaDeEscena { get => restaurarMusicaDeEscena; set => restaurarMusicaDeEscena = value; }

        [Min(0f)] [SerializeField] private float fundidoDeMusica = 2f;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (!NarrativeServices.TryGet(out INarrativeScreen screen, nameof(ScreenFadeNode)))
            {
                onReadyToAdvance?.Invoke();
                return;
            }

            if (!fadeIn && transicion != null)
            {
                ctx.Runner.StartCoroutine(Co_DestaparConTransicion(screen, onReadyToAdvance));
                return;
            }

            if (duration <= 0f)
            {
                screen.SetImmediate(fadeIn ? color : Color.clear);
                RestaurarMusica();
                onReadyToAdvance?.Invoke();
                return;
            }

            ctx.Runner.StartCoroutine(DoFade(screen, onReadyToAdvance));
        }

        private IEnumerator DoFade(INarrativeScreen screen, Action onReadyToAdvance)
        {
            yield return screen.Fade(color, duration, fadeIn);
            RestaurarMusica();
            onReadyToAdvance?.Invoke();
        }

        private IEnumerator Co_DestaparConTransicion(INarrativeScreen screen, Action onReadyToAdvance)
        {
            yield return screen.UncoverWithTransition(color, duration, transicion, RestaurarMusica);
            onReadyToAdvance?.Invoke();
        }

        private void RestaurarMusica()
        {
            if (restaurarMusicaDeEscena && NarrativeServices.TryGet(out INarrativeAudio audio, nameof(ScreenFadeNode)))
                audio.RequestLocationMusic(fundidoDeMusica);
        }
    }
}
