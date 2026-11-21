using UnityEngine;

/// <summary>
/// Placa de la zona de lucha del LAB: al pisarla empieza su oleada en <see cref="PeleaContraNpcsDelLab"/>.
/// Solo para escenas de prueba. Ver INC-1357.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PlacaDeOleadaDelLab : MonoBehaviour
{
    [SerializeField] private PeleaContraNpcsDelLab zona;
    [SerializeField, Min(0)] private int oleada;
    [Tooltip("Segundos sin volver a empezar tras pisarla (el jugador tiene varios colliders).")]
    [SerializeField, Min(0f)] private float pausa = 3f;

    private float _siguiente;

    public void Configurar(PeleaContraNpcsDelLab deZona, int indice)
    {
        zona = deZona;
        oleada = Mathf.Max(0, indice);
    }

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (zona == null || Time.time < _siguiente || !other.CompareTag(GameTags.Player)) return;
        _siguiente = Time.time + pausa;
        zona.Empezar(oleada);
    }
}
