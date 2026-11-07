using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Lanza al jugador hacia arriba (con voltereta si se quiere) y le devuelve el doble salto, para
/// alcanzar sitios altos: una runa en el suelo o un trampolín. Como trampolín, solo lanza al caer
/// encima, aplasta y recupera su lona y suma el rebote extra que dé lo que lleva puesto el
/// personaje al mando (<see cref="RasgosDeMovimiento.reboteExtra"/>). Solo reacciona al jugador:
/// los NPCs pasan por encima sin más. Genérico: se coloca donde haga falta. Necesita un collider en
/// modo trigger (en un trampolín, una caja fina sobre la lona). El impulso lo da el motor
/// (<c>vThirdPersonController.Impulsar</c>). Ver INC-655.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LanzadorDeSalto : MonoBehaviour
{
    [Header("Impulso")]
    [Tooltip("Velocidad vertical (m/s) con la que sale el jugador. 22 sube unos 8 m.")]
    [SerializeField, Min(1f)] private float velocidadVertical = 22f;
    [Tooltip("Velocidad (m/s) hacia donde mira el lanzador (su eje Z azul), para llevar al jugador hasta lo alto. 0 = solo hacia arriba.")]
    [SerializeField, Min(0f)] private float empujeHaciaDelante = 4f;
    [Tooltip("Al salir del lanzador el doble salto vuelve a estar disponible.")]
    [SerializeField] private bool devolverDobleSalto = true;
    [Tooltip("Segundos tras el despegue en los que empieza la voltereta.")]
    [SerializeField, Min(0f)] private float retrasoDeLaVoltereta = 0.12f;
    [Tooltip("Segundos en los que el lanzador no vuelve a dispararse.")]
    [SerializeField, Min(0.1f)] private float enfriamiento = 0.8f;
    [Tooltip("Da una voltereta al salir.")]
    [SerializeField] private bool conVoltereta = true;
    [Tooltip("Suma el rebote extra de lo que lleva puesto el personaje al mando (RasgosDeMovimiento de sus atuendos).")]
    [SerializeField] private bool conReboteDeLaRopa = true;

    [Header("Trampolín")]
    [Tooltip("Solo lanza si el jugador llega desde arriba (cayendo o de pie encima), no al entrar por un lado o desde abajo.")]
    [SerializeField] private bool soloDesdeArriba;
    [Tooltip("Parte que se aplasta y recupera al lanzar (la lona). Vacío = no se mueve nada.")]
    [SerializeField] private Transform parteQueRebota;
    [Tooltip("Cuánto se aplasta en vertical al lanzar: 0,3 = baja al 70 % y luego se estira.")]
    [SerializeField, Range(0f, 0.9f)] private float aplastamiento = 0.3f;
    [Tooltip("Segundos que dura el rebote de la lona.")]
    [SerializeField, Min(0.05f)] private float duracionDelRebote = 0.45f;

    [Header("Efectos")]
    [Tooltip("Efecto de un solo uso al lanzar (pool de VFX). Vacío = nada.")]
    [SerializeField] private GameObject vfxDeImpulso;
    [SerializeField, Min(0.1f)] private float duracionDelVfx = 2f;
    [Tooltip("Clave de SFX al lanzar. Vacío = sin sonido.")]
    [SerializeField] private string sfxDeImpulso = "";

    private float _listoEn;
    private Collider _zona;
    private Vector3 _escalaDeLaLona;
    private Coroutine _rebote;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        _zona = GetComponent<Collider>();
        if (parteQueRebota != null) _escalaDeLaLona = parteQueRebota.localScale;
    }

    void OnDisable()
    {
        if (_rebote != null) { StopCoroutine(_rebote); _rebote = null; }
        if (parteQueRebota != null) parteQueRebota.localScale = _escalaDeLaLona;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < _listoEn) return;

        var controller = other.GetComponentInParent<vThirdPersonController>();
        if (controller == null || !controller.enabled) return;
        var jugador = PlayerService.Player;
        if (jugador == null || (controller.gameObject != jugador && !controller.transform.IsChildOf(jugador.transform))) return;

        if (soloDesdeArriba && !LlegaDesdeArriba(controller)) return;

        _listoEn = Time.time + enfriamiento;
        Vector3 frente = transform.forward; frente.y = 0f;
        Vector3 empuje = frente.sqrMagnitude > 0.0001f ? frente.normalized * empujeHaciaDelante : Vector3.zero;
        float vertical = velocidadVertical * (conReboteDeLaRopa ? RasgosDeMovimiento.AlMando.MultiplicadorDeRebote : 1f);
        controller.Impulsar(vertical, empuje, devolverDobleSalto);
        Rebotar();

        if (vfxDeImpulso != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxDeImpulso, transform.position, transform.rotation, duracionDelVfx);
        if (!string.IsNullOrEmpty(sfxDeImpulso) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxDeImpulso, 1f, transform.position);

        if (!conVoltereta) return;
        var voltereta = controller.GetComponent<VolteretaDelJugador>();
        if (voltereta != null) StartCoroutine(Co_Voltereta(voltereta));
    }

    /// Cae encima: no sube (o apenas) y tiene los pies por encima del centro de la zona.
    private bool LlegaDesdeArriba(vThirdPersonController controller)
    {
        if (controller.TryGetComponent(out Rigidbody cuerpo) && cuerpo.linearVelocity.y > 0.5f) return false;
        return _zona == null || controller.transform.position.y >= _zona.bounds.center.y;
    }

    /// <summary>
    /// Aplasta y recupera la lona como si alguien cayera encima. Lo llama el propio lanzador al
    /// lanzar al jugador y quien mueva a un personaje con guion que bota en él.
    /// </summary>
    public void Rebotar()
    {
        if (parteQueRebota == null || aplastamiento <= 0f || !isActiveAndEnabled) return;
        if (_rebote != null) StopCoroutine(_rebote);
        _rebote = StartCoroutine(Co_Rebote());
    }

    /// La lona se aplasta, se estira y vuelve a su sitio, cada vez menos.
    private IEnumerator Co_Rebote()
    {
        for (float t = 0f; t < duracionDelRebote; t += Time.deltaTime)
        {
            float k = t / duracionDelRebote;
            float onda = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k) * aplastamiento;
            parteQueRebota.localScale = Vector3.Scale(_escalaDeLaLona, new Vector3(1f + onda * 0.5f, 1f - onda, 1f + onda * 0.5f));
            yield return null;
        }
        parteQueRebota.localScale = _escalaDeLaLona;
        _rebote = null;
    }

    private IEnumerator Co_Voltereta(VolteretaDelJugador voltereta)
    {
        if (retrasoDeLaVoltereta > 0f) yield return new WaitForSeconds(retrasoDeLaVoltereta);
        if (voltereta != null) voltereta.EnElAire();
    }
}
