using UnityEngine;

/// Pega las salpicaduras de la lluvia al suelo que hay bajo el ancla del clima. Sin esto, cuando el
/// ancla cuelga del jugador o de la cámara (cinemáticas), los aros salen a media altura.
[DisallowMultipleComponent]
public sealed class SalpicadurasDeLluvia : MonoBehaviour
{
    [SerializeField] private Transform salpicaduras;
    [Tooltip("Cada cuánto se vuelve a medir el suelo (segundos).")]
    [SerializeField] private float cada = 0.2f;
    [Tooltip("Altura máxima (m) a la que se busca el suelo bajo el ancla.")]
    [SerializeField] private float alcance = 60f;

    private LayerMask _suelo;
    private float _siguiente;

    public void Configurar(Transform objetivo) => salpicaduras = objetivo;

    void Awake()
    {
        // Floor para el terreno, Default para props, Obstacle para rocas y árboles, Water para el agua.
        _suelo = LayerMask.GetMask("Floor", "Default", "Obstacle", "Water");
    }

    void LateUpdate()
    {
        if (salpicaduras == null) return;
        if (Time.time < _siguiente) return;
        _siguiente = Time.time + cada;

        Vector3 desde = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(desde, Vector3.down, out RaycastHit golpe, alcance, _suelo, QueryTriggerInteraction.Ignore))
            salpicaduras.position = golpe.point + Vector3.up * 0.04f;
        else
            salpicaduras.position = new Vector3(transform.position.x, transform.position.y + 0.04f, transform.position.z);
    }
}
