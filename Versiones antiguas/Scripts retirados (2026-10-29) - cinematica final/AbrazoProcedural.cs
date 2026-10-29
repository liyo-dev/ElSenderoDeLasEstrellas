using UnityEngine;

/// <summary>
/// Abrazo entre dos personajes humanoides sin necesidad de una animación de abrazo.
/// En LateUpdate, encima de la animación que esté sonando, cada uno inclina el pecho hacia el otro,
/// lleva los dos brazos a la espalda del compañero (IK analítico de dos huesos, una mano alta y otra
/// baja, como un abrazo de verdad) y ladea la cabeza hacia un lado para que las caras queden mejilla
/// con mejilla: con cabezas grandes y brazos cortos, si no se ladean, las cabezas se atraviesan.
/// Un balanceo lento de lado a lado lo mantiene vivo mientras dura.
/// No necesita «IK Pass» en el Animator: escribe los huesos después de la animación, así que sirve
/// con cualquier controlador (NPC o jugador). Se pide con <see cref="Abrazar"/> y se suelta con
/// <see cref="Soltar"/>, ambos con fundido.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class AbrazoProcedural : MonoBehaviour
{
    [Tooltip("Grados que se inclina el pecho hacia el compañero.")]
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

    private Animator _animator;
    private Transform _pecho, _cabeza;
    private Transform _hombroIzq, _codoIzq, _manoIzq;
    private Transform _hombroDer, _codoDer, _manoDer;
    private bool _valido;

    private AbrazoProcedural _otro;
    private float _peso, _pesoObjetivo, _velocidadPeso;
    private float _fase;
    // +1 o -1: hacia qué lado ladea la cabeza. Los dos lo deciden igual para no chocar.
    private float _lado = 1f;

    /// <summary>Hueso del pecho (o la cadera si no hay), para que el compañero sepa dónde abrazar.</summary>
    public Transform Pecho => _pecho;
    public bool Abrazando => _peso > 0.001f;

    /// <summary>
    /// Dos personajes se abrazan. Los dos llegan al abrazo en <paramref name="fundido"/> segundos.
    /// Devuelve false si alguno no tiene un Animator humanoide con brazos.
    /// </summary>
    public static bool Abrazar(Transform a, Transform b, float fundido)
    {
        var ca = Obtener(a);
        var cb = Obtener(b);
        if (ca == null || cb == null || ca == cb) return false;

        // Cada cabeza se aparta hacia el lado contrario a donde está el compañero: así quedan
        // mejilla con mejilla, una a cada lado de la otra, sin atravesarse. Si uno tiene al otro a
        // su derecha, el otro también lo tiene a la suya (están de frente), así que el lado coincide.
        float lado = Vector3.Dot(b.position - a.position, a.right) >= 0f ? 1f : -1f;
        ca.Empezar(cb, lado, fundido);
        cb.Empezar(ca, lado, fundido);
        return true;
    }

    /// <summary>Los dos sueltan el abrazo en <paramref name="fundido"/> segundos y vuelven a su animación.</summary>
    public static void Soltar(Transform a, Transform b, float fundido)
    {
        Obtener(a, crear: false)?.Terminar(fundido);
        Obtener(b, crear: false)?.Terminar(fundido);
    }

    /// <summary>El componente del Animator humanoide visible del personaje, creándolo si falta.</summary>
    public static AbrazoProcedural Obtener(Transform raiz, bool crear = true)
    {
        if (raiz == null) return null;
        var animators = raiz.GetComponentsInChildren<Animator>(false);
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null || !anim.isActiveAndEnabled || !anim.isHuman) continue;
            if (anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) == null) continue;
            var abrazo = anim.GetComponent<AbrazoProcedural>();
            if (abrazo == null && crear) abrazo = anim.gameObject.AddComponent<AbrazoProcedural>();
            return abrazo;
        }
        return null;
    }

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
        _otro = null;
    }

    private void Empezar(AbrazoProcedural otro, float lado, float fundido)
    {
        _otro = otro;
        _lado = lado;
        _fase = Random.value * 10f;
        _pesoObjetivo = 1f;
        _velocidadPeso = fundido > 0f ? 1f / fundido : 0f;
        if (_velocidadPeso <= 0f) _peso = 1f;
    }

    private void Terminar(float fundido)
    {
        _pesoObjetivo = 0f;
        _velocidadPeso = fundido > 0f ? 1f / fundido : 0f;
        if (_velocidadPeso <= 0f) _peso = 0f;
    }

    private void LateUpdate()
    {
        if (!_valido) return;

        if (_velocidadPeso > 0f)
            _peso = Mathf.MoveTowards(_peso, _pesoObjetivo, _velocidadPeso * Time.deltaTime);
        if (_peso <= 0.001f)
        {
            if (_pesoObjetivo <= 0f) _otro = null;
            return;
        }
        if (_otro == null || _otro.Pecho == null) return;

        float w = Mathf.SmoothStep(0f, 1f, _peso);
        Vector3 miPecho = _pecho.position;
        Vector3 suPecho = _otro.Pecho.position;
        Vector3 haciaEl = suPecho - miPecho;
        haciaEl.y = 0f;
        if (haciaEl.sqrMagnitude < 1e-4f) haciaEl = transform.forward;
        haciaEl.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, haciaEl); // mi derecha mirando al compañero

        // 1. El pecho se inclina hacia el compañero y se balancea despacio.
        _fase += Time.deltaTime * ritmoDelBalanceo * Mathf.PI * 2f;
        Quaternion inclina = Quaternion.AngleAxis(inclinacion * w, lateral);
        Quaternion mece = Quaternion.AngleAxis(Mathf.Sin(_fase) * balanceo * w, Vector3.up);
        _pecho.rotation = mece * inclina * _pecho.rotation;

        // 2. Los brazos rodean la espalda: una mano alta (omoplato) y otra baja (cintura).
        Vector3 espalda = suPecho + haciaEl * profundidadEnLaEspalda;
        Vector3 manoIzq = espalda - lateral * aperturaDeManos + Vector3.up * (alturaEntreManos * 0.5f * _lado);
        Vector3 manoDer = espalda + lateral * aperturaDeManos - Vector3.up * (alturaEntreManos * 0.5f * _lado);
        // Codos hacia fuera y un poco por debajo: brazos que envuelven, no que empujan.
        Vector3 codoIzq = _hombroIzq.position - lateral * 0.35f - Vector3.up * 0.15f + haciaEl * 0.1f;
        Vector3 codoDer = _hombroDer.position + lateral * 0.35f - Vector3.up * 0.15f + haciaEl * 0.1f;
        ResolverBrazo(_hombroIzq, _codoIzq, _manoIzq, manoIzq, codoIzq, w);
        ResolverBrazo(_hombroDer, _codoDer, _manoDer, manoDer, codoDer, w);

        // 3. La cabeza se gira y se ladea hacia un lado: mejilla con mejilla.
        if (_cabeza != null)
        {
            Quaternion gira = Quaternion.AngleAxis(giroDeCabeza * _lado * w, Vector3.up);
            Quaternion ladea = Quaternion.AngleAxis(-ladeoDeCabeza * _lado * w, haciaEl);
            _cabeza.rotation = gira * ladea * _cabeza.rotation;
        }
    }

    /// <summary>
    /// IK de dos huesos (hombro, codo, mano) hacia <paramref name="objetivo"/>, con el codo en el plano
    /// que marca <paramref name="pista"/>. Si no llega, estira el brazo hacia el objetivo. Mezcla con
    /// la pose animada según <paramref name="peso"/>.
    /// </summary>
    private static void ResolverBrazo(Transform hombro, Transform codo, Transform mano, Vector3 objetivo, Vector3 pista, float peso)
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
