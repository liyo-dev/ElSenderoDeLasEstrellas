using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Pregunta con dos respuestas y una salida por respuesta: lo que ocurre según la contestación se
    /// ve como flechas en el grafo. La pregunta la enseña el INarrativeChoicePresenter del juego.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Diálogo", "Pregunta con respuestas", "Muestra una pregunta con dos opciones; cada opción es una salida distinta.")]
    [UnsafeForSave("Hay una UI modal abierta")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class DialogueChoiceNode : NarrativeNode
    {
        [Header("Pregunta")]
        [NarrativeKey(NarrativeKeyKind.LocKey)]
        [Tooltip("Clave de localización del texto de la pregunta. Si está vacía se usa 'promptText'.")]
        [SerializeField] private string promptTextId;
        [TextArea(2, 4)] [SerializeField] private string promptText;
        public string PromptText { get => promptText; set => promptText = value; }

        [Header("Opción A (salida 1)")]
        [NarrativeKey(NarrativeKeyKind.LocKey)] [SerializeField] private string optionATextId;
        [SerializeField] private string optionAText = "Sí";
        public string OptionAText { get => optionAText; set => optionAText = value; }

        [Header("Opción B (salida 2)")]
        [NarrativeKey(NarrativeKeyKind.LocKey)] [SerializeField] private string optionBTextId;
        [SerializeField] private string optionBText = "No";
        public string OptionBText { get => optionBText; set => optionBText = value; }

        [Header("Memoria (opcional)")]
        [NarrativeKey(NarrativeKeyKind.Flag)]
        [Tooltip("Si se indica, la respuesta se guarda como flag: '{clave}' = eligió A. Deja vacío para no guardar nada.")]
        [SerializeField] private string rememberAsFlag;

        public override string[] GetOutputPorts() => new[] { PortLabel(optionAText, optionATextId, "A"), PortLabel(optionBText, optionBTextId, "B") };

        static string PortLabel(string text, string id, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(text)) return text;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            return fallback;
        }

        public override void Describe(INarrativeNodeDescriber d)
        {
            var prompt = d.Text(promptTextId, promptText);
            d.Line("❓ " + (string.IsNullOrEmpty(prompt) ? "(sin texto)" : "«" + prompt + "»"), NarrativeTextTone.Strong);
            d.Line("A → " + (d.Text(optionATextId, optionAText) ?? "A"));
            d.Line("B → " + (d.Text(optionBTextId, optionBText) ?? "B"));
            if (!string.IsNullOrWhiteSpace(rememberAsFlag)) d.Flag(rememberAsFlag, $"{rememberAsFlag} = eligió A");
        }

        public override string DescribeDecision()
            => $"Pregunta con respuestas \"{DisplayTitle}\" (guid {Guid}): \"{(string.IsNullOrEmpty(promptText) ? promptTextId : promptText)}\" → '{optionAText}' / '{optionBText}'.";

        public override void Enter(NarrativeContext ctx, Action ready)
        {
            string prompt = NarrativeText.Resolve(promptTextId, promptText);
            string a = NarrativeText.Resolve(optionATextId, optionAText);
            string b = NarrativeText.Resolve(optionBTextId, optionBText);

            bool shown = NarrativeServices.TryGet(out INarrativeChoicePresenter presenter, nameof(DialogueChoiceNode))
                && presenter.TryShowChoice(prompt, a, b,
                    onA: () =>
                    {
                        if (!string.IsNullOrWhiteSpace(rememberAsFlag)) NarrativeFlags.Set(rememberAsFlag, true);
                        AdvanceThrough(ctx, ready, 0);
                    },
                    onB: () =>
                    {
                        if (!string.IsNullOrWhiteSpace(rememberAsFlag)) NarrativeFlags.Set(rememberAsFlag, false);
                        AdvanceThrough(ctx, ready, 1);
                    });

            if (!shown)
            {
                Debug.LogError($"[DialogueChoiceNode:{Guid}] No se puede enseñar la pregunta → salida A por defecto.");
                AdvanceThrough(ctx, ready, 0);
            }
        }
    }
}
