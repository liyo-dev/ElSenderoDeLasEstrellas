using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de interacción del mundo donde un NPC puede realizar una actividad ambiental.
/// Se añade a objetos de escena (bancos, mesas, camas) para que los NPCs puedan usarlos.
/// </summary>
public class NPCWorldPoint : MonoBehaviour
{
    [Header("Actividad")]
    [Tooltip("Tipo de actividad que se puede realizar en este punto")]
    public NPCAmbientActivity activityType = NPCAmbientActivity.SitMedium;

    [Tooltip("Estado del Animator que sustituye al bucle de su actividad mientras se está aquí (vacío: el de la actividad). " +
             "Para asientos con su propia postura, como el columpio (piernas adelante). Lo usa el jugador.")]
    public string estadoDelBucle = "";

    [Header("Posición de Interacción")]
    [Tooltip("Transform exacto desde donde el NPC realiza la actividad. Si es null, usa este transform.")]
    public Transform interactionPoint;

    [Tooltip("Si es true, el NPC girará mirando en la dirección Forward de interactionPoint al llegar")]
    public bool overrideFacing = true;

    [Header("Al levantarse")]
    [Tooltip("Punto donde queda de pie quien se levanta. Vacío = en el suelo delante del asiento, a 'Distancia Al Levantarse'.")]
    public Transform puntoAlLevantarse;

    [Min(0f), Tooltip("Distancia hacia delante del asiento (según hacia dónde mira quien está sentado) a la que queda de pie al levantarse, si no hay 'Punto Al Levantarse'.")]
    public float distanciaAlLevantarse = 0.45f;

    [Header("Interacción del Jugador")]
    [Tooltip("Distancia máxima desde el interactionPoint para que el jugador pueda activar este punto. " +
             "Evita que se pueda sentar desde muy lejos aunque esté dentro del rango global del InteractionDetector.")]
    public float requiredProximity = 1.5f;

    [Tooltip("Solo lo usa el jugador: los NPCs no lo eligen al buscar dónde sentarse (columpios, sitios con mecánica propia).")]
    public bool soloJugador;

    [Header("Prop (beber/comer)")]
    [Tooltip("Objeto que el personaje cogerá con la mano derecha al iniciar la actividad (ej: vaso, taza).")]
    public Transform prop;

    [Tooltip("Posición local del prop relativa al hueso de la mano derecha.")]
    public Vector3 propHandOffset = Vector3.zero;

    [Tooltip("Rotación local del prop relativa al hueso de la mano derecha (ángulos Euler).")]
    public Vector3 propHandRotation = Vector3.zero;

    [Header("Duración")]
    [Min(0f), Tooltip("Duración mínima de la ocupación (segundos)")]
    public float minOccupationDuration = 15f;

    [Min(0f), Tooltip("Duración máxima de la ocupación (segundos)")]
    public float maxOccupationDuration = 90f;

    [Header("Cadera sobre la tabla")]
    [Tooltip("Quien lo mueve con el asiento (un columpio) coloca al ocupante para que su cadera quede sobre la tabla, " +
             "midiendo la cadera de cada personaje en su pose de sentado.")]
    public bool apoyarLaCadera;
    [Tooltip("Centro de la cara de arriba de la tabla, en el espacio del punto de interacción.")]
    public Vector3 apoyoDeLaCadera = new Vector3(0f, 0.2f, -0.25f);
    [Range(0f, 0.25f), Tooltip("Altura del hueso de la cadera sobre la tabla.")]
    public float holguraDeLaCadera = 0.1f;

    [Header("Plano de cámara al sentarse (jugador)")]
    [Tooltip("Al sentarse el jugador, la cámara deja de seguirle y encuadra el asiento quieta, de frente y algo de lado, " +
             "desde donde no haya nada en medio. Para asientos que se mueven (columpios) o con poco sitio alrededor.")]
    public bool planoFijo;
    [Tooltip("Altura del punto al que mira la cámara sobre el punto de interacción.")]
    public float alturaDelPlano = 0.75f;
    [Min(1f), Tooltip("Distancia de la cámara a ese punto.")]
    public float distanciaDelPlano = 3.6f;
    [Range(-10f, 45f), Tooltip("Grados que mira hacia abajo.")]
    public float inclinacionDelPlano = 10f;

    private bool _isOccupied;
    private Transform _occupant;

    // Estado del prop
    private Transform _propOriginalParent;
    private Vector3 _propOriginalLocalPos;
    private Quaternion _propOriginalLocalRot;
    private bool _propAttached;

    public bool IsOccupied => _isOccupied;
    /// <summary>Quién lo ocupa ahora (null si está libre).</summary>
    public Transform Occupant => _isOccupied ? _occupant : null;
    public NPCAmbientActivity ActivityType => activityType;

    public Vector3 InteractionPosition =>
        interactionPoint != null ? interactionPoint.position : transform.position;

    public Quaternion InteractionRotation =>
        overrideFacing
            ? (interactionPoint != null ? interactionPoint.rotation : transform.rotation)
            : Quaternion.identity;

    /// <summary>
    /// Dónde queda de pie quien se levanta (a la altura del asiento; quien lo usa lo baja al suelo).
    /// <paramref name="haciaDelante"/> es hacia dónde mira el ocupante sentado.
    /// </summary>
    public Vector3 PosicionAlLevantarse(Vector3 haciaDelante)
    {
        if (puntoAlLevantarse != null) return puntoAlLevantarse.position;
        haciaDelante.y = 0f;
        if (haciaDelante.sqrMagnitude < 0.0001f) return InteractionPosition;
        return InteractionPosition + haciaDelante.normalized * distanciaAlLevantarse;
    }

    // ── Registro estático ───────────────────────────────────────────────────────
    private static readonly List<NPCWorldPoint> _all = new List<NPCWorldPoint>();
    public static IReadOnlyList<NPCWorldPoint> All => _all;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();
#endif

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); Release(); }

    // ── Ocupación ───────────────────────────────────────────────────────────────

    public bool TryOccupy(Transform occupant)
    {
        if (_isOccupied) return false;
        _isOccupied = true;
        _occupant   = occupant;
        OlvidarCadera();
        return true;
    }

    public void Release(Transform occupant = null)
    {
        if (occupant != null && _occupant != occupant) return;
        _isOccupied = false;
        _occupant   = null;
        OlvidarCadera();
    }

    // ── Cadera sobre la tabla (INC-1327) ─────────────────────────────────────────

    private const float RitmoDeAjusteDeCadera = 6f;
    private const float AjusteMaximoDeCadera = 0.6f;
    private Animator _animadorDelOcupante;
    private Transform _caderaDelOcupante;
    private int _bucleDelAsiento, _bucleDeLaActividad;
    private bool _caderaPreparada;
    private Vector3 _desfaseDeCadera;
    private float _pesoDeCadera;

    private void OlvidarCadera()
    {
        _animadorDelOcupante = null;
        _caderaDelOcupante = null;
        _caderaPreparada = false;
        _desfaseDeCadera = Vector3.zero;
        _pesoDeCadera = 0f;
    }

    private void PrepararCadera(Transform ocupante)
    {
        _caderaPreparada = true;
        _bucleDelAsiento = string.IsNullOrEmpty(estadoDelBucle) ? 0 : Animator.StringToHash(estadoDelBucle);
        _bucleDeLaActividad = Animator.StringToHash(activityType + "_Loop");
        foreach (var animador in ocupante.GetComponentsInChildren<Animator>())
        {
            if (animador == null || !animador.isHuman) continue;
            var cadera = animador.GetBoneTransform(HumanBodyBones.Hips);
            if (cadera == null) continue;
            _animadorDelOcupante = animador;
            _caderaDelOcupante = cadera;
            return;
        }
    }

    /// <summary>
    /// Dónde poner al ocupante para que su cadera quede sobre la tabla (<see cref="apoyoDeLaCadera"/>), con el
    /// giro <see cref="InteractionRotation"/>. Mide la cadera de su pose de sentado en cada fotograma, así que vale
    /// para cualquier personaje y cualquier pose; mientras se sienta (antes del bucle del asiento) se queda en el
    /// punto de interacción, y el ajuste entra y sale suave. Sin «Apoyar la cadera», el punto de interacción.
    /// Lo llama, en LateUpdate, quien lleva al ocupante con el asiento.
    /// </summary>
    public Vector3 PosicionDelOcupante(Transform ocupante)
    {
        Vector3 punto = InteractionPosition;
        if (!apoyarLaCadera || ocupante == null) return punto;
        if (!_caderaPreparada) PrepararCadera(ocupante);
        Quaternion giro = InteractionRotation;
        float dt = Time.deltaTime;

        bool sentado = false;
        if (_caderaDelOcupante != null && _animadorDelOcupante != null && _animadorDelOcupante.isActiveAndEnabled)
        {
            var estado = _animadorDelOcupante.GetCurrentAnimatorStateInfo(0);
            int h = estado.shortNameHash;
            sentado = !_animadorDelOcupante.IsInTransition(0) && (h == _bucleDelAsiento || h == _bucleDeLaActividad);
        }

        if (sentado)
        {
            // Cadera respecto al ocupante (no depende de dónde esté), pasada al espacio del asiento.
            Vector3 cadera = Quaternion.Inverse(ocupante.rotation) * (_caderaDelOcupante.position - ocupante.position);
            Vector3 medido = apoyoDeLaCadera + Vector3.up * holguraDeLaCadera - cadera;
            medido = Vector3.ClampMagnitude(medido, AjusteMaximoDeCadera);
            float k = 1f - Mathf.Exp(-RitmoDeAjusteDeCadera * dt);
            _desfaseDeCadera = _pesoDeCadera <= 0f ? medido : Vector3.Lerp(_desfaseDeCadera, medido, k);
        }
        _pesoDeCadera = Mathf.MoveTowards(_pesoDeCadera, sentado ? 1f : 0f, dt * 2.5f);
        float w = _pesoDeCadera * _pesoDeCadera * (3f - 2f * _pesoDeCadera);
        return punto + giro * (_desfaseDeCadera * w);
    }

    // ── Plano de cámara al sentarse (INC-1327) ──────────────────────────────────

    private static readonly float[] GirosDelPlano = { 35f, -35f, 60f, -60f, 0f, 90f, -90f, 120f, -120f };
    private const float RadioDelPlano = 0.3f;
    private static readonly RaycastHit[] s_golpesDelPlano = new RaycastHit[16];

    /// <summary>
    /// Plano fijo para el jugador sentado aquí: el punto al que mira (sobre el asiento en reposo) y el giro,
    /// inclinación y distancia de la cámara. Prueba de frente y algo de lado, luego más de lado, y se queda con
    /// el primero sin nada entre la cámara y el asiento (sin contar personajes); si ninguno está libre, el que
    /// más se aleja, acercando la cámara. False si el asiento no pide plano fijo.
    /// </summary>
    public bool TryPlanoDeCamara(Transform ocupante, out Vector3 pivote, out float giro, out float inclinacion, out float distancia)
    {
        pivote = InteractionPosition + Vector3.up * alturaDelPlano;
        Vector3 delante = InteractionRotation * Vector3.forward;
        delante.y = 0f;
        float frente = delante.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(delante.normalized, Vector3.up).eulerAngles.y : transform.eulerAngles.y;
        inclinacion = inclinacionDelPlano;
        giro = frente + 180f;
        distancia = distanciaDelPlano;
        if (!planoFijo) return false;

        float mejorLibre = -1f;
        for (int i = 0; i < GirosDelPlano.Length; i++)
        {
            float g = frente + 180f + GirosDelPlano[i];
            Vector3 haciaLaCamara = -(Quaternion.Euler(inclinacion, g, 0f) * Vector3.forward);
            float libre = DistanciaLibre(pivote, haciaLaCamara, distanciaDelPlano + RadioDelPlano, ocupante);
            if (libre >= distanciaDelPlano + RadioDelPlano)
            {
                giro = g;
                distancia = distanciaDelPlano;
                return true;
            }
            if (libre > mejorLibre)
            {
                mejorLibre = libre;
                giro = g;
            }
        }
        distancia = Mathf.Max(1.2f, mejorLibre - RadioDelPlano);
        return true;
    }

    /// Hasta dónde llega la cámara desde el pivote en esa dirección sin chocar con escenario.
    private float DistanciaLibre(Vector3 desde, Vector3 direccion, float hasta, Transform ocupante)
    {
        int n = Physics.SphereCastNonAlloc(desde, RadioDelPlano, direccion, s_golpesDelPlano, hasta, ~0, QueryTriggerInteraction.Ignore);
        float libre = hasta;
        for (int i = 0; i < n; i++)
        {
            var golpe = s_golpesDelPlano[i];
            var c = golpe.collider;
            if (c == null) continue;
            if (ocupante != null && c.transform.IsChildOf(ocupante)) continue;
            // Los personajes no tapan el plano: se mueven y se disuelven solos (CLAUDE.md, raycasts).
            if (c.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            // Lo que ya toca el pivote al salir (la tabla, el propio asiento) no cuenta.
            if (golpe.distance <= 0f) continue;
            if (golpe.distance < libre) libre = golpe.distance;
        }
        return libre;
    }

    /// <summary>True si este punto está ocupado y el ocupante es justo el transform indicado.</summary>
    public bool IsOccupiedBy(Transform occupant) => _isOccupied && occupant != null && _occupant == occupant;

    // ── Prop ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adjunta el prop a la mano derecha del ocupante. Llama justo antes de reproducir la animación.
    /// </summary>
    public void AttachPropToOccupant(Animator occupantAnimator)
    {
        if (prop == null || occupantAnimator == null || _propAttached) return;

        var handBone = occupantAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null) return;

        _propOriginalParent   = prop.parent;
        _propOriginalLocalPos = prop.localPosition;
        _propOriginalLocalRot = prop.localRotation;

        ColocarPropEnMano(prop, handBone, propHandOffset, propHandRotation);
        _propAttached = true;
    }

    /// Coloca un objeto en un hueso conservando su escala de mundo. Sirve también para secuencias.
    public static void ColocarPropEnMano(Transform objeto, Transform mano, Vector3 offset, Vector3 giro)
    {
        if (objeto == null || mano == null) return;
        objeto.SetParent(mano, true);
        objeto.localPosition = offset;
        objeto.localRotation = Quaternion.Euler(giro);
    }

    /// <summary>
    /// Devuelve el prop a su padre y posición/rotación originales.
    /// </summary>
    public void DetachProp()
    {
        if (prop == null || !_propAttached) return;

        prop.SetParent(_propOriginalParent, worldPositionStays: false);
        prop.localPosition = _propOriginalLocalPos;
        prop.localRotation = _propOriginalLocalRot;
        _propAttached = false;
    }

    // ── Búsqueda ────────────────────────────────────────────────────────────────

    public static bool TryFindNearest(
        Vector3 position,
        NPCAmbientActivity activityFilter,
        float maxDist,
        out NPCWorldPoint result)
    {
        result = null;
        float bestSqr = maxDist * maxDist;

        foreach (var wp in _all)
        {
            if (wp == null || wp._isOccupied || wp.soloJugador) continue;
            if (activityFilter != NPCAmbientActivity.None && wp.activityType != activityFilter) continue;

            // No ofrecer puntos cuya actividad todavía no tiene clips de animación reales asignados
            // en el Animator Controller (ver NPCSimpleAnimator.HasWorkingAnimation). Sin este
            // filtro, el NPC camina hasta el banco, PlayAmbientActivity ahora rechaza la actividad,
            // pero WalkToActivityState.Arrive() ya había teleportado el transform a la altura del
            // asiento — el NPC se queda de pie flotando sobre el banco en vez de sentado ("se
            // sientan en el aire"). Filtrando aquí, el NPC ni siquiera camina hacia ese punto: sigue
            // vagando con normalidad hasta que se importen los clips y HasWorkingAnimation() vuelva
            // a dar true para esta actividad.
            if (!NPCSimpleAnimator.HasWorkingAnimation(wp.activityType)) continue;

            float sqr = (wp.InteractionPosition - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                result  = wp;
            }
        }

        return result != null;
    }

    public static bool TryFindAny(Vector3 position, float maxDist, out NPCWorldPoint result)
        => TryFindNearest(position, NPCAmbientActivity.None, maxDist, out result);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isOccupied ? Color.red : Color.green;
        Vector3 pos = InteractionPosition;
        Gizmos.DrawWireSphere(pos, 0.35f);
        if (overrideFacing)
        {
            Quaternion rot = interactionPoint != null ? interactionPoint.rotation : transform.rotation;
            Gizmos.DrawRay(pos, rot * Vector3.forward * 0.8f);
        }
        UnityEditor.Handles.Label(pos + Vector3.up * 0.6f,
            $"{activityType}\n{(_isOccupied ? "Ocupado" : "Libre")}");
    }
#endif
}
