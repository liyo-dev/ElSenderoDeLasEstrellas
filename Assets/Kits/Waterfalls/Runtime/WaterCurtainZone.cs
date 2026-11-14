using UnityEngine;
using UnityEngine.Events;

namespace Kits.Waterfalls
{
    /// Zona de una cortina de agua que se puede atravesar: al entrar o salir alguien que cumpla el
    /// filtro, salta espuma donde toca la cortina y se avisa por eventos (mojar al personaje, sonido…).
    /// Necesita un collider en modo trigger en el mismo objeto.
    [RequireComponent(typeof(Collider))]
    public class WaterCurtainZone : MonoBehaviour
    {
        [System.Serializable] public class CurtainEvent : UnityEvent<Collider> { }

        [Tooltip("Capas que cuentan (por defecto, todas).")]
        [SerializeField] private LayerMask bodies = ~0;

        [Tooltip("Si no está vacía, solo cuenta quien tenga esta etiqueta (o su raíz).")]
        [SerializeField] private string requiredTag = "";

        [Tooltip("Salpicadura que salta en el punto de la cortina por el que se pasa (opcional).")]
        [SerializeField] private ParticleSystem splash;

        [Tooltip("Gotas que suelta la salpicadura al pasar.")]
        [SerializeField, Min(0)] private int splashCount = 24;

        public CurtainEvent onEnter = new CurtainEvent();
        public CurtainEvent onExit = new CurtainEvent();

        private Collider _zone;

        private void Awake() => _zone = GetComponent<Collider>();

        public void Configure(ParticleSystem splashSystem, LayerMask layers, string tagFilter)
        {
            splash = splashSystem;
            bodies = layers;
            requiredTag = tagFilter ?? "";
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Counts(other)) return;
            Splash(other);
            onEnter.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Counts(other)) return;
            Splash(other);
            onExit.Invoke(other);
        }

        private bool Counts(Collider other)
        {
            if ((bodies.value & (1 << other.gameObject.layer)) == 0) return false;
            if (string.IsNullOrEmpty(requiredTag)) return true;
            return other.CompareTag(requiredTag) || other.transform.root.CompareTag(requiredTag);
        }

        private void Splash(Collider other)
        {
            if (splash == null || splashCount <= 0) return;
            if (_zone == null) _zone = GetComponent<Collider>();
            Vector3 p = _zone.ClosestPoint(other.bounds.center);
            var emit = new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true };
            splash.Emit(emit, splashCount);
        }
    }
}
