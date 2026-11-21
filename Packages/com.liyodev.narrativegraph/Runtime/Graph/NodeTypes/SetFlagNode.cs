using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Marca (o borra) un flag narrativo persistente (NarrativeFlags): se guarda con la partida y se
    /// consulta con BranchFlagNode.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Flujo", "Poner flag", "Guarda un hecho persistente ('PUENTE_REPARADO') consultable con 'Según flag'.")]
    [SavePoint("Instantáneo")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class SetFlagNode : NarrativeNode, INarrativeStateEffect
    {
        [NarrativeKey(NarrativeKeyKind.Flag)]
        [Tooltip("Clave del flag. Convención: MAYÚSCULAS_CON_GUIONES, sin prefijo.")]
        [SerializeField] private string flagKey;
        public string FlagKey { get => flagKey; set => flagKey = value; }

        [Tooltip("true = activar el flag; false = borrarlo.")]
        [SerializeField] private bool value = true;
        public bool Value { get => this.value; set => this.value = value; }

        public override void Describe(INarrativeNodeDescriber d) => d.Flag(flagKey, $"{flagKey} = {(value ? "sí" : "no")}");

        public void Project(INarrativeStateWriter state)
        {
            if (!string.IsNullOrWhiteSpace(flagKey)) state.SetFlag(flagKey, value);
        }

        public override void Enter(NarrativeContext ctx, Action ready)
        {
            if (string.IsNullOrWhiteSpace(flagKey))
                Debug.LogWarning($"[SetFlagNode:{Guid}] flagKey vacío; no se guarda nada.");
            else
            {
                NarrativeFlags.Set(flagKey, value);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[SetFlagNode:{Guid}] {flagKey} = {value}");
#endif
            }
            ready?.Invoke();
        }
    }
}
