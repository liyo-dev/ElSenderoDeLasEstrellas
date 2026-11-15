using Game.NPC;
using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Un tramo del templo que canta: se juega con un pulso que no perdona y se reintenta en el acto,
/// sin pantalla de derrota. Cada tramo es su propio punto de vuelta: al fallar (caerse al foso, que
/// se apaguen las velas o que la marea alcance) se vuelve a empezar con la música en ese punto y el
/// grupo colocado como estaba al entrar.
/// </summary>
public sealed class TramoDelTemploQueCanta : PruebaDelJugador
{
    [Header("El tramo")]
    [Tooltip("Pulso de la música del tramo.")]
    [SerializeField] private MusicPulse pulso;
    [Tooltip("Velas de la cuenta atrás (en las salas donde se para a pensar). Puede ir vacío.")]
    [SerializeField] private MeasureCandles velas;
    [Tooltip("Marea que persigue (en las huidas). Puede ir vacío.")]
    [SerializeField] private PulseTide marea;
    [Tooltip("Losas que laten y se hunden.")]
    [SerializeField] private PulseTile[] losas;
    [Tooltip("Rejillas y plataformas que duran compases.")]
    [SerializeField] private BeatGate[] rejas;

    [Header("Caída")]
    [Tooltip("Por debajo de esta altura se da el tramo por perdido (foso del templo).")]
    [SerializeField] private float alturaDelFoso = -8f;

    [Header("Meta")]
    [Tooltip("Donde se acaba el tramo. Va vacío en los tramos que no acaban al llegar a un sitio.")]
    [SerializeField] private Transform meta;
    [Tooltip("Metros a la meta a los que se da el tramo por ganado.")]
    [SerializeField, Min(0.5f)] private float radioDeLaMeta = 1.5f;

    private bool _empezadaLaCuenta;

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

        if (pulso != null) pulso.StartPulse();
        if (velas != null) velas.Refill();
        if (marea != null) marea.ResetTide();

        for (int i = 0; losas != null && i < losas.Length; i++)
            if (losas[i] != null) losas[i].ResetTile();
        for (int i = 0; rejas != null && i < rejas.Length; i++)
            if (rejas[i] != null) rejas[i].ResetGate();

        // Los compañeros vuelven junto al jugador: al fallar pueden quedar anclados lejos.
        if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(avisar: false);
    }

    protected override void Arrancar()
    {
        // En las salas de pensar la marea se queda parada en la entrada: el reloj es de las velas.
        if (marea != null && velas == null) marea.Resume();
        if (velas != null)
        {
            velas.BeginCountdown();
            _empezadaLaCuenta = true;
        }
    }

    protected override void Actualizar(float dt)
    {
        var jugador = Jugador;
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
    }

    protected override void Detener()
    {
        if (marea != null) marea.Pause();
        if (velas != null) velas.StopCountdown();
    }
}
