using UnityEngine;

namespace ithappy
{
    public class OscillateScale : MonoBehaviour
    {
        public Vector3 scaleAxis = Vector3.one;
        public float scaleFactor = 2f;
        public float duration = 2f;
        public bool useRandomDelay = false; // Toggle random delay
        public float maxRandomDelay = 1f; // Maximum random delay

        private Vector3 startScale;
        private float timeElapsed = 0f;
        private bool isReversing = false;
        private float randomDelay = 0f;

        void Start()
        {
            startScale = transform.localScale;

            if (useRandomDelay)
            {
                randomDelay = Random.Range(0f, maxRandomDelay);
            }
        }

        void Update()
        {
            if (timeElapsed < randomDelay)
            {
                timeElapsed += Time.deltaTime;
                return;
            }

            float progress = (timeElapsed - randomDelay) / (duration / 2f);
            progress = Mathf.Clamp01(progress);

            progress = EaseInOut(progress);

            Vector3 currentScale = startScale + scaleAxis.normalized * (scaleFactor - 1) * (isReversing ? (1 - progress) : progress);

            transform.localScale = currentScale;

            timeElapsed += Time.deltaTime;

            if (timeElapsed >= duration / 2f + randomDelay)
            {
                timeElapsed = randomDelay;
                isReversing = !isReversing;
            }
        }

        private float EaseInOut(float t)
        {
            return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        }
    }
}
