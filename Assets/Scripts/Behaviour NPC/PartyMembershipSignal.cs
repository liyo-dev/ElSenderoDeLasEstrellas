using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC
{
    /// <summary>
    /// Escucha un evento narrativo custom y une o saca a un NPCPartyMember del grupo.
    /// Mismo patron que CastleNarrativeEventListener: se resuscribe tras un reset del grafo
    /// narrativo (DefaultNarrativeSignals.OnAfterReset), asi que sobrevive a un reinicio
    /// de partida sin quedarse "sordo".
    ///
    /// Pensado para companeros que se unen/salen del grupo por guion (p.ej. Oliver), sin
    /// pasar por el prompt "Sigueme" del jugador.
    /// </summary>
    public class PartyMembershipSignal : MonoBehaviour
    {
        public enum SignalAction
        {
            Join,
            Leave,
        }

        [Tooltip("Clave del evento narrativo que dispara la accion (RaiseCustomEventNode en el grafo)")]
        [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
        [SerializeField] private string narrativeEventKey;

        [SerializeField] private NPCPartyMember partyMember;

        [SerializeField] private SignalAction action = SignalAction.Join;

        private System.Action _handler;

        void Start()
        {
            // NPCPartyMember se anade en tiempo de ejecucion por NPCBehaviourManagerV2
            // cuando el NPC tiene el flag Companion, asi que no existe todavia al editar
            // el prefab/la escena -- se resuelve aqui por si no se asigno a mano.
            if (partyMember == null) partyMember = GetComponent<NPCPartyMember>();
            if (string.IsNullOrEmpty(narrativeEventKey) || partyMember == null) return;

            _handler = OnNarrativeEvent;
            DefaultNarrativeSignals.OnAfterReset += ReSubscribe;
            TrySubscribe();
        }

        void OnDestroy()
        {
            DefaultNarrativeSignals.OnAfterReset -= ReSubscribe;
            DefaultNarrativeSignals.Instance?.OffCustom(narrativeEventKey, _handler);
        }

        private void TrySubscribe()
        {
            DefaultNarrativeSignals.Instance?.OnCustom(narrativeEventKey, _handler);
        }

        private void ReSubscribe() => TrySubscribe();

        private void OnNarrativeEvent()
        {
            if (action == SignalAction.Join)
                partyMember.JoinParty();
            else
                partyMember.LeaveParty();
        }
    }
}
