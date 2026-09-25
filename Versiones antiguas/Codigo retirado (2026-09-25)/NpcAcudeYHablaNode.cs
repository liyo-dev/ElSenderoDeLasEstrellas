using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Un NPC viene andando hasta el jugador y le habla (INC-439).
///
/// Para los momentos en que la historia necesita que alguien «te busque» sin montar una cinemática
/// en la escena: al comprar la estrella, Eldran se acerca a Will y le pide lo de la caja.
///
/// Mientras dura: el jugador queda quieto (ActionMode.Cinematic) y el NPC fuera de su
/// comportamiento ambiental (SequenceActor.Hold), igual que en una secuencia. Si el NPC está muy
/// lejos, aparece a unos metros del jugador por detrás de la cámara y llega andando: nunca se
/// teletransporta a la vista. Al llegar se encaran, se abre la caja de diálogo (avanza el jugador)
/// y al cerrarla se suelta todo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "NPC viene a hablarte", "Un NPC se acerca andando al jugador y abre un diálogo.")]
public sealed class NpcAcudeYHablaNode : NarrativeNode
{
    [Header("Quién")]
    [Tooltip("ID del NPC (el mismo que usan las secuencias, p. ej. NPC_Eldran).")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    public string npcId = "NPC_Eldran";

    [Header("Llamada (opcional)")]
    [Tooltip("Clave de un bocadillo corto al empezar a acercarse («¡Will, espera!»). Vacío = nada.")]
    public string llamadaKey;

    [Header("Acercamiento")]
    [Tooltip("A qué distancia del jugador se para.")]
    public float distancia = 1.6f;
    [Tooltip("Segundos máximos andando antes de rendirse y hablar desde donde esté.")]
    public float tiempoMaximo = 15f;
    [Tooltip("Si el NPC está más lejos que esto, aparece fuera de cámara a 'distanciaDeAparicion' del jugador.")]
    public float distanciaMaxima = 28f;
    public float distanciaDeAparicion = 11f;

    [Header("Diálogo")]
    public DialogueAsset dialogo;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Runner.StartCoroutine(Run(onReadyToAdvance));
    }

    private IEnumerator Run(Action listo)
    {
        if (!SequenceActor.TryResolve(npcId, out var npc) || npc.Transform == null ||
            !SequenceActor.TryResolve(SequenceActor.PlayerId, out var jugador) || jugador.Transform == null)
        {
            Debug.LogWarning($"[NpcAcudeYHablaNode] No se encuentra a '{npcId}' o al jugador: se abre el diálogo sin acercamiento.");
            yield return Hablar(null);
            listo?.Invoke();
            yield break;
        }

        var pam = jugador.Transform.GetComponent<PlayerActionManager>();
        pam?.PushMode(ActionMode.Cinematic);
        npc.Hold();
        try
        {
            jugador.StopMovement();

            if (!string.IsNullOrEmpty(llamadaKey) && SpeechBubbleUI.Instance != null)
            {
                string texto = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get(llamadaKey, llamadaKey) : llamadaKey;
                SpeechBubbleUI.Instance.Show(npc.Transform, texto, SpeechBubbleUI.Instance.TiempoDeLectura(texto));
            }

            // Muy lejos: aparece detrás de la cámara, a unos metros, y llega andando.
            Vector3 pj = jugador.Transform.position;
            if ((npc.Transform.position - pj).magnitude > distanciaMaxima && npc.Agent != null && npc.Agent.enabled)
            {
                var cam = Camera.main;
                Vector3 atras = cam != null ? -cam.transform.forward : -jugador.Transform.forward;
                atras.y = 0f;
                if (atras.sqrMagnitude < 0.01f) atras = -jugador.Transform.forward;
                Vector3 punto = pj + atras.normalized * distanciaDeAparicion;
                if (NavMesh.SamplePosition(punto, out var hit, 6f, NavMesh.AllAreas))
                    npc.Agent.Warp(hit.position);
            }

            // Se para en la línea entre los dos, a 'distancia' del jugador.
            Vector3 haciaNpc = npc.Transform.position - pj; haciaNpc.y = 0f;
            if (haciaNpc.sqrMagnitude < 0.01f) haciaNpc = jugador.Transform.forward;
            Vector3 destino = pj + haciaNpc.normalized * distancia;
            if (NavMesh.SamplePosition(destino, out var suelo, 2f, NavMesh.AllAreas)) destino = suelo.position;

            if ((npc.Transform.position - destino).sqrMagnitude > 0.2f)
            {
                npc.BeginAgentOverride(0f);
                yield return SequenceMovement.MoveTo(npc, destino, 0f, tiempoMaximo);
                npc.EndAgentOverride();
            }
            npc.StopMovement();
            npc.Face(jugador.Transform.position);
            jugador.Face(npc.Transform.position);
            yield return new WaitForSeconds(0.3f);

            yield return Hablar(npc.Transform);
        }
        finally
        {
            npc.EndAgentOverride();
            npc.Release();
            pam?.PopMode(ActionMode.Cinematic);
        }

        listo?.Invoke();
    }

    private IEnumerator Hablar(Transform npc)
    {
        if (dialogo == null || dialogo.lines == null || dialogo.lines.Length == 0 || DialogueManager.Instance == null)
            yield break;

        bool fin = false;
        if (npc != null) DialogueManager.Instance.StartDialogue(dialogo, npc, () => fin = true);
        else DialogueManager.Instance.StartDialogue(dialogo, () => fin = true);
        while (!fin) yield return null;
    }
}
