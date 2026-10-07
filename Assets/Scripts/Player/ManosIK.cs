using UnityEngine;

/// <summary>
/// Lleva las dos manos de un humanoide a sostener algo delante del cuerpo (un orbe, un objeto),
/// por IK, encima de la animación que esté sonando. Parte de dónde la animación ya tiene las manos,
/// así que sirve con cualquier proporción de brazos. Quien la usa mueve <see cref="Desplazamiento"/>
/// y <see cref="Separacion"/> cada fotograma (con muelles, ruido…) y la pide o la suelta con fundido.
/// Necesita «IK Pass» en la capa del Animator donde se quiera aplicar. Ver INC-643.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class ManosIK : MonoBehaviour
{
    [Tooltip("Animator humanoide. Vacío: el de este objeto.")]
    [SerializeField] private Animator animator;
    [Tooltip("Cuánto se abren los codos hacia fuera, en metros.")]
    [SerializeField, Min(0f)] private float aperturaDeCodos = 0.25f;

    private object _quien;
    private float _peso, _pesoObjetivo, _velocidadPeso;
    private Transform _manoIzq, _manoDer;

    /// <summary>Desde el punto medio de las manos animadas, en ejes del personaje (x derecha, y arriba, z delante).</summary>
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
        if (animator != null && animator.isHuman)
        {
            _manoIzq = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _manoDer = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
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
            return;
        }

        Quaternion giro = transform.rotation;
        Vector3 derecha = giro * Vector3.right;
        Vector3 izqAnim = animator.GetIKPosition(AvatarIKGoal.LeftHand);
        Vector3 derAnim = animator.GetIKPosition(AvatarIKGoal.RightHand);
        Vector3 centro = (izqAnim + derAnim) * 0.5f + giro * Desplazamiento;
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
    }

    private void Fundir(float objetivo, float segundos)
    {
        _pesoObjetivo = objetivo;
        _velocidadPeso = segundos > 0f ? 1f / segundos : 0f;
    }
}
