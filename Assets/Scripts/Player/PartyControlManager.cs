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
/// En Libre, el que va al mando puede pedir «Sígueme» a un compañero anclado y forman una pareja
/// (líder y seguidor; una sola a la vez). Con pareja, el cambio de personaje va por grupos: del
/// líder al que está solo, y del que está solo otra vez al líder; el seguidor no se elige con la
/// cruceta y se queda quieto junto al líder mientras el jugador lleva a otro. «Deja de seguir»
/// (al seguidor, de frente) o volver a Siguiendo deshacen la pareja. El HUD lo enseña así: el
/// retrato del seguidor va pegado al de su líder (<see cref="LeerPareja"/>). Ver INC-1200.
///
/// Cada personaje tiene su cuerpo: cambiar de personaje es pasar el mando de un cuerpo a otro
/// (<see cref="ControlDelJugador.CambiarElMando"/>).
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
        OnParejaChanged = null;
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

    private readonly List<NPCPartyMember> _companeros = new List<NPCPartyMember>(4);
    // Pareja del modo Libre: el personaje que pidió «Sígueme» y el que le sigue (-1 si no hay).
    private int _lider = -1;
    private int _seguidor = -1;
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

    /// <summary>Se dispara al formarse o deshacerse la pareja del modo Libre («Sígueme»).</summary>
    public static event Action OnParejaChanged;
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
            if (i != desde && IsSlotAvailable(i) && !EsSeguidorDeLaPareja(i)) return i;
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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        WillOnlyMomentManager.OnSwitchLockChanged += SetSwitchingLocked;
        // Sincronizar estado inicial por si EnterMoment fue llamado antes de Start
        if (WillOnlyMomentManager.Instance != null)
            _switchingLocked = WillOnlyMomentManager.Instance.IsSwitchingLocked;
    }

    private void OnDestroy()
    {
        GamepadInputReader.OnInput -= HandleInput;
        GameBootService.OnProfileReady -= HandleProfileReady;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
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
        // El del cuerpo que lleva el jugador ahora.
        if (!PlayerService.TryGetComponent(out PlayerActionManager acciones, allowSceneLookup: false) || acciones == null)
            return true; // sin manager resuelto, no bloquear por esto

        var top = acciones.Top;
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
            Debug.LogWarning($"[PartyControlManager] DPad-Left ignorado — ActionMode actual no permite cambiar de personaje.");
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
            Debug.LogWarning($"[PartyControlManager] DPad-Right ignorado — ActionMode actual no permite cambiar de personaje.");
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

        if (!ControlDelJugador.TieneCuerpo(to))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PartyControlManager] SwitchToCharacter({from}→{to}): {to} no tiene cuerpo en escena.");
#endif
            return;
        }

        // Con pareja, el seguidor va con su líder: se queda quieto junto a él mientras el jugador
        // lleva a otro y vuelve a seguirle al regresar. Si el mando pasa al propio seguidor (relevo
        // al caer, momento «solo Will»), la pareja se deshace.
        bool volverAlLider = false;
        if (HayParejaValida())
        {
            if (newIndex == _seguidor) DeshacerPareja(anclarAlSeguidor: false);
            else if (_activeIndex == _lider) AnclarAlSeguidor();
            else if (newIndex == _lider) volverAlLider = true;
        }

        _activeIndex = newIndex;
        // Con el grupo en «Siguiendo» y sin pareja, el personaje nuevo aparece donde iba el jugador. Ver INC-1369
        if (_isPartyFollowing && !HayParejaValida()) ControlDelJugador.CambiarElMandoEnElSitio(from, to);
        else ControlDelJugador.CambiarElMando(from, to);
        if (volverAlLider) SoltarAlSeguidor();
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
        // Al cambiar de modo no queda pareja: reagrupados todos siguen, y en Libre todos se anclan.
        if (_seguidor >= 0) DeshacerPareja(anclarAlSeguidor: false);
        ApplyFollowModeToParty();
        OnFollowModeChanged?.Invoke(_isPartyFollowing);
    }

    /// <summary>
    /// Deja el grupo en «Siguiendo» aunque estuviera en Libre. Lo piden las pruebas del jugador al
    /// empezar cada intento: la prueba se juega con el grupo y no con los compañeros anclados de
    /// una sala anterior. La pantalla está tapada cuando se llama.
    /// </summary>
    public void PonerEnModoSiguiendo()
    {
        if (_isPartyFollowing) return;

        _isPartyFollowing = true;
        if (_seguidor >= 0) DeshacerPareja(anclarAlSeguidor: false);
        ApplyFollowModeToParty();
        OnFollowModeChanged?.Invoke(true);
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
        if (DebeSeguirAlJugador(npc))
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

    // Un miembro del grupo sigue como tal; Will no es miembro de PlayerParty y sigue sin esa comprobación.
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

    #region Pareja del modo Libre («Sígueme»)
    /// Hay pareja: en Libre, un compañero sigue al personaje que se lo pidió.
    public bool HayPareja => HayParejaValida();

    /// <summary>
    /// La pareja del modo Libre tal como está ahora: quién la pidió y quién le sigue, con la
    /// pareja validada una sola vez. La usa el HUD para pintar al seguidor junto a su líder.
    /// </summary>
    public bool LeerPareja(out int lider, out int seguidor)
    {
        if (!HayParejaValida()) { lider = -1; seguidor = -1; return false; }
        lider = _lider;
        seguidor = _seguidor;
        return true;
    }

    /// Se le puede pedir «Sígueme» a ese personaje: grupo en Libre, sin otra pareja, lo lleva la IA,
    /// no está caído y el modo de acción permite cambios en el grupo.
    public bool PuedePedirQueSiga(CharacterSlot quien)
    {
        if (_isPartyFollowing || HayParejaValida()) return false;
        if ((int)quien == _activeIndex || !IsSlotAvailable((int)quien)) return false;
        if (ControlDelJugador.NpcDe(quien) == null) return false;
        return IsPartyChangeAllowedByCurrentMode();
    }

    /// Se le puede decir «Deja de seguir» a ese personaje: es el seguidor y va al mando su líder.
    public bool PuedeDejarDeSeguir(CharacterSlot quien) =>
        HayParejaValida() && (int)quien == _seguidor && _activeIndex == _lider;

    /// El personaje al mando pide a otro que le siga. Devuelve false si no se puede.
    public bool PedirQueSiga(CharacterSlot quien)
    {
        if (!PuedePedirQueSiga(quien)) return false;
        var npc = ControlDelJugador.NpcDe(quien);
        _lider = _activeIndex;
        _seguidor = (int)quien;
        npc.NPCManager?.Context?.Soltar(AnclaDelModoLibre);
        Seguir(npc);
        OnParejaChanged?.Invoke();
        return true;
    }

    /// El seguidor se queda donde está y la pareja se deshace.
    public void DejarDeSeguir()
    {
        if (HayParejaValida()) DeshacerPareja(anclarAlSeguidor: true);
    }

    /// Ese compañero tiene que ir detrás del jugador: con el grupo reagrupado, todos; en Libre,
    /// solo el seguidor de la pareja y solo mientras va al mando su líder. Es la pregunta única
    /// para cualquier sistema que vaya a poner a seguir a un compañero.
    public static bool DebeSeguirAlJugador(NPCPartyMember npc)
    {
        var pcm = Instance;
        if (pcm == null || pcm._isPartyFollowing) return true;
        if (!pcm.HayParejaValida() || pcm._activeIndex != pcm._lider) return false;
        return ControlDelJugador.PersonajeDelNpc(npc, out var slot) && (int)slot == pcm._seguidor;
    }

    private bool EsSeguidorDeLaPareja(int slot) => slot == _seguidor && HayParejaValida();

    // La pareja deja de valer si el grupo se reagrupa, si uno de los dos sale del grupo o si el
    // seguidor pasa a ir al mando; entonces se deshace aquí, la pregunte quien la pregunte.
    private bool HayParejaValida()
    {
        if (_seguidor < 0) return false;
        bool valida = !_isPartyFollowing
                      && _seguidor != _activeIndex
                      && ControlDelJugador.EnElGrupo((CharacterSlot)_lider)
                      && ControlDelJugador.EnElGrupo((CharacterSlot)_seguidor);
        if (!valida) DeshacerPareja(anclarAlSeguidor: !_isPartyFollowing);
        return valida;
    }

    private void DeshacerPareja(bool anclarAlSeguidor)
    {
        int seguidor = _seguidor;
        _lider = -1;
        _seguidor = -1;
        if (anclarAlSeguidor && seguidor >= 0) AnclarDondeEsta(ControlDelJugador.NpcDe((CharacterSlot)seguidor));
        OnParejaChanged?.Invoke();
    }

    private void AnclarAlSeguidor() => AnclarDondeEsta(ControlDelJugador.NpcDe((CharacterSlot)_seguidor));

    private void SoltarAlSeguidor()
    {
        var npc = ControlDelJugador.NpcDe((CharacterSlot)_seguidor);
        if (npc == null) return;
        npc.NPCManager?.Context?.Soltar(AnclaDelModoLibre);
        Seguir(npc);
    }

    private static void AnclarDondeEsta(NPCPartyMember npc)
    {
        if (npc == null) return;
        npc.StopFollowing();
        npc.NPCManager?.Context?.Anclar(AnclaDelModoLibre);
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

    // Los cuerpos de una escena nueva aparecen con el piloto de su prefab: el del personaje que va
    // al mando lo toma el jugador y los demás pasan a la IA.
    private void AlCargarEscena(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.LoadSceneMode __)
        => ControlDelJugador.RepartirPilotos();

    private void HandleProfileReady()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PartyControlManager] GameBootService.OnProfileReady recibido. _activeIndex={_activeIndex} ({(CharacterSlot)_activeIndex}). Reinicializando estado del party.");
#endif

        // WillOnlyMomentManager es DontDestroyOnLoad: un momento «solo Will» interrumpido sin
        // cerrarse (p. ej. morir durante un minijuego) dejaría el cambio de personaje bloqueado en
        // todas las partidas cargadas después.
        WillOnlyMomentManager.Instance?.ClearAllMoments();

        // Al cargar partida siempre se arranca como Will
        _activeIndex = (int)CharacterSlot.Will;
        ControlDelJugador.RepartirPilotos();

        _isPartyFollowing = true;
        _lider = -1;
        _seguidor = -1;
        OnParejaChanged?.Invoke();
        ForceRefreshFollowMode();
    }
}
