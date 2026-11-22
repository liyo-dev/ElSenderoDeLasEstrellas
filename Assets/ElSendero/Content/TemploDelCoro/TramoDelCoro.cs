using Game.NPC;
using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Un tramo del Templo del Coro: se corre con la canción del guardián sonando, y lo que cae, se hunde
/// o se abre va a sus golpes (lo leen las piezas del kit de la partitura). Detrás viene la sombra a
/// la velocidad de la canción: pararse es que te alcance. Al fallar (la sombra, el foso o caer
/// rendido) se vuelve en el acto al punto de reintento con la canción en su sitio.
///
/// El primer intento empieza donde diga <see cref="PruebaDelJugador"/> (la sala de la afinación, con
/// la canción desde el principio); los siguientes, desde el último <see cref="puntosDeControl"/>
/// pasado, con la canción un compás antes de que toque salir de él. El grupo espera quieto hasta
/// ese segundo y luego hay que mantenerse dentro del recuadro que va con la canción por el suelo: por detrás espera la
/// sombra (quien se queda atrás, repite) y al borde de delante no se corre más que la canción (lo que
/// cae está puesto a su paso). Dentro, el sprint va libre. En los <see cref="calderones"/> (lo que
/// pide pararse: cambiar de personaje, disparar) el recuadro y la sombra se paran unos segundos y
/// siguen; la canción no para.
/// La salida se cuenta al compás («3, 2, 1, ¡Ya!» en los tiempos de antes) y la cámara la lleva
/// <see cref="CamaraDelCoro"/>: plano del Tenor en la presentación del primer intento y cámara de la huida.
/// Ver INC-1393.
/// </summary>
public sealed class TramoDelCoro : PruebaDelJugador
{
    private const string ClaveDeLaMusica = "TemploDelCoro";

    [Header("La canción")]
    [Tooltip("Reloj que toca la canción y sigue su partitura.")]
    [SerializeField] private TempoClock reloj;
    [Tooltip("Segundo de la canción en el primer intento.")]
    [SerializeField, Min(0f)] private float segundoDeSalida;

    [Tooltip("Cámara del tramo: presentación y huida.")]
    [SerializeField] private CamaraDelCoro camara;
    [Tooltip("Tiempos de antes de la salida en los que se cuenta («3, 2, 1»).")]
    [SerializeField, Range(0, 4)] private int tiemposDeCuenta = 3;
    [Tooltip("Tiempos antes de la cuenta en los que acaba el plano de la presentación.")]
    [SerializeField, Min(0)] private int tiemposDeVuelta = 1;

    [Header("Reintento")]
    [Tooltip("Puntos de control en orden por el corredor: al fallar se vuelve al último que se haya pasado.")]
    [SerializeField] private Transform[] puntosDeControl = System.Array.Empty<Transform>();
    [Tooltip("Tiempos de la canción que suenan antes de salir al reintentar (para coger el ritmo).")]
    [SerializeField, Min(0)] private int tiemposAntesDelReintento = 4;
    [Tooltip("Lo que se rompe o se abre por el camino (puertas que se queman) y vuelve a estar al reintentar.")]
    [SerializeField] private GameObject[] objetosQueVuelven = System.Array.Empty<GameObject>();
    [Tooltip("Lo que aparece al resolver algo por el camino (un corista que se cae sentado) y se quita al reintentar.")]
    [SerializeField] private GameObject[] objetosQueSeVan = System.Array.Empty<GameObject>();

    [Header("La sombra que persigue")]
    [Tooltip("Lo que se ve de la sombra (se mueve por el eje Z del tramo).")]
    [SerializeField] private Transform sombra;
    [Tooltip("Metros por segundo a los que va la canción por el corredor: donde está puesto cada peligro.")]
    [SerializeField, Min(0f)] private float metrosPorSegundo = 6f;
    [Tooltip("Tope de velocidad por detrás del centro del recuadro: el sprint, para recuperar el centro.")]
    [SerializeField, Min(0f)] private float topeLibre = 9f;
    [Tooltip("El recuadro que va con la canción por el suelo (se mueve por el eje Z del tramo).")]
    [SerializeField] private Transform recuadro;
    [Tooltip("Metros por delante de la canción hasta el borde que limita el avance.")]
    [SerializeField, Min(0f)] private float metrosAdelante = 2.0f;
    [Tooltip("Lo mismo, con el recuadro parado (un respiro): margen para cruzar lo que pide el respiro (un hueco con plataformas).")]
    [SerializeField, Min(0f)] private float metrosAdelanteEnRespiro = 12f;
    [Tooltip("Segundo de la canción desde el que no hay borde por delante del recuadro (un salto largo del final). 0: siempre lo hay.")]
    [SerializeField, Min(0f)] private float segundoSinTope;
    [Tooltip("Calderones en orden (X: la Z donde se paran el recuadro y la sombra; Y: los segundos): un respiro para lo que pide pararse, como cambiar de personaje y encender una antorcha. La canción sigue.")]
    [SerializeField] private Vector2[] calderones = System.Array.Empty<Vector2>();
    [Tooltip("Tramos en los que el recuadro y la sombra van más deprisa (X: la Z donde empieza; Y: los segundos que dura; Z: los metros de más que ganan en ese rato). Hay que tirar de sprint.")]
    [SerializeField] private Vector3[] acelerones = System.Array.Empty<Vector3>();
    [Tooltip("Tramos de la canción (X: desde; Y: hasta) en los que el grupo no se mueve: un momento de cine con la cámara girada.")]
    [SerializeField] private Vector2[] tramosSinControl = System.Array.Empty<Vector2>();
    [Tooltip("Segundo de la canción en el que la sombra pasa por la Z de arranque.")]
    [SerializeField] private float segundoDeArranque;
    [Tooltip("Z en la que empieza el corredor.")]
    [SerializeField] private float zDeArranque;
    [Tooltip("Metros del recuadro por detrás de donde marca la canción: ahí va la sombra (quien se queda atrás, repite).")]
    [SerializeField, Min(0f)] private float metrosDeVentaja = 12f;
    [Tooltip("Filas de suelo en orden. Desaparecen al quedar por detrás de la sombra y vuelven al reintentar.")]
    [SerializeField] private Transform[] sueloQueSePierde = System.Array.Empty<Transform>();
    private int _primeraFilaVisible;
    private Vector3[] _posicionesDelSuelo;
    private float[] _iniciosDeCaida;
    [SerializeField] private GameObject polvoDelSuelo;
    private CanvasGroup _vineta;
    private Texture2D _texturaDeLaVineta;
    private const float SegundosDeAvisoDeSombra = 2.2f;
    private const float SegundosDeMargenExtra = 0.67f;
    private const float DistanciaDeAvisoDelSuelo = 5f;
    private const float DuracionDeCaida = 0.68f;
    private float _margenPuesto;
    [Tooltip("Segundos tras un cambio de personaje en los que la sombra no gana terreno.")]
    [SerializeField, Min(0f)] private float graciaDelCambio = 0.8f;

    [Header("Fin")]
    [SerializeField] private float alturaDelFoso = -5f;
    [Tooltip("Durante la tralla, la sombra cierra el escenario por detrás. X: inicio del aviso, Y: primera nota, Z: final de la tralla, W: borde trasero del recinto.")]
    [SerializeField] private Vector4 cierreTrasero;
    [Tooltip("Segundo de la canción desde el que ya no se falla (el golpe final: el suelo se hunde y se cae al vacío). 0: no hay.")]
    [SerializeField, Min(0f)] private float segundoSinFallar;
    [Tooltip("Segundo de la canción en el que se gana sin llegar a la meta (el final de la canción). 0: solo con la meta.")]
    [SerializeField, Min(0f)] private float segundoDeGanar;
    [SerializeField] private Transform meta;
    [SerializeField, Min(0.5f)] private float radioDeLaMeta = 3f;

    [Tooltip("Plataformas que aparecen durante el tramo: al reintentar se esconden.")]
    [SerializeField] private PlataformaTemporal[] plataformas;
    [Tooltip("Antorchas y cristales del tramo: al reintentar se apagan.")]
    [SerializeField] private ReceptorDeMagia[] receptores;

    private float _segundoDeEsteIntento;
    private int _puntoPasado = -1;
    private float _segundoDeSoltar;
    private bool _corriendo;
    private int _tiempoDeSoltar = -1;
    private int _siguienteCuenta;
    // Metros que la sombra ha cedido mientras el jugador lanzaba o cambiaba de personaje.
    private float _cedido;
    private float _topePuesto;
    private float _zAntes;
    private float _ultimoCambio = -100f;
    private bool _sinControl;
    private GameObject _cuerpoDelCaster;
    private MagicCaster _caster;

    /// <summary>El grupo no se mueve hasta que toca correr (lo suelta <see cref="Actualizar"/>).</summary>
    protected override bool SoltarElControlAlEmpezar => false;

    protected override void Awake()
    {
        base.Awake();
        _segundoDeEsteIntento = segundoDeSalida;
        _salidaDelPrimerIntento = salidaDelJugador;
        _posicionesDelSuelo = new Vector3[sueloQueSePierde.Length];
        _iniciosDeCaida = new float[sueloQueSePierde.Length];
        for (int i = 0; i < sueloQueSePierde.Length; i++)
            _posicionesDelSuelo[i] = sueloQueSePierde[i].localPosition;
        MontarAvisoDeSombra();
    }

    private Transform _salidaDelPrimerIntento;

    /// <summary>Ganada: vuelta de cámara de despedida y, al acabar, la pregunta de repetir.</summary>
    [Header("Saludo final")]
    [SerializeField] private Transform marcaDelSaludo;
    [SerializeField] private Light focoDelSaludo;
    [SerializeField] private GameObject confetiDelSaludo;

    private void AlGanar(PruebaDelJugador _)
    {
        if (marcaDelSaludo != null)
        {
            var cuerpo = ControlDelJugador.CuerpoDelJugador;
            if (cuerpo != null)
            {
                TeleportService.TeleportToPosition(cuerpo, marcaDelSaludo.position, marcaDelSaludo.rotation, null);
                if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(forzar: true, avisar: false);
            }

            if (focoDelSaludo != null) focoDelSaludo.enabled = true;
            var piscina = VfxPoolService.Instance;
            if (confetiDelSaludo != null && piscina != null)
            {
                piscina.Play(confetiDelSaludo, marcaDelSaludo.position - marcaDelSaludo.right * 2.6f, Quaternion.identity, 5f);
                piscina.Play(confetiDelSaludo, marcaDelSaludo.position + marcaDelSaludo.right * 2.6f, Quaternion.identity, 5f);
            }
        }
        if (camara != null) camara.Despedir(PreguntarSiRepetir);
        else PreguntarSiRepetir();
    }

    private void PreguntarSiRepetir()
    {
        bool desdeElLab = VueltaAlLab.LlegoDesdeElLab(gameObject.scene);
        var pregunta = ConfirmationPopupUI.Instance;
        if (pregunta == null) { CerrarLaFuncion(desdeElLab); return; }
        // Con la pregunta en pantalla, el plano queda limpio: solo el cuadro y el Tenor a la derecha.
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD(this);
        _hudOculto = true;
        string salida = desdeElLab ? PresentadorDeLinea.Texto("CORO_VOLVER_AL_LAB", "Volver al Lab")
                                   : PresentadorDeLinea.Texto("CORO_REPETIR_NO", "Quedarme");
        pregunta.Show(PresentadorDeLinea.Texto("CORO_REPETIR", "¡Bravo! ¿Repetir la función?"), Repetir, () => CerrarLaFuncion(desdeElLab),
                      PresentadorDeLinea.Texto("CORO_REPETIR_SI", "Repetir"), salida,
                      selectConfirm: true, centrado: true);
    }

    private void CerrarLaFuncion(bool desdeElLab)
    {
        SoltarLaCamara();
        if (desdeElLab) VueltaAlLab.VolverALaZonaDeJefes();
    }

    private bool _hudOculto;

    private void DevolverElHUD()
    {
        if (!_hudOculto) return;
        _hudOculto = false;
        Sendero.UI.PlayerHUDV2.Instance?.ShowHUD(this);
    }

    private void SoltarLaCamara()
    {
        DevolverElHUD();
        if (focoDelSaludo != null) focoDelSaludo.enabled = false;
        if (camara != null) camara.Soltar();
    }

    /// <summary>Vuelve a empezar desde la sala de la afinación, con la canción desde el principio.</summary>
    private void Repetir()
    {
        SoltarLaCamara();
        salidaDelJugador = _salidaDelPrimerIntento;
        _segundoDeEsteIntento = segundoDeSalida;
        _puntoPasado = -1;
        Empezar();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Fallada += AlFallar;
        Ganada += AlGanar;
        PartyControlManager.OnActiveCharacterChanged += AlCambiarDePersonaje;
    }

    private void AlCambiarDePersonaje(int _) => _ultimoCambio = Time.time;

    protected override void OnDisable()
    {
        Fallada -= AlFallar;
        Ganada -= AlGanar;
        PartyControlManager.OnActiveCharacterChanged -= AlCambiarDePersonaje;
        SoltarLaMusica();
        DejarDeCorrer();
        DevolverElHUD();
        if (focoDelSaludo != null) focoDelSaludo.enabled = false;
        if (camara != null) camara.Soltar();
        Kits.Core.GameContext.Exit(Situaciones.PruebaSinPausa, this);
        SombraDeContacto.SoltarRefuerzo(this);
        base.OnDisable();
    }

    private void AlFallar(PruebaDelJugador prueba)
    {
        // Desde el primer fallo se vuelve al último punto de control pasado (como poco, el primero:
        // la entrada del corredor), no a la sala de la afinación.
        if (puntosDeControl.Length == 0) return;
        int i = Mathf.Clamp(_puntoPasado, 0, puntosDeControl.Length - 1);
        if (puntosDeControl[i] == null) return;
        salidaDelJugador = puntosDeControl[i];
        float sale = SegundoEnZ(salidaDelJugador.position.z);
        var partitura = reloj != null ? reloj.Score : null;
        _segundoDeEsteIntento = partitura != null && partitura.BeatCount > 1
            ? partitura.BeatTime(Mathf.Max(0, partitura.BeatIndexAt(sale) - tiemposAntesDelReintento))
            : Mathf.Max(0f, sale - 1.6f);
    }

    protected override void PrepararIntento()
    {
        if (reloj != null) reloj.StopPulse();
        for (int i = 0; plataformas != null && i < plataformas.Length; i++)
            if (plataformas[i] != null) plataformas[i].OcultarYa();
        for (int i = 0; receptores != null && i < receptores.Length; i++)
            if (receptores[i] != null) receptores[i].Reiniciar();
        for (int i = 0; i < objetosQueVuelven.Length; i++)
            if (objetosQueVuelven[i] != null && !objetosQueVuelven[i].activeSelf) objetosQueVuelven[i].SetActive(true);
        for (int i = 0; i < objetosQueSeVan.Length; i++)
            if (objetosQueSeVan[i] != null && objetosQueSeVan[i].activeSelf) objetosQueSeVan[i].SetActive(false);
        for (int i = 0; i < sueloQueSePierde.Length; i++)
        {
            var fila = sueloQueSePierde[i];
            fila.localPosition = _posicionesDelSuelo[i];
            _iniciosDeCaida[i] = -1f;
            if (!fila.gameObject.activeSelf) fila.gameObject.SetActive(true);
        }
        _primeraFilaVisible = 0;
        _sinControl = false;
        if (PlayerParty.HasInstance) PlayerParty.Instance.TeleportAllMembersToPlayer(forzar: true, avisar: false);
        var salud = SaludDelGrupo.Instance;
        if (salud != null)
        {
            salud.LevantarATodos(1f);
            salud.CurarATodos();
        }
        _cedido = 0f;
        DejarDeCorrer();
        // Segundo en el que hay que salir para pasar por el arranque justo con la canción.
        float desde = salidaDelJugador != null ? salidaDelJugador.position.z : zDeArranque;
        _segundoDeSoltar = metrosPorSegundo > 0f ? SegundoEnZ(desde) : _segundoDeEsteIntento;
        // La salida cae en el tiempo de la canción más cercano, para contarla al compás.
        _tiempoDeSoltar = -1;
        _siguienteCuenta = tiemposDeCuenta;
        var partitura = reloj != null ? reloj.Score : null;
        if (partitura != null && partitura.BeatCount > 1)
        {
            int i = Mathf.Max(0, partitura.BeatIndexAt(_segundoDeSoltar));
            if (i + 1 < partitura.BeatCount && _segundoDeSoltar - partitura.BeatTime(i) > partitura.BeatLength(i) * 0.5f) i++;
            _tiempoDeSoltar = i;
            _segundoDeSoltar = partitura.BeatTime(i);
        }
        float borde = ColocarLaSombra(_segundoDeEsteIntento);
        // Al retomar desde un control, la parte que queda muy atrás ya ha caído.
        while (_primeraFilaVisible < sueloQueSePierde.Length &&
               sueloQueSePierde[_primeraFilaVisible].position.z < borde - 4f)
            sueloQueSePierde[_primeraFilaVisible++].gameObject.SetActive(false);
    }

    protected override void Arrancar()
    {
        // Mientras se juega, nada de menús rápidos (un toque sin querer al cambiar de personaje).
        Kits.Core.GameContext.Enter(Situaciones.PruebaSinPausa, this);
        // Sombras de los personajes fuertes y desde más alto: en los saltos se ve dónde se cae.
        SombraDeContacto.Reforzar(this);
        // La música del sitio se calla mientras suena la del guardián.
        AudioService.Instance?.AtenuarBus(ClaveDeLaMusica, AudioBus.Music, 0f);
        if (reloj != null) reloj.StartAtSeconds(_segundoDeEsteIntento);
        // Mientras el grupo espera quieto la cuenta, lo que ya suena (un regalito, una llamarada) no le
        // hace daño: solo se puede esquivar desde que se suelta.
        float espera = _segundoDeSoltar - _segundoDeEsteIntento;
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        var salud = espera > 0f && cuerpo != null ? cuerpo.GetComponentInChildren<PlayerHealthSystem>() : null;
        if (salud != null) salud.HacerInvulnerable(espera + 0.3f, parpadeo: false);
        if (camara == null) return;
        float finDelPlano = SegundoDelTiempo(_tiempoDeSoltar - tiemposDeCuenta - tiemposDeVuelta, _segundoDeSoltar - 2f);
        if (finDelPlano - _segundoDeEsteIntento > 2f) camara.Presentar(reloj, finDelPlano);
        else camara.Huir(deGolpe: true);
    }

    protected override void Actualizar(float dt)
    {
        var jugador = Jugador;
        if (jugador == null) return;
        float ahora = reloj != null && reloj.IsRunning ? (float)reloj.SongTime : -1f;

        if (segundoDeGanar > 0f && ahora >= segundoDeGanar) { Ganar(); return; }
        // Pasado el golpe final ya no se falla: se cae al vacío con el suelo.
        if (segundoSinFallar > 0f && ahora >= segundoSinFallar) return;

        if (jugador.position.y < alturaDelFoso) { Fallar(); return; }

        if (SaludDelGrupo.EstaCaido(ControlDelJugador.Activo)) { Fallar(); return; }

        if (_corriendo)
        {
            bool sinControl = EnTramoSinControl(ahora);
            if (sinControl != _sinControl)
            {
                _sinControl = sinControl;
                if (sinControl) BloquearElControl();
                else SoltarElControl();
            }
        }

        if (meta != null && (jugador.position - meta.position).sqrMagnitude <= radioDeLaMeta * radioDeLaMeta)
        {
            Ganar();
            return;
        }

        if (!_corriendo) Contar();

        if (_corriendo)
        {
            // Lanzar un hechizo o cambiar de personaje frena al grupo: lo que se pierde por eso,
            // la sombra lo cede (no se acerca más que si se hubiera seguido corriendo).
            float avance = jugador.position.z - _zAntes;
            if (EstaOcupado() && dt > 0f)
                _cedido += Mathf.Max(0f, metrosPorSegundo * dt - Mathf.Max(0f, avance));
            if (reloj != null && reloj.IsRunning) AjustarElTope(jugador.position.z, (float)reloj.SongTime);
        }
        _zAntes = jugador.position.z;
        while (_puntoPasado + 1 < puntosDeControl.Length && puntosDeControl[_puntoPasado + 1] != null
               && jugador.position.z >= puntosDeControl[_puntoPasado + 1].position.z - 1f)
            _puntoPasado++;

        if (reloj != null && reloj.IsRunning)
        {
            float z = ColocarLaSombra((float)reloj.SongTime);
            // Grabando marcas a mano, la sombra no hace repetir.
            if (GrabadorDeMarcas.Grabando || !_corriendo) return;
            ActualizarSuelo(z, ahora);
            float distancia = jugador.position.z - z;
            float distanciaDeAviso = Mathf.Max(0.1f, metrosPorSegundo * SegundosDeAvisoDeSombra);
            float riesgo = 1f - Mathf.Clamp01(distancia / distanciaDeAviso);
            // El borde se nota al empezar a rezagarse y se aclara en cuanto se recupera terreno.
            _vineta.alpha = Mathf.MoveTowards(_vineta.alpha, 0.9f * Mathf.Sqrt(riesgo), dt * 2.5f);
            if (distancia < 0f) Fallar();
        }
    }

    private void ActualizarSuelo(float z, float segundo)
    {
        for (int i = _primeraFilaVisible; i < sueloQueSePierde.Length; i++)
        {
            Transform fila = sueloQueSePierde[i];
            Vector3 origen = _posicionesDelSuelo[i];
            float paso = z - fila.parent.TransformPoint(origen).z;
            if (_iniciosDeCaida[i] < 0f)
            {
                if (paso < -DistanciaDeAvisoDelSuelo) break;
                if (paso < 0f)
                {
                    float golpe = Mathf.Sin(segundo * 42f + i);
                    fila.localPosition = origen + Vector3.up * (0.035f * golpe * (1f + paso / DistanciaDeAvisoDelSuelo));
                    continue;
                }
                _iniciosDeCaida[i] = segundo;
                if (polvoDelSuelo != null && VfxPoolService.Instance != null)
                {
                    float lado = (i & 1) == 0 ? -1f : 1f;
                    VfxPoolService.Instance.Play(polvoDelSuelo, fila.position + new Vector3(lado * 3.3f, 0f, -0.2f), Quaternion.identity, 0.8f);
                }
            }
            float u = (segundo - _iniciosDeCaida[i]) / DuracionDeCaida;
            if (u >= 1f)
            {
                fila.gameObject.SetActive(false);
                _primeraFilaVisible = i + 1;
                continue;
            }
            fila.localPosition = origen + new Vector3(Mathf.Sin(i) * 0.15f * u, -8f * u * u, 0f);
        }
    }

    private void MontarAvisoDeSombra()
    {
        var aviso = new GameObject("Sombra cerca · viñeta", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        aviso.transform.SetParent(transform, false);
        var canvas = aviso.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;
        _vineta = aviso.GetComponent<CanvasGroup>();
        _vineta.alpha = 0f;
        _vineta.interactable = false;
        _vineta.blocksRaycasts = false;

        var capa = new GameObject("Bordes", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        capa.transform.SetParent(aviso.transform, false);
        var imagen = capa.GetComponent<UnityEngine.UI.RawImage>();
        _texturaDeLaVineta = NocheDeLaCaida.Vineta(128);
        imagen.texture = _texturaDeLaVineta;
        imagen.raycastTarget = false;
        var rect = imagen.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_texturaDeLaVineta != null) Destroy(_texturaDeLaVineta);
    }

    protected override void Detener()
    {
        if (reloj != null) reloj.StopPulse();
        SoltarLaMusica();
        DejarDeCorrer();
        // Al ganar, la cámara se queda para la despedida (la suelta la pregunta de repetir).
        if (camara != null && EstadoActual != Estado.Ganada) camara.Soltar();
        Kits.Core.GameContext.Exit(Situaciones.PruebaSinPausa, this);
        SombraDeContacto.SoltarRefuerzo(this);
    }

    /// <summary>«3, 2, 1» en los tiempos de antes de la salida y «¡Ya!» al soltar al grupo.</summary>
    private void Contar()
    {
        double t = reloj != null && reloj.IsRunning ? reloj.SongTime : double.MaxValue;
        while (_siguienteCuenta > 0 && t >= SegundoDelTiempo(_tiempoDeSoltar - _siguienteCuenta, _segundoDeSoltar))
        {
            HudToastService.Instance?.ShowRaw(_siguienteCuenta.ToString(), 0.38f);
            _siguienteCuenta--;
        }
        if (t < _segundoDeSoltar) return;
        HudToastService.Instance?.ShowRaw(PresentadorDeLinea.Texto("PRUEBA_YA", "¡Ya!"), 0.8f);
        _siguienteCuenta = 0;
        _corriendo = true;
        SoltarElControl();
        _topePuesto = -1f;
        _margenPuesto = float.NaN;
        if (reloj != null) AjustarElTope(Jugador != null ? Jugador.position.z : zDeArranque, (float)reloj.SongTime);
    }

    /// <summary>
    /// Se corre a la velocidad del recuadro; por detrás de su centro el tope sube con lo que se va
    /// perdiendo, para recuperar el centro con el sprint. Del centro en adelante no se corre más que la
    /// canción, para no adelantarse a los peligros (están puestos a su paso).
    /// </summary>
    private void AjustarElTope(float zDelJugador, float segundo)
    {
        float centro = CentroDelRecuadro(segundo);
        float tope = Mathf.Clamp(metrosPorSegundo + (centro - zDelJugador) * 1.5f, metrosPorSegundo,
                                 Mathf.Max(metrosPorSegundo, topeLibre));
        // El borde limita solo el avance: se puede cambiar de fila incluso pegado a él.
        bool parado = CentroDelRecuadro(segundo + 0.1f) - centro < 0.01f;
        if (parado) tope = metrosPorSegundo;
        bool conBorde = recuadro != null && (segundoSinTope <= 0f || segundo < segundoSinTope);
        float margen = conBorde ? (parado ? metrosAdelanteEnRespiro : metrosAdelante) : -1f;
        if (Mathf.Abs(tope - _topePuesto) < 0.1f && margen == _margenPuesto) return;
        _topePuesto = tope;
        _margenPuesto = margen;
        if (conBorde)
            TopeDeVelocidadDelJugador.PonerSeguimiento(this, tope, recuadro, Vector3.forward, 0f, -margen);
        else
        {
            TopeDeVelocidadDelJugador.QuitarSeguimiento(this);
            TopeDeVelocidadDelJugador.Poner(this, tope);
        }
    }

    /// <summary>Segundo de un tiempo de la partitura (o <paramref name="siNo"/> sin partitura).</summary>
    private float SegundoDelTiempo(int indice, float siNo)
    {
        var partitura = reloj != null ? reloj.Score : null;
        if (partitura == null || indice < 0 || _tiempoDeSoltar < 0) return siNo;
        return partitura.BeatTime(indice);
    }

    private void DejarDeCorrer()
    {
        _corriendo = false;
        if (_vineta != null) _vineta.alpha = 0f;
        TopeDeVelocidadDelJugador.Quitar(this);
    }

    private void SoltarLaMusica() => AudioService.Instance?.SoltarAtenuacionDeBus(ClaveDeLaMusica, AudioBus.Music);

    private bool EstaOcupado()
    {
        if (Time.time - _ultimoCambio < graciaDelCambio) return true;
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo != _cuerpoDelCaster)
        {
            _cuerpoDelCaster = cuerpo;
            _caster = cuerpo != null ? cuerpo.GetComponentInChildren<MagicCaster>() : null;
        }
        return _caster != null && _caster.IsCasting;
    }

    private bool EnTramoSinControl(float segundo)
    {
        for (int i = 0; i < tramosSinControl.Length; i++)
            if (segundo >= tramosSinControl[i].x && segundo < tramosSinControl[i].y) return true;
        return false;
    }

    /// <summary>
    /// Donde marca la canción en ese segundo: quieto en la salida hasta soltar al grupo, y quieto en
    /// el calderón los segundos que dure; después sigue desde ahí, con ese retraso.
    /// </summary>
    private float CentroDelRecuadro(float segundo) => ZEnSegundo(Mathf.Max(segundo, _segundoDeSoltar));

    /// <summary>
    /// Z del centro del recuadro en ese segundo: a la velocidad de la canción, parado en cada calderón
    /// y ganando metros en cada acelerón (calderones y acelerones, en el orden en que se llega a ellos).
    /// </summary>
    private float ZEnSegundo(float segundo)
    {
        float z = zDeArranque + metrosPorSegundo * (segundo - segundoDeArranque);
        int c = 0, a = 0;
        while (c < calderones.Length || a < acelerones.Length)
        {
            if (a >= acelerones.Length || (c < calderones.Length && calderones[c].x <= acelerones[a].x))
            {
                var k = calderones[c++];
                if (z <= k.x) return z;
                z = Mathf.Max(k.x, z - metrosPorSegundo * k.y);
            }
            else
            {
                var k = acelerones[a++];
                if (z <= k.x) return z;
                z += k.z * Mathf.Clamp01((z - k.x) / Mathf.Max(0.01f, metrosPorSegundo * k.y));
            }
        }
        return z;
    }

    /// <summary>Segundo de la canción en el que el centro del recuadro llega a esa Z (el primero, si se para en ella).</summary>
    private float SegundoEnZ(float z)
    {
        if (metrosPorSegundo <= 0f) return segundoDeArranque;
        float lo = segundoDeArranque - 120f, hi = segundoDeArranque + 1200f;
        for (int i = 0; i < 48; i++)
        {
            float m = (lo + hi) * 0.5f;
            if (ZEnSegundo(m) >= z) hi = m; else lo = m;
        }
        return hi;
    }

    /// <summary>
    /// Pone el recuadro donde marca la canción y la sombra en su borde de atrás (menos lo que haya
    /// cedido), y devuelve la Z de la sombra.
    /// </summary>
    private float ColocarLaSombra(float segundo)
    {
        float centro = CentroDelRecuadro(segundo);
        if (recuadro != null)
        {
            var r = recuadro.position;
            recuadro.position = new Vector3(r.x, r.y, centro);
        }
        float z = centro - metrosDeVentaja - metrosPorSegundo * SegundosDeMargenExtra - _cedido;
        if (segundo >= cierreTrasero.x && segundo < cierreTrasero.z)
        {
            float progreso = Mathf.InverseLerp(cierreTrasero.x, cierreTrasero.y, segundo);
            z = Mathf.Lerp(z, Mathf.Max(z, cierreTrasero.w), progreso);
        }
        if (sombra != null)
        {
            var p = sombra.position;
            sombra.position = new Vector3(p.x, p.y, z);
        }
        return z;
    }
}
