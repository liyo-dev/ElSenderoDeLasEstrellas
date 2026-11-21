using UnityEngine;

namespace Kits.Core
{
    /// <summary>
    /// IK de dos huesos hecho a mano (hombro, codo y mano; o cadera, rodilla y pie): gira los dos
    /// primeros huesos para que el tercero llegue a un punto, doblando la articulación hacia donde ya
    /// la dobla la animación. Va después de la animación (en LateUpdate) y no necesita «IK Pass» en el
    /// Animator. Si el punto queda fuera de alcance, el miembro se estira hacia él lo que puede.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <summary>Lleva <paramref name="end"/> hacia <paramref name="target"/> con un peso de 0 (nada) a 1 (del todo).</summary>
        public static void Reach(Transform upper, Transform lower, Transform end, Vector3 target, float weight)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            Vector3 toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : (c - a).normalized;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            Vector3 bend = Vector3.ProjectOnPlane(b - a, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            Vector3 joint = a + dir * (l1 * cosA) + bend * (l1 * Mathf.Sqrt(1f - cosA * cosA));

            Quaternion upperRot = Quaternion.FromToRotation(b - a, joint - a) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, upperRot, weight);
            b = lower.position; c = end.position;
            Quaternion lowerRot = Quaternion.FromToRotation(c - b, target - b) * lower.rotation;
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerRot, weight);
        }

        /// <summary>Largo del miembro estirado (de <paramref name="upper"/> a <paramref name="end"/> pasando por <paramref name="lower"/>).</summary>
        public static float Length(Transform upper, Transform lower, Transform end)
            => upper == null || lower == null || end == null ? 0f
             : (lower.position - upper.position).magnitude + (end.position - lower.position).magnitude;
    }
}
