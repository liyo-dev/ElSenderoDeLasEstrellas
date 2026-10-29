using System;
using UnityEngine;

/// <summary>
/// Muestra un bocadillo flotante sobre un personaje (INarrativeSpeechBubbles: traduce, enseña y
/// suelta la voz si la hay). Si duration > 0 y waitForCompletion, el grafo espera a que se oculte.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "Bocadillo", "Frase corta sobre la cabeza de un personaje.")]
public sealed class ShowSpeechBubbleNode : NarrativeNode
{
    [Tooltip("Tag del GameObject sobre el que aparece el bocadillo (ej: 'Player').")]
    public string targetTag = "Player";

    [TextArea(1, 3)]
    public string text;

    [Tooltip("Clave de localización del texto y de su voz. Si está traducida, manda sobre 'text'.")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    public string textId;

    [Tooltip("Segundos que el bocadillo está en pantalla antes de desaparecer solo (nunca menos de lo que se tarda en leerlo ni de lo que dura su voz). 0 = permanece.")]
    [Min(0f)]
    public float duration = 3f;

    [Tooltip("Si true y duration > 0, el grafo espera a que el bocadillo desaparezca.")]
    public bool waitForCompletion = true;

    [Header("Personaje")]
    [Tooltip("Nombre exacto del estado/animación del Animator del personaje (ej: 'Talk', 'Wave'). Vacío = sin animación.")]
    public string animTrigger;

    [Tooltip("Si true, usa el sprite de énfasis (burbuja explosiva) en lugar del normal.")]
    public bool emphasis;

    [Tooltip("Nombre de quien habla. Opcional: se enseña encima del texto cuando la frase sale fija en pantalla o en subtítulo; en el bocadillo con pico, el pico ya señala a quien habla.")]
    public string speakerName;

    // Frase que el grafo está esperando, para cortarla si se abandona el nodo antes de que termine.
    [NonSerialized] private INarrativeLineHandle _activeLine;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Speaker(!string.IsNullOrEmpty(speakerName) ? speakerName : targetTag == "Player" ? "Jugador" : targetTag);
        var t = d.Text(textId, text);
        d.Line(string.IsNullOrEmpty(t) ? "(sin texto)" : "«" + t + "»");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (!NarrativeServices.TryGet(out INarrativeSpeechBubbles bubbles, nameof(ShowSpeechBubbleNode)) || !bubbles.IsAvailable)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[ShowSpeechBubbleNode] No se pueden enseñar bocadillos ahora (falta la interfaz en pantalla).");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        GameObject target = string.IsNullOrEmpty(targetTag)
            ? null
            : GameObject.FindWithTag(targetTag);

        if (target == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[ShowSpeechBubbleNode] No se encontró objeto con tag '{targetTag}'.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        bool willWait = waitForCompletion && duration > 0f;
        bool done = false;
        var line = new NarrativeBubbleLine
        {
            TextKey = textId,
            Text = text,
            Anchor = target.transform,
            Duration = duration,
            UntilHidden = duration <= 0f,
            SpeakerName = speakerName,
            Gesture = animTrigger,
            Emphasis = emphasis,
        };

        // Si el grafo espera el bocadillo, el botón de saltar del juego puede cortarlo; el aviso
        // llega una sola vez, termine solo o se salte.
        Action onHidden = null;
        if (willWait)
        {
            onHidden = () =>
            {
                done = true;
                _activeLine = null;
                onReadyToAdvance?.Invoke();
            };
        }
        var handle = bubbles.Say(line, onHidden);

        if (!willWait || handle == null)
        {
            // Sin espera, o sin frase que esperar (texto vacío): se sigue ya.
            onReadyToAdvance?.Invoke();
            return;
        }
        if (!done) _activeLine = handle;
    }

    /// Si el runner abandona este nodo (StopExecution, cambio de escena…) mientras espera el
    /// bocadillo, el aviso de fin no llega: aquí se deja de esperar y se calla la voz.
    public override void Exit(NarrativeContext ctx)
    {
        if (_activeLine == null) return;
        _activeLine.Cancel();
        _activeLine = null;
    }
}
