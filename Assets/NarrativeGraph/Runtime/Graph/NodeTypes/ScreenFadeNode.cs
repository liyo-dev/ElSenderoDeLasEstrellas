using System;
using System.Collections;
using UnityEngine;

/// Tapa o destapa la pantalla (INarrativeScreen).
///
/// Al destapar con una transición asignada, la pantalla se destapa con la animación de esa
/// transición en lugar de con el fundido: si estaba tapada de otro color, primero pasa a 'color'
/// en 'duration' sin llegar a destaparse, y luego la transición hace el resto. El nodo avanza
/// cuando la pantalla está destapada.
[Serializable]
[NarrativeNodeInfo("Mundo", "Fundido de pantalla", "")]
public sealed class ScreenFadeNode : NarrativeNode
{
    public Color color = Color.black;
    [Min(0f)] public float duration = 0.5f;
    /// <summary>
    /// true = tapar la pantalla con el color.
    /// false = destaparla.
    /// </summary>
    public bool fadeIn = true;

    [Tooltip("Solo al destapar (Fade In desmarcado). Transición del juego con la que se destapa la " +
             "pantalla. Vacío = fundido normal. Con transición, 'Duration' es lo que tarda la pantalla " +
             "en pasar a 'Color' si estaba tapada de otro color.")]
    public ScriptableObject transicion;

    [Tooltip("Al terminar de destapar, pedir la música del lugar en el que se está.")]
    public bool restaurarMusicaDeEscena = false;

    [Min(0f)] public float fundidoDeMusica = 2f;

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
