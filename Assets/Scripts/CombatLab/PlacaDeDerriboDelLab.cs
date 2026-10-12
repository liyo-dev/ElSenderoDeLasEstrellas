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
    [Tooltip("Lanzamiento y derribo que aplica la placa para practicar la recuperación en el aire.")]
    [SerializeField] private DerriboDelJugador derribo = DerriboDelJugador.Estandar;
    [Tooltip("Segundos sin volver a lanzar tras un derribo (el jugador tiene varios colliders y se levanta encima).")]
    [SerializeField, Min(0f)] private float pausa = 3f;

    private float _siguiente;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < _siguiente || !other.CompareTag(GameTags.Player)) return;
        // El golpe viene de delante de la placa: el jugador sale despedido hacia atrás.
        var cuerpo = other.GetComponentInParent<Invector.vCharacterController.vThirdPersonController>();
        if (cuerpo == null) return;
        Vector3 origen = cuerpo.transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (derribo.Aplicar(other.gameObject, origen))
            _siguiente = Time.time + pausa;
    }
}
