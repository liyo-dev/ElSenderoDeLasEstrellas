using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Las preguntas con rayos de un cerebro de combate: si ve al objetivo, si tiene línea de fuego,
    /// si un sitio le cubre de verdad de todos los que le atacan, si un sitio es un rincón y qué
    /// hechizo del jugador le viene encima. Cada cerebro tiene el suyo (buffers propios, sin
    /// allocations al usarlo). Los personajes viven en Default como la geometría: se distinguen por
    /// <see cref="NPCSimpleAnimator"/> en la raíz (CLAUDE.md § 2).
    /// </summary>
    public sealed class SondeoDelTerreno
    {
        private const float AlturaDeOjos = 1.5f;
        private const float AlturaDePecho = 1.1f;

        private static readonly Vector3[] s_rumbos =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f),
        };

        private readonly Transform _yo;
        private readonly int _mascaraSolida;
        private readonly int _mascaraDeProyectiles;
        private readonly RaycastHit[] _impactos = new RaycastHit[24];
        private readonly Collider[] _cercanos = new Collider[32];
        private readonly Collider[] _hueco = new Collider[8];
        private readonly Collider[] _proyectiles = new Collider[16];

        public SondeoDelTerreno(Transform yo)
        {
            _yo = yo;
            // Lo sólido que para un hechizo (igual que MagicProjectile, INC-1167).
            _mascaraSolida = LayerMask.GetMask("Default", "Obstacle", "Floor", "Climb");
            _mascaraDeProyectiles = LayerMask.GetMask("PlayerProjectile", "Projectile", "ProjectilePlayer", "MagicProjectile");
        }

        public static Vector3 Ojos(Vector3 pies) => pies + Vector3.up * AlturaDeOjos;
        public static Vector3 Pecho(Vector3 pies) => pies + Vector3.up * AlturaDePecho;

        /// <summary>¿Ve al objetivo desde esos pies? Los personajes no tapan la vista.</summary>
        public bool Ve(Vector3 desdePies, Transform objetivo)
        {
            if (objetivo == null) return false;
            return PrimerObstaculo(Ojos(desdePies), Pecho(objetivo.position), objetivo, personajesTapan: false) == null;
        }

        /// <summary>
        /// ¿Puede lanzarle desde esa boca sin chocar con algo ni dar a otro personaje por el camino?
        /// Un obstáculo pegado a la boca (menos de 1,5 m) también cuenta, aunque el rayo central pase.
        /// </summary>
        public bool LineaDeFuego(Vector3 boca, Transform objetivo)
        {
            if (objetivo == null) return false;
            Vector3 destino = Pecho(objetivo.position);
            if (PrimerObstaculo(boca, destino, objetivo, personajesTapan: true) != null) return false;
            Vector3 hacia = destino - boca;
            float distancia = hacia.magnitude;
            if (distancia < 0.01f) return true;
            if (Physics.SphereCast(boca, 0.25f, hacia / distancia, out RaycastHit roce, Mathf.Min(1.5f, distancia * 0.9f), _mascaraSolida, QueryTriggerInteraction.Ignore))
                return EsDe(roce.collider, objetivo) || Ignorable(roce.collider);
            return true;
        }

        /// <summary>
        /// ¿Ese sitio le tapa de todos los que le atacan? Se comprueba a la altura del pecho y de la
        /// cabeza desde los ojos de cada uno; solo vale geometría sólida, no otro personaje.
        /// </summary>
        public bool CubreDeTodos(Vector3 pies, IReadOnlyList<Transform> amenazas)
        {
            for (int i = 0; i < amenazas.Count; i++)
            {
                var a = amenazas[i];
                if (a == null) continue;
                Vector3 ojo = Ojos(a.position);
                if (!Tapado(ojo, Pecho(pies)) || !Tapado(ojo, Ojos(pies))) return false;
            }
            return true;
        }

        /// <summary>Cuántos de 8 rayos horizontales chocan con algo sólido a menos de 'radio': 4 o más es un rincón.</summary>
        public int Encierro(Vector3 pies, float radio)
        {
            int n = 0;
            Vector3 origen = pies + Vector3.up;
            for (int i = 0; i < s_rumbos.Length; i++)
                if (Physics.Raycast(origen, s_rumbos[i], radio, _mascaraSolida, QueryTriggerInteraction.Ignore)) n++;
            return n;
        }

        /// <summary>Cabe un personaje de pie y no tiene techo encima (no es el interior de algo).</summary>
        public bool HayHueco(Vector3 pies)
        {
            const float radio = 0.45f;
            int n = Physics.OverlapCapsuleNonAlloc(pies + Vector3.up * (radio + 0.1f), pies + Vector3.up * 1.6f, radio, _hueco, _mascaraSolida, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!EsPersonaje(_hueco[i])) return false;
            return !Physics.Raycast(pies + Vector3.up * 0.1f, Vector3.up, 3f, _mascaraSolida, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Busca el mejor sitio a cubierto de todos los que le atacan, alcanzable andando, que no sea
        /// un rincón ni uno de los ya descartados en la memoria. Prefiere los cercanos y los que dejan
        /// asomarse a disparar al objetivo.
        /// </summary>
        public bool BuscarCobertura(Transform objetivo, IReadOnlyList<Transform> amenazas, float radio,
            NavMeshAgent agente, NavMeshPath camino, MemoriaDeCombate memoria, IReadOnlyList<Transform> companeros, out Vector3 sitio)
        {
            sitio = _yo.position;
            if (agente == null || !agente.enabled || !agente.isOnNavMesh || objetivo == null) return false;

            Vector3 centroDeAmenaza = objetivo.position;
            int n = Physics.OverlapSphereNonAlloc(_yo.position, radio, _cercanos, _mascaraSolida, QueryTriggerInteraction.Ignore);
            float mejor = float.MinValue;
            bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var obstaculo = _cercanos[i];
                if (obstaculo == null || obstaculo.isTrigger || EsPersonaje(obstaculo)) continue;
                Bounds b = obstaculo.bounds;
                if (b.size.x > 15f || b.size.z > 15f || b.size.y < 1.2f) continue;

                Vector3 detras = b.center - centroDeAmenaza;
                detras.y = 0f;
                if (detras.sqrMagnitude < 0.01f) continue;
                detras.Normalize();
                float borde = Mathf.Max(b.extents.x, b.extents.z);

                for (float d = 0.8f; d <= 2.6f; d += 0.6f)
                {
                    Vector3 prueba = b.center + detras * (borde + d);
                    if (!NavMesh.SamplePosition(prueba, out NavMeshHit enMalla, 1.5f, NavMesh.AllAreas)) continue;
                    Vector3 p = enMalla.position;
                    if (memoria.CoberturaDescartada(p)) continue;
                    if (!HayHueco(p) || Encierro(p, 2f) >= 5) continue;
                    if (!CubreDeTodos(p, amenazas)) continue;
                    if (!agente.CalculatePath(p, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;

                    float puntos = 20f - Vector3.Distance(_yo.position, p);
                    if (SePuedeAsomar(p, objetivo)) puntos += 6f;
                    puntos -= 4f * Apretura(p, companeros, 2.5f);
                    if (puntos > mejor) { mejor = puntos; sitio = p; hay = true; }
                    break;
                }
            }
            return hay;
        }

        /// <summary>
        /// El hechizo del jugador que antes le llegaría (en un radio), con los segundos que faltan y
        /// de dónde viene. 'id' distingue un proyectil de otro para decidir solo una vez si se esquiva.
        /// </summary>
        public bool HechizoEntrante(float radio, out float segundos, out Vector3 deDonde, out int id)
        {
            segundos = float.PositiveInfinity;
            deDonde = Vector3.zero;
            id = 0;
            Vector3 centro = Pecho(_yo.position);
            int n = Physics.OverlapSphereNonAlloc(_yo.position, radio, _proyectiles, _mascaraDeProyectiles, QueryTriggerInteraction.Collide);
            // Algunos prefabs no tienen la capa de proyectil en el collider: si el jugador acaba de
            // lanzar, se mira en todas las capas (solo entonces, que es caro).
            if (n <= 0 && LecturaDelJugador.SegundosDesdeQueLanzo < 3f)
                n = Physics.OverlapSphereNonAlloc(_yo.position, radio, _proyectiles, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _proyectiles[i];
                if (c == null) continue;
                var proyectil = c.GetComponentInParent<MagicProjectile>();
                if (proyectil == null || proyectil.transform.IsChildOf(_yo)) continue;
                Vector3 velocidad = c.attachedRigidbody != null ? c.attachedRigidbody.linearVelocity : Vector3.zero;
                if (velocidad.sqrMagnitude < 0.01f) velocidad = proyectil.transform.forward * 10f;
                Vector3 hacia = centro - proyectil.transform.position;
                float distancia = hacia.magnitude;
                float rapidez = velocidad.magnitude;
                if (distancia < 0.05f) { segundos = 0f; deDonde = -velocidad; id = proyectil.gameObject.GetHashCode(); return true; }
                if (Vector3.Dot(velocidad / rapidez, hacia / distancia) < 0.75f) continue;
                float t = distancia / Mathf.Max(1f, rapidez);
                if (t < segundos) { segundos = t; deDonde = -velocidad; id = proyectil.gameObject.GetHashCode(); }
            }
            return !float.IsPositiveInfinity(segundos);
        }

        /// <summary>Compañeros a menos de 'radio' de ese punto (para no amontonarse).</summary>
        public static int Apretura(Vector3 p, IReadOnlyList<Transform> companeros, float radio)
        {
            int n = 0;
            float r2 = radio * radio;
            for (int i = 0; i < companeros.Count; i++)
                if (companeros[i] != null && (companeros[i].position - p).sqrMagnitude < r2) n++;
            return n;
        }

        private bool SePuedeAsomar(Vector3 pies, Transform objetivo)
        {
            Vector3 hacia = objetivo.position - pies;
            hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.01f) return false;
            Vector3 lado = Vector3.Cross(Vector3.up, hacia.normalized);
            return LineaDeFuego(Ojos(pies + lado * 1.4f), objetivo) || LineaDeFuego(Ojos(pies - lado * 1.4f), objetivo);
        }

        private bool Tapado(Vector3 desde, Vector3 hasta)
        {
            Vector3 hacia = hasta - desde;
            float distancia = hacia.magnitude;
            if (distancia < 0.01f) return false;
            int n = Physics.RaycastNonAlloc(desde, hacia / distancia, _impactos, distancia, _mascaraSolida, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _impactos[i].collider;
                if (c == null || EsPersonaje(c) || Ignorable(c)) continue;
                return true;
            }
            return false;
        }

        private Collider PrimerObstaculo(Vector3 desde, Vector3 hasta, Transform objetivo, bool personajesTapan)
        {
            Vector3 hacia = hasta - desde;
            float distancia = hacia.magnitude;
            if (distancia < 0.01f) return null;
            int n = Physics.RaycastNonAlloc(desde, hacia / distancia, _impactos, distancia, ~0, QueryTriggerInteraction.Ignore);
            if (n == 0) return null;
            System.Array.Sort(_impactos, 0, n, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < n; i++)
            {
                var c = _impactos[i].collider;
                if (c == null || Ignorable(c)) continue;
                if (EsDe(c, objetivo)) return null;
                if (EsPersonaje(c) && !personajesTapan) continue;
                return c;
            }
            return null;
        }

        private bool EsDe(Collider c, Transform quien) => quien != null && (c.transform == quien || c.transform.IsChildOf(quien));

        private bool Ignorable(Collider c)
        {
            if (c.transform == _yo || c.transform.IsChildOf(_yo)) return true;
            if (c.GetComponentInParent<MagicProjectile>() != null) return true;
            if (c.GetComponentInParent<EnemyProjectile>() != null) return true;
            return c.GetComponentInParent<NPCShieldController.NPCShieldMarker>() != null;
        }

        private static bool EsPersonaje(Collider c) => c.transform.root.GetComponent<NPCSimpleAnimator>() != null;
    }
}
