using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Controlador del nivel jugable de huida del Sendero de las Estrellas mientras colapsa.
/// Will abre camino por delante resolviendo fracturas y barreras para que Estela avance
/// caminando con Liam en brazos, mientras un vórtice de colapso cósmico los persigue desde atrás.
///
/// Mientras está activo, el nivel prohíbe volar (bloqueo de PlayerActionManager, sin tocar las
/// habilidades de la partida) y oculta el HUD y el minimapa con su clave; todo se libera en
/// OnDisable. Las habilidades que pide (saltar, trepar, magia) vienen de la partida. Los textos
/// son claves de localización (ESCAPE_SENDERO_*): los bocadillos pasan por Bocadillos.Decir, que
/// traduce y busca la voz.
/// </summary>
public class NivelEscapeSendero : MonoBehaviour
{
    [Header("Actores")]
    [SerializeField] private Transform jugador;
    [SerializeField] private PorteoDeCompanero porteoEstela;

    [Header("Avance de Estela")]
    [Tooltip("Puntos de descanso por los que Estela avanza caminando con Liam.")]
    [SerializeField] private Transform[] puntosSegurosEstela;
    [SerializeField] private float velocidadPasoEstela = 2.8f;

    [Header("Checkpoints de Seguridad")]
    [Tooltip("Posiciones seguras para devolver a Will si cae al vacío.")]
    [SerializeField] private Transform[] checkpointsWill;
    [SerializeField] private float alturaCaidaVacio = -8f;

    [Header("Muro de Colapso Persecutor")]
    [Tooltip("Entidad cósmica que avanza desde el Altar hacia el Umbral metiendo presión.")]
    [SerializeField] private Transform muroColapso;
    [SerializeField] private float velocidadColapsoBase = 1.35f;
    [SerializeField] private float velocidadColapsoOleada = 3.2f;

    [Header("Obstáculos a Abrir por Will")]
    [Tooltip("Barrera de cristales celestes en la calzada (Z=220).")]
    [SerializeField] private GameObject barreraCristal;

    [Tooltip("Barrera de obsidiana que bloquea el arco del camino (Z=142).")]
    [SerializeField] private GameObject barreraObsidiana;

    [Tooltip("Sello mágico del Umbral que bloquea la rampa final (Z=60).")]
    [SerializeField] private GameObject selloUmbral;

    [Header("Eventos")]
    public UnityEvent alIniciarEscape = new UnityEvent();
    public UnityEvent alSuperarObstaculo1 = new UnityEvent();
    public UnityEvent alSuperarObstaculo2 = new UnityEvent();
    public UnityEvent alSuperarObstaculo3 = new UnityEvent();
    public UnityEvent alLlegarAlUmbral = new UnityEvent();

    // Propiedades de configuración para el builder
    public Transform MuroColapso { get => muroColapso; set => muroColapso = value; }
    public GameObject BarreraCristal { get => barreraCristal; set => barreraCristal = value; }
    public GameObject BarreraObsidiana { get => barreraObsidiana; set => barreraObsidiana = value; }
    public GameObject SelloUmbral { get => selloUmbral; set => selloUmbral = value; }

    private int _seccionActual = 0;
    private bool _enRespawn;
    private bool _escapeFinalizado;
    private bool _enOleadaColapso;
    private float _siguienteAvisoColapso;
    private float _tiempoProximoTemblor = 6f;

    private bool _obstaculo1Resuelto;
    private bool _obstaculo2Resuelto;
    private bool _obstaculo3Resuelto;

    private PlayerActionManager _accionesBloqueadas;
    private bool _interfazOculta;
    private bool _empezado;

    private Animator _animatorEstela;
    private NPCSimpleAnimator _npcAnimEstela;
    private Coroutine _movimientoEstela;

    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashGroundDistance = Animator.StringToHash("GroundDistance");

    private void Awake()
    {
        DesactivarInteractuables();
    }

    private void DesactivarInteractuables()
    {
        if (porteoEstela != null)
        {
            foreach (var it in porteoEstela.GetComponentsInChildren<Interactable>(true))
            {
                it.SetHintVisible(false);
                it.EnableInteraction(false);
                it.enabled = false;
            }
            if (porteoEstela.Cuerpo != null)
            {
                foreach (var it in porteoEstela.Cuerpo.GetComponentsInChildren<Interactable>(true))
                {
                    it.SetHintVisible(false);
                    it.EnableInteraction(false);
                    it.enabled = false;
                }
            }
        }
    }

    private void OnEnable()
    {
        // La primera vez lo prepara Start, cuando el jugador y el HUD ya están registrados.
        if (_empezado) PrepararJugadorEInterfaz();
    }

    private void OnDisable() => LiberarJugadorEInterfaz();

    private void Start()
    {
        DesactivarInteractuables();

        if (jugador == null && PlayerService.Player != null)
            jugador = PlayerService.Player.transform;

        PrepararJugadorEInterfaz();
        _empezado = true;

        // Iniciar con Estela llevando a Liam en brazos
        if (porteoEstela != null)
        {
            if (!porteoEstela.EstaCargando)
                porteoEstela.CargarDirecto();

            _animatorEstela = porteoEstela.GetComponentInChildren<Animator>(true);
            _npcAnimEstela = porteoEstela.GetComponent<NPCSimpleAnimator>();

            InmovilizarLiam();
        }

        // Estado inicial de los obstáculos del escenario
        if (barreraCristal != null) barreraCristal.SetActive(true);
        if (barreraObsidiana != null) barreraObsidiana.SetActive(true);
        if (selloUmbral != null) selloUmbral.SetActive(true);

        alIniciarEscape?.Invoke();
        StartCoroutine(Co_SecuenciaInicial());
    }

    // Plataformeo atlético sin vuelo (si entra volando, cae) e interfaz limpia para la huida.
    private void PrepararJugadorEInterfaz()
    {
        if (_accionesBloqueadas == null && PlayerService.TryGetComponent(out PlayerActionManager acciones, allowSceneLookup: false))
        {
            _accionesBloqueadas = acciones;
            acciones.AnadirBloqueo(PlayerAbility.Fly, this);
            var vuelo = acciones.GetComponentInChildren<PlayerFlyingController>();
            if (vuelo != null && vuelo.IsFlying) vuelo.TerminarVuelo();
        }

        if (!_interfazOculta)
        {
            _interfazOculta = true;
            PlayerHUDV2.Instance?.HideHUD(this);
            MinimapController.Ocultar(this);
        }
    }

    private void LiberarJugadorEInterfaz()
    {
        if (_accionesBloqueadas != null)
        {
            _accionesBloqueadas.QuitarBloqueo(PlayerAbility.Fly, this);
            _accionesBloqueadas = null;
        }

        if (_interfazOculta)
        {
            _interfazOculta = false;
            PlayerHUDV2.Instance?.ShowHUD(this);
            MinimapController.Mostrar(this);
        }
    }

    private void InmovilizarLiam()
    {
        if (porteoEstela != null && porteoEstela.Cuerpo != null)
        {
            var anims = porteoEstela.Cuerpo.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] != null)
                {
                    anims[i].enabled = true;
                    anims[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anims[i].Play("Sleeping_NoWeapon", 0, 0f);
                    anims[i].speed = 0f;
                }
            }
        }
    }

    private IEnumerator Co_SecuenciaInicial()
    {
        yield return new WaitForSeconds(0.6f);
        InmovilizarLiam();

        MostrarAyuda("ESCAPE_SENDERO_AYUDA_INICIO");
        DecirEstela("ESCAPE_SENDERO_ESTELA_INICIO", 4.0f);

        // Estela comienza a andar hacia el primer punto de espera (frente a la barrera de cristales)
        yield return new WaitForSeconds(1.2f);
        AvanzarEstelaHaciaSeccion(0);
    }

    private void Update()
    {
        if (_escapeFinalizado || _enRespawn) return;

        // Detección de caída al abismo
        if (jugador != null && jugador.position.y < alturaCaidaVacio)
        {
            StartCoroutine(Co_RespawnJugador());
            return;
        }

        // Temblores ambientales del colapso del Sendero
        _tiempoProximoTemblor -= Time.deltaTime;
        if (_tiempoProximoTemblor <= 0f)
        {
            _tiempoProximoTemblor = Random.Range(6.5f, 9.5f);
            FeedbackService.CameraShake(0.22f, 0.45f);
        }

        // Actualizar el muro de colapso cósmico que persigue
        ActualizarMuroColapso();
    }

    private void ActualizarMuroColapso()
    {
        if (muroColapso == null) return;

        // Cota Z máxima según los obstáculos resueltos para no empujar injustamente al jugador
        float topeZ = 246f;
        if (_obstaculo1Resuelto) topeZ = 170f;
        if (_obstaculo2Resuelto) topeZ = 85f;
        if (_obstaculo3Resuelto) topeZ = 12f;

        if (muroColapso.position.z > topeZ)
        {
            float vel = _enOleadaColapso ? velocidadColapsoOleada : velocidadColapsoBase;
            muroColapso.position += Vector3.back * (vel * Time.deltaTime);

            if (muroColapso.position.z <= topeZ + 1.5f)
            {
                _enOleadaColapso = false;
            }
        }

        if (jugador != null)
        {
            float distZ = muroColapso.position.z - jugador.position.z;

            // Alerta de colapso cercano (< 8.5 metros)
            if (distZ < 8.5f && distZ > 1.2f)
            {
                if (Time.time > _siguienteAvisoColapso)
                {
                    _siguienteAvisoColapso = Time.time + 6.0f;
                    FeedbackService.CameraShake(0.35f, 0.6f);
                    FeedbackService.ScreenFlash(new Color(0.22f, 0.04f, 0.35f, 0.55f), 0.5f);
                    DecirEstela("ESCAPE_SENDERO_ESTELA_COLAPSO_CERCA", 3.0f);
                }
            }
            // El colapso alcanza al jugador
            else if (distZ <= 1.2f && !_enRespawn)
            {
                FeedbackService.ScreenFlash(new Color(0.35f, 0.02f, 0.15f, 0.8f), 0.7f);
                StartCoroutine(Co_RespawnJugador());
            }
        }
    }

    private void AvanzarEstelaHaciaSeccion(int indicePunto)
    {
        if (puntosSegurosEstela == null || indicePunto >= puntosSegurosEstela.Length) return;
        Transform destino = puntosSegurosEstela[indicePunto];
        if (destino == null) return;

        MoverEstelaHacia(destino, () =>
        {
            OnEstelaLlegaAPunto(indicePunto);
        });
    }

    private void OnEstelaLlegaAPunto(int indicePunto)
    {
        switch (indicePunto)
        {
            case 0: // Llegada frente a la barrera de cristales (Z=227)
                if (!_obstaculo1Resuelto)
                {
                    DecirEstela("ESCAPE_SENDERO_ESTELA_CRISTALES", 4.2f);
                    MostrarAyuda("ESCAPE_SENDERO_AYUDA_CRISTALES");
                }
                else
                {
                    AvanzarEstelaHaciaSeccion(1);
                }
                break;

            case 1: // Llegada frente al desfiladero de obsidiana (Z=148)
                if (!_obstaculo2Resuelto)
                {
                    DecirEstela("ESCAPE_SENDERO_ESTELA_OBSIDIANA", 4.2f);
                    MostrarAyuda("ESCAPE_SENDERO_AYUDA_OBSIDIANA");
                }
                else
                {
                    AvanzarEstelaHaciaSeccion(2);
                }
                break;

            case 2: // Llegada al Santuario de las Memorias (Z=64)
                if (!_obstaculo3Resuelto)
                {
                    DecirEstela("ESCAPE_SENDERO_ESTELA_SELLO", 4.0f);
                    MostrarAyuda("ESCAPE_SENDERO_AYUDA_SELLO");
                }
                else
                {
                    AvanzarEstelaHaciaSeccion(3);
                }
                break;

            case 3: // Llegada a la antesala del Umbral (Z=-4)
                DecirEstela("ESCAPE_SENDERO_ESTELA_ANTESALA", 3.5f);
                break;
        }
    }

    /// <summary>
    /// Will destruye la barrera de cristales en la calzada.
    /// </summary>
    public void ResolverObstaculo1()
    {
        if (_obstaculo1Resuelto) return;
        _obstaculo1Resuelto = true;
        _seccionActual = 1;
        _enOleadaColapso = true;

        if (barreraCristal != null)
            barreraCristal.SetActive(false);

        FeedbackService.CameraShake(0.45f, 0.7f);
        FeedbackService.ScreenFlash(new Color(1f, 0.85f, 0.4f, 0.5f), 0.4f);
        TutorialPromptUI.Instance?.Hide();

        DecirEstela("ESCAPE_SENDERO_ESTELA_CRISTALES_ROTOS", 3.5f);

        alSuperarObstaculo1?.Invoke();
        AvanzarEstelaHaciaSeccion(1);
    }

    /// <summary>
    /// Will destruye con magia la barrera de obsidiana en el desfiladero.
    /// </summary>
    public void ResolverObstaculo2()
    {
        if (_obstaculo2Resuelto) return;
        _obstaculo2Resuelto = true;
        _seccionActual = 2;
        _enOleadaColapso = true;

        if (barreraObsidiana != null)
            barreraObsidiana.SetActive(false);

        FeedbackService.CameraShake(0.5f, 0.75f);
        FeedbackService.ScreenFlash(new Color(0.8f, 0.9f, 1f, 0.5f), 0.35f);
        TutorialPromptUI.Instance?.Hide();

        DecirEstela("ESCAPE_SENDERO_ESTELA_OBSIDIANA_ROTA", 3.5f);

        alSuperarObstaculo2?.Invoke();
        AvanzarEstelaHaciaSeccion(2);
    }

    /// <summary>
    /// Will golpea el Megalito en el santuario: el sello del Umbral se abre.
    /// </summary>
    public void ResolverObstaculo3()
    {
        if (_obstaculo3Resuelto) return;
        _obstaculo3Resuelto = true;
        _seccionActual = 3;
        _enOleadaColapso = true;

        if (selloUmbral != null)
            selloUmbral.SetActive(false);

        FeedbackService.CameraShake(0.55f, 0.8f);
        FeedbackService.ScreenFlash(new Color(0.6f, 1f, 0.8f, 0.5f), 0.4f);
        TutorialPromptUI.Instance?.Hide();

        DecirEstela("ESCAPE_SENDERO_ESTELA_SELLO_ABIERTO", 3.5f);

        alSuperarObstaculo3?.Invoke();
        AvanzarEstelaHaciaSeccion(3);
    }

    /// <summary>
    /// Cruzar la meta del Umbral de la Tierra.
    /// </summary>
    public void LlegarAlUmbral()
    {
        if (_escapeFinalizado) return;
        _escapeFinalizado = true;

        FeedbackService.ScreenFlash(Color.white, 2.0f);
        FeedbackService.CameraShake(0.7f, 1.2f);
        TutorialPromptUI.Instance?.Hide();
        if (jugador != null) Bocadillos.Decir(jugador, "ESCAPE_SENDERO_WILL_SALIMOS", "Will", 4f);
        alLlegarAlUmbral?.Invoke();

        DefaultNarrativeSignals.Instance?.RaiseCustom("SENDERO_UMBRAL_ALCANZADO", name);
    }

    private void MoverEstelaHacia(Transform destino, System.Action alLlegar)
    {
        if (porteoEstela == null || destino == null) return;
        if (_movimientoEstela != null) StopCoroutine(_movimientoEstela);
        _movimientoEstela = StartCoroutine(Co_MoverEstela(destino.position, alLlegar));
    }

    private IEnumerator Co_MoverEstela(Vector3 destino, System.Action alLlegar)
    {
        Transform t = porteoEstela.transform;
        int mascaraSuelo = LayerMask.GetMask("Floor", "Default");

        while (Vector3.Distance(new Vector3(t.position.x, 0, t.position.z), new Vector3(destino.x, 0, destino.z)) > 0.35f)
        {
            Vector3 dir = (destino - t.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 360f * Time.deltaTime);
                Vector3 nuevaPos = t.position + dir.normalized * (velocidadPasoEstela * Time.deltaTime);

                // Proyectar pies exactamente al suelo físico de la calzada
                Vector3 origenRayo = new Vector3(nuevaPos.x, nuevaPos.y + 1.8f, nuevaPos.z);
                if (Physics.Raycast(origenRayo, Vector3.down, out RaycastHit hit, 8.0f, mascaraSuelo, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<NPCSimpleAnimator>() == null)
                    {
                        nuevaPos.y = hit.point.y;
                    }
                }

                t.position = nuevaPos;

                // Animación de pasos en las piernas de Estela sincronizada con su locomoción
                if (_animatorEstela != null)
                {
                    _animatorEstela.SetFloat(HashInputMagnitude, 0.60f);
                    _animatorEstela.SetBool(HashIsGrounded, true);
                    _animatorEstela.SetFloat(HashGroundDistance, 0f);
                }
                if (_npcAnimEstela != null)
                {
                    _npcAnimEstela.SetMovementSpeed(0.60f);
                }
            }
            yield return null;
        }

        Vector3 posLlegada = new Vector3(destino.x, destino.y, destino.z);
        Vector3 rayoLlegada = new Vector3(posLlegada.x, posLlegada.y + 1.8f, posLlegada.z);
        if (Physics.Raycast(rayoLlegada, Vector3.down, out RaycastHit hitFinal, 8.0f, mascaraSuelo, QueryTriggerInteraction.Ignore))
        {
            if (hitFinal.collider.GetComponentInParent<NPCSimpleAnimator>() == null)
            {
                posLlegada.y = hitFinal.point.y;
            }
        }
        t.position = posLlegada;

        // Al detenerse en el punto, volver a pose idle de pies conservando brazos con Liam
        if (_animatorEstela != null)
        {
            _animatorEstela.SetFloat(HashInputMagnitude, 0f);
        }
        if (_npcAnimEstela != null)
        {
            _npcAnimEstela.ResetMovement();
        }

        alLlegar?.Invoke();
    }

    private IEnumerator Co_RespawnJugador()
    {
        _enRespawn = true;
        FeedbackService.ScreenFlash(new Color(0.2f, 0.05f, 0.35f, 0.75f), 0.4f);

        yield return new WaitForSeconds(0.15f);

        Transform cp = null;
        if (checkpointsWill != null && checkpointsWill.Length > 0)
        {
            int idx = Mathf.Clamp(_seccionActual, 0, checkpointsWill.Length - 1);
            cp = checkpointsWill[idx];
        }

        Vector3 posDestino = cp != null ? cp.position : new Vector3(0.5f, 2.95f, 260f);
        Quaternion rotDestino = cp != null ? cp.rotation : Quaternion.Euler(0f, 180f, 0f);

        if (jugador != null)
        {
            jugador.SetPositionAndRotation(posDestino, rotDestino);

            var rb = jugador.GetComponent<Rigidbody>() ?? jugador.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.position = posDestino;
                rb.rotation = rotDestino;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            Physics.SyncTransforms();
        }

        yield return new WaitForSeconds(0.25f);
        _enRespawn = false;
    }

    // Estela habla sobre su cabeza; sin ella en escena, no se dice nada (no hay a quién atarlo).
    private void DecirEstela(string clave, float duracion)
    {
        if (porteoEstela != null) Bocadillos.Decir(porteoEstela.transform, clave, "Estela", duracion);
    }

    private static void MostrarAyuda(string clave)
    {
        var ayuda = TutorialPromptUI.Instance;
        if (ayuda == null) return;
        var idioma = LocalizationManager.Instance;
        ayuda.Show(idioma != null ? idioma.Get(clave, clave) : clave);
    }
}
