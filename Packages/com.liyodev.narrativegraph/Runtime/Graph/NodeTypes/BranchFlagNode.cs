using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Bifurca según un flag narrativo persistente (SetFlagNode) o, si no existe como flag, según
    /// si la señal con esa misma clave se disparó alguna vez (Signals.HasEverRaised).
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Flujo", "Según flag", "Bifurca Sí/No según un flag narrativo (o una señal ya disparada).")]
    [SavePoint("Solo consulta estado; seguro para guardar")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class BranchFlagNode : NarrativeNode, INarrativeRouteEffect
    {
        [NarrativeKey(NarrativeKeyKind.Flag)]
        [Tooltip("Clave del flag (SetFlagNode) o de la señal a comprobar.")]
        [SerializeField] private string flagKey;
        public string FlagKey { get => flagKey; set => flagKey = value; }

        [Tooltip("Si está marcado, invierte el resultado (Sí ↔ No).")]
        [SerializeField] private bool invert;

        static readonly string[] Ports = { "Sí", "No" };
        public override string[] GetOutputPorts() => Ports;

        public override void Describe(INarrativeNodeDescriber d) => d.Flag(flagKey, $"¿{flagKey}?{(invert ? " (invertido)" : "")}");

        public override string DescribeDecision()
            => $"Según flag \"{DisplayTitle}\" (guid {Guid}): comprueba '{flagKey}'{(invert ? " (invertido)" : "")}. Salidas: Sí / No.";

        /// The branch taken tells what the flag was worth at that point.
        public void ProjectRoute(INarrativeStateWriter state, int chosenPort)
        {
            if (string.IsNullOrWhiteSpace(flagKey) || chosenPort < 0 || chosenPort > 1) return;
            bool yes = chosenPort == 0;
            state.SetFlag(flagKey, invert ? !yes : yes);
        }

        public override void Enter(NarrativeContext ctx, Action ready)
        {
            bool value = false;
            if (string.IsNullOrWhiteSpace(flagKey))
            {
                Debug.LogWarning($"[BranchFlagNode:{Guid}] flagKey vacío → 'No'.");
            }
            else
            {
                value = NarrativeFlags.Has(flagKey);
                if (!value && ctx?.Signals != null)
                    value = ctx.Signals.HasEverRaised(flagKey);
            }
            if (invert) value = !value;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[BranchFlagNode:{Guid}] {flagKey} → {(value ? "Sí" : "No")}");
#endif
            AdvanceThrough(ctx, ready, value ? 0 : 1);
        }
    }
}
