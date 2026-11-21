using System;
using System.Collections;
using UnityEngine;
using Core;
using Invector.vCharacterController;
using Sendero.Core.Feedback;

/// <summary>
/// Coreografía del contraataque a un hechizo devuelto en el momento justo (B). Corrección de Raúl
/// (20 nov, 18:00): la burbuja es solo la primera parte; no se queda dentro de ella ni sostiene la
/// bola en otra esfera.
/// <list type="number">
/// <item>Nos protegemos: la bola se clava en la burbuja un instante (destello y sacudida) y el golpe
/// nos arrastra hacia atrás en pose de aguantar. La burbuja se apaga aquí.</item>
/// <item>El hechizo (ya devuelto, <see cref="ProyectilDevuelto"/>) salta hacia arriba; el mundo va
/// a cámara lenta. Cogemos impulso, subimos al aire girando y lo atrapamos arriba.</item>
/// <item>En lo alto, cogemos impulso echándonos atrás con la bola por detrás de la cabeza y nos
/// doblamos hacia delante para devolverlo desde el aire; al soltarla, un retardo (casi congelado).</item>
/// <item>El empuje del lanzamiento nos echa hacia atrás: caemos así y se recupera el control.</item>
/// </list>
/// El personaje va en tiempo real (Animator sin escalar) mientras el mundo va lento, es
/// invulnerable toda la coreografía y no se le puede mover. Solo para lo lanzado: el cuerpo a
/// cuerpo sigue con su desvío. Lo pide <see cref="PlayerShieldController"/>. Ver INC-1352.
/// </summary>
[DisallowMultipleComponent]
public class ContraataqueCoreografiado : MonoBehaviour
{
    [Header("Parar el golpe")]
    [Tooltip("Pose de cuerpo entero aguantando el golpe mientras lo arrastra.")]
    [SerializeField] private string estadoDeParar = EstadoDePararPorDefecto;
    [Tooltip("Burbuja que se ve al parar (sin colisión), pegada al personaje mientras lo arrastra.")]
    [SerializeField] private GameObject burbujaAlParar;
    [Tooltip("Destello y ondas en el punto de la burbuja donde se clava la bola.")]
    [SerializeField] private GameObject golpeAlParar;
    [Tooltip("Segundos (reales) que la bola se queda clavada donde se para antes de subir a las manos.")]
    [SerializeField, Min(0f)] private float bolaClavada = 0.15f;
    [SerializeField, Min(0f)] private float sacudidaAlParar = 0.55f;
    [SerializeField, Min(0f)] private float duracionDeLaSacudida = 0.22f;

    [Header("Arrastre")]
    [Tooltip("Metros que el golpe arrastra al personaje hacia atrás (menos si hay una pared).")]
    [SerializeField, Min(0f)] private float arrastre = 0.7f;
    [SerializeField, Min(0.05f)] private float duracionDelArrastre = 0.2f;

    [Header("Rebote del hechizo")]
    [Tooltip("Metros sobre el hueso de la cabeza (la base del cráneo) a los que se atrapa la bola; con la cabeza grande, por encima del pelo.")]
    [SerializeField] private float alturaDeLaBola = AlturaDeLaBolaPorDefecto;

    /// <summary>Altura de la bola sobre la cabeza al atraparla (el montaje la pone en el cuerpo del grupo).</summary>
    public const float AlturaDeLaBolaPorDefecto = 1.1f;
    [Tooltip("Metros hacia delante (hacia el enemigo) que se va la bola al saltar hacia arriba.")]
    [SerializeField] private float adelanteDelRebote = 0.25f;
    [Tooltip("Parte de la subida del personaje que tarda la bola en llegar a lo alto (1 = llegan a la vez; menos = la bola espera arriba).")]
    [SerializeField, Range(0.3f, 1f)] private float llegadaDeLaBola = 0.8f;
    [Tooltip("Velocidad del mundo desde que salta el hechizo hasta devolverlo.")]
    [SerializeField, Range(0.05f, 1f)] private float camaraLenta = 0.3f;
    [SerializeField, Min(0)] private int capaDeBrazos = 1;

    [Header("Subir al aire")]
    [Tooltip("Segundos (reales) cogiendo impulso en el suelo (agachado, en la pose de aguantar) antes de subir.")]
    [SerializeField, Min(0f)] private float antesDeSaltar = 0.1f;
    [SerializeField] private string estadoEnElAire = "JumpAir_InPlace_NoWeapon";
    [SerializeField, Min(0f)] private float alturaDelSalto = 1.1f;
    [SerializeField, Min(0.05f)] private float duracionDeLaSubida = 0.35f;
    [Tooltip("Vueltas completas en el sitio durante la subida.")]
    [SerializeField, Min(0f)] private float vueltas = 1f;
    [Tooltip("Destello pequeño al atrapar la bola arriba (vacío = el del golpe, más pequeño).")]
    [SerializeField] private GameObject alAtrapar;

    [Header("Coger impulso")]
    [Tooltip("Pose de cuerpo entero echado atrás con la bola por detrás de la cabeza, en lo alto del salto.")]
    [SerializeField] private string estadoDeImpulso = EstadoDeImpulsoPorDefecto;
    [Tooltip("Segundos (reales) cogiendo impulso, quieto en lo alto, antes de doblarse para lanzar.")]
    [SerializeField, Min(0f)] private float duracionDelImpulso = 0.28f;
    [Tooltip("Fundido hacia la pose de impulso (más largo = se ve cómo se echa atrás).")]
    [SerializeField, Min(0f)] private float fundidoDelImpulso = 0.16f;
    [Tooltip("Fundido hacia la pose de lanzar (corto = latigazo).")]
    [SerializeField, Min(0f)] private float fundidoDeLanzar = 0.07f;

    [Header("Lanzar")]
    [Tooltip("Estado de la capa base con las manos hacia delante.")]
    [SerializeField] private string estadoDeLanzar = EstadoDeLanzarPorDefecto;
    [Tooltip("Segundos (reales) entre empezar el gesto y soltar la bola.")]
    [SerializeField, Min(0f)] private float antesDeSoltar = 0.12f;
    [Tooltip("Retardo al soltar: el mundo casi parado este rato.")]
    [SerializeField, Min(0f)] private float retardo = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float velocidadDelRetardo = 0.05f;

    [Header("Caída")]
    [Tooltip("Metros que el empuje del lanzamiento echa al personaje hacia atrás mientras cae.")]
    [SerializeField, Min(0f)] private float retroceso = 1.6f;
    [SerializeField, Min(0.05f)] private float duracionDeLaCaida = 0.3f;
    [SerializeField] private string estadoDeLocomocion = "Free Locomotion";
    [Tooltip("Parámetro bool que bloquea la transición de cualquier estado a Falling (el del vuelo).")]
    [SerializeField] private string parametroDeBloqueoAereo = "isFlying";
    [SerializeField, Min(0f)] private float fundido = 0.08f;

    /// <summary>Poses propias del contraataque (las crea la tabla de poses de actuación).</summary>
    public const string EstadoDePararPorDefecto = "Contraataque_Parar";
    public const string EstadoDeImpulsoPorDefecto = "Contraataque_Impulso";
    public const string EstadoDeLanzarPorDefecto = "Contraataque_Lanzar";
    /// <summary>Ya no se usa (la bola no se sostiene sobre la cabeza desde el 20 nov); la pose sigue en la tabla.</summary>
    public const string EstadoDeSostenerPorDefecto = "Contraataque_Sostener";
    /// <summary>Ya no se usa; el montaje antiguo creaba este parámetro en el Animator.</summary>
    public const string ParametroDeSostenerPorDefecto = "VelocidadDeSostener";

    private vThirdPersonController _controller;
    private Animator _animator;
    private Rigidbody _body;
    private PlayerActionManager _acciones;
    private PlayerHealthSystem _salud;
    private Coroutine _co;
    private ProyectilDevuelto _bola;
    private Transform _ancla;
    private bool _tomado;
    private bool _controladorAntes, _bloqueoAntes, _aireAntes, _cinematicoAntes;
    private AnimatorUpdateMode _modoAntes;
    private float _pesoDeBrazosAntes;
    private int _hashParar, _hashImpulso, _hashAire, _hashLanzar, _hashLocomocion, _hashBloqueo;
    private bool _tieneBloqueo;
    private Vector3 _dondeSeClava;
    private readonly RaycastHit[] _golpes = new RaycastHit[8];

    /// <summary>Hay una coreografía en curso.</summary>
    public bool EnCurso => _co != null;

    /// <summary>Empieza (true) o termina la coreografía.</summary>
    public event Action<bool> OnCoreografia;

    void Awake()
    {
        _controller = GetComponentInParent<vThirdPersonController>();
        if (_controller) _controller.TryGetComponent(out _animator);
        if (!_animator) _animator = GetComponentInChildren<Animator>();
        if (_controller) _controller.TryGetComponent(out _body);
        _acciones = GetComponentInParent<PlayerActionManager>();
        _salud = GetComponentInParent<PlayerHealthSystem>();

        _hashParar = Animator.StringToHash(estadoDeParar);
        _hashImpulso = Animator.StringToHash(estadoDeImpulso);
        _hashAire = Animator.StringToHash(estadoEnElAire);
        _hashLanzar = Animator.StringToHash(estadoDeLanzar);
        _hashLocomocion = Animator.StringToHash(estadoDeLocomocion);
        _hashBloqueo = Animator.StringToHash(parametroDeBloqueoAereo);
        _tieneBloqueo = TieneParametro(_hashBloqueo, AnimatorControllerParameterType.Bool);

        _ancla = new GameObject("Ancla del contraataque").transform;
        _ancla.SetParent(transform, false);
    }

    void OnDisable() => Cortar();

    /// <summary>
    /// Empieza la coreografía con una bola ya devuelta. False si no se puede (en el aire, ya en
    /// curso, sin bola): entonces la bola sale sola como siempre.
    /// </summary>
    public bool Empezar(ProyectilDevuelto bola, Transform objetivo, Vector3 haciaDonde)
    {
        if (!isActiveAndEnabled || _co != null || bola == null) return false;
        if (_controller && (!_controller.PisandoSuelo || _controller.suppressAirMovement)) return false;

        _bola = bola;
        Vector3 frente = objetivo != null ? objetivo.position - transform.position : haciaDonde;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.0001f) frente = transform.forward;
        frente.Normalize();

        // La bola se queda clavada donde la para la burbuja; luego sigue al ancla, que la hace saltar.
        _dondeSeClava = _bola.transform.position;
        _ancla.position = _dondeSeClava;
        _bola.Sostener(_ancla, bolaClavada);
        var pool = VfxPoolService.Instance;
        if (pool != null)
        {
            Vector3 desdeMi = _dondeSeClava - (transform.position + Vector3.up);
            if (golpeAlParar != null)
                pool.Play(golpeAlParar, _dondeSeClava, Quaternion.LookRotation(desdeMi.sqrMagnitude > 0.0001f ? desdeMi : frente), 0.8f);
            // La burbuja solo dura lo que se aguanta el golpe: se apaga antes de subir. Del color del escudo.
            if (burbujaAlParar != null)
            {
                var burbuja = pool.Play(burbujaAlParar, transform.position, transform.rotation, Mathf.Max(duracionDelArrastre, bolaClavada) + 0.05f, transform);
                var escudo = PlayerShieldController.De(this);
                if (burbuja != null && escudo != null)
                    PlayerShieldController.PintarConElemento(burbuja.GetComponentsInChildren<Renderer>(true), escudo.ElementoDelEscudo);
            }
        }
        if (sacudidaAlParar > 0f) FeedbackService.CameraShake(sacudidaAlParar, duracionDeLaSacudida);

        _co = StartCoroutine(Co_Coreografia(frente));
        return true;
    }

    private IEnumerator Co_Coreografia(Vector3 frente)
    {
        Tomar();
        OnCoreografia?.Invoke(true);
        float aguantar = Mathf.Max(duracionDelArrastre, bolaClavada);
        float total = aguantar + antesDeSaltar + duracionDeLaSubida + duracionDelImpulso + antesDeSoltar + retardo + duracionDeLaCaida;
        if (_salud) _salud.HacerInvulnerable(total + 0.25f, parpadeo: false);
        transform.rotation = Quaternion.LookRotation(frente, Vector3.up);

        // 1. Nos protegemos: el golpe nos arrastra hacia atrás, aguantando; la bola, clavada en la burbuja.
        PonerBrazos(0f);
        Reproducir(_hashParar, 0);
        Vector3 inicio = transform.position;
        Vector3 atras = RecortarContraParedes(inicio, -frente * arrastre);
        Vector3 suelo = SueloEn(inicio + atras, inicio.y);
        for (float t = 0f; t < aguantar; t += Time.unscaledDeltaTime)
        {
            float u = Mathf.Clamp01(t / duracionDelArrastre);
            u = 1f - (1f - u) * (1f - u);
            transform.position = Vector3.Lerp(inicio, suelo, u);
            yield return null;
        }
        transform.position = suelo;

        // 2. El hechizo salta hacia arriba (mundo lento); cogemos impulso, subimos girando y lo
        //    atrapamos en lo alto.
        TimeScaleArbiterService.Request(this, camaraLenta);
        Vector3 base0 = transform.position;
        float alturaDeLaCabeza = AlturaDeLaCabeza();
        Vector3 arribaDelTodo = base0 + Vector3.up * (alturaDelSalto + alturaDeLaCabeza + alturaDeLaBola) + frente * adelanteDelRebote;
        Vector3 desde = _dondeSeClava;
        float tiempoDeSubida = antesDeSaltar + duracionDeLaSubida;
        float llegaEn = Mathf.Max(0.05f, tiempoDeSubida * llegadaDeLaBola);
        float giroInicial = transform.eulerAngles.y;
        bool enElAire = false;
        for (float t = 0f; t < tiempoDeSubida; t += Time.unscaledDeltaTime)
        {
            // La bola: sube deprisa y frena arriba (como algo que salta).
            float b = Mathf.Clamp01(t / llegaEn);
            Vector3 p = Vector3.Lerp(desde, arribaDelTodo, Mathf.SmoothStep(0f, 1f, b));
            p.y = Mathf.Lerp(desde.y, arribaDelTodo.y, Mathf.Sin(b * Mathf.PI * 0.5f));
            _ancla.position = p;

            // Nosotros: un instante agachados cogiendo impulso y arriba, girando en el sitio.
            if (t >= antesDeSaltar)
            {
                if (!enElAire) { enElAire = true; Reproducir(_hashAire, 0); }
                float u = Mathf.Clamp01((t - antesDeSaltar) / duracionDeLaSubida);
                transform.position = base0 + Vector3.up * (alturaDelSalto * Mathf.Sin(u * Mathf.PI * 0.5f));
                transform.rotation = Quaternion.Euler(0f, giroInicial + 360f * vueltas * Mathf.SmoothStep(0f, 1f, u), 0f);
            }
            yield return null;
        }
        transform.position = base0 + Vector3.up * alturaDelSalto;
        transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
        ColocarAncla();
        if (_bola != null && VfxPoolService.Instance != null)
        {
            var destello = alAtrapar != null ? alAtrapar : golpeAlParar;
            if (destello != null)
            {
                var d = VfxPoolService.Instance.Play(destello, _bola.transform.position, Quaternion.LookRotation(frente), 0.5f);
                if (d != null && alAtrapar == null) d.localScale *= 0.5f;
            }
        }

        // 3. Coge impulso quieto en lo alto: se echa atrás con la bola por detrás de la cabeza.
        PonerBrazos(0f);
        Reproducir(_hashImpulso, 0, fundidoDelImpulso);
        for (float t = 0f; t < duracionDelImpulso; t += Time.unscaledDeltaTime)
        {
            ColocarAncla(delante: -frente * 0.7f);
            yield return null;
        }

        // 4. Se dobla hacia delante y lo devuelve desde el aire; al soltar, el retardo.
        Reproducir(_hashLanzar, 0, fundidoDeLanzar);
        for (float t = 0f; t < antesDeSoltar; t += Time.unscaledDeltaTime)
        {
            ColocarAncla(delante: frente);
            yield return null;
        }
        TimeScaleArbiterService.Request(this, velocidadDelRetardo);
        for (float t = 0f; t < retardo; t += Time.unscaledDeltaTime) yield return null;
        if (_bola != null) _bola.Soltar();
        _bola = null;
        TimeScaleArbiterService.Release(this);

        // 5. El empuje del lanzamiento lo echa hacia atrás mientras cae.
        Vector3 arriba = transform.position;
        Vector3 haciaAtras = RecortarContraParedes(new Vector3(arriba.x, base0.y, arriba.z), -frente * retroceso);
        Vector3 aterrizaje = SueloEn(arriba + haciaAtras, base0.y);
        for (float t = 0f; t < duracionDeLaCaida; t += Time.unscaledDeltaTime)
        {
            float u = t / duracionDeLaCaida;
            Vector3 p = Vector3.Lerp(arriba, aterrizaje, 1f - (1f - u) * (1f - u));
            p.y = Mathf.Lerp(arriba.y, aterrizaje.y, u * u);
            transform.position = p;
            yield return null;
        }
        transform.position = aterrizaje;

        _co = null;
        Soltar();
    }

    /// Corta lo que esté en curso y devuelve todo; la bola sostenida sale. Idempotente.
    private void Cortar()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        if (_bola != null) { _bola.Soltar(); _bola = null; }
        TimeScaleArbiterService.Release(this);
        Soltar();
    }

    private void Tomar()
    {
        if (_tomado) return;
        _tomado = true;
        if (_acciones) _acciones.PushMode(ActionMode.Stunned);
        if (_controller)
        {
            _controladorAntes = _controller.enabled;
            _bloqueoAntes = _controller.lockMovement;
            _aireAntes = _controller.suppressAirMovement;
            _controller.ResetInputSmoothing();
            _controller.lockMovement = true;
            _controller.suppressAirMovement = true;
            _controller.enabled = false;
        }
        if (_body)
        {
            _cinematicoAntes = _body.isKinematic;
            if (!_body.isKinematic) _body.linearVelocity = Vector3.zero;
            _body.isKinematic = true;
        }
        if (_animator)
        {
            _modoAntes = _animator.updateMode;
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            _pesoDeBrazosAntes = _animator.layerCount > capaDeBrazos ? _animator.GetLayerWeight(capaDeBrazos) : 0f;
            if (_tieneBloqueo) _animator.SetBool(_hashBloqueo, true);
        }
    }

    private void Soltar()
    {
        if (!_tomado) return;
        _tomado = false;
        if (_body)
        {
            _body.isKinematic = _cinematicoAntes;
            if (!_body.isKinematic) _body.linearVelocity = Vector3.zero;
        }
        if (_controller)
        {
            _controller.lockMovement = _bloqueoAntes;
            _controller.suppressAirMovement = _aireAntes;
            _controller.enabled = _controladorAntes;
        }
        if (_animator)
        {
            _animator.updateMode = _modoAntes;
            if (_tieneBloqueo) _animator.SetBool(_hashBloqueo, false);
            PonerBrazos(_pesoDeBrazosAntes);
            if (_animator.isActiveAndEnabled && _animator.HasState(0, _hashLocomocion))
                _animator.CrossFadeInFixedTime(_hashLocomocion, 0.15f, 0);
        }
        if (_acciones) _acciones.PopMode(ActionMode.Stunned);
        OnCoreografia?.Invoke(false);
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    private void Reproducir(int hash, int capa, float fundidoPropio = -1f)
    {
        if (!_animator || !_animator.isActiveAndEnabled || capa >= _animator.layerCount || !_animator.HasState(capa, hash)) return;
        _animator.CrossFadeInFixedTime(hash, fundidoPropio >= 0f ? fundidoPropio : fundido, capa);
    }

    private void PonerBrazos(float peso)
    {
        if (_animator && capaDeBrazos > 0 && _animator.layerCount > capaDeBrazos)
            _animator.SetLayerWeight(capaDeBrazos, peso);
    }

    private float TiempoNormalizado(int hash, int capa)
    {
        if (!_animator || capa >= _animator.layerCount) return 1f;
        if (_animator.IsInTransition(capa))
        {
            var siguiente = _animator.GetNextAnimatorStateInfo(capa);
            if (siguiente.shortNameHash == hash) return siguiente.normalizedTime;
        }
        var actual = _animator.GetCurrentAnimatorStateInfo(capa);
        return actual.shortNameHash == hash ? actual.normalizedTime : 0f;
    }

    private bool TieneParametro(int hash, AnimatorControllerParameterType tipo)
    {
        if (!_animator || _animator.runtimeAnimatorController == null) return false;
        for (int i = 0; i < _animator.parameterCount; i++)
        {
            var p = _animator.GetParameter(i);
            if (p.nameHash == hash && p.type == tipo) return true;
        }
        return false;
    }

    // ── Geometría ─────────────────────────────────────────────────────────────

    /// Metros del suelo (los pies) a la base del cráneo.
    private float AlturaDeLaCabeza()
    {
        if (_animator && _animator.isHuman)
        {
            var hueso = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (hueso) return Mathf.Max(0.3f, hueso.position.y - transform.position.y);
        }
        return 1.2f;
    }

    /// La bola, sobre la cabeza; al lanzar, un poco por delante de las manos.
    private void ColocarAncla(Vector3 delante = default)
    {
        Vector3 cabeza = transform.position + Vector3.up * 1.2f;
        if (_animator && _animator.isHuman)
        {
            var hueso = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (hueso) cabeza = hueso.position;
        }
        _ancla.position = cabeza + Vector3.up * alturaDeLaBola + delante * 0.6f;
    }

    private Vector3 RecortarContraParedes(Vector3 desde, Vector3 desplazamiento)
    {
        float dist = desplazamiento.magnitude;
        if (dist < 0.01f) return Vector3.zero;
        Vector3 dir = desplazamiento / dist;
        int n = Physics.SphereCastNonAlloc(desde + Vector3.up, 0.3f, dir, _golpes, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);
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

    private Vector3 SueloEn(Vector3 punto, float yPorDefecto)
    {
        LayerMask capas = _controller ? _controller.groundLayer : (LayerMask)~0;
        if (Physics.Raycast(punto + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, capas, QueryTriggerInteraction.Ignore))
            punto.y = hit.point.y;
        else
            punto.y = yPorDefecto;
        return punto;
    }
}
