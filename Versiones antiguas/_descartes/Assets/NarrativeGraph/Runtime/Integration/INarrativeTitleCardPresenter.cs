using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Un rótulo de texto grande centrado sobre la pantalla, del tipo «A la mañana siguiente…».
/// Los tiempos van en segundos de tiempo real.
/// </summary>
public struct NarrativeTitleCard
{
    public string Text;
    public Color Color;
    /// <summary>Null = la fuente que tenga el presentador por defecto.</summary>
    public TMP_FontAsset Font;
    public float Size;
    public float FadeIn;
    public float Hold;
    public float FadeOut;
}

/// <summary>
/// Quien sabe dibujar un <see cref="NarrativeTitleCard"/>. El grafo no dibuja texto por su
/// cuenta: el juego registra su presentador en <see cref="NarrativeTitleCards"/> y los nodos se
/// lo piden a él.
/// </summary>
public interface INarrativeTitleCardPresenter
{
    /// <summary>
    /// Enseña el rótulo por encima del fundido de pantalla: aparece, se mantiene y se desvanece.
    /// Llama a <paramref name="onDone"/> al acabar, también si otro texto lo corta, salvo que se
    /// cancele con <see cref="CancelTitleCard"/>.
    /// </summary>
    void ShowTitleCard(NarrativeTitleCard card, Action onDone);

    /// <summary>Quita el rótulo en curso, si lo hay, sin llamar a su onDone.</summary>
    void CancelTitleCard();
}

/// <summary>Registro del presentador de rótulos que usa el grafo.</summary>
public static class NarrativeTitleCards
{
    public static INarrativeTitleCardPresenter Presenter { get; private set; }

    public static void Register(INarrativeTitleCardPresenter presenter) => Presenter = presenter;

    public static void Unregister(INarrativeTitleCardPresenter presenter)
    {
        if (ReferenceEquals(Presenter, presenter)) Presenter = null;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Presenter = null;
#endif
}
