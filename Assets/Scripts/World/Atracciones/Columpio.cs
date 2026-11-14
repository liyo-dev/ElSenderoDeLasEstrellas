using UnityEngine;

/// <summary>
/// Columpio: un asiento (<see cref="NPCWorldPoint"/>) que cuelga de una pieza con <see cref="BalanceoPendular"/>.
/// Mientras alguien va sentado, el balanceo coge fuerza poco a poco y quien va sentado sigue al asiento; al
/// bajarse, el columpio se va parando. Sentarse y levantarse son los de siempre (interactuar y cancelar).
/// </summary>
[DisallowMultipleComponent]
public sealed class Columpio : MonoBehaviour
{
    [Tooltip("Asiento: su punto de interacción cuelga de la pieza que se balancea.")]
    [SerializeField] private NPCWorldPoint asiento;
    [Tooltip("Pieza que se balancea (cuerdas y tabla).")]
    [SerializeField] private BalanceoPendular balanceo;
    [Tooltip("Postura de quien va sentado (estado del Animator): rodillas algo abiertas y piernas adelante que se balancean. " +
             "Con la de sentarse normal, de frente parecía que no tenía piernas.")]
    [SerializeField] private string postura = "SitSwing_Loop";
    [Tooltip("Segundos que tarda en coger el balanceo entero al sentarse.")]
    [SerializeField, Min(0.1f)] private float arranque = 3f;
    [Tooltip("Segundos que tarda en pararse al bajarse.")]
    [SerializeField, Min(0.1f)] private float frenada = 4f;
    [Tooltip("Segundos en los que quien se sienta se acerca al asiento (sin saltos de cámara).")]
    [SerializeField, Min(0f)] private float acercarse = 0.3f;

    private Transform _ocupante;
    private float _desdeQueSeSento;

    void Awake()
    {
        if (balanceo != null) balanceo.Intensidad = 0f;
        if (asiento != null && string.IsNullOrEmpty(asiento.estadoDelBucle)) asiento.estadoDelBucle = postura;
    }

    void OnDisable()
    {
        SoltarOcupante();
        if (balanceo != null) balanceo.Intensidad = 0f;
    }

    void LateUpdate()
    {
        if (asiento == null || balanceo == null) return;
        Transform ocupante = asiento.Occupant;
        if (ocupante != _ocupante)
        {
            SoltarOcupante();
            _ocupante = ocupante;
            _desdeQueSeSento = 0f;
        }

        float objetivo = _ocupante != null ? 1f : 0f;
        float ritmo = Time.deltaTime / (_ocupante != null ? arranque : frenada);
        balanceo.Intensidad = Mathf.MoveTowards(balanceo.Intensidad, objetivo, ritmo);

        if (_ocupante == null) return;
        _desdeQueSeSento += Time.deltaTime;
        Vector3 destino = asiento.InteractionPosition;
        Quaternion giro = asiento.InteractionRotation;
        if (_desdeQueSeSento < acercarse)
        {
            float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
            _ocupante.SetPositionAndRotation(Vector3.Lerp(_ocupante.position, destino, t), Quaternion.Slerp(_ocupante.rotation, giro, t));
        }
        else
            _ocupante.SetPositionAndRotation(destino, giro);
    }

    /// Quien se baja se queda derecho: el columpio le inclinaba con la tabla.
    private void SoltarOcupante()
    {
        if (_ocupante == null) return;
        Vector3 delante = _ocupante.forward;
        delante.y = 0f;
        if (delante.sqrMagnitude > 0.0001f) _ocupante.rotation = Quaternion.LookRotation(delante, Vector3.up);
        _ocupante = null;
    }

#if UNITY_EDITOR
    /// <summary>Lo pone el montador del prefab.</summary>
    public void Configurar(NPCWorldPoint nuevoAsiento, BalanceoPendular nuevoBalanceo)
    {
        asiento = nuevoAsiento;
        balanceo = nuevoBalanceo;
    }
#endif
}
