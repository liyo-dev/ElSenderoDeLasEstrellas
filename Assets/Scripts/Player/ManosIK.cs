using UnityEngine;

/// <summary>
/// Lleva las dos manos de un humanoide a sostener algo delante del pecho (un orbe, un objeto),
/// por IK, encima de la animación que esté sonando. El punto de agarre se mide en largos de brazo
/// desde los hombros (<see cref="delante"/>, <see cref="bajoHombros"/>), así que no depende de dónde
/// tenga las manos la animación y sirve con cualquier proporción de cuerpo (también cabezones de
/// brazos cortos). Además endereza la cabeza hacia el frente (<see cref="cabezaAlFrente"/>) para que
/// no se incline sobre lo que sostiene.
/// Quien la usa mueve <see cref="Desplazamiento"/> y <see cref="Separacion"/> cada fotograma
/// (con muelles, ruido…) y la pide o la suelta con fundido.
/// Necesita «IK Pass» en la capa del Animator donde se quiera aplicar. Ver INC-643, INC-694.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class ManosIK : MonoBehaviour
{
    [Tooltip("Animator humanoide. Vacío: el de este objeto.")]
    [SerializeField] private Animator animator;
    [Tooltip("Distancia del agarre por delante de los hombros, en largos de brazo.")]
    [SerializeField, Range(0f, 1.2f)] private float delante = 0.7f;
    [Tooltip("Cuánto queda el agarre por debajo de los hombros, en largos de brazo.")]
    [SerializeField, Range(-0.5f, 1f)] private float bajoHombros = 0.45f;
    [Tooltip("Cuánto endereza la cabeza para mirar al frente mientras sostiene, para que no se incline sobre las manos.")]
    [SerializeField, Range(0f, 1f)] private float cabezaAlFrente = 0.6f;
    [Tooltip("Cuánto se abren los codos hacia fuera, en metros.")]
    [SerializeField, Min(0f)] private float aperturaDeCodos = 0.25f;

    private object _quien;
    private float _peso, _pesoObjetivo, _velocidadPeso;
    private Transform _manoIzq, _manoDer;
    // Medidas del esqueleto (longitudes, válidas en cualquier pose). Sin ellas se parte de las manos animadas.
    private float _largoBrazo, _alturaHombros;
    private bool _anclaAlCuerpo;

    /// <summary>Desde el punto de agarre, en ejes del personaje (x derecha, y arriba, z delante).</summary>
    public Vector3 Desplazamiento { get; set; }
    /// <summary>Distancia entre las dos manos, en metros.</summary>
    public float Separacion { get; set; } = 0.2f;
    /// <summary>Punto medio real de las manos tras el IK (válido desde LateUpdate).</summary>
    public Vector3 PuntoMedio => _manoIzq != null && _manoDer != null
        ? (_manoIzq.position + _manoDer.position) * 0.5f : transform.position;
    public bool Activo => _peso > 0.001f;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) return;

        _manoIzq = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _manoDer = animator.GetBoneTransform(HumanBodyBones.RightHand);
        MedirEsqueleto();
    }

    /// <summary>
    /// Largo del brazo y altura de los hombros sobre la cadera. Se miden con los huesos una sola vez:
    /// en OnAnimatorIK los huesos aún tienen la pose del fotograma anterior (con lo que otros
    /// componentes hayan movido en LateUpdate), así que allí solo se usan datos del Animator.
    /// </summary>
    private void MedirEsqueleto()
    {
        Transform hombroIzq = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform hombroDer = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform codoIzq = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform codoDer = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform cadera = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hombroIzq == null || hombroDer == null || codoIzq == null || codoDer == null
            || cadera == null || _manoIzq == null || _manoDer == null) return;

        float izq = Vector3.Distance(hombroIzq.position, codoIzq.position) + Vector3.Distance(codoIzq.position, _manoIzq.position);
        float der = Vector3.Distance(hombroDer.position, codoDer.position) + Vector3.Distance(codoDer.position, _manoDer.position);
        _largoBrazo = (izq + der) * 0.5f;
        Vector3 hombros = (hombroIzq.position + hombroDer.position) * 0.5f;
        _alturaHombros = Vector3.Dot(hombros - cadera.position, transform.up);
        _anclaAlCuerpo = _largoBrazo > 0.001f && _alturaHombros > 0.001f;
    }

    /// <summary>Empieza a sostener; las manos llegan en <paramref name="segundos"/>.</summary>
    public void Sostener(object quien, float segundos)
    {
        _quien = quien;
        Fundir(1f, segundos);
    }

    /// <summary>Suelta si lo sostenía <paramref name="quien"/>; las manos vuelven a la animación en <paramref name="segundos"/>.</summary>
    public void Soltar(object quien, float segundos)
    {
        if (!ReferenceEquals(_quien, quien)) return;
        _quien = null;
        Fundir(0f, segundos);
    }

    void Update()
    {
        if (_velocidadPeso <= 0f) { _peso = _pesoObjetivo; return; }
        _peso = Mathf.MoveTowards(_peso, _pesoObjetivo, _velocidadPeso * Time.unscaledDeltaTime);
    }

    void OnDisable()
    {
        _quien = null;
        _peso = _pesoObjetivo = 0f;
    }

    void OnAnimatorIK(int capa)
    {
        if (animator == null) return;
        if (_peso <= 0.001f)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            if (_anclaAlCuerpo) animator.SetLookAtWeight(0f);
            return;
        }

        Quaternion giro = transform.rotation;
        Vector3 derecha = giro * Vector3.right;
        Vector3 arriba = giro * Vector3.up;

        // Punto de agarre: delante del pecho, medido desde los hombros en largos de brazo.
        Vector3 hombros = Vector3.zero;
        Vector3 centro;
        if (_anclaAlCuerpo)
        {
            hombros = animator.bodyPosition + arriba * _alturaHombros;
            centro = hombros + giro * new Vector3(0f, -bajoHombros * _largoBrazo, delante * _largoBrazo);
        }
        else
        {
            centro = (animator.GetIKPosition(AvatarIKGoal.LeftHand) + animator.GetIKPosition(AvatarIKGoal.RightHand)) * 0.5f;
        }
        centro += giro * Desplazamiento;
        Vector3 mitad = derecha * (Separacion * 0.5f);

        animator.SetIKPosition(AvatarIKGoal.LeftHand, centro - mitad);
        animator.SetIKPosition(AvatarIKGoal.RightHand, centro + mitad);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _peso);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _peso);

        // Codos hacia fuera, algo por debajo y por detrás de las manos: postura de «sostener energía».
        Vector3 lateral = derecha * (Separacion * 0.5f + aperturaDeCodos);
        Vector3 abajoYAtras = Vector3.down * (aperturaDeCodos * 0.5f) - giro * Vector3.forward * (aperturaDeCodos * 0.5f);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, centro - lateral + abajoYAtras);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, centro + lateral + abajoYAtras);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _peso);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _peso);

        // Cabeza al frente, a la altura de los ojos, para que no se meta sobre las manos.
        if (_anclaAlCuerpo)
        {
            animator.SetLookAtPosition(hombros + giro * Vector3.forward * 3f + arriba * (_alturaHombros * 0.5f));
            animator.SetLookAtWeight(_peso * cabezaAlFrente, 0f, 1f, 0f, 0.5f);
        }
    }

    private void Fundir(float objetivo, float segundos)
    {
        _pesoObjetivo = objetivo;
        _velocidadPeso = segundos > 0f ? 1f / segundos : 0f;
    }
}
