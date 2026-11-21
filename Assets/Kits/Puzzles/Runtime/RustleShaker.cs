using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Agita una pieza (un arbusto, una mata) como si algo pasara por dentro: cabeceo y balanceo
    /// con subida y caída suaves. Solo trabaja mientras se agita; quieta, no gasta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RustleShaker : MonoBehaviour
    {
        [Tooltip("Pieza que se agita. Vacío = este mismo objeto.")]
        [SerializeField] private Transform pieza;
        [Tooltip("Grados de giro con intensidad 1.")]
        [SerializeField, Min(0f)] private float grados = 7f;
        [Tooltip("Golpes por segundo del agitado.")]
        [SerializeField, Min(0.1f)] private float frecuencia = 9f;
        [Tooltip("Segundos de subida y de caída del agitado.")]
        [SerializeField, Min(0.01f)] private float rampa = 0.25f;

        private Quaternion _reposo;
        private bool _hayReposo;
        private float _inicio, _fin, _intensidad;
        private float _fase;

        public bool IsShaking => enabled;

        private void Awake()
        {
            if (pieza == null) pieza = transform;
            _reposo = pieza.localRotation;
            _hayReposo = true;
            _fase = (transform.position.x * 1.7f + transform.position.z * 0.9f) % 6.283f;
            enabled = false;
        }

        /// <summary>Se agita <paramref name="segundos"/> tras esperar <paramref name="retraso"/>.</summary>
        public void Shake(float intensidad, float segundos, float retraso = 0f)
        {
            if (pieza == null) return;
            float ahora = Time.time;
            _inicio = ahora + Mathf.Max(0f, retraso);
            _fin = _inicio + Mathf.Max(0.05f, segundos);
            _intensidad = Mathf.Max(0f, intensidad);
            enabled = true;
        }

        /// <summary>Deja de agitarse con una caída corta.</summary>
        public void Calm()
        {
            if (!enabled) return;
            float ahora = Time.time;
            if (_fin > ahora + rampa) _fin = ahora + rampa;
        }

        private void Update()
        {
            float ahora = Time.time;
            if (ahora >= _fin) { Parar(); return; }
            if (ahora < _inicio) return;
            float sube = Mathf.Clamp01((ahora - _inicio) / rampa);
            float baja = Mathf.Clamp01((_fin - ahora) / rampa);
            float a = grados * _intensidad * Mathf.Min(sube, baja);
            float t = (ahora - _inicio) * frecuencia * 6.283f + _fase;
            float x = Mathf.Sin(t) * a;
            float z = Mathf.Sin(t * 1.37f + 1.1f) * a * 0.8f;
            pieza.localRotation = _reposo * Quaternion.Euler(x, 0f, z);
        }

        private void Parar()
        {
            if (_hayReposo && pieza != null) pieza.localRotation = _reposo;
            enabled = false;
        }

        private void OnDisable()
        {
            if (_hayReposo && pieza != null) pieza.localRotation = _reposo;
        }
    }
}
