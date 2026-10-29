using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bus de señales del grafo narrativo (ver INarrativeSignals). Las señales sin oyente se guardan
/// hasta que alguien las espere («sticky»). Lo de misiones lo delega en el <see cref="IQuestService"/>
/// que el juego registre en <see cref="NarrativeServices"/>.
/// </summary>
public class DefaultNarrativeSignals : MonoBehaviour, INarrativeSignals
{
    public static DefaultNarrativeSignals Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnAfterReset = null;
    }
    #endif

    /// <summary>La instancia activa; si no hay ninguna, crea una persistente.</summary>
    public static DefaultNarrativeSignals EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
        }
        else
        {
            var go = new GameObject("DefaultNarrativeSignals (Auto)");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DefaultNarrativeSignals>();
        }
        return Instance;
    }

    // Suscriptores por clave
    readonly Dictionary<string, Action> _custom = new();
    // Eventos que llegaron antes de que hubiera oyentes (se consumen al suscribirse)
    readonly HashSet<string> _pending = new();
    // Registro persistente: eventos sin oyentes que sobreviven a ResetState(preservePending=true).
    // Garantiza que una señal disparada antes de que el grafo se suscriba no se pierda nunca.
    // Solo se limpia en reset completo (nueva partida).
    readonly HashSet<string> _raised = new();

    // Registro durable de «¿esta clave se ha disparado alguna vez en esta partida?», a diferencia
    // de _pending/_raised (que se vacían al consumirlos el primer suscriptor). Sirve a quien
    // pregunta por un hecho pasado sin haber estado suscrito en el instante del disparo. No
    // participa en la entrega sticky de OnCustom. Sobrevive a ResetState(preservePending:true)
    // igual que _raised; se limpia solo en el reset completo (nueva partida).
    readonly HashSet<string> _everRaised = new();

    /// <summary>
    /// Se dispara justo después de cualquier llamada a ResetState(). Permite que otros sistemas
    /// vuelvan a suscribirse tras el borrado de suscriptores que hace el reset.
    /// </summary>
    public static event Action OnAfterReset;

    // ── Observabilidad (Editor + Development builds) ─────────────
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    public enum SignalStatus { Fired, Queued, Consumed, Reset }

    public readonly struct SignalRecord
    {
        public readonly float  time;
        public readonly string key;
        public readonly SignalStatus status;
        public readonly string detail;
        public readonly string caller;
        public SignalRecord(string key, SignalStatus status, string detail, string caller)
        {
            this.time   = UnityEngine.Time.time;
            this.key    = key;
            this.status = status;
            this.detail = detail;
            this.caller = caller;
        }
    }

    public static readonly System.Collections.Generic.List<SignalRecord> History = new();
    private const int MaxHistory = 400;
    public static event Action HistoryChanged;

    // skipFrames=2 salta Record() + el método que llama a Record() (RaiseCustom/OnCustom/ResetState)
    // y deja en frame 0 el código de juego que realmente disparó el evento.
    static void Record(string key, SignalStatus status, string detail)
    {
        string caller = CaptureGameCaller(skipFrames: 2);
        if (History.Count >= MaxHistory) History.RemoveAt(0);
        History.Add(new SignalRecord(key, status, detail, caller));
        try { HistoryChanged?.Invoke(); } catch { }
    }

    // Variante que antepone el nombre del GO/contexto al caller capturado por stack trace.
    static void RecordWithContext(string key, SignalStatus status, string detail, string context)
    {
        string caller = CaptureGameCaller(skipFrames: 3); // +1 frame extra por la indirección RaiseCustom(key,ctx)→RecordWithContext
        if (!string.IsNullOrEmpty(context))
            caller = string.IsNullOrEmpty(caller) ? $"[{context}]" : $"[{context}]  {caller}";
        if (History.Count >= MaxHistory) History.RemoveAt(0);
        History.Add(new SignalRecord(key, status, detail, caller));
        try { HistoryChanged?.Invoke(); } catch { }
    }

    static string CaptureGameCaller(int skipFrames)
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(skipFrames, fNeedFileInfo: true);
            for (int i = 0; i < System.Math.Min(st.FrameCount, 12); i++)
            {
                var frame  = st.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null) continue;
                string typeName = method.DeclaringType?.Name ?? "";
                // Saltar frames internos de Unity/Mono/.NET y del propio DefaultNarrativeSignals
                if (typeName.StartsWith("UnityEngine")
                 || typeName.StartsWith("UnityEditor")
                 || typeName.StartsWith("System")
                 || typeName == nameof(DefaultNarrativeSignals))
                    continue;
                string file  = frame.GetFileName() ?? "";
                int    line  = frame.GetFileLineNumber();
                string fShort = file.Length > 0
                    ? System.IO.Path.GetFileName(file)
                    : "";
                return string.IsNullOrEmpty(fShort)
                    ? $"{typeName}.{method.Name}()"
                    : $"{typeName}.{method.Name}()  [{fShort}:{line}]";
            }
        }
        catch { }
        return "";
    }

    public static void ClearHistory() { History.Clear(); try { HistoryChanged?.Invoke(); } catch { } }

    public System.Collections.Generic.IReadOnlyDictionary<string, Action> CurrentSubscribers => _custom;
    public IReadOnlyCollection<string> CurrentPending => _pending;
    public IReadOnlyCollection<string> CurrentRaised  => _raised;
#endif

    // ====== BATTLE subscribers (por arena key) ======
    readonly Dictionary<object, Action> _battleSubscribers = new();
    readonly HashSet<object> _battlePending = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[DefaultNarrativeSignals] Instancia duplicada detectada. Se usará la primera creada.");
#endif
            return;
        }
        Instance = this;
    }

    public void UnraiseCustom(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _raised.Remove(key);
        _pending.Remove(key);
    }

    /// <summary>
    /// True si hay algún oyente suscrito AHORA MISMO a esta clave (vía OnCustom).
    /// Pensado para eventos "en vivo" ligados a un estado físico/transitorio (p.ej. el
    /// jugador cruzando un trigger de zona), donde NO queremos el comportamiento sticky
    /// de RaiseCustom (que banca el evento en _pending/_raised para cuando aparezca un
    /// oyente). Si el emisor solo debe contar como válido mientras alguien lo está
    /// esperando de verdad, debe comprobar esto antes de llamar a RaiseCustom.
    /// </summary>
    public bool HasCustomListener(string key)
        => !string.IsNullOrWhiteSpace(key) && _custom.ContainsKey(key);

    /// <summary>
    /// True si esta clave se ha disparado (RaiseCustom) alguna vez en la partida actual, con
    /// independencia de si tuvo oyentes en el momento o de si ya fue consumida por _pending/_raised.
    /// Fuente de verdad durable para quien necesite preguntar «¿ya pasó esto?» sin mantener su
    /// propio flag.
    /// </summary>
    public bool HasEverRaised(string key)
        => !string.IsNullOrWhiteSpace(key) && _everRaised.Contains(key);

    /// Señales emitidas alguna vez en esta partida, para guardarlas con ella.
    public IReadOnlyCollection<string> EverRaisedKeys => _everRaised;

    /// Recupera el «ya pasó» de una partida guardada. Solo alimenta <see cref="HasEverRaised"/>:
    /// no deja nada pendiente ni despierta a ningún suscriptor.
    public void RestoreEverRaised(IEnumerable<string> keys)
    {
        if (keys == null) return;
        foreach (var k in keys)
            if (!string.IsNullOrWhiteSpace(k)) _everRaised.Add(k);
    }

    /// <summary>
    /// True si esta key se ha disparado y está esperando a su primer oyente (en _pending o
    /// _raised). Solo consulta: no la consume, a diferencia de OnCustom.
    /// </summary>
    public bool IsPendingDelivery(string key)
        => !string.IsNullOrWhiteSpace(key) && (_pending.Contains(key) || _raised.Contains(key));

    public void ResetState()
    {
        ResetState(preservePending: false);
    }

    /// Olvida las señales de la partida anterior (emitidas, pendientes y batallas) sin tocar a los
    /// suscriptores: las secuencias y nodos que ya escuchan siguen escuchando. Es lo que necesita
    /// «Nueva partida» sin cerrar el juego, para que la partida nueva no consuma señales de la
    /// vieja. Ver INC-448.
    public void OlvidarSenalesDePartida()
    {
        _pending.Clear();
        _battlePending.Clear();
        _raised.Clear();
        _everRaised.Clear();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Record("__RESET__", SignalStatus.Reset, "Nueva partida: señales olvidadas, suscriptores intactos");
#endif
        try { OnAfterReset?.Invoke(); }
        catch (Exception e) { Debug.LogError($"[Signals] Error en OnAfterReset: {e}"); }
    }

    /// <summary>
    /// Resetea el estado de señales.
    /// Si preservePending es true, mantiene los eventos pendientes (útil al cargar partida).
    /// </summary>
    public void ResetState(bool preservePending)
    {
        _custom.Clear();
        if (!preservePending)
        {
            _pending.Clear();
            _battlePending.Clear();
            _raised.Clear();
            _everRaised.Clear();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Record("__RESET__", SignalStatus.Reset, "ResetState completo — _pending, _raised y _custom limpiados");
#endif
        }
        else if (_pending.Count > 0 || _battlePending.Count > 0 || _raised.Count > 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] ResetState preservando {_pending.Count} pendientes, {_raised.Count} persistentes, {_battlePending.Count} batallas");
#endif
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Record("__RESET__", SignalStatus.Reset, $"ResetState suave — preservando {_pending.Count} pending, {_raised.Count} raised");
#endif
        }
        _battleSubscribers.Clear();

        try { OnAfterReset?.Invoke(); }
        catch (Exception e) { Debug.LogError($"[Signals] Error en OnAfterReset: {e}"); }
    }

    IQuestService QS => NarrativeServices.TryGet(out IQuestService qs, nameof(DefaultNarrativeSignals)) ? qs : null;

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    // ===================== QUEST =====================
    public void OfferQuest(string questId, object npcContext)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Signals] OfferQuest {questId} (svc={(QS!=null?QS.GetType().Name:"NULL")})");
#endif
        QS?.Offer(questId, npcContext);
    }

    public bool IsQuestCompleted(string questId) => QS != null && QS.IsCompleted(questId);
    public void OnQuestCompleted(string questId, Action cb) => QS?.OnCompleted(questId, cb);
    public void OffQuestCompleted(string questId, Action cb) => QS?.OffCompleted(questId, cb);

    public void StartQuest(string questId, object npcContext)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Signals] StartQuest {questId} (svc={(QS!=null?QS.GetType().Name:"NULL")})");
#endif
        QS?.StartQuest(questId);
    }

    public void CompleteQuest(string questId) => QS?.Complete(questId);

    public void CompleteQuestStep(string questId, int stepIndex)
    {
        if (string.IsNullOrEmpty(questId)) return;
        QS?.CompleteStep(questId, stepIndex);
    }

    public void CompleteQuestStepByConditionId(string questId, string stepConditionId)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(stepConditionId)) return;
        QS?.CompleteStepByConditionId(questId, stepConditionId);
    }

    /// <summary>Estado detallado de la quest para los nodos de bifurcación (BranchQuestStateNode).</summary>
    public NarrativeQuestState GetQuestState(string questId)
        => QS != null ? QS.GetState(questId) : NarrativeQuestState.NotStarted;

    /// <summary>
    /// Descarta una señal pendiente sin consumirla (ver INarrativeSignals.ClearPendingCustom).
    /// No toca _everRaised: "ya pasó alguna vez" sigue siendo verdad.
    /// </summary>
    public void ClearPendingCustom(string key) => UnraiseCustom(key);

    // ============= CUSTOM (sticky) =============
    public void RaiseCustom(string key) => RaiseCustom(key, null);

    public void RaiseCustom(string key, string context)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        _everRaised.Add(key);

        if (_custom.TryGetValue(key, out var a) && a != null)
        {
            int listenerCount = a.GetInvocationList().Length;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] Custom: {key}" + (string.IsNullOrEmpty(context) ? "" : $" ({context})"));
            RecordWithContext(key, SignalStatus.Fired, $"{listenerCount} oyente(s)", context);
#endif
            try { a.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }
        else
        {
            _pending.Add(key);
            _raised.Add(key);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] Custom: {key} (sin oyentes → pendiente)" + (string.IsNullOrEmpty(context) ? "" : $" ({context})"));
            RecordWithContext(key, SignalStatus.Queued, "sin oyentes, guardado en _pending y _raised", context);
#endif
        }
    }

    public void OnCustom(string key, Action cb)
    {
        if (string.IsNullOrWhiteSpace(key) || cb == null) return;

        // Consumir desde _pending (sesión actual) o _raised (persistente a través de resets suaves).
        // Ambos se limpian juntos: si el evento estaba en cualquiera de los dos, lo disparamos ya.
        bool wasPending = _pending.Remove(key);
        bool wasRaised  = _raised.Remove(key);

        if (wasPending || wasRaised)
        {
            string src = wasRaised && !wasPending ? "_raised" : "_pending";
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] Custom: {key} (consumido desde {(wasRaised && !wasPending ? "registro persistente" : "pendientes")})");
            Record(key, SignalStatus.Consumed, $"consumido desde {src} al suscribirse");
#endif
            try { cb(); } catch (Exception e) { Debug.LogException(e); }
            return;
        }

        if (_custom.TryGetValue(key, out var a)) _custom[key] = a + cb;
        else _custom[key] = cb;
    }

    /// <summary>
    /// Devuelve una señal a _pending/_raised sin invocar suscriptores activos. Diferente de
    /// RaiseCustom, que despacha de inmediato si hay oyentes. Útil cuando un suscriptor consumió
    /// la señal desde OnCustom() pero decide que no era para él: sin devolverla se perdería para
    /// cualquier suscriptor posterior. Es seguro llamarlo desde dentro del propio callback que
    /// consumió la señal porque no invoca nada.
    /// </summary>
    public void RequeueCustom(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        _pending.Add(key);
        _raised.Add(key);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Record(key, SignalStatus.Queued, "reencolada sin invocar (consumidor la descartó, ver RequeueCustom)");
#endif
    }

    public void OffCustom(string key, Action cb)
    {
        if (string.IsNullOrWhiteSpace(key) || cb == null) return;
        if (_custom.TryGetValue(key, out var a))
        {
            a -= cb;
            if (a == null) _custom.Remove(key);
            else _custom[key] = a;
        }
    }

    // ====== BATTLE (implementación) ======
    public void OnBattleWon(object arena, Action cb)
    {
        if (cb == null) return;
        var key = arena ?? "__NULL__";

        // Si hubo un RaiseBattleWon antes de suscribirse, consumimos inmediatamente
        if (_battlePending.Remove(key))
        {
            try { cb(); } catch (Exception e) { Debug.LogException(e); }
            return;
        }

        if (_battleSubscribers.TryGetValue(key, out var a)) _battleSubscribers[key] = a + cb;
        else _battleSubscribers[key] = cb;
    }

    public void OffBattleWon(object arena, Action cb)
    {
        if (cb == null) return;
        var key = arena ?? "__NULL__";
        if (_battleSubscribers.TryGetValue(key, out var a))
        {
            a -= cb;
            if (a == null) _battleSubscribers.Remove(key);
            else _battleSubscribers[key] = a;
        }
    }

    // Llamar esto cuando una arena se considere ganada
    public void RaiseBattleWon(object arena)
    {
        var key = arena ?? "__NULL__";
        
        // Los suscriptores globales reciben la victoria antes que los específicos de arena.
        if (_battleSubscribers.TryGetValue("__GLOBAL__", out var globalAction) && globalAction != null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] BattleWon GLOBAL disparado para arena: {key}");
#endif
            try { globalAction.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        // Luego disparar a suscriptores específicos de esta arena
        if (_battleSubscribers.TryGetValue(key, out var a) && a != null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] BattleWon: {key}");
#endif
            try { a.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }
        else
        {
            _battlePending.Add(key);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Signals] BattleWon: {key} (sin oyentes específicos → pendiente)");
#endif
        }
    }
}
