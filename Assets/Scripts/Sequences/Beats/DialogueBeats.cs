using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Beats de habla: bocadillo paginado por tiempo y diálogo normal de avance manual.

/// Un bocadillo sobre la cabeza de un actor, paginado por tiempo.
///
/// El texto se parte por saltos de línea: cada línea es una página. Para el ritmo de la escena es
/// preferible a un diálogo normal — no obliga al jugador a pulsar nada — así que es lo que se usa
/// para casi todo menos para contenido que el jugador deba poder leer con calma (ver DialogueBeat).
///
/// Sobre los gestos: el gesto específico ('gesture') se reproduce las primeras 'gestureRepeats'
/// veces y después el actor pasa a variaciones genéricas de hablar (Talk01/02/03), para que no se
/// quede repitiendo "enfadado" en bucle durante todo el párrafo ni congelado en una pose. Subir
/// 'gestureRepeats' a 2 sirve, por ejemplo, para un saludo al principio de una escena, cuando el
/// primer ciclo se pierde con la transición de entrada y apenas se ve.
[Serializable]
public class SayBeat : SequenceBeat
{
    [Tooltip("Quién habla: 'Player' para Will, o el Persistence ID del NPC. Vacío = usar " +
             "'markName' en su lugar (una voz sin personaje visible, ver más abajo).")]
    public string actorId;

    [Tooltip("Si 'actorId' está vacío, el bocadillo sale anclado a esta marca de posición del " +
             "SequenceStage en vez de a un actor — para una voz que viene de fuera de plano " +
             "(alguien tras una ventana, algo que habla sin personaje en escena). Añadido para " +
             "SEQ_DiscusionVentana (17 sep 2026): dos voces discutiendo que Will oye desde la " +
             "cama, sin que haga falta tener ya colocados los NPCs que discuten. Una marca no " +
             "anima nada — sin gestos, sin pose de conversación.")]
    public string markName;

    [Tooltip("Clave de localización del texto. Los saltos de línea dentro del texto separan páginas.")]
    public string textKey;

    [Tooltip("Segundos que dura cada página en pantalla.")]
    public float pageDuration = 2.6f;

    [Tooltip("Gesto específico de esta frase (p. ej. 'Cheer02', 'Angry01'). Vacío = solo " +
             "variaciones genéricas de hablar. Se ignora si se habla desde una marca ('markName'): " +
             "una marca no tiene animator que mover.")]
    public string gesture;

    [Tooltip("Cuántas veces se reproduce el gesto específico antes de pasar a las variaciones de " +
             "hablar. 1 es lo normal; 2 para que un gesto corto se lea bien al principio de una escena.")]
    public int gestureRepeats = 1;

    [Tooltip("Clave de localización del nombre del hablante, si el bocadillo debe mostrarlo. Vacío = sin nombre.")]
    public string speakerNameKey;

    [Tooltip("Desmárcalo cuando el personaje está haciendo otra cosa mientras habla (corriendo, " +
             "huyendo, peleando): solo sale el bocadillo, sin pose de conversación ni gestos. " +
             "Los gestos viven en la capa de tronco superior, así que encima de una carrera " +
             "producen posturas rotas — el clásico 'parece que se va a partir el cuello'. Sin " +
             "efecto si se habla desde una marca: ahí nunca hay gestos, con o sin marcar esto.")]
    public bool playGestures = true;

    [Tooltip("Sube o baja el punto sobre el que aparece el bocadillo respecto al offset de " +
             "siempre (2,2 m hacia arriba, pensado para la cabeza de un personaje). Pensado sobre " +
             "todo para 'markName': coloca la marca donde de verdad quieres el bocadillo y pon " +
             "aquí (0,0,0) en vez de tener que enterrar la marca 2,2 m bajo el suelo para " +
             "compensar.")]
    public bool overrideBubbleOffset = false;

    [Tooltip("Offset a aplicar cuando 'overrideBubbleOffset' está marcado.")]
    public Vector3 bubbleOffset = Vector3.zero;

    /// Cada cuántos segundos se relanza un gesto DENTRO de una misma página, para que el actor no
    /// se quede en una pose estática mientras el bocadillo sigue en pantalla.
    private const float TalkRetriggerInterval = 1.6f;

    private static readonly string[] TalkVariations = { "Talk01", "Talk02", "Talk03" };

    public override string Describe()
        => $"Dice: {(string.IsNullOrEmpty(actorId) ? $"(marca) {markName}" : actorId)} → {textKey}"
           + (!string.IsNullOrEmpty(actorId) && playGestures
               ? (string.IsNullOrEmpty(gesture) ? "" : $" ({gesture}×{gestureRepeats})")
               : " (sin gestos)");

    public override IEnumerator Run(SequenceContext ctx)
    {
        // 'actorId' resuelve un actor de verdad (con animator); 'markName' resuelve solo un punto
        // del escenario — una voz sin personaje visible, como los dos que discuten al otro lado de
        // la ventana en SEQ_DiscusionVentana. 'animar' queda en false en ese segundo caso: no hay
        // nada que gesticular.
        Transform anchor;
        SequenceActor actor = null;

        if (!string.IsNullOrEmpty(actorId))
        {
            actor = ctx.GetActor(actorId);
            if (actor?.Transform == null) yield break;
            anchor = actor.Transform;
        }
        else
        {
            var mark = ctx.Stage != null ? ctx.Stage.GetMark(markName) : null;
            if (mark == null) yield break; // el aviso ya lo ha dado el stage
            anchor = mark;
        }

        if (SpeechBubbleUI.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SayBeat] SpeechBubbleUI.Instance es null — no hay bocadillo que mostrar. " +
                "¿Arrancaste la escena desde Start.unity?");
#endif
            yield break;
        }

        string text = Localize(textKey);
        string speakerName = string.IsNullOrEmpty(speakerNameKey) ? null : Localize(speakerNameKey);

        bool animar = playGestures && actor != null;

        // Mantener al actor en su pose de interacción durante toda la frase: si no, entre gesto y
        // gesto cae a Idle de pie y se le ve "parado sin hablar" a mitad de la línea.
        // Con playGestures desmarcado (o hablando desde una marca) NO se entra en modo interacción:
        // esa pose es la de alguien plantado conversando, y encima de un personaje que corre da el
        // andar ortopédico de siempre; una marca directamente no tiene nada que poner en esa pose.
        if (animar)
        {
            actor.BeginInteraction();
            actor.SetTalking(true);
        }

        int specificFired = 0;
        int talkIndex = 0;
        // Dónde se apoya el bocadillo. Sin override, SpeechBubbleUI usa su offset de siempre
        // (2,2 m sobre los pies), que es la altura de la cabeza de una persona de verdad y deja el
        // bocadillo flotando muy por encima del pelo de estos personajes. Cuando hay un actor de
        // carne y hueso se mide su altura real y el bocadillo se apoya justo encima; hablando desde
        // una marca no hay a quién medir, así que ahí se deja el comportamiento de siempre.
        Vector3? offsetOverride;
        if (overrideBubbleOffset) offsetOverride = bubbleOffset;
        else if (actor != null) offsetOverride = new Vector3(0f, actor.HeadTopHeight + 0.30f, 0f);
        else offsetOverride = null;

        // Páginas: los saltos de línea del texto, y además ninguna de más de tres líneas en el
        // bocadillo (INC-435): SpeechBubbleUI las mide con la fuente y el ancho de verdad. Cada
        // página dura lo que pide el beat, pero nunca menos de lo que se tarda en leerla.
        var bubble = SpeechBubbleUI.Instance;
        var pages = new System.Collections.Generic.List<string>();
        foreach (string raw in text.Split('\n'))
        {
            string trozo = raw.Trim();
            if (!string.IsNullOrEmpty(trozo)) pages.AddRange(bubble.Paginar(trozo));
        }

        foreach (string page in pages)
        {
            if (string.IsNullOrEmpty(page)) continue;

            bool done = false;
            string trigger = animar ? NextGesture(ref specificFired, ref talkIndex) : null;
            float duracion = Mathf.Max(pageDuration, bubble.TiempoDeLectura(page));

            bubble.Show(anchor, page, duracion, () => done = true,
                trigger, speakerName: speakerName, worldOffset: offsetOverride);

            // Tope de seguridad (auditoría 17 sep 2026). SpeechBubbleUI.Show() y Hide() matan el
            // temporizador del bocadillo anterior SIN llamar a su callback, así que dos bocadillos
            // solapados dejan al primero esperando un aviso que no va a llegar nunca — y esta
            // espera no tenía salida. El resultado sería la secuencia colgada para siempre, con el
            // input bloqueado y el HUD oculto: un cuelgue total del que no se sale jugando.
            //
            // Y solapar dos bocadillos es fácil de hacer sin querer: basta con meter dos beats de
            // hablar en el mismo Parallel, que es algo que el propio catálogo anima a hacer.
            float limite = duracion + 2f;
            float esperado = 0f;
            float sinceRetrigger = 0f;

            while (!done && esperado < limite)
            {
                yield return null;
                esperado += Time.unscaledDeltaTime;
                sinceRetrigger += Time.deltaTime;
                // Un gesto nuevo cada 1,6 s mientras dura el bocadillo, para que no se quede
                // parado a mitad de la frase. PERO nunca por encima de una pose sostenida: esa es
                // una decisión del montaje y pisarla es un fallo, no una animación.
                //
                // El caso que lo hace obligatorio: «Protección Absoluta» se dice EN EL AIRE, con
                // el Archimago sosteniendo la pose de salto. Sin esta guarda, a los 1,6 s de
                // frase le entra un gesto de charla y se le cae la pose en pleno vuelo.
                if (animar && !actor.SosteniendoPose && sinceRetrigger >= TalkRetriggerInterval)
                {
                    sinceRetrigger = 0f;
                    actor.PlayGesture(NextGesture(ref specificFired, ref talkIndex));
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!done)
                Debug.LogWarning($"[SayBeat] El bocadillo de '{(string.IsNullOrEmpty(actorId) ? markName : actorId)}' " +
                    $"({textKey}) no avisó de que había terminado y se ha seguido adelante por el tope de " +
                    "seguridad. Lo normal es que otro bocadillo lo haya pisado: mira si hay dos beats de " +
                    "hablar a la vez.");
#endif
        }

        if (animar)
        {
            actor.SetTalking(false);
            actor.EndInteraction();
        }
    }

    /// Devuelve el gesto que toca: el específico mientras queden repeticiones, y a partir de ahí
    /// las variaciones genéricas de hablar, rotando.
    private string NextGesture(ref int specificFired, ref int talkIndex)
    {
        if (!string.IsNullOrEmpty(gesture) && specificFired < Mathf.Max(1, gestureRepeats))
        {
            specificFired++;
            return gesture;
        }

        string chosen = TalkVariations[talkIndex % TalkVariations.Length];
        talkIndex++;
        return chosen;
    }

    private static string Localize(string key)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, key) : key;
}

/// Un diálogo normal, con el componente de siempre (DialogueManager): avance manual, doblaje,
/// retratos.
///
/// Se usa cuando el jugador tiene que poder leer con calma — típicamente contenido que enseña a
/// jugar — en vez de que las frases pasen solas por tiempo. La secuencia se queda esperando hasta
/// que el diálogo termina.
[Serializable]
public class DialogueBeat : SequenceBeat
{
    [Tooltip("El DialogueAsset a reproducir.")]
    public DialogueAsset dialogue;

    [Tooltip("Sobre qué actor se ancla el diálogo (normalmente el NPC que habla).")]
    public string actorId;

    [Tooltip("Al terminar, apagar YA la cámara del diálogo en vez de esperar su período de gracia " +
             "(0,5 s, pensado para detectar diálogos encadenados). Dentro de una secuencia no hay " +
             "encadenamiento que detectar, y esperar deja la cámara del diálogo congelada en su " +
             "último encuadre medio segundo más — que es de donde salía el plano de la cabeza del " +
             "oyente al final. Desmarcar solo si de verdad se encadena otro diálogo justo después.")]
    public bool endDialogueCameraImmediately = true;

    [Tooltip("Marcado, el diálogo se ve con el plano que ya tenía la secuencia: no enciende la " +
             "cámara de diálogo (la que va de primer plano en primer plano). Para escenas en las " +
             "que se quiere ver a los dos personajes a la vez todo el rato.")]
    public bool conservarCamaraDeLaSecuencia = false;

    [Tooltip("Cómo reaccionan los que ESCUCHAN, línea a línea. DialogueManager solo anima al que " +
             "habla; sin esto, el otro personaje se queda en idle durante todo el diálogo.")]
    public List<DialogueReaction> reactions = new();

    public override string Describe()
        => $"Diálogo: {(dialogue != null ? dialogue.name : "SIN ASIGNAR")} con {actorId}"
           + (reactions != null && reactions.Count > 0 ? $" (+{reactions.Count} reaccionando)" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (dialogue == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[DialogueBeat] Sin DialogueAsset asignado ({note}) — se salta este beat.");
#endif
            yield break;
        }

        if (DialogueManager.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[DialogueBeat] DialogueManager.Instance es null — se salta este beat.");
#endif
            yield break;
        }

        var actor = ctx.GetActor(actorId);
        Transform anchor = actor?.Transform;

        // Reacciones de los que escuchan. DialogueManager.ActivateSpeakerTalkAnimation() solo
        // anima al hablante de cada línea, así que el resto de personajes se quedan quietos durante
        // todo el diálogo. Enganchándonos a OnDialogueLineChanged podemos animar a los demás al
        // ritmo real de las líneas, sin tocar DialogueManager ni depender de tiempos fijos.
        int lineIndex = -1;
        System.Action<DialogueLine, Transform> onLine = (line, _) =>
        {
            lineIndex++;
            if (reactions == null) return;

            // Quién habla en ESTA línea, para no "reaccionar" sobre el que ya se está animando solo.
            string speakerId = line.isPlayerSpeaking ? SequenceActor.PlayerId : actorId;

            foreach (var reaction in reactions)
            {
                if (reaction == null || string.IsNullOrEmpty(reaction.actorId)) continue;
                if (reaction.actorId == speakerId) continue;

                var listener = ctx.GetActor(reaction.actorId);
                if (listener == null) continue;

                reaction.ApplyTo(listener, lineIndex);
            }
        };

        bool finished = false;
        DialogueManager.OnDialogueLineChanged += onLine;

        // OnDialogueLineChanged es ESTÁTICO, así que un handler olvidado sigue disparando el resto
        // de la partida: cada diálogo posterior aplicaría las reacciones de ESTA escena a quien
        // hablara, y además mantendría vivo el contexto entero de la secuencia. El finally de abajo
        // no basta, porque saltar la cinemática hace StopCoroutine sobre la corrutina principal y
        // eso NO ejecuta los finally de las anidadas (el proyecto lo documenta en tres sitios).
        // Registrándola en el reproductor, el cierre la ejecuta pase lo que pase.
        bool desuscrito = false;
        System.Action desuscribir = () =>
        {
            if (desuscrito) return;
            desuscrito = true;
            DialogueManager.OnDialogueLineChanged -= onLine;
        };
        ctx.Player?.RegisterCleanup(desuscribir);

        // El interruptor de la cámara es de DialogueManager, que es un singleton: se enciende
        // solo para este diálogo y se apaga pase lo que pase (también si se salta la cinemática,
        // que no ejecuta los finally de las corrutinas anidadas — por eso va también al cleanup).
        if (conservarCamaraDeLaSecuencia)
        {
            DialogueManager.Instance.ConservarCamaraDeSecuencia = true;
            ctx.Player?.RegisterCleanup(() =>
            {
                if (DialogueManager.Instance != null) DialogueManager.Instance.ConservarCamaraDeSecuencia = false;
            });
        }

        try
        {
            DialogueManager.Instance.StartDialogue(dialogue, anchor, () => finished = true,
                isSequenceDialogue: true);

            yield return new WaitUntil(() => finished);
        }
        finally
        {
            desuscribir();

            if (conservarCamaraDeLaSecuencia && DialogueManager.Instance != null)
                DialogueManager.Instance.ConservarCamaraDeSecuencia = false;

            // FIX 16 sep 2026 (Raúl: "al acabar el diálogo del tutorial se ve el cabezón de Will",
            // y seguía viéndose tras el primer intento): devolver la cámara a la secuencia YA.
            //
            // DialogueManager llama a EndCinematic(), que NO apaga nada — espera
            // `chainDialogueGracePeriod` (0,5 s) por si viene otro diálogo encadenado, y durante ese
            // medio segundo la cámara de diálogo sigue activa y congelada en su último encuadre. El
            // primer intento de arreglarlo fue un corte de cámara más 0,4 s de margen, y falló por
            // dos motivos a la vez: 0,4 < 0,5, y el corte iba a NUESTRA cámara cinemática, que en
            // ese momento no es la que está pintando.
            //
            // Dentro de una secuencia no hay encadenamiento que detectar — el guion es nuestro — así
            // que se pide el apagado inmediato y recuperamos la cámara sin esperar.
            if (endDialogueCameraImmediately && !conservarCamaraDeLaSecuencia)
                DialogueCinematicController.Instance?.EndCinematicNow();
        }
    }
}

/// Cómo reacciona un personaje que ESCUCHA mientras otro habla, en un DialogueBeat.
///
/// Los gestos y las emociones se recorren en orden según avanzan las líneas, y vuelven a empezar
/// cuando se acaban: con dos gestos y siete líneas, alterna entre los dos. Dejar una de las dos
/// listas vacía significa "esa parte no cambia".
[System.Serializable]
public class DialogueReaction
{
    [Tooltip("Quién reacciona: 'Player' para Will, o el Persistence ID del NPC. Si coincide con " +
             "quien habla en esa línea, se ignora (a ese ya lo anima DialogueManager).")]
    public string actorId;

    [Tooltip("Gestos a ir soltando línea a línea (p. ej. HeadNod01, Question01). Vacío = sin gestos.")]
    public string[] gestures;

    [Tooltip("Caras a ir poniendo línea a línea. Vacío = no se toca la cara.")]
    public NPCEmotion[] emotions;

    /// Aplica la reacción que toca para el índice de línea dado.
    public void ApplyTo(SequenceActor listener, int lineIndex)
    {
        if (listener == null || lineIndex < 0) return;

        if (gestures != null && gestures.Length > 0)
            listener.PlayGesture(gestures[lineIndex % gestures.Length]);

        if (emotions != null && emotions.Length > 0)
            listener.SetEmotion(emotions[lineIndex % emotions.Length]);
    }
}
