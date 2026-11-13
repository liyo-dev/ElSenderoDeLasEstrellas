using UnityEngine;

namespace Kits.StylizedVfx
{
    /// Mantiene su pieza pegada al suelo, justo debajo de donde esté: para efectos que viajan
    /// (un proyectil que vuela a la altura de la mano) pero dejan su rastro por el suelo (grietas
    /// que corren, surcos). Toma el impacto más lejano del rayo hacia abajo, que es el suelo aunque
    /// el rayo roce antes a un personaje.
    [DisallowMultipleComponent]
    public class StickToGround : MonoBehaviour
    {
        [Tooltip("Capas que cuentan como suelo.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("Metros por encima de la pieza desde los que se lanza el rayo.")]
        [SerializeField] private float castFrom = 0.5f;

        [Tooltip("Metros que baja el rayo como mucho.")]
        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Altura sobre el suelo a la que se queda la pieza.")]
        [SerializeField] private float lift = 0.02f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Transform _parent;

        private void Awake()
        {
            _parent = transform.parent;
        }

        private void LateUpdate()
        {
            Vector3 origen = (_parent != null ? _parent.position : transform.position) + Vector3.up * castFrom;
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, _hits, maxDistance + castFrom, groundLayers,
                                            QueryTriggerInteraction.Ignore);
            if (n == 0) return;
            float lejos = -1f;
            Vector3 punto = default;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].distance <= lejos) continue;
                lejos = _hits[i].distance;
                punto = _hits[i].point;
            }
            transform.SetPositionAndRotation(punto + Vector3.up * lift, Quaternion.identity);
        }
    }
}
