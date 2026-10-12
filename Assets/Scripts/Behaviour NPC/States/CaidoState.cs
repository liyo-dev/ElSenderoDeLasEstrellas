using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Un personaje del grupo que lleva la IA ha caído en combate: está en el suelo y no lucha ni
    /// sigue a nadie hasta que lo levantan (<see cref="CuerpoDelGrupo.Levantar"/>, que lo saca de
    /// este estado). No tiene transiciones propias. Ver INC-717.
    /// </summary>
    public class CaidoState : NPCStateBase
    {
        public const string Nombre = "Caido";
        public override string StateName => Nombre;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            if (context.Agent != null) NavMeshAgentUtility.HardStop(context.Agent);
            context.IsInCombat = false;
            context.IsActivelyFollowingPlayer = false;
            context.Animator?.PlayDeath();
        }

        public override INPCState CheckTransitions(NPCStateContext context) => null;
    }
}
