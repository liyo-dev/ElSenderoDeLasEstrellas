using System.Collections;
using UnityEngine;

/// <summary>
/// Base de los enemigos del mundo abierto que no son jefes (Fantasma, Beholder, Mímico, Imp
/// ladrón, Roca). Reúne lo que todos comparten y deja a cada uno solo su comportamiento:
/// <list type="bullet">
/// <item>Objetivo: <see cref="CombatTargetProvider"/>, revisado cada medio segundo.</item>
/// <item>Combate: <see cref="Targetable"/>, <see cref="EnemyMarker"/> y
/// <see cref="ActiveCombatRegistry"/> (enemigo menor: no engancha la cámara).</item>
/// <item>Pausa mientras hay un diálogo abierto.</item>
/// <item>Muerte: animación, colisiones fuera y desaparición; los orbes y la Esencia los suelta
/// <see cref="OrbDropper"/> por su cuenta (INC-635).</item>
/// <item>Daño al objetivo con la defensa del jugador incluida (<see cref="PlayerHealthSystem"/>).</item>
/// </list>
/// Las reglas de «cuándo se le hace daño» de cada enemigo van como <see cref="IFiltroDeDano"/>.
/// Ver INC-722.
/// </summary>
[RequireComponent(typeof(Damageable))]
public abstract class EnemigoDelMundo : MonoBehaviour
{
    [Header("Detección")]
    [Tooltip("Metros a los que ve al objetivo y entra en combate.")]
    [SerializeField, Min(0.5f)] protected float alcanceDeDeteccion = 10f;
    [Tooltip("Metros a los que deja de perseguirlo y sale de combate.")]
    [SerializeField, Min(0.5f)] protected float alcanceDePerdida = 18f;

    [Header("Animación")]
    [Tooltip("Animator del modelo (puede estar en un hijo). Vacío = el primero que haya.")]
    [SerializeField] protected Animator animador;
    [Tooltip("Segundos de fundido entre animaciones.")]
    [SerializeField, Min(0f)] private float fundido = 0.12f;

    [Header("Muerte")]
    [Tooltip("Estado del Animator al morir (vacío = sin animación).")]
    [SerializeField] private string animacionDeMuerte = "Die";
    [Tooltip("Segundos desde la muerte hasta que desaparece.")]
    [SerializeField, Min(0.1f)] private float segundosHastaDesaparecer = 2.5f;
    [Tooltip("Efecto al desaparecer (opcional).")]
    [SerializeField] protected GameObject vfxAlDesaparecer;
    [Tooltip("Puntos de Sintonía Estelar que otorga al morir (INC-847).")]
    [SerializeField, Min(0)] protected int puntosDeSintonia = 20;

    protected Damageable Vida { get; private set; }
    protected Transform Objetivo { get; private set; }
    protected bool Muerto { get; private set; }
    protected bool EnCombate { get; private set; }

    private Targetable _apuntable;
    private Collider[] _colisiones;
    private float _proximaRevision;
    private int _animacionActual;
    private bool _pausado;

    private static readonly Collider[] BufferCercano = new Collider[32];

    protected virtual void Awake()
    {
        Vida = GetComponent<Damageable>();
        // Esta clase decide cuándo desaparece el cuerpo (tras la animación de muerte).
        Vida.SetDestroyOnDeath(false);
        if (animador == null) animador = GetComponentInChildren<Animator>(true);
        _apuntable = GetComponent<Targetable>();
        if (_apuntable == null) _apuntable = gameObject.AddComponent<Targetable>();
        if (GetComponent<EnemyMarker>() == null) gameObject.AddComponent<EnemyMarker>();
        _colisiones = GetComponentsInChildren<Collider>(true);
        SombraDeContacto.Asegurar(gameObject);
    }

    protected virtual void OnEnable()
    {
        Vida.OnDamagedBy += AlRecibirDanoInterno;
        Vida.OnDied += AlMorirInterno;
    }

    protected virtual void OnDisable()
    {
        Vida.OnDamagedBy -= AlRecibirDanoInterno;
        Vida.OnDied -= AlMorirInterno;
        AmenazasAlJugador.RetirarAnuncio(transform);
        SalirDeCombate();
    }

    private void Update()
    {
        if (Muerto) return;
        bool pausa = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
        if (pausa != _pausado)
        {
            _pausado = pausa;
            AlCambiarPausa(pausa);
        }
        if (pausa) return;

        if (Time.time >= _proximaRevision)
        {
            _proximaRevision = Time.time + 0.5f;
            Objetivo = CombatTargetProvider.GetNearestTarget(transform.position);
        }
        Pensar(Time.deltaTime);
    }

    /// <summary>Comportamiento de cada frame (no se llama muerto ni con un diálogo abierto).</summary>
    protected abstract void Pensar(float dt);

    /// <summary>Se abre o se cierra un diálogo: parar o reanudar lo que se esté moviendo.</summary>
    protected virtual void AlCambiarPausa(bool pausado) { }

    /// <summary>Ha recibido daño (después de sus reglas de daño).</summary>
    protected virtual void AlRecibirDano(float cantidad, GameObject quien) { }

    /// <summary>Acaba de morir: parar lo propio. La base pone la animación y lo hace desaparecer.</summary>
    protected virtual void AlMorir() { }

    // ── Objetivo y combate ───────────────────────────────────────────────

    protected bool HayObjetivo => Objetivo != null;

    protected Vector3 HaciaElObjetivoPlano()
    {
        if (Objetivo == null) return Vector3.zero;
        Vector3 d = Objetivo.position - transform.position;
        d.y = 0f;
        return d;
    }

    protected float DistanciaAlObjetivo()
    {
        if (Objetivo == null) return float.PositiveInfinity;
        return HaciaElObjetivoPlano().magnitude;
    }

    protected void EntrarEnCombate()
    {
        if (EnCombate || Muerto) return;
        EnCombate = true;
        _apuntable.isInActiveCombat = true;
        ActiveCombatRegistry.RegisterNPC(gameObject, allowsCameraLock: false);
    }

    protected void SalirDeCombate()
    {
        if (!EnCombate) return;
        EnCombate = false;
        if (_apuntable != null) _apuntable.isInActiveCombat = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
    }

    /// <summary>Se puede apuntar o no (p. ej. un fantasma de día).</summary>
    protected void Apuntable(bool si)
    {
        if (_apuntable != null) _apuntable.enabled = si;
    }

    // ── Golpes al objetivo ───────────────────────────────────────────────

    /// <summary>
    /// Hace daño a quien sea el objetivo: al jugador por su sistema de vida (con su defensa) y a
    /// cualquier otro por su Damageable. True si el golpe ha entrado.
    /// </summary>
    protected bool Danar(Transform quien, float dano)
    {
        if (quien == null || dano <= 0f) return false;
        var jugador = quien.GetComponentInParent<PlayerHealthSystem>();
        if (jugador != null) return jugador.TakeDamage(dano);
        var vida = quien.GetComponentInParent<Damageable>();
        if (vida != null && vida.IsAlive)
        {
            vida.TakeDamage(dano, gameObject);
            return true;
        }
        var otro = quien.GetComponentInParent<IDamageable>();
        if (otro != null && otro.IsAlive)
        {
            otro.TakeDamage(dano);
            return true;
        }
        return false;
    }

    /// <summary>Aparta al objetivo (solo el jugador sabe ser empujado).</summary>
    protected static void Empujar(Transform quien, Vector3 direccion, float fuerza)
    {
        if (quien == null) return;
        var jugador = quien.GetComponentInParent<PlayerHealthSystem>();
        if (jugador != null) jugador.Empujar(direccion, fuerza);
    }

    // ── Animación, efectos y sonido ──────────────────────────────────────

    protected void Animar(string estado)
    {
        if (animador == null || string.IsNullOrEmpty(estado)) return;
        int hash = Animator.StringToHash(estado);
        if (hash == _animacionActual) return;
        if (!animador.HasState(0, hash)) return;
        _animacionActual = hash;
        animador.CrossFadeInFixedTime(hash, fundido, 0);
    }

    /// <summary>Vuelve a poner la animación aunque ya fuera la actual (golpes repetidos).</summary>
    protected void AnimarDeNuevo(string estado)
    {
        _animacionActual = 0;
        Animar(estado);
    }

    protected static Transform Efecto(GameObject prefab, Vector3 posicion, float segundos, Transform padre = null)
    {
        if (prefab == null || VfxPoolService.Instance == null) return null;
        return VfxPoolService.Instance.Play(prefab, posicion, Quaternion.identity, segundos, padre);
    }

    protected void Sonar(string clave)
    {
        if (!string.IsNullOrEmpty(clave) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(clave, 1f, transform.position);
    }

    protected void MirarHacia(Vector3 direccion, float gradosPorSegundo)
    {
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;
        var meta = Quaternion.LookRotation(direccion);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, meta, gradosPorSegundo * Time.deltaTime);
    }

    // ── Detección de lo que tiene cerca ──────────────────────────────────

    /// <summary>
    /// ¿Hay cerca algo con el componente T que cumpla la condición? Sirve para saber qué ha
    /// provocado un golpe (una bola devuelta, una luz) cuando el golpe no lo dice.
    /// </summary>
    public static bool HayCerca<T>(Vector3 centro, float radio, System.Predicate<T> condicion = null) where T : Component
    {
        int n = Physics.OverlapSphereNonAlloc(centro, radio, BufferCercano, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = BufferCercano[i] != null ? BufferCercano[i].GetComponentInParent<T>() : null;
            if (c != null && (condicion == null || condicion(c))) return true;
        }
        return false;
    }

    // ── Muerte ───────────────────────────────────────────────────────────

    private void AlRecibirDanoInterno(float cantidad, GameObject quien)
    {
        if (!Muerto) AlRecibirDano(cantidad, quien);
    }

    private void AlMorirInterno()
    {
        if (Muerto) return;
        Muerto = true;
        AmenazasAlJugador.RetirarAnuncio(transform);
        SalirDeCombate();
        StopAllCoroutines();
        if (puntosDeSintonia > 0 && SintoniaEstelarService.Instance != null)
        {
            SintoniaEstelarService.Instance.SumarSintonia(puntosDeSintonia, transform.position);
        }
        AlMorir();
        _animacionActual = 0;
        Animar(animacionDeMuerte);
        StartCoroutine(Desaparecer());
    }

    private IEnumerator Desaparecer()
    {
        yield return new WaitForSeconds(Mathf.Min(1f, segundosHastaDesaparecer));
        foreach (var c in _colisiones) if (c != null) c.enabled = false;
        float resto = segundosHastaDesaparecer - 1f;
        if (resto > 0f) yield return new WaitForSeconds(resto);
        Efecto(vfxAlDesaparecer, transform.position + Vector3.up * 0.5f, 2f);
        Destroy(gameObject);
    }

    /// <summary>Se va sin morir (huye, se esfuma): sin orbes ni animación de muerte.</summary>
    protected void Esfumarse()
    {
        if (Muerto) return;
        Muerto = true;
        AmenazasAlJugador.RetirarAnuncio(transform);
        SalirDeCombate();
        StopAllCoroutines();
        Efecto(vfxAlDesaparecer, transform.position + Vector3.up * 0.5f, 2f);
        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcanceDeDeteccion);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, alcanceDePerdida);
    }
}
