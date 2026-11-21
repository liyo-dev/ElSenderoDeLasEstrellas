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
/// Mientras dura, la transición «cualquier estado → Falling» del Animator queda bloqueada con el
/// mismo parámetro que la bloquea en vuelo, y la capa de brazos baja a 0 para no tapar el giro del
/// torso. Ver INC-651.
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
    [Tooltip("Tramo del clip (0-1) en el que los pies no tocan el suelo: entre el impulso y la caída. Antes se agacha y se impulsa (los pies en el suelo) y después amortigua; la parábola solo sube en este tramo.")]
    [SerializeField] private Vector2 tramoDeLosPiesEnElAire = new Vector2(0.4f, 0.73f);
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
    private bool _animacionAjena;   // otro sistema ha puesto su estado: al acabar no se toca el Animator
    private int _hashSuelo, _hashAire, _hashCaida, _hashLocomocion, _hashBloqueo;
    private bool _tieneBloqueo;
    private Vector2 _tramo;
    private float _terminaEn = 0.92f;
    private float _duracionFija;
    private float _velocidadDeSalida;
    private Avance _avance;
    private bool _hayOtroTramo;
    private Tramo _otroTramo;
    private float _fundidoDeEntrada, _fundidoDeSalida;   // 0 = los de siempre
    private readonly RaycastHit[] _golpes = new RaycastHit[8];

    /// <summary>Cómo reparte el avance a lo largo de su tramo.</summary>
    public enum Avance
    {
        /// Arranca y llega despacio (el de siempre).
        Suave,
        /// Velocidad constante: un impulso que no frena (el salto de una esquiva).
        Constante,
        /// Sale lanzado y va frenando: un dash que acaba rodando.
        Frenando
    }

    /// <summary>Un tramo de <see cref="EnDosTramos"/>.</summary>
    public struct Tramo
    {
        /// Altura del salto, en alturas de la cabeza (0 = por el suelo).
        public float altura;
        /// Desplazamiento horizontal en mundo (se recorta contra paredes).
        public Vector3 desplazamiento;
        /// Estado del Animator (vacío = el de la voltereta desde el suelo).
        public string estado;
        /// Tramo del clip (0-1) en el que avanza.
        public Vector2 tramoDeMovimiento;
        /// Punto (0-1) en el que acaba el tramo.
        public float terminaEn;
        /// Segundos fijos del tramo (0 = lo que dure el clip).
        public float duracionFija;
        public Avance avance;
        /// Velocidad horizontal al final de un tramo que frena, en metros por segundo.
        public float velocidadDeSalida;
        /// Segundos de fundido al entrar en el clip y al salir a la locomoción (0 = los de siempre).
        public float fundidoDeEntrada, fundidoDeSalida;
    }

    /// <summary>
    /// Salto con la voltereta en el aire (esquiva con stick, dibujo de Raúl, 20 nov 2026): un solo
    /// arco. Sube; en lo alto abre los brazos y junta las piernas preparando la voltereta; la da
    /// mientras baja; y cae como un salto normal. Fracciones del arco (0-1) para cada parte.
    /// </summary>
    public struct SaltoConVoltereta
    {
        /// Altura en alturas de la cabeza.
        public float altura;
        /// Desplazamiento horizontal en mundo (se recorta contra paredes).
        public Vector3 desplazamiento;
        /// Segundos en el aire.
        public float duracion;
        public string estadoSubida, estadoPreparar, estadoVoltereta, estadoCaida;
        /// Fracción del arco en la que deja de subir y se prepara, y en la que empieza la voltereta.
        public float empiezaAPrepararEn, empiezaLaVolteretaEn;
        /// Segundos de la caída en el suelo antes de devolver el control.
        public float caida;
    }

    /// <summary>Salto con la voltereta en el aire. False si no se puede empezar.</summary>
    public bool ConVolteretaEnElAire(SaltoConVoltereta salto)
    {
        if (!PuedeEmpezar()) return false;
        _hayOtroTramo = false;
        _co = StartCoroutine(Co_ConVolteretaEnElAire(salto));
        return true;
    }

    private float _velocidadDelAnimatorAntes = -1f;

    private IEnumerator Co_ConVolteretaEnElAire(SaltoConVoltereta s)
    {
        EnCursoDesdeElSuelo = true;
        _animacionAjena = false;
        Tomar();

        Vector3 inicio = transform.position;
        Vector3 desplazamiento = s.desplazamiento; desplazamiento.y = 0f;
        desplazamiento = RecortarContraParedes(inicio, desplazamiento);
        _aterrizaje = SueloEn(inicio + desplazamiento, inicio.y);
        float alto = AlturaDeLaCabeza() * Mathf.Max(0f, s.altura);
        float dur = Mathf.Max(0.2f, s.duracion);
        float preparar = Mathf.Clamp01(s.empiezaAPrepararEn);
        float voltereta = Mathf.Clamp(s.empiezaLaVolteretaEn, preparar, 1f);

        int fase = -1;
        int hashVoltereta = Animator.StringToHash(string.IsNullOrEmpty(s.estadoVoltereta) ? estadoEnElAire : s.estadoVoltereta);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            BajarBrazos();
            float u = t / dur;
            int ahora = u < preparar ? 0 : u < voltereta ? 1 : 2;
            if (ahora != fase)
            {
                fase = ahora;
                if (fase == 0) ReproducirEstado(s.estadoSubida);
                else if (fase == 1) ReproducirEstado(s.estadoPreparar);
                else
                {
                    // La voltereta entera en lo que queda de arco: el clip se acelera para caber.
                    if (Reproducir(hashVoltereta)) AjustarVelocidadDelClip(s.estadoVoltereta ?? estadoEnElAire, dur * (1f - voltereta));
                }
            }
            Vector3 p = Vector3.Lerp(inicio, _aterrizaje, u);
            p.y += alto * 4f * u * (1f - u);
            transform.position = p;
            yield return null;
        }
        RestaurarVelocidadDelAnimator();
        transform.position = _aterrizaje;

        // La caída de un salto normal, ya en el suelo.
        if (s.caida > 0f && ReproducirEstado(s.estadoCaida))
            for (float t = 0f; t < s.caida; t += Time.deltaTime) { BajarBrazos(); yield return null; }

        _co = null;
        Soltar();
    }

    private bool ReproducirEstado(string estado) => !string.IsNullOrEmpty(estado) && Reproducir(Animator.StringToHash(estado));

    private void AjustarVelocidadDelClip(string nombreDelClip, float segundos)
    {
        if (!animator || animator.runtimeAnimatorController == null || segundos <= 0.05f) return;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null || clip.name != nombreDelClip || clip.length <= 0.01f) continue;
            if (_velocidadDelAnimatorAntes < 0f) _velocidadDelAnimatorAntes = animator.speed;
            animator.speed = clip.length / segundos;
            return;
        }
    }

    private void RestaurarVelocidadDelAnimator()
    {
        if (_velocidadDelAnimatorAntes < 0f || !animator) return;
        animator.speed = _velocidadDelAnimatorAntes;
        _velocidadDelAnimatorAntes = -1f;
    }

    /// <summary>Hay una voltereta en curso.</summary>
    public bool EnCurso => _co != null;

    /// <summary>La voltereta en curso es la de <see cref="DesdeElSuelo"/>.</summary>
    public bool EnCursoDesdeElSuelo { get; private set; }

    /// <summary>Terminó (o se canceló) la voltereta.</summary>
    public event Action OnTerminada;
    public float ProgresoDesdeElSuelo { get; private set; }

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
        => DesdeElSuelo(alturaEnCabezas, desplazamiento, estado, tramoDeLosPiesEnElAire, 0.92f);

    /// <summary>
    /// Como <see cref="DesdeElSuelo(float, Vector3, string)"/>, con el tramo del clip (0-1) en el
    /// que se recorre el desplazamiento y el punto del clip en el que termina. Para volteretas que
    /// no son un salto (rodar por el suelo): con altura 0, el personaje avanza solo en ese tramo.
    /// </summary>
    public bool DesdeElSuelo(float alturaEnCabezas, Vector3 desplazamiento, string estado,
                             Vector2 tramoDeMovimiento, float terminaEn)
        => DesdeElSuelo(alturaEnCabezas, desplazamiento, estado, tramoDeMovimiento, terminaEn, 0f);

    /// <summary>
    /// Como la anterior, pero si <paramref name="duracionFija"/> es mayor que 0 la voltereta dura esos
    /// segundos sea cual sea el clip (para poses fijas, que no avanzan). El estado se pone igual.
    /// </summary>
    public bool DesdeElSuelo(float alturaEnCabezas, Vector3 desplazamiento, string estado,
                             Vector2 tramoDeMovimiento, float terminaEn, float duracionFija)
    {
        if (!PuedeEmpezar()) return false;
        _hayOtroTramo = false;
        PrepararTramo(tramoDeMovimiento, terminaEn, duracionFija, Avance.Suave);
        _co = StartCoroutine(Co_DesdeElSuelo(Mathf.Max(0f, alturaEnCabezas), desplazamiento, estado));
        return true;
    }

    /// <summary>
    /// Dos volteretas desde el suelo seguidas sin soltar el cuerpo entre medias (sin pasar por la
    /// locomoción ni devolver el control): p. ej. un salto en plancha que cae rodando. El segundo
    /// tramo sale de donde aterriza el primero. False si no se puede empezar.
    /// </summary>
    public bool EnDosTramos(Tramo primero, Tramo segundo)
    {
        if (!PuedeEmpezar()) return false;
        _hayOtroTramo = true;
        _otroTramo = segundo;
        PrepararTramo(primero.tramoDeMovimiento, primero.terminaEn, primero.duracionFija, primero.avance);
        _co = StartCoroutine(Co_DesdeElSuelo(Mathf.Max(0f, primero.altura), primero.desplazamiento, primero.estado));
        return true;
    }

    /// <summary>Una voltereta desde el suelo descrita por un tramo (con su forma de avanzar).</summary>
    public bool DesdeElSuelo(Tramo tramo)
    {
        if (!PuedeEmpezar()) return false;
        _hayOtroTramo = false;
        PrepararTramo(tramo.tramoDeMovimiento, tramo.terminaEn, tramo.duracionFija, tramo.avance);
        _velocidadDeSalida = Mathf.Max(0f, tramo.velocidadDeSalida);
        _fundidoDeEntrada = Mathf.Max(0f, tramo.fundidoDeEntrada);
        _fundidoDeSalida = Mathf.Max(0f, tramo.fundidoDeSalida);
        _co = StartCoroutine(Co_DesdeElSuelo(Mathf.Max(0f, tramo.altura), tramo.desplazamiento, tramo.estado));
        return true;
    }

    private void PrepararTramo(Vector2 tramoDeMovimiento, float terminaEn, float duracionFija, Avance avance)
    {
        _duracionFija = Mathf.Max(0f, duracionFija);
        _tramo = new Vector2(Mathf.Clamp01(tramoDeMovimiento.x), Mathf.Clamp(tramoDeMovimiento.y, tramoDeMovimiento.x + 0.01f, 1f));
        _terminaEn = Mathf.Clamp(terminaEn, _tramo.y, 1f);
        _avance = avance;
        _velocidadDeSalida = 0f;
        ProgresoDesdeElSuelo = 0f;
    }

    /// Parte del recorrido hecha en el punto 'u' (0-1) del tramo.
    private float Recorrido(float u, float salidaRelativa)
    {
        switch (_avance)
        {
            case Avance.Constante: return u;
            case Avance.Frenando: return u + (1f - salidaRelativa) * u * (1f - u);
            default: return Mathf.SmoothStep(0f, 1f, u);
        }
    }

    /// <summary>
    /// Voltereta en el aire: solo la animación; el movimiento lo lleva quien ya lo llevara. Al
    /// acabar vuelve a la caída si sigue en el aire o a la locomoción si ha tocado suelo. False si
    /// no se puede (otra voltereta, vuelo).
    /// </summary>
    public bool EnElAire(string estado = null)
    {
        if (!PuedeEmpezar(permitirLanzado: true)) return false;
        _co = StartCoroutine(Co_EnElAire(estado));
        return true;
    }

    /// <summary>Corta la voltereta en curso y devuelve todo lo que tomó. Idempotente.</summary>
    public void Cancelar()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        _hayOtroTramo = false;
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
        Tomar();
        while (true)
        {
            yield return Co_UnTramo(alturaEnCabezas, desplazamiento, estado);
            if (!_hayOtroTramo || _animacionAjena) break;

            // Sigue con el segundo tramo desde donde ha aterrizado, sin soltar el cuerpo.
            _hayOtroTramo = false;
            transform.position = _aterrizaje;
            var t = _otroTramo;
            PrepararTramo(t.tramoDeMovimiento, t.terminaEn, t.duracionFija, t.avance);
            alturaEnCabezas = Mathf.Max(0f, t.altura);
            desplazamiento = t.desplazamiento;
            estado = t.estado;
        }

        _co = null;
        Soltar();
    }

    private IEnumerator Co_UnTramo(float alturaEnCabezas, Vector3 desplazamiento, string estado)
    {
        _animacionAjena = false;
        int hash = string.IsNullOrEmpty(estado) ? _hashSuelo : Animator.StringToHash(estado);
        bool animado = Reproducir(hash, _fundidoDeEntrada > 0f ? _fundidoDeEntrada : fundido);

        Vector3 inicio = transform.position;
        desplazamiento.y = 0f;
        desplazamiento = RecortarContraParedes(inicio, desplazamiento);
        _aterrizaje = SueloEn(inicio + desplazamiento, inicio.y);
        float alto = AlturaDeLaCabeza() * alturaEnCabezas;
        float distanciaLibre = desplazamiento.magnitude;
        float salidaRelativa = distanciaLibre > 0.01f
            ? Mathf.Clamp01(_velocidadDeSalida * _duracionFija * (_tramo.y - _tramo.x) / distanciaLibre)
            : 0f;

        float empezo = Time.time;
        float tope = empezo + duracionMaxima;
        bool visto = false;
        while (Time.time < tope)
        {
            BajarBrazos();

            float t;
            if (_duracionFija > 0f)
            {
                t = (Time.time - empezo) / _duracionFija;
                if (t >= _terminaEn) break;
            }
            else if (animado)
            {
                t = TiempoNormalizado(hash);
                if (t >= 0f) visto = true;
                else if (visto || Time.time > empezo + 0.3f) { _animacionAjena = true; break; }   // otro estado lo ha sustituido
                if (t >= _terminaEn) break;
            }
            else
            {
                t = (Time.time - empezo) / duracionSinAnimacion;
                if (t >= 1f) break;
            }

            float u = Mathf.InverseLerp(_tramo.x, _tramo.y, Mathf.Max(0f, t));
            ProgresoDesdeElSuelo = u;
            float subida = t > _tramo.x && t < _tramo.y ? alto * 4f * u * (1f - u) : 0f;
            Vector3 p = Vector3.Lerp(inicio, _aterrizaje, Recorrido(u, salidaRelativa));
            p.y += subida;
            transform.position = p;
            yield return null;
        }
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
            if (Time.time > empezo + 0.15f && controller && controller.isActiveAndEnabled && !controller.IsAirborne) break;   // ya en el suelo
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
        RestaurarVelocidadDelAnimator();
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
            if (animator.HasState(0, salida))
                animator.CrossFadeInFixedTime(salida, _fundidoDeSalida > 0f ? _fundidoDeSalida : 0.15f, 0);
        }
        _fundidoDeEntrada = _fundidoDeSalida = 0f;
        OnTerminada?.Invoke();
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    private bool Reproducir(int hash) => Reproducir(hash, fundido);

    private bool Reproducir(int hash, float segundosDeFundido)
    {
        if (!animator || !animator.isActiveAndEnabled || !animator.HasState(0, hash)) return false;
        BajarBrazos();
        animator.CrossFadeInFixedTime(hash, segundosDeFundido, 0);
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
        // Si ha echado a volar, el parámetro es del vuelo: no se le quita.
        if (!activo && controller && controller.suppressAirMovement) return;
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
            if (EsUnPersonaje(h.collider)) continue;
            libre = Mathf.Min(libre, Mathf.Max(0f, h.distance - 0.3f));
        }
        return dir * libre;
    }

    /// Personajes (el propio, compañeros, NPCs, enemigos): no son pared. Antes solo se miraba la
    /// raíz con NPCSimpleAnimator y los compañeros que van detrás recortaban la esquiva hacia atrás
    /// a casi nada (rodaba en el sitio).
    private bool EsUnPersonaje(Collider c)
    {
        Transform t = c.transform;
        if (t.IsChildOf(transform)) return true;
        if (c is CharacterController) return true;
        return t.GetComponentInParent<NPCSimpleAnimator>() != null
            || t.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null
            || t.root.GetComponent<NPCSimpleAnimator>() != null;
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
