using UnityEngine;

/// <summary>
/// Gestos de cuerpo que no tienen animación propia, hechos a mano encima de la animación que esté
/// sonando: abrazar (entre dos o entre tres) y alcanzar algo con un brazo extendido.
///
/// Escribe los huesos en LateUpdate (después del Animator), así que no necesita «IK Pass» y sirve
/// con cualquier controlador, NPC o jugador. Los brazos se resuelven con IK analítico de dos huesos
/// (hombro, codo, mano) y se mezclan con la pose animada por un peso que entra y sale con fundido.
///
/// Abrazo: cada uno inclina el pecho hacia su compañero principal, lleva una mano a la espalda de
/// quien tenga a la izquierda y otra a la de quien tenga a la derecha (una alta y otra baja, como un
/// abrazo de verdad), ladea la cabeza al lado contrario para quedar mejilla con mejilla (con cabezas
/// grandes y brazos cortos, si no, se atraviesan) y se balancea despacio.
///
/// Alcanzar: un brazo se estira hacia un punto del mundo (la mano abierta hacia algo que se va) y el
/// pecho se adelanta un poco hacia él.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class GestosProcedurales : MonoBehaviour
{
    [Tooltip("Grados que se inclina el pecho hacia el compañero en el abrazo.")]
    [SerializeField, Range(0f, 30f)] private float inclinacion = 12f;
    [Tooltip("Grados que gira la cabeza hacia un lado para quedar mejilla con mejilla.")]
    [SerializeField, Range(0f, 45f)] private float giroDeCabeza = 22f;
    [Tooltip("Grados que se ladea la cabeza hacia el hombro del compañero.")]
    [SerializeField, Range(0f, 30f)] private float ladeoDeCabeza = 12f;
    [Tooltip("Grados del balanceo lento de lado a lado mientras dura el abrazo.")]
    [SerializeField, Range(0f, 10f)] private float balanceo = 3.5f;
    [Tooltip("Ciclos por segundo del balanceo.")]
    [SerializeField, Range(0.05f, 2f)] private float ritmoDelBalanceo = 0.45f;
    [Tooltip("Cuánto pasan las manos por detrás del eje del compañero, en metros.")]
    [SerializeField, Range(0f, 0.3f)] private float profundidadEnLaEspalda = 0.07f;
    [Tooltip("Separación lateral de cada mano respecto al centro de la espalda, en metros.")]
    [SerializeField, Range(0f, 0.3f)] private float aperturaDeManos = 0.09f;
    [Tooltip("Diferencia de altura entre la mano alta y la baja, en metros.")]
    [SerializeField, Range(0f, 0.3f)] private float alturaEntreManos = 0.12f;
    [Tooltip("Grados que se adelanta el pecho al alcanzar algo con la mano.")]
    [SerializeField, Range(0f, 30f)] private float inclinacionAlAlcanzar = 10f;

    private Animator _animator;
    private Transform _pecho, _cabeza;
    private Transform _hombroIzq, _codoIzq, _manoIzq;
    private Transform _hombroDer, _codoDer, _manoDer;
    private bool _valido;

    // Abrazo
    private GestosProcedurales _principal, _parejaIzq, _parejaDer;
    private float _peso, _pesoObjetivo, _velocidadPeso;
    private float _fase;
    private float _lado = 1f;

    // Alcanzar
    private Vector3 _puntoQueAlcanza;
    private bool _alcanzaConLaDerecha = true;
    private float _pesoAlcance, _pesoAlcanceObjetivo, _velocidadAlcance;

    /// <summary>Hueso del pecho (o la cadera si no hay), para que los compañeros sepan dónde abrazar.</summary>
    public Transform Pecho => _pecho;

    // ── API ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Dos personajes se abrazan; llegan al abrazo en <paramref name="fundido"/> segundos.</summary>
    public static bool Abrazar(Transform a, Transform b, float fundido)
    {
        var ga = Obtener(a);
        var gb = Obtener(b);
        if (ga == null || gb == null || ga == gb) return false;

        // Cada cabeza se aparta al lado contrario de donde está el otro. Están de frente, así que si
        // uno tiene al otro a su derecha, el otro también: el lado coincide y no chocan.
        float lado = Vector3.Dot(b.position - a.position, a.right) >= 0f ? 1f : -1f;
        ga.EmpezarAbrazo(gb, gb, gb, lado, fundido);
        gb.EmpezarAbrazo(ga, ga, ga, lado, fundido);
        return true;
    }

    /// <summary>
    /// Abrazo de tres: <paramref name="centro"/> abraza con un brazo a cada uno (según quién le quede
    /// a cada lado) y los otros dos le abrazan a él con los dos brazos.
    /// </summary>
    public static bool AbrazarATres(Transform centro, Transform a, Transform b, float fundido)
    {
        var gc = Obtener(centro);
        var ga = Obtener(a);
        var gb = Obtener(b);
        if (gc == null || ga == null || gb == null) return false;

        bool aALaIzquierda = Vector3.Dot(a.position - centro.position, centro.right) < Vector3.Dot(b.position - centro.position, centro.right);
        var izq = aALaIzquierda ? ga : gb;
        var der = aALaIzquierda ? gb : ga;
        var principal = Vector3.Distance(a.position, centro.position) <= Vector3.Distance(b.position, centro.position) ? ga : gb;

        float ladoA = Vector3.Dot(centro.position - a.position, a.right) >= 0f ? 1f : -1f;
        float ladoB = Vector3.Dot(centro.position - b.position, b.right) >= 0f ? 1f : -1f;
        gc.EmpezarAbrazo(principal, izq, der, principal == ga ? ladoA : ladoB, fundido);
        ga.EmpezarAbrazo(gc, gc, gc, ladoA, fundido);
        gb.EmpezarAbrazo(gc, gc, gc, ladoB, fundido);
        return true;
    }

    /// <summary>
    /// <paramref name="quien"/> rodea con un brazo a cada uno de <paramref name="a"/> y
    /// <paramref name="b"/> (que siguen con lo que estuvieran haciendo, por ejemplo abrazados entre
    /// ellos): el que llega y se suma al abrazo.
    /// </summary>
    public static bool RodearConLosBrazos(Transform quien, Transform a, Transform b, float fundido)
    {
        var gq = Obtener(quien);
        var ga = Obtener(a);
        var gb = Obtener(b);
        if (gq == null || ga == null || gb == null) return false;

        bool aALaIzquierda = Vector3.Dot(a.position - quien.position, quien.right) < Vector3.Dot(b.position - quien.position, quien.right);
        var izq = aALaIzquierda ? ga : gb;
        var der = aALaIzquierda ? gb : ga;
        var principal = Vector3.Distance(a.position, quien.position) <= Vector3.Distance(b.position, quien.position) ? ga : gb;
        float lado = Vector3.Dot(principal.transform.position - quien.position, quien.right) >= 0f ? -1f : 1f;
        gq.EmpezarAbrazo(principal, izq, der, lado, fundido);
        return true;
    }

    /// <summary>Suelta el abrazo de todos los que se pasen, con fundido.</summary>
    public static void Soltar(float fundido, params Transform[] personajes)
    {
        foreach (var p in personajes) Obtener(p, crear: false)?.TerminarAbrazo(fundido);
    }

    /// <summary>Estira un brazo hacia <paramref name="punto"/> (coordenadas del mundo).</summary>
    public static void Alcanzar(Transform personaje, Vector3 punto, bool conLaDerecha, float fundido)
    {
        var g = Obtener(personaje);
        if (g == null) return;
        g._puntoQueAlcanza = punto;
        g._alcanzaConLaDerecha = conLaDerecha;
        g._pesoAlcanceObjetivo = 1f;
        g._velocidadAlcance = fundido > 0f ? 1f / fundido : 0f;
        if (g._velocidadAlcance <= 0f) g._pesoAlcance = 1f;
    }

    /// <summary>Baja el brazo que alcanzaba algo, con fundido.</summary>
    public static void DejarDeAlcanzar(Transform personaje, float fundido)
    {
        var g = Obtener(personaje, crear: false);
        if (g == null) return;
        g._pesoAlcanceObjetivo = 0f;
        g._velocidadAlcance = fundido > 0f ? 1f / fundido : 0f;
        if (g._velocidadAlcance <= 0f) g._pesoAlcance = 0f;
    }

    /// <summary>El componente del Animator humanoide visible del personaje, creándolo si falta.</summary>
    public static GestosProcedurales Obtener(Transform raiz, bool crear = true)
    {
        if (raiz == null) return null;
        var animators = raiz.GetComponentsInChildren<Animator>(false);
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null || !anim.isActiveAndEnabled || !anim.isHuman) continue;
            if (anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) == null) continue;
            var gestos = anim.GetComponent<GestosProcedurales>();
            if (gestos == null && crear) gestos = anim.gameObject.AddComponent<GestosProcedurales>();
            return gestos;
        }
        return null;
    }

    // ── Ciclo ────────────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman) return;

        _pecho = _animator.GetBoneTransform(HumanBodyBones.UpperChest)
            ?? _animator.GetBoneTransform(HumanBodyBones.Chest)
            ?? _animator.GetBoneTransform(HumanBodyBones.Spine)
            ?? _animator.GetBoneTransform(HumanBodyBones.Hips);
        _cabeza = _animator.GetBoneTransform(HumanBodyBones.Head);
        _hombroIzq = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _codoIzq = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _manoIzq = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _hombroDer = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _codoDer = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _manoDer = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        _valido = _pecho != null && _hombroIzq != null && _codoIzq != null && _manoIzq != null
            && _hombroDer != null && _codoDer != null && _manoDer != null;
    }

    private void OnDisable()
    {
        _peso = _pesoObjetivo = 0f;
        _pesoAlcance = _pesoAlcanceObjetivo = 0f;
        _principal = _parejaIzq = _parejaDer = null;
    }

    private void EmpezarAbrazo(GestosProcedurales principal, GestosProcedurales izq, GestosProcedurales der, float lado, float fundido)
    {
        _principal = principal;
        _parejaIzq = izq;
        _parejaDer = der;
        _lado = lado;
        _fase = Random.value * 10f;
        _pesoObjetivo = 1f;
        _velocidadPeso = fundido > 0f ? 1f / fundido : 0f;
        if (_velocidadPeso <= 0f) _peso = 1f;
    }

    private void TerminarAbrazo(float fundido)
    {
        _pesoObjetivo = 0f;
        _velocidadPeso = fundido > 0f ? 1f / fundido : 0f;
        if (_velocidadPeso <= 0f) _peso = 0f;
    }

    private void LateUpdate()
    {
        if (!_valido) return;
        float dt = Time.deltaTime;
        if (_velocidadPeso > 0f) _peso = Mathf.MoveTowards(_peso, _pesoObjetivo, _velocidadPeso * dt);
        if (_velocidadAlcance > 0f) _pesoAlcance = Mathf.MoveTowards(_pesoAlcance, _pesoAlcanceObjetivo, _velocidadAlcance * dt);

        if (_peso > 0.001f && _principal != null && _principal.Pecho != null) Abrazo(Mathf.SmoothStep(0f, 1f, _peso), dt);
        else if (_peso <= 0.001f && _pesoObjetivo <= 0f) _principal = _parejaIzq = _parejaDer = null;

        if (_pesoAlcance > 0.001f) Alcance(Mathf.SmoothStep(0f, 1f, _pesoAlcance));
    }

    private void Abrazo(float w, float dt)
    {
        Vector3 miPecho = _pecho.position;
        Vector3 haciaEl = Horizontal(_principal.Pecho.position - miPecho, transform.forward);
        Vector3 lateral = Vector3.Cross(Vector3.up, haciaEl);

        // 1. El pecho se inclina hacia el compañero y se balancea despacio.
        _fase += dt * ritmoDelBalanceo * Mathf.PI * 2f;
        Quaternion inclina = Quaternion.AngleAxis(inclinacion * w, lateral);
        Quaternion mece = Quaternion.AngleAxis(Mathf.Sin(_fase) * balanceo * w, Vector3.up);
        _pecho.rotation = mece * inclina * _pecho.rotation;

        // 2. Cada mano a la espalda de quien quede a ese lado: una alta y otra baja.
        Vector3 manoIzq = PuntoEnLaEspalda(_parejaIzq, miPecho, -1f, +1f);
        Vector3 manoDer = PuntoEnLaEspalda(_parejaDer, miPecho, +1f, -1f);
        Vector3 codoIzq = _hombroIzq.position - lateral * 0.35f - Vector3.up * 0.15f + haciaEl * 0.1f;
        Vector3 codoDer = _hombroDer.position + lateral * 0.35f - Vector3.up * 0.15f + haciaEl * 0.1f;
        ResolverBrazo(_hombroIzq, _codoIzq, _manoIzq, manoIzq, codoIzq, w);
        ResolverBrazo(_hombroDer, _codoDer, _manoDer, manoDer, codoDer, w);

        // 3. La cabeza se gira y se ladea: mejilla con mejilla.
        if (_cabeza != null)
        {
            Quaternion gira = Quaternion.AngleAxis(giroDeCabeza * _lado * w, Vector3.up);
            Quaternion ladea = Quaternion.AngleAxis(-ladeoDeCabeza * _lado * w, haciaEl);
            _cabeza.rotation = gira * ladea * _cabeza.rotation;
        }
    }

    // Dónde va una mano en la espalda de 'pareja': pasada su línea de pecho, abierta hacia 'ladoDeLaMano'
    // y a una altura u otra según 'alto' (+1 alta, -1 baja) combinado con el lado de la cabeza.
    private Vector3 PuntoEnLaEspalda(GestosProcedurales pareja, Vector3 miPecho, float ladoDeLaMano, float alto)
    {
        var objetivo = pareja != null && pareja.Pecho != null ? pareja : _principal;
        Vector3 suPecho = objetivo.Pecho.position;
        Vector3 haciaEl = Horizontal(suPecho - miPecho, transform.forward);
        Vector3 lateral = Vector3.Cross(Vector3.up, haciaEl);
        Vector3 espalda = suPecho + haciaEl * profundidadEnLaEspalda;
        return espalda + lateral * (aperturaDeManos * ladoDeLaMano) + Vector3.up * (alturaEntreManos * 0.5f * alto * _lado);
    }

    private void Alcance(float w)
    {
        Transform hombro = _alcanzaConLaDerecha ? _hombroDer : _hombroIzq;
        Transform codo = _alcanzaConLaDerecha ? _codoDer : _codoIzq;
        Transform mano = _alcanzaConLaDerecha ? _manoDer : _manoIzq;

        Vector3 haciaElPunto = Horizontal(_puntoQueAlcanza - _pecho.position, transform.forward);
        Vector3 lateral = Vector3.Cross(Vector3.up, haciaElPunto);
        _pecho.rotation = Quaternion.AngleAxis(inclinacionAlAlcanzar * w, lateral) * _pecho.rotation;

        // El brazo casi estirado hacia el punto (no se clava en él aunque esté lejos), con el codo
        // ligeramente hacia fuera y abajo.
        float largo = Vector3.Distance(hombro.position, codo.position) + Vector3.Distance(codo.position, mano.position);
        Vector3 direccion = (_puntoQueAlcanza - hombro.position).normalized;
        Vector3 objetivo = hombro.position + direccion * (largo * 0.97f);
        float fuera = _alcanzaConLaDerecha ? 1f : -1f;
        Vector3 pista = codo.position + lateral * (0.2f * fuera) - Vector3.up * 0.2f;
        ResolverBrazo(hombro, codo, mano, objetivo, pista, w);
    }

    private static Vector3 Horizontal(Vector3 v, Vector3 respaldo)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 1e-4f) { v = respaldo; v.y = 0f; }
        return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
    }

    /// <summary>
    /// IK de dos huesos (hombro, codo, mano) hacia <paramref name="objetivo"/>, con el codo en el plano
    /// que marca <paramref name="pista"/>. Si no llega, estira el brazo hacia el objetivo. Mezcla con
    /// la pose animada según <paramref name="peso"/>.
    /// </summary>
    public static void ResolverBrazo(Transform hombro, Transform codo, Transform mano, Vector3 objetivo, Vector3 pista, float peso)
    {
        Quaternion hombroAnimado = hombro.localRotation;
        Quaternion codoAnimado = codo.localRotation;

        float largoA = Vector3.Distance(hombro.position, codo.position);
        float largoB = Vector3.Distance(codo.position, mano.position);
        if (largoA < 1e-4f || largoB < 1e-4f) return;

        Vector3 haciaObjetivo = objetivo - hombro.position;
        float distancia = Mathf.Clamp(haciaObjetivo.magnitude, Mathf.Abs(largoA - largoB) + 0.001f, largoA + largoB - 0.001f);
        Vector3 direccion = haciaObjetivo.sqrMagnitude > 1e-8f ? haciaObjetivo.normalized : (mano.position - hombro.position).normalized;

        Vector3 haciaPista = pista - hombro.position;
        Vector3 normal = Vector3.Cross(direccion, haciaPista);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(direccion, Vector3.up);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(direccion, Vector3.right);
        normal.Normalize();
        Vector3 haciaElCodo = Vector3.Cross(normal, direccion);
        if (Vector3.Dot(haciaElCodo, haciaPista) < 0f) haciaElCodo = -haciaElCodo;

        float cosA = (largoA * largoA + distancia * distancia - largoB * largoB) / (2f * largoA * distancia);
        float anguloA = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));
        Vector3 codoDeseado = hombro.position + (direccion * Mathf.Cos(anguloA) + haciaElCodo * Mathf.Sin(anguloA)) * largoA;

        hombro.rotation = Quaternion.FromToRotation(codo.position - hombro.position, codoDeseado - hombro.position) * hombro.rotation;
        Vector3 manoDeseada = hombro.position + direccion * distancia;
        codo.rotation = Quaternion.FromToRotation(mano.position - codo.position, manoDeseada - codo.position) * codo.rotation;

        if (peso < 0.999f)
        {
            hombro.localRotation = Quaternion.Slerp(hombroAnimado, hombro.localRotation, peso);
            codo.localRotation = Quaternion.Slerp(codoAnimado, codo.localRotation, peso);
        }
    }
}
