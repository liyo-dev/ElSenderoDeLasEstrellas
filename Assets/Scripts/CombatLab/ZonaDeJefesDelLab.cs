using System.Collections;
using UnityEngine;

/// Zona de jefes del CombatLab: una entrada con un portal por jefe y una arena grande con su
/// círculo. Al tocar un portal, el jugador pasa al centro de la arena y empieza el encuentro con
/// la arena del juego (BossArenaController en modo radio), con su música, su guía y su informe.
/// Al ganar vuelve a la entrada con la vida llena (paso de cierre de batalla); al caer, el
/// laboratorio se recarga y empieza en la entrada (VueltaAlLab).
public sealed class ZonaDeJefesDelLab : MonoBehaviour, IPasoDeCierre
{
    /// Prefijo de las batallas del laboratorio: cada intento lleva un id nuevo, así ganar no deja
    /// al jefe marcado como vencido y se puede repetir.
    public const string PrefijoDeBatalla = "Lab_";
    private const float EsperaDelTeletransporte = 4f;

    [Tooltip("Dónde aparece el jugador al llegar a la zona y al volver de una pelea.")]
    [SerializeField] private Transform entrada;
    [Tooltip("Centro de la arena: ahí empieza la pelea.")]
    [SerializeField] private Transform centroDeArena;

    private static ZonaDeJefesDelLab s_actual;
    private static bool s_empezarEnLaZona;
    private static int s_intentos;
    private bool _enCurso;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_actual = null;
        s_empezarEnLaZona = false;
        s_intentos = 0;
    }
#endif

    /// Hay una pelea de la zona en marcha.
    public static bool HayCombateEnCurso => s_actual != null && s_actual._enCurso;

    /// La próxima vez que cargue el laboratorio, el jugador aparece en la entrada de la zona.
    public static void EmpezarEnLaZonaAlCargar() => s_empezarEnLaZona = true;

    public int Orden => 900;   // después del informe

    private void OnEnable()
    {
        s_actual = this;
        CierreDeBatalla.Registrar(this);
    }

    private void OnDisable()
    {
        CierreDeBatalla.Quitar(this);
        if (s_actual == this) s_actual = null;
    }

    private IEnumerator Start()
    {
        if (!s_empezarEnLaZona) yield break;
        s_empezarEnLaZona = false;
        yield return null;   // que el arranque del laboratorio coloque antes al jugador
        yield return null;
        var jugador = PlayerService.Player;
        if (jugador != null && entrada != null) Llevar(jugador, entrada, transicion: false);
    }

    public void Empezar(BattleEncounterSO encuentro, string idDeBatalla)
    {
        if (_enCurso || encuentro == null || centroDeArena == null) return;
        StartCoroutine(Co_Empezar(encuentro, idDeBatalla));
    }

    private IEnumerator Co_Empezar(BattleEncounterSO encuentro, string idDeBatalla)
    {
        _enCurso = true;
        var jugador = PlayerService.Player;
        if (jugador == null) { _enCurso = false; yield break; }
        yield return Co_Llevar(jugador, centroDeArena);

        string id = $"{PrefijoDeBatalla}{idDeBatalla}_{++s_intentos}";
        var arena = BossArenaController.CreateRuntimeArena(id, encuentro);
        if (arena == null) { _enCurso = false; yield break; }
        arena.TriggerStartBattle(encuentro);
    }

    // ── Al ganar: vuelta a la entrada ─────────────────────────────────────

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        if (!_enCurso || resultado == null || resultado.BattleId == null || !resultado.BattleId.StartsWith(PrefijoDeBatalla))
            yield break;
        var jugador = PlayerService.Player;
        if (jugador != null)
        {
            var salud = jugador.GetComponent<PlayerHealthSystem>();
            if (salud != null) salud.Revive(1f);
            if (entrada != null) yield return Co_Llevar(jugador, entrada);
        }
        _enCurso = false;
    }

    public void Terminar(ResultadoDeBatalla resultado) { }

    // ── Teletransporte ────────────────────────────────────────────────────

    /// Lleva al jugador y al grupo con el teletransporte del juego (fundido y compañeros).
    public static void Llevar(GameObject jugador, Transform destino, bool transicion = true)
    {
        if (jugador == null || destino == null) return;
        if (TeleportService.Inst != null) TeleportService.Inst.DoTeleportToAnchor(jugador, destino, transicion);
        else jugador.transform.SetPositionAndRotation(destino.position, destino.rotation);
    }

    private static IEnumerator Co_Llevar(GameObject jugador, Transform destino)
    {
        bool terminado = false;
        void AlTerminar() => terminado = true;
        TeleportService.OnTeleportEnded += AlTerminar;
        Llevar(jugador, destino);
        float limite = Time.unscaledTime + EsperaDelTeletransporte;
        while (!terminado && Time.unscaledTime < limite && TeleportService.Inst != null) yield return null;
        TeleportService.OnTeleportEnded -= AlTerminar;
    }
}
