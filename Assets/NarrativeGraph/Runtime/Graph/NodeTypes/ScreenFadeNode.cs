using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

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

    [Tooltip("Al terminar de destapar, poner la música de la escena en la que se está. Para la " +
             "entrada a la habitación de Will después del prólogo (INC-358), que antes hacía el " +
             "texto dramático «Will, ¡DESPIERTA!».")]
    public bool restaurarMusicaDeEscena = false;

    [Min(0f)] public float fundidoDeMusica = 2f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
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

    private void RestaurarMusica()
    {
        if (restaurarMusicaDeEscena) AudioService.Instance?.RestoreSceneMusic(fundidoDeMusica);
    }
}
