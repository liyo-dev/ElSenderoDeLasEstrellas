using UnityEngine;

/// <summary>
/// Dos personajes que van de la mano: el que lleva este componente (quien guía) y <see cref="Otro"/>.
/// Después de la animación, lleva una mano de cada uno al punto medio entre las dos (IK de dos huesos
/// hecho a mano sobre los huesos del humanoide, así que no necesita «IK Pass» en el Animator).
/// Las manos que se cogen dependen del lado: si el otro va a la derecha de quien guía, la derecha de
/// quien guía con la izquierda del otro. Se pide y se suelta con fundido (<see cref="Coger"/> / <see cref="Soltar"/>).
/// Las posiciones de los dos las pone quien los mueva (el guion los lleva juntos con «de_la_mano»).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class DeLaMano : MonoBehaviour
{
    private Transform _otro;
    private Brazo _mio, _suyo;
    private float _peso, _objetivo, _velocidad;

    public Transform Otro => _otro;

    private struct Brazo
    {
        public Transform hombro, codo, mano;
        public bool Valido => hombro != null && codo != null && mano != null;
    }

    /// <summary>Coge de la mano a <paramref name="otro"/> en <paramref name="segundos"/>.</summary>
    public void Coger(Transform otro, float segundos = 0.35f)
    {
        _otro = otro;
        var yo = GetComponentInChildren<Animator>();
        var el = otro != null ? otro.GetComponentInChildren<Animator>() : null;
        if (yo == null || el == null || !yo.isHuman || !el.isHuman) { _otro = null; return; }
        // ¿A qué lado va el otro?
        bool aMiDerecha = Vector3.Dot(otro.position - transform.position, transform.right) >= 0f;
        _mio = BrazoDe(yo, aMiDerecha);
        _suyo = BrazoDe(el, !aMiDerecha);
        Fundir(1f, segundos);
    }

    /// <summary>Suelta la mano en <paramref name="segundos"/>.</summary>
    public void Soltar(float segundos = 0.3f) => Fundir(0f, segundos);

    private static Brazo BrazoDe(Animator a, bool derecho) => new Brazo
    {
        hombro = a.GetBoneTransform(derecho ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm),
        codo = a.GetBoneTransform(derecho ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm),
        mano = a.GetBoneTransform(derecho ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand),
    };

    private void Fundir(float objetivo, float segundos)
    {
        _objetivo = objetivo;
        _velocidad = segundos > 0f ? 1f / segundos : 0f;
        if (_velocidad <= 0f) _peso = objetivo;
    }

    void OnDisable() { _peso = _objetivo = 0f; _otro = null; }

    void LateUpdate()
    {
        if (_velocidad > 0f) _peso = Mathf.MoveTowards(_peso, _objetivo, _velocidad * Time.deltaTime);
        if (_peso <= 0.001f || _otro == null || !_mio.Valido || !_suyo.Valido) return;

        // El punto de encuentro: entre las dos manos tal como las deja la animación, algo por
        // delante (si no, los brazos se cruzan por detrás de las caderas al andar).
        Vector3 a = _mio.mano.position, b = _suyo.mano.position;
        Vector3 medio = (a + b) * 0.5f;
        Vector3 delante = transform.forward; delante.y = 0f;
        medio += delante.normalized * 0.06f;
        Vector3 entre = b - a; entre.y = 0f;
        Vector3 separa = entre.sqrMagnitude > 1e-4f ? entre.normalized * 0.035f : Vector3.zero;
        Alcanzar(_mio, medio - separa, _peso);
        Alcanzar(_suyo, medio + separa, _peso);
    }

    /// IK de dos huesos: gira el hombro y el codo para que la mano llegue a <paramref name="objetivo"/>,
    /// doblando el codo hacia donde ya lo dobla la animación.
    private static void Alcanzar(Brazo br, Vector3 objetivo, float peso)
    {
        Vector3 A = br.hombro.position, B = br.codo.position, C = br.mano.position;
        float l1 = (B - A).magnitude, l2 = (C - B).magnitude;
        if (l1 < 1e-4f || l2 < 1e-4f) return;
        Vector3 haciaObj = objetivo - A;
        float d = Mathf.Clamp(haciaObj.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
        Vector3 dir = haciaObj.sqrMagnitude > 1e-6f ? haciaObj.normalized : (C - A).normalized;
        float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
        Vector3 doblez = Vector3.ProjectOnPlane(B - A, dir);
        if (doblez.sqrMagnitude < 1e-6f) doblez = Vector3.ProjectOnPlane(Vector3.down, dir);
        doblez.Normalize();
        Vector3 codo = A + dir * (l1 * cosA) + doblez * (l1 * Mathf.Sqrt(1f - cosA * cosA));

        Quaternion giroHombro = Quaternion.FromToRotation(B - A, codo - A) * br.hombro.rotation;
        br.hombro.rotation = Quaternion.Slerp(br.hombro.rotation, giroHombro, peso);
        B = br.codo.position; C = br.mano.position;
        Quaternion giroCodo = Quaternion.FromToRotation(C - B, objetivo - B) * br.codo.rotation;
        br.codo.rotation = Quaternion.Slerp(br.codo.rotation, giroCodo, peso);
    }
}
