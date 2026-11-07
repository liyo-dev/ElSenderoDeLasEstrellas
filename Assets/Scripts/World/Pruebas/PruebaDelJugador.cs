using System;
using System.Collections;
using System.Collections.Generic;
using Sendero.Core.Feedback;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base de las pruebas que juega el jugador con salida y sin derrota: una carrera, una escalada,
/// reventar dianas contra el reloj. Lleva el ciclo común a todas y deja a cada prueba solo sus
/// reglas (<see cref="PrepararIntento"/>, <see cref="Arrancar"/>, <see cref="Detener"/>, y
/// <see cref="Ganar"/> o <see cref="Fallar"/> cuando toque).
///
/// Ciclo de cada intento: se tapa la pantalla, el jugador aparece en la salida con el control
/// bloqueado, se prepara la prueba y lo que la acompaña (<see cref="IEtapasDePrueba"/> en este
/// mismo objeto), se destapa, se presenta, cuenta atrás por el rótulo breve (que pasa por el árbitro
/// de avisos) y se suelta el control. Al ganar se celebra, se lleva al jugador donde diga
/// <see cref="llevarAlGanarA"/> y se avisa con <see cref="alGanar"/> (lo último que hace: quien lo
/// escuche puede apagar la prueba). Al fallar, con <see cref="reintentarAlFallar"/>, se vuelve a
/// empezar en el acto; sin él, se acaba. Cada intento perdido se avisa con <see cref="alFallar"/>.
///
/// Arranca sola al activarse (<see cref="empezarAlActivarse"/>, p. ej. con un
/// <see cref="ActivoEnTramo"/>), al acercarse el jugador a la salida (<see cref="radioDeLaSalida"/>)
/// o con <see cref="Empezar"/>. Todo lo que sujeta (control, pantalla, etapas) lo suelta también al
/// desactivarse.
/// </summary>
public abstract class PruebaDelJugador : MonoBehaviour
{
    public enum Estado { Esperando, Preparando, CuentaAtras, EnCurso, Ganada, Fallada, Terminada }

    [Header("Salida")]
    [Tooltip("Donde aparece el jugador al empezar cada intento (mira hacia su eje Z).")]
    [SerializeField] protected Transform salidaDelJugador;
    [Tooltip("Empieza sola en cuanto el objeto se activa (y el jugador está listo).")]
    [SerializeField] private bool empezarAlActivarse = true;
    [Tooltip("Si no empieza al activarse: metros a la salida a los que empieza al acercarse el jugador (0 = solo con Empezar()).")]
    [SerializeField, Min(0f)] private float radioDeLaSalida;
    [Tooltip("Segundos de fundido a negro al colocar al jugador (0 = sin fundido).")]
    [SerializeField, Min(0f)] private float fundido = 0.35f;

    [Header("Cuenta atrás")]
    [SerializeField, Min(0)] private int segundosDeCuentaAtras = 3;
    [Tooltip("Clave del rótulo de salida («¡Ya!»). Con la clave y el texto vacíos no sale rótulo.")]
    [SerializeField] private string claveDeSalida = "PRUEBA_YA";
    [Tooltip("Texto si la clave no está traducida.")]
    [SerializeField] private string textoDeSalida = "¡Ya!";
    [SerializeField] private string sfxDeCuenta = "";
    [SerializeField] private string sfxDeSalida = "";

    [Header("Final")]
    [Tooltip("Al fallar se vuelve a empezar en el acto (sin derrota). Sin marcar, la prueba se acaba.")]
    [SerializeField] private bool reintentarAlFallar = true;
    [Tooltip("Segundos entre fallar y tapar la pantalla para el reintento.")]
    [SerializeField, Min(0f)] private float pausaAntesDelReintento = 1f;
    [Tooltip("Si se rellena, al ganar se lleva al jugador aquí (con fundido) antes de avisar.")]
    [SerializeField] private Transform llevarAlGanarA;

    [Header("Eventos")]
    [Tooltip("Al preparar cada intento, con la pantalla tapada.")]
    public UnityEvent alPrepararse = new UnityEvent();
    [Tooltip("Al dar la salida.")]
    public UnityEvent alEmpezar = new UnityEvent();
    [Tooltip("Al ganar, lo último que hace la prueba (puede apagarla quien lo escuche).")]
    public UnityEvent alGanar = new UnityEvent();
    [Tooltip("Al perder un intento (con reintento, antes de volver a empezar).")]
    public UnityEvent alFallar = new UnityEvent();

    /// <summary>Cada intento al dar la salida, al ganar y al fallar (antes del reintento).</summary>
    public event Action<PruebaDelJugador> Empezada, Ganada, Fallada;

    public Estado EstadoActual { get; private set; } = Estado.Esperando;
    /// <summary>Intento en curso: 1 el primero.</summary>
    public int Intento { get; private set; }
    /// <summary>Segundos desde la salida del intento en curso.</summary>
    public float TiempoEnCurso { get; private set; }
    public bool EnCurso => EstadoActual == Estado.EnCurso;

    private readonly List<IEtapasDePrueba> _etapas = new List<IEtapasDePrueba>();
    private Coroutine _ciclo;
    private bool _controlBloqueado;
    private bool _pantallaTapada;
    private float _siguienteMirada;

    protected Transform Jugador => PlayerService.Player != null ? PlayerService.Player.transform : null;

    protected virtual void Awake()
    {
        GetComponents(_etapas);
    }

    protected virtual void OnEnable()
    {
        EstadoActual = Estado.Esperando;
        Intento = 0;
        if (empezarAlActivarse) Empezar();
    }

    protected virtual void OnDisable()
    {
        _ciclo = null;
        if (EstadoActual == Estado.EnCurso) Detener();
        SoltarTodo();
        if (EstadoActual != Estado.Terminada) EstadoActual = Estado.Esperando;
    }

    protected virtual void Update()
    {
        if (EstadoActual == Estado.Esperando && !empezarAlActivarse && radioDeLaSalida > 0f && Time.time >= _siguienteMirada)
        {
            _siguienteMirada = Time.time + 0.25f;
            var jugador = Jugador;
            if (jugador != null && salidaDelJugador != null &&
                (jugador.position - salidaDelJugador.position).sqrMagnitude <= radioDeLaSalida * radioDeLaSalida)
                Empezar();
            return;
        }

        if (EstadoActual != Estado.EnCurso) return;
        TiempoEnCurso += Time.deltaTime;
        Actualizar(Time.deltaTime);
    }

    // ── API ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Empieza la prueba desde el primer intento. No hace nada si ya está en marcha.</summary>
    public void Empezar()
    {
        if (!isActiveAndEnabled) return;
        if (EstadoActual != Estado.Esperando && EstadoActual != Estado.Terminada) return;
        Intento = 0;
        _ciclo = StartCoroutine(Co_Intento());
    }

    /// <summary>Corta la prueba sin ganar ni fallar y lo suelta todo.</summary>
    public void Abandonar()
    {
        if (_ciclo != null) StopCoroutine(_ciclo);
        _ciclo = null;
        if (EstadoActual == Estado.EnCurso) Detener();
        SoltarTodo();
        EstadoActual = Estado.Terminada;
    }

    // ── Lo que pone cada prueba ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Al dar la salida se devuelve el control al jugador. Las pruebas que se juegan sin moverse
    /// (un concurso de botones) lo dejan bloqueado hasta acabar.
    /// </summary>
    protected virtual bool SoltarElControlAlEmpezar => true;

    /// <summary>Con la pantalla tapada y el jugador en la salida: deja la prueba como al principio.</summary>
    protected abstract void PrepararIntento();

    /// <summary>Se da la salida.</summary>
    protected abstract void Arrancar();

    /// <summary>Cada fotograma mientras está en curso.</summary>
    protected abstract void Actualizar(float dt);

    /// <summary>Se acaba el intento (ganado, perdido o cortado): parar lo que se mueva.</summary>
    protected abstract void Detener();

    /// <summary>La prueba en curso se ha ganado.</summary>
    protected void Ganar()
    {
        if (EstadoActual != Estado.EnCurso) return;
        EstadoActual = Estado.Ganada;
        Detener();
        if (_ciclo != null) StopCoroutine(_ciclo);
        _ciclo = StartCoroutine(Co_Ganar());
    }

    /// <summary>La prueba en curso se ha perdido.</summary>
    protected void Fallar()
    {
        if (EstadoActual != Estado.EnCurso) return;
        EstadoActual = Estado.Fallada;
        Detener();
        if (_ciclo != null) StopCoroutine(_ciclo);
        _ciclo = StartCoroutine(Co_Fallar());
    }

    // ── El ciclo ─────────────────────────────────────────────────────────────────────────────

    private IEnumerator Co_Intento()
    {
        EstadoActual = Estado.Preparando;
        // Con la escena recién cargada, espera a que haya jugador y a que se abra el telón de la carga.
        while (PlayerService.Player == null || Telon.Cerrado) yield return null;
        yield return null;

        Intento++;
        TiempoEnCurso = 0f;
        BloquearControl(true);

        yield return Tapar(fundido);
        ColocarAlJugador(salidaDelJugador);
        PrepararIntento();
        alPrepararse.Invoke();
        for (int i = 0; i < _etapas.Count; i++) yield return _etapas[i].Colocar(this);
        yield return Destapar(fundido);

        for (int i = 0; i < _etapas.Count; i++) yield return _etapas[i].Presentar(this);

        EstadoActual = Estado.CuentaAtras;
        for (int n = segundosDeCuentaAtras; n > 0; n--)
        {
            while (ArbitroDeAvisos.Silenciado) yield return null;
            HudToastService.Instance?.ShowRaw(n.ToString(), 0.9f);
            if (!string.IsNullOrEmpty(sfxDeCuenta)) AudioService.Instance?.PlaySFX(sfxDeCuenta);
            yield return new WaitForSeconds(1f);
        }
        if (!string.IsNullOrEmpty(claveDeSalida) || !string.IsNullOrEmpty(textoDeSalida))
            HudToastService.Instance?.ShowRaw(PresentadorDeLinea.Texto(claveDeSalida, textoDeSalida), 1.2f);
        if (!string.IsNullOrEmpty(sfxDeSalida)) AudioService.Instance?.PlaySFX(sfxDeSalida);

        if (SoltarElControlAlEmpezar) BloquearControl(false);
        EstadoActual = Estado.EnCurso;
        _ciclo = null;
        Arrancar();
        alEmpezar.Invoke();
        Empezada?.Invoke(this);
    }

    private IEnumerator Co_Ganar()
    {
        for (int i = 0; i < _etapas.Count; i++) yield return _etapas[i].Celebrar(this);
        if (llevarAlGanarA != null)
        {
            yield return Tapar(fundido);
            ColocarAlJugador(llevarAlGanarA);
            yield return Destapar(fundido);
        }
        _ciclo = null;
        SoltarTodo();
        EstadoActual = Estado.Terminada;
        Ganada?.Invoke(this);
        alGanar.Invoke();
    }

    private IEnumerator Co_Fallar()
    {
        Fallada?.Invoke(this);
        alFallar.Invoke();
        for (int i = 0; i < _etapas.Count; i++) yield return _etapas[i].Lamentar(this);
        if (pausaAntesDelReintento > 0f) yield return new WaitForSeconds(pausaAntesDelReintento);
        if (reintentarAlFallar)
        {
            _ciclo = StartCoroutine(Co_Intento());
            yield break;
        }
        _ciclo = null;
        SoltarTodo();
        EstadoActual = Estado.Terminada;
    }

    // ── Utilidades para las pruebas ──────────────────────────────────────────────────────────

    /// <summary>Lleva al jugador a un punto (sin fundido), mirando hacia su eje Z.</summary>
    protected void ColocarAlJugador(Transform punto)
    {
        var jugador = PlayerService.Player;
        if (jugador == null || punto == null) return;
        if (TeleportService.Inst != null) TeleportService.Inst.DoTeleportToAnchor(jugador, punto, false);
        else jugador.transform.SetPositionAndRotation(punto.position, punto.rotation);
    }

    /// <summary>Lleva al jugador a un punto con un fundido corto (al caerse, al salirse).</summary>
    protected IEnumerator Co_Reaparecer(Transform punto, float segundos)
    {
        yield return Tapar(segundos);
        ColocarAlJugador(punto);
        yield return Destapar(segundos);
    }

    private IEnumerator Tapar(float segundos)
    {
        if (segundos <= 0f) yield break;
        _pantallaTapada = true;
        yield return FeedbackService.ScreenFadeAsync(Color.black, segundos, true);
    }

    private IEnumerator Destapar(float segundos)
    {
        if (!_pantallaTapada) yield break;
        yield return FeedbackService.ScreenFadeAsync(Color.black, segundos, false);
        _pantallaTapada = false;
    }

    private void BloquearControl(bool bloquear)
    {
        if (bloquear == _controlBloqueado) return;
        _controlBloqueado = bloquear;
        // Al soltar no se crea el servicio si ya no existe (cierre del juego).
        var servicio = bloquear || PlayerLockService.HasInstance ? PlayerLockService.Instance : null;
        if (servicio == null) return;
        if (bloquear) servicio.Acquire(this);
        else servicio.Release(this);
    }

    private void SoltarTodo()
    {
        BloquearControl(false);
        if (_pantallaTapada)
        {
            _pantallaTapada = false;
            FeedbackService.ScreenFade(Color.black, 0.25f, false);
        }
        for (int i = 0; i < _etapas.Count; i++) _etapas[i]?.Soltar(this);
    }
}
