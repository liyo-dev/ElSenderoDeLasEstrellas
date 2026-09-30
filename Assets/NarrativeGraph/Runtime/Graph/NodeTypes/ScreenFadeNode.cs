using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;
using EasyTransition;

/// Tapa o destapa la pantalla.
///
/// Al destapar con una transición de Easy Transitions asignada (la misma de entrar y salir de las
/// casas), la pantalla se destapa con esa animación en lugar de con el fundido:
///   1. Si la pantalla está tapada de otro color (p. ej. el blanco en que acaba el prólogo), pasa a
///      'color' en 'duration' sin llegar a destaparse.
///   2. La transición tapa por debajo del fundido; en su punto de corte se quita el fundido, que
///      ya es del mismo color, y la transición destapa con su animación.
/// El nodo avanza cuando la transición ha terminado de destapar.
[Serializable]
[NarrativeNodeInfo("Mundo", "Fundido de pantalla", "")]
public sealed class ScreenFadeNode : NarrativeNode
{
    public Color color = Color.black;
    [Min(0f)] public float duration = 0.5f;
    /// <summary>
    /// true = fundir a negro (pantalla se cubre con el color).
    /// false = quitar el negro (pantalla vuelve a ser visible).
    /// </summary>
    public bool fadeIn = true;

    [Tooltip("Solo al destapar (Fade In desmarcado). Transición de Easy Transitions con la que se " +
             "destapa la pantalla, p. ej. Fade. Vacío = fundido normal. Con transición, 'Duration' " +
             "es lo que tarda la pantalla en pasar a 'Color' si estaba tapada de otro color.")]
    public TransitionSettings transicion;

    [Tooltip("Al terminar de destapar, pedir la música del lugar en el que se está.")]
    public bool restaurarMusicaDeEscena = false;

    [Min(0f)] public float fundidoDeMusica = 2f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (!fadeIn && transicion != null)
        {
            ctx.Runner.StartCoroutine(Co_DestaparConTransicion(onReadyToAdvance));
            return;
        }

        if (duration <= 0f)
        {
            FeedbackService.SetScreenFadeImmediate(fadeIn ? color : Color.clear);
            RestaurarMusica();
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(DoFade(onReadyToAdvance));
    }

    private IEnumerator DoFade(Action onReadyToAdvance)
    {
        yield return FeedbackService.ScreenFadeAsync(color, duration, fadeIn);
        RestaurarMusica();
        onReadyToAdvance?.Invoke();
    }

    private IEnumerator Co_DestaparConTransicion(Action onReadyToAdvance)
    {
        yield return FeedbackService.ScreenRecolorAsync(color, duration);

        TransitionManager tm = null;
        if (!ServiceLocator.TryGet(out tm) || tm == null) tm = TransitionManager.Instance();

        // Sin gestor, o con otra transición en marcha, se destapa con el fundido: el grafo nunca
        // se queda esperando un evento que no va a llegar.
        if (tm == null || tm.IsRunning)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ScreenFadeNode:{guid}] Sin TransitionManager libre: destapo con fundido normal.");
#endif
            yield return FeedbackService.ScreenFadeAsync(color, Mathf.Max(duration, 0.5f), fadeIn: false);
            RestaurarMusica();
            onReadyToAdvance?.Invoke();
            yield break;
        }

        bool terminada = false;
        UnityEngine.Events.UnityAction alCortar = null;
        UnityEngine.Events.UnityAction alTerminar = null;
        alCortar = () =>
        {
            tm.onTransitionCutPointReached -= alCortar;
            FeedbackService.SetScreenFadeImmediate(Color.clear);
            RestaurarMusica();
        };
        alTerminar = () =>
        {
            tm.onTransitionEnd -= alTerminar;
            terminada = true;
        };
        tm.onTransitionCutPointReached += alCortar;
        tm.onTransitionEnd += alTerminar;
        tm.Transition(transicion, 0f);

        // Si el gestor se destruye a mitad (salir de Play), no se espera para siempre.
        while (!terminada && tm != null && tm.IsRunning) yield return null;
        if (tm != null)
        {
            tm.onTransitionCutPointReached -= alCortar;
            tm.onTransitionEnd -= alTerminar;
        }
        onReadyToAdvance?.Invoke();
    }

    private void RestaurarMusica()
    {
        if (restaurarMusicaDeEscena) AudioService.Instance?.PedirMusicaDelLugar(fundidoDeMusica);
    }
}
