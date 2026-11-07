using Game.NPC;
using Game.NPC.States;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Un compañero del grupo llevado por algo que se mueve (un barco, una atracción). Al tomarlo se le
/// ancla con la clave de quien lo lleva (<see cref="Game.NPC.Common.NPCStateContext.Anclar"/>) para
/// que ni el seguimiento del grupo ni el cambio de personaje lo muevan, se le para el agente y se
/// le cuelga de su puesto, que se lo lleva sin escribir su transform cada frame. Al soltarlo se le
/// deja en tierra junto al punto dado y se le devuelve todo. Ver INC-891.
/// </summary>
public sealed class PasajeroNpc
{
    private readonly object _quien;
    private readonly NavMeshAgent _agente;
    private readonly bool _agenteEstabaActivo;
    private readonly Transform _padreOriginal;
    private NPCAmbientActivity _pose = NPCAmbientActivity.None;

    /// <param name="npc">El compañero.</param>
    /// <param name="quien">Clave del ancla: quien lo lleva (el barco, la atracción).</param>
    public PasajeroNpc(NPCPartyMember npc, object quien)
    {
        Npc = npc;
        _quien = quien;
        _agente = npc.GetComponent<NavMeshAgent>();
        _agenteEstabaActivo = _agente != null && _agente.enabled;
        Animador = npc.GetComponentInChildren<NPCSimpleAnimator>();
        _padreOriginal = npc.transform.parent;
    }

    public NPCPartyMember Npc { get; }
    public NPCSimpleAnimator Animador { get; }

    /// <summary>
    /// Lo ancla con la clave de quien lo lleva. Si venía de combate deja de estarlo: mientras va
    /// llevado manda quien lo lleva, no AllyCombatState.
    /// </summary>
    public void Anclar()
    {
        var contexto = Npc != null && Npc.NPCManager != null ? Npc.NPCManager.Context : null;
        if (contexto == null) return;
        contexto.Anclar(_quien);
        contexto.IsInCombat = false;
    }

    /// <summary>
    /// Le quita el control a su IA: el estado de diálogo se ancla (o pasa a reposo) y el agente deja
    /// de escribir la posición; si siguiera navegando pelearía con quien lo mueve.
    /// </summary>
    public void TomarControl()
    {
        if (Npc == null) return;

        if (Npc.NPCManager?.Brain?.CurrentState is DialoguePositionState estadoDeDialogo)
            estadoDeDialogo.Anclar();
        else
            Npc.NPCManager?.ForceIdle();

        if (_agente != null)
        {
            if (_agente.isOnNavMesh)
            {
                _agente.isStopped = true;
                _agente.ResetPath();
            }
            _agente.velocity = Vector3.zero;
            _agente.updatePosition = false;
            _agente.updateRotation = false;
            _agente.enabled = false;
        }

        if (Animador != null)
        {
            Animador.DisableAutoRotation();
            Animador.StopMovement();
            Animador.ForzarEnElSuelo();
        }
    }

    /// <summary>
    /// Lo cuelga de <paramref name="puesto"/> (en su origen) con la rotación dada y la pose del
    /// puesto: <see cref="NPCAmbientActivity.None"/> = de pie, con el idle normal.
    /// </summary>
    public void Colgar(Transform puesto, Quaternion rotacion, NPCAmbientActivity pose, bool ponerPoseYa = true)
    {
        if (Npc == null || puesto == null) return;
        _pose = pose;

        Npc.transform.SetParent(puesto, false);
        Npc.transform.localPosition = Vector3.zero;
        Npc.transform.rotation = rotacion;

        if (Animador == null) return;

        // Cualquier actividad ambiental anterior (sentarse, comer...) dejaría viva su corrutina y
        // volvería a pisar la pose del puesto al terminar su Begin.
        Animador.DetenerActividadAmbiental();

        // El movimiento a mano deja desfasado el chequeo de suelo; sin esto, el «Any State → Falling»
        // del Animator interrumpe la pose y parece que el personaje se mueve o convulsiona.
        Animador.ForzarEnElSuelo();
        Animador.QuitarRootMotion();

        if (ponerPoseYa) PonerPose();
    }

    /// <summary>Pone la pose del puesto (sentado o de pie), si sigue colgado de él.</summary>
    public void PonerPose()
    {
        if (Animador == null || Npc == null) return;
        Animador.ForzarEnElSuelo();
        Animador.QuitarRootMotion();
        if (_pose != NPCAmbientActivity.None) Animador.PlayAmbientActivity(_pose);
        else Animador.PlayIdleNormal();
    }

    /// <summary>
    /// Lo deja en tierra alrededor de <paramref name="destino"/> (sobre el NavMesh más cercano) y le
    /// devuelve su agente, su padre, el ancla y el seguimiento del grupo.
    /// </summary>
    public void Soltar(Vector3 destino, int indice)
    {
        if (Npc == null) return;

        // La postura se apaga antes de devolverlo al grupo: si no, se queda sentado.
        if (_pose != NPCAmbientActivity.None && Animador != null)
            Animador.StopAmbientActivity(_pose);

        Npc.transform.SetParent(_padreOriginal, true);

        Vector3 salida = destino + Quaternion.Euler(0f, 120f * indice, 0f) * Vector3.forward * 0.7f;
        if (NavMesh.SamplePosition(salida, out var navHit, 2f, NavMesh.AllAreas))
            salida = navHit.position;
        Npc.transform.position = salida;

        if (_agente != null)
        {
            _agente.enabled = _agenteEstabaActivo;
            if (_agente.enabled)
            {
                _agente.updatePosition = true;
                _agente.updateRotation = true;
                _agente.velocity = Vector3.zero;
                if (_agente.isOnNavMesh)
                {
                    _agente.Warp(salida);
                    _agente.isStopped = false;
                    _agente.ResetPath();
                }
            }
        }

        if (Animador != null)
        {
            Animador.ForzarEnElSuelo();
            Animador.PlayIdleNormal();
            Animador.EnableAutoRotation();
        }

        // Suelta solo su ancla: si otro sistema lo tenía anclado (modo Libre), sigue.
        var contexto = Npc.NPCManager != null ? Npc.NPCManager.Context : null;
        contexto?.Soltar(_quien);

        bool siguiendo = PartyControlManager.Instance == null || PartyControlManager.Instance.IsPartyFollowing;
        if (!siguiendo || (contexto != null && contexto.EstaAnclado))
        {
            Npc.NPCManager?.ForceIdle();
            return;
        }

        if (Npc.IsInParty) Npc.StartFollowing();
        else Npc.StartFollowingIgnorePartyCheck();
    }
}
