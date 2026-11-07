using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// El jugador a bordo de una embarcación.
///
/// Mientras está a bordo el jugador queda colgado del puesto de conducción (hijo del barco),
/// así que el barco se lo lleva, y su locomoción queda parada con <see cref="PasajeroJugador"/>
/// (lo mismo que usan las atracciones montables).
///
/// Al desembarcar se devuelve al jugador todo tal como estaba (padre, posición, física, input y
/// modo de acción).
/// </summary>
[DisallowMultipleComponent]
public class ConductorDeBarco : MonoBehaviour
{
    [Header("Embarque")]
    [Tooltip("Segundos que tarda el jugador en subir a la cubierta con el salto.")]
    [SerializeField] private float duracionDeSubida = 0.45f;

    [Header("Cámara")]
    [Tooltip("Metros extra que se aleja la cámara mientras el jugador está a bordo: a ras de cubierta " +
             "la cámara de exploración se queda dentro de la barco.")]
    [SerializeField] private float distanciaExtraEnBarco = 1.6f;

    // Modo de acción propio: bloquea salto, sprint, voltereta, ataque, magia, porteo, trepar y
    // volar. El movimiento NO se bloquea (el barco lo lee) ni la interacción (para volver a
    // pulsar el botón y bajar).
    private readonly PasajeroJugador _pasajero = new PasajeroJugador(ActionMode.Boating);
    private BarcoNavegable _barco;
    private VolteretaDelJugador _voltereta;
    private vThirdPersonCamera _camara;
    private float _distanciaDeCamaraNormal;
    private Coroutine _acercamiento;

    private GameObject _jugador => _pasajero.Jugador;

    /// <summary>True mientras hay un jugador a bordo.</summary>
    public bool TieneConductor => _jugador != null;

    /// <summary>El jugador a bordo (null si el barco está vacío).</summary>
    public GameObject Conductor => _jugador;

    void Awake()
    {
        _barco = GetComponent<BarcoNavegable>();
    }

    void Update()
    {
        // Si el modo de acción se ha reiniciado (muerte o respawn del jugador), el jugador ya no
        // está a bordo: se suelta para que no se quede bloqueado sin poder moverse.
        if (_jugador == null) return;
        var acciones = _pasajero.Acciones;
        if (acciones != null && !acciones.IsInMode(ActionMode.Boating)) SoltarJugador();
    }

    /// <summary>Deja al jugador donde está y devuelve su control, sin moverlo de sitio.</summary>
    void SoltarJugador()
    {
        var t = _jugador != null ? _jugador.transform : null;
        if (t != null) t.SetParent(_pasajero.PadreAnterior, true);
        _pasajero.Devolver();
        AlejarCamara(false);
        DevolverMusicaDelLugar();
    }

    /// <summary>
    /// Aleja la cámara mientras el jugador va a bordo. La cámara de tercera persona va detrás
    /// del personaje a 2,5 m: dentro de un barco eso es estar dentro del casco, así que se
    /// separa un poco más y al bajar vuelve a su sitio.
    /// </summary>
    void AlejarCamara(bool alejar)
    {
        if (_camara == null) _camara = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (_camara == null) return;

        if (alejar)
        {
            _distanciaDeCamaraNormal = _camara.defaultDistance;
            _camara.defaultDistance = _distanciaDeCamaraNormal + distanciaExtraEnBarco;
        }
        else if (_distanciaDeCamaraNormal > 0f)
        {
            _camara.defaultDistance = _distanciaDeCamaraNormal;
            _distanciaDeCamaraNormal = 0f;
        }
    }

    /// <summary>Música propia del barco mientras se navega (si el barco tiene clip).</summary>
    void PonerMusicaDelBarco()
    {
        var audio = AudioService.Instance;
        if (audio == null || _barco == null) return;
        var clip = _barco.MusicaDelBarco;
        if (clip == null) return;
        audio.PlayMusic(clip, _barco.FundidoDeMusica);
    }

    /// <summary>
    /// Al bajar del barco vuelve la música del sitio. Se pide (no se pone) para que el relevo
    /// sea con gracia, como el de la cámara.
    /// </summary>
    void DevolverMusicaDelLugar()
    {
        var audio = AudioService.Instance;
        if (audio == null) return;
        audio.PedirMusicaDelLugar(1.8f);
    }

    /// <summary>
    /// Sube al jugador al puesto de conducción. Devuelve false si no se pudo (ya hay alguien a
    /// bordo, o el puesto no existe).
    /// </summary>
    public bool Embarcar(GameObject jugador, Transform puesto)
    {
        if (jugador == null || puesto == null || _jugador != null) return false;

        // Si venía nadando, sale del agua antes de tomar el barco: el nado y la conducción
        // escriben ambos en el Rigidbody del jugador y se pisarían.
        var nadadora = jugador.GetComponent<PlayerSwimmingController>();
        if (nadadora != null && nadadora.EstaEnElAgua) nadadora.SalirDelAgua();

        _pasajero.Tomar(jugador);
        var t = jugador.transform;
        _voltereta = jugador.GetComponentInParent<VolteretaDelJugador>();

        // Colgado del puesto: a partir de aquí el barco se lleva al jugador.
        t.SetParent(puesto, true);

        AlejarCamara(true);
        PonerMusicaDelBarco();

        if (_acercamiento != null) StopCoroutine(_acercamiento);
        _acercamiento = StartCoroutine(ColocarEnElPuesto(puesto));
        return true;
    }

    /// <summary>Baja al jugador del barco y lo deja en <paramref name="destino"/>.</summary>
    public void Desembarcar(Vector3 destino)
    {
        if (_jugador == null) return;

        if (_acercamiento != null)
        {
            StopCoroutine(_acercamiento);
            _acercamiento = null;
        }

        var t = _jugador.transform;
        t.SetParent(_pasajero.PadreAnterior, true);
        t.position = destino;

        // Al bajar, el jugador mira hacia fuera del barco (hacia donde se baja), no de costado.
        Vector3 fuera = destino - transform.position;
        fuera.y = 0f;
        t.rotation = fuera.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(fuera.normalized, Vector3.up)
            : Quaternion.LookRotation(ProaMundo(), Vector3.up);

        _pasajero.Devolver();
        AlejarCamara(false);
        DevolverMusicaDelLugar();
    }

    void OnDisable()
    {
        // El barco se va (escena, destruction): el jugador no puede quedarse colgado.
        if (_jugador != null) SoltarJugador();
    }

    // ── El salto al puesto ───────────────────────────────────────────────────

    /// <summary>
    /// El salto a bordo: el jugador sube desde el agua hasta la cubierta con un salto con
    /// voltereta. La voltereta (el giro) la pone el sistema oficial
    /// (<see cref="VolteretaDelJugador.EnElAire"/>); la parábola la lleva este componente, que
    /// es quien lo acerca al puesto.
    /// </summary>
    IEnumerator ColocarEnElPuesto(Transform puesto)
    {
        var t = _jugador != null ? _jugador.transform : null;
        if (t == null)
        {
            _acercamiento = null;
            yield break;
        }

        Vector3 inicio = t.localPosition;
        Quaternion giroInicio = t.localRotation;
        // Al final el jugador mira hacia la proa del barco (no hacia el +Z local del modelo:
        // estos barcos no vienen con la proa en +Z).
        Quaternion giroFinal = _barco != null ? _barco.RotacionLocalDeProa : Quaternion.identity;

        if (_voltereta != null) _voltereta.EnElAire();

        float duracion = Mathf.Max(0.2f, duracionDeSubida);
        float altura = Mathf.Max(0.9f, Mathf.Abs(inicio.y) * 0.6f + 0.9f);

        for (float s = 0f; s < duracion; s += Time.deltaTime)
        {
            if (_jugador == null || t == null)
            {
                _acercamiento = null;
                yield break;
            }
            float u = Mathf.Clamp01(s / duracion);
            Vector3 p = Vector3.Lerp(inicio, Vector3.zero, u);
            p.y += altura * 4f * u * (1f - u);
            t.localPosition = p;
            t.localRotation = Quaternion.Slerp(giroInicio, giroFinal, u);
            yield return null;
        }

        if (_jugador != null && t != null)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = giroFinal;
        }
        _acercamiento = null;
    }

    Vector3 ProaMundo()
    {
        if (_barco != null) return _barco.DireccionDeProa;
        return transform.forward;
    }
}
