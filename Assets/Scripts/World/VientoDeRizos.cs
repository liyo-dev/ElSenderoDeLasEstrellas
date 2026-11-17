using UnityEngine;

/// Mantiene varios ricitos de viento alrededor del ancla: los lanza a favor del viento, cada uno
/// con su lazada y su tamaño, y los recicla cuando terminan. Acompaña a las hojas de VfxDeClima.
[DisallowMultipleComponent]
public sealed class VientoDeRizos : MonoBehaviour
{
    [SerializeField] private int cuantos = 7;
    [SerializeField] private Vector2 intervalo = new Vector2(0.25f, 0.55f);

    private RizoDeViento[] _rizos;
    private Vector3 _direccion = Vector3.forward;
    private float _siguiente;

    public void Configurar(Vector3 direccion)
    {
        direccion.y = 0f;
        _direccion = direccion.sqrMagnitude > 0.0001f ? direccion.normalized : Vector3.forward;
    }

    private void Awake()
    {
        _rizos = new RizoDeViento[Mathf.Max(1, cuantos)];
        for (int i = 0; i < _rizos.Length; i++)
            _rizos[i] = RizoDeViento.Crear(transform, VfxDeClima.MaterialDeRizo());
    }

    private void Update()
    {
        if (Time.time < _siguiente) return;
        _siguiente = Time.time + Random.Range(intervalo.x, intervalo.y);

        RizoDeViento libre = null;
        for (int i = 0; i < _rizos.Length; i++)
            if (!_rizos[i].Activo) { libre = _rizos[i]; break; }
        if (libre == null) return;

        Vector3 lado = Vector3.Cross(Vector3.up, _direccion).normalized;
        Vector3 origen = transform.position
            + lado * Random.Range(-7f, 7f)
            + Vector3.up * Random.Range(0.4f, 4.5f)
            - _direccion * Random.Range(1f, 5f);

        libre.Lanzar(origen, _direccion,
            Random.Range(4f, 6.5f),
            Random.Range(0.45f, 0.65f),
            Random.Range(2, 4),
            Random.Range(1.4f, 2.4f),
            Random.Range(0.025f, 0.05f));
    }
}
