using UnityEngine;

/// <summary>
/// Placa del laboratorio: al pisarla da una cantidad de un objeto (monedas, Esencia…) para probar
/// tiendas sin tener que conseguirlo antes. Solo para escenas de prueba.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PlacaDeRecursosDelLab : MonoBehaviour
{
    [SerializeField] private ItemData objeto;
    [SerializeField, Min(1)] private int cantidad = 100;
    [Tooltip("Segundos sin volver a dar nada tras pisarla (el jugador tiene varios colliders).")]
    [SerializeField, Min(0f)] private float pausa = 1.5f;

    private float _siguiente;

    public void Configurar(ItemData item, int cuanto)
    {
        objeto = item;
        cantidad = Mathf.Max(1, cuanto);
    }

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (objeto == null || Time.time < _siguiente || !other.CompareTag(GameTags.Player)) return;
        if (!PlayerService.TryGetComponent(out Inventory inventario, allowSceneLookup: false)) return;
        _siguiente = Time.time + pausa;
        inventario.Add(objeto, cantidad);
    }
}
