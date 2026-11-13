using System;
using UnityEngine;

namespace Kits.StylizedVfx
{
    /// Marca que va tumbada en el suelo (aviso de dónde va a caer algo, círculo, sombra): se apoya en
    /// el suelo que tiene debajo y se inclina con la pendiente, para que no se meta bajo tierra en
    /// cuestas, bultos o cuando quien la pone le da una altura que no es la del suelo (los pies de
    /// alguien que salta, un punto de la NavMesh). Mide el centro y cuatro puntos del borde; si el
    /// suelo abomba, la sube lo justo para que el borde no quede enterrado.
    /// Las piezas en HorizontalBillboard (siempre horizontales en el mundo) pasan a ir tumbadas en
    /// su propio plano para poder inclinarse; solo las que salen quietas en su sitio (sin forma de
    /// emisión), que son las de una marca.
    /// Solo vuelve a medir cuando alguien la mueve.
    [DisallowMultipleComponent]
    public class GroundMark : MonoBehaviour
    {
        [Tooltip("Capas que cuentan como suelo.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("Metros por encima del punto pedido desde los que se busca el suelo.")]
        [SerializeField, Min(0.1f)] private float castFrom = 2f;

        [Tooltip("Metros por debajo del punto pedido hasta los que se busca el suelo.")]
        [SerializeField, Min(0.1f)] private float castBelow = 6f;

        [Tooltip("Altura sobre el suelo a la que queda la marca.")]
        [SerializeField, Min(0f)] private float lift = 0.06f;

        [Tooltip("Radio de la marca a tamaño completo, en metros: dónde se mide el borde.")]
        [SerializeField, Min(0f)] private float footprintRadius = 1.5f;

        [Tooltip("Inclinación máxima, en grados. Con más pendiente (una pared) se queda en esta.")]
        [SerializeField, Range(0f, 89f)] private float maxTilt = 45f;

        /// Decide si un collider es un cuerpo (personaje, criatura) y no suelo. El juego lo pone si
        /// sus personajes no se distinguen por Rigidbody o CharacterController.
        public static Func<Collider, bool> IsBody;

        private static readonly RaycastHit[] s_hits = new RaycastHit[16];
        private static readonly Vector3[] s_borde =
            { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        private Vector3 _ultimaPosicion;
        private Quaternion _ultimoGiro;
        private Quaternion _giroPedido = Quaternion.identity;
        private bool _colocada;

        public float FootprintRadius { get => footprintRadius; set => footprintRadius = Mathf.Max(0f, value); }

        /// La marca de este efecto (la añade si el prefab no la trae).
        public static GroundMark On(Transform efecto)
        {
            if (efecto == null) return null;
            var marca = efecto.GetComponent<GroundMark>();
            if (marca == null) marca = efecto.gameObject.AddComponent<GroundMark>();
            marca.Colocar();
            return marca;
        }

        /// El punto del suelo bajo 'cerca' (o 'cerca' tal cual si no lo hay).
        public static Vector3 GroundBelow(Vector3 cerca, float desdeArriba = 2f, float haciaAbajo = 6f)
            => TryFindGround(cerca, ~0, desdeArriba, haciaAbajo, out Vector3 punto, out _) ? punto : cerca;

        /// Busca el suelo bajo 'cerca': el impacto más alto del rayo hacia abajo que no sea un cuerpo.
        public static bool TryFindGround(Vector3 cerca, LayerMask capas, float desdeArriba, float haciaAbajo,
                                         out Vector3 punto, out Vector3 normal)
        {
            punto = cerca;
            normal = Vector3.up;
            Vector3 origen = cerca + Vector3.up * desdeArriba;
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, s_hits, desdeArriba + haciaAbajo, capas,
                                            QueryTriggerInteraction.Ignore);
            float masCerca = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var hit = s_hits[i];
                if (hit.distance >= masCerca || hit.distance <= 0f) continue;
                if (EsCuerpo(hit.collider)) continue;
                masCerca = hit.distance;
                punto = hit.point;
                normal = hit.normal;
            }
            return !float.IsPositiveInfinity(masCerca);
        }

        private static bool EsCuerpo(Collider c)
        {
            if (c.attachedRigidbody != null || c is CharacterController) return true;
            return IsBody != null && IsBody(c);
        }

        private void Awake()
        {
            foreach (var render in GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (render.renderMode != ParticleSystemRenderMode.HorizontalBillboard) continue;
                var ps = render.GetComponent<ParticleSystem>();
                if (ps != null && ps.shape.enabled) continue;
                render.renderMode = ParticleSystemRenderMode.Billboard;
                render.alignment = ParticleSystemRenderSpace.Local;
                render.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * render.transform.localRotation;
            }
        }

        private void OnDisable() => _colocada = false;

        private void LateUpdate() => Colocar();

        /// Vuelve a apoyarla si alguien la ha movido o girado desde la última vez.
        public void Colocar()
        {
            var t = transform;
            if (_colocada && t.position == _ultimaPosicion && t.rotation == _ultimoGiro) return;
            if (!_colocada || t.rotation != _ultimoGiro) _giroPedido = t.rotation;

            Vector3 pedido = t.position;
            if (!TryFindGround(pedido, groundLayers, castFrom, castBelow, out Vector3 centro, out Vector3 normal))
            {
                _colocada = true;
                _ultimaPosicion = t.position;
                _ultimoGiro = t.rotation;
                return;
            }

            // Plano del suelo con los cuatro puntos del borde; si no se encuentran, el del centro.
            Vector3 n = normal;
            float subir = 0f;
            if (footprintRadius > 0.01f)
            {
                Vector3 norte = default, sur = default, este = default, oeste = default;
                bool todos = true;
                for (int i = 0; i < s_borde.Length; i++)
                {
                    Vector3 p = pedido + s_borde[i] * footprintRadius;
                    if (!TryFindGround(p, groundLayers, castFrom, castBelow, out Vector3 enSuelo, out _))
                    {
                        todos = false;
                        continue;
                    }
                    switch (i)
                    {
                        case 0: norte = enSuelo; break;
                        case 1: sur = enSuelo; break;
                        case 2: este = enSuelo; break;
                        default: oeste = enSuelo; break;
                    }
                }
                if (todos)
                {
                    Vector3 plano = Vector3.Cross(norte - sur, este - oeste);
                    if (plano.y < 0f) plano = -plano;
                    if (plano.sqrMagnitude > 1e-6f) n = plano.normalized;
                }
                n = LimitarInclinacion(n);
                if (todos)
                {
                    subir = Mathf.Max(subir, Vector3.Dot(norte - centro, n));
                    subir = Mathf.Max(subir, Vector3.Dot(sur - centro, n));
                    subir = Mathf.Max(subir, Vector3.Dot(este - centro, n));
                    subir = Mathf.Max(subir, Vector3.Dot(oeste - centro, n));
                }
            }
            else n = LimitarInclinacion(n);

            Quaternion giro = Quaternion.FromToRotation(Vector3.up, n) * _giroPedido;
            t.SetPositionAndRotation(centro + n * (lift + subir), giro);
            _colocada = true;
            _ultimaPosicion = t.position;
            _ultimoGiro = t.rotation;
        }

        private Vector3 LimitarInclinacion(Vector3 n)
        {
            float angulo = Vector3.Angle(Vector3.up, n);
            if (angulo <= maxTilt) return n;
            return Vector3.Slerp(Vector3.up, n, maxTilt / angulo).normalized;
        }
    }
}
