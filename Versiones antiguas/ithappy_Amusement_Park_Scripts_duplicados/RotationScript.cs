using UnityEngine;

namespace ithappy
{
    public class RotationScript : MonoBehaviour
    {
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }

        public RotationAxis rotationAxis = RotationAxis.Y;
        public float rotationSpeed = 50.0f;

        void Update()
        {
            float rotationValue = rotationSpeed * Time.deltaTime;

            Vector3 axis = Vector3.zero;
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    axis = Vector3.right;
                    break;
                case RotationAxis.Y:
                    axis = Vector3.up;
                    break;
                case RotationAxis.Z:
                    axis = Vector3.forward;
                    break;
            }

            transform.Rotate(axis, rotationValue);
        }
    }
}
