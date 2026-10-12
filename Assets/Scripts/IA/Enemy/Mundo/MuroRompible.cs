using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>Algo que se rompe cuando lo embiste un enemigo (o cualquier golpe de embestida).</summary>
public interface IRompiblePorEmbestida
{
    void RecibirEmbestida(Vector3 punto, Vector3 direccion, GameObject quien);
}

/// <summary>
/// Muro de piezas que se viene abajo cuando algo lo embiste (la Roca del mundo abierto): cada
/// pieza (los hijos con collider) sale despedida y desaparece. Mientras está en pie, su
/// NavMeshObstacle (si lo tiene) corta el NavMesh; al romperse lo libera. Ver INC-722.
/// </summary>
[DisallowMultipleComponent]
public sealed class MuroRompible : MonoBehaviour, IRompiblePorEmbestida
{
    [Tooltip("Velocidad con la que salen despedidas las piezas.")]
    [SerializeField, Min(0f)] private float empuje = 7f;
    [Tooltip("Segundos hasta que las piezas desaparecen.")]
    [SerializeField, Min(0.2f)] private float segundosDePiezas = 2.5f;
    [SerializeField] private GameObject vfxRotura;
    [SerializeField] private string sfxRotura;
    [Tooltip("Al romperse (para abrir paso a algo, un puzle…).")]
    public UnityEvent alRomperse;

    public bool Roto { get; private set; }

    public void RecibirEmbestida(Vector3 punto, Vector3 direccion, GameObject quien)
    {
        if (Roto) return;
        Roto = true;
        var obstaculo = GetComponent<NavMeshObstacle>();
        if (obstaculo != null) obstaculo.enabled = false;
        foreach (var propio in GetComponents<Collider>()) propio.enabled = false;

        direccion.y = 0f;
        direccion = direccion.sqrMagnitude > 0.001f ? direccion.normalized : transform.forward;
        foreach (Transform pieza in transform)
        {
            if (!pieza.TryGetComponent(out Collider _)) continue;
            if (!pieza.TryGetComponent(out Rigidbody rb)) rb = pieza.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = 2f;
            Vector3 lateral = pieza.position - punto;
            lateral.y = 0f;
            Vector3 salida = direccion * empuje + lateral.normalized * (empuje * 0.3f) + Vector3.up * (empuje * 0.4f);
            rb.AddForce(salida * Random.Range(0.7f, 1.2f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * empuje, ForceMode.VelocityChange);
            Destroy(pieza.gameObject, segundosDePiezas * Random.Range(0.8f, 1.2f));
        }
        if (vfxRotura != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRotura, punto, Quaternion.identity, 2.5f);
        if (!string.IsNullOrEmpty(sfxRotura) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxRotura, 1f, punto);
        alRomperse.Invoke();
    }
}
