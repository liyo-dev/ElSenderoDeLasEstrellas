using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.Modules;
using Sendero.Core.Feedback;
using Liyodev.NarrativeGraph;

/// Pone a uno o varios NPCs en combate contra el jugador (con su FSM de combate) y, si se pide,
/// espera a que caigan todos. Sale por «Gana» al vencerlos. Si «Pierde» está conectada, caer en el
/// combate no lleva a la pantalla de derrota: el grupo se levanta curado tras un fundido a negro,
/// los NPCs vuelven a estar en paz y la historia sigue por «Pierde» (p. ej. para repetir el tramo).
/// Si empieza en un sitio malo para pelear (un puente, un borde, la orilla de un río), tras un
/// fundido lleva al jugador y a los NPCs al terreno abierto más cercano (<see cref="Kits.Core.GroundProbe"/>).
[Serializable]
[NarrativeNodeInfo("Combate", "Iniciar combate NPC", "Pone a uno o varios NPCs en combate contra el jugador y opcionalmente espera a que caigan todos. Con «Pierde» conectada, perder sigue por ahí en vez de ir a la pantalla de derrota.")]
public sealed class PedirCombateContraNpc : NarrativeRequest, INarrativeStateEffect
{
    static readonly string[] Salidas = { "Gana", "Pierde" };

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2 / NarrativeActor).")]
    public string npcId;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Otros NPCs que pelean a la vez (persistenceId). Se gana cuando han caído todos.")]
    public List<string> otrosNpcs = new();

    [Tooltip("Configuración de combate a usar. Vacía = la que ya tenga cada NPC.")]
    public NPCCombatConfig combatConfig;

    [Tooltip("Perfil de combate propio de algún NPC (persistenceId): pisa el de la configuración solo para él, para que dos NPCs con la misma configuración peleen distinto.")]
    public List<PerfilDeCombatePorNpc> perfilesPorNpc = new();

    [Tooltip("Clave del evento que se emite cuando cae cada NPC del combate.")]
    public string defeatEventKey = "NPC_DEFEATED";

    [Tooltip("Espera a que caigan todos los NPCs del combate para seguir.")]
    public bool waitForDefeat = true;

    [Tooltip("Tras la derrota, los NPCs vuelven a la capa Interactable y a estar quietos.")]
    public bool restorePeacefulOnDefeat = true;

    [Tooltip("Si el combate empieza en un sitio malo para pelear (un puente, un borde, agua honda cerca), tras un fundido lleva al jugador y a los NPCs al terreno abierto más cercano.")]
    public bool llevarATerrenoAbierto = true;

    [Min(5f)]
    [Tooltip("Hasta dónde se busca terreno abierto, en metros.")]
    public float distanciaAlTerrenoAbierto = 20f;

    [Min(0f)]
    [Tooltip("Arena del combate: radio en metros alrededor de donde empieza la pelea. Si un NPC del combate se sale, vuelve al borde (como el límite de la sala de un jefe). 0 = sin límite.")]
    public float radioDeArena = 0f;

    public override string[] Exits => Salidas;

    public override string DescribeDecision(NarrativeNode step)
        => $"Combate \"{step?.DisplayTitle}\" (guid {step?.Guid}) contra '{npcId}'{(otrosNpcs.Count > 0 ? " y " + string.Join(", ", otrosNpcs) : "")}: ¿gana el jugador? Salidas: Gana / Pierde.";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "pelea");
        foreach (var id in otrosNpcs) if (!string.IsNullOrWhiteSpace(id)) d.Actor(id, "pelea");
        if (combatConfig != null) d.Asset(combatConfig, "combate");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC del combate.");
        foreach (var id in otrosNpcs)
            if (string.IsNullOrWhiteSpace(id) || id == npcId) warnings.Add("Hay un NPC de más vacío o repetido.");
    }

    public string ClaveDeDerrota => !string.IsNullOrEmpty(defeatEventKey) ? defeatEventKey : $"NPC_DEFEATED_{npcId}";

    public void Project(INarrativeStateWriter state)
    {
        if (waitForDefeat && !string.IsNullOrWhiteSpace(npcId)) state.MarkSignalRaised(ClaveDeDerrota);
    }

    public IEnumerable<string> Ids()
    {
        yield return npcId;
        foreach (var id in otrosNpcs)
            if (!string.IsNullOrWhiteSpace(id) && id != npcId) yield return id;
    }
}

public sealed class CombateContraNpcEnCurso : NarrativeRequestRun<PedirCombateContraNpc>, IRespuestaALaDerrota
{
    const int SalidaGana = 0;
    const int SalidaPierde = 1;
    const float FundidoAlPerder = 0.6f;
    const float FundidoAlColocar = 0.35f;
    // Distancia a la que quedan los NPCs del jugador al llevarlos a terreno abierto.
    const float DistanciaMinimaDeLosNpcs = 3.5f;
    const float DistanciaMaximaDeLosNpcs = 6f;
    static readonly float[] s_girosParaColocar = { 0f, 40f, -40f, 80f, -80f, 130f, -130f, 180f };

    readonly List<NPCBehaviourManagerV2> _npcs = new();
    // Configuración que tenía cada NPC antes de la copia de este combate: se le devuelve al
    // acabar para que la copia no se quede colgada del NPC (ver PrepararParaPelear).
    readonly List<NPCBehaviourManagerV2> _npcConCopia = new();
    readonly List<NPCCombatConfig> _configDeAntes = new();
    INarrativeSignals _signals;
    Action _alCaer;
    bool _cerrado;
    bool _respondeALaDerrota;
    Coroutine _colocando;
    Coroutine _arena;
    Coroutine _cierre;
    LimiteDeArena _limite;
    bool _tapaLaPantalla;

    protected override void Start()
    {
        if (string.IsNullOrWhiteSpace(R.npcId))
        {
            Debug.LogWarning($"[Combate:{StepGuid}] npcId vacío → sigue para no bloquear.");
            Finish(SalidaGana);
            return;
        }

        foreach (var id in R.Ids())
        {
            var m = BuscarNpc(id);
            if (m == null)
            {
                Debug.LogWarning($"[Combate:{StepGuid}] No hay NPCBehaviourManagerV2 con id='{id}'.");
                continue;
            }

            // La historia puede tenerlo retirado («no está»): el combate lo pide, así que vuelve a
            // escena aunque el jugador lo vea. Si aun así no está (una cinemática lo tiene tomado),
            // no entra en la pelea ni cuenta para esperar su derrota: si contara, no acabaría nunca.
            if (!m.gameObject.activeInHierarchy && !TraerAEscena(id, m))
            {
                Debug.LogWarning($"[Combate:{StepGuid}] '{id}' está fuera de escena y no se le puede devolver ahora: no pelea.");
                continue;
            }

            if (!_npcs.Contains(m)) _npcs.Add(m);
        }
        if (_npcs.Count == 0)
        {
            Debug.LogWarning($"[Combate:{StepGuid}] Ningún NPC del combate está en la escena → sigue.");
            Finish(SalidaGana);
            return;
        }

        // El combate no puede empezar detrás de una charla abierta: si un NPC soltó su diálogo
        // de alerta mientras el grafo decía otra cosa, se cierra antes de forzar la pelea (si no,
        // el jugador se queda mirando la caja sin poder moverse ni atacar).
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            DialogueManager.Instance.Close();

        string clave = R.ClaveDeDerrota;
        foreach (var m in _npcs) PrepararParaPelear(m, clave);

        // Mientras dura, ningún otro enemigo se mete en la pelea.
        ActiveCombatRegistry.CerrarCombate(this);
        foreach (var m in _npcs) ActiveCombatRegistry.AnadirAlCombateCerrado(this, m.gameObject);
        _cerrado = true;

        // Cada NPC que cae emite la misma clave.
        if (R.waitForDefeat)
        {
            _signals = Ctx?.Signals ?? DefaultNarrativeSignals.Instance;
            if (_signals == null)
            {
                Debug.LogWarning($"[Combate:{StepGuid}] Sin proveedor de señales para esperar la derrota → sigue.");
                Finish(SalidaGana);
                return;
            }
            // Cada caída emite la misma clave; se gana cuando están vencidos todos, se mire quien
            // la emitió (contar señales daba la victoria si una llegaba repetida). Ver INC-1331.
            _alCaer = () =>
            {
                if (!TodosVencidos() || _cierre != null) return;
                if (Runner != null) _cierre = Runner.StartCoroutine(Co_Ganar());
                else
                {
                    if (R.restorePeacefulOnDefeat) DejarEnPaz();
                    Finish(SalidaGana);
                }
            };
            _signals.OnCustom(clave, _alCaer);
            if (IsExitConnected(SalidaPierde))
            {
                GameOverManager.RegistrarRespuestaALaDerrota(this);
                _respondeALaDerrota = true;
            }
        }

        if (!PlayerService.TryGetPlayer(out GameObject jugador) || jugador == null)
        {
            Debug.LogWarning($"[Combate:{StepGuid}] No se encontró al jugador para empezar el combate con '{R.npcId}'.");
            if (!R.waitForDefeat) Finish(SalidaGana);
            return;
        }

        if (R.llevarATerrenoAbierto && Runner != null && BuscarTerrenoAbierto(jugador.transform.position, out Vector3 sitio))
            _colocando = Runner.StartCoroutine(Co_LlevarAlTerrenoYPelear(jugador, sitio));
        else
            Pelear(jugador);
    }

    void Pelear(GameObject jugador)
    {
        if (jugador != null)
            foreach (var m in _npcs) if (m != null) m.ForceEnterCombat(jugador.transform);
        if (!R.waitForDefeat) Finish(SalidaGana);
        else if (R.radioDeArena > 0f && jugador != null && Runner != null && _arena == null)
            _arena = Runner.StartCoroutine(Co_Arena(jugador.transform.position));
    }

    // Han caído todos: se espera a que terminen de levantarse (mareo y frase) antes de seguir, para
    // que lo siguiente de la historia (una charla, irse andando) no choque con la caída y el NPC no
    // se quede plantado (INC-1406).
    const float EsperaMaximaTrasLaCaida = 6f;
    IEnumerator Co_Ganar()
    {
        float limite = Time.time + EsperaMaximaTrasLaCaida;
        while (Time.time < limite && AlgunoSeLevanta()) yield return null;
        foreach (var m in _npcs)
        {
            var ciclo = m != null ? m.GetComponent<NPCCombatLifecycleHandler>() : null;
            if (ciclo != null && ciclo.ProcesandoLaDerrota) ciclo.CancelDizzySequence();
        }
        if (R.restorePeacefulOnDefeat) DejarEnPaz();
        _cierre = null;
        Finish(SalidaGana);
    }

    bool AlgunoSeLevanta()
    {
        foreach (var m in _npcs)
        {
            var ciclo = m != null ? m.GetComponent<NPCCombatLifecycleHandler>() : null;
            if (ciclo != null && ciclo.ProcesandoLaDerrota) return true;
        }
        return false;
    }

    // La arena: quien pelea no puede alejarse más de 'radioDeArena' de donde empezó la pelea. Si se
    // sale (un NPC que huye, que rodea, que se pierde), vuelve al borde, por el NavMesh.
    IEnumerator Co_Arena(Vector3 centro)
    {
        float radio = R.radioDeArena;
        var espera = new WaitForSeconds(0.1f);
        // El borde se ve en el suelo, como en las arenas de los jefes.
        _limite = LimiteDeArena.Crear(null, centro, radio);
        while (true)
        {
            // El jugador tampoco sale: si cruza el borde, vuelve a él. Así los NPCs no se quedan
            // pegados al borde persiguiéndole por fuera.
            if (PlayerService.TryGetPlayer(out GameObject jugador) && jugador != null)
            {
                Vector3 dj = jugador.transform.position - centro;
                dj.y = 0f;
                if (dj.sqrMagnitude > radio * radio)
                {
                    Vector3 dentro = centro + dj.normalized * (radio - 0.5f);
                    dentro.y = jugador.transform.position.y;
                    if (NavMesh.SamplePosition(dentro, out NavMeshHit hj, 3f, NavMesh.AllAreas)) dentro = hj.position;
                    TeleportService.TeleportToPosition(jugador, dentro, jugador.transform.rotation, null);
                }
            }
            foreach (var m in _npcs)
            {
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                var ciclo = m.GetComponent<NPCCombatLifecycleHandler>();
                if (ciclo != null && ciclo.IsDefeatedAndInactive) continue;
                Vector3 d = m.transform.position - centro;
                d.y = 0f;
                if (d.sqrMagnitude <= radio * radio) continue;
                Vector3 borde = centro + d.normalized * Mathf.Max(0f, radio - 1f);
                borde.y = m.transform.position.y;
                if (!NavMesh.SamplePosition(borde, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                var agente = m.Agent;
                if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(hit.position);
                else m.transform.position = hit.position;
            }
            yield return espera;
        }
    }

    // El sitio donde está el jugador no vale para pelear (ver GroundProbe.IsOpenGround) y hay
    // terreno abierto a mano. Ver INC-1331.
    bool BuscarTerrenoAbierto(Vector3 desde, out Vector3 sitio)
    {
        sitio = desde;
        var ajustes = Kits.Core.OpenGroundSettings.Default(LayerMask.GetMask("Water"), R.distanciaAlTerrenoAbierto);
        if (Kits.Core.GroundProbe.IsOpenGround(desde, ajustes)) return false;
        if (Kits.Core.GroundProbe.TryFindOpenGround(desde, ajustes, out sitio)) return true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[Combate:{StepGuid}] El combate empieza en un sitio estrecho y no hay terreno abierto a menos de {R.distanciaAlTerrenoAbierto:0} m: se pelea ahí.");
#endif
        return false;
    }

    // Fundido, el jugador al terreno abierto mirando a los NPCs, cada NPC a unos metros por el lado
    // por el que estaba, y vuelta; entonces empieza la pelea.
    IEnumerator Co_LlevarAlTerrenoYPelear(GameObject jugador, Vector3 sitio)
    {
        _tapaLaPantalla = !FeedbackService.IsScreenFaded;
        if (_tapaLaPantalla)
            yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlColocar, fadeIn: true);

        if (jugador != null)
        {
            Vector3 desde = jugador.transform.position;
            Vector3 centro = Vector3.zero;
            int cuantos = 0;
            foreach (var m in _npcs)
            {
                if (m == null) continue;
                Vector3 rumbo = m.transform.position - desde;
                rumbo.y = 0f;
                float lejos = Mathf.Clamp(rumbo.magnitude, DistanciaMinimaDeLosNpcs, DistanciaMaximaDeLosNpcs);
                if (rumbo.sqrMagnitude < 0.01f) rumbo = Quaternion.Euler(0f, 360f * cuantos / Mathf.Max(1, _npcs.Count), 0f) * Vector3.forward;
                Vector3 donde = SitioParaElNpc(sitio, rumbo.normalized, lejos);
                Colocar(m, donde, sitio);
                centro += donde;
                cuantos++;
            }
            Vector3 mirar = cuantos > 0 ? centro / cuantos - sitio : jugador.transform.forward;
            mirar.y = 0f;
            Quaternion giro = mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar, Vector3.up) : jugador.transform.rotation;
            TeleportService.TeleportToPosition(jugador, sitio, giro, null);
        }
        yield return null;

        if (_tapaLaPantalla)
        {
            _tapaLaPantalla = false;
            yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlColocar, fadeIn: false);
        }
        _colocando = null;
        Pelear(jugador);
    }

    // Un punto del NavMesh a 'lejos' metros de 'sitio' por 'rumbo' (o girando poco a poco si por ahí
    // hay un borde), al que se llega en línea recta desde el sitio.
    static Vector3 SitioParaElNpc(Vector3 sitio, Vector3 rumbo, float lejos)
    {
        foreach (float g in s_girosParaColocar)
        {
            Vector3 prueba = sitio + Quaternion.Euler(0f, g, 0f) * rumbo * lejos;
            if (!NavMesh.SamplePosition(prueba, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if (NavMesh.Raycast(sitio, hit.position, out _, NavMesh.AllAreas)) continue;
            return hit.position;
        }
        return sitio + rumbo * DistanciaMinimaDeLosNpcs;
    }

    static void Colocar(NPCBehaviourManagerV2 npc, Vector3 donde, Vector3 mirandoA)
    {
        Vector3 mirar = mirandoA - donde;
        mirar.y = 0f;
        Quaternion giro = mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar, Vector3.up) : npc.transform.rotation;
        var agente = npc.Agent;
        if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(donde);
        npc.transform.SetPositionAndRotation(donde, giro);
        npc.SimpleAnimator?.SyncTargetRotation();
    }

    protected override void Stop()
    {
        if (_cerrado)
        {
            ActiveCombatRegistry.AbrirCombate(this);
            _cerrado = false;
        }
        if (_colocando != null && Runner != null) Runner.StopCoroutine(_colocando);
        _colocando = null;
        if (_arena != null && Runner != null) Runner.StopCoroutine(_arena);
        _arena = null;
        if (_cierre != null && Runner != null) Runner.StopCoroutine(_cierre);
        _cierre = null;
        if (_limite != null) _limite.Retirar();
        _limite = null;
        if (_tapaLaPantalla)
        {
            _tapaLaPantalla = false;
            FeedbackService.ScreenFade(Color.black, FundidoAlColocar, fadeIn: false);
        }
        if (_respondeALaDerrota)
        {
            GameOverManager.QuitarRespuestaALaDerrota(this);
            _respondeALaDerrota = false;
        }
        if (_signals != null && _alCaer != null) _signals.OffCustom(R.ClaveDeDerrota, _alCaer);
        _alCaer = null;
        _signals = null;
        SoltarCopiasDeConfiguracion();
    }

    // Vencidos todos los NPCs del combate (uno que ya no existe cuenta como vencido).
    bool TodosVencidos()
    {
        foreach (var m in _npcs)
        {
            if (m == null) continue;
            var ciclo = m.GetComponent<NPCCombatLifecycleHandler>();
            if (ciclo == null || !ciclo.IsDefeatedAndInactive) return false;
        }
        return true;
    }

    // Devuelve a cada NPC la configuración que tenía y suelta las copias hechas para este combate.
    void SoltarCopiasDeConfiguracion()
    {
        for (int i = 0; i < _npcConCopia.Count; ++i)
        {
            var npc = _npcConCopia[i];
            var antes = _configDeAntes[i];
            if (npc != null && npc.Configuration != null)
            {
                var copia = npc.Configuration.combatConfig;
                npc.Configuration.combatConfig = antes;
                if (copia != null && copia != antes) UnityEngine.Object.Destroy(copia);
            }
        }
        _npcConCopia.Clear();
        _configDeAntes.Clear();
    }

    /// El grupo ha caído en este combate y «Pierde» está conectada.
    public bool Responder()
    {
        if (Runner == null) return false;
        Runner.StartCoroutine(Co_AlPerder(Runner));
        return true;
    }

    IEnumerator Co_AlPerder(NarrativeRunner runner)
    {
        Stop();
        yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: true);

        GameOverManager.Instance?.ForceResetState();
        if (PlayerService.TryGetComponent(out PlayerActionManager modos, allowSceneLookup: false)) modos.ResetToDefault();
        SaludDelGrupo.Instance?.CurarATodos();

        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        foreach (var m in _npcs)
        {
            if (m == null) continue;
            var dmg = m.GetComponent<Damageable>();
            var config = m.Configuration?.combatConfig;
            float hp = config != null ? config.health : 100f;
            if (dmg != null) dmg.SetMaxAndCurrent(hp, hp);
            var ciclo = m.GetComponent<NPCCombatLifecycleHandler>();
            if (ciclo != null && ciclo.IsDefeatedAndInactive) ciclo.Resurrect();
            if (capaInteractuable >= 0) m.gameObject.layer = capaInteractuable;
            m.ForceIdle();
        }

        Finish(SalidaPierde);
        yield return null;
        // Si el paso siguiente ya ha destapado o vuelto a tapar la pantalla, manda el suyo.
        if (FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(Color.black, FundidoAlPerder, fadeIn: false);
    }

    Game.NPC.Combate.PerfilDeCombate PerfilPara(string id)
    {
        if (string.IsNullOrEmpty(id) || R.perfilesPorNpc == null) return null;
        for (int i = 0; i < R.perfilesPorNpc.Count; i++)
        {
            var p = R.perfilesPorNpc[i];
            if (p != null && p.perfil != null && p.npcId == id) return p.perfil;
        }
        return null;
    }

    // Configuración de combate, vida, ciclo de caída y capa Enemy de un NPC del combate.
    void PrepararParaPelear(NPCBehaviourManagerV2 npc, string clave)
    {
        // Copia propia de la configuración: el asset es compartido (los contrabandistas usan el
        // del pirata) y no puede quedarse con la clave de derrota de este combate ni soltar la
        // charla de alerta del pirata encima de la conversación que ya lleva el grafo.
        var configuracion = npc.Configuration;
        var original = R.combatConfig != null ? R.combatConfig : configuracion?.combatConfig;
        if (original != null && configuracion != null)
        {
            var configDelCombate = UnityEngine.Object.Instantiate(original);
            configDelCombate.dialogueOnAlert = null;
            configDelCombate.dialogueOnAlertRepeat = null;
            configDelCombate.waitForAlertDialogue = false;
            configDelCombate.freezePlayerOnAlert = false;
            // Lo que se dice al caer lo pone la historia (su charla tras «Gana»): sin las frases de
            // mareo del pirata, que se encadenaban con la de la historia.
            configDelCombate.dialogueOnDizzy = null;
            configDelCombate.dialogueOnDizzyRepeat = null;
            configDelCombate.dialogueOnDefeat = null;
            var perfilPropio = PerfilPara(npc.PersistenceId);
            if (perfilPropio != null) configDelCombate.perfilDeCombate = perfilPropio;
            _npcConCopia.Add(npc);
            _configDeAntes.Add(configuracion.combatConfig);
            configuracion.combatConfig = configDelCombate;
        }

        var config = npc.Configuration?.combatConfig;
        if (config != null)
        {
            config.sendEventOnDefeat = true;
            config.defeatEventKey = clave;
            config.sendDefeatEventBeforeDeath = false;
            config.postDeathBehavior = PostDeathBehavior.GetUpDizzy;
        }

        var dmg = npc.GetComponent<Damageable>();
        float hp = config != null ? config.health : 100f;
        if (dmg == null) dmg = npc.gameObject.AddComponent<Damageable>();
        dmg.SetMaxAndCurrent(hp, hp);
        dmg.SetDestroyOnDeath(false);

        // En una revancha, el NPC ya derrotado se levanta para poder volver a pelear: ForceEnterCombat
        // ignora a un NPC derrotado.
        var ciclo = npc.GetComponent<NPCCombatLifecycleHandler>();
        if (!ciclo) npc.gameObject.AddComponent<NPCCombatLifecycleHandler>();
        else if (ciclo.IsDefeatedAndInactive) ciclo.Resurrect();

        int capaEnemigo = LayerMask.NameToLayer("Enemy");
        if (capaEnemigo >= 0) npc.gameObject.layer = capaEnemigo;
    }

    void DejarEnPaz()
    {
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        foreach (var m in _npcs)
        {
            if (m == null) continue;
            if (capaInteractuable >= 0) m.gameObject.layer = capaInteractuable;
            m.ForceIdle();
        }
    }

    // Devuelve a escena al NPC si la historia lo tenía retirado («no está») o apagado: el combate
    // empieza ya y se le puede ver aparecer. Sin la capa de la historia (pruebas), se enciende tal cual.
    internal static bool TraerAEscena(string id, NPCBehaviourManagerV2 npc)
    {
        if (NarrativeServices.TryGet(out INarrativeActorPresence presencia, nameof(CombateContraNpcEnCurso)))
        {
            if (presencia.BringOnStage(id)) return true;
            // Una cinemática lo tiene tomado: no se le quita. Si no está en el registro (el NPC
            // llegó aquí por la búsqueda de escena), se enciende directamente.
            if (RepartoDeEscena.EstaTomado(id)) return false;
        }
        if (!npc.gameObject.activeSelf) npc.gameObject.SetActive(true);
        return npc.gameObject.activeInHierarchy;
    }

    internal static NPCBehaviourManagerV2 BuscarNpc(string id)
    {
        if (NPCRegistry.HasInstance)
        {
            var m = NPCRegistry.Instance.GetNPCByID(id);
            if (m != null) return m;
        }
        if (SequenceActor.TryResolve(id, out var actor) && actor?.Transform != null)
        {
            var m = actor.Transform.GetComponent<NPCBehaviourManagerV2>();
            if (m != null) return m;
        }
        foreach (var m in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include))
            if (m.PersistenceId == id) return m;
        return null;
    }
}

/// Perfil de combate de un NPC concreto dentro de un «Iniciar combate NPC».
[Serializable]
public sealed class PerfilDeCombatePorNpc
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC.")]
    public string npcId;

    [Tooltip("Perfil con el que pelea ese NPC.")]
    public Game.NPC.Combate.PerfilDeCombate perfil;
}
