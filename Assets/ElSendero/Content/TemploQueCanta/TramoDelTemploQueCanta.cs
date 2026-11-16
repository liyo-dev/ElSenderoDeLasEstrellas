using Game.NPC;
using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Un tramo del templo que canta: se juega con un pulso que no perdona y se reintenta en el acto,
/// sin pantalla de derrota. Cada tramo es su propio punto de vuelta: al fallar (caerse al foso, que
/// se apaguen las velas o que la marea alcance) se vuelve a empezar con el reloj desde el principio
/// y el grupo colocado como estaba al entrar.
/// </summary>
public sealed class TramoDelTemploQueCanta : PruebaDelJugador
{
    [Header("El tramo")]
    [Tooltip("Reloj del tramo.")]
    [SerializeField] private TempoClock pulso;
    [Tooltip("Velas de la cuenta atrás (en las salas donde se para a pensar). Puede ir vacío.")]
    [SerializeField] private MeasureCandles velas;
    [Tooltip("Marea que persigue (en las huidas). Puede ir vacío.")]
    [SerializeField] private PulseTide marea;
    [Tooltip("Losas que laten y se hunden.")]
    [SerializeField] private PulseTile[] losas;
    [Tooltip("Rejillas y plataformas que duran compases.")]
    [SerializeField] private BeatGate[] rejas;

    [Tooltip("Pulsos por minuto de este tramo (el estribillo va más deprisa).")]
    [SerializeField, Min(40f)] private float bpm = 120f;

    [Tooltip("Compases de gracia antes de que la marea empiece a avanzar: los primeros metros se hacen sin ella encima.")]
    [SerializeField, Min(0)] private int compasesDeGracia = 4;

    [Header("Caída")]
    [Tooltip("Por debajo de esta altura se da el tramo por perdido (foso del templo).")]
    [SerializeField] private float alturaDelFoso = -8f;

    [Header("Meta")]
    [Tooltip("Donde se acaba el tramo. Va vacío en los tramos que no acaban al llegar a un sitio.")]
    [SerializeField] private Transform meta;
    [Tooltip("Metros a la meta a los que se da el tramo por ganado.")]
    [SerializeField, Min(0.5f)] private float radioDeLaMeta = 1.5f;

    [Header("Compañeros")]
    [Tooltip("Donde esperan los compañeros al empezar el tramo (su cornisa, si la hay). Vacío: se quedan donde estén.")]
    [SerializeField] private Transform puestoDeLosCompaneros;

    private bool _empezadaLaCuenta;
    private float _siguienteRecogida;
    private readonly object _anclaDeLosCompaneros = new object();

    protected override void OnEnable()
    {
        base.OnEnable();
        if (marea != null) marea.Caught += AlAlcanzarLaMarea;
    }

    protected override void OnDisable()
    {
        if (marea != null) marea.Caught -= AlAlcanzarLaMarea;
        base.OnDisable();
    }

    private void AlAlcanzarLaMarea(PulseTide quien)
    {
        if (EnCurso) Fallar();
    }

    protected override void PrepararIntento()
    {
        _empezadaLaCuenta = false;
        SoltarElSitio();

        if (pulso != null)
        {
            pulso.StartPulse();
        }
        if (velas != null) velas.Refill();
        if (marea != null) marea.ResetTide();

        for (int i = 0; losas != null && i < losas.Length; i++)
            if (losas[i] != null) losas[i].ResetTile();
        for (int i = 0; rejas != null && i < rejas.Length; i++)
            if (rejas[i] != null) rejas[i].ResetGate();

        // Los compañeros vuelven junto al jugador: al fallar pueden quedar anclados lejos, o detrás
        // de la rejilla que se ha cerrado. Se fuerza el viaje: si hay muro, se cruza igual.
        if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(forzar: true, avisar: false);

        // Quien se quedó en la lava vuelve entero: el reintento no puede empezar con un caído.
        var salud = SaludDelGrupo.Instance;
        if (salud != null)
        {
            salud.LevantarATodos(1f);
            salud.CurarATodos();
        }
    }

    protected override void Arrancar()
    {
        if (pulso != null) pulso.SetBpm(bpm);
        // En las salas de pensar la marea se queda parada en la entrada: el reloj es de las velas.
        // En las huidas arranca con unos compases de gracia, cuando ya llevas recorrido.
        if (marea != null && velas == null)
        {
            if (compasesDeGracia > 0) marea.PauseForMeasures(compasesDeGracia);
            else marea.Resume();
        }
        if (velas != null)
        {
            velas.BeginCountdown();
            _empezadaLaCuenta = true;
        }
        // Los compañeros salen desde su sitio (su cornisa), no desde donde les dejara el reintento.
        if (puestoDeLosCompaneros != null) ColocarCompaneros();
    }

    private void ColocarCompaneros()
    {
        var cuerpos = PilotoDelCuerpo.Cuerpos;
        int cuantos = 0;
        for (int i = 0; i < cuerpos.Count; i++)
        {
            var cuerpo = cuerpos[i];
            if (cuerpo == null || cuerpo.LoLlevaElJugador) continue;

            Vector3 sitio = puestoDeLosCompaneros.position +
                            puestoDeLosCompaneros.right * (1.3f * (cuantos == 0 ? -1f : 1f));
            var agente = cuerpo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null && agente.enabled) agente.Warp(sitio);
            cuerpo.transform.SetPositionAndRotation(sitio, puestoDeLosCompaneros.rotation);
            // Se quedan ahí: no saben saltar las losas, así que no se les deja intentarlo.
            cuerpo.GetComponent<NPCPartyMember>()?.NPCManager?.Context?.Anclar(_anclaDeLosCompaneros);
            cuantos++;
        }
    }

    /// <summary>Suelta a los compañeros del sitio del tramo para poder traerlos al reagrupar.</summary>
    private void SoltarElSitio()
    {
        var cuerpos = PilotoDelCuerpo.Cuerpos;
        for (int i = 0; i < cuerpos.Count; i++)
            cuerpos[i]?.GetComponent<NPCPartyMember>()?.NPCManager?.Context?.Soltar(_anclaDeLosCompaneros);
    }

    protected override void Actualizar(float dt)
    {
        var jugador = Jugador;
        // Solo pierde el que lleva el mando: si un compañero se queda atrás o se cae, se le trae.
        if (jugador != null && jugador.position.y < alturaDelFoso)
        {
            Fallar();
            return;
        }

        if (meta != null && jugador != null &&
            (jugador.position - meta.position).sqrMagnitude <= radioDeLaMeta * radioDeLaMeta)
        {
            Ganar();
            return;
        }

        if (_empezadaLaCuenta && velas != null && velas.RemainingMeasures <= 0)
            Fallar();

        RecogerCompaneros(dt);
    }

    /// <summary>
    /// A los compañeros que se quedan atrás o se caen no se les deja perder el tramo: se les trae al
    /// lado del jugador. No saben saltar plataformas (INC-1213), así que esto es lo que hay.
    /// </summary>
    private void RecogerCompaneros(float dt)
    {
        _siguienteRecogida -= dt;
        if (_siguienteRecogida > 0f) return;
        _siguienteRecogida = 0.4f;

        var jugador = Jugador;
        if (jugador == null) return;

        var cuerpos = PilotoDelCuerpo.Cuerpos;
        for (int i = 0; i < cuerpos.Count; i++)
        {
            var cuerpo = cuerpos[i];
            if (cuerpo == null || cuerpo.LoLlevaElJugador) continue;

            Vector3 suyo = cuerpo.transform.position;
            bool seCae = suyo.y < alturaDelFoso + 0.2f;
            bool seQueda = (suyo - jugador.position).sqrMagnitude > 22f * 22f;
            if (!seCae && !seQueda) continue;

            Vector3 sitio = jugador.position - jugador.forward * 2.2f + jugador.right * (i % 2 == 0 ? 1.3f : -1.3f);
            var agente = cuerpo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null && agente.enabled) agente.Warp(sitio);
            cuerpo.transform.position = sitio;
        }
    }

    protected override void Detener()
    {
        if (marea != null) marea.Pause();
        if (velas != null) velas.StopCountdown();
        // Al acabar el tramo (ganado o fallado) los compañeros vuelven con el jugador: en este
        // templo puede haber una rejilla o el ascensor de por medio, y andando no llegarían.
        SoltarElSitio();
        if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(forzar: true, avisar: false);
    }
}
