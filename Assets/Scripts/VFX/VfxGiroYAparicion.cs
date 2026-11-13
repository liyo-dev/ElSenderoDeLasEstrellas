using UnityEngine;

/// Pieza de efecto en 3D que aparece con un pequeño rebote cada vez que se enciende y gira
/// despacio sobre su eje vertical (la flor de luz de los escudos). Ver INC-1127
public sealed class VfxGiroYAparicion : MonoBehaviour
{
    [SerializeField, Tooltip("Giro sobre el eje vertical, en grados por segundo.")]
    private float gradosPorSegundo = 30f;
    [SerializeField, Min(0.01f), Tooltip("Lo que tarda en aparecer, en segundos.")]
    private float segundosDeAparicion = 0.25f;
    [SerializeField, Min(1f), Tooltip("Cuánto se pasa de su tamaño antes de asentarse.")]
    private float rebote = 1.12f;

    private Vector3 _escala;
    private float _tiempo;

    private void Awake() => _escala = transform.localScale;

    private void OnEnable()
    {
        _tiempo = 0f;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        _tiempo += Time.deltaTime;
        float k = Mathf.Clamp01(_tiempo / segundosDeAparicion);
        float s = k < 0.7f ? Mathf.Lerp(0f, rebote, k / 0.7f) : Mathf.Lerp(rebote, 1f, (k - 0.7f) / 0.3f);
        transform.localScale = _escala * s;
        transform.Rotate(0f, gradosPorSegundo * Time.deltaTime, 0f, Space.Self);
    }
}
