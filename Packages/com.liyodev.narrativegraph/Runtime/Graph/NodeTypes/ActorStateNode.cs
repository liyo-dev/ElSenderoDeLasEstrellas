using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Si está en el mundo un personaje (y dónde) durante una situación de la historia.
    public enum ActorPresence
    {
        /// Se queda donde esté.
        Unchanged,
        /// Se coloca en una marca del mundo, sin que el jugador lo vea.
        AtMark,
        /// No está (se ha ido de viaje, aún no ha llegado…).
        Away,
    }

    /// «Personaje desde aquí»: desde este paso, dónde está un personaje y qué dice si le hablas cuando
    /// la historia no le espera. Vale hasta la siguiente pieza de ese mismo personaje.
    ///
    /// Dos capas: «Lo de siempre» (lo que hace cuando no pasa nada) y «Desde aquí» (una situación que
    /// manda mientras dura; «Vuelve a lo de siempre» la acaba). El juego aplica lo que dice a través de
    /// <see cref="INarrativeActorPresence"/> y da sus frases con <see cref="NarrativeStandingLines"/>;
    /// el GameObject del personaje solo hace lo suyo (andar, animarse, hablar).
    [Serializable]
    [NarrativeNodeInfo("NPCs", "Personaje desde aquí", "Desde este paso, dónde está un personaje y qué dice si le hablas cuando la historia no le espera. Vale hasta la siguiente pieza de ese personaje.")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class ActorStateNode : NarrativeNode, INarrativeStateEffect
    {
        public enum Layer
        {
            /// Una situación de la historia: manda hasta «Vuelve a lo de siempre» u otra «Desde aquí».
            FromHere,
            /// Lo que hace cuando no hay ninguna situación.
            Usual,
            /// Acaba la situación «Desde aquí»: vuelve a lo de siempre.
            BackToUsual,
        }

        [NarrativeKey(NarrativeKeyKind.Actor)]
        [Tooltip("Id persistente del personaje.")]
        [SerializeField] private string actorId;
        public string ActorId { get => actorId; set => actorId = value; }

        [Tooltip("Desde aquí: una situación que manda mientras dura. Lo de siempre: cuando no pasa nada. Vuelve a lo de siempre: acaba la situación.")]
        [SerializeField] private Layer layer = Layer.FromHere;
        public Layer PieceLayer { get => layer; set => layer = value; }

        [Tooltip("Si está y dónde. Los cambios se hacen sin que el jugador lo vea.")]
        [SerializeField] private ActorPresence presence = ActorPresence.Unchanged;
        public ActorPresence Presence { get => presence; set => presence = value; }

        [NarrativeKey(NarrativeKeyKind.Anchor)]
        [Tooltip("Marca del mundo donde se coloca (con «En una marca»).")]
        [SerializeField] private string markId;
        public string MarkId { get => markId; set => markId = value; }

        [Tooltip("Lo que dice si le hablas y la historia no le espera: una por charla, en orden; al acabar vuelve a empezar.")]
        [SerializeField] private List<NarrativeDialogueAsset> lines = new();
        public List<NarrativeDialogueAsset> Lines { get => lines; set => lines = value; }

        /// El hecho que deja en el estado de la historia (categoría y valor).
        public (string category, string value) Fact => layer switch
        {
            Layer.Usual => (NarrativeCoreFacts.ActorUsual, Guid),
            Layer.BackToUsual => (NarrativeCoreFacts.ActorState, null),
            _ => (NarrativeCoreFacts.ActorState, Guid),
        };

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            NarrativeActorStates.Apply(this);
            onReadyToAdvance?.Invoke();
        }

        public void Project(INarrativeStateWriter state)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return;
            // Solo el hecho: dónde está lo deduce quien lo lee de la pieza que vale (así «vuelve a lo
            // de siempre» no deja al personaje colocado donde le puso la pieza anterior).
            var (category, value) = Fact;
            state.SetFact(category, actorId, value);
        }

        public override void Describe(INarrativeNodeDescriber d)
        {
            string who = string.IsNullOrEmpty(actorId) ? "(sin personaje)" : actorId;
            switch (layer)
            {
                case Layer.BackToUsual:
                    d.Line($"{who}: vuelve a lo de siempre", NarrativeTextTone.Strong);
                    return;
                case Layer.Usual:
                    d.Line($"{who}: lo de siempre", NarrativeTextTone.Strong);
                    break;
                default:
                    d.Line($"{who} desde aquí", NarrativeTextTone.Strong);
                    break;
            }
            d.Field("está", presence switch
            {
                ActorPresence.AtMark => string.IsNullOrEmpty(markId) ? "en una marca (falta)" : markId,
                ActorPresence.Away => "no está",
                _ => "donde esté",
            });
            int n = lines?.FindAll(l => l != null).Count ?? 0;
            if (n > 0) d.Field("si le hablas", n == 1 ? "1 frase" : $"{n} frases");
        }

        public override void CollectWarnings(List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(actorId)) warnings.Add("falta el personaje");
            if (layer != Layer.BackToUsual && presence == ActorPresence.AtMark && string.IsNullOrWhiteSpace(markId)) warnings.Add("falta la marca");
        }
    }
}
