using UnityEngine;

/// <summary>
/// Embarcación navegable: flota sobre el agua y se conduce con el jugador a bordo.
///
/// La superficie del agua se busca en la capa "Water" (la misma que usa
/// <see cref="PlayerSwimmingController"/>), así que vale para cualquier masa de agua del juego.
///
/// La dirección de la proa se declara en <see cref="direccionDeProa"/> y no se supone que sea
/// +Z: los barcos del pack vienen con la proa en ejes distintos según el modelo, y el sistema
/// tiene que poder conducirlos igual. El rumbo (<see cref="_rumbo"/>) es hacia dónde apunta la
/// proa en el mundo; la rotación del transform se deduce de ahí.
///
/// El jugador a bordo lo gestiona <see cref="ConductorDeBarco"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ConductorDeBarco))]
public class BarcoNavegable : MonoBehaviour
{
    [Header("Proa")]
    [Tooltip("Dirección local de la proa del barco, en horizontal. Por defecto +Z.")]
    [SerializeField] private Vector3 direccionDeProa = Vector3.forward;

    [Header("Puestos")]
    [Tooltip("Punto donde se coloca el conductor. Debe ser hijo del barco: así se mueve con él.")]
    [SerializeField] private Transform puestoDeConduccion;
    [Tooltip("Punto donde baja el jugador al desembarcar. Si está vacío, baja junto al casco.")]
    [SerializeField] private Transform puntoDeDesembarque;
    [Tooltip("Radio (m) dentro del cual se busca el punto de amarre donde se puede bajar.")]
    [SerializeField] private float radioDeAmarre = 30f;

    [Header("Flotación")]
    [Tooltip("Metros que queda la parte más baja del casco por debajo de la superficie del agua.")]
    [SerializeField, Min(0f)] private float profundidadDeQuilla = 0.4f;
    [Tooltip("Amplitud del vaivén vertical (m).")]
    [SerializeField] private float amplitudDeVaiven = 0.05f;
    [Tooltip("Vueltas por segundo del vaivén vertical.")]
    [SerializeField] private float frecuenciaDeVaiven = 0.35f;
    [Tooltip("Grados de balanceo al virar.")]
    [SerializeField] private float balanceoPorGiro = 6f;
    [Tooltip("Grados de cabeceo al acelerar.")]
    [SerializeField] private float cabeceoPorAceleracion = 3f;
    [Tooltip("Altura de la sonda para buscar la superficie del agua (m).")]
    [SerializeField, Min(1f)] private float alturaDeSonda = 4f;
    [Tooltip("Suavizado del ajuste de flotación (1/s). Cuanto más alto, más rápido se asienta.")]
    [SerializeField] private float suavidadDeFlotacion = 8f;

    [Header("Conducción")]
    [Tooltip("Velocidad máxima hacia delante (m/s).")]
    [SerializeField] private float velocidadMaxima = 6f;
    [Tooltip("Velocidad máxima hacia atrás (m/s).")]
    [SerializeField] private float velocidadMaximaAtras = 2.5f;
    [Tooltip("Aceleración (m/s²).")]
    [SerializeField] private float aceleracion = 3f;
    [Tooltip("Frenada al soltar el stick (m/s²).")]
    [SerializeField] private float frenado = 2f;
    [Tooltip("Velocidad de giro (grados por segundo) con el stick al máximo.")]
    [SerializeField] private float velocidadDeGiro = 50f;
    [Tooltip("Distancia a la que el barco frena si tiene algo delante (m).")]
    [SerializeField, Min(0.1f)] private float distanciaDeFreno = 1.5f;

    [Header("Entrada")]
    [Tooltip("Conduce solo con el timón si el stick está quieto (el barco puede girar en el sitio).")]
    [SerializeField] private float giroEnElSitio = 0.35f;

    [Header("Botones del barco")]
    [Tooltip("Segundos que el botón de acelerar sube la velocidad máxima del barco.")]
    [SerializeField] private float factorDeAceleracion = 1.6f;

    [Header("Efectos")]
    [Tooltip("Segundos entre ondas de la estela (a velocidad normal).")]
    [SerializeField] private float intervaloDeEstela = 0.45f;
    [Tooltip("Gotas que salpican en la proa por segundo a máxima velocidad.")]
    [SerializeField] private float gotasPorSegundo = 14f;

    [Header("Música")]
    [Tooltip("Clip que suena mientras el jugador va a bordo. Vacío = sigue la música del lugar.")]
    [SerializeField] private AudioClip musicaDelBarco;
    [SerializeField] private float fundidoDeMusica = 1.5f;

    private ConductorDeBarco _conductor;
    private TripulacionDelBarco _tripulacion;
    private Interactable _interactable;
    private PuntoDeAmarre _amarre;
    private ControlesDelBarcoHUD _controlesDelHud;
    private bool _suscrito;
    private PlayerControls _controles;
    private bool _poseeControles;
    private int _mascaraDeAgua;
    private int _mascaraDeSolidos;

    private readonly RaycastHit[] _hitsDeAgua = new RaycastHit[8];
    private readonly RaycastHit[] _hitsDeFreno = new RaycastHit[8];

    private Vector3 _proaLocal;
    private Quaternion _correccionDeProa;
    private float _alturaDeQuilla;
    private float _distanciaAProa;
    private float _distanciaAPopa;
    private float _rumbo;
    private float _velocidad;
    private float _balanceo;
    private float _cabeceo;
    private float _alturaActual;
    private bool _alturaIniciada;
    private float _proximaEstela;
    private SalpicaduraDeAgua _salpicadura;

    /// <summary>True mientras hay alguien a bordo.</summary>
    public bool TieneConductor => _conductor != null && _conductor.TieneConductor;

    /// <summary>Velocidad actual en m/s (negativa si va hacia atrás).</summary>
    public float Velocidad => _velocidad;

    /// <summary>Rumbo en grados: hacia dónde apunta la proa.</summary>
    public float Rumbo => _rumbo;

    /// <summary>Dirección de la proa en el mundo (horizontal).</summary>
    public Vector3 DireccionDeProa => ProaMundo();

    /// <summary>Clip que suena mientras el jugador va a bordo (puede ser null).</summary>
    public AudioClip MusicaDelBarco => musicaDelBarco;

    /// <summary>Segundos de fundido al cambiar de música al subir o bajar del barco.</summary>
    public float FundidoDeMusica => fundidoDeMusica;

    /// <summary>
    /// Rotación local que hay que darle al conductor para que mire hacia la proa (la proa no
    /// está en el +Z local del modelo).
    /// </summary>
    public Quaternion RotacionLocalDeProa => Quaternion.FromToRotation(Vector3.forward, _proaLocal);

    void Awake()
    {
        _conductor = GetComponent<ConductorDeBarco>();
        _tripulacion = GetComponent<TripulacionDelBarco>();

        _mascaraDeAgua = LayerMask.GetMask("Water");
        _mascaraDeSolidos = Physics.DefaultRaycastLayers & ~_mascaraDeAgua;

        _controles = Core.PlayerInputManager.GetSharedOrNew(out _poseeControles);
    }

    void Start()
    {
        // La proa, la quilla y el rumbo se calculan en Start y no en Awake: en el Editor, cuando
        // el generador añade el componente, el Awake se ejecuta antes de que se le asignen los
        // campos, y ahí saldrían mal.
        _proaLocal = direccionDeProa;
        _proaLocal.y = 0f;
        if (_proaLocal.sqrMagnitude < 0.0001f) _proaLocal = Vector3.forward;
        _proaLocal.Normalize();
        _correccionDeProa = Quaternion.FromToRotation(_proaLocal, Vector3.forward);

        CalcularQuilla();

        // Salpicaduras: el mismo emisor que usa el nado (ondas, gotas y burbujas), sin instanciar
        // ni destruir nada.
        var vfx = new GameObject("Salpicaduras");
        vfx.transform.SetParent(transform, false);
        _salpicadura = vfx.AddComponent<SalpicaduraDeAgua>();

        // El rumbo inicial es el que tiene el barco al aparecer, no cero: si el barco ya
        // estaba girado en la escena, el timón sigue girando hacia el mismo lado.
        Vector3 proaMundo = transform.TransformDirection(_proaLocal);
        proaMundo.y = 0f;
        if (proaMundo.sqrMagnitude > 0.0001f)
            _rumbo = Mathf.Atan2(proaMundo.x, proaMundo.z) * Mathf.Rad2Deg;

        // El Interactable puede haberse añadido después que este componente (en el generador
        // del LAB, por ejemplo), así que se busca otra vez aquí por si acaso.
        IntentarSuscribir();
    }

    void OnEnable()
    {
        if (_poseeControles) _controles?.Enable();
        IntentarSuscribir();
    }

    void OnDisable()
    {
        if (_interactable != null) _interactable.OnInteract.RemoveListener(HandleInteract);
        _suscrito = false;
        if (_poseeControles) _controles?.Disable();
        // El jugador no puede quedarse colgado de un barco que desaparece.
        if (TieneConductor) Desembarcar();
    }

    /// <summary>Se engancha al Interactable del barco para poder embarcar y desembarcar.</summary>
    void IntentarSuscribir()
    {
        if (_suscrito) return;
        if (_interactable == null) _interactable = GetComponentInChildren<Interactable>();
        if (_interactable == null) return;
        _interactable.OnInteract.AddListener(HandleInteract);
        _suscrito = true;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        Vector2 entrada = Vector2.zero;
        bool aBordo = TieneConductor;
        if (aBordo) entrada = LeerEntrada();

        if (aBordo) BotonesDelBarco();

        Conducir(entrada, dt, aBordo);
        Flotar(dt);
        EfectosDeNavegacion(dt);
    }

    /// <summary>
    /// Los botones del barco mientras se navega: <b>Y</b> (Q en teclado) para bajar y
    /// <b>LT</b> para acelerar. El Y es el mismo botón que el ataque mágico especial, que con
    /// el modo Boating está bloqueado, así que queda libre para bajar.
    /// </summary>
    void BotonesDelBarco()
    {
        if (_controles == null) return;

        if (_controles.GamePlay.AttackMagicNorth.WasPressedThisFrame())
        {
            Desembarcar();
            return;
        }

        _acelerando = _controles.GamePlay.LT.IsPressed();
    }

    private bool _acelerando;

    // ── Efectos ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Estela y spray: el barco deja ondas abiertas detrás de la popa y salpica por la proa
    /// cuando va deprisa. Usa el mismo emisor de agua que el nado.
    /// </summary>
    void EfectosDeNavegacion(float dt)
    {
        if (_salpicadura == null) return;

        float rapidez = Mathf.Abs(_velocidad);
        if (rapidez < 0.4f) return;
        if (!TryGetSuperficieDelAgua(out float superficie)) return;

        float relativa = Mathf.Clamp01(rapidez / Mathf.Max(0.5f, velocidadMaxima));

        // Estela: anillos que se abren en la superficie detrás de la popa.
        if (Time.time >= _proximaEstela)
        {
            _proximaEstela = Time.time + intervaloDeEstela / Mathf.Max(0.35f, relativa);
            Vector3 popa = transform.position - ProaMundo() * _distanciaAPopa;
            _salpicadura.Onda(popa, Mathf.Lerp(0.9f, 2.6f, relativa), 1.1f);
        }

        // Spray en la proa.
        int gotas = Mathf.RoundToInt(gotasPorSegundo * dt * relativa);
        if (gotas > 0)
        {
            Vector3 proa = transform.position + ProaMundo() * _distanciaAProa;
            _salpicadura.Gotitas(proa, gotas);
        }
    }

    /// <summary>Salpicadura grande en la proa (choque contra el fondo o el muro).</summary>
    void SalpicarEnLaProa(float fuerza)
    {
        if (_salpicadura == null) return;
        if (!TryGetSuperficieDelAgua(out float superficie)) return;
        Vector3 proa = transform.position + ProaMundo() * _distanciaAProa;
        _salpicadura.Salpicar(new Vector3(proa.x, superficie, proa.z), fuerza);
    }

    // ── Conducción ────────────────────────────────────────────────────────────

    void Conducir(Vector2 entrada, float dt, bool aBordo)
    {
        float gas = Mathf.Clamp(entrada.y, -1f, 1f);
        float timon = Mathf.Clamp(entrada.x, -1f, 1f);

        // Timón: gira más cuanto más gas hay; en el sitio gira poco.
        float factor = Mathf.Lerp(giroEnElSitio, 1f, Mathf.Abs(gas));
        _rumbo += timon * velocidadDeGiro * factor * dt;

        // Velocidad: acelera hacia el gas pedido o frena si no hay stick.
        float tope = aBordo && _acelerando ? velocidadMaxima * factorDeAceleracion : velocidadMaxima;
        float objetivo = gas >= 0f ? gas * tope : gas * velocidadMaximaAtras;
        // El barco espera a que suba la tripulación antes de zarpar.
        if (_tripulacion != null && _tripulacion.Subiendo) objetivo = 0f;
        if (Mathf.Abs(gas) > 0.05f && Mathf.Abs(objetivo) > 0.01f)
            _velocidad = Mathf.MoveTowards(_velocidad, objetivo, aceleracion * dt);
        else
            _velocidad = Mathf.MoveTowards(_velocidad, 0f, frenado * dt);

        // Avance.
        Vector3 proa = ProaMundo();
        float avance = _velocidad * dt;
        if (Mathf.Abs(avance) > 0.0001f)
        {
            if (HayObsticuloAdelante())
            {
                // Choque contra el fondo o el muro: salpica por la proa.
                if (Mathf.Abs(_velocidad) > 1.5f) SalpicarEnLaProa(0.7f);
                _velocidad = 0f;
            }
            else
            {
                transform.position += proa * avance;
            }
        }

        // Balanceo y cabeceo: el barco se inclina al virar y al acelerar.
        float balanceoObjetivo = -timon * balanceoPorGiro;
        float cabeceoObjetivo = -Mathf.Clamp(_velocidad / Mathf.Max(0.1f, velocidadMaxima), -1f, 1f) * cabeceoPorAceleracion;
        _balanceo = Mathf.Lerp(_balanceo, balanceoObjetivo, 1f - Mathf.Exp(-6f * dt));
        _cabeceo = Mathf.Lerp(_cabeceo, cabeceoObjetivo, 1f - Mathf.Exp(-4f * dt));

        AplicarRotacion();
    }

    void AplicarRotacion()
    {
        Quaternion rumbo = Quaternion.Euler(0f, _rumbo, 0f);
        Vector3 ejeTransversal = Vector3.Cross(Vector3.up, _proaLocal).normalized;
        if (ejeTransversal.sqrMagnitude < 0.0001f) ejeTransversal = Vector3.right;

        transform.rotation = rumbo * _correccionDeProa
                             * Quaternion.AngleAxis(_balanceo, _proaLocal)
                             * Quaternion.AngleAxis(_cabeceo, ejeTransversal);
    }

    /// <summary>
    /// Dirección de la proa en el mundo. El rumbo es el yaw de la proa: la rotación del transform
    /// lleva la proa local (que puede no ser +Z) a esa dirección.
    /// </summary>
    Vector3 ProaMundo()
    {
        Vector3 proa = Quaternion.Euler(0f, _rumbo, 0f) * Vector3.forward;
        proa.y = 0f;
        return proa.sqrMagnitude > 0.0001f ? proa.normalized : transform.forward;
    }

    Vector2 LeerEntrada()
    {
        if (_controles == null) return Vector2.zero;
        Vector2 mov = _controles.GamePlay.Move.ReadValue<Vector2>();
        if (mov.sqrMagnitude > 1f) mov.Normalize();
        return mov;
    }

    /// <summary>El barco frena si tiene algo sólido delante (la proa contra un muro).</summary>
    bool HayObsticuloAdelante()
    {
        if (_mascaraDeSolidos == 0) return false;

        // La sonda sale de la proa, no del centro: el pivote del modelo no está en el centro del
        // casco, y desde el centro el barco se colaría en el muro antes de frenar.
        Vector3 origen = transform.position + ProaMundo() * _distanciaAProa + Vector3.up * 0.5f;
        Vector3 proa = ProaMundo();
        int hits = Physics.RaycastNonAlloc(origen, proa, _hitsDeFreno, distanciaDeFreno, _mascaraDeSolidos,
                                          QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; ++i)
        {
            var col = _hitsDeFreno[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue; // el propio barco
            return true;
        }
        return false;
    }

    // ── Flotación ─────────────────────────────────────────────────────────────

    void Flotar(float dt)
    {
        if (TryGetSuperficieDelAgua(out float superficie))
        {
            float vaiven = Mathf.Sin(Time.time * frecuenciaDeVaiven * Mathf.PI * 2f) * amplitudDeVaiven;
            float objetivo = superficie - _alturaDeQuilla - profundidadDeQuilla + vaiven;
            if (!_alturaIniciada)
            {
                _alturaActual = objetivo;
                _alturaIniciada = true;
            }
            else
            {
                _alturaActual = Mathf.Lerp(_alturaActual, objetivo, 1f - Mathf.Exp(-suavidadDeFlotacion * dt));
            }
        }

        var pos = transform.position;
        pos.y = _alturaIniciada ? _alturaActual : pos.y;
        transform.position = pos;
    }

    /// <summary>Altura de la superficie del agua justo debajo del barco.</summary>
    public bool TryGetSuperficieDelAgua(out float altura)
    {
        altura = 0f;
        if (_mascaraDeAgua == 0) return false;

        Vector3 origen = transform.position + Vector3.up * alturaDeSonda;
        int hits = Physics.RaycastNonAlloc(origen, Vector3.down, _hitsDeAgua, alturaDeSonda * 2f, _mascaraDeAgua,
                                          QueryTriggerInteraction.Collide);
        bool encontrado = false;
        float mejor = float.MinValue;
        for (int i = 0; i < hits; ++i)
        {
            var hit = _hitsDeAgua[i];
            if (hit.collider == null) continue;
            if (!encontrado || hit.point.y > mejor)
            {
                mejor = hit.point.y;
                encontrado = true;
            }
        }

        altura = mejor;
        return encontrado;
    }

    /// <summary>Altura local de la parte más baja del casco (para saber dónde va la línea de agua).</summary>
    void CalcularQuilla()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            _alturaDeQuilla = 0f;
            return;
        }

        Bounds local = new Bounds(transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        bool primero = true;
        foreach (var r in renderers)
        {
            Bounds b = r.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;
            Vector3[] esquinas =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (var c in esquinas)
            {
                Vector3 localc = transform.InverseTransformPoint(c);
                if (primero)
                {
                    local = new Bounds(localc, Vector3.zero);
                    primero = false;
                }
                else
                {
                    local.Encapsulate(localc);
                }
            }
        }

        _alturaDeQuilla = local.min.y;
        // Distancia del pivote del modelo hasta la proa y hasta la popa (a lo largo de la eslora):
        // el pivote de estos modelos no está en el centro del casco.
        _distanciaAProa = Mathf.Max(0f, Vector3.Dot(local.max, _proaLocal));
        _distanciaAPopa = Mathf.Max(0f, -Vector3.Dot(local.min, _proaLocal));
    }

    // ── Embarque y desembarque ────────────────────────────────────────────────

    void HandleInteract(GameObject interactor)
    {
        AlternarEmbarque(interactor);
    }

    /// <summary>
    /// Deja el barco configurado (lo llaman el generador del LAB y el del mundo). Se hace por
    /// asignación directa y no por SerializedObject para que el valor quede guardado en la
    /// escena aunque el componente se acabe de añadir a una instancia de prefab.
    /// </summary>
    public void Configurar(Vector3 proa, Transform puesto, Transform desembarque,
                            float profundidad, AudioClip musica)
    {
        direccionDeProa = proa;
        puestoDeConduccion = puesto;
        puntoDeDesembarque = desembarque;
        profundidadDeQuilla = profundidad;
        musicaDelBarco = musica;
    }

    /// <summary>
    /// El punto donde el barco se arrima. Al subir el jugador, el punto de amarre más cercano
    /// enciende su icono para poder bajar a tierra firme.
    /// </summary>
    public void ConfigurarAmarre(PuntoDeAmarre amarre)
    {
        _amarre = amarre;
    }

    /// <summary>Sube al barco o baja de él, según esté o no ocupado.</summary>
    public void AlternarEmbarque(GameObject interactor)
    {
        if (interactor == null) return;

        if (TieneConductor)
        {
            Desembarcar();
            return;
        }

        Embarcar(interactor);
    }

    /// <summary>Coloca al jugador en el puesto de conducción.</summary>
    public bool Embarcar(GameObject jugador)
    {
        if (jugador == null || TieneConductor) return false;
        if (puestoDeConduccion == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[BarcoNavegable] No hay puesto de conducción asignado: no se puede embarcar.", this);
#endif
            return false;
        }

        if (!_conductor.Embarcar(jugador, puestoDeConduccion)) return false;

        // El HUD: en vez de la cruz de combate, los botones del barco (bajar, acelerar).
        _controlesDelHud = ControlesDelBarcoHUD.Mostrar(jugador);

        // El punto de amarre más cercano enciende su icono: desde ahí se baja a tierra firme.
        BuscarPuntoDeAmarre();

        // Los compañeros también suben, cada uno a su sitio y a su manera.
        _tripulacion?.SubirATripulacion();
        return true;
    }

    /// <summary>Baja al jugador del barco (al punto de amarre, si lo hay).</summary>
    public void Desembarcar()
    {
        if (!TieneConductor) return;

        Vector3 destino = _amarre != null
            ? _amarre.transform.position
            : (puntoDeDesembarque != null ? puntoDeDesembarque.position : transform.position + transform.right * 2f);

        _conductor.Desembarcar(destino);
        _tripulacion?.BajarATripulacion();

        // El HUD vuelve a la cruz de combate.
        if (_controlesDelHud != null)
        {
            _controlesDelHud.Ocultar();
            _controlesDelHud = null;
        }

        // Ya no hay nadie a bordo: el punto de amarre apaga su icono.
        _amarre?.Desactivar();
        _amarre = null;
    }

    /// <summary>Busca el punto de amarre más cercano y enciende su icono.</summary>
    void BuscarPuntoDeAmarre()
    {
        _amarre = PuntoDeAmarre.MasCerca(transform.position, radioDeAmarre);
        _amarre?.Activar(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (puestoDeConduccion != null)
        {
            Gizmos.DrawWireSphere(puestoDeConduccion.position, 0.3f);
            Gizmos.DrawLine(puestoDeConduccion.position, puestoDeConduccion.position + Vector3.up * 0.5f);
        }
        if (puntoDeDesembarque != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(puntoDeDesembarque.position, 0.3f);
        }
    }
#endif
}
