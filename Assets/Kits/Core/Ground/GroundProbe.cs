using UnityEngine;
using UnityEngine.AI;

namespace Kits.Core
{
    /// <summary>
    /// Ajustes de <see cref="GroundProbe.IsOpenGround"/> y <see cref="GroundProbe.TryFindOpenGround"/>:
    /// qué cuenta como terreno abierto (sitio para pelear o para dejar a alguien a salvo).
    /// </summary>
    [System.Serializable]
    public struct OpenGroundSettings
    {
        [Tooltip("Radio que se mira alrededor del punto, en metros.")]
        public float radius;
        [Tooltip("Rumbos que se prueban alrededor del punto.")]
        public int directions;
        [Tooltip("Rumbos que tienen que estar libres (sin borde, desnivel ni agua honda) para que el sitio valga.")]
        public int minOpenDirections;
        [Tooltip("Desnivel máximo entre el punto y cada rumbo, en metros.")]
        public float maxHeightDelta;
        [Tooltip("Capas del agua (volúmenes con la superficie arriba). Vacío = no se mira el agua.")]
        public LayerMask waterMask;
        [Tooltip("Agua más honda que esto, en metros, no cuenta como suelo.")]
        public float maxWaterDepth;
        [Tooltip("Hasta dónde se busca un sitio abierto, en metros (también el camino máximo, por el doble).")]
        public float searchRadius;
        [Tooltip("Áreas del NavMesh que valen.")]
        public int areaMask;

        /// Valores para un combate a pie: 3,5 m libres en 6 de 8 rumbos y nada de agua de más de medio metro.
        public static OpenGroundSettings Default(LayerMask waterMask, float searchRadius = 20f) => new OpenGroundSettings
        {
            radius = 3.5f,
            directions = 8,
            minOpenDirections = 6,
            maxHeightDelta = 1f,
            waterMask = waterMask,
            maxWaterDepth = 0.5f,
            searchRadius = searchRadius,
            areaMask = NavMesh.AllAreas,
        };
    }

    /// <summary>
    /// Preguntas sobre el suelo que comparten varios sistemas: si hay un borde (una caída o agua) en
    /// una dirección, cómo apartar un punto del borde del NavMesh y si un sitio es terreno abierto
    /// (para pelear o para dejar a alguien a salvo). Sin allocations: buffers propios; solo desde
    /// el hilo principal.
    /// </summary>
    public static class GroundProbe
    {
        private const float ProbeStep = 0.3f;
        private const float ProbeHeight = 0.6f;
        private const float WaterProbeHeight = 4f;
        private const float RingStep = 2.5f;
        private const int RingDirections = 12;

        private static readonly RaycastHit[] s_hits = new RaycastHit[8];
        private static readonly Vector3[] s_corners = new Vector3[32];
        private static NavMeshPath s_path;

        /// <summary>
        /// Metros que se puede avanzar desde <paramref name="feet"/> hacia <paramref name="direction"/>
        /// (en el plano) sin que el suelo baje más de <paramref name="maxDrop"/> ni desaparezca. Los
        /// colliders de <paramref name="ignore"/> (el que pregunta) no cuentan como suelo; los
        /// triggers (el agua) tampoco, así que un río cuenta por su fondo.
        /// </summary>
        public static float DistanceToEdge(Vector3 feet, Vector3 direction, float maxDistance, float maxDrop, int groundMask, Transform ignore)
        {
            direction.y = 0f;
            if (maxDistance <= 0f || direction.sqrMagnitude < 0.0001f) return Mathf.Max(0f, maxDistance);
            direction.Normalize();
            float alcanzado = 0f;
            for (float d = ProbeStep; d < maxDistance + ProbeStep; d += ProbeStep)
            {
                float paso = Mathf.Min(d, maxDistance);
                Vector3 p = feet + direction * paso;
                if (!GroundHeight(p, feet.y + ProbeHeight, ProbeHeight + maxDrop, groundMask, ignore, out _))
                    return alcanzado;
                alcanzado = paso;
            }
            return maxDistance;
        }

        /// <summary>
        /// Altura del suelo más alto bajo <paramref name="point"/>, buscando desde <paramref name="fromY"/>
        /// hasta <paramref name="depth"/> metros más abajo.
        /// </summary>
        public static bool GroundHeight(Vector3 point, float fromY, float depth, int groundMask, Transform ignore, out float y)
        {
            y = 0f;
            var origen = new Vector3(point.x, fromY, point.z);
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, s_hits, depth, groundMask, QueryTriggerInteraction.Ignore);
            bool hay = false;
            float mejor = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var c = s_hits[i].collider;
                if (c == null || (ignore != null && c.transform.IsChildOf(ignore))) continue;
                if (s_hits[i].point.y > mejor) { mejor = s_hits[i].point.y; hay = true; }
            }
            if (hay) y = mejor;
            return hay;
        }

        /// <summary>
        /// Lleva <paramref name="point"/> al NavMesh y lo aparta del borde hasta <paramref name="margin"/>
        /// metros si cabe (en un puente estrecho se queda en el centro). Sin NavMesh cerca, devuelve
        /// el punto tal cual.
        /// </summary>
        public static Vector3 AwayFromNavMeshEdge(Vector3 point, float margin, float sampleRadius = 1.5f, int areaMask = NavMesh.AllAreas)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, areaMask)) return point;
            Vector3 p = hit.position;
            for (int i = 0; i < 3; i++)
            {
                if (!NavMesh.FindClosestEdge(p, out NavMeshHit borde, areaMask) || borde.distance >= margin) break;
                Vector3 fuera = p - borde.position;
                fuera.y = 0f;
                if (fuera.sqrMagnitude < 0.0001f) { fuera = borde.normal; fuera.y = 0f; }
                if (fuera.sqrMagnitude < 0.0001f) break;
                Vector3 destino = p + fuera.normalized * (margin - borde.distance + 0.05f);
                if (NavMesh.Raycast(p, destino, out NavMeshHit tope, areaMask)) destino = tope.position;
                if (!NavMesh.SamplePosition(destino, out NavMeshHit dentro, 0.5f, areaMask)) break;
                if ((dentro.position - p).sqrMagnitude < 0.0004f) break;
                p = dentro.position;
            }
            return p;
        }

        /// <summary>
        /// ¿Es <paramref name="center"/> terreno abierto? Está en el NavMesh, no tiene agua honda y, de
        /// los rumbos que se prueban a <see cref="OpenGroundSettings.radius"/>, al menos
        /// <see cref="OpenGroundSettings.minOpenDirections"/> se alcanzan en línea recta por el NavMesh,
        /// sin desnivel ni agua honda. Un puente, una cornisa o la orilla de un río no lo son.
        /// </summary>
        public static bool IsOpenGround(Vector3 center, in OpenGroundSettings s)
        {
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 1f, s.areaMask)) return false;
            Vector3 c = hit.position;
            if (DeepWater(c, s)) return false;
            int rumbos = Mathf.Max(1, s.directions);
            int libres = 0;
            for (int i = 0; i < rumbos; i++)
            {
                float a = i * (Mathf.PI * 2f / rumbos);
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.radius;
                if (NavMesh.Raycast(c, p, out NavMeshHit tope, s.areaMask) && tope.distance < s.radius - 0.1f) continue;
                if (!NavMesh.SamplePosition(p, out NavMeshHit enP, 0.6f, s.areaMask)) continue;
                if (Mathf.Abs(enP.position.y - c.y) > s.maxHeightDelta) continue;
                if (DeepWater(enP.position, s)) continue;
                libres++;
            }
            return libres >= Mathf.Min(s.minOpenDirections, rumbos);
        }

        /// <summary>
        /// Busca el terreno abierto más cercano a <paramref name="from"/> al que se llega andando (por el
        /// NavMesh, con un camino de como mucho el doble de <see cref="OpenGroundSettings.searchRadius"/>).
        /// Prueba anillos cada vez más anchos y se queda con el de camino más corto del primer anillo
        /// que tiene alguno.
        /// </summary>
        public static bool TryFindOpenGround(Vector3 from, in OpenGroundSettings s, out Vector3 result)
        {
            result = from;
            if (!NavMesh.SamplePosition(from, out NavMeshHit origen, 2f, s.areaMask)) return false;
            s_path ??= new NavMeshPath();
            float caminoMaximo = s.searchRadius * 2f;
            for (float radio = RingStep; radio <= s.searchRadius + 0.01f; radio += RingStep)
            {
                bool hay = false;
                float mejor = float.MaxValue;
                for (int i = 0; i < RingDirections; i++)
                {
                    float a = i * (Mathf.PI * 2f / RingDirections);
                    Vector3 prueba = origen.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radio;
                    if (!NavMesh.SamplePosition(prueba, out NavMeshHit enP, 1.5f, s.areaMask)) continue;
                    if (!IsOpenGround(enP.position, s)) continue;
                    if (!NavMesh.CalculatePath(origen.position, enP.position, s.areaMask, s_path) || s_path.status != NavMeshPathStatus.PathComplete) continue;
                    float largo = PathLength(s_path);
                    if (largo > caminoMaximo || largo >= mejor) continue;
                    mejor = largo;
                    result = enP.position;
                    hay = true;
                }
                if (hay) return true;
            }
            return false;
        }

        private static bool DeepWater(Vector3 point, in OpenGroundSettings s)
        {
            if (s.waterMask.value == 0) return false;
            var origen = point + Vector3.up * WaterProbeHeight;
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, s_hits, WaterProbeHeight + 0.05f, s.waterMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (s_hits[i].collider != null && s_hits[i].point.y - point.y > s.maxWaterDepth) return true;
            return false;
        }

        private static float PathLength(NavMeshPath path)
        {
            int n = path.GetCornersNonAlloc(s_corners);
            float largo = 0f;
            for (int i = 1; i < n; i++) largo += Vector3.Distance(s_corners[i - 1], s_corners[i]);
            return largo;
        }
    }
}
