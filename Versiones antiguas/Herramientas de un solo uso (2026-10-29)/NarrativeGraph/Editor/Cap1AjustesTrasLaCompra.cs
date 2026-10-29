using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cap1, dos ajustes de puesta en escena (INC-452). Por AssetDatabase e idempotente:
///   1. Oliver va AL LADO de Will cuando le sigue (Oliver_PartyConfig.quedarseDetras = false), a
///      su izquierda y a 1,5 m (lado y distancia de la sección de diálogos del config).
///   2. SEQ_EldranCaja: Will se gira hacia Eldran al oír «¡Will! ¡Espera!». Eldran se para
///      «enfrente» de Will (MoveToBeat, ángulo 0), y Will seguía mirando a Tomasa: Eldran acababa
///      pegado a ella.
/// </summary>
public static class Cap1AjustesTrasLaCompra
{
    private const string RutaOliver = "Assets/_NPCs/Party/Oliver_PartyConfig.asset";
    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_EldranCaja.asset";

    [MenuItem("El Sendero/Narrativa/Cap1: Oliver al lado de Will y Will se gira hacia Eldran")]
    public static void Aplicar()
    {
        var log = new System.Text.StringBuilder("[Cap1AjustesTrasLaCompra]\n");

        var oliver = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RutaOliver);
        if (oliver == null) log.AppendLine($"✗ No encuentro {RutaOliver}.");
        else
        {
            var so = new SerializedObject(oliver);
            var detras = so.FindProperty("quedarseDetras");
            if (!detras.boolValue) log.AppendLine("= Oliver ya va al lado de Will.");
            else
            {
                detras.boolValue = false;
                so.ApplyModifiedProperties();
                log.AppendLine("✓ Oliver va al lado de Will (a su izquierda, 1,5 m).");
            }
        }

        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        var fase = seq != null ? seq.phases.FirstOrDefault(f => f.beats.Any(b => b is SayBeat)) : null;
        if (fase == null) log.AppendLine($"✗ No encuentro la fase del «¡Will! ¡Espera!» en {RutaSecuencia}.");
        else if (fase.beats.Any(b => b is FaceBeat f && f.actorId == SequenceActor.PlayerId && f.targetActorId == "NPC_Eldran"))
            log.AppendLine("= Will ya se gira hacia Eldran.");
        else
        {
            Undo.RecordObject(seq, "SEQ_EldranCaja: Will se gira");
            int i = fase.beats.FindIndex(b => b is SayBeat);
            fase.beats.Insert(i + 1, new FaceBeat
            {
                note = "Will se gira al oirle: asi Eldran se para delante de el y no al lado de Tomasa",
                actorId = SequenceActor.PlayerId,
                targetActorId = "NPC_Eldran",
                turnDuration = 0.4f,
            });
            foreach (var m in seq.phases.SelectMany(f => f.beats).OfType<MoveToBeat>())
                if (m.note != null && m.note.Contains("ReproducirSecuenciaNode"))
                    m.note = "Camino por NavMesh. Si venia de muy lejos, PlayCinematicNode ya lo acerco fuera de camara";
            EditorUtility.SetDirty(seq);
            log.AppendLine("✓ SEQ_EldranCaja: Will se gira hacia Eldran justo después de «¡Will! ¡Espera!».");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }
}
