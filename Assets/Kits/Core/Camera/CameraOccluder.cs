using System.Collections.Generic;
using UnityEngine;

namespace Kits.Core
{
    /// <summary>
    /// Marca algo que tapa la vista aunque no tenga colliders (un carro que se mueve sin chocar, un
    /// decorado solo visual): las cámaras que buscan un ángulo despejado lo tienen en cuenta con la
    /// caja de sus renderers, y la red de seguridad que oculta lo que se cruza delante puede
    /// ocultarlo. Sin allocations al consultarlo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraOccluder : MonoBehaviour
    {
        private static readonly List<CameraOccluder> s_active = new List<CameraOccluder>();
        private Renderer[] _renderers;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();
#endif

        /// <summary>Los que están encendidos ahora.</summary>
        public static IReadOnlyList<CameraOccluder> Active => s_active;

        private void Awake() => _renderers = GetComponentsInChildren<Renderer>(true);

        private void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        private void OnDisable() => s_active.Remove(this);

        /// <summary>¿Corta el segmento de <paramref name="from"/> a <paramref name="to"/>, con
        /// <paramref name="radius"/> metros de holgura, alguno de sus renderers visibles?</summary>
        public bool Blocks(Vector3 from, Vector3 to, float radius)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.0001f || _renderers == null) return false;
            var ray = new Ray(from, d / length);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                b.Expand(radius * 2f);
                if (b.Contains(from)) return true;
                if (b.IntersectRay(ray, out float distance) && distance <= length) return true;
            }
            return false;
        }

        /// <summary>¿Lo corta alguno de los encendidos, salvo los que cuelgan de <paramref name="ignore"/>?</summary>
        public static bool AnyBlocks(Vector3 from, Vector3 to, float radius, Transform ignore = null)
        {
            for (int i = 0; i < s_active.Count; i++)
            {
                var o = s_active[i];
                if (o == null || (ignore != null && o.transform.IsChildOf(ignore))) continue;
                if (o.Blocks(from, to, radius)) return true;
            }
            return false;
        }
    }
}
