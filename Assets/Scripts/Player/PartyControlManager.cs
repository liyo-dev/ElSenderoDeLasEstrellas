using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;
using Game.NPC;

/// <summary>
/// Gestiona el personaje activo y el modo seguir/libre del equipo.
///
/// Slots fijos: 0 = Liam (izquierda), 1 = Will (centro), 2 = Estela (derecha)
///
/// DPad Down  → alterna Siguiendo / Libre para todos los compañeros
/// DPad Left  → cambia al personaje disponible a la izquierda
/// DPad Right → cambia al personaje disponible a la derecha
///
/// El cambio de personaje se delega a ActiveCharacterSwapper, que gestiona
/// el teleport del controller, el swap de apariencia y los hechizos.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PartyControlManager : MonoBehaviour
{
    #region Singleton
    public static PartyControlManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnActiveCharacterChanged = null;
        OnFollowModeChanged = null;
    }
#endif
    #endregion

    #region Slots
    public enum CharacterSlot { Liam = 0, Will = 1, Estela = 2 }
    #endregion

    #region Inspector
    [Tooltip("Al llegar a un extremo, DPad sigue por el otro (de Liam a la izquierda se pasa a Estela). " +
             "Así los retratos del HUD pueden poner siempre al que va al mando en el centro.")]
    [SerializeField] private bool cambioCircular = true;
    #endregion

    #region State
    private int _activeIndex = (int)CharacterSlot.Will;
    private bool _isPartyFollowing = true;
    private bool _switchingLocked;
    // Se resuelve perezosamente por ServiceLocator para decidir por ActionMode si se puede cambiar
    // el grupo (ver IsPartyChangeAllowedByCurrentMode).
    private PlayerActionManager _actionManager;
    private readonly List<NPCPartyMember> _companeros = new List<NPCPartyMember>(4);
    #endregion

    #region Anclas
    /// <summary>
    /// Clave con la que el modo Libre (DPad abajo) ancla a cada compañero donde está
    /// (<see cref="Game.NPC.Common.NPCStateContext.Anclar"/>). Solo la suelta el modo seguir
    /// (o «Sígueme» para uno), así no pisa las anclas de otros sistemas como el barco.
    /// </summary>
    public static readonly object AnclaDelModoLibre = new object();
    #endregion

    #region Events
    /// <summary>Se dispara cuando cambia el personaje activo (índice 0=Liam, 1=Will, 2=Estela)</summary>
    public static event Action<int> OnActiveCharacterChanged;

    /// <summary>Se dispara cuando cambia el modo del equipo (true=Siguiendo, false=Libre)</summary>
    public static event Action<bool> OnFollowModeChanged;
    #endregion

    #region Properties
    public int ActiveIndex => _activeIndex;
    public bool IsPartyFollowing => _isPartyFollowing;
    public CharacterSlot ActiveSlot => (CharacterSlot)_activeIndex;
    public bool IsSwitchingLocked => _switchingLocked;
    /// DPad da la vuelta al llegar a un extremo (ver cambioCircular).
    public bool CambioCircular => cambioCircular;
    /// El personaje al que se cambiaría con DPad hacia la izquierda (-1) o la derecha (+1); -1 si no hay.
    public int SlotVecino(int desde, int direccion)
    {
        for (int paso = 1; paso <= 2; paso++)
        {
            int i = desde + direccion * paso;
            if (cambioCircular) i = (i % 3 + 3) % 3;
            else if (i < 0 || i > 2) return -1;
            if (i != desde && IsSlotAvailable(i)) return i;
        }
        return -1;
    }
    #endregion

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GamepadInputReader.OnInput += HandleInput;
        GameBootService.OnProfileReady += HandleProfileReady;
        WillOnlyMomentManager.OnSwitchLockChanged += SetSwitchingLocked;
        // Sincronizar estado inicial por si EnterMoment fue llamado antes de Start
        if (WillOnlyMomentManager.Instance != null)
            _switchingLocked = WillOnlyMomentManager.Instance.IsSwitchingLocked;
    }

    private void OnDestroy()
    {
        GamepadInputReader.OnInput -= HandleInput;
        GameBootService.OnProfileReady -= HandleProfileReady;
        WillOnlyMomentManager.OnSwitchLockChanged -= SetSwitchingLocked;
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Input
    private void HandleInput(GamepadInputReader.InputEvent evt)
    {
        if (evt.Phase != InputActionPhase.Performed) return;
        if (Core.PlayerInputManager.Instance != null && Core.PlayerInputManager.Instance.IsInUIMode) return;

        switch (evt.Type)
        {
            case GamepadInputReader.InputEventType.DpadDown:
                ToggleFollowMode();
                break;
            case GamepadInputReader.InputEventType.DpadLeft:
                TrySwitchLeft();
                break;
            case GamepadInputReader.InputEventType.DpadRight:
                TrySwitchRight();
                break;
        }
    }
    #endregion

    #region Character Switching

    /// <summary>
    /// El modo activo permite cambiar el grupo: relevo o modo seguir. Solo en el reposo normal
    /// (Default) o en combate; en vuelo, nado, carga, aturdimiento, cinemática, barco o uso de
    /// punto del mundo, no: el cuerpo pasaría a un personaje con los modos de PlayerActionManager
    /// apilados sobre el anterior.
    ///
    /// El modo seguir también teletransporta al grupo, así que se bloquea igual: en el barco
    /// saca a la tripulación de su puesto (INC-808).
    /// </summary>
    private bool IsPartyChangeAllowedByCurrentMode()
    {
        if (_actionManager == null)
            _actionManager = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        if (_actionManager == null) return true; // sin manager resuelto, no bloquear por esto

        var top = _actionManager.Top;
        return top == ActionMode.Default || top == ActionMode.Combat;
    }

    private void TrySwitchLeft()
    {
        if (_switchingLocked)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PartyControlManager] DPad-Left ignorado — switching BLOQUEADO. _activeIndex={_activeIndex}");
            #endif
            return;
        }
        if (!IsPartyChangeAllowedByCurrentMode())
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PartyControlManager] DPad-Left ignorado — ActionMode actual no permite cambiar de personaje ({_actionManager?.Top}).");
            #endif
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] TrySwitchLeft — _activeIndex={_activeIndex}");
#endif
        int izquierda = SlotVecino(_activeIndex, -1);
        if (izquierda >= 0)
        {
            SwitchToCharacter(izquierda);
            return;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] TrySwitchLeft — no hay slot disponible a la izquierda de {_activeIndex}");
#endif
    }

    private void TrySwitchRight()
    {
        if (_switchingLocked)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PartyControlManager] DPad-Right ignorado — switching BLOQUEADO. _activeIndex={_activeIndex}");
            #endif
            return;
        }
        if (!IsPartyChangeAllowedByCurrentMode())
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PartyControlManager] DPad-Right ignorado — ActionMode actual no permite cambiar de personaje ({_actionManager?.Top}).");
            #endif
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] TrySwitchRight — _activeIndex={_activeIndex}");
#endif
        int derecha = SlotVecino(_activeIndex, 1);
        if (derecha >= 0)
        {
            SwitchToCharacter(derecha);
            return;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] TrySwitchRight — no hay slot disponible a la derecha de {_activeIndex}");
#endif
    }

    private void SetSwitchingLocked(bool locked)
    {
        _switchingLocked = locked;
    }

    private bool IsSlotAvailable(int index)
    {
        // A un personaje caído no se le puede pasar el mando hasta que se levante (INC-717).
        if (SaludDelGrupo.EstaCaido((CharacterSlot)index)) return false;
        return ControlDelJugador.EnElGrupo((CharacterSlot)index);
    }

    private void SwitchToCharacter(int newIndex)
    {
        if (newIndex == _activeIndex) return;

        var from = (CharacterSlot)_activeIndex;
        var to   = (CharacterSlot)newIndex;

        var swapper = ActiveCharacterSwapper.Instance;
        if (swapper == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[PartyControlManager] SwitchToCharacter({from}→{to}) ABORTADO — ActiveCharacterSwapper.Instance es null. _activeIndex NO modificado.");
#endif
            return;
        }
        if (!swapper.IsReady)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[PartyControlManager] SwitchToCharacter({from}→{to}) ABORTADO — ActiveCharacterSwapper.IsReady=false. _activeIndex NO modificado.");
#endif
            return;
        }

        _activeIndex = newIndex;
        swapper.SwitchCharacter(from, to);
        OnActiveCharacterChanged?.Invoke(_activeIndex);
    }
    #endregion

    #region Follow Mode
    private void ToggleFollowMode()
    {
        // En el barco (y en vuelo, nado, cinemáticas...) el grupo está anclado: cambiar a
        // «seguir» lo teletransportaría al jugador y sacaría a la tripulación de su puesto.
        if (!IsPartyChangeAllowedByCurrentMode()) return;

        _isPartyFollowing = !_isPartyFollowing;
        ApplyFollowModeToParty();
        OnFollowModeChanged?.Invoke(_isPartyFollowing);
    }

    /// <summary>
    /// Aplica el modo del grupo a los compañeros que lleva la IA (el personaje al mando no). En
    /// Libre cada uno se queda anclado donde está; siguiendo, se sueltan y se reagrupan con el
    /// jugador (quien no pueda llegar por una pared o una reja va andando, con aviso; INC-819).
    /// </summary>
    private void ApplyFollowModeToParty()
    {
        var companeros = CompanerosQueLlevaLaIA();
        if (_isPartyFollowing)
        {
            // Se sueltan antes de reagrupar para que el teletransporte los ponga a seguir.
            for (int i = 0; i < companeros.Count; i++)
                companeros[i].NPCManager?.Context?.Soltar(AnclaDelModoLibre);

            if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(avisar: true);
            ActiveCharacterSwapper.Instance?.TeleportWillNpcToPlayer(avisar: true);

            for (int i = 0; i < companeros.Count; i++)
                if (!companeros[i].EstaSiguiendo) Seguir(companeros[i]);
        }
        else
        {
            for (int i = 0; i < companeros.Count; i++)
            {
                companeros[i].StopFollowing();
                companeros[i].NPCManager?.Context?.Anclar(AnclaDelModoLibre);
            }
        }
    }

    /// <summary>
    /// Pone a un compañero en el modo actual del grupo: siguiendo, lo suelta y lo pone a seguir;
    /// en Libre, lo ancla donde está. Lo usa el relevo de personaje al devolver un cuerpo a la IA.
    /// </summary>
    public static void PonerEnModoDelGrupo(NPCPartyMember npc)
    {
        if (npc == null) return;
        var contexto = npc.NPCManager != null ? npc.NPCManager.Context : null;
        if (Instance == null || Instance._isPartyFollowing)
        {
            contexto?.Soltar(AnclaDelModoLibre);
            Seguir(npc);
        }
        else
        {
            npc.StopFollowing();
            contexto?.Anclar(AnclaDelModoLibre);
        }
    }

    // Un miembro del grupo sigue como tal; el NPC de Will no es miembro y sigue sin esa comprobación.
    private static void Seguir(NPCPartyMember npc)
    {
        if (npc.IsInParty) npc.StartFollowing();
        else npc.StartFollowingIgnorePartyCheck();
    }

    /// <summary>
    /// Los compañeros que lleva la IA: el NPC de cada personaje que no va al mando
    /// (<see cref="ControlDelJugador.NpcDe"/>) y los invitados del grupo que no son personajes.
    /// </summary>
    private List<NPCPartyMember> CompanerosQueLlevaLaIA()
    {
        _companeros.Clear();
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var npc = ControlDelJugador.NpcDe((CharacterSlot)i);
            if (npc != null) _companeros.Add(npc);
        }
        if (PlayerParty.HasInstance)
        {
            var miembros = PlayerParty.Instance.Members;
            for (int i = 0; i < miembros.Count; i++)
            {
                var m = miembros[i];
                if (m != null && !ControlDelJugador.SlotDeNombre(m.DisplayName, out _)) _companeros.Add(m);
            }
        }
        return _companeros;
    }
    #endregion

    #region Public API
    /// <summary>Fuerza el refresco de eventos al cargar una escena o inicializar la UI.</summary>
    public void ForceRefreshFollowMode()
    {
        OnFollowModeChanged?.Invoke(_isPartyFollowing);
        OnActiveCharacterChanged?.Invoke(_activeIndex);
    }

    /// <summary>
    /// Fuerza el cambio de personaje activo, ignorando el bloqueo de switching.
    /// Usado internamente por WillOnlyMomentManager.
    /// </summary>
    public void ForceSwitch(CharacterSlot slot)
    {
        int newIndex = (int)slot;
        if (newIndex == _activeIndex)
        {
            // Ya en este personaje: refrescar listeners sin hacer swap visual
            OnActiveCharacterChanged?.Invoke(_activeIndex);
            return;
        }
        SwitchToCharacter(newIndex);
    }
    #endregion

    private void HandleProfileReady()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] 🔄 GameBootService.OnProfileReady recibido. _activeIndex={_activeIndex} ({(CharacterSlot)_activeIndex}). Reinicializando estado del party.");
#endif

        // WillOnlyMomentManager es DontDestroyOnLoad: un momento «solo Will» interrumpido sin
        // cerrarse (p. ej. morir durante un minijuego) dejaría el cambio de personaje bloqueado en
        // todas las partidas cargadas después.
        WillOnlyMomentManager.Instance?.ClearAllMoments();

        // Al cargar partida siempre se arranca como Will
        _activeIndex = (int)CharacterSlot.Will;
        ActiveCharacterSwapper.Instance?.ResetState();

        _isPartyFollowing = true;
        ForceRefreshFollowMode();
    }
}
