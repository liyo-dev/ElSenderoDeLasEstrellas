using System;
using UnityEngine;

namespace Kits.Core
{
    /// <summary>
    /// Hace girar las ruedas de algo que se mueve (un carro, una carretilla, un juguete) según lo que
    /// avanza de verdad: cada rueda gira sobre su eje lo que corresponde a la distancia recorrida en su
    /// dirección de rodadura (ángulo = distancia / radio). No sabe quién mueve el objeto: vale igual si lo
    /// lleva un personaje, una cinemática o la física. Hacia atrás, giran al revés; de lado, no giran.
    /// Las ruedas tienen que ser objetos propios con el pivote en su centro (ver la herramienta
    /// «Separar ruedas» de Kits.EditorTools, que las saca de una malla de una pieza).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WheelSpin : MonoBehaviour
    {
        [Serializable]
        public struct Wheel
        {
            [Tooltip("La rueda: un objeto con el pivote en el centro del eje.")]
            public Transform transform;
            [Tooltip("Radio de la rueda en el espacio local de su padre (antes de escalar).")]
            public float radius;
            [Tooltip("Eje de giro en el espacio local de la rueda (p. ej. X si el eje cruza el carro de lado a lado).")]
            public Vector3 axle;
        }

        [SerializeField] private Wheel[] wheels = new Wheel[0];
        [Tooltip("Un salto mayor que esto en un fotograma, en metros, es una recolocación: las ruedas no giran por él.")]
        [SerializeField, Min(0.1f)] private float teleportDistance = 3f;

        private Quaternion[] _baseRotation;
        private float[] _angle;
        private Vector3 _lastPosition;

        public void Configure(Wheel[] newWheels)
        {
            wheels = newWheels ?? new Wheel[0];
            Prepare();
        }

        private void Awake() => Prepare();

        private void OnEnable() => _lastPosition = transform.position;

        private void Prepare()
        {
            int n = wheels.Length;
            _baseRotation = new Quaternion[n];
            _angle = new float[n];
            for (int i = 0; i < n; i++)
                _baseRotation[i] = wheels[i].transform != null ? wheels[i].transform.localRotation : Quaternion.identity;
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            Vector3 now = transform.position;
            Vector3 delta = now - _lastPosition;
            _lastPosition = now;
            float moved = delta.sqrMagnitude;
            if (moved < 1e-10f || moved > teleportDistance * teleportDistance) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                Transform t = wheels[i].transform;
                if (t == null || wheels[i].radius <= 0f) continue;
                Vector3 axleLocal = wheels[i].axle.sqrMagnitude > 0f ? wheels[i].axle.normalized : Vector3.right;
                Vector3 axleWorld = t.parent != null ? t.parent.TransformDirection(_baseRotation[i] * axleLocal) : _baseRotation[i] * axleLocal;
                // Dirección en la que rueda: perpendicular al eje, sobre el suelo.
                Vector3 roll = Vector3.Cross(axleWorld, Vector3.up);
                if (roll.sqrMagnitude < 1e-6f) continue;
                roll.Normalize();
                float scale = t.parent != null ? t.parent.lossyScale.y : 1f;
                float radius = wheels[i].radius * Mathf.Abs(scale);
                if (radius < 1e-4f) continue;
                _angle[i] = Mathf.Repeat(_angle[i] + Vector3.Dot(delta, roll) / radius * Mathf.Rad2Deg, 360f);
                t.localRotation = _baseRotation[i] * Quaternion.AngleAxis(_angle[i], axleLocal);
            }
        }
    }
}
