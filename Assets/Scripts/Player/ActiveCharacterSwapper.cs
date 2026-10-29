using UnityEngine;
using UnityEngine.AI;
using Game.NPC;

/// <summary>
/// Orquesta el cambio de personaje activo en el sistema de equipo.
///
/// Cuando el jugador cambia a Liam o Estela:
///   1. Teleporta el controller de Will a la posición del NPC objetivo.
///   2. Aplica la apariencia del personaje vía CharacterAppearanceRegistry.
///   3. Actualiza los hechizos del MagicCaster con los del NPCPartyConfig.
///   4. Oculta el NPC objetivo (el controller ES ese personaje ahora).
///   5. Reactiva el NPC anterior (Will vuelve a ser un compañero IA, o Liam/Estela reanudan seguimiento).
///
/// Will nunca desaparece del mundo: cuando no es el personaje activo su NPC
/// permanece visible y sigue al jugador como IA.
///
/// Es el único dueño de la visibilidad del NPC oculto (el del personaje al mando): lo oculta al
/// relevar y lo vuelve a mostrar al soltarlo. Los demás sistemas preguntan
/// <see cref="ControlDelJugador.RepresentadoPorElJugador"/> antes de encender renderers.
/// Quién va al mando y qué cuerpo tiene cada personaje se pregunta a
/// <see cref="ControlDelJugador"/>, no a <see cref="HiddenNpc"/> ni a <see cref="WillNpcInstance"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActiveCharacterSwapper : MonoBehaviour
{
    #region Singleton
    public static ActiveCharacterSwapper Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Componentes del player")]
    [Tooltip("El MagicCaster del jugador. Se auto-busca si no se asigna.")]
    [SerializeField] private MagicCaster magicCaster;

    [Header("NPC de Will (prefab, se instancia cuando Will no es el activo)")]
    [Tooltip("Prefab de Will como NPC. Se instancia al cambiar a Liam/Estela y se destruye al volver a Will.")]
    [SerializeField] private GameObject willNpcPrefab;

    private NPCPartyMember _willNpcInstance;

    /// <summary>
    /// El NPC instanciado de Will (cuando no es el personaje activo). Se pregunta a través de
    /// <see cref="ControlDelJugador.NpcDe"/>.
    /// </summary>
    public NPCPartyMember WillNpcInstance => _willNpcInstance;

    // Hechizos de Will, actualizados cada vez que se abandona su slot
    // Básicos de Will (orden de rotación) mientras el jugador lleva a otro personaje.
    private readonly MagicSpellSO[] _willBasics = new MagicSpellSO[MagicCaster.BasicSlotCount];

    // NPC actualmente oculto porque el controller lo está representando
    private NPCPartyMember _hiddenNpc;

    private bool _ready;
    public bool IsReady => _ready;

    // Temporizador de la comprobación periódica de que el NPC de Will sigue al jugador.
    private float _willFollowCheckTimer;

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (magicCaster == null)
            magicCaster = GetComponentInParent<MagicCaster>()
                ?? UnityEngine.Object.FindAnyObjectByType<MagicCaster>();

        CaptureWillSpells();
        _ready = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_willNpcInstance == null) return;

        _willFollowCheckTimer += Time.deltaTime;
        if (_willFollowCheckTimer < 0.5f) return;
        _willFollowCheckTimer = 0f;

        var brain = _willNpcInstance.NPCManager?.Brain;
        if (brain == null) return;
        var contexto = _willNpcInstance.NPCManager.Context;
        if (contexto.IsInCinematic) return;

        // Si hay un enemigo activo y el NPC de Will no está en combate (el Brain no estaba listo
        // al relevar o el enemigo se registró un frame más tarde), entra ahora.
        var activeEnemy = GetActiveCombatEnemy();
        if (activeEnemy != null && !contexto.IsInCombat)
        {
            _willNpcInstance.OnPlayerEnteredCombat(activeEnemy);
            return;
        }

        if (contexto.IsInCombat) return;
        if (!(PartyControlManager.Instance?.IsPartyFollowing ?? true)) return;

        // El NPC de Will no es miembro del grupo, así que la comprobación de NPCPartyMember no le
        // vale: si cayó en Idle sin estar anclado, vuelve a seguir.
        if (brain.CurrentState?.StateName == "Idle" && !contexto.EstaAnclado)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
    }
    #endregion

    #region API pública
    /// <summary>
    /// Ejecuta el cambio de personaje activo.
    /// Llamado por PartyControlManager cuando el jugador pulsa DPad Izquierda/Derecha.
    /// </summary>
    public void SwitchCharacter(PartyControlManager.CharacterSlot from, PartyControlManager.CharacterSlot to)
    {
        if (!_ready || from == to) return;

        var registry = CharacterAppearanceRegistry.Instance;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ActiveCharacterSwapper] SwitchCharacter {from}→{to} | registry={(object)registry ?? "NULL"} | _ready={_ready}");
#endif

        // Capturar posición actual del controller ANTES de teleportar, para anclar NPCs
        PlayerService.TryGetPlayer(out var playerGO);
        Vector3 fromPos = playerGO != null ? playerGO.transform.position : Vector3.zero;
        Quaternion fromRot = playerGO != null ? playerGO.transform.rotation : Quaternion.identity;

        // 1. Guardar estado del personaje que se abandona
        registry?.CaptureCurrentAppearance(from);
        if (from == PartyControlManager.CharacterSlot.Will)
        {
            CaptureWillSpells();
            _willNpcInstance?.SetRuntimeBasics(_willBasics);
        }

        // 2. Teleportar el controller a la posición del NPC objetivo (solo al ir a Liam/Estela)
        var toNpc = GetNpc(to);
        if (to != PartyControlManager.CharacterSlot.Will && toNpc != null)
            TeleportPlayer(toNpc.transform.position, toNpc.transform.rotation);

        // 3. Cambiar apariencia visual
        registry?.ApplyAppearance(to);

        // 4. Cambiar hechizos del player
        ApplySpells(to);

        // 5. Gestionar visibilidad de NPCs de Liam/Estela
        // IMPORTANT: actualizar _hiddenNpc antes de llamar SetNpcVisible para que el guard
        // en OnPlayerEnteredCombat no bloquee al NPC que acaba de ser liberado del control del jugador.
        var prevHidden = _hiddenNpc;
        _hiddenNpc = toNpc;
        // Devolver el NPC previo exactamente a donde estaba el controller (ej: encima de un botón)
        if (prevHidden != null)
            WarpNpcToPosition(prevHidden, fromPos, fromRot);
        SetNpcVisible(prevHidden, true);
        SetNpcVisible(_hiddenNpc, false);

        // 5b. Desvincular compañeros del personaje que se abandona.
        // Un NPC que se unió mientras jugábamos como 'from' (Liam/Estela, no Will) es
        // compañero de ese personaje concreto. Al cambiar de personaje debe quedarse
        // junto al NPC prevHidden (están en la misma posición), no seguir al nuevo.
        // Excepción: si ese NPC es el toNpc (pasa a ser el personaje activo), se mantiene.
        {
            var party = Game.NPC.PlayerParty.Instance;
            if (party != null && from != PartyControlManager.CharacterSlot.Will)
            {
                var toDetach = new System.Collections.Generic.List<Game.NPC.NPCPartyMember>();
                foreach (var member in party.Members)
                {
                    if (member == null || member == toNpc) continue;
                    if (member._joinedForSlot == from) toDetach.Add(member);
                }
                foreach (var member in toDetach)
                    party.RemoveMember(member);
            }
        }

        // 6. NPC de Will: instanciar al alejarse de Will, destruir al volver
        bool willIsActive = to == PartyControlManager.CharacterSlot.Will;
        if (willIsActive)
        {
            // Teleportar el controller a donde está el NPC de Will antes de destruirlo
            if (_willNpcInstance != null)
                TeleportPlayer(_willNpcInstance.transform.position, _willNpcInstance.transform.rotation);
            DestroyWillNpc();
        }
        else if (_willNpcInstance == null)
        {
            // Will spawna exactamente donde estaba el controller (ej: encima de un botón)
            SpawnWillNpc(fromPos, fromRot);
        }
        else
        {
            // Will ya estaba instanciado (cambio Liam↔Estela): colocar a Will donde estaba
            // el personaje que se abandona. En combate activo se respeta la posición actual
            // para no interrumpir la IA de combate. Anclado (p. ej. sobre una placa de presión
            // en modo Libre) o en cinemática no se mueve: el jugador lo dejó ahí a propósito.
            var willContext = _willNpcInstance.NPCManager?.Context;
            bool willPinnedOrInCinematic = (willContext?.EstaAnclado ?? false) || (willContext?.IsInCinematic ?? false);

            if (willPinnedOrInCinematic)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[ActiveCharacterSwapper] Will NPC anclado (modo Libre) o en cinemática — no se reposiciona al cambiar de personaje.");
#endif
            }
            else
            {
                var activeCombatEnemy = GetActiveCombatEnemy();
                if (activeCombatEnemy == null)
                {
                    // Buscar posición válida en NavMesh (radio amplio para cubrir vuelo/natación)
                    Vector3 willPos = fromPos;
                    if (NavMesh.SamplePosition(fromPos, out NavMeshHit willHit, 30f, NavMesh.AllAreas))
                        willPos = willHit.position;
                    WarpNpcToPosition(_willNpcInstance, willPos, fromRot);
                }
                else if (!(_willNpcInstance.NPCManager?.Context?.IsInCombat ?? false))
                {
                    // Hay combate activo pero Will no está participando (perdió el target, etc.)
                    // Re-notificarle para que entre en AllyCombatState.
                    _willNpcInstance.OnPlayerEnteredCombat(activeCombatEnemy);
                }
            }
        }
    }

    /// <summary>
    /// Resetea el estado del swapper, útil al cargar una nueva partida o volver del menú.
    /// </summary>
    public void ResetState()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[ActiveCharacterSwapper] 🔄 Reseteando estado.");
#endif
        DestroyWillNpc();

        // Con recarga de escena el NPC oculto ya está destruido; si la escena se reutiliza, se le
        // devuelve la visibilidad para que no se quede oculto para siempre.
        if (_hiddenNpc != null)
            SetNpcVisible(_hiddenNpc, true);
        _hiddenNpc = null;

        // Asegurarse de que los hechizos de Will se capturen de nuevo si es necesario
        CaptureWillSpells();
        _ready = true; // Asegurarse de que esté listo para operar
    }

    /// <summary>
    /// El NPC oculto porque el jugador lleva a su personaje. Se pregunta a través de
    /// <see cref="ControlDelJugador.RepresentadoPorElJugador"/>.
    /// </summary>
    public NPCPartyMember HiddenNpc => _hiddenNpc;

    /// <summary>
    /// Teleporta el NPC instanciado de Will cerca del jugador.
    /// Llamado por TeleportService tras teleportar al jugador. Con <paramref name="forzar"/> se
    /// omite la comprobación de alcance (viaje rápido, cinemáticas); si no, Will no cruza paredes
    /// ni rejas y, con <paramref name="avisar"/>, muestra el aviso de que no puede llegar.
    /// Ver INC-819.
    /// </summary>
    public void TeleportWillNpcToPlayer(bool forzar = false, bool avisar = false)
    {
        if (_willNpcInstance == null) return;
        // Anclado (modo Libre, barco): se queda donde está.
        if (_willNpcInstance.NPCManager?.Context?.EstaAnclado == true) return;
        if (!PlayerService.TryGetPlayer(out var playerGO)) return;

        // El mismo hueco de formación que los miembros del grupo (el siguiente libre, detrás de
        // ellos): fuera del encuadre de la cámara y ya validado sobre el NavMesh.
        Vector3 behind;
        if (PlayerParty.HasInstance)
        {
            int slotIndex = PlayerParty.Instance.Members.Count;
            behind = PlayerParty.Instance.GetFormationPosition(slotIndex);
        }
        else
        {
            behind = playerGO.transform.position - playerGO.transform.forward * 1.5f;
            if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                behind = hit.position;
        }

        if (!forzar)
        {
            // Mismo criterio que los miembros del party: Will no atraviesa una pared ni una reja
            // para reunirse con el jugador. Ver INC-819.
            if (!PlayerParty.PuedeLlegarHasta(_willNpcInstance.transform, playerGO.transform.position) ||
                !PlayerParty.PuedeLlegarHasta(_willNpcInstance.transform, behind))
            {
                if (avisar) PlayerParty.AvisarNoPuedeLlegar(_willNpcInstance.DisplayName);
                return;
            }
        }

        var agent = _willNpcInstance.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
            agent.Warp(behind);
        else
            _willNpcInstance.transform.position = behind;

        // Reanudar seguimiento si corresponde
        if (PartyControlManager.Instance?.IsPartyFollowing == true)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
    }
    #endregion

    #region Internals
    private void SpawnWillNpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (willNpcPrefab == null) return;

        // En combate activo nace junto al jugador para atacar de inmediato (desde la posición
        // previa del controller podría quedar a más de 30 m y AllyCombatState solo le seguiría).
        if (GetActiveCombatEnemy() != null && PlayerService.TryGetPlayer(out var playerGO))
        {
            Vector3 behind = playerGO.transform.position - playerGO.transform.forward * 1.5f;
            if (NavMesh.SamplePosition(behind, out NavMeshHit combatHit, 3f, NavMesh.AllAreas))
                spawnPos = combatHit.position;
            else
                spawnPos = playerGO.transform.position;
            spawnRot = playerGO.transform.rotation;
        }

        // Posición válida en NavMesh lo más cerca posible de donde estaba el controller.
        Vector3 pos = NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : spawnPos;

        var go = Instantiate(willNpcPrefab, pos, spawnRot);
        var willInteractable = go.GetComponent<Interactable>();
        if (willInteractable != null)
        {
            willInteractable.EnableInteraction(false);
            willInteractable.SetHintVisible(false);
        }
        _willNpcInstance = go.GetComponent<NPCPartyMember>();
        _willNpcInstance?.SetRuntimeBasics(_willBasics);
        _willFollowCheckTimer = 0f;

        // Apariencia actual de Will en el NPC recién instanciado.
        if (CharacterAppearanceRegistry.Instance != null)
        {
            var willAppearance = CharacterAppearanceRegistry.Instance.GetAppearance(PartyControlManager.CharacterSlot.Will);
            var npcBuilder = go.GetComponentInChildren<ModularAutoBuilder>(true);
            if (npcBuilder == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[ActiveCharacterSwapper] willNpcPrefab '{go.name}' sin ModularAutoBuilder — activando partes por nombre como fallback.");
#endif
                ActivateWillPartsByName(go, willAppearance);
            }
            else if (willAppearance != null)
            {
                npcBuilder.DeactivateAllCategories();
                npcBuilder.ApplySelection(willAppearance);
            }
        }

        PrepararParaVerseDondeSea(_willNpcInstance);

        if (_willNpcInstance == null) return;
        StartCoroutine(ReassertVisibilityNextFrames(_willNpcInstance));

        // El modo del grupo se aplica cuando el Brain esté listo. Se captura si había combate al
        // nacer: el registro de combate puede vaciarse mientras se espera.
        bool hadCombatEnemyAtSpawn = GetActiveCombatEnemy() != null;
        StartCoroutine(ApplyFollowModeToWillNpc(hadCombatEnemyAtSpawn));
    }

    /// <summary>
    /// Deja un NPC que aparece o se recoloca en cualquier sitio listo para verse desde el primer
    /// frame. Su posición no estaba en el horneado de Occlusion Culling de MainWorld
    /// (allowOcclusionWhenDynamic apagado), y con el Animator en CullUpdateTransforms y los
    /// SkinnedMeshRenderer sin updateWhenOffscreen sus bounds de culling se quedan sin calcular:
    /// al no verse no se anima, y al no animarse no se ve. AlwaysAnimate, updateWhenOffscreen y
    /// una pasada de Animator con lectura de bounds lo sacan de ese bucle. Son 3-4 NPCs del grupo.
    /// </summary>
    private static void PrepararParaVerseDondeSea(NPCPartyMember npc)
    {
        if (npc == null) return;
        foreach (var rend in npc.GetComponentsInChildren<Renderer>(true))
            rend.allowOcclusionWhenDynamic = false;

        var anim = npc.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Update(0f);
        }
        foreach (var smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.updateWhenOffscreen) smr.updateWhenOffscreen = true;
            if (smr.gameObject.activeInHierarchy) _ = smr.bounds;
        }
    }

    private System.Collections.IEnumerator ApplyFollowModeToWillNpc(bool spawnedDuringCombat = false)
    {
        // Esperar hasta que el Brain esté inicializado (con timeout de seguridad)
        float waited = 0f;
        while (_willNpcInstance != null && _willNpcInstance.NPCManager?.Brain == null)
        {
            waited += Time.deltaTime;
            if (waited > 3f) yield break;
            yield return null;
        }

        if (_willNpcInstance == null) yield break;

        _willFollowCheckTimer = 0f; // Reiniciar timer para no duplicar la primera verificación

        var enemy = GetActiveCombatEnemy();
        if (enemy != null)
            _willNpcInstance.OnPlayerEnteredCombat(enemy);
        else if (spawnedDuringCombat)
            // Había combate al nacer pero el registro se vació mientras se esperaba al Brain:
            // AllyCombatState busca enemigos cercanos o sale por timeout si ya no quedan.
            _willNpcInstance.OnPlayerEnteredCombat(null);
        else
            PartyControlManager.PonerEnModoDelGrupo(_willNpcInstance);
    }

    private void DestroyWillNpc()
    {
        if (_willNpcInstance == null) return;
        Destroy(_willNpcInstance.gameObject);
        _willNpcInstance = null;
    }

    private static NPCPartyMember GetNpc(PartyControlManager.CharacterSlot slot) => ControlDelJugador.MiembroDe(slot);

    private void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        if (!PlayerService.TryGetPlayer(out var playerGO)) return;

        var cc = playerGO.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerGO.transform.SetPositionAndRotation(position, rotation);

        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Si Will no está al mando (lo lleva la IA), guarda sus básicos para cuando vuelva y se los da
    /// al Will de la IA. Devuelve false si Will está al mando: entonces van directos al MagicCaster.
    /// Lo usa PlayerPresetService al cambiar los hechizos desde el menú con otro personaje. INC-498.
    /// </summary>
    public bool TrySetWillBasicsWhileAway(System.Collections.Generic.IReadOnlyList<MagicSpellSO> basics)
    {
        var pcm = PartyControlManager.Instance;
        if (pcm == null || pcm.ActiveSlot == PartyControlManager.CharacterSlot.Will) return false;
        for (int i = 0; i < _willBasics.Length; i++)
            _willBasics[i] = basics != null && i < basics.Count ? basics[i] : null;
        _willNpcInstance?.SetRuntimeBasics(_willBasics);
        return true;
    }

    private void CaptureWillSpells()
    {
        if (magicCaster == null) return;
        var basics = magicCaster.BasicSpells;
        for (int i = 0; i < _willBasics.Length; i++)
            _willBasics[i] = i < basics.Count ? basics[i] : null;
    }

    private void ApplySpells(PartyControlManager.CharacterSlot slot)
    {
        if (magicCaster == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ActiveCharacterSwapper] ApplySpells({slot}): magicCaster es NULL");
#endif
            return;
        }

        if (slot == PartyControlManager.CharacterSlot.Will)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[ActiveCharacterSwapper] ApplySpells(Will): {_willBasics[0]?.displayName}, {_willBasics[1]?.displayName}, {_willBasics[2]?.displayName}, {_willBasics[3]?.displayName}");
#endif
            magicCaster.SetBasicSpells(_willBasics);
            return;
        }

        var npc = GetNpc(slot);
        var personaje = npc != null ? npc.GetComponent<Personaje>() : null;
        var ficha = personaje != null ? personaje.Ficha : null;
        if (ficha != null)
        {
            // Controlado por el jugador, el compañero lleva los básicos de su ficha (hasta 4, rotan con
            // LB igual que los de Will, INC-498); su hechizo especial es de la IA.
            // Los que tenga equipados (menú de pausa) o, si nunca se han tocado, los de su ficha (INC-503).
            magicCaster.SetBasicSpells(GrimorioDelPersonaje.BasicosEquipados(slot));
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ActiveCharacterSwapper] ApplySpells({slot}): NPC={npc?.name ?? "null"} sin ficha de personaje — hechizos no actualizados");
#endif
        }
    }

    /// <summary>
    /// Muestra u oculta el NPC de un personaje al relevar. Es el único sitio que oculta al NPC
    /// del personaje al mando; quien encienda renderers de un compañero pregunta antes
    /// <see cref="ControlDelJugador.RepresentadoPorElJugador"/>.
    /// </summary>
    private void SetNpcVisible(NPCPartyMember npc, bool visible)
    {
        if (npc == null) return;

        ApplyRendererVisibility(npc, visible);

        // Al mostrarlo se reafirma un par de veces en el primer segundo: sistemas que lo tenían
        // apagado a su manera (un diálogo que retira a los que estorban) pueden restaurar su
        // estado justo después del relevo. Ver INC-050.
        if (visible)
            StartCoroutine(ReassertVisibilityNextFrames(npc));

        // Oculto, sin colliders: los proyectiles enemigos alcanzan al CharacterController del
        // jugador en vez de chocar con el NPC.
        foreach (var col in npc.GetComponentsInChildren<Collider>(true))
            col.enabled = visible;

        var it = npc.GetComponent<Interactable>();
        if (it != null && npc.IsInParty)
        {
            it.EnableInteraction(false);
            it.SetHintVisible(false);
        }

        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (visible)
            {
                agent.isStopped = false;
                var enemy = GetActiveCombatEnemy();
                if (enemy != null)
                    npc.OnPlayerEnteredCombat(enemy);
                else
                    // Siguiendo, vuelve a seguir; en Libre se queda anclado donde se le deja
                    // (p. ej. sobre un botón).
                    PartyControlManager.PonerEnModoDelGrupo(npc);
            }
            else
            {
                agent.ResetPath();
                agent.isStopped = true;
                // AllyCombatState reactiva el agente y sigue disparando aunque el NPC esté oculto:
                // sale del combate para que no haya un atacante invisible.
                var npcMgr = npc.NPCManager;
                if (npcMgr != null && npcMgr.Context.IsInCombat)
                {
                    npcMgr.ExitCombat();
                    npcMgr.ForceIdle();
                }
            }
        }
    }

    private static void ApplyRendererVisibility(NPCPartyMember npc, bool visible)
    {
        if (npc == null) return;
        foreach (var r in npc.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
            // Ver PrepararParaVerseDondeSea: se muestra en posiciones fuera del horneado de oclusión.
            r.allowOcclusionWhenDynamic = false;
        }
    }

    private System.Collections.IEnumerator ReassertVisibilityNextFrames(NPCPartyMember npc)
    {
        // Si mientras tanto vuelve a ser el personaje al mando (oculto), manda el estado más reciente.
        yield return null;
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);

        yield return new WaitForSeconds(0.25f);
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);

        yield return new WaitForSeconds(0.5f);
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);
    }

    /// Fallback para cuando el willNpcPrefab no tiene ModularAutoBuilder:
    /// activa los GOs cuyos nombres coincidan con las partes de la apariencia de Will.
    /// Desactiva todos los demás GOs con Renderer para evitar mezclas visuales.
    private void ActivateWillPartsByName(GameObject npcRoot, System.Collections.Generic.Dictionary<PartCategory, string> willAppearance)
    {
        if (willAppearance == null || willAppearance.Count == 0) return;

        var partNames = new System.Collections.Generic.HashSet<string>(willAppearance.Values);
        int activated = 0;

        foreach (var t in npcRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject == npcRoot) continue;
            var hasRenderer = t.GetComponent<Renderer>() != null;
            if (!hasRenderer) continue;

            bool shouldBeActive = partNames.Contains(t.gameObject.name);
            t.gameObject.SetActive(shouldBeActive);
            if (shouldBeActive) activated++;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ActiveCharacterSwapper] ActivateWillPartsByName — {activated}/{partNames.Count} partes activadas en '{npcRoot.name}'.");
#endif
    }

    private void WarpNpcToPosition(NPCPartyMember npc, Vector3 pos, Quaternion rot)
    {
        if (npc == null) return;
        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.Warp(pos);
        else
            npc.transform.position = pos;
        npc.transform.rotation = rot;

        // Un teleport brusco deja los bounds de culling con la posición anterior.
        PrepararParaVerseDondeSea(npc);
    }

    /// <summary>
    /// Devuelve el Transform del primer enemigo activo en combate (excluye compañeros de party).
    /// </summary>
    private Transform GetActiveCombatEnemy()
    {
        foreach (var go in ActiveCombatRegistry.GetAllInCombat())
        {
            if (go == null) continue;
            if (go.GetComponent<NPCPartyMember>() != null) continue;
            return go.transform;
        }
        return null;
    }
    #endregion
}
