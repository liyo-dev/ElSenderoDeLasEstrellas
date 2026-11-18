using UnityEngine;

/// <summary>
/// Balanceo de péndulo para una parte de una atracción (la barca del barco vikingo): gira sobre un
/// eje local alrededor de una rotación de reposo. Sustituye al Animator del pack cuando su animación
/// deja la pieza boca abajo (la animación escribe la rotación entera y no se puede corregir encima).
/// Ver INC-893.
/// </summary>
[DisallowMultipleComponent]
public sealed class BalanceoPendular : MonoBehaviour
{
    [Tooltip("Rotación local en reposo (la barca colgando hacia abajo).")]
    [SerializeField] private Vector3 reposo = Vector3.zero;
    [Tooltip("Eje local del balanceo.")]
    [SerializeField] private Vector3 eje = Vector3.right;
    [Tooltip("Grados a cada lado.")]
    [SerializeField, Range(0f, 120f)] private float amplitud = 40f;
    [Tooltip("Segundos de una ida y vuelta completa.")]
    [SerializeField, Min(0.5f)] private float periodo = 8f;

    private Quaternion _reposo;
    private float _fase;

    /// <summary>Cuánto del balanceo se hace (0 = quieto en reposo, 1 = la amplitud entera). Lo sube y baja
    /// quien lo maneja (un columpio, mientras alguien va sentado).</summary>
    public float Intensidad { get; set; } = 1f;

    /// <summary>Segundos de una ida y vuelta completa.</summary>
    public float Periodo => periodo;

    /// <summary>Dónde va el vaivén dentro de su periodo (0–1; 0 y 0,5 = pasando por el centro, 0,25 y 0,75 = en los extremos).
    /// Sirve para que una animación de quien va montado lleve el mismo compás que la tabla.</summary>
    public float Fase01 => Mathf.Repeat((Time.time + _fase) / periodo, 1f);

    void Awake()
    {
        _reposo = Quaternion.Euler(reposo);
        _fase = Random.value * periodo;
    }

    void Update()
    {
        float angulo = amplitud * Intensidad * Mathf.Sin((Time.time + _fase) * 2f * Mathf.PI / periodo);
        transform.localRotation = _reposo * Quaternion.AngleAxis(angulo, eje);
    }

    /// <summary>Lo pone el constructor de la escena.</summary>
    public void Configurar(Vector3 nuevoReposo, Vector3 nuevoEje, float nuevaAmplitud, float nuevoPeriodo)
    {
        reposo = nuevoReposo;
        eje = nuevoEje;
        amplitud = nuevaAmplitud;
        periodo = nuevoPeriodo;
    }
}
