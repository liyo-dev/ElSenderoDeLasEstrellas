using System;
using UnityEngine;

/// <summary>
/// Bocadillos del grafo por el camino único de las frases (<see cref="PresentadorDeLinea"/>:
/// traduce, enseña y suelta la voz). Mientras el grafo espera una frase, el botón global de saltar
/// (<see cref="NarrativeSkipHub"/>) puede cortarla aunque el jugador no esté bloqueado.
/// </summary>
public sealed class SpeechBubbleNarrativeAdapter : INarrativeSpeechBubbles
{
    public bool IsAvailable => PresentadorDeLinea.Disponible;

    public INarrativeLineHandle Say(NarrativeBubbleLine line, Action onHidden)
    {
        var handle = new LineHandle();
        Action alTerminar = null;
        if (onHidden != null)
        {
            handle.Skip = () =>
            {
                handle.StopVoice();
                PresentadorDeLinea.SaltarLaActual();
            };
            // SpeechBubbleUI avisa una sola vez, termine la frase sola o se salte.
            alTerminar = () =>
            {
                handle.StopListeningSkip();
                onHidden();
            };
            NarrativeSkipHub.RegisterSkipHandler(handle.Skip);
        }

        var dicha = PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = line.TextKey,
            textoPorDefecto = line.Text,
            ancla = line.Anchor,
            duracion = line.Duration,
            hastaQueSeEsconda = line.UntilHidden,
            nombreDelHablante = line.SpeakerName,
            gesto = line.Gesture,
            enfasis = line.Emphasis,
            alTerminar = alTerminar,
        });
        handle.Voice = dicha.Voz;

        if (!dicha.Dicha)
        {
            // Sin frase (texto vacío): no hay nada que esperar ni que saltar.
            handle.StopListeningSkip();
            return null;
        }
        return handle;
    }

    sealed class LineHandle : INarrativeLineHandle
    {
        public Action Skip;
        public AudioClip Voice;

        public void StopVoice()
        {
            PresentadorDeLinea.CallarLaVoz(Voice);
            Voice = null;
        }

        public void StopListeningSkip()
        {
            if (Skip == null) return;
            NarrativeSkipHub.UnregisterSkipHandler(Skip);
            Skip = null;
        }

        /// Si quien esperaba la frase la abandona antes de que acabe, el aviso de fin no llega:
        /// aquí se calla la voz y se deja de atender el botón de saltar.
        public void Cancel()
        {
            if (Skip == null) return;
            StopVoice();
            StopListeningSkip();
        }
    }
}
