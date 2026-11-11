using UnityEngine;

/// Endereza a cualquier personaje sentado. Las animaciones de sentado (SitGround, SitLow, SitMedium,
/// SitHigh: una distinta según el asiento) están hechas para cuerpos de proporciones normales; con
/// la cabeza grande de los chibis la espalda curvada y la cabeza gacha se leen como «encorvados».
///
/// Después de la animación (LateUpdate) lleva el tronco y la cabeza hacia la vertical una parte.
/// Al ser «hacia la vertical» y no un giro fijo, la corrección se adapta sola a cada animación de
/// sentado: la que ya va derecha apenas se toca. Entra y sale con el Begin y el Exit del sentado,
/// así que no hay tirón al sentarse ni al levantarse.
///
/// Lo añaden solos NPCSimpleAnimator (todos los NPCs) y PlayerAmbientActivityHandler (Will): vale
/// en juego y en cinemáticas, sin que nadie tenga que acordarse. Antes solo lo hacía la cinemática
/// final, y solo con la cabeza y dos de los cuatro asientos (INC-866).
///
/// Además, a un NPC sentado en juego (no en una cinemática, que ya coloca a cada uno con su guion)
/// lo asienta de verdad: mide dónde le ha quedado la cadera con la animación puesta y lo mueve lo
/// justo para que quede sobre el asiento que tiene debajo o al lado, ni flotando ni hundido. Así da
/// igual que el punto de salida estuviera un poco mal, que otro sistema lo haya movido o que el
/// asiento sea de otra altura (INC-1040).
[DisallowMultipleComponent]
public sealed class PosturaDeSentado : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Cuánto se endereza la espalda (0 = como la animación, 1 = vertical).")]
    private float espalda = 0.45f;
    [SerializeField, Range(0f, 1f), Tooltip("Cuánto se levanta la cabeza hacia el frente.")]
    private float cabeza = 0.7f;

    private static readonly int[] Sentados =
    {
        Animator.StringToHash("SitGround_Loop"), Animator.StringToHash("SitLow_Loop"),
        Animator.StringToHash("SitMedium_Loop"), Animator.StringToHash("SitHigh_Loop"),
    };
    private static readonly int[] Sentandose =
    {
        Animator.StringToHash("SitGround_Begin"), Animator.StringToHash("SitLow_Begin"),
        Animator.StringToHash("SitMedium_Begin"), Animator.StringToHash("SitHigh_Begin"),
    };
    private static readonly int[] Levantandose =
    {
        Animator.StringToHash("SitGround_Exit"), Animator.StringToHash("SitLow_Exit"),
        Animator.StringToHash("SitMedium_Exit"), Animator.StringToHash("SitHigh_Exit"),
    };

    [SerializeField, Tooltip("Asentar en el asiento real a los NPCs sentados en juego (cadera sobre la tabla).")]
    private bool ajustarAlAsiento = true;
    [SerializeField, Range(0f, 0.25f), Tooltip("Altura de la cadera (el hueso) sobre la tabla del asiento.")]
    private float holguraDeCadera = 0.1f;

    private const float EsperaAntesDeAsentar = 0.35f;
    private const float CadaCuantoRevisar = 1.5f;
    private const float AjusteMinimo = 0.025f;
    private const float AjusteMaximo = 0.9f;
    private const float AlturaMinimaDeAsiento = 0.2f;
    private const float AlturaMaximaDeAsiento = 1.0f;
    private const float RadioDeBusquedaDelAsiento = 0.9f;
    private const float MargenDentroDelAsiento = 0.12f;
    private static readonly RaycastHit[] s_rayos = new RaycastHit[16];
    private static readonly Collider[] s_cercanos = new Collider[24];
    private static readonly int SentadoEnElSuelo = Animator.StringToHash("SitGround_Loop");

    private float _sentadoDesde = -1f;
    private float _proximaRevision;
    private Game.NPC.NPCBehaviourManagerV2 _npc;

    private Animator _animador;
    private Transform _cadera, _espalda, _pecho, _cuello, _cabeza;
    private bool _preparado;

    /// Se lo pone a quien no lo tenga. Barato: si el personaje no se sienta, no hace nada.
    public static void Asegurar(GameObject quien)
    {
        if (quien != null && quien.GetComponent<PosturaDeSentado>() == null) quien.AddComponent<PosturaDeSentado>();
    }

    private void Preparar()
    {
        _preparado = true;
        _animador = GetComponentInChildren<Animator>(true);
        if (_animador == null || !_animador.isHuman) { _animador = null; return; }
        _cadera = _animador.GetBoneTransform(HumanBodyBones.Hips);
        _espalda = _animador.GetBoneTransform(HumanBodyBones.Spine);
        _pecho = _animador.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animador.GetBoneTransform(HumanBodyBones.Chest);
        _cuello = _animador.GetBoneTransform(HumanBodyBones.Neck);
        _cabeza = _animador.GetBoneTransform(HumanBodyBones.Head);
        _npc = GetComponentInParent<Game.NPC.NPCBehaviourManagerV2>();
    }

    private void LateUpdate()
    {
        if (!_preparado) Preparar();
        if (_animador == null || !_animador.isActiveAndEnabled || _animador.runtimeAnimatorController == null) return;

        float peso = PesoDeSentado();
        if (peso < 0.999f) _sentadoDesde = -1f;
        else
        {
            if (_sentadoDesde < 0f) { _sentadoDesde = Time.time; _proximaRevision = Time.time + EsperaAntesDeAsentar; }
            if (ajustarAlAsiento && Time.time >= _proximaRevision)
            {
                _proximaRevision = Time.time + CadaCuantoRevisar;
                Asentar();
            }
        }
        if (peso <= 0f) return;

        // Tronco: de la cadera al cuello. Se reparte entre la espalda y el pecho para que se curve
        // como una columna y no gire de golpe en un solo hueso.
        if (_cadera != null && _cuello != null && espalda > 0f)
        {
            Quaternion giro = HaciaLaVertical(_cuello.position - _cadera.position, espalda * peso);
            if (_espalda != null && _pecho != null && _pecho != _espalda)
            {
                Quaternion mitad = Quaternion.Slerp(Quaternion.identity, giro, 0.5f);
                _espalda.rotation = mitad * _espalda.rotation;
                _pecho.rotation = mitad * _pecho.rotation;
            }
            else if (_espalda != null) _espalda.rotation = giro * _espalda.rotation;
        }

        // Cabeza: el eje cuello → cabeza hacia la vertical, repartido entre los dos huesos.
        if (_cuello != null && _cabeza != null && cabeza > 0f)
        {
            Quaternion giro = HaciaLaVertical(_cabeza.position - _cuello.position, cabeza * peso);
            Quaternion mitad = Quaternion.Slerp(Quaternion.identity, giro, 0.5f);
            _cuello.rotation = mitad * _cuello.rotation;
            _cabeza.rotation = mitad * _cabeza.rotation;
        }
    }

    // ── Asentar en el asiento ─────────────────────────────────────────────────────────────

    /// Mueve al NPC lo justo para que su cadera quede sobre la tabla del asiento: primero en
    /// horizontal (si está sentado al lado del asiento, sobre el aire) y luego en altura.
    private void Asentar()
    {
        if (_npc == null || _cadera == null || _npc.IsInCinematic) return;
        bool enElSuelo = _animador.GetCurrentAnimatorStateInfo(0).shortNameHash == SentadoEnElSuelo;
        var raiz = Raiz;
        Vector3 cadera = _cadera.position;
        float suelo = AlturaDelSuelo(raiz.position);

        if (enElSuelo)
        {
            // En el suelo solo se corrige si se ha hundido: la animación ya lo deja a ras.
            if (!float.IsNegativeInfinity(suelo) && cadera.y < suelo + 0.05f)
                raiz.position += Vector3.up * (suelo + 0.05f - cadera.y);
            return;
        }

        float tabla = TablaBajo(cadera, suelo);
        if (float.IsNegativeInfinity(tabla))
        {
            // Nada que sirva de asiento justo debajo: el asiento está al lado. Se busca y se lleva la
            // cadera a su tabla, por dentro del borde.
            if (!AsientoCercano(cadera, suelo, out Bounds asiento)) return;
            float x = Mathf.Clamp(cadera.x, asiento.min.x + MargenDentroDelAsiento, asiento.max.x - MargenDentroDelAsiento);
            float z = Mathf.Clamp(cadera.z, asiento.min.z + MargenDentroDelAsiento, asiento.max.z - MargenDentroDelAsiento);
            if (asiento.size.x < MargenDentroDelAsiento * 2f) x = asiento.center.x;
            if (asiento.size.z < MargenDentroDelAsiento * 2f) z = asiento.center.z;
            Vector3 movimiento = new Vector3(x - cadera.x, 0f, z - cadera.z);
            if (movimiento.magnitude > RadioDeBusquedaDelAsiento) return;
            raiz.position += movimiento;
            cadera += movimiento;
            tabla = TablaBajo(cadera, suelo);
            if (float.IsNegativeInfinity(tabla)) tabla = asiento.max.y;
        }

        float ajuste = tabla + holguraDeCadera - cadera.y;
        if (Mathf.Abs(ajuste) < AjusteMinimo || Mathf.Abs(ajuste) > AjusteMaximo) return;
        raiz.position += Vector3.up * ajuste;
    }

    /// Altura de lo que hay bajo la cadera que pueda ser un asiento (entre el suelo y un metro por
    /// encima), o -infinito si debajo solo está el suelo.
    private float TablaBajo(Vector3 cadera, float suelo)
    {
        int n = Physics.RaycastNonAlloc(cadera + Vector3.up * 0.4f, Vector3.down, s_rayos, 1.6f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var h = s_rayos[i];
            if (!EsMueble(h.collider)) continue;
            float alto = float.IsNegativeInfinity(suelo) ? 1f : h.point.y - suelo;
            if (alto < AlturaMinimaDeAsiento || alto > AlturaMaximaDeAsiento) continue;
            if (h.point.y > mejor) mejor = h.point.y;
        }
        return mejor;
    }

    private bool AsientoCercano(Vector3 cadera, float suelo, out Bounds asiento)
    {
        asiento = default;
        int n = Physics.OverlapSphereNonAlloc(cadera, RadioDeBusquedaDelAsiento, s_cercanos, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = s_cercanos[i];
            if (!EsMueble(c)) continue;
            var b = c.bounds;
            float alto = float.IsNegativeInfinity(suelo) ? b.size.y : b.max.y - suelo;
            if (alto < AlturaMinimaDeAsiento || alto > AlturaMaximaDeAsiento) continue;
            Vector3 cerca = b.ClosestPoint(cadera);
            float d = new Vector2(cerca.x - cadera.x, cerca.z - cadera.z).sqrMagnitude;
            if (d < mejor) { mejor = d; asiento = b; }
        }
        return mejor < float.MaxValue;
    }

    /// Un mueble: algo sólido que no es el propio personaje, ni otro personaje, ni el terreno.
    private bool EsMueble(Collider c)
    {
        if (c == null || c.isTrigger || c is TerrainCollider) return false;
        var t = c.transform;
        if (t.IsChildOf(Raiz)) return false;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
        return c.GetComponentInParent<NPCSimpleAnimator>() == null;
    }

    private Transform Raiz => _npc != null ? _npc.transform : transform;
    private static int s_capaSuelo = -1;

    private static float AlturaDelSuelo(Vector3 pie)
    {
        if (s_capaSuelo < 0) s_capaSuelo = LayerMask.GetMask("Floor");
        int capa = s_capaSuelo;
        if (capa == 0) return float.NegativeInfinity;
        return Physics.Raycast(pie + Vector3.up * 1.5f, Vector3.down, out var h, 4f, capa, QueryTriggerInteraction.Ignore)
            ? h.point.y : float.NegativeInfinity;
    }

    /// 1 sentado del todo, entre 0 y 1 mientras se sienta o se levanta, 0 de pie.
    private float PesoDeSentado()
    {
        var info = _animador.GetCurrentAnimatorStateInfo(0);
        float peso = PesoDe(info);
        if (_animador.IsInTransition(0))
        {
            float t = _animador.GetAnimatorTransitionInfo(0).normalizedTime;
            peso = Mathf.Lerp(peso, PesoDe(_animador.GetNextAnimatorStateInfo(0)), t);
        }
        return peso;
    }

    private static float PesoDe(AnimatorStateInfo info)
    {
        int h = info.shortNameHash;
        if (Contiene(Sentados, h)) return 1f;
        if (Contiene(Sentandose, h)) return Mathf.Clamp01(info.normalizedTime);
        if (Contiene(Levantandose, h)) return 1f - Mathf.Clamp01(info.normalizedTime);
        return 0f;
    }

    private static bool Contiene(int[] lista, int h)
    {
        for (int i = 0; i < lista.Length; i++) if (lista[i] == h) return true;
        return false;
    }

    private static Quaternion HaciaLaVertical(Vector3 eje, float cuanto)
    {
        if (eje.sqrMagnitude < 1e-6f) return Quaternion.identity;
        return Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(eje.normalized, Vector3.up), cuanto);
    }
}
