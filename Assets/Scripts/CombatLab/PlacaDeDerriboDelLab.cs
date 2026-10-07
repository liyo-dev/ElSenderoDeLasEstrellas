using UnityEngine;

/// <summary>
/// Placa del laboratorio que lanza y derriba al jugador como un golpe fuerte de jefe
/// (<see cref="AerialKnockbackReceiver.Derribar"/>), para practicar la recuperación en el aire
/// (saltar durante el vuelo) sin tener que pelear con el Gólem. Solo para escenas de prueba.
/// Ver INC-654.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PlacaDeDerriboDelLab : MonoBehaviour
{
    [Tooltip("Metros que sale despedido el jugador (hacia atrás de la placa, su eje Z azul).")]
    [SerializeField, Min(0f)] private float distancia = 4f;
    [Tooltip("Altura del vuelo, en metros.")]
    [SerializeField, Min(0f)] private float altura = 2f;
    [Tooltip("Segundos que dura el vuelo.")]
    [SerializeField, Min(0.1f)] private float duracion = 0.7f;
    [Tooltip("Segundos sin volver a lanzar tras un derribo (el jugador tiene varios colliders y se levanta encima).")]
    [SerializeField, Min(0f)] private float pausa = 3f;

    private float _siguiente;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < _siguiente || !other.CompareTag(GameTags.Player)) return;
        // El golpe viene de delante de la placa: el jugador sale despedido hacia atrás.
        Vector3 origen = other.transform.position + transform.forward;
        if (AerialKnockbackReceiver.Derribar(other.gameObject, origen, distancia, altura, duracion))
            _siguiente = Time.time + pausa;
    }
}
