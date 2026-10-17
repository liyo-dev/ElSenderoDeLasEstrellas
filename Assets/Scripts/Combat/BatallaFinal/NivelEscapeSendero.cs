using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Sendero.Core.Feedback;
using Sendero.UI;
using Invector.vCharacterController;

/// <summary>
/// Controlador del nivel jugable de huida del Sendero de las Estrellas mientras colapsa.
/// Gestiona el avance de Estela portando a Liam, los checkpoints del jugador, la respuesta
/// a la caída al vacío (respawn sin Game Over punitivo) y la llegada al Umbral final.
/// </summary>
public class NivelEscapeSendero : MonoBehaviour
{
    [Header("Actores")]
    [SerializeField] private Transform jugador;
    [SerializeField] private PorteoDeCompanero porteoEstela;

    [Header("Avance de Estela")]
    [Tooltip("Puntos a los que Estela avanza según Will va abriendo los puentes mágicos.")]
    [SerializeField] private Transform[] puntosSegurosEstela;
    [SerializeField] private float velocidadPasoEstela = 3.2f;

    [Header("Checkpoints de Caída al Vacío")]
    [Tooltip("Posiciones seguras para devolver a Will si cae al abismo durante el plataformeo.")]
    [SerializeField] private Transform[] checkpointsWill;
    [SerializeField] private float alturaCaidaVacio = -8f;

    [Header("Elementos de Escenario")]
    [SerializeField] private PlatformElevator elevadorAstral;
    [SerializeField] private GameObject puenteFinalVisual;

    public PlatformElevator ElevadorAstral { get => elevadorAstral; set => elevadorAstral = value; }
    public GameObject PuenteFinalVisual { get => puenteFinalVisual; set => puenteFinalVisual = value; }

    [Header("Eventos")]
    public UnityEvent alIniciarEscape = new UnityEvent();
    public UnityEvent alCompletarSeccion1 = new UnityEvent();
    public UnityEvent alCompletarSeccion2 = new UnityEvent();
    public UnityEvent alLlegarAlUmbral = new UnityEvent();

    private int _seccionActual = 0;
    private bool _enRespawn;
    private bool _escapeFinalizado;
    private Coroutine _movimientoEstela;

    private void Start()
    {
        if (jugador == null && PlayerService.Player != null)
            jugador = PlayerService.Player.transform;

        // Desactivar vuelo en Will obligatoriamente para plataformeo atlético
        if (jugador != null)
        {
            var flying = jugador.GetComponentInChildren<PlayerFlyingController>(true);
            if (flying != null) flying.enabled = false;
        }

        var preset = UnlockService.GetActivePreset();
        if (preset != null && preset.abilities != null)
        {
            preset.abilities.fly = false;
            preset.abilities.jump = true;
            preset.abilities.magic = true;
            preset.abilities.climb = true;
            preset.abilities.sprint = true;
            preset.abilities.shield = true;
            if (preset.maxMP < 100f) preset.maxMP = 100f;
            preset.currentMP = preset.maxMP;
        }

        // Ocultar minimapa y HUD de combate para máxima inmersión
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD(this);
        MinimapController.Ocultar("EscapeSendero");

        // Iniciar con Estela llevando a Liam en brazos tumbado y con ojos cerrados
        if (porteoEstela != null && !porteoEstela.EstaCargando)
        {
            porteoEstela.CargarDirecto();
        }

        alIniciarEscape?.Invoke();
        StartCoroutine(Co_DialogoInicio());
    }

    private void Update()
    {
        if (_escapeFinalizado || _enRespawn) return;

        // Detección de caída al abismo
        if (jugador != null && jugador.position.y < alturaCaidaVacio)
        {
            StartCoroutine(Co_RespawnJugador());
        }
    }

    private IEnumerator Co_DialogoInicio()
    {
        yield return new WaitForSeconds(0.8f);
        TutorialPromptUI.Instance?.Show("¡El Sendero colapsa! Salta en la runa y dispara al cristal en el aire para abrir el puente.");
        DecirBocadillo(porteoEstela != null ? porteoEstela.transform : null,
            "¡Will! ¡No puedo saltar ese abismo con Liam en brazos! ¡Activa el cristal con tu magia!",
            "Estela", 4.5f);
    }

    /// <summary>
    /// Llamado cuando Will activa el cristal de luz de la Sección 1.
    /// Despliega el puente y hace que Estela cruce con Liam.
    /// </summary>
    public void ActivarPuenteSeccion1()
    {
        if (_seccionActual < 1)
        {
            _seccionActual = 1;
            FeedbackService.CameraShake(0.45f, 0.6f);
            alCompletarSeccion1?.Invoke();
            TutorialPromptUI.Instance?.Show("¡Puente de luz activo! Avanza hacia la cota media.");

            if (puntosSegurosEstela != null && puntosSegurosEstela.Length > 0 && puntosSegurosEstela[0] != null)
            {
                MoverEstelaHacia(puntosSegurosEstela[0], () =>
                {
                    DecirBocadillo(porteoEstela != null ? porteoEstela.transform : null,
                        "¡El puente aguanta! ¡Sigue adelante Will!", "Estela", 3.2f);
                });
            }
        }
    }

    /// <summary>
    /// Llamado cuando Will activa el elevador/molinete de la Sección 2.
    /// </summary>
    public void ActivarAvanceSeccion2()
    {
        if (_seccionActual < 2)
        {
            _seccionActual = 2;
            FeedbackService.CameraShake(0.5f, 0.8f);
            if (elevadorAstral != null) elevadorAstral.Raise(false);
            alCompletarSeccion2?.Invoke();
            TutorialPromptUI.Instance?.Show("¡Elevador en marcha! Encadena los saltos entre islas hacia el Umbral.");

            if (puntosSegurosEstela != null && puntosSegurosEstela.Length > 1 && puntosSegurosEstela[1] != null)
            {
                MoverEstelaHacia(puntosSegurosEstela[1], () =>
                {
                    DecirBocadillo(porteoEstela != null ? porteoEstela.transform : null,
                        "¡El elevador nos baja! ¡Casi llegamos al Umbral!", "Estela", 3.4f);
                });
            }
        }
    }

    /// <summary>
    /// Llamado al activar el interruptor final que extiende el puente hacia el Umbral.
    /// </summary>
    public void ActivarPuenteFinalUmbral()
    {
        if (_seccionActual < 3)
        {
            _seccionActual = 3;
            FeedbackService.CameraShake(0.6f, 1f);
            if (puenteFinalVisual != null) puenteFinalVisual.SetActive(true);
            TutorialPromptUI.Instance?.Show("¡Puente de retorno extendido! ¡Cruza el Umbral de la Tierra!");

            if (puntosSegurosEstela != null && puntosSegurosEstela.Length > 2 && puntosSegurosEstela[2] != null)
            {
                MoverEstelaHacia(puntosSegurosEstela[2], () =>
                {
                    DecirBocadillo(porteoEstela != null ? porteoEstela.transform : null,
                        "¡Will! ¡Estamos a salvo! ¡Crucemos el portal!", "Estela", 3.5f);
                });
            }
        }
    }

    /// <summary>
    /// Cruzar la meta del Umbral.
    /// </summary>
    public void LlegarAlUmbral()
    {
        if (_escapeFinalizado) return;
        _escapeFinalizado = true;

        FeedbackService.ScreenFlash(Color.white, 2.0f);
        FeedbackService.CameraShake(0.7f, 1.2f);
        TutorialPromptUI.Instance?.Hide();
        DecirBocadillo(jugador, "¡Lo hemos conseguido... Hemos salido!", "Will", 4f);
        alLlegarAlUmbral?.Invoke();

        // Levantar señal narrativa oficial
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
        while (Vector3.Distance(new Vector3(t.position.x, 0, t.position.z), new Vector3(destino.x, 0, destino.z)) > 0.35f)
        {
            Vector3 dir = (destino - t.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 360f * Time.deltaTime);
                t.position += dir.normalized * (velocidadPasoEstela * Time.deltaTime);
            }
            yield return null;
        }

        t.position = new Vector3(destino.x, t.position.y, destino.z);
        alLlegar?.Invoke();
    }

    private IEnumerator Co_RespawnJugador()
    {
        _enRespawn = true;
        FeedbackService.ScreenFlash(new Color(0.2f, 0.05f, 0.35f, 0.75f), 0.4f);

        yield return new WaitForSeconds(0.15f);

        // Buscar checkpoint seguro correspondiente a la sección actual
        Transform cp = null;
        if (checkpointsWill != null && checkpointsWill.Length > 0)
        {
            int idx = Mathf.Clamp(_seccionActual, 0, checkpointsWill.Length - 1);
            cp = checkpointsWill[idx];
        }

        Vector3 posDestino = cp != null ? cp.position : new Vector3(0f, 0.5f, 0f);
        Quaternion rotDestino = cp != null ? cp.rotation : Quaternion.identity;

        if (jugador != null)
        {
            var motor = jugador.GetComponent<vThirdPersonMotor>();
            if (motor != null)
            {
                motor.transform.position = posDestino;
                motor.transform.rotation = rotDestino;
            }
            else
            {
                jugador.position = posDestino;
                jugador.rotation = rotDestino;
            }

            var rb = jugador.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        yield return new WaitForSeconds(0.25f);
        _enRespawn = false;
    }

    private void DecirBocadillo(Transform quien, string texto, string nombre, float duracion)
    {
        var bubble = SpeechBubbleUI.Instance;
        if (bubble != null && quien != null)
        {
            bubble.Show(quien, texto, duration: duracion, speakerName: nombre);
        }
    }

    private void OnDestroy()
    {
        Sendero.UI.PlayerHUDV2.Instance?.ShowHUD(this);
        MinimapController.Mostrar("EscapeSendero");
    }
}
