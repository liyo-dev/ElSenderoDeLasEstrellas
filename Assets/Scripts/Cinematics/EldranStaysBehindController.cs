using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Liyodev.NarrativeGraph;

/// INC-062 (parte C): por canon (ver `novela/manuscrito-novela-completo.md`, Cap. X-XIII) Eldran
/// no es arrestado — se queda fuera del castillo mientras Will, Estela y Liam entran con el
/// guardia. Sin esto, al ser un NPCPartyMember normal en FollowPlayerState, Eldran seguiría a
/// Will automáticamente en cuanto el guardia se los lleve, aunque el diálogo ya dijera lo
/// contrario (ver `DLG_GUARD.asset`, ahora "vosotros TRES debéis acompañarme"). Sacarlo del
/// equipo en el momento justo reutiliza el mismo mecanismo ya existente de "disolver equipo"
/// (NPCBehaviourManagerV2.RemoveFromParty() → NPCPartyMember.LeaveParty()), así que Eldran se
/// queda quieto en su sitio en vez de acompañarlos. Se dispara con el evento EVT_ARRESTADOS, que
/// `NPC_InteractiveNarrative_Config_Guard.asset` lanza justo al terminar el diálogo del guardia
/// y ANTES del paso de escolta que mueve al grupo hacia el castillo.
/// Añadir al GameObject de Eldran.
///
/// INC-114: Eldran también se reposiciona aquí mismo al anchor `OutSideKingdom` — el mismo punto
/// donde la novela lo sitúa esperando cuando el trío sale del castillo, Cap. XIII: "Eldran los
/// esperaba fuera, y en cuanto los vio, no pudo evitar soltar una risa cargada de alivio". Eso
/// tenía sentido cuando Eldran seguía cerca del grupo hasta ese punto, pero con este controlador
/// (Eldran se separa mucho antes, en la escena del guardia) ese corte de cámara pasaría a enseñar
/// a Eldran caminando desde un sitio arbitrario/lejano justo al terminar de hablar con el rey,
/// dentro del castillo — antes incluso de que el jugador haya salido. El paso legacy
/// correspondiente en `NPC_InteractiveNarrative_Config_Eldran.asset` se ha eliminado (su
/// `LeaveParty` ya era redundante con este script, y `OPEN_THE_DOORS` lo sigue lanzando el propio
/// grafo — `MainNarrative.asset`, nodo `RaiseCustomEventNode` "Reabrir puertas del castillo (fin
/// mision 13)" — al completarse la Misión 13, sin depender de este paso).
[DisallowMultipleComponent]
public class EldranStaysBehindController : MonoBehaviour
{
    [Header("Señal narrativa")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
    [SerializeField] private string leaveSignal = "EVT_ARRESTADOS";

    [Header("Punto de espera (INC-114)")]
    [Tooltip("Anchor (SpawnAnchor.anchorId) al que se reposiciona a Eldran en cuanto se le saca " +
             "del equipo, sin cinemática. Debe coincidir con el punto donde la novela lo sitúa " +
             "esperando cuando el trío sale del castillo.")]
    [SerializeField] private string waitingAnchorId = "OutSideKingdom";

    private NPCBehaviourManagerV2 _behaviour;

    void Awake()
    {
        _behaviour = GetComponent<NPCBehaviourManagerV2>();
    }

    void OnEnable()
    {
        DefaultNarrativeSignals.EnsureInstance().OnCustom(leaveSignal, HandleArrested);
    }

    void OnDisable()
    {
        var signals = DefaultNarrativeSignals.Instance;
        signals?.OffCustom(leaveSignal, HandleArrested);
    }

    private void HandleArrested()
    {
        if (_behaviour == null) return;
        if (_behaviour.IsInPlayerParty())
            _behaviour.RemoveFromParty();

        MoveToWaitingSpot();
    }

    private void MoveToWaitingSpot()
    {
        if (string.IsNullOrEmpty(waitingAnchorId)) return;

        var anchor = SpawnAnchor.FindById(waitingAnchorId);
        if (anchor == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[EldranStaysBehindController] No se encontró el anchor '{waitingAnchorId}' — Eldran se queda donde estaba.");
#endif
            return;
        }

        Vector3 targetPos = anchor.transform.position;
        Quaternion targetRot = anchor.GetCharacterRotation();

        // Mismo patrón que GameBootProfile.ApplyNpcPositionsToScene: Warp() sobre el NavMeshAgent
        // en vez de mover el transform a mano, para no dejar al agente en isOnNavMesh=false.
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(targetPos);
        }
        else
        {
            transform.position = targetPos;
        }
        transform.rotation = targetRot;

        GetComponent<NPCSimpleAnimator>()?.SyncTargetRotation();
    }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    [ContextMenu("Simular: Eldran se queda fuera")]
    void EditorSimulate() => HandleArrested();
#endif
}
