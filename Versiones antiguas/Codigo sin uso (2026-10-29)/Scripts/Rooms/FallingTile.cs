using UnityEngine;

public class FallingTile : MonoBehaviour
{
    public float delay = 0.4f;
    public float respawnAfter = -1f; // <0 = no vuelve
    public string playerTag = "Player";
    public Rigidbody rb;

    Vector3 startPos; Quaternion startRot;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
        if (rb) rb.isKinematic = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            Invoke(nameof(Drop), delay);
    }

    void Drop()
    {
        if (!rb) return;
        rb.isKinematic = false;
        if (respawnAfter > 0f) Invoke(nameof(Respawn), respawnAfter);
    }

    void Respawn()
    {
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPos, startRot);
    }
}
