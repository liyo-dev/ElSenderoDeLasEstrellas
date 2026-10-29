using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// Rótulo centrado sobre la pantalla tapada, del tipo «A la mañana siguiente…».
///
/// Si la pantalla no está tapada, primero funde a 'fondo'. El rótulo aparece, se mantiene y se
/// desvanece; la pantalla se queda tapada para quien venga después (una cinemática la destapa
/// en su primer plano, o un ScreenFadeNode). Mientras dura, el telón (INarrativeScreen) la
/// mantiene tapada. El texto lo dibuja el INarrativeTitleCardPresenter que registre el juego.
[Serializable]
[NarrativeNodeInfo("Mundo", "Rótulo sobre la pantalla", "Texto centrado sobre la pantalla tapada, p. ej. «A la mañana siguiente…».")]
public sealed class TitleCardNode : NarrativeNode
{
    [Tooltip("Texto de respaldo si no hay ID de localización o no se encuentra.")]
    public string text;
    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    public string textId;

    public Color color = new Color(1f, 0.83f, 0.45f, 1f);
    [Tooltip("Color con el que se tapa la pantalla si no lo estaba ya.")]
    public Color fondo = Color.black;
    [Tooltip("Vacío = fuente por defecto del presentador.")]
    public TMP_FontAsset fuente;
    [Min(1f)] public float tamano = 64f;

    [Min(0f)] public float aparecer = 1f;
    [Min(0f)] public float mantener = 2.2f;
    [Min(0f)] public float desaparecer = 1f;

    // El rótulo de esta entrada al nodo sigue vivo (Exit lo invalida para que un rótulo
    // cancelado no haga avanzar el grafo).
    [NonSerialized] int _vez;
    [NonSerialized] INarrativeTitleCardPresenter _presentador;
    [NonSerialized] INarrativeScreen _pantalla;

    private string ClaveTelon => "rotulo_" + guid;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        NarrativeServices.TryGet(out _pantalla, nameof(TitleCardNode));
        _pantalla?.CloseCurtain(ClaveTelon);
        int vez = ++_vez;
        ctx.Runner.StartCoroutine(Co_Rotulo(vez, onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx)
    {
        _vez++;
        _presentador?.CancelTitleCard();
        _presentador = null;
        _pantalla?.ReleaseCurtain(ClaveTelon);
    }

    IEnumerator Co_Rotulo(int vez, Action onReadyToAdvance)
    {
        if (_pantalla != null && !_pantalla.IsCovered)
            yield return _pantalla.Fade(fondo, 0.5f, cover: true);
        if (vez != _vez) yield break;

        if (!NarrativeServices.TryGet(out INarrativeTitleCardPresenter presentador, nameof(TitleCardNode)))
        {
            Terminar(onReadyToAdvance);
            yield break;
        }

        string resuelto = NarrativeText.Resolve(textId, text);
        _presentador = presentador;
        presentador.ShowTitleCard(new NarrativeTitleCard
        {
            Text = resuelto,
            Color = color,
            Font = fuente,
            Size = tamano,
            FadeIn = aparecer,
            Hold = mantener,
            FadeOut = desaparecer,
        }, () =>
        {
            if (vez != _vez) return;
            _presentador = null;
            Terminar(onReadyToAdvance);
        });
    }

    void Terminar(Action onReadyToAdvance)
    {
        onReadyToAdvance?.Invoke();
        _pantalla?.ReleaseCurtain(ClaveTelon);
    }
}
