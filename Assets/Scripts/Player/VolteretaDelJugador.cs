using System;
using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Voltereta del jugador: la única forma de que dé una. Dos modos:
/// <list type="bullet">
/// <item><see cref="DesdeElSuelo"/>: salto con parábola propia. La animación es «en el sitio»
/// (el clip gira sin subir); la altura y el desplazamiento los pone este componente, sincronizados
/// con el tramo en el aire del clip, y aterriza en el suelo real. Rigidbody cinemático y
/// controlador apagado mientras dura.</item>
/// <item><see cref="EnElAire"/>: solo la animación; el salto y la caída los sigue llevando el
/// motor (o quien mueva al personaje, como el lanzamiento por un golpe).</item>
/// </list>
/// Además, al caer desde mucha altura da sola una voltereta antes de aterrizar.
/// Mientras dura, la transición «cualquier estado → Falling» del Animator queda bloqueada con el
/// mismo parámetro que la bloquea en vuelo, y la capa de brazos baja a 0 para no tapar el giro del
/// torso. Ver INC-651 e INC-656.
/// </summary>
[DisallowMultipleComponent]
public class VolteretaDelJugador : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Controlador del personaje. Se busca solo si está vacío.")]
    [SerializeField] private vThirdPersonController controller;
    [Tooltip("Animator del personaje. Se busca solo si está vacío.")]
    [SerializeField] private Animator animator;
    [Tooltip("Rigidbody del personaje. Se busca solo si está vacío.")]
    [SerializeField] private Rigidbody body;

    [Header("Desde el suelo")]
    [Tooltip("Estado del Animator de la voltereta desde el suelo (en el sitio: la altura la pone este componente).")]
    [SerializeField] private string estadoDesdeElSuelo = "JumpFullSpin_InPlace_NoWeapon";
    [Tooltip("Tramo del clip (0-1) en el que el personaje está en el aire.")]
    [SerializeField] private Vector2 tramoEnElAire = new Vector2(0.1f, 0.81f);
    [Tooltip("Segundos de la voltereta si el Animator no tiene el estado.")]
    [SerializeField, Min(0.2f)] private float duracionSinAnimacion = 0.8f;

    [Header("En el aire")]
    [Tooltip("Estado del Animator de la voltereta en el aire.")]
    [SerializeField] private string estadoEnElAire = "JumpAirSpin_InPlace_NoWeapon";
    [Tooltip("Estado al que vuelve si sigue en el aire al acabar.")]
    [SerializeField] private string estadoDeCaida = "Falling";

    [Header("Animator")]
    [Tooltip("Estado al que vuelve si está en el suelo al acabar.")]
    [SerializeField] private string estadoDeLocomocion = "Free Locomotion";
    [Tooltip("Parámetro bool que bloquea la transición de cualquier estado a Falling (el del vuelo).")]
    [SerializeField] private string parametroDeBloqueoAereo = "isFlying";
    [Tooltip("Capa de brazos del Animator: se baja a 0 mientras dura la voltereta.")]
    [SerializeField, Min(0)] private int capaDeBrazos = 1;
    [SerializeField, Min(0f)] private float fundido = 0.08f;
    [Tooltip("Tope de segundos de una voltereta, por si el estado no termina nunca.")]
    [SerializeField, Min(0.3f)] private float duracionMaxima = 1.8f;

    [Header("Caída larga (INC-656)")]
    [Tooltip("Da una voltereta sola al caer desde mucha altura.")]
    [SerializeField] private bool volteretaEnCaidaLarga = true;
    [Tooltip("Metros sin suelo por debajo, cayendo, a partir de los que da la voltereta.")]
    [SerializeField, Min(1f)] private float alturaMinimaDeCaida = 5f;
    [Tooltip("Velocidad de caída (m/s) a partir de la que se mira la altura.")]
    [SerializeField, Min(0f)] private float velocidadMinimaDeCaida = 3f;

    private PlayerActionManager _acciones;
    private Coroutine _co;
    private bool _modoPuesto;
    private bool _bloqueoPuesto;
    private bool _cinematicoAntes;
    private bool _controladorAntes;
    private bool _bloqueoDeMovimientoAntes;
    private bool _aireSuprimidoAntes;
    private bool _devolverCuerpo;
    private Vector3 _aterrizaje;
    private bool _caidaArmada = true;
    private bool _animacionAjena;   // otro sistema ha puesto su estado: al acabar no se toca el Animator
    private int _hashSuelo, _hashAire, _hashCaida, _hashLocomocion, _hashBloqueo;
    private bool _tieneBloqueo;
    private readonly RaycastHit[] _golpes = new RaycastHit[8];

    /// <summary>Hay una voltereta en curso.</summary>
    public bool EnCurso => _co != null;

    /// <summary>La voltereta en curso es la de <see cref="DesdeElSuelo"/>.</summary>
    public bool EnCursoDesdeElSuelo { get; private set; }

    /// <summary>Terminó (o se canceló) la voltereta.</summary>
    public event Action OnTerminada;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!animator && controller) controller.TryGetComponent(out animator);
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!body && controller) controller.TryGetComponent(out body);
        if (!body) body = GetComponentInParent<Rigidbody>();
        _acciones = GetComponentInParent<PlayerActionManager>();

        _hashSuelo = Animator.StringToHash(estadoDesdeElSuelo);
        _hashAire = Animator.StringToHash(estadoEnElAire);
        _hashCaida = Animator.StringToHash(estadoDeCaida);
        _hashLocomocion = Animator.StringToHash(estadoDeLocomocion);
        _hashBloqueo = Animator.StringToHash(parametroDeBloqueoAereo);
        _tieneBloqueo = TieneParametroBool(_hashBloqueo);
    }

    void OnDisable() => Cancelar();

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Voltereta desde el suelo: sube 'alturaEnCabezas' (1 = de los pies a la cabeza) y se
    /// desplaza 'desplazamiento' metros en horizontal (en mundo), recortado si hay una pared. Aterriza
    /// en el suelo real. Bloquea moverse, saltar y lanzar mientras dura. 'estado' sustituye a la
    /// animación por defecto. False si no se puede (otra voltereta, vuelo, lanzado por un golpe).
    /// </summary>
    public bool DesdeElSuelo(float alturaEnCabezas, Vector3 desplazamiento, string estado = null)
    {
        if (!PuedeEmpezar()) return false;
        _co = StartCoroutine(Co_DesdeElSuelo(Mathf.Max(0f, alturaEnCabezas), desplazamiento, estado));
        return true;
    }

    /// <summary>
    /// Voltereta en el aire: solo la animación; el movimiento lo lleva quien ya lo llevara. Al
    /// acabar vuelve a la caída si sigue en el aire o a la locomoción si ha tocado suelo. False si
    /// no se puede (otra voltereta, vuelo).
    /// </summary>
    public bool EnElAire(string estado = null)
    {
        if (!PuedeEmpezar(permitirLanzado: true)) return false;
        _caidaArmada = false;
        _co = StartCoroutine(Co_EnElAire(estado));
        return true;
    }

    /// <summary>Corta la voltereta en curso y devuelve todo lo que tomó. Idempotente.</summary>
    public void Cancelar()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        Soltar();
    }

    // ── Desarrollo ────────────────────────────────────────────────────────────

    private bool PuedeEmpezar(bool permitirLanzado = false)
    {
        if (_co != null || !isActiveAndEnabled) return false;
        if (controller && controller.suppressAirMovement) return false;
        if (!permitirLanzado && TryGetComponent(out AerialKnockbackReceiver lanzado) && lanzado.IsLaunching) return false;
        return true;
    }

    private IEnumerator Co_DesdeElSuelo(float alturaEnCabezas, Vector3 desplazamiento, string estado)
    {
        EnCursoDesdeElSuelo = true;
        _animacionAjena = false;
        int hash = string.IsNullOrEmpty(estado) ? _hashSuelo : Animator.StringToHash(estado);
        bool animado = Reproducir(hash);

        Vector3 inicio = transform.position;
        desplazamiento.y = 0f;
        desplazamiento = RecortarContraParedes(inicio, desplazamiento);
        _aterrizaje = SueloEn(inicio + desplazamiento, inicio.y);
        float alto = AlturaDeLaCabeza() * alturaEnCabezas;

        Tomar();

        float empezo = Time.time;
        float tope = empezo + duracionMaxima;
        bool visto = false;
        while (Time.time < tope)
        {
            BajarBrazos();

            float t;
            if (animado)
            {
                t = TiempoNormalizado(hash);
                if (t >= 0f) visto = true;
                else if (visto || Time.time > empezo + 0.3f) { _animacionAjena = true; break; }   // otro estado lo ha sustituido
                if (t >= 0.92f) break;
            }
            else
            {
                t = (Time.time - empezo) / duracionSinAnimacion;
                if (t >= 1f) break;
            }

            float u = Mathf.InverseLerp(tramoEnElAire.x, tramoEnElAire.y, Mathf.Max(0f, t));
            float subida = t > tramoEnElAire.x && t < tramoEnElAire.y ? alto * 4f * u * (1f - u) : 0f;
            Vector3 p = Vector3.Lerp(inicio, _aterrizaje, Mathf.SmoothStep(0f, 1f, u));
            p.y += subida;
            transform.position = p;
            yield return null;
        }

        _co = null;
        Soltar();
    }

    private IEnumerator Co_EnElAire(string estado)
    {
        EnCursoDesdeElSuelo = false;
        _animacionAjena = false;
        int hash = string.IsNullOrEmpty(estado) ? _hashAire : Animator.StringToHash(estado);
        bool animado = Reproducir(hash);
        PonerBloqueo(true);

        float empezo = Time.time;
        float tope = empezo + (animado ? duracionMaxima : duracionSinAnimacion);
        bool visto = false;
        while (Time.time < tope)
        {
            BajarBrazos();
            if (Time.time > empezo + 0.15f && controller && !controller.IsAirborne) break;   // ya en el suelo
            if (controller && controller.suppressAirMovement) { _animacionAjena = true; break; }   // ha echado a volar
            if (animado)
            {
                float t = TiempoNormalizado(hash);
                if (t >= 0f) visto = true;
                else if (visto || Time.time > empezo + 0.3f) { _animacionAjena = true; break; }   // doble salto u otro estado
                if (t >= 0.95f) break;
            }
            yield return null;
        }

        _co = null;
        Soltar();
    }

    /// Controlador apagado (y su movimiento en tierra y aire bloqueado: el motor sigue corriendo
    /// y no debe dar velocidad a un Rigidbody cinemático), Rigidbody cinemático, acciones
    /// bloqueadas y transición a Falling bloqueada: la voltereta desde el suelo es la única que
    /// mueve al personaje.
    private void Tomar()
    {
        if (_acciones && !_modoPuesto) { _acciones.PushMode(ActionMode.Stunned); _modoPuesto = true; }
        if (!_devolverCuerpo)
        {
            _devolverCuerpo = true;
            if (controller)
            {
                _controladorAntes = controller.enabled;
                _bloqueoDeMovimientoAntes = controller.lockMovement;
                _aireSuprimidoAntes = controller.suppressAirMovement;
                controller.ResetInputSmoothing();
                controller.lockMovement = true;
                controller.suppressAirMovement = true;
                controller.enabled = false;
            }
            if (body)
            {
                _cinematicoAntes = body.isKinematic;
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }
        PonerBloqueo(true);
    }

    /// Devuelve todo lo tomado y sale de la animación: la voltereta desde el suelo deja al
    /// personaje en su punto de aterrizaje. Idempotente.
    private void Soltar()
    {
        bool habia = _devolverCuerpo || _modoPuesto || _bloqueoPuesto;
        bool desdeElSuelo = _devolverCuerpo;

        if (_devolverCuerpo)
        {
            _devolverCuerpo = false;
            transform.position = _aterrizaje;
            if (body)
            {
                body.isKinematic = _cinematicoAntes;
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            }
            if (controller)
            {
                controller.lockMovement = _bloqueoDeMovimientoAntes;
                controller.suppressAirMovement = _aireSuprimidoAntes;
                controller.enabled = _controladorAntes;
            }
        }
        if (_modoPuesto && _acciones) _acciones.PopMode(ActionMode.Stunned);
        _modoPuesto = false;
        PonerBloqueo(false);

        if (!habia) return;
        EnCursoDesdeElSuelo = false;
        if (!_animacionAjena && animator && animator.isActiveAndEnabled)
        {
            bool enElAire = !desdeElSuelo && controller && controller.IsAirborne;
            int salida = enElAire ? _hashCaida : _hashLocomocion;
            if (animator.HasState(0, salida)) animator.CrossFadeInFixedTime(salida, 0.15f, 0);
        }
        OnTerminada?.Invoke();
    }

    // ── Caída larga ───────────────────────────────────────────────────────────

    void Update()
    {
        if (!volteretaEnCaidaLarga || _co != null || !controller || !controller.enabled) return;
        if (!controller.IsAirborne) { _caidaArmada = true; return; }
        if (!_caidaArmada || controller.suppressAirMovement || controller.IsHoldingAirborne) return;
        if (!body || body.isKinematic || body.linearVelocity.y > -velocidadMinimaDeCaida) return;
        if ((Time.frameCount & 3) != 0) return;   // basta con mirar cada pocos fotogramas

        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, alturaMinimaDeCaida,
                controller.groundLayer, QueryTriggerInteraction.Ignore)) return;   // el suelo está cerca

        EnElAire();
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    private bool Reproducir(int hash)
    {
        if (!animator || !animator.isActiveAndEnabled || !animator.HasState(0, hash)) return false;
        BajarBrazos();
        animator.CrossFadeInFixedTime(hash, fundido, 0);
        return true;
    }

    private void BajarBrazos()
    {
        if (animator && animator.layerCount > capaDeBrazos && capaDeBrazos > 0)
            animator.SetLayerWeight(capaDeBrazos, 0f);
    }

    private void PonerBloqueo(bool activo)
    {
        if (!_tieneBloqueo || !animator) return;
        if (activo == _bloqueoPuesto) return;
        _bloqueoPuesto = activo;
        animator.SetBool(_hashBloqueo, activo);
    }

    /// Tiempo normalizado del estado en la capa base (también mientras se entra en él), o -1.
    private float TiempoNormalizado(int hash)
    {
        if (animator.IsInTransition(0))
        {
            var siguiente = animator.GetNextAnimatorStateInfo(0);
            if (siguiente.shortNameHash == hash) return siguiente.normalizedTime;
        }
        var actual = animator.GetCurrentAnimatorStateInfo(0);
        return actual.shortNameHash == hash ? actual.normalizedTime : -1f;
    }

    private bool TieneParametroBool(int hash)
    {
        if (!animator || animator.runtimeAnimatorController == null) return false;
        for (int i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.GetParameter(i);
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool) return true;
        }
        return false;
    }

    // ── Geometría ─────────────────────────────────────────────────────────────

    /// De los pies a la cabeza, en metros (1 si el Animator no es humanoide).
    private float AlturaDeLaCabeza()
    {
        if (animator && animator.isHuman)
        {
            var cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
            if (cabeza)
            {
                float h = cabeza.position.y - transform.position.y;
                if (h > 0.2f) return h;
            }
        }
        return 1f;
    }

    /// Recorta el desplazamiento ante la primera pared. Personajes y geometría comparten capa:
    /// los personajes (NPCSimpleAnimator en la raíz, también el propio jugador) no cuentan.
    private Vector3 RecortarContraParedes(Vector3 desde, Vector3 desplazamiento)
    {
        float dist = desplazamiento.magnitude;
        if (dist < 0.01f) return Vector3.zero;
        Vector3 dir = desplazamiento / dist;
        Vector3 origen = desde + Vector3.up * 1f;
        int n = Physics.SphereCastNonAlloc(origen, 0.3f, dir, _golpes, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        float libre = dist;
        for (int i = 0; i < n; i++)
        {
            var h = _golpes[i];
            if (h.collider == null || h.distance <= 0f) continue;
            if (h.collider.transform.root.GetComponent<NPCSimpleAnimator>() != null) continue;
            libre = Mathf.Min(libre, Mathf.Max(0f, h.distance - 0.3f));
        }
        return dir * libre;
    }

    /// Punto de suelo bajo 'punto' (la altura de 'yPorDefecto' si no encuentra nada).
    private Vector3 SueloEn(Vector3 punto, float yPorDefecto)
    {
        LayerMask capas = controller ? controller.groundLayer : (LayerMask)~0;
        if (Physics.Raycast(punto + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, capas, QueryTriggerInteraction.Ignore))
            punto.y = hit.point.y;
        else
            punto.y = yPorDefecto;
        return punto;
    }
}
