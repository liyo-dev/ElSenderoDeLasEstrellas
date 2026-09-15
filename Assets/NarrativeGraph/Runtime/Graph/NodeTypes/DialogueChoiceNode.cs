using System;
using UnityEngine;

/// <summary>
/// Pregunta con dos respuestas y una salida por respuesta. Es la forma de que "si el diálogo
/// tiene contestación, qué ocurre" se vea como flechas en el grafo.
///
/// Usa DialogueManager.ShowWithChoices (el panel sí/no que ya existe en el juego). Cuando la
/// UI soporte N opciones, este nodo crecerá a N puertos sin cambiar el grafo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "Pregunta con respuestas", "Muestra una pregunta con dos opciones; cada opción es una salida distinta.")]
[UnsafeForSave("Hay una UI modal abierta")]
public sealed class DialogueChoiceNode : NarrativeNode
{
    [Header("Pregunta")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    [Tooltip("Clave de localización del texto de la pregunta. Si está vacía se usa 'promptText'.")]
    public string promptTextId;
    [TextArea(2, 4)] public string promptText;

    [Header("Opción A (salida 1)")]
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string optionATextId;
    public string optionAText = "Sí";

    [Header("Opción B (salida 2)")]
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string optionBTextId;
    public string optionBText = "No";

    [Header("Memoria (opcional)")]
    [NarrativeKey(NarrativeKeyKind.Flag)]
    [Tooltip("Si se indica, la respuesta se guarda como flag: '{clave}' = eligió A. Deja vacío para no guardar nada.")]
    public string rememberAsFlag;

    public override string[] GetOutputPorts() => new[] { PortLabel(optionAText, optionATextId, "A"), PortLabel(optionBText, optionBTextId, "B") };

    static string PortLabel(string text, string id, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(text)) return text;
        if (!string.IsNullOrWhiteSpace(id)) return id;
        return fallback;
    }

    static string Localize(string id, string fallback)
    {
        if (!string.IsNullOrEmpty(id) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(id, fallback);
        return fallback;
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        var dm = DialogueManager.Instance;
        if (dm == null)
        {
            Debug.LogError($"[DialogueChoiceNode:{guid}] DialogueManager.Instance es null → salida A por defecto.");
            AdvanceThrough(ctx, ready, 0);
            return;
        }

        string prompt = Localize(promptTextId, promptText);
        string a = Localize(optionATextId, optionAText);
        string b = Localize(optionBTextId, optionBText);

        dm.ShowWithChoices(prompt, a, b,
            onYes: () =>
            {
                if (!string.IsNullOrWhiteSpace(rememberAsFlag)) NarrativeFlags.Set(rememberAsFlag, true);
                AdvanceThrough(ctx, ready, 0);
            },
            onNo: () =>
            {
                if (!string.IsNullOrWhiteSpace(rememberAsFlag)) NarrativeFlags.Set(rememberAsFlag, false);
                AdvanceThrough(ctx, ready, 1);
            });
    }
}
