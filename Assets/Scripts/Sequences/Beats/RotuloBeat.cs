using System;
using System.Collections;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// Un rótulo dentro de una cinemática («Hace siglos…», «Siglos después.»): el mismo que pone el
/// grafo con su paso de rótulo (TitleCardNode), dibujado por el presentador de rótulos del juego por
/// encima del fundido de pantalla. No cubre ni descubre la pantalla: eso lo decide la secuencia.
[Serializable]
public class RotuloBeat : SequenceBeat
{
    [Tooltip("Clave del texto en las tablas (se resuelve con el idioma); si no existe, se enseña tal cual.")]
    public string texto;
    [Tooltip("Color del texto. Sobre negro, el dorado de siempre; sobre blanco, tinta.")]
    public Color color = new Color(1f, 0.83f, 0.45f, 1f);
    [Min(1f)] public float tamano = 64f;
    [Min(0f)] public float aparecer = 0.6f;
    [Min(0f)] public float mantener = 1.4f;
    [Min(0f)] public float desaparecer = 0.6f;
    [Tooltip("Esperar a que acabe antes de seguir con la secuencia.")]
    public bool esperar;

    public override string Describe() => $"Rótulo: {texto}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (string.IsNullOrWhiteSpace(texto)) yield break;
        if (!NarrativeServices.TryGet(out INarrativeTitleCardPresenter presentador, nameof(RotuloBeat))) yield break;
        bool acabado = false;
        presentador.ShowTitleCard(new NarrativeTitleCard
        {
            Text = NarrativeText.Resolve(texto, texto),
            Color = color,
            Size = tamano,
            FadeIn = aparecer,
            Hold = mantener,
            FadeOut = desaparecer,
        }, () => acabado = true);
        if (!esperar) yield break;
        while (!acabado) yield return null;
    }
}
