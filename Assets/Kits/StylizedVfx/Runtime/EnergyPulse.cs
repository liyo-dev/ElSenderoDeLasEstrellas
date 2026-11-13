using UnityEngine;

namespace Kits.StylizedVfx
{
    /// Da vida a una pieza de energía hecha con el shader «Kits/StylizedVfx/Energy»: al encenderse
    /// aparece a mordiscos (erosión), luego late (color y luz a la vez) y hace girar sus piezas, cada
    /// una sobre su eje y a su velocidad (anillos que vuelcan alrededor de un orbe, cuentas que corren).
    /// La luz acompaña el tamaño de la pieza: si el efecto crece, ilumina más lejos, pero no más fuerte.
    [DisallowMultipleComponent]
    public class EnergyPulse : MonoBehaviour
    {
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int ErosionId = Shader.PropertyToID("_Erosion");

        [Tooltip("Piezas con el shader de energía que laten y aparecen por erosión.")]
        [SerializeField] private Renderer[] energyRenderers;

        [Tooltip("Luz que late con la energía. Vacío = sin luz.")]
        [SerializeField] private Light pulseLight;

        [Tooltip("Intensidad de la luz en lo alto del latido.")]
        [SerializeField] private float lightIntensity = 2.5f;

        [Tooltip("Alcance de la luz con la pieza a escala 1.")]
        [SerializeField] private float lightRange = 2.5f;

        [Tooltip("Tope del multiplicador de alcance cuando la pieza crece.")]
        [SerializeField] private float maxRangeScale = 4f;

        [Tooltip("Segundos de un latido completo. 0 = sin latido.")]
        [SerializeField] private float pulsePeriod = 1.2f;

        [Tooltip("Cuánto baja la luz en el valle del latido (0 = nada, 1 = se apaga).")]
        [Range(0f, 1f)]
        [SerializeField] private float pulseDepth = 0.35f;

        [Tooltip("Segundos que tarda en aparecer por erosión al encenderse. 0 = aparece de golpe.")]
        [SerializeField] private float appearTime = 0.35f;

        [Tooltip("Segundos desde que se enciende hasta que empieza a irse a mordiscos. 0 = no se va solo.")]
        [SerializeField] private float disappearAfter;

        [Tooltip("Segundos que tarda en irse a mordiscos.")]
        [SerializeField] private float disappearTime = 0.3f;

        [Tooltip("Si tiene claves, la luz sigue esta curva (0-1, por segundos desde que se enciende) en vez del latido: destellos de impacto.")]
        [SerializeField] private AnimationCurve lightOverLife = new AnimationCurve();

        [Tooltip("Piezas que giran: cada una sobre su eje local y a su velocidad.")]
        [SerializeField] private Spin[] spins;

        [Tooltip("Usar tiempo sin escalar: sigue vivo durante cámaras lentas y pausas de cinemática.")]
        [SerializeField] private bool unscaledTime = true;

        private MaterialPropertyBlock _block;
        private float _time;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _time = 0f;
            // Fuera de Play (vistas previas, fotos de prefabs) la pieza se ve entera.
            Apply(0f, Application.isPlaying && appearTime > 0f ? 1f : 0f);
        }

        private void Update()
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _time += dt;

            float pulse = pulsePeriod > 0f ? 0.5f - 0.5f * Mathf.Cos(_time * (2f * Mathf.PI / pulsePeriod)) : 0f;
            float erosion = appearTime > 0f ? 1f - Mathf.Clamp01(_time / appearTime) : 0f;
            if (disappearAfter > 0f && _time > disappearAfter)
                erosion = Mathf.Max(erosion, disappearTime > 0f ? Mathf.Clamp01((_time - disappearAfter) / disappearTime) : 1f);
            Apply(pulse, erosion);

            if (spins == null) return;
            for (int i = 0; i < spins.Length; i++)
            {
                var giro = spins[i];
                if (giro.target == null) continue;
                giro.target.Rotate(giro.axis, giro.degreesPerSecond * dt, Space.Self);
            }
        }

        private void Apply(float pulse, float erosion)
        {
            if (energyRenderers != null)
            {
                for (int i = 0; i < energyRenderers.Length; i++)
                {
                    var r = energyRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetFloat(PulseId, pulse);
                    _block.SetFloat(ErosionId, erosion);
                    r.SetPropertyBlock(_block);
                }
            }

            if (pulseLight != null)
            {
                float escala = Mathf.Clamp(transform.lossyScale.x, 0.25f, maxRangeScale);
                float forma = lightOverLife != null && lightOverLife.length > 0
                    ? Mathf.Max(0f, lightOverLife.Evaluate(_time))
                    : (1f - pulseDepth + pulseDepth * pulse) * (1f - erosion);
                pulseLight.intensity = lightIntensity * forma;
                pulseLight.range = lightRange * escala;
            }
        }

        /// Monta la pieza desde código (constructores de editor).
        public void Configure(Renderer[] renderers, Light light, Spin[] spinning)
        {
            energyRenderers = renderers;
            pulseLight = light;
            spins = spinning;
        }

        /// Ajusta la luz y el ritmo desde código (constructores de editor).
        public void ConfigureTiming(float intensity, float range, float period, float depth, float appear)
        {
            lightIntensity = intensity;
            lightRange = range;
            pulsePeriod = period;
            pulseDepth = depth;
            appearTime = appear;
        }

        /// Hace que la pieza se vaya sola, a mordiscos, y opcionalmente da forma a la luz (destellos).
        public void ConfigureLifetime(float disappearAfterSeconds, float disappearSeconds, AnimationCurve lightCurve)
        {
            disappearAfter = disappearAfterSeconds;
            disappearTime = disappearSeconds;
            lightOverLife = lightCurve ?? new AnimationCurve();
        }
    }

    /// Una pieza que gira sobre un eje local a velocidad constante.
    [System.Serializable]
    public struct Spin
    {
        public Transform target;
        public Vector3 axis;
        public float degreesPerSecond;

        public Spin(Transform target, Vector3 axis, float degreesPerSecond)
        {
            this.target = target;
            this.axis = axis;
            this.degreesPerSecond = degreesPerSecond;
        }
    }
}
