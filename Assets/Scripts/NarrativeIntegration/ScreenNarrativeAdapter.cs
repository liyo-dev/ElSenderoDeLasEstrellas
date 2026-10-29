using System;
using System.Collections;
using EasyTransition;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// La pantalla para el grafo: fundidos de <see cref="FeedbackService"/>, el <see cref="Telon"/> y
/// las transiciones de Easy Transitions (las mismas de entrar y salir de las casas).
/// </summary>
public sealed class ScreenNarrativeAdapter : INarrativeScreen
{
    public bool IsCovered => FeedbackService.IsScreenFaded;

    public IEnumerator Fade(Color color, float seconds, bool cover) => FeedbackService.ScreenFadeAsync(color, seconds, cover);

    public void SetImmediate(Color color) => FeedbackService.SetScreenFadeImmediate(color);

    public void CloseCurtain(string key) => Telon.Cerrar(key);

    public void KeepCurtainClosed(string key) => Telon.Mantener(key);

    public void ReleaseCurtain(string key) => Telon.Soltar(key);

    /// <summary>
    /// La transición tapa por debajo del fundido; en su punto de corte se quita el fundido, que ya
    /// es del mismo color, y la transición destapa con su animación.
    /// </summary>
    public IEnumerator UncoverWithTransition(Color color, float recolorSeconds, ScriptableObject transition, Action onUncovered)
    {
        yield return FeedbackService.ScreenRecolorAsync(color, recolorSeconds);

        var settings = transition as TransitionSettings;
        TransitionManager tm = null;
        if (settings != null && (!ServiceLocator.TryGet(out tm) || tm == null)) tm = TransitionManager.Instance();

        // Sin transición válida, sin gestor o con otra transición en marcha, se destapa con el
        // fundido: el grafo nunca se queda esperando un evento que no va a llegar.
        if (settings == null || tm == null || tm.IsRunning)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ScreenNarrativeAdapter] Sin transición o sin TransitionManager libre: destapo con fundido normal.");
#endif
            yield return FeedbackService.ScreenFadeAsync(color, Mathf.Max(recolorSeconds, 0.5f), fadeIn: false);
            onUncovered?.Invoke();
            yield break;
        }

        bool terminada = false;
        UnityEngine.Events.UnityAction alCortar = null;
        UnityEngine.Events.UnityAction alTerminar = null;
        alCortar = () =>
        {
            tm.onTransitionCutPointReached -= alCortar;
            FeedbackService.SetScreenFadeImmediate(Color.clear);
            onUncovered?.Invoke();
        };
        alTerminar = () =>
        {
            tm.onTransitionEnd -= alTerminar;
            terminada = true;
        };
        tm.onTransitionCutPointReached += alCortar;
        tm.onTransitionEnd += alTerminar;
        tm.Transition(settings, 0f);

        // Si el gestor se destruye a mitad (salir de Play), no se espera para siempre.
        while (!terminada && tm != null && tm.IsRunning) yield return null;
        if (tm != null)
        {
            tm.onTransitionCutPointReached -= alCortar;
            tm.onTransitionEnd -= alTerminar;
        }
    }
}
