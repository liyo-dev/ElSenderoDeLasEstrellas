using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Punto de arranque: «aquí el jugador está así». En juego no hace nada (pasa de largo); al probar
    /// desde cualquier paso, la historia se rehace en orden y cada punto, en su sitio, pone lo que fija
    /// en la ficha del jugador (dónde aparece, vida, lo que sea que tenga el juego). Lo que la historia
    /// da después del punto se suma encima.
    ///
    /// El id es estable aunque el grafo cambie, a diferencia del guid del nodo.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Flujo", "Punto de arranque", "Cómo está el jugador al llegar aquí; al probar desde un paso se usa el punto anterior más cercano.")]
    [SavePoint("Instantáneo")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class CheckpointNode : NarrativeNode, INarrativePlayerSetup, INarrativeStateEffect
    {
        [Tooltip("Identificador estable y legible: 'ANTES_DEL_PUENTE'.")]
        [SerializeField] private string checkpointId;
        public string CheckpointId { get => checkpointId; set => checkpointId = value; }

        [Tooltip("Lo que fija este punto en la ficha del jugador. Se edita en la pestaña Arranque del editor.")]
        [SerializeField] private NarrativePlayerValues player = new();
        public NarrativePlayerValues Player { get => player; set => player = value; }

        public NarrativePlayerValues PlayerValues => player;
        public override string Badge => "◆";
        public override bool OpensSection => true;

        public void Project(INarrativeStateWriter state)
        {
            if (player != null && !player.IsEmpty && state.TryGetExtension<INarrativePlayerValuesState>(out var target))
                target.ApplyPlayerValues(player);
        }

        public override void Describe(INarrativeNodeDescriber d)
        {
            d.Line("◆ " + (string.IsNullOrEmpty(checkpointId) ? "(sin id)" : checkpointId), NarrativeTextTone.Strong);
            if (player != null && !player.IsEmpty) d.Field("jugador", string.Join(", ", player.fields));
        }

        public override void CollectWarnings(List<string> warnings)
        {
            if (string.IsNullOrEmpty(checkpointId)) warnings.Add("checkpointId vacío");
        }

        public override void Enter(NarrativeContext ctx, Action ready) => ready?.Invoke();
    }
}
